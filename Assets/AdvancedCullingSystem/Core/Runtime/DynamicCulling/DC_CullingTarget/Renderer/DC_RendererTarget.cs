using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    /// <summary>
    /// 单个 Renderer 可见性剔除目标
    /// 继承自 DC_RendererTargetBase，用于控制单个 Renderer 的显隐状态
    /// </summary>
    public class DC_RendererTarget : DC_RendererTargetBase
    {
        // 当前 Renderer
        private Renderer _renderer;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="renderer">需要剔除的 Renderer</param>
        public DC_RendererTarget(Renderer renderer) 
            : base(renderer)
        {
            _renderer = Renderer; // 保存 Renderer
        }

        /// <summary>
        /// 设置 Renderer 为可见状态
        /// 启用 Renderer
        /// </summary>
        public override void MakeVisible()
        {
            _renderer.enabled = true;
            CullingDiagnostics.ReportTargetState(GameObject.GetInstanceID(), true);
        }

        /// <summary>
        /// 设置 Renderer 为不可见状态
        /// 禁用 Renderer
        /// </summary>
        public override void MakeInvisible()
        {
            _renderer.enabled = false;
            CullingDiagnostics.ReportTargetState(GameObject.GetInstanceID(), false);
        }
    }
}
