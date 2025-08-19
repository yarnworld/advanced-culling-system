using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    /// <summary>
    /// 单目标剔除源
    /// 仅关联一个 ICullingTarget，对其可见性进行管理
    /// </summary>
    public class DC_SingleSource : DC_Source
    {
        // 当前关联的剔除目标
        private ICullingTarget _cullingTarget;

        // 当前可见性状态
        private bool _visible;

        /// <summary>
        /// 设置剔除目标
        /// 初始时隐藏目标
        /// </summary>
        /// <param name="target">剔除目标</param>
        public override void SetCullingTarget(ICullingTarget target)
        {
            _cullingTarget = target;
            _cullingTarget.MakeInvisible(); // 初始化为不可见
        }

        /// <summary>
        /// 移除剔除目标
        /// 同时禁用自身并销毁 GameObject
        /// </summary>
        /// <param name="target">剔除目标</param>
        public override void RemoveCullingTarget(ICullingTarget target)
        {
            enabled = false;
            Destroy(gameObject); // 释放对象
        }

        /// <summary>
        /// 射线命中处理逻辑
        /// 将关联目标显示，并标记为可见
        /// </summary>
        protected override void OnHitInternal()
        {
            if (_visible)
                return;

            _cullingTarget.MakeVisible();
            _visible = true;
        }

        /// <summary>
        /// 生命周期到期或禁用处理逻辑
        /// 将关联目标隐藏，并标记为不可见
        /// </summary>
        protected override void OnTimeout()
        {
            _cullingTarget.MakeInvisible();
            _visible = false;
        }
    }
}