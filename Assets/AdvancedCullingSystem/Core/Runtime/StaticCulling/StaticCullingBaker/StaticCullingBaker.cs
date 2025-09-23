using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;

#if COLLECTIONS_1_3_1_OR_NEWER

// 针对新版 Unity Collections，使用 NativeParallelHashMap
using NativeHashMap_Int_UnsafeListInt =
    Unity.Collections.NativeParallelHashMap<int, Unity.Collections.LowLevel.Unsafe.UnsafeList<int>>;

#else

// 针对旧版 Unity Collections，使用 NativeHashMap
using NativeHashMap_Int_UnsafeListInt =
    Unity.Collections.NativeHashMap<int, Unity.Collections.LowLevel.Unsafe.UnsafeList<int>>;

#endif

namespace NGS.AdvancedCullingSystem.Static
{
    /// <summary>
    /// Static Culling 烘焙器（Baker）
    /// 负责在编辑期或离线阶段，根据 GeometryTree 与 VisibilityTree，
    /// 计算每个可见性单元（Cell）能够“看到”的 CullingTarget 集合，
    /// 并将结果写入 VisibilityTree 中。
    /// </summary>
    public partial class StaticCullingBaker : IDisposable
    {
        /// <summary>
        /// 同时运行的最大可见性计算进程数量
        /// 用于限制并发，避免占用过多 CPU 资源
        /// </summary>
        private const int MAX_PROCESSES = 50;

        /// <summary>
        /// 单次可见性计算中允许的最大射线数量
        /// 用于防止极端场景下射线数量失控
        /// </summary>
        private const int COMMANDS_LIMIT = 100000;

        /// <summary>
        /// 场景几何空间树（空间索引）
        /// </summary>
        private GeometryTree _geometryTree;

        /// <summary>
        /// 所有参与剔除的目标集合
        /// </summary>
        private IReadOnlyList<CullingTarget> _cullingTargets;

        /// <summary>
        /// 几何树节点的 Native 数据结构表示
        /// 用于 Job / Burst 中的高性能访问
        /// </summary>
        private NativeArray<GeometryNodeStruct> _geometryTreeStruct;

        /// <summary>
        /// 剔除目标的 Native 数据结构表示
        /// </summary>
        private NativeArray<CullingTargetStruct> _cullingTargetsStruct;

        /// <summary>
        /// CellIndex → CullingTargetIndex 列表 的映射
        /// 表示某个几何 Cell 中包含哪些剔除目标
        /// </summary>
        private NativeHashMap_Int_UnsafeListInt _cellTargetsMap;

        /// <summary>
        /// 从几何树的哪一层开始进行可见性计算
        /// </summary>
        private int _startDepth;

        /// <summary>
        /// 单位长度发射的射线数量
        /// </summary>
        private float _raysPerUnit;

        /// <summary>
        /// 单个可见性源允许的最大射线数量
        /// </summary>
        private int _maxRays;

        /// <summary>
        /// 最大射线检测距离
        /// </summary>
        private float _maxDistance;

        /// <summary>
        /// 射线检测使用的 LayerMask
        /// </summary>
        private int _layerMask;

        /// <summary>
        /// 当前正在执行的可见性计算进程列表
        /// </summary>
        private List<VisibilityComputingProcess> _processes;

