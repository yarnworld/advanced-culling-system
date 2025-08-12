using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    /// <summary>
    /// 自定义剔除目标类
    /// 实现 ICullingTarget 接口，可用于将任意 GameObject 绑定到动态剔除系统
    /// 支持自定义可见与不可见事件回调
    /// </summary>
    public class DC_CustomTarget : ICullingTarget
    {
        /// <summary>
        /// 对应的 GameObject
        /// </summary>
        public GameObject GameObject { get; private set; }

        /// <summary>
        /// 对应的包围盒，用于剔除计算
        /// </summary>
        public Bounds Bounds { get; private set; }

        // 可见事件回调
        private DC_CustomTargetEvent _onVisible;

        // 不可见事件回调
        private DC_CustomTargetEvent _onInvisible;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="go">目标 GameObject</param>
        /// <param name="bounds">目标包围盒</param>
        /// <param name="onVisible">可见时回调事件，可为空</param>
        /// <param name="onInvisible">不可见时回调事件，可为空</param>
        public DC_CustomTarget(GameObject go, Bounds bounds,
            DC_CustomTargetEvent onVisible,
            DC_CustomTargetEvent onInvisible)
        {
            GameObject = go;
            Bounds = bounds;

            // 如果传入回调为空，则创建空事件，保证调用安全
            _onVisible = onVisible != null ? onVisible : new DC_CustomTargetEvent();
            _onInvisible = onInvisible != null ? onInvisible : new DC_CustomTargetEvent();
        }

        /// <summary>
        /// 将对象设置为可见
        /// 会触发注册的可见事件
        /// </summary>
        public void MakeVisible()
        {
            _onVisible?.Invoke(this);
        }

        /// <summary>
        /// 将对象设置为不可见
        /// 会触发注册的不可见事件
        /// </summary>
        public void MakeInvisible()
        {
            _onInvisible?.Invoke(this);
        }
    }

    /// <summary>
    /// 自定义剔除目标事件类
    /// UnityEvent 泛型为 DC_CustomTarget，可在 Inspector 中注册回调
    /// </summary>
    [System.Serializable]
    public class DC_CustomTargetEvent : UnityEvent<DC_CustomTarget>
    {

    }
}
