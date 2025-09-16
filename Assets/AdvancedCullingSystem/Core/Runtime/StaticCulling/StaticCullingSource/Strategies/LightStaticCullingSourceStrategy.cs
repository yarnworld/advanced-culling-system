using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Static
{
    /// <summary>
    /// Light 类型的静态剔除 Source 策略
    /// 
    /// 该策略用于将 Unity 的 Light 组件
    /// 转换为可参与静态剔除系统的 CullingTarget。
    /// 
    /// 主要目的是：
    /// - 对灯光进行可见性剔除
    /// - 减少不必要的灯光计算开销
    /// </summary>
    public class LightStaticCullingSourceStrategy : IStaticCullingSourceStrategy
    {
        /// <summary>
        /// 灯光 Source 的本地空间包围盒
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
        /// Source 所属的上下文对象（宿主 GameObject）
        /// </summary>
        [SerializeField]
        private GameObject _context;

        /// <summary>
        /// 该 Source 关联的 Light 组件
        /// </summary>
        [SerializeField]
        private Light _light;

        /// <summary>
        /// Light 的本地包围盒
        /// </summary>
        [SerializeField]
        private Bounds _localBounds;

        /// <summary>
        /// 构造函数
        /// 
        /// 根据已有的 LightCullingTarget 或 Light 组件，
        /// 自动初始化灯光的本地包围盒
        /// </summary>
        public LightStaticCullingSourceStrategy(GameObject context)
        {
            _context = context;

            // 获取 Light 组件
            _light = _context.GetComponent<Light>();

            // 如果已存在 LightCullingTarget，则复用其 Bounds
            LightCullingTarget target = _context.GetComponent<LightCullingTarget>();

            if (target != null)
            {
                // 将世界空间 Bounds 转换为本地空间 Bounds
                _localBounds = new Bounds(
                    target.Bounds.center - target.transform.position,
                    target.Bounds.size);
            }
            else if (_light != null)
            {
                // 默认初始化一个最小的本地包围盒
                _localBounds = new Bounds
                {
                    center = Vector3.zero,
                    size = Vector3.one
                };

                // 对点光源，根据其 range 设置包围盒大小
                if (_light.type == LightType.Point)
                    _localBounds.size = _light.range * Vector3.one;
            }
        }

        /// <summary>
        /// 校验 Light Source 是否合法
        /// </summary>
        public bool Validate(out string errorMessage)
        {
            // 确保上下文对象上存在 Light 组件
            _light = _context.GetComponent<Light>();

            if (_light == null)
            {
                errorMessage = "Light component not found";
                return false;
            }

            errorMessage = "";
            return true;
        }

        /// <summary>
        /// 获取灯光在世界空间中的包围盒
        /// </summary>
        public bool TryGetBounds(out Bounds bounds)
        {
            bounds = _localBounds;

            // 将本地包围盒转换为世界空间
            bounds.center += _context.transform.position;

            return true;
        }

        /// <summary>
        /// 创建用于运行时的 LightCullingTarget
        /// </summary>
        public CullingTarget CreateCullingTarget()
        {
            LightCullingTarget target = _context.gameObject.AddComponent<LightCullingTarget>();

            TryGetBounds(out Bounds bounds);

            target.Bounds = bounds;

            return target;
        }

        /// <summary>
        /// Baking 前准备阶段
        /// 
        /// 灯光 Source 不需要额外的 Baking 预处理
        /// </summary>
        public void PrepareForBaking()
        {

        }

        /// <summary>
        /// Baking 后清理阶段
        /// 
        /// 灯光 Source 不需要额外的清理逻辑
        /// </summary>
        public void ClearAfterBaking()
        {

        }
    }
}