        /// <summary>
        /// 构造函数
        /// 根据 GeometryTree 构建用于 Job 系统的原生数据结构
        /// </summary>
        public StaticCullingBaker(GeometryTree geometryTree)
        {
            int index = 0;

            _geometryTree = geometryTree;

            // 为几何树节点创建 NativeArray
            _geometryTreeStruct =
                new NativeArray<GeometryNodeStruct>(_geometryTree.NodesCount, Allocator.Persistent);

            // 递归填充几何树结构数据
            FillGeometryTreeStruct((GeometryTreeNode)_geometryTree.Root, -1, false, ref index);

            // 缓存剔除目标列表
            _cullingTargets = geometryTree.CullingTargets;

            // 创建剔除目标结构体数组
            _cullingTargetsStruct =
                new NativeArray<CullingTargetStruct>(_cullingTargets.Count, Allocator.Persistent);

            // 创建 Cell → Targets 映射
            _cellTargetsMap =
                new NativeHashMap_Int_UnsafeListInt(_geometryTreeStruct.Length, Allocator.Persistent);

            index = 0;

            // 建立 CullingTarget → Index 的映射
            FillTargetsIndexesDic(out Dictionary<CullingTarget, int> targetsIndexes);

            // 填充每个几何 Cell 中包含的目标索引
            FillCellTargetsMap((GeometryTreeNode)_geometryTree.Root, targetsIndexes, ref index);

            // 默认从第 7 层开始计算可见性
            _startDepth = 7;

            // 最大检测距离取整个几何树的包围盒尺寸
            _maxDistance = _geometryTree.Root.Bounds.size.magnitude;

            // 射线检测使用的 Layer
            _layerMask = LayerMask.GetMask(StaticCullingPreferences.LayerName);

            _processes = new List<VisibilityComputingProcess>();
        }

        /// <summary>
        /// 执行静态可见性烘焙
        /// </summary>
        /// <param name="visibilityTree">可见性树</param>
        /// <param name="raysPerUnit">单位长度射线数</param>
        /// <param name="maxRaysPerSource">单源最大射线数</param>
        /// <param name="error">错误信息</param>
        /// <returns>是否成功完成烘焙</returns>
        public bool Bake(
            VisibilityTree visibilityTree,
            float raysPerUnit,
            int maxRaysPerSource,
            out string error)
        {
            error = "";
            bool success = false;

            _raysPerUnit = raysPerUnit;
            _maxRays = maxRaysPerSource;

            try
            {
                _processes.Clear();

                // 收集所有可见性 Cell（叶子节点）
                List<VisibilityTreeNode> cells = new List<VisibilityTreeNode>();
                FillVisibilityCells((VisibilityTreeNode)visibilityTree.Root, cells);

                int current = 0;
                int finishedCount = 0;
                int cellsCount = cells.Count;

                string title = "Processing...";

                // 主循环：调度并更新可见性计算进程
                while (finishedCount < cellsCount)
                {
#if UNITY_EDITOR
                    string info = string.Format(
                        "Finished : {0} / {1}", finishedCount, cellsCount);

                    float progress = (float)finishedCount / cellsCount;

                    // 编辑器进度条，可取消
                    if (UnityEditor.EditorUtility.DisplayCancelableProgressBar(
                        title, info, progress))
                    {
                        error = "Cancelled";
                        break;
                    }
#endif
                    // 启动新的计算进程（受 MAX_PROCESSES 限制）
                    while (_processes.Count < MAX_PROCESSES && current < cellsCount)
                    {
                        _processes.Add(
                            new VisibilityComputingProcess(cells[current], this));
                        current++;
                    }

                    // 更新已有的计算进程
                    int i = 0;
                    while (i < _processes.Count)
                    {
                        VisibilityComputingProcess process = _processes[i];

                        process.Update(out bool finished);

                        if (finished)
                        {
                            // 计算完成，应用结果并释放资源
                            process.ApplyData();
                            process.Dispose();
                            _processes.RemoveAt(i);
                            finishedCount++;
                        }
                        else
                        {
                            i++;
                        }
                    }
                }

                success = finishedCount >= cellsCount;
            }
            catch (Exception ex)
            {
                error = ex.Message + "\n" + ex.StackTrace;
            }

            // 烘焙成功，写回 VisibilityTree
            if (success)
            {
                visibilityTree.SetTargets(_geometryTree.CullingTargets.ToArray());
                visibilityTree.Optimize();
                visibilityTree.Apply();
            }
            else
            {
                // 失败时清理所有进程
                foreach (var process in _processes)
                    process.Dispose();

                _processes.Clear();
            }

#if UNITY_EDITOR
            UnityEditor.EditorUtility.ClearProgressBar();
#endif

            return success;
        }

