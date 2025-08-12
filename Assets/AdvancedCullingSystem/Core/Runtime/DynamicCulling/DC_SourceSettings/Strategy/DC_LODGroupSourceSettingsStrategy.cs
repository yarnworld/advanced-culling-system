using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    /// <summary>
    /// LODGroup 类型的 SourceSettings 策略
    /// 用于处理 LODGroup 对象的动态剔除逻辑
    /// 可选择是否保留阴影
    /// </summary>
    public class DC_LODGroupSourceSettingsStrategy : IDC_SourceSettingsStrategy
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

        [SerializeField]
        private DC_SourceSettings _context; // 持有的 SourceSettings 对象上下文

        [SerializeField]
        private CullingMethod _cullingMethod;

        [SerializeField]
        private LODGroup _group;

        [SerializeField]
        private Renderer[] _renderers;

        [SerializeField]
        private MeshCollider[] _colliders;

        [SerializeField]
        private Bounds _bounds;

        [SerializeField]
        private bool _convexCollider;

        [SerializeField]
        private bool _rigibodiesChecked;


        public DC_LODGroupSourceSettingsStrategy(DC_SourceSettings context)
        {
            _context = context;
        }

        /// <summary>
        /// 准备剔除，创建 MeshCollider
        /// </summary>
        public void PrepareForCulling()
        {
            if (ReadyForCulling)
                return;

            _colliders = _group.GetLODs()[0].renderers
                .Where(r => (r != null && IsCompatibleRenderer(r)))
                .Select(r =>
                {
                    GameObject go = new GameObject("DC_Collider");

                    go.transform.parent = r.transform;
                    go.layer = _context.CullingLayer;
                    go.transform.localPosition = Vector3.zero;
                    go.transform.localEulerAngles = Vector3.zero;
                    go.transform.localScale = Vector3.one;

                    MeshCollider collider = go.AddComponent<MeshCollider>();
                    collider.sharedMesh = r.GetComponent<MeshFilter>().sharedMesh;

                    collider.convex = _convexCollider;

                    return collider;
                }).ToArray();

            ReadyForCulling = true;
        }

        /// <summary>
        /// 清理数据，销毁创建的 Collider
        /// </summary>
        public void ClearData()
        {
            if (!ReadyForCulling)
                return;

            for (int i = 0; i < _colliders.Length; i++)
            {
                Collider collider = _colliders[i];

                if (collider == null || collider.gameObject == null)
                    continue;

                UnityEngine.Object.DestroyImmediate(collider.gameObject);
            }
            _colliders = null;

            ReadyForCulling = false;
        }

        /// <summary>
        /// 获取对象包围盒
        /// </summary>
        public bool TryGetBounds(ref Bounds bounds)
        {
            if (_renderers != null)
            {
                bounds = _bounds;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 创建 CullingTarget，根据剔除方法返回不同类型
        /// </summary>
        public ICullingTarget CreateCullingTarget()
        {
            if (CullingMethod == CullingMethod.KeepShadows)
                return new DC_LODGroupShadowsTarget(_group, _renderers, _bounds);

            return new DC_LODGroupTarget(_group, _renderers, _bounds);
        }

        /// <summary>
        /// 返回 Collider，用于射线检测
        /// </summary>
        public IEnumerable<Collider> GetColliders()
        {
            if (_colliders == null)
                yield break;

            for (int i = 0; i < _colliders.Length; i++)
                yield return _colliders[i];
        }

        /// <summary>
        /// 检查兼容性并获取 LODGroup 及 Renderer 组件
        /// </summary>
        public bool CheckCompatibilityAndGetComponents(out string incompatibilityReason)
        {
            if (_group == null)
            {
                if (!_context.TryGetComponent(out _group))
                {
                    incompatibilityReason = "LODGroup not found";
                    return false;
                }
            }

            if (_renderers == null)
            {
                LOD[] lods = _group.GetLODs();

                int count = lods.Count(IsCompatibleRenderer);

                if (count == 0)
                {
                    incompatibilityReason = "Can't find any compatible renderer";
                    return false;
                }

                _renderers = new Renderer[count];
                _bounds = new Bounds(_group.transform.position, Vector3.zero);

                int idx = 0;
                for (int i = 0; i < lods.Length; i++)
                {
                    Renderer[] lodRenderers = lods[i].renderers;

                    for (int c = 0; c < lodRenderers.Length; c++)
                    {
                        Renderer renderer = lodRenderers[c];

                        if (renderer != null && IsCompatibleRenderer(renderer))
                        {
                            _renderers[idx++] = renderer;
                            _bounds.Encapsulate(renderer.bounds);
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] == null)
                    {
                        incompatibilityReason = "Missing renderer at index : " + i;
                        return false;
                    }
                }
            }

            if (!_rigibodiesChecked)
            {
                foreach (var rb in _context.GetComponentsInParent<Rigidbody>())
                {
                    if (!rb.isKinematic)
                    {
                        _convexCollider = true;
                        break;
                    }
                }

                _rigibodiesChecked = true;
            }

            incompatibilityReason = "";
            return true;
        }

        /// <summary>
        /// 检查 Renderer 是否兼容
        /// </summary>
        private bool IsCompatibleRenderer(Renderer renderer)
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();

            return filter != null && filter.sharedMesh != null;
        }
    }
}
