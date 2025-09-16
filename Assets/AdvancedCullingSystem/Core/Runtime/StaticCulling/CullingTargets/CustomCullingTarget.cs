using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace NGS.AdvancedCullingSystem.Static
{
    /// <summary>
    /// 自定义剔除目标（CustomCullingTarget）
    /// 继承自 CullingTarget，可以在可见/不可见时触发自定义事件
    /// </summary>
    public class CustomCullingTarget : CullingTarget
    {
        /// <summary>
        /// 对象变为可见时触发的事件
        /// </summary>
        public CustomTargetEvent OnVisible
        {
            get
            {
                return _onVisible;
            }
        }

        /// <summary>
        /// 对象变为不可见时触发的事件
        /// </summary>
        public CustomTargetEvent OnInvisible
        {
            get
            {
                return _onInvisible;
            }
        }

        /// <summary>
        /// 标记该对象是否是遮挡体（Occluder）
        /// </summary>
        [field : SerializeField, HideInInspector]
        public bool IsOccluder { get; set; }

        // 内部存储可见/不可见事件
        [SerializeField]
        private CustomTargetEvent _onVisible;

        [SerializeField]
        private CustomTargetEvent _onInvisible;

        /// <summary>
        /// Awake 生命周期回调，确保事件实例化
        /// </summary>
        private void Awake()
        {
            if (_onVisible == null)
                _onVisible = new CustomTargetEvent();

            if (_onInvisible == null)
                _onInvisible = new CustomTargetEvent();
        }

        /// <summary>
        /// 设置对象可见/不可见时的事件回调
        /// </summary>
        /// <param name="onVisible">可见事件</param>
        /// <param name="onInvisible">不可见事件</param>
        public void SetActions(CustomTargetEvent onVisible, CustomTargetEvent onInvisible)
        {
            _onVisible = onVisible;
            _onInvisible = onInvisible;
        }

        /// <summary>
        /// 当对象变为可见时调用，触发自定义事件
        /// </summary>
        protected override void MakeVisible()
        {
            _onVisible.Invoke(this);
        }

        /// <summary>
        /// 当对象变为不可见时调用，触发自定义事件
        /// </summary>
        protected override void MakeInvisible()
        {
            _onInvisible.Invoke(this);
        }
    }

    /// <summary>
    /// 自定义事件类型，支持传入 CullingTarget 作为参数
    /// </summary>
    [System.Serializable]
    public class CustomTargetEvent : UnityEvent<CullingTarget>
    {
        
    }
}
