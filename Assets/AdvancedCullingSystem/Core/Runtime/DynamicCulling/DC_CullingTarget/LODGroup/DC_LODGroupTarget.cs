using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    /// <summary>
    /// LODGroup 可见性剔除目标
    /// 继承自 DC_LODGroupTargetBase，用于控制 LODGroup 下所有 Renderer 的显隐状态
    /// </summary>
    public class DC_LODGroupTarget : DC_LODGroupTargetBase
    {
        // 当前 LODGroup 下的所有 Renderer
        private Renderer[] _renderers;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="group">LODGroup 对象</param>
        /// <param name="renderers">LODGroup 下的所有 Renderer</param>
        /// <param name="bounds">LODGroup 包围盒</param>
        public DC_LODGroupTarget(LODGroup group, Renderer[] renderers, Bounds bounds) 
            : base(group, renderers, bounds)
        {
            _renderers = Renderers; // 保存 Renderer 数组
        }

        /// <summary>
        /// 设置 LODGroup 为可见状态
        /// 启用所有 Renderer
        /// </summary>
        public override void MakeVisible()
        {
            for (int i = 0; i < _renderers.Length; i++)
                _renderers[i].enabled = true;
        }

        /// <summary>
        /// 设置 LODGroup 为不可见状态
        /// 禁用所有 Renderer
        /// </summary>
        public override void MakeInvisible()
        {
            for (int i = 0; i < _renderers.Length; i++)
                _renderers[i].enabled = false;
        }
    }
}