using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Static
{
    /// <summary>
    /// 静态剔除系统的偏好设置类，用于统一管理层级信息
    /// </summary>
    public static class StaticCullingPreferences
    {
        /// <summary>
        /// 获取静态剔除使用的层级名称
        /// </summary>
        public static string LayerName 
        {
            get
            {
                return "ACSCulling"; // 默认层名称为 "ACSCulling"
            }
        }

        /// <summary>
        /// 获取静态剔除使用的层级索引
        /// </summary>
        public static int Layer
        {
            get
            {
                // 根据层名称获取对应的层索引
                return LayerMask.NameToLayer(LayerName);
            }
        }
    }
}