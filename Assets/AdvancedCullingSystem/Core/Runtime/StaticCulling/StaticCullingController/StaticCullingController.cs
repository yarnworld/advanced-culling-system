using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Static
{
    /// <summary>
    /// 静态剔除控制器
    /// 管理场景中的 CameraZone、GeometryTree 以及静态剔除烘焙流程
    /// </summary>
    public class StaticCullingController : MonoBehaviour
    {
        /// <summary>
        /// 获取所有 CameraZone 的只读列表
        /// </summary>
        public IReadOnlyList<CameraZone> CameraZones
        {
            get { return _cameraZones; }
        }

        /// <summary>
        /// 获取或设置几何树深度，范围 [7, 20]
        /// </summary>
        public int GeometryTreeDepth
        {
            get { return _geometryTreeDepth; }

            set { _geometryTreeDepth = Mathf.Clamp(value, 7, 20); }
        }

        /// <summary>
        /// 获取或设置 Cell 大小，最小为 0.1
        /// </summary>
        public float CellSize
        {
            get { return _cellSize; }

            set { _cellSize = Mathf.Max(value, 0.1f); }
        }

        /// <summary>
        /// 获取场景中总 Cell 数量
        /// </summary>
        public int TotalCellsCount
        {
            get { return _totalCellsCount; }
        }

        /// <summary>
        /// 每单位长度生成射线数量
        /// </summary>
        public float RaysPerUnit
        {
            get { return _raysPerUnit; }
            set { _raysPerUnit = Mathf.Max(0.1f, value); }
        }

        /// <summary>
        /// 每个源最多生成射线数
        /// </summary>
        public int MaxRaysPerSource
        {
            get { return _maxRaysPerSource; }
            set { _maxRaysPerSource = Mathf.Max(10, value); }
        }

        [SerializeField] private List<CameraZone> _cameraZones; // 管理的 CameraZone 列表

        [SerializeField] private GeometryTree _geometryTree; // 场景几何树实例

        [SerializeField] private int _geometryTreeDepth = 11; // 默认几何树深度

        [SerializeField] private float _cellSize = 5f; // 默认 Cell 大小

        [SerializeField] private int _totalCellsCount; // 场景总 Cell 数量

        [SerializeField] private float _raysPerUnit = 10f; // 默认每单位长度射线数

        [SerializeField] private int _maxRaysPerSource = 300; // 默认每个源最大射线数

        /// <summary>
        /// 添加 CameraZone
        /// </summary>
        public bool AddCameraZone(CameraZone zone)
        {
            if (zone == null)
                return false;

            if (_cameraZones == null)
                _cameraZones = new List<CameraZone>();

            if (_cameraZones.Contains(zone))
                return false;

            _cameraZones.Add(zone);

            return true;
        }

        /// <summary>
        /// 移除 CameraZone
        /// </summary>
        public bool RemoveCameraZone(CameraZone zone)
        {
            if (zone == null)
                return false;

            if (_cameraZones == null)
                return false;

            if (!_cameraZones.Contains(zone))
                return false;

            return _cameraZones.Remove(zone);
        }

        /// <summary>
        /// 创建几何树的预览（用于编辑器预览）
        /// </summary>
        public void CreatePreviewGeometryTree()
        {
            string error;

            if (!ReadyToCreateGeometryTree(out error))
            {
                Debug.Log("Unable to create GeometryTree : " + error);
                return;
            }

            List<StaticCullingSource> validSources = new List<StaticCullingSource>();

            foreach (var source in FindObjectsOfType<StaticCullingSource>())
            {
                if (source.Validate())
                {
                    source.PrepareForBaking(); // 准备烘焙
                    validSources.Add(source);
                }
            }

            CreateGeometryTree(validSources, out error);

            foreach (var source in validSources)
            {
                source.ClearAfterBaking();
                DestroyImmediate(source.gameObject.GetComponent<CullingTarget>()); // 移除临时 CullingTarget 组件
            }
        }

        /// <summary>
        /// 创建 CameraZone 的可见性树预览（编辑器用）
        /// </summary>
        public void CreatePreviewCameraZones()
        {
            string error;

            if (!ReadyToBakeCameraZones(out error))
            {
                Debug.Log("Unable to bake camera zones : " + error);
                return;
            }

            if (!CreateVisibilityTrees(out error))
            {
                Debug.Log("Unable to bake camera zones : " + error);
            }
        }

        /// <summary>
        /// 烘焙整个场景
        /// </summary>
        public void Bake()
        {
            string error;

            if (!ReadyToBake(out error))
            {
                Debug.Log("Unable to bake scene : " + error);
                return;
            }

            List<StaticCullingSource> sources;

            if (!PrepareForBake(out sources, out error))
            {
                Debug.Log("Baking process aborted");
                Debug.Log("Reason : " + error);
                ClearBakedData();
            }

            if (!CreateGeometryTree(sources, out error))
            {
                Debug.Log("Baking process aborted");
                Debug.Log("Reason : " + error);
                ClearBakedData();
            }

            if (!CreateVisibilityTrees(out error))
            {
                Debug.Log("Baking process aborted");
                Debug.Log("Reason : " + error);
                ClearBakedData();
            }

            if (BakeScene(_geometryTree, out error))
            {
                ClearAfterBaking(sources); // 烘焙完成后清理临时数据
                Debug.Log("Scene sucessfully baked!");
            }
            else
            {
                Debug.Log("Baking process aborted");
                Debug.Log("Reason : " + error);
                ClearBakedData();
            }
        }

        /// <summary>
        /// 清理已烘焙的数据
        /// </summary>
        public void Clear()
        {
            ClearBakedData();
        }

        /// <summary>
        /// 检查是否可以创建几何树
        /// </summary>
        private bool ReadyToCreateGeometryTree(out string error)
        {
            StaticCullingSource[] sources = FindObjectsOfType<StaticCullingSource>();

            if (sources == null || sources.Length == 0)
            {
                error = "StaticCullingSources not found. Add in 'Step 1'";
                return false;
            }

            foreach (var source in sources)
            {
                if (source.Validate())
                {
                    error = "";
                    return true;
                }
            }

            error = "Valid StaticCullingSources not found. Check in 'Step 1'";
            return false;
        }


        /// <summary>
        /// 检查是否可以烘焙 CameraZones
        /// </summary>
        private bool ReadyToBakeCameraZones(out string error)
        {
            if (_cameraZones == null || _cameraZones.Count == 0)
            {
                error = "Camera Zones not added. Add in 'Step 3'";
                return false;
            }

            // 移除 null 的 CameraZone
            int i = 0;
            while (i < _cameraZones.Count)
            {
                if (_cameraZones[i] == null)
                    _cameraZones.RemoveAt(i);
                else
                    i++;
            }

            if (_cameraZones.Count == 0)
            {
                error = "Camera Zones not added. Add in 'Step 3'";
                return false;
            }

            error = "";
            return true;
        }

        /// <summary>
        /// 检查是否可以烘焙整个场景（几何树 + CameraZones）
        /// </summary>
        private bool ReadyToBake(out string error)
        {
            if (!ReadyToCreateGeometryTree(out error))
                return false;

            if (!ReadyToBakeCameraZones(out error))
                return false;

            error = "";
            return true;
        }

        /// <summary>
        /// 烘焙准备：获取有效的 StaticCullingSource 并调用准备方法
        /// </summary>
        private bool PrepareForBake(out List<StaticCullingSource> sources, out string error)
        {
            sources = new List<StaticCullingSource>();

            try
            {
                StaticCullingSource[] sceneSources = FindObjectsOfType<StaticCullingSource>();

                foreach (var source in sceneSources)
                {
                    if (source.Validate())
                    {
                        source.PrepareForBaking(); // 准备烘焙
                        sources.Add(source);
                    }
                }

                error = "";
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message + ex.StackTrace;
                return false;
            }
        }

        /// <summary>
        /// 创建几何树
        /// </summary>
        private bool CreateGeometryTree(List<StaticCullingSource> sources, out string error)
        {
            try
            {
                // 根据有效的 CullingTarget 构建几何树
                _geometryTree = new GeometryTree(sources
                    .Select(s => s.CullingTarget)
                    .ToArray(), _geometryTreeDepth);

                error = "";
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message + ex.StackTrace;
                return false;
            }
        }

        /// <summary>
        /// 创建 CameraZone 的可见性树
        /// </summary>
        private bool CreateVisibilityTrees(out string error)
        {
            try
            {
                _totalCellsCount = 0;

                foreach (var zone in _cameraZones)
                {
                    if (zone != null)
                    {
                        zone.ClearVisibilityTree(); // 清理旧树
                        zone.CreateVisibilityTree(_cellSize); // 创建新树

                        _totalCellsCount += zone.CellsCount;
                    }
                }

                error = "";
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message + ex.StackTrace;
                return false;
            }
        }

        /// <summary>
        /// 使用 StaticCullingBaker 烘焙整个场景
        /// </summary>
        private bool BakeScene(GeometryTree geometryTree, out string error)
        {
            StaticCullingBaker baker = new StaticCullingBaker(geometryTree);

            error = "";
            bool aborted = false;

            foreach (var zone in _cameraZones)
            {
                if (!baker.Bake(zone.VisibilityTree, _raysPerUnit, _maxRaysPerSource, out error))
                {
                    aborted = true;
                    break;
                }
            }

            baker.Dispose();

            return !aborted;
        }

        /// <summary>
        /// 烘焙完成后清理源对象
        /// </summary>
        private void ClearAfterBaking(List<StaticCullingSource> sources)
        {
            foreach (var source in sources)
            {
                source.ClearAfterBaking();
                DestroyImmediate(source);
            }
        }

        /// <summary>
        /// 清理场景中已烘焙的数据，包括 CullingTarget、CameraZones
        /// </summary>
        private void ClearBakedData()
        {
            foreach (var source in FindObjectsOfType<StaticCullingSource>())
                source.ClearAfterBaking();

            int clearedCullingTargets = 0;

            foreach (var target in FindObjectsOfType<CullingTarget>())
            {
                target.gameObject.AddComponent<StaticCullingSource>();
                DestroyImmediate(target);
                clearedCullingTargets++;
            }

            int clearedCameraZones = 0;

            if (_cameraZones != null)
            {
                foreach (var zone in _cameraZones)
                {
                    if (zone != null)
                    {
                        zone.ClearVisibilityTree();
                        clearedCameraZones++;
                    }
                }
            }

            Debug.Log("Cleared Culling Targets : " + clearedCullingTargets);
            Debug.Log("Cleared Camera Zones : " + clearedCameraZones);
        }

#if UNITY_EDITOR

        public bool DrawGeometryTreeGizmo; // 是否绘制几何树 Gizmo
        public bool DrawCameraZones; // 是否绘制 CameraZones

        private BinaryTreeDrawer _treeDrawer; // 辅助绘制二叉树的工具

        /// <summary>
        /// 编辑器下绘制 Gizmo
        /// </summary>
        private void OnDrawGizmos()
        {
            if (_treeDrawer == null)
                _treeDrawer = new BinaryTreeDrawer();

            // 绘制几何树
            if (DrawGeometryTreeGizmo && _geometryTree != null)
            {
                _treeDrawer.Color = Color.blue;
                _treeDrawer.DrawTreeGizmos(_geometryTree.Root);
            }

            // 绘制 CameraZone 可见性树
            if (DrawCameraZones)
            {
                if (_cameraZones != null)
                {
                    foreach (var zone in _cameraZones)
                    {
                        if (zone.VisibilityTree != null)
                        {
                            _treeDrawer.Color = Color.white;
                            _treeDrawer.DrawTreeGizmos(zone.VisibilityTree.Root);
                        }
                    }
                }
            }
        }

#endif
    }
}
