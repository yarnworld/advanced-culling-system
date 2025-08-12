using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;

// 根据 Collections 版本，决定使用 NativeHashMap 还是 NativeParallelHashMap
#if COLLECTIONS_1_3_1_OR_NEWER

// GeometryNodeIndex -> UnsafeList<CullingTargetIndex>
using NativeHashMap_Int_UnsafeListInt = Unity.Collections.NativeParallelHashMap<int, Unity.Collections.LowLevel.Unsafe.UnsafeList<int>>;

#else

using NativeHashMap_Int_UnsafeListInt = Unity.Collections.NativeHashMap<int, Unity.Collections.LowLevel.Unsafe.UnsafeList<int>>;

#endif

namespace NGS.AdvancedCullingSystem.Static
{
    public partial class StaticCullingBaker
    {
        /// <summary>
        /// 单个 Cell 的可见性计算流程
        /// 负责通过多阶段 Job + Raycast 批处理，
        /// 计算该 Cell 能看到哪些 GeometryNode / CullingTarget
        /// </summary>
        private class VisibilityComputingProcess : IDisposable
        {
            /// <summary>
            /// 当前正在计算的 Cell
            /// </summary>
            private VisibilityTreeNode _cell;

            /// <summary>
            /// Cell 的世界空间中心点（射线起点）
            /// </summary>
            private Vector3 _cellPosition;

            /// <summary>
            /// Cell 尺寸（取最小边，用于控制射线密度）
            /// </summary>
            private float _cellSize;

            /// <summary>
            /// Geometry Tree 的只读结构数据
            /// </summary>
            private NativeArray<GeometryNodeStruct> _geometryTreeStruct;

            /// <summary>
            /// 所有可被剔除目标的结构数据
            /// </summary>
            private NativeArray<CullingTargetStruct> _cullingTargetsStruct;

            /// <summary>
            /// GeometryNodeIndex -> CullingTargetIndex 列表映射
            /// 用于从可见 GeometryNode 推导可见目标
            /// </summary>
            private NativeHashMap_Int_UnsafeListInt _nodeTargetsMap;

            /// <summary>
            /// GeometryTree 中每个节点是否被当前 Cell 看见
            /// </summary>
            private NativeArray<bool> _geometryTreeVisibility;

            /// <summary>
            /// 每个 CullingTarget 是否被当前 Cell 看见
            /// </summary>
            private NativeArray<bool> _cullingTargetsVisibility;

            /// <summary>
            /// 标记 CullingTarget 是否已经被计算过
            /// （防止重复处理）
            /// </summary>
            private NativeArray<bool> _computedCullingTargets;

            /// <summary>
            /// 射线批次信息（每个 GeometryNode 对应一组射线）
            /// </summary>
            private NativeList<RaycastBatchInfo> _raycastBatches;

            /// <summary>
            /// RaycastCommand 列表（Unity Job 射线系统）
            /// </summary>
            private NativeList<RaycastCommand> _commands;

            /// <summary>
            /// Raycast 命中结果
            /// </summary>
            private NativeList<RaycastHit> _hits;

            /// <summary>
            /// 上一轮 Job 处理到的 GeometryNode 索引
            /// 用于分帧 / 分批推进
            /// </summary>
            private NativeArray<int> _lastNodeIndex;

            /// <summary>
            /// 上一轮 Job 处理到的 CullingTarget 索引
            /// </summary>
            private NativeArray<int> _lastTargetIndex;

            /// <summary>
            /// 每单位长度发射的射线数量
            /// </summary>
            private float _raysPerUnit;

            /// <summary>
            /// 单轮最大射线数量
            /// </summary>
            private int _maxRays;

            /// <summary>
            /// 射线最大检测距离
            /// </summary>
            private float _maxDistance;

            /// <summary>
            /// 射线检测 LayerMask
            /// </summary>
            private int _layerMask;

            /// <summary>
            /// GeometryTree 开始检测的深度
            /// </summary>
            private int _startDepth;

            /// <summary>
            /// GeometryTree 总高度
            /// </summary>
            private int _treeHeight;

            /// <summary>
            /// 当前正在处理的深度
            /// </summary>
            private int _depth;

            /// <summary>
            /// 当前 JobHandle
            /// </summary>
            private JobHandle _handle;

            /// <summary>
            /// 当前执行到的 Job 阶段索引
            /// 0~4 对应不同计算阶段
            /// </summary>
            private int _jobIndex;

            /// <summary>
            /// 创建一个 Cell 的可见性计算流程
            /// </summary>
            public VisibilityComputingProcess(VisibilityTreeNode cell, StaticCullingBaker context)
            {
                _cell = cell;
                _cellPosition = _cell.Center;
                _cellSize = Mathf.Min(cell.Size.x, cell.Size.y, cell.Size.z);

                // 引用 Baker 中已经构建好的共享数据
                _geometryTreeStruct = context._geometryTreeStruct;
                _cullingTargetsStruct = context._cullingTargetsStruct;
                _nodeTargetsMap = context._cellTargetsMap;

                // 运行时临时可见性标记
                _geometryTreeVisibility = new NativeArray<bool>(_geometryTreeStruct.Length, Allocator.TempJob);
                _cullingTargetsVisibility = new NativeArray<bool>(_cullingTargetsStruct.Length, Allocator.TempJob);
                _computedCullingTargets = new NativeArray<bool>(_cullingTargetsStruct.Length, Allocator.TempJob);

                // 射线相关缓存
                _raycastBatches = new NativeList<RaycastBatchInfo>(COMMANDS_LIMIT, Allocator.TempJob);
                _commands = new NativeList<RaycastCommand>(COMMANDS_LIMIT + context._maxRays, Allocator.TempJob);
                _hits = new NativeList<RaycastHit>(COMMANDS_LIMIT + context._maxRays, Allocator.TempJob);

                // 分批推进用索引
                _lastNodeIndex = new NativeArray<int>(1, Allocator.TempJob);
                _lastTargetIndex = new NativeArray<int>(1, Allocator.TempJob);

                // 拷贝参数
                _startDepth = context._startDepth;
                _raysPerUnit = context._raysPerUnit;
                _maxRays = context._maxRays;
                _maxDistance = context._maxDistance;
                _layerMask = context._layerMask;
                _treeHeight = context._geometryTree.Height;

                _depth = _startDepth;
            }

