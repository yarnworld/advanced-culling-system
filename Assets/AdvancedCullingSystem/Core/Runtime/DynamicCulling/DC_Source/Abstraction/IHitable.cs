using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    /// <summary>
    /// 可被射线命中的接口
    /// DC_Camera 在射线检测命中对象时调用 OnHit() 方法
    /// </summary>
    public interface IHitable
    {
        /// <summary>
        /// 射线命中回调
        /// 触发对象状态更新（如刷新生命周期、显示对象等）
        /// </summary>
        void OnHit();
    }
}