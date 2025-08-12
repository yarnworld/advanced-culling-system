using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NGS.AdvancedCullingSystem.Utils;

namespace NGS.AdvancedCullingSystem.Static
{
    /// <summary>
    /// 自定义静态剔除 Source 的策略实现
    /// 
    /// 该类允许开发者通过自定义 Bounds、回调事件以及 Collider，
    /// 将任意 GameObject 作为静态剔除系统中的 Source 使用。
    /// 
    /// 常用于：
    /// - 非 MeshRenderer 的对象
    /// - 逻辑对象 / 触发器
    /// - 需要手动控制可见 / 不可见行为的对象
    /// </summary>
    public class CustomStaticCullingSourceStrategy : IStaticCullingSourceStrategy
    {
        /// <summary>
        /// Source 的本地空间包围盒
        /// 
        /// 实际用于剔除计算的世界空间 Bounds
        /// 会在 TryGetBounds 中转换得到
        /// </summary>
        public Bounds LocalBounds
        {
            get
            {
                return _localBounds;
            }
            set
            {
                _localBounds = value;
            }
        }

        /// <summary>
        /// 该 Source 所属的上下文对象（宿主 GameObject）
        /// </summary>
        [SerializeField]
        private GameObject _context;

        /// <summary>
        /// 是否作为遮挡体（Occluder）参与剔除计算
        /// </summary>
        [SerializeField]
        private bool _isOccluder;

        /// <summary>
        /// Source 的本地包围盒
        /// </summary>
        [SerializeField]
        private Bounds _localBounds;

        /// <summary>
        /// 当目标变为“可见”时触发的事件
        /// </summary>
        [SerializeField]
        private CustomTargetEvent _onVisible;

        /// <summary>
        /// 当目标变为“不可见”时触发的事件
        /// </summary>
        [SerializeField]
        private CustomTargetEvent _onInvisible;

        /// <summary>
        /// 用于生成遮挡体的源 Collider 列表
        /// 仅在 IsOccluder = true 时使用
        /// </summary>
        [SerializeField]
        private List<Collider> _colliders;

        /// <summary>
        /// Baking 过程中动态创建的 Collider 实例
        /// 用于在 Baking 完成后统一清理
        /// </summary>
        [SerializeField]
        private List<Collider> _createdColliders;


        /// <summary>
        /// 构造函数
        /// 
        /// 根据 context 上是否已存在 CustomCullingTarget，
        /// 自动初始化 Bounds、事件回调和 Occluder 配置
        /// </summary>
        public CustomStaticCullingSourceStrategy(GameObject context)
        {
            _context = context;

            CustomCullingTarget target = context.GetComponent<CustomCullingTarget>();

            if (target != null)
            {
                // 将已有 CullingTarget 的世界空间 Bounds
                // 转换为相对于 Transform 的本地 Bounds
                _localBounds = new Bounds(
                    target.Bounds.center - target.transform.position,
                    target.Bounds.size);

                _onVisible = target.OnVisible;
                _onInvisible = target.OnInvisible;
                _isOccluder = target.IsOccluder;
            }
            else
            {
                // 若不存在已有配置，则使用默认值
                _localBounds = new Bounds(Vector3.zero, Vector3.one * 3);
                _isOccluder = false;
            }

            // 初始化可见 / 不可见事件
            _onVisible = new CustomTargetEvent();
            _onInvisible = new CustomTargetEvent();

            // 初始化 Collider 列表
            _colliders = new List<Collider>();
            _createdColliders = new List<Collider>();
        }

        /// <summary>
        /// 获取 Source 在世界空间中的包围盒
        /// </summary>
        public bool TryGetBounds(out Bounds bounds)
        {
            bounds = _localBounds;
            bounds.center += _context.transform.position;

            return true;
        }

        /// <summary>
        /// 校验 Source 配置是否合法
        /// </summary>
        public bool Validate(out string errorMessage)
        {
            // 可见 / 不可见事件至少要有一个
            if (_onVisible == null && _onInvisible == null)
            {
                errorMessage = "Visible and Invisible actions not assigned";
                return false;
            }

            // 如果作为遮挡体，则必须配置 Collider
            if (_isOccluder)
            {
                if (_colliders == null || _colliders.Count == 0)
                {
                    errorMessage = "Source marked as occluder but colliders not assigned";
                    return false;
                }
            }

            errorMessage = "";
            return true;
        }

        /// <summary>
        /// 创建运行时使用的 CullingTarget
        /// 
        /// 该方法通常在 Baking 阶段调用，
        /// 将 Source 数据转换为真正参与运行时剔除的对象
        /// </summary>
        public CullingTarget CreateCullingTarget()
        {
            CustomCullingTarget cullingTarget = _context.AddComponent<CustomCullingTarget>();

            TryGetBounds(out Bounds bounds);

            cullingTarget.Bounds = bounds;
            cullingTarget.SetActions(_onVisible, _onInvisible);
            cullingTarget.IsOccluder = _isOccluder;

            return cullingTarget;
        }

        /// <summary>
        /// Baking 开始前的准备工作
        /// 
        /// 如果该 Source 是 Occluder，
        /// 则复制 Collider 用于 Baking 阶段的遮挡计算
        /// </summary>
        public void PrepareForBaking()
        {
            if (!_isOccluder)
                return;

            if (_colliders == null || _colliders.Count == 0)
                return;

            _createdColliders.Clear();

            for (int i = 0; i < _colliders.Count; i++)
            {
                _createdColliders.Add(CreateCollider(_colliders[i]));
            }
        }

        /// <summary>
        /// Baking 完成后的清理逻辑
        /// 
        /// 删除在 Baking 期间动态创建的 Collider 对象，
        /// 避免污染场景
        /// </summary>
        public void ClearAfterBaking()
        {
            if (_createdColliders == null || _createdColliders.Count == 0)
                return;

            for (int i = 0; i < _createdColliders.Count; i++)
                Object.DestroyImmediate(_createdColliders[i].gameObject);

            _createdColliders.Clear();
        }

        /// <summary>
        /// 根据源 Collider 创建一个用于 Baking 的临时 Collider 实例
        /// </summary>
        public Collider CreateCollider(Collider source)
        {
            // 复制 Collider（类型、参数等）
            Collider instance = ColliderUtils.Duplicate(source);
            GameObject instanceGO = instance.gameObject;

            // 同步 Transform 数据
            instanceGO.transform.localPosition = source.transform.position;
            instanceGO.transform.localEulerAngles = source.transform.eulerAngles;
            instanceGO.transform.localScale = source.transform.lossyScale;

            // 设置到静态剔除专用 Layer
            instanceGO.layer = StaticCullingPreferences.Layer;
            instanceGO.transform.parent = _context.transform;

            return instance;
        }
    }
}
