using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    /// <summary>
    /// 动态剔除目标接口
    /// 定义了动态剔除系统中可被剔除或显示的对象的基本属性和行为
    /// </summary>
    public interface ICullingTarget
    {
        /// <summary>
        /// 获取目标对应的 GameObject
        /// </summary>
        GameObject GameObject { get; }

        /// <summary>
        /// 获取目标的包围盒，用于剔除计算
        /// </summary>
        Bounds Bounds { get; }

        /// <summary>
        /// 将对象设置为可见状态
        /// 在剔除系统判断对象可见时调用
        /// </summary>
        void MakeVisible();

        /// <summary>
        /// 将对象设置为不可见状态
        /// 在剔除系统判断对象不可见时调用
        /// </summary>
        void MakeInvisible();
    }
}