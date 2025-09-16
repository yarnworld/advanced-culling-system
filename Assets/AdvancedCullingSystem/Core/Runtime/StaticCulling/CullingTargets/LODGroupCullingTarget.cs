using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace NGS.AdvancedCullingSystem.Static
{
    /// <summary>
    /// LODGroup 剔除目标  
    /// 用于对 LODGroup 下的 Renderer 进行统一剔除控制，
    /// 支持“完全禁用渲染”或“仅保留阴影”的两种剔除策略。
    /// </summary>
    public class LODGroupCullingTarget : CullingTarget
    {
        /// <summary>
        /// 剔除方式  
        /// FullDisable：完全启用 / 禁用 Renderer  
        /// 其他模式：物体不可见时仍保留阴影（ShadowsOnly）
        /// </summary>
        [field: SerializeField]
        public CullingMethod CullingMethod { get; set; }

        /// <summary>
        /// 是否作为遮挡物（Occluder）参与剔除计算  
        /// 通常由系统自动设置，运行时不在 Inspector 中显示。
        /// </summary>
        [field: SerializeField, HideInInspector]
        public bool IsOccluder { get; set; }

        /// <summary>
        /// 当前 LODGroup 关联的所有 Renderer 组件  
        /// 在初始化阶段由外部系统统一注入。
        /// </summary>
        [SerializeField, HideInInspector]
        private Renderer[] _renderers;

        /// <summary>
        /// 物体变为可见时执行的行为委托
        /// </summary>
        private Action _makeVisibleAction;

        /// <summary>
        /// 物体变为不可见时执行的行为委托
        /// </summary>
        private Action _makeInvisibleAction;


        /// <summary>
        /// Unity 生命周期函数  
        /// 根据剔除方式，在初始化阶段绑定对应的“显示 / 隐藏”策略，
        /// 避免在运行时频繁进行条件判断，提升剔除执行效率。
        /// </summary>
        private void Awake()
        {
            if (CullingMethod == CullingMethod.FullDisable)
            {
                // 完全剔除：直接启用 / 禁用 Renderer
                _makeVisibleAction = MakeRenderersVisible;
                _makeInvisibleAction = MakeRenderersInvisible;
            }
            else
            {
                // 保留阴影剔除：物体不可见但仍投射阴影
                _makeVisibleAction = MakeRenderersVisibleKeepShadows;
                _makeInvisibleAction = MakeRenderersInvisibleKeepShadows;
            }
        }

        /// <summary>
        /// 设置需要被剔除系统控制的 Renderer 集合  
        /// 通常由 LODGroup 或剔除系统在初始化时调用。
        /// </summary>
        /// <param name="renderers">LODGroup 下的 Renderer 列表</param>
        public void SetRenderers(IEnumerable<Renderer> renderers)
        {
            _renderers = renderers.ToArray();
        }

        /// <summary>
        /// 当剔除系统判定该目标“可见”时调用  
        /// 实际执行逻辑由初始化阶段绑定的委托决定。
        /// </summary>
        protected override void MakeVisible()
        {
            _makeVisibleAction();
        }

        /// <summary>
        /// 当剔除系统判定该目标“不可见”时调用  
        /// 根据剔除方式选择禁用渲染或仅保留阴影。
        /// </summary>
        protected override void MakeInvisible()
        {
            _makeInvisibleAction();
        }


        /// <summary>
        /// 完全启用所有 Renderer 的渲染
        /// </summary>
        private void MakeRenderersVisible()
        {
            for (int i = 0; i < _renderers.Length; i++)
                _renderers[i].enabled = true;
        }

        /// <summary>
        /// 完全禁用所有 Renderer 的渲染
        /// </summary>
        private void MakeRenderersInvisible()
        {
            for (int i = 0; i < _renderers.Length; i++)
                _renderers[i].enabled = false;
        }

        /// <summary>
        /// 启用 Renderer 渲染，并允许其正常投射阴影
        /// </summary>
        private void MakeRenderersVisibleKeepShadows()
        {
            for (int i = 0; i < _renderers.Length; i++)
                _renderers[i].shadowCastingMode = ShadowCastingMode.On;
        }

        /// <summary>
        /// 禁用物体本身的可见渲染，但仍保留阴影投射  
        /// 常用于远距离物体或性能优化场景。
        /// </summary>
        private void MakeRenderersInvisibleKeepShadows()
        {
            for (int i = 0; i < _renderers.Length; i++)
                _renderers[i].shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }
    }
}