            /// <summary>
            /// 推进一次可见性计算流程
            /// finished = true 表示该 Cell 的计算完成
            /// </summary>
            public void Update(out bool finished)
            {
                finished = false;

                // 当前 Job 还未完成，直接返回
                if (!_handle.IsCompleted)
                    return;

                _handle.Complete();

                // Job 0：生成射线
                if (_jobIndex == 0)
                {
                    RunJob0();
                    _jobIndex = 1;
                }
                // Job 1：处理 GeometryTree 结果
                else if (_jobIndex == 1)
                {
                    RunJob1();

                    // 当前深度节点已处理完
                    if (_lastNodeIndex[0] == 0)
                    {
                        // 如果已到最深层，进入 CullingTarget 阶段
                        if (_depth == _treeHeight)
                        {
                            _jobIndex = 2;
                        }
                        // 否则进入下一层 GeometryTree
                        else
                        {
                            _depth++;
                            _jobIndex = 0;
                        }
                    }
                    else
                    {
                        // 还有节点没处理完，继续当前深度
                        _jobIndex = 0;
                    }
                }
                // Job 2：生成 CullingTarget 射线
                else if (_jobIndex == 2)
                {
                    RunJob2();
                    _jobIndex = 3;
                }
                // Job 3：计算 CullingTarget 可见性
                else if (_jobIndex == 3)
                {
                    RunJob3();

                    // 所有节点和目标都处理完成
                    if (_lastNodeIndex[0] == 0 && _lastTargetIndex[0] == 0)
                    {
                        _jobIndex = 4;
                    }
                    else
                    {
                        _jobIndex = 2;
                    }
                }
                // Job 4：流程结束
                else if (_jobIndex == 4)
                {
                    finished = true;
                }
            }

            /// <summary>
            /// 将计算得到的可见 CullingTarget 写回 Cell
            /// </summary>
            public void ApplyData()
            {
                for (int i = 0; i < _cullingTargetsVisibility.Length; i++)
                {
                    if (_cullingTargetsVisibility[i])
                        _cell.AddVisibleCullingTarget(i);
                }
            }

            /// <summary>
            /// 释放所有 Native 容器
            /// </summary>
            public void Dispose()
            {
                _handle.Complete();

                _geometryTreeVisibility.Dispose();
                _cullingTargetsVisibility.Dispose();
                _computedCullingTargets.Dispose();

                _raycastBatches.Dispose();
                _commands.Dispose();
                _hits.Dispose();
                _lastNodeIndex.Dispose();
                _lastTargetIndex.Dispose();
            }

            /// <summary>
            /// Job 0：
            /// 根据 GeometryTree 深度生成射线（RaycastCommand）
            /// </summary>
            private void RunJob0()
            {
                _handle = new TreeCreateRaysJob()
                {
                    cellPosition = _cellPosition,
                    cellSize = _cellSize,

                    geometryTreeStruct = _geometryTreeStruct,
                    geometryTreeVisibility = _geometryTreeVisibility,

                    rayBatches = _raycastBatches,
                    commands = _commands,
                    hits = _hits,

                    lastNodeIndex = _lastNodeIndex,

                    raysPerUnit = _raysPerUnit,
                    maxRays = _maxRays,
                    maxDistance = _maxDistance,
                    layerMask = _layerMask,

                    startDepth = _startDepth,
                    targetDepth = _depth,
                    commandsLimit = COMMANDS_LIMIT

                }.Schedule();
            }

