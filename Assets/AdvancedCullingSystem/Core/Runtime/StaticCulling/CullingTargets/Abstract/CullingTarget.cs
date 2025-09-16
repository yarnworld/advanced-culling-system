using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Static
{
    /// <summary>
    /// 抽象剔除目标类（CullingTarget）
    /// 用于静态剔除系统中，管理单个对象的可见性
    /// 子类需实现具体显示和隐藏逻辑
    /// </summary>
    public abstract class CullingTarget : MonoBehaviour
    {
        /// <summary>
        /// 该对象的包围盒（Bounds），用于可见性判断
        /// </summary>
        [field: SerializeField]
        public Bounds Bounds { get; set; }

        /// <summary>
        /// 对象当前是否应该显示
        /// </summary>
        private bool _isVisible;

        /// <summary>
        /// 对象是否已经被标记为显示（避免重复调用 MakeVisible）
        /// </summary>
        private bool _makedVisible;

        /// <summary>
        /// Unity 生命周期回调，在每帧结束时更新对象的可见性状态
        /// </summary>
        private void LateUpdate()
        {
            if (_isVisible)
            {
                // 如果对象应该显示但尚未标记显示
                if (!_makedVisible)
                {
                    MakeVisible();      // 调用子类实现的显示逻辑
                    _makedVisible = true;
                }

                // 重置 _isVisible，等待下一帧判断
                _isVisible = false;

                return;
            }
            else
            {
                // 对象不应该显示，调用隐藏逻辑
                MakeInvisible();

                _makedVisible = false;
                enabled = false; // 禁用脚本，提高性能
            }
        }

        /// <summary>
        /// 外部调用，将对象标记为可见
        /// </summary>
        public void SetVisible()
        {
            if (!_isVisible)
            {
                enabled = true;   // 启用脚本以触发 LateUpdate
                _isVisible = true;
            }
        }

        /// <summary>
        /// 子类需实现的显示逻辑
        /// </summary>
        protected abstract void MakeVisible();

        /// <summary>
        /// 子类需实现的隐藏逻辑
        /// </summary>
        protected abstract void MakeInvisible();
    }
}