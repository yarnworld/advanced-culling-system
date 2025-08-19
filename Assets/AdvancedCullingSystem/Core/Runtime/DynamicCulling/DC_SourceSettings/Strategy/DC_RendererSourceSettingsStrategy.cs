using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    /// <summary>
    /// Renderer 类型的 SourceSettings 策略
    /// 用于处理单个 MeshRenderer 对象的动态剔除逻辑
    /// 可选择是否保留阴影，并根据 Rigidbody 自动决定是否使用凸形碰撞体
    /// </summary>
    [Serializable]
    public class DC_RendererSourceSettingsStrategy : IDC_SourceSettingsStrategy
    {
        [field: SerializeField]
        /// <summary>
        /// 是否已准备好进行剔除
        /// </summary>
        public bool ReadyForCulling { get; private set; }

        /// <summary>
        /// 剔除方法（FullDisable / KeepShadows）
        /// </summary>
        public CullingMethod CullingMethod
        {
            get { return _cullingMethod; }
            set { _cullingMethod = value; }
        }

        /// <summary>
        /// 是否使用凸形 MeshCollider
        /// </summary>
        public bool ConvexCollider
        {
            get { return _convexCollider; }
            set { _convexCollider = value; }
        }

        [SerializeField]
        private DC_SourceSettings _context; // 持有的 SourceSettings 对象上下文

        [SerializeField]
        private CullingMethod _cullingMethod;

        [SerializeField]
        private bool _convexCollider;

        [SerializeField]
        private MeshRenderer _renderer;

        [SerializeField]
        private Mesh _mesh;

        [SerializeField]
        private MeshCollider _collider;

        [SerializeField]
        private bool _rigibodiesChecked;


        public DC_RendererSourceSettingsStrategy(DC_SourceSettings context)
        {
            _context = context;
        }

        /// <summary>
        /// 检查兼容性并获取 MeshRenderer 和 Mesh
        /// </summary>
        public bool CheckCompatibilityAndGetComponents(out string incompatibilityReason)
        {
            if (_renderer == null)
            {
                if (!_context.TryGetComponent(out _renderer))
                {
                    incompatibilityReason = "MeshRenderer not found";
                    return false;
                }
            }

            if (_mesh == null)
            {
                MeshFilter filter = _context.GetComponent<MeshFilter>();

                if (filter == null)
                {
                    incompatibilityReason = "MeshFilter not found";
                    return false;
                }

                _mesh = filter.sharedMesh;

                if (_mesh == null)
                {
                    incompatibilityReason = "Mesh not found";
                    return false;
                }
            }

            // 检查父级 Rigidbody 是否为非 kinematic，如果是，则需要凸碰撞体
            if (!_rigibodiesChecked)
            {
                foreach (var rb in _context.GetComponentsInParent<Rigidbody>())
                {
                    if (!rb.isKinematic)
                    {
                        ConvexCollider = true;
                        break;
                    }
                }

                _rigibodiesChecked = true;
            }

            incompatibilityReason = "";
            return true;
        }

        /// <summary>
        /// 为剔除准备 MeshCollider
        /// </summary>
        public void PrepareForCulling()
        {
            if (ReadyForCulling)
                return;

            GameObject go = new GameObject("DC_Collider");

            go.transform.parent = _renderer.transform;
            go.layer = _context.CullingLayer;

            go.transform.localPosition = Vector3.zero;
            go.transform.localEulerAngles = Vector3.zero;
            go.transform.localScale = Vector3.one;

            _collider = go.AddComponent<MeshCollider>();
            _collider.sharedMesh = _mesh;

            if (ConvexCollider)
                _collider.convex = true;

            ReadyForCulling = true;
        }

        /// <summary>
        /// 清理数据，销毁 MeshCollider
        /// </summary>
        public void ClearData()
        {
            if (!ReadyForCulling)
                return;

            if (_collider != null)
                UnityEngine.Object.DestroyImmediate(_collider.gameObject);

            _collider = null;
            ReadyForCulling = false;
        }

        /// <summary>
        /// 获取 Renderer 包围盒
        /// </summary>
        public bool TryGetBounds(ref Bounds bounds)
        {
            if (_renderer != null)
            {
                bounds = _renderer.bounds;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 创建 CullingTarget，根据剔除策略返回 DC_RendererTarget 或 DC_RendererShadowsTarget
        /// </summary>
        public ICullingTarget CreateCullingTarget()
        {
            if (CullingMethod == CullingMethod.KeepShadows)
                return new DC_RendererShadowsTarget(_renderer);

            return new DC_RendererTarget(_renderer);
        }

        /// <summary>
        /// 返回用于射线检测的 Collider
        /// </summary>
        public IEnumerable<Collider> GetColliders()
        {
            if (_collider == null)
                yield break;

            yield return _collider;
        }
    }
}