            /// <summary>
            /// Job 1：
            /// 执行射线检测，并计算 GeometryNode / CullingTarget 的可见性
            /// </summary>
            private void RunJob1()
            {
                _handle = new TreeComputeResultsJob()
                {
                    cellPosition = _cellPosition,

                    geometryTree = _geometryTreeStruct,
                    cullingTargetsStruct = _cullingTargetsStruct,
                    nodeTargetsMap = _nodeTargetsMap,

                    rayBatches = _raycastBatches,
                    commands = _commands,
                    hits = _hits,

                    geometryTreeVisibility = _geometryTreeVisibility,
                    cullingTargetsVisibility = _cullingTargetsVisibility

                }.Schedule(RaycastCommand.ScheduleBatch(_commands, _hits, 1));
            }

            
            /// <summary>
            /// Job 2：
            /// 为 CullingTarget 阶段生成射线（TargetsCreateRaysJob）
            /// 
            /// 作用说明：
            /// 1. 基于已确认可见的 GeometryTree 节点
            /// 2. 找出这些节点关联的 CullingTarget
            /// 3. 为尚未计算过的 CullingTarget 生成射线
            /// 4. 控制射线数量，分批推进，避免一次性生成过多 RaycastCommand
            ///
            /// 这是从「几何节点可见」
            /// 过渡到「具体剔除目标可见」的关键阶段
            /// </summary>
            private void RunJob2()
            {
                _handle = new TargetsCreateRaysJob()
                {
                    // Cell 的世界空间中心点（射线起点）
                    cellPosition = _cellPosition,

                    // Cell 尺寸（用于射线密度计算）
                    cellSize = _cellSize,

                    // GeometryTree 的结构数据
                    geometryTreeStruct = _geometryTreeStruct,

                    // 所有剔除目标的数据结构
                    cullingTargetsStruct = _cullingTargetsStruct,

                    // GeometryTree 各节点的可见性结果
                    // 只有可见节点才会参与后续 Target 的射线生成
                    geometryTreeVisibility = _geometryTreeVisibility,

                    // CullingTarget 的可见性标记（写入）
                    cullingTargetsVisibility = _cullingTargetsVisibility,

                    // 标记哪些 CullingTarget 已经被处理过
                    // 防止重复生成射线
                    computedCullingTargets = _computedCullingTargets,

                    // GeometryNode -> CullingTarget 映射表
                    // 用于从可见节点快速找到关联目标
                    nodeTargetsMap = _nodeTargetsMap,

                    // 射线批次信息（记录每批射线对应的目标）
                    rayBatches = _raycastBatches,

                    // RaycastCommand 列表
                    commands = _commands,

                    // RaycastHit 结果缓存
                    hits = _hits,

                    // 上一轮处理到的 GeometryNode 索引
                    // 用于分帧 / 分批推进
                    lastNodeIndex = _lastNodeIndex,

                    // 上一轮处理到的 CullingTarget 索引
                    lastTargetIndex = _lastTargetIndex,

                    // 每单位长度的射线数量
                    raysPerUnit = _raysPerUnit,

                    // 本轮最大射线数量限制
                    maxRays = _maxRays,

                    // 射线最大检测距离
                    maxDistance = _maxDistance,

                    // 射线检测 LayerMask
                    layerMask = _layerMask,

                    // GeometryTree 起始深度
                    startDepth = _startDepth,

                    // 当前正在处理的 GeometryTree 深度
                    targetDepth = _depth,

                    // 单批 RaycastCommand 数量上限
                    commandsLimit = COMMANDS_LIMIT

                }.Schedule();
            }

            /// <summary>
            /// Job 3：
            /// 执行 CullingTarget 阶段的射线检测，并计算最终可见性
            /// （TargetsComputeResultsJob）
            ///
            /// 作用说明：
            /// 1. 执行 RunJob2 中生成的 RaycastCommand
            /// 2. 根据 RaycastHit 结果判断 CullingTarget 是否被遮挡
            /// 3. 将可见结果写入 cullingTargetsVisibility
            ///
            /// 这是整个 Cell 可见性计算流程的最终判定阶段
            /// </summary>
            private void RunJob3()
            {
                _handle = new TargetsComputeResultsJob()
                {
                    // Cell 的世界空间中心点
                    cellPosition = _cellPosition,

                    // GeometryTree 的结构数据
                    geometryTree = _geometryTreeStruct,

                    // 所有剔除目标的数据
                    cullingTargetsStruct = _cullingTargetsStruct,

                    // GeometryNode -> CullingTarget 映射
                    nodeTargetsMap = _nodeTargetsMap,

                    // 射线批次信息
                    rayBatches = _raycastBatches,

                    // RaycastCommand 列表
                    commands = _commands,

                    // RaycastHit 结果
                    hits = _hits,

                    // CullingTarget 的最终可见性结果（写入）
                    cullingTargetsVisibility = _cullingTargetsVisibility

                }
                // 使用 Unity RaycastCommand 批处理调度射线
                // 第二个参数为命中结果数组
                // 第三个参数为最小批次大小
                .Schedule(RaycastCommand.ScheduleBatch(_commands, _hits, 1));
            }
        }


                /// <summary>
        /// Geometry Tree 中的节点结构
        /// 用于描述场景几何的层级关系（类似 BVH / Octree）
        /// </summary>
        [BurstCompile]
        private struct GeometryNodeStruct
        {
            /// <summary>
            /// 当前节点在 GeometryTree 中的索引
            /// </summary>
            public int index;

            /// <summary>
            /// 左子节点索引（< 0 表示无子节点）
            /// </summary>
            public int left;

            /// <summary>
            /// 右子节点索引
            /// </summary>
            public int right;

            /// <summary>
            /// 当前节点的包围盒
            /// </summary>
            public Bounds bounds;

            /// <summary>
            /// 是否为叶子节点
            /// </summary>
            public bool isLeaf;

            /// <summary>
            /// 是否为空节点（无有效几何）
            /// </summary>
            public bool isEmpty;
        }

        /// <summary>
        /// 可被剔除目标的数据结构
        /// （通常对应 Renderer / Mesh / Instance）
        /// </summary>
        [BurstCompile]
        private struct CullingTargetStruct
        {
            /// <summary>
            /// CullingTarget 在数组中的索引
            /// </summary>
            public int index;

            /// <summary>
            /// 目标的包围盒
            /// </summary>
            public Bounds bounds;
        }

        /// <summary>
        /// 一批射线的描述信息
        /// 用于将 Raycast 结果映射回对应的 GeometryNode / CullingTarget
        /// </summary>
        [BurstCompile]
        private struct RaycastBatchInfo
        {
            /// <summary>
            /// 对应的 GeometryNode 或 CullingTarget 索引
            /// </summary>
            public int targetIndex;

            /// <summary>
            /// 本批射线在 commands 数组中的起始索引
            /// </summary>
            public int raysStart;

            /// <summary>
            /// 本批射线在 commands 数组中的结束索引
            /// </summary>
            public int raysEnd;
        }

        /// <summary>
        /// Job：为 GeometryTree 节点生成射线（第一阶段）
        /// 
        /// 目标：
        /// - 从 Cell 中心向 GeometryNode 的包围盒发射射线
        /// - 用于判断 GeometryNode 是否被遮挡
        /// - 按 GeometryTree 的深度逐层推进
        /// </summary>
        [BurstCompile]
        private struct TreeCreateRaysJob : IJob
        {
            /// <summary>
            /// 低差异序列相关常量
            /// （用于在包围盒内均匀采样射线点）
            /// </summary>
            private static readonly double g = 1.22074408460575947536;
            private static readonly double a1 = 1.0 / g;
            private static readonly double a2 = 1.0 / (g * g);
            private static readonly double a3 = 1.0 / (g * g * g);

            /// <summary>
            /// GeometryTree 的结构数据（只读）
            /// </summary>
            [ReadOnly]
            public NativeArray<GeometryNodeStruct> geometryTreeStruct;

            /// <summary>
            /// GeometryTree 节点的可见性结果（只读）
            /// </summary>
            [ReadOnly]
            public NativeArray<bool> geometryTreeVisibility;

            /// <summary>
            /// 生成的 RaycastCommand 列表
            /// </summary>
            [WriteOnly]
            public NativeList<RaycastCommand> commands;

            /// <summary>
            /// Raycast 命中结果缓存
            /// </summary>
            [WriteOnly]
            public NativeList<RaycastHit> hits;

            /// <summary>
            /// 每一批射线的描述信息
            /// </summary>
            [WriteOnly]
            public NativeList<RaycastBatchInfo> rayBatches;

            /// <summary>
            /// 上一轮处理到的 GeometryNode 索引
            /// 用于分批 / 分帧推进
            /// </summary>
            public NativeArray<int> lastNodeIndex;

            /// <summary>
            /// Cell 的世界空间中心点（射线起点）
            /// </summary>
            public Vector3 cellPosition;

            /// <summary>
            /// Cell 尺寸（用于射线密度计算）
            /// </summary>
            public float cellSize;

            /// <summary>
            /// 每单位长度生成的射线数量
            /// </summary>
            public float raysPerUnit;

            /// <summary>
            /// 本轮最大射线数量限制
            /// </summary>
            public int maxRays;

            /// <summary>
            /// 射线最大检测距离
            /// </summary>
            public float maxDistance;

            /// <summary>
            /// 射线检测 LayerMask
            /// </summary>
            public int layerMask;

            /// <summary>
            /// GeometryTree 起始深度
            /// </summary>
            public int startDepth;

            /// <summary>
            /// 当前要处理的目标深度
            /// </summary>
            public int targetDepth;

            /// <summary>
            /// 单批 RaycastCommand 上限
            /// </summary>
            public int commandsLimit;

            /// <summary>
            /// 当前已生成的 RaycastCommand 数量
            /// </summary>
            private int _commandsCount;

            /// <summary>
            /// Job 执行入口
            /// </summary>
            public void Execute()
            {
                // 清空上一轮数据
                rayBatches.Clear();
                commands.Clear();
                hits.Clear();

                // 从 GeometryTree 根节点开始遍历
                TraverseTree(geometryTreeStruct[0], 1);

                // RaycastHit 数组长度与命令数保持一致
                hits.Length = _commandsCount;

                // 如果未超过限制，说明本深度已处理完成
                if (_commandsCount <= commandsLimit)
                    lastNodeIndex[0] = 0;
            }

            /// <summary>
            /// 递归遍历 GeometryTree
            /// 按深度生成射线
            /// </summary>
            private void TraverseTree(GeometryNodeStruct node, int depth)
            {
                // 空节点直接跳过
                if (node.isEmpty)
                    return;

                // 超出射线数量限制，停止生成
                if (_commandsCount > commandsLimit)
                    return;

                // 到达目标深度，开始为该节点生成射线
                if (depth == targetDepth)
                {
                    // 确保不会重复处理已经处理过的节点
                    if (node.index > lastNodeIndex[0])
                    {
                        // 仅对当前不可见的节点生成射线
                        if (!geometryTreeVisibility[node.index])
                        {
                            CreateRaysBatch(node);

                            // 超限时记录当前位置，下次从这里继续
                            if (_commandsCount > commandsLimit)
                                lastNodeIndex[0] = node.index;
                        }
                    }

                    return;
                }

                // 如果该节点已被判定不可见，且已达到起始深度，剪枝
                if (!geometryTreeVisibility[node.index] && depth >= startDepth)
                    return;

                // 没有子节点，结束
                if (node.left < 0)
                    return;

                // 深度优先遍历左右子节点
                TraverseTree(geometryTreeStruct[node.left], depth + 1);
                TraverseTree(geometryTreeStruct[node.right], depth + 1);
            }

            /// <summary>
            /// 为单个 GeometryNode 创建一批射线
            /// </summary>
            private void CreateRaysBatch(GeometryNodeStruct node)
            {
                Bounds bounds = node.bounds;

                // 如果 Cell 位于节点包围盒内部，直接认为可见
                if (bounds.Contains(cellPosition))
                {
                    rayBatches.AddNoResize(new RaycastBatchInfo()
                    {
                        targetIndex = node.index,
                        raysStart = -1,
                        raysEnd = -1,
                    });

                    return;
                }

                // 计算 Cell 到节点中心的距离
                float distance = Vector3.Distance(bounds.center, cellPosition);

                // 根据距离调整射线密度（越远射线越少）
                float distanceRatio = Mathf.Max((distance / maxDistance), 0.01f);

                // 根据包围盒尺寸和距离计算射线数量
                int raysCount = Mathf.RoundToInt(5 + bounds.size.magnitude * raysPerUnit * distanceRatio);
                raysCount = Mathf.Min(raysCount, maxRays);

                // 记录该节点对应的射线范围
                rayBatches.AddNoResize(new RaycastBatchInfo()
                {
                    targetIndex = node.index,
                    raysStart = _commandsCount,
                    raysEnd = _commandsCount + raysCount,
                });

                _commandsCount += raysCount;

                // 在包围盒内生成多个采样点，并向其发射射线
                for (int i = 0; i < raysCount; i++)
                {
                    Vector3 targetPoint = GetPointInsideBoundingBox(i, bounds);
                    Vector3 dir = (targetPoint - cellPosition).normalized;

                    RaycastCommand command =
                        UnityAPI.NewRaycastCommand(cellPosition, dir, layerMask: layerMask);

                    commands.AddNoResize(command);
                }
            }
            
