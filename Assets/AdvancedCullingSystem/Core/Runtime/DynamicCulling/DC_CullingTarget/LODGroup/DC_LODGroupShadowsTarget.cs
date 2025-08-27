using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    /// <summary>
    /// LODGroup 阴影剔除目标
    /// 继承自 DC_LODGroupTargetBase，用于控制 LODGroup 中所有 Renderer 的阴影显示
    /// </summary>
    public class DC_LODGroupShadowsTarget : DC_LODGroupTargetBase
    {
        // 当前 LODGroup 下的所有 Renderer
        private Renderer[] _renderers;

        // 原始阴影模式，用于恢复可见状态时使用
        private ShadowCastingMode _shadowMode;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="group">LODGroup 对象</param>
        /// <param name="renderers">LODGroup 下的所有 Renderer</param>
        /// <param name="bounds">LODGroup 包围盒</param>
        public DC_LODGroupShadowsTarget(LODGroup group, Renderer[] renderers, Bounds bounds)
            : base(group, renderers, bounds)
        {
            _renderers = Renderers; // 保存 Renderer 数组
            _shadowMode = _renderers[0].shadowCastingMode; // 默认使用第一个 Renderer 的阴影模式
        }

        /// <summary>
        /// 设置 LODGroup 阴影为可见
        /// 恢复原始 ShadowCastingMode
        /// </summary>
        public override void MakeVisible()
        {
            for (int i = 0; i < _renderers.Length; i++)
                _renderers[i].shadowCastingMode = _shadowMode;
        }

        /// <summary>
        /// 设置 LODGroup 阴影为不可见
        /// 只保留 ShadowsOnly，隐藏实际渲染
        /// </summary>
        public override void MakeInvisible()
        {
            for (int i = 0; i < _renderers.Length; i++)
                _renderers[i].shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }
    }
}