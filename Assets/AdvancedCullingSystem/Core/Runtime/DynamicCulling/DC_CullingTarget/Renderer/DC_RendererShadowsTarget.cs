using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    /// <summary>
    /// 单个 Renderer 阴影剔除目标
    /// 继承自 DC_RendererTargetBase，用于控制单个 Renderer 的阴影显示
    /// </summary>
    public class DC_RendererShadowsTarget : DC_RendererTargetBase
    {
        // 当前 Renderer
        private Renderer _renderer;

        // 原始阴影模式，用于恢复可见状态
        private ShadowCastingMode _shadowMode;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="renderer">需要剔除的 Renderer</param>
        public DC_RendererShadowsTarget(Renderer renderer) 
            : base(renderer)
        {
            _renderer = Renderer; // 保存 Renderer
            _shadowMode = _renderer.shadowCastingMode; // 保存初始阴影模式
        }

        /// <summary>
        /// 设置 Renderer 阴影为可见
        /// 恢复原始 ShadowCastingMode
        /// </summary>
        public override void MakeVisible()
        {
            _renderer.shadowCastingMode = _shadowMode;
            CullingDiagnostics.ReportTargetState(GameObject.GetInstanceID(), true);
        }

        /// <summary>
        /// 设置 Renderer 阴影为不可见
        /// 只保留 ShadowsOnly，隐藏模型但保留阴影
        /// </summary>
        public override void MakeInvisible()
        {
            _renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            CullingDiagnostics.ReportTargetState(GameObject.GetInstanceID(), false);
        }
    }
}