            /// <summary>
            /// 在包围盒内部生成一个均匀分布的采样点
            /// 
            /// 该方法使用低差异序列（Low-Discrepancy Sequence），
            /// 相比纯随机采样，能够在较少采样次数下
            /// 更均匀地覆盖整个包围盒体积。
            /// 
            /// 主要用途：
            /// - 为 GeometryNode / CullingTarget 生成射线的目标点
            /// - 提高可见性判定的稳定性与准确性
            /// - 避免射线集中导致的误判
            /// </summary>
            private Vector3 GetPointInsideBoundingBox(int index, Bounds bounds)
            {
                // 包围盒尺寸
                Vector3 size = bounds.size;

                // 使用低差异序列在 [0, 1) 范围内生成 3 个伪随机值
                // 不同维度使用不同的基数，避免采样点在空间中聚集
                float x = (float)(0.5 + a1 * index) % 1;
                float y = (float)(0.5 + a2 * index) % 1;
                float z = (float)(0.5 + a3 * index) % 1;

                // 将归一化采样值映射到包围盒实际尺寸
                Vector3 offset = new Vector3(
                    x * size.x,
                    y * size.y,
                    z * size.z
                );

                // 从包围盒最小点出发，加上偏移量，得到最终采样点
                return bounds.min + offset;
            }

        }

                /// <summary>
        /// Job：处理 GeometryTree 阶段的射线结果
        /// 
        /// 作用：
        /// 1. 解析 TreeCreateRaysJob 生成的 Raycast 结果
        /// 2. 判定 GeometryNode 是否对当前 Cell 可见
        /// 3. 在必要时进一步标记 CullingTarget 的可见性
        ///
        /// 这是静态剔除中「从射线命中结果 → 空间可见性」的关键逻辑
        /// </summary>
        [BurstCompile]
        private struct TreeComputeResultsJob : IJob
        {
            /// <summary>
            /// Cell 的世界空间中心点
            /// </summary>
            [ReadOnly]
            public Vector3 cellPosition;

            /// <summary>
            /// GeometryTree 的结构数据
            /// </summary>
            [ReadOnly]
            public NativeArray<GeometryNodeStruct> geometryTree;

            /// <summary>
            /// 所有 CullingTarget 的结构数据
            /// </summary>
            [ReadOnly]
            public NativeArray<CullingTargetStruct> cullingTargetsStruct;

            /// <summary>
            /// GeometryNode -> CullingTarget 索引映射
            /// </summary>
            [ReadOnly]
            public NativeHashMap_Int_UnsafeListInt nodeTargetsMap;

            /// <summary>
            /// 射线批次信息
            /// （每一批对应一个 GeometryNode）
            /// </summary>
            [ReadOnly]
            public NativeList<RaycastBatchInfo> rayBatches;

            /// <summary>
            /// 所有 RaycastCommand
            /// </summary>
            [ReadOnly]
            public NativeList<RaycastCommand> commands;

            /// <summary>
            /// 所有 RaycastHit 结果
            /// </summary>
            [ReadOnly]
            public NativeList<RaycastHit> hits;

            /// <summary>
            /// GeometryTree 节点的可见性结果（写入）
            /// </summary>
            public NativeArray<bool> geometryTreeVisibility;

            /// <summary>
            /// CullingTarget 的可见性结果（写入）
            /// </summary>
            public NativeArray<bool> cullingTargetsVisibility;

            /// <summary>
            /// Job 执行入口
            /// </summary>
            public void Execute()
            {
                // 遍历每一批射线
                for (int i = 0; i < rayBatches.Length; i++)
                {
                    RaycastBatchInfo raycastBatch = rayBatches[i];
                    GeometryNodeStruct node = geometryTree[raycastBatch.targetIndex];

                    // raysStart < 0 表示 Cell 位于该节点包围盒内部
                    // 该 GeometryNode 必然可见
                    if (raycastBatch.raysStart < 0)
                    {
                        geometryTreeVisibility[node.index] = true;
                        continue;
                    }

                    int start = raycastBatch.raysStart;
                    int end = raycastBatch.raysEnd;

                    int tracedRays = 0;
                    int tracedPoints = 0;

                    // 遍历该 GeometryNode 对应的所有射线
                    for (int c = start; c < end; c++)
                    {
                        RaycastHit hit = hits[c];
                        RaycastCommand command = commands[c];
                        Ray ray = new Ray(command.from, command.direction);

                        float targetDistance = 0;
                        float hitDistance = hit.distance;

                        // 计算射线与 GeometryNode 包围盒的交点距离
                        node.bounds.IntersectRay(ray, out targetDistance);

                        // 未命中任何物体（hit.distance 为 0）
                        if (hitDistance < 0.0001f)
                        {
                            hitDistance = float.MaxValue;
                        }
                        // 对前几个命中点进行精细分析
                        else if (tracedPoints < 5)
                        {
                            // 从 GeometryTree 根节点向下追踪命中点
                            TracePoint(geometryTree[0], hit.point);
                            tracedPoints++;
                        }

                        // 如果射线在到达 GeometryNode 前未被遮挡
                        if (hitDistance > targetDistance)
                        {
                            // 沿射线路径追踪可见的 GeometryNode
                            TraceRay(geometryTree[0], ray, hitDistance);

                            tracedRays++;

                            // 达到最小可见射线数量即可提前结束
                            if (tracedRays >= 5)
                                break;
                        }
                    }
                }
            }

            /// <summary>
            /// 沿射线路径递归标记 GeometryNode 可见性
            /// 
            /// 逻辑：
            /// - 如果射线在 hitDistance 之前与节点包围盒相交
            /// - 则该节点在当前 Cell 下可见
            /// </summary>
            private void TraceRay(GeometryNodeStruct node, Ray ray, float hitDistance)
            {
                if (node.isEmpty)
                    return;

                if (node.bounds.IntersectRay(ray, out float nodeIntersectDistance))
                {
                    // 只处理射线命中点之前的节点
                    if (nodeIntersectDistance < hitDistance)
                    {
                        geometryTreeVisibility[node.index] = true;

                        // 递归处理子节点
                        if (node.left != -1)
                        {
                            TraceRay(geometryTree[node.left], ray, hitDistance);
                            TraceRay(geometryTree[node.right], ray, hitDistance);
                        }
                    }
                }
            }

            /// <summary>
            /// 根据射线命中点，反推可能可见的 CullingTarget
            /// 
            /// 用于捕捉「射线命中 GeometryNode 内部」但
            /// 直接 TraceRay 无法覆盖的可见目标
            /// </summary>
            private void TracePoint(GeometryNodeStruct node, Vector3 hitPoint)
            {
                if (node.isEmpty)
                    return;

                // 命中点不在该节点包围盒内，直接剪枝
                if (!node.bounds.Contains(hitPoint))
                    return;

                // 叶子节点，直接检查关联的 CullingTarget
                if (node.isLeaf)
                {
                    UnsafeList<int> targets = nodeTargetsMap[node.index];

                    for (int i = 0; i < targets.Length; i++)
                    {
                        int idx = targets[i];

                        // 已经标记可见的目标无需再次处理
                        if (cullingTargetsVisibility[idx])
                            continue;

                        CullingTargetStruct target = cullingTargetsStruct[idx];

                        // 如果命中点位于目标包围盒内，认为该目标可见
                        if (target.bounds.Contains(hitPoint))
                            cullingTargetsVisibility[idx] = true;
                    }
                }
                else
                {
                    // 非叶子节点，继续向子节点递归
                    if (node.left >= 0)
                    {
                        TracePoint(geometryTree[node.left], hitPoint);
                        TracePoint(geometryTree[node.right], hitPoint);
                    }
                }
            }
        }


        [BurstCompile]
        private struct TargetsCreateRaysJob : IJob
        {
            // ===========================
            // 用于生成低差异采样点的常量（类似 Halton / 黄金比例序列）
            // 目的是在 Bounds 内生成分布均匀的采样点，而不是完全随机
            // ===========================
            private static readonly double g = 1.22074408460575947536;
            private static readonly double a1 = 1.0 / g;
            private static readonly double a2 = 1.0 / (g * g);
            private static readonly double a3 = 1.0 / (g * g * g);

            // ===========================
            // 当前 Cell（剔除单元）的世界坐标
            // 所有射线都会从这个点发射
            // ===========================
            [ReadOnly]
            public Vector3 cellPosition;

            // 当前 Cell 的尺寸（边长）
            [ReadOnly]
            public float cellSize;

            // ===========================
            // 几何树（BVH / KD-Tree / Octree 之类）
            // ===========================
            [ReadOnly]
            public NativeArray<GeometryNodeStruct> geometryTreeStruct;

            // 所有剔除目标（Renderer / Instance 等）
            [ReadOnly]
            public NativeArray<CullingTargetStruct> cullingTargetsStruct;

            // GeometryNode.index -> UnsafeList<int>（该节点包含的 Target 索引）
            [ReadOnly]
            public NativeHashMap_Int_UnsafeListInt nodeTargetsMap;

            // 上一帧或前一阶段计算得到的 GeometryNode 可见性
            [ReadOnly]
            public NativeArray<bool> geometryTreeVisibility;

            // 上一帧或前一阶段计算得到的 Target 可见性
            [ReadOnly]
            public NativeArray<bool> cullingTargetsVisibility;

            // 本轮 Job 中已经处理过的 Target（防止重复发射射线）
            public NativeArray<bool> computedCullingTargets;

            // ===========================
            // 输出：Raycast 批次信息
            // 每个 Target 对应一段射线区间
            // ===========================
            [WriteOnly]
            public NativeList<RaycastBatchInfo> rayBatches;

            // 输出：RaycastCommand 列表
            [WriteOnly]
            public NativeList<RaycastCommand> commands;

            // 输出：RaycastHit，占位，长度会在 Execute 末尾统一设置
            [WriteOnly]
            public NativeList<RaycastHit> hits;

            // ===========================
            // 用于“断点续跑”的索引
            // 防止一次 Job 射线过多
            // ===========================
            public NativeArray<int> lastNodeIndex;
            public NativeArray<int> lastTargetIndex;

            // ===========================
            // 射线生成相关参数
            // ===========================
            public float raysPerUnit;     // 每单位尺寸生成多少条射线
            public int maxRays;           // 单个 Target 最大射线数
            public float maxDistance;     // 最大检测距离
            public int layerMask;         // Raycast LayerMask

            // ===========================
            // 树遍历深度控制
            // ===========================
            public int startDepth;        // 开始生成射线的最小深度
            public int targetDepth;       // 目标深度（当前实现中未直接使用）
            public int commandsLimit;     // 单次 Job 最大 RaycastCommand 数量

            // 当前 Job 已生成的 RaycastCommand 数量
            private int _commandsCount;

            // ===========================
            // Job 主入口
            // ===========================
            public void Execute()
            {
                // 清空上一次的数据
                rayBatches.Clear();
                commands.Clear();
                hits.Clear();

                // 从几何树根节点开始递归遍历
                TraverseTree(geometryTreeStruct[0], 1);

                // RaycastHit 数量必须与 Command 数量一致
                hits.Length = _commandsCount;

                // 如果本轮没有超出限制，重置断点索引
                if (_commandsCount <= commandsLimit)
                {
                    lastNodeIndex[0] = 0;
                    lastTargetIndex[0] = 0;
                }
            }

            /// <summary>
            /// 递归遍历几何树，根据可见性和深度决定是否生成射线
            /// </summary>
            private void TraverseTree(GeometryNodeStruct node, int depth)
            {
                // 空节点直接跳过
                if (node.isEmpty)
                    return;

                // 超出射线数量上限，直接终止
                if (_commandsCount > commandsLimit)
                    return;

                // 如果该节点不可见，并且已经达到起始深度，则不再继续
                if (!geometryTreeVisibility[node.index] && depth >= startDepth)
                    return;

                // ===========================
                // 叶子节点：处理剔除目标
                // ===========================
                if (node.isLeaf)
                {
                    // 断点续跑：只处理 index >= lastNodeIndex 的节点
                    if (node.index >= lastNodeIndex[0])
                    {
                        UnsafeList<int> targets = nodeTargetsMap[node.index];
                        int lastTarget = lastTargetIndex[0];

                        for (int i = 0; i < targets.Length; i++)
                        {
                            // 跳过上次已经处理过的 Target
                            if (lastTarget != 0 && i <= lastTarget)
                                continue;

                            int targetIdx = targets[i];

                            // 已经在本轮计算过的 Target，跳过
                            if (computedCullingTargets[targetIdx])
                                continue;

                            // 如果 Target 已经被判定为可见，不需要发射射线
                            if (cullingTargetsVisibility[targetIdx])
                            {
                                computedCullingTargets[targetIdx] = true;
                                continue;
                            }

                            // 为该 Target 创建射线批次
                            CreateRaysBatch(cullingTargetsStruct[targetIdx]);
                            computedCullingTargets[targetIdx] = true;

                            // 如果超出命令限制，记录断点并退出
                            if (_commandsCount > commandsLimit)
                            {
                                lastNodeIndex[0] = node.index;
                                lastTargetIndex[0] = i;
                                break;
                            }
                        }

                        // 如果本节点处理完成且未超限，重置 Target 断点
                        if (_commandsCount <= commandsLimit)
                        {
                            if (lastNodeIndex[0] == node.index)
                                lastTargetIndex[0] = 0;
                        }

                        return;
                    }
                }

                // 非叶子节点，递归左右子树
                if (node.left < 0)
                    return;

                TraverseTree(geometryTreeStruct[node.left], depth + 1);
                TraverseTree(geometryTreeStruct[node.right], depth + 1);
            }

            /// <summary>
            /// 为单个 CullingTarget 生成一批射线
            /// </summary>
            private void CreateRaysBatch(CullingTargetStruct target)
            {
                Bounds bounds = target.bounds;

                // 如果 Cell 在 Target 包围盒内部，直接判定可见（无需射线）
                if (bounds.Contains(cellPosition))
                {
                    rayBatches.AddNoResize(new RaycastBatchInfo()
                    {
                        targetIndex = target.index,
                        raysStart = -1,
                        raysEnd = -1,
                    });

                    return;
                }

                // 根据 Target 距离动态调整射线密度
                float distance = Vector3.Distance(bounds.center, cellPosition);
                float distanceRatio = Mathf.Max((distance / maxDistance), 0.01f);

                int raysCount = Mathf.RoundToInt(5 + bounds.size.magnitude * raysPerUnit * distanceRatio);
                raysCount = Mathf.Min(raysCount, maxRays);

                // 记录该 Target 对应的射线区间
                rayBatches.AddNoResize(new RaycastBatchInfo()
                {
                    targetIndex = target.index,
                    raysStart = _commandsCount,
                    raysEnd = _commandsCount + raysCount,
                });

                _commandsCount += raysCount;

                // 在 Bounds 内生成多个采样点，并从 Cell 向这些点发射射线
                for (int i = 0; i < raysCount; i++)
                {
                    Vector3 targetPoint = GetPointInsideBoundingBox(i, bounds);
                    Vector3 dir = (targetPoint - cellPosition).normalized;

                    RaycastCommand command = UnityAPI.NewRaycastCommand(
                        cellPosition,
                        dir,
                        layerMask: layerMask
                    );

                    commands.AddNoResize(command);
                }
            }
            
            /// <summary>
            /// 在指定的包围盒（Bounds）内部生成一个“分布均匀”的采样点
            /// 用于射线剔除时，避免所有射线都指向中心或角点
            ///
            /// 该方法不是随机采样，而是基于低差异序列（类似 Halton / 黄金比例序列）
            /// 能在多次调用时，让点在包围盒内部均匀铺开，减少射线聚集
            /// </summary>
            /// <param name="index">
            /// 当前采样点的索引值，
            /// 不同 index 会生成 Bounds 内不同位置的点
            /// </param>
            /// <param name="bounds">
            /// 目标物体的包围盒
            /// </param>
            /// <returns>
            /// 包围盒内部的一个采样点（世界坐标）
            /// </returns>
            private Vector3 GetPointInsideBoundingBox(int index, Bounds bounds)
            {
                // 包围盒在三个轴向上的尺寸
                Vector3 size = bounds.size;

                // =====================================================
                // 使用低差异序列生成 [0,1) 区间内的三个采样值
                //
                // a1 / a2 / a3 是黄金比例的不同幂次倒数：
                //   a1 = 1 / g
                //   a2 = 1 / g^2
                //   a3 = 1 / g^3
                //
                // 这种方式可以让随着 index 增加，
                // 采样点在 0~1 区间内“均匀铺开”，
                // 而不是像 Random 那样可能出现聚集
                // =====================================================

                float x = (float)(0.5 + a1 * index) % 1;
                float y = (float)(0.5 + a2 * index) % 1;
                float z = (float)(0.5 + a3 * index) % 1;

                // =====================================================
                // 将 [0,1) 的归一化坐标映射到包围盒尺寸
                // 得到相对于 bounds.min 的局部偏移量
                // =====================================================
                Vector3 offset = new Vector3(
                    x * size.x,
                    y * size.y,
                    z * size.z
                );

                // =====================================================
                // 最终点 = 包围盒最小点 + 偏移
                // 保证返回点一定在 Bounds 内部
                // =====================================================
                return bounds.min + offset;
            }

        }

        [BurstCompile]
        private struct TargetsComputeResultsJob : IJob
        {
            // 当前剔除 Cell 的世界坐标中心
            [ReadOnly]
            public Vector3 cellPosition;

            // 几何 BVH 树（用于追踪命中点所在的节点）
            [ReadOnly]
            public NativeArray<GeometryNodeStruct> geometryTree;

            // 所有剔除目标的数据（索引 + Bounds）
            [ReadOnly]
            public NativeArray<CullingTargetStruct> cullingTargetsStruct;

            // BVH 节点 -> 目标索引列表的映射
            // 用于在叶子节点中快速找到可能受影响的目标
            [ReadOnly]
            public NativeHashMap_Int_UnsafeListInt nodeTargetsMap;

            // 每个目标对应的一组射线批次信息
            [ReadOnly]
            public NativeList<RaycastBatchInfo> rayBatches;

            // 所有射线命令（由 TargetsCreateRaysJob 生成）
            [ReadOnly]
            public NativeList<RaycastCommand> commands;

            // 射线检测结果
            [ReadOnly]
            public NativeList<RaycastHit> hits;

            // 剔除目标的最终可见性结果
            // true = 该目标在当前 Cell 中被判定为可见
            public NativeArray<bool> cullingTargetsVisibility;

            public void Execute()
            {
                // 遍历所有目标的射线批次
                for (int i = 0; i < rayBatches.Length; i++)
                {
                    RaycastBatchInfo raycastBatch = rayBatches[i];
                    CullingTargetStruct target = cullingTargetsStruct[raycastBatch.targetIndex];

                    // raysStart < 0 表示：
                    // Cell 本身位于目标 Bounds 内部
                    // 该目标必然可见，无需射线检测
                    if (raycastBatch.raysStart < 0)
                    {
                        cullingTargetsVisibility[target.index] = true;
                        continue;
                    }

                    int start = raycastBatch.raysStart;
                    int end = raycastBatch.raysEnd;

                    // 用于限制命中点回溯次数，避免递归过深
                    int tracedPoints = 0;

                    // 遍历该目标对应的所有射线
                    for (int c = start; c < end; c++)
                    {
                        RaycastHit hit = hits[c];
                        RaycastCommand command = commands[c];
                        Ray ray = new Ray(command.from, command.direction);

                        float targetDistance = 0;
                        float hitDistance = hit.distance;

                        // 计算射线与目标 Bounds 的理论交点距离
                        target.bounds.IntersectRay(ray, out targetDistance);

                        // 给目标距离一个微小的偏移，避免浮点精度问题
                        targetDistance -= 0.01f;

                        // hitDistance 极小表示未命中任何物体
                        if (hitDistance < 0.0001f)
                        {
                            hitDistance = float.MaxValue;
                        }
                        // 对前几个有效命中点进行回溯
                        // 用于标记被命中路径上的其他目标
                        else if (tracedPoints < 2)
                        {
                            TracePoint(geometryTree[0], hit.point);
                            tracedPoints++;
                        }

                        // 如果射线在命中障碍物之前就已经到达目标 Bounds
                        // 说明该方向上目标是可见的
                        if (hitDistance > targetDistance)
                        {
                            cullingTargetsVisibility[target.index] = true;
                            break;
                        }
                    }
                }
            }

            /// <summary>
            /// 从 BVH 树根节点开始，回溯命中点所在路径
            /// 用于标记与该命中点重叠的其他剔除目标
            /// </summary>
            private void TracePoint(GeometryNodeStruct node, Vector3 hitPoint)
            {
                // 空节点直接跳过
                if (node.isEmpty)
                    return;

                // 命中点不在该节点包围盒内，直接剪枝
                if (!node.bounds.Contains(hitPoint))
                    return;

                // 叶子节点：检查该节点关联的所有剔除目标
                if (node.isLeaf)
                {
                    UnsafeList<int> targets = nodeTargetsMap[node.index];

                    for (int i = 0; i < targets.Length; i++)
                    {
                        int idx = targets[i];

                        // 已经标记为可见的目标不再处理
                        if (cullingTargetsVisibility[idx])
                            continue;

                        CullingTargetStruct target = cullingTargetsStruct[idx];

                        // 如果命中点位于该目标 Bounds 内
                        // 则认为该目标也被“顺带看到”
                        if (target.bounds.Contains(hitPoint))
                            cullingTargetsVisibility[idx] = true;
                    }
                }
                else
                {
                    // 非叶子节点，递归检查左右子节点
                    if (node.left >= 0)
                    {
                        TracePoint(geometryTree[node.left], hitPoint);
                        TracePoint(geometryTree[node.right], hitPoint);
                    }
                }
            }
        }
    }
}
