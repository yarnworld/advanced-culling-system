using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    /// <summary>
    /// 自定义 SourceSettings 策略
    /// 用于处理非 Renderer / LODGroup 的自定义对象
    /// 可绑定可见/不可见事件，实现自定义逻辑
    /// </summary>
    [Serializable]
    public class DC_CustomSourceSettingsStrategy : IDC_SourceSettingsStrategy
    {
        [field: SerializeField]
        /// <summary>
        /// 是否已经准备好进行剔除
        /// </summary>
        public bool ReadyForCulling { get; private set; }

        /// <summary>
        /// 自定义的局部包围盒
        /// </summary>
        public Bounds LocalBounds
        {
            get { return _localBounds; }
            set { _localBounds = value; }
        }

        /// <summary>
        /// 可见事件
        /// </summary>
        public DC_CustomTargetEvent OnVisibleEvent
        {
            get { return _onVisible; }
        }

        /// <summary>
        /// 不可见事件
        /// </summary>
        public DC_CustomTargetEvent OnInvisibleEvent
        {
            get { return _onInvisible; }
        }

        [SerializeField]
        private DC_SourceSettings _context; // 持有的 SourceSettings 对象上下文

        [SerializeField]
        private Bounds _localBounds;

        [SerializeField]
        private bool _alignRotation; // 是否对齐旋转

        [SerializeField]
        private List<Renderer> _renderers;

        [SerializeField]
        private DC_CustomTargetEvent _onVisible;

        [SerializeField]
        private DC_CustomTargetEvent _onInvisible;

        [SerializeField]
        private BoxCollider _collider;


        public DC_CustomSourceSettingsStrategy(DC_SourceSettings context)
        {
            _context = context;
            _localBounds = new Bounds(Vector3.zero, Vector3.one);

            _renderers = new List<Renderer>();

            _onVisible = new DC_CustomTargetEvent();
            _onInvisible = new DC_CustomTargetEvent();
        }

        /// <summary>
        /// 检查兼容性
        /// </summary>
        public bool CheckCompatibilityAndGetComponents(out string incompatibilityReason)
        {
            incompatibilityReason = "";
            return true;
        }

        /// <summary>
        /// 准备剔除，创建 BoxCollider
        /// </summary>
        public void PrepareForCulling()
        {
            if (ReadyForCulling)
                return;

            GameObject go = new GameObject("DC_Collider");
            go.transform.localScale = Vector3.one;
            go.transform.parent = _context.transform;
            go.layer = _context.CullingLayer;
            go.transform.localPosition = Vector3.zero;

            if (_alignRotation)
                go.transform.localEulerAngles = Vector3.zero;

            _collider = go.AddComponent<BoxCollider>();
            _collider.center = _localBounds.center;
            _collider.size = _localBounds.size;

            ReadyForCulling = true;
        }

        /// <summary>
        /// 清理数据，销毁创建的 Collider
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
        /// 获取对象包围盒
        /// </summary>
        public bool TryGetBounds(ref Bounds bounds)
        {
            bounds.center = _context.transform.position + _localBounds.center;
            bounds.size = _localBounds.size;

            return true;
        }

        /// <summary>
        /// 创建自定义剔除目标
        /// </summary>
        public ICullingTarget CreateCullingTarget()
        {
            Bounds bounds = new Bounds();
            TryGetBounds(ref bounds);

            RegisterRenderersInEvents();

            return new DC_CustomTarget(_context.gameObject, bounds, _onVisible, _onInvisible);
        }

        /// <summary>
        /// 获取参与剔除的 Collider
        /// </summary>
        public IEnumerable<Collider> GetColliders()
        {
            if (_collider == null)
                yield break;

            yield return _collider;
        }

        /// <summary>
        /// 根据子 Renderer 对齐本地 Bounds
        /// </summary>
        public void AlignLocalBoundsByChildRenderers()
        {
            Renderer[] renderers = _context.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            bounds.center -= _context.transform.position;
            _localBounds = bounds;
        }

        /// <summary>
        /// 根据子 Collider 对齐本地 Bounds
        /// </summary>
        public void AlignLocalBoundsByChildColliders()
        {
            Collider[] colliders = _context.GetComponentsInChildren<Collider>();
            if (colliders == null || colliders.Length == 0) return;

            Bounds bounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
                bounds.Encapsulate(colliders[i].bounds);

            bounds.center -= _context.transform.position;
            _localBounds = bounds;
        }

        /// <summary>
        /// 将子 Renderer 添加到列表
        /// </summary>
        public void AddChildRenderersToList()
        {
            foreach (var renderer in _context.GetComponentsInChildren<Renderer>())
            {
                if (!_renderers.Contains(renderer))
                    _renderers.Add(renderer);
            }
        }

        /// <summary>
        /// 从列表中移除子 Renderer
        /// </summary>
        public void RemoveChildRenderersFromList()
        {
            foreach (var renderer in _context.GetComponentsInChildren<Renderer>())
                _renderers.Remove(renderer);
        }

        /// <summary>
        /// 将 Renderer 注册到可见/不可见事件
        /// </summary>
        private void RegisterRenderersInEvents()
        {
            if (_renderers == null)
                return;

            foreach(var renderer in _renderers)
            {
                if (renderer == null)
                    continue;

                _onVisible.AddListener((t) => renderer.enabled = true);
                _onInvisible.AddListener((t) => renderer.enabled = false);
            }
        }
    }
}