        /// <summary>
        /// 释放所有 Native 资源
        /// </summary>
        public void Dispose()
        {
            foreach (var process in _processes)
                process.Dispose();

            _processes.Clear();

            if (_geometryTreeStruct.IsCreated)
                _geometryTreeStruct.Dispose();

            if (_cullingTargetsStruct.IsCreated)
                _cullingTargetsStruct.Dispose();

            if (_cellTargetsMap.IsCreated)
            {
#if COLLECTIONS_1_3_1_OR_NEWER
                foreach (var pair in _cellTargetsMap)
                    pair.Value.Dispose();
#else
                using (var keys = _cellTargetsMap.GetKeyArray(Allocator.Temp))
                {
                    foreach (var key in keys)
                        _cellTargetsMap[key].Dispose();
                }
#endif
                _cellTargetsMap.Dispose();
            }
        }

        /// <summary>
        /// 将 GeometryTree 转换为线性数组结构，方便 Job 访问
        /// </summary>
        private void FillGeometryTreeStruct(
            GeometryTreeNode current,
            int parentIndex,
            bool isLeft,
            ref int index)
        {
            GeometryNodeStruct nodeStruct = new GeometryNodeStruct()
            {
                index = index,
                bounds = current.Bounds,
                left = -1,
                right = -1,
                isEmpty = current.IsEmpty,
                isLeaf = current.IsLeaf,
            };

            // 设置父节点对子节点的引用
            if (parentIndex >= 0)
            {
                GeometryNodeStruct parent = _geometryTreeStruct[parentIndex];

                if (isLeft)
                    parent.left = index;
                else
                    parent.right = index;

                _geometryTreeStruct[parentIndex] = parent;
            }

            _geometryTreeStruct[index++] = nodeStruct;

            // 递归处理子节点
            if (current.HasChilds)
            {
                FillGeometryTreeStruct(current.Left, nodeStruct.index, true, ref index);
                FillGeometryTreeStruct(current.Right, nodeStruct.index, false, ref index);
            }
        }

        /// <summary>
        /// 构建 CullingTarget → Index 的映射字典
        /// 同时填充 Native 目标结构数组
        /// </summary>
        private void FillTargetsIndexesDic(
            out Dictionary<CullingTarget, int> targetsIndexes)
        {
            targetsIndexes = new Dictionary<CullingTarget, int>();

            for (int i = 0; i < _cullingTargets.Count; i++)
            {
                _cullingTargetsStruct[i] = new CullingTargetStruct
                {
                    index = i,
                    bounds = _cullingTargets[i].Bounds
                };

                targetsIndexes.Add(_cullingTargets[i], i);
            }
        }

        /// <summary>
        /// 为每个几何 Cell 构建其包含的 CullingTarget 索引列表
        /// </summary>
        private void FillCellTargetsMap(
            GeometryTreeNode current,
            Dictionary<CullingTarget, int> targetToIndexDic,
            ref int index)
        {
            int nodeIndex = index;
            index++;

            if (current.IsLeaf)
            {
                UnsafeList<int> targets;

                // 叶子节点中没有目标
                if (current.CullingTargets == null || current.CullingTargets.Count == 0)
                {
                    targets = new UnsafeList<int>(0, Allocator.Persistent);
                }
                else
                {
                    // 将 CullingTarget 转换为索引存储
                    targets =
                        new UnsafeList<int>(current.CullingTargets.Count, Allocator.Persistent);

                    foreach (var target in current.CullingTargets)
                        targets.Add(targetToIndexDic[target]);
                }

                _cellTargetsMap.Add(nodeIndex, targets);
            }
            else if (current.HasChilds)
            {
                FillCellTargetsMap(current.Left, targetToIndexDic, ref index);
                FillCellTargetsMap(current.Right, targetToIndexDic, ref index);
            }
        }

        /// <summary>
        /// 收集 VisibilityTree 中的所有叶子节点（可见性 Cell）
        /// </summary>
        private void FillVisibilityCells(
            VisibilityTreeNode current,
            List<VisibilityTreeNode> result)
        {
            if (current.IsLeaf)
            {
                result.Add(current);
            }
            else if (current.HasChilds)
            {
                FillVisibilityCells(current.Left, result);
                FillVisibilityCells(current.Right, result);
            }
        }
    }
}
