using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    /// <summary>
    /// 单一源提供器
    /// 每个剔除目标对应一个独立的 DC_Source 实例
    /// 适用于不需要将多个目标合并在一个 MultiSource 中管理的情况
    /// </summary>
    public class DC_SingleSourcesProvider : IDC_SourcesProvider
    {
        /// <summary>
        /// 根据剔除目标生成对应的源对象
        /// </summary>
        /// <param name="cullingTarget">剔除目标</param>
        /// <returns>对应的 DC_Source 对象</returns>
        public DC_Source GetSource(ICullingTarget cullingTarget)
        {
            // 创建新的 GameObject 作为源对象承载体
            GameObject go = new GameObject("DC_SingleSource");

            // 添加 DC_SingleSource 组件并绑定剔除目标
            DC_SingleSource source = go.AddComponent<DC_SingleSource>();
            source.SetCullingTarget(cullingTarget);

            return source;
        }
    }
}