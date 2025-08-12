using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    /// <summary>
    /// 多目标剔除源
    /// 可关联多个 ICullingTarget，对它们统一管理可见性状态
    /// </summary>
    public class DC_MultiSource : DC_Source
    {
        // 当前关联的所有剔除目标
        private List<ICullingTarget> _cullingTargets;

        // 当前可见性状态
        private bool _visible;

        /// <summary>
        /// 初始化
        /// </summary>
        private void Awake()
        {
            _cullingTargets = new List<ICullingTarget>();
        }

        /// <summary>
        /// 设置剔除目标
        /// 当源对象可见时，立即显示目标，否则隐藏目标
        /// </summary>
        /// <param name="target">剔除目标</param>
        public override void SetCullingTarget(ICullingTarget target)
        {
            if (_cullingTargets == null)
                _cullingTargets = new List<ICullingTarget>();

            _cullingTargets.Add(target);

            if (_visible)
                target.MakeVisible();
            else
                target.MakeInvisible();
        }

        /// <summary>
        /// 移除剔除目标
        /// </summary>
        /// <param name="target">剔除目标</param>
        public override void RemoveCullingTarget(ICullingTarget target)
        {
            _cullingTargets.Remove(target);
        }

        /// <summary>
        /// 射线命中处理逻辑
        /// 将所有关联目标显示，并标记可见
        /// </summary>
        protected override void OnHitInternal()
        {
            if (_visible)
                return;

            int i = 0;
            while (i < _cullingTargets.Count)
            {
                try
                {
                    _cullingTargets[i].MakeVisible();
                    i++;
                }
                catch(MissingReferenceException)
                {
                    // 处理目标被销毁的情况
                    RemoveCullingTarget(_cullingTargets[i]);
                }
            }

            _visible = true;
        }

        /// <summary>
        /// 生命周期到期或禁用处理逻辑
        /// 将所有关联目标隐藏，并标记不可见
        /// </summary>
        protected override void OnTimeout()
        {
            int i = 0;
            while (i < _cullingTargets.Count)
            {
                try
                {
                    _cullingTargets[i].MakeInvisible();
                    i++;
                }
                catch (MissingReferenceException)
                {
                    // 处理目标被销毁的情况
                    RemoveCullingTarget(_cullingTargets[i]);
                }
            }

            _visible = false;
        }
    }
}
