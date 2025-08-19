using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    /// <summary>
    /// 源提供者接口
    /// 用于根据剔除目标(ICullingTarget)获取对应的 DC_Source 对象
    /// 不同实现可以使用不同的数据结构和策略（如单源或空间分区）
    /// </summary>
    public interface IDC_SourcesProvider
    {
        /// <summary>
        /// 根据剔除目标获取对应的源对象
        /// 如果源对象不存在，具体实现可能会创建一个新的 DC_Source
        /// </summary>
        /// <param name="cullingTarget">剔除目标</param>
        /// <returns>对应的 DC_Source 对象</returns>
        DC_Source GetSource(ICullingTarget cullingTarget);
    }
}