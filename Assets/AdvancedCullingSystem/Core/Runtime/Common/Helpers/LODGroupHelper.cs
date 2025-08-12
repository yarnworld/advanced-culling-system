using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem
{
    /// <summary>
    /// LODGroup 辅助类，提供针对 LOD 和 Renderer 的统计和过滤功能
    /// </summary>
    public static class LODGroupHelper
    {
        /// <summary>
        /// 统计 LODGroup 中满足条件的 Renderer 数量
        /// </summary>
        /// <param name="group">LODGroup 对象</param>
        /// <param name="filter">对 Renderer 的筛选函数</param>
        /// <returns>满足条件的 Renderer 数量</returns>
        public static int Count(this LODGroup group, Func<Renderer, bool> filter)
        {
            LOD[] lods = group.GetLODs(); // 获取该 LODGroup 的所有 LOD 层级

            return Count(lods, filter); // 委托给 LOD[] 的 Count 方法
        }

        /// <summary>
        /// 统计 LOD 数组中满足条件的 Renderer 数量
        /// </summary>
        /// <param name="lods">LOD 数组</param>
        /// <param name="filter">对 Renderer 的筛选函数</param>
        /// <returns>满足条件的 Renderer 数量</returns>
        public static int Count(this LOD[] lods, Func<Renderer, bool> filter)
        {
            int count = 0;

            for (int i = 0; i < lods.Length; i++)
            {
                Renderer[] renderers = lods[i].renderers; // 当前 LOD 层的所有 Renderer

                for (int c = 0; c < renderers.Length; c++)
                {
                    Renderer renderer = renderers[c];

                    if (renderer == null) // 跳过空引用
                        continue;

                    if (filter(renderer)) // 如果符合条件，计数加一
                        count++;
                }
            }

            return count;
        }

        /// <summary>
        /// 判断 LOD 数组中是否存在任意一个 Renderer 满足条件
        /// </summary>
        /// <param name="lods">LOD 数组</param>
        /// <param name="filter">对 Renderer 的筛选函数</param>
        /// <returns>如果存在至少一个满足条件的 Renderer，则返回 true</returns>
        public static bool ContainsAny(this LOD[] lods, Func<Renderer, bool> filter)
        {
            for (int i = 0; i < lods.Length; i++)
            {
                Renderer[] renderers = lods[i].renderers;

                for (int c = 0; c < renderers.Length; c++)
                    if (filter(renderers[c])) // 一旦找到符合条件的 Renderer 就返回 true
                        return true;
            }

            return false; // 没有任何符合条件的 Renderer
        }
    }
}
