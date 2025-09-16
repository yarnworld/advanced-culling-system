using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Static
{
    /// <summary>
    /// LODGroup 类型的静态剔除 Source 策略
    /// 
    /// 该策略用于将 Unity 的 LODGroup
    /// 转换为可参与静态剔除系统的 CullingTarget，
    /// 并支持：
    /// - 多 LOD Renderer 的统一剔除
    /// - 基于 LOD0 自动生成遮挡用 Collider
    /// </summary>
    public class LODGroupStaticCullingSourceStrategy : IStaticCullingSourceStrategy
    {
        /// <summary>
        /// Source 所属的上下文对象（宿主 GameObject）
        /// </summary>
        [SerializeField]
        private GameObject _context;

        /// <summary>
        /// LODGroup 剔除方式
        /// （例如按整体剔除、按 Renderer 剔除等）
        /// </summary>
        [SerializeField]
        private CullingMethod _cullingMethod;

        /// <summary>
        /// 是否作为遮挡体（Occluder）参与剔除计算
        /// </summary>
        [SerializeField]
        private bool _isOccluder;

        /// <summary>
        /// 关联的 LODGroup 组件
        /// </summary>
        [SerializeField]
        private LODGroup _lodGroup;

        /// <summary>
        /// 所有参与剔除的 Renderer 列表
        /// （来自 LODGroup 的所有 LOD 层级）
        /// </summary>
        [SerializeField]
        private List<Renderer> _renderers;

        /// <summary>
        /// 缓存的本地包围盒
        /// 
        /// 使用 Nullable 是为了支持延迟计算
        /// </summary>
        [SerializeField]
        private Bounds? _localBounds;

        /// <summary>
        /// Baking 阶段动态创建的 Collider
        /// （通常基于 LOD0 的 Mesh）
        /// </summary>
        [SerializeField]
        private List<Collider> _colliders;


        /// <summary>
        /// 构造函数
        /// 
        /// 从已有的 LODGroupCullingTarget 中
        /// 读取是否作为 Occluder 的配置
        /// </summary>
        public LODGroupStaticCullingSourceStrategy(GameObject context)
        {
            _context = context;

            LODGroupCullingTarget target = context.GetComponent<LODGroupCullingTarget>();

            if (target != null)
                _isOccluder = target.IsOccluder;
            else
                _isOccluder = true;
        }

        /// <summary>
        /// 校验 LODGroup Source 的合法性
        /// </summary>
        public bool Validate(out string errorMessage)
        {
            _lodGroup = _context.GetComponent<LODGroup>();

            // 必须存在 LODGroup 组件
            if (_lodGroup == null)
            {
                errorMessage = "LODGroup not found";
                return false;
            }

            // 如果作为遮挡体，则要求 LOD0 中
            // 至少存在一个可用于生成 Collider 的 Renderer
            if (_isOccluder)
            {
                LOD lod = _lodGroup.GetLODs()[0];
                bool containsRenderersForColliders = false;

                for (int i = 0; i < lod.renderers.Length; i++)
                {
                    if (CheckRenderer(lod.renderers[i]))
                    {
                        containsRenderersForColliders = true;
                        break;
                    }
                }

                if (!containsRenderersForColliders)
                {
                    errorMessage = "Not found valid Renderers on LOD0 for creating colliders";
                    return false;
                }
            }

            // 收集所有 LOD 层级中的有效 Renderer
            CollectRenderers();

            if (_renderers.Count == 0)
            {
                errorMessage = "Not found valid Renderers";
                return false;
            }

            errorMessage = "";
            return true;
        }

        /// <summary>
        /// 获取 LODGroup 在世界空间中的包围盒
        /// </summary>
        public bool TryGetBounds(out Bounds bounds)
        {
            // 如果已经缓存过本地 Bounds，直接使用
            if (_localBounds.HasValue)
            {
                bounds = _localBounds.Value;
                bounds.center += _context.transform.position;
                return true;
            }

            if (_renderers == null || _renderers.Count == 0)
            {
                bounds = default;
                return false;
            }

            // 手动计算所有 Renderer 的世界空间包围盒并集
            Vector3 min = Vector3.one * float.MaxValue;
            Vector3 max = -Vector3.one * float.MaxValue;

            for (int i = 0; i < _renderers.Count; i++)
            {
                Bounds rBounds = _renderers[i].bounds;

                Vector3 rMin = rBounds.min;
                Vector3 rMax = rBounds.max;

                min.x = Mathf.Min(min.x, rMin.x);
                min.y = Mathf.Min(min.y, rMin.y);
                min.z = Mathf.Min(min.z, rMin.z);

                max.x = Mathf.Max(max.x, rMax.x);
                max.y = Mathf.Max(max.y, rMax.y);
                max.z = Mathf.Max(max.z, rMax.z);
            }

            // 转换为本地空间 Bounds 并缓存
            _localBounds = new Bounds(
                min + ((max - min) / 2) - _context.transform.position,
                max - min);

            bounds = _localBounds.Value;
            bounds.center += _context.transform.position;

            return true;
        }

        /// <summary>
        /// 创建运行时使用的 LODGroupCullingTarget
        /// </summary>
        public CullingTarget CreateCullingTarget()
        {
            LODGroupCullingTarget cullingTarget = _context.AddComponent<LODGroupCullingTarget>();

            CollectRenderers();

            // 重置 Bounds 缓存，确保重新计算
            _localBounds = null;

            TryGetBounds(out Bounds bounds);

            cullingTarget.Bounds = bounds;
            cullingTarget.SetRenderers(_renderers);
            cullingTarget.CullingMethod = _cullingMethod;
            cullingTarget.IsOccluder = _isOccluder;

            return cullingTarget;
        }

        /// <summary>
        /// Baking 前准备阶段
        /// 
        /// 如果该 LODGroup 作为 Occluder，
        /// 则基于 LOD0 的 Mesh 动态创建 MeshCollider
        /// </summary>
        public void PrepareForBaking()
        {
            if (!_isOccluder)
                return;

            LOD lod = _lodGroup.GetLODs()[0];

            for (int c = 0; c < lod.renderers.Length; c++)
            {
                Renderer renderer = lod.renderers[c];

                if (CheckRenderer(renderer))
                {
                    Collider collider = CreateCollider(renderer.GetComponent<MeshFilter>());

                    if (_colliders == null)
                        _colliders = new List<Collider>();

                    _colliders.Add(collider);
                }
            }
        }

        /// <summary>
        /// Baking 完成后的清理阶段
        /// 
        /// 删除动态创建的 Collider
        /// </summary>
        public void ClearAfterBaking()
        {
            if (_colliders == null || _colliders.Count == 0)
                return;

            for (int i = 0; i < _colliders.Count; i++)
                UnityEngine.Object.DestroyImmediate(_colliders[i].gameObject);

            _colliders.Clear();
        }


        /// <summary>
        /// 收集 LODGroup 中所有有效的 Renderer
        /// </summary>
        private void CollectRenderers()
        {
            if (_renderers == null)
                _renderers = new List<Renderer>();
            else
                _renderers.Clear();

            LOD[] lods = _lodGroup.GetLODs();

            for (int i = 0; i < _lodGroup.lodCount; i++)
            {
                LOD lod = lods[i];

                for (int c = 0; c < lod.renderers.Length; c++)
                {
                    Renderer renderer = lod.renderers[c];

                    if (CheckRenderer(renderer))
                        _renderers.Add(renderer);
                }
            }
        }

        /// <summary>
        /// 检查 Renderer 是否可用于剔除与 Collider 生成
        /// </summary>
        private bool CheckRenderer(Renderer renderer)
        {
            if (renderer == null)
                return false;

            MeshFilter filter = renderer.GetComponent<MeshFilter>();

            if (filter == null || filter.sharedMesh == null)
                return false;

            return true;
        }

        /// <summary>
        /// 根据 MeshFilter 创建用于 Baking 的 MeshCollider
        /// </summary>
        public Collider CreateCollider(MeshFilter filter)
        {
            Mesh mesh = filter.sharedMesh;

            GameObject colliderGo = new GameObject("SC_Collider");

            colliderGo.layer = StaticCullingPreferences.Layer;
            colliderGo.transform.parent = _context.transform;
            colliderGo.transform.localPosition = Vector3.zero;
            colliderGo.transform.localEulerAngles = Vector3.zero;
            colliderGo.transform.localScale = Vector3.one;

            MeshCollider collider = colliderGo.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;

            return collider;
        }
    }
}
