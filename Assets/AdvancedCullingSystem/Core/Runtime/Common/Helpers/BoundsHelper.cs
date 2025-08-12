using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem
{
    /// <summary>
    /// Bounds 帮助类，扩展 Unity 自带 Bounds 的功能
    /// </summary>
    public static class BoundsHelper
    {
        /// <summary>
        /// 判断一个 Bounds 是否完全包含另一个 Bounds
        /// </summary>
        /// <param name="bounds">当前 Bounds</param>
        /// <param name="target">需要判断的目标 Bounds</param>
        /// <returns>如果 target 的最小点和最大点都在 bounds 内，则返回 true</returns>
        public static bool Contains(this Bounds bounds, Bounds target)
        {
            // 同时判断目标的 min 和 max 是否都包含在当前 Bounds 内
            return bounds.Contains(target.min) && bounds.Contains(target.max);
        }
    }
}