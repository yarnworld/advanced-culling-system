using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NGS.AdvancedCullingSystem.Static
{
    /// <summary>
    /// 剔除方式枚举
    /// </summary>
    public enum CullingMethod
    {
        /// <summary>
        /// 不渲染物体本身，但仍然保留阴影投射
        /// </summary>
        KeepShadows,

        /// <summary>
        /// 完全禁用 Renderer（既不渲染，也不投射阴影）
        /// </summary>
        FullDisable
    }

    /// <summary>
    /// MeshRenderer 剔除目标  
    /// 用于将单个 MeshRenderer 纳入静态剔除系统管理，
    /// 根据剔除结果动态控制渲染启用状态或阴影投射方式。
    /// </summary>
    public class MeshRendererCullingTarget : CullingTarget
    {
        /// <summary>
        /// 当前物体使用的剔除方式  
        /// 可在 Inspector 中配置。
        /// </summary>
        [field: SerializeField]
        public CullingMethod CullingMethod { get; set; }

        /// <summary>
        /// 是否作为遮挡物（Occluder）参与剔除计算  
        /// 通常由系统在构建剔除数据时自动设置。
        /// </summary>
        [field: SerializeField, HideInInspector]
        public bool IsOccluder { get; set; }

        /// <summary>
        /// 当前物体上的 MeshRenderer 组件缓存
        /// </summary>
        private MeshRenderer _renderer;

        /// <summary>
        /// 物体变为可见时执行的行为委托
        /// </summary>
        private Action _makeVisAction;

        /// <summary>
        /// 物体变为不可见时执行的行为委托
        /// </summary>
        private Action _makeInvisAction;


        /// <summary>
        /// Unity 生命周期函数  
        /// 在初始化阶段缓存 MeshRenderer，
        /// 并根据剔除方式绑定对应的显示 / 隐藏逻辑，
        /// 避免运行时频繁判断分支，提高剔除效率。
        /// </summary>
        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();

            if (CullingMethod == CullingMethod.FullDisable)
            {
                // 完全剔除：直接启用 / 禁用 MeshRenderer
                _makeVisAction = EnableRenderer;
                _makeInvisAction = DisableRenderer;
            }
            else
            {
                // 保留阴影剔除：不可见时仅投射阴影
                _makeVisAction = EnableRendererKeepShadows;
                _makeInvisAction = DisableRendererKeepShadows;
            }
        }


        /// <summary>
        /// 当剔除系统判定该目标“不可见”时调用
        /// </summary>
        protected override void MakeInvisible()
        {
            _makeInvisAction();
        }

        /// <summary>
        /// 当剔除系统判定该目标“可见”时调用
        /// </summary>
        protected override void MakeVisible()
        {
            _makeVisAction();
        }


        /// <summary>
        /// 启用 MeshRenderer 的正常渲染
        /// </summary>
        private void EnableRenderer()
        {
            _renderer.enabled = true;
        }

        /// <summary>
        /// 禁用 MeshRenderer 的渲染
        /// </summary>
        private void DisableRenderer()
        {
            _renderer.enabled = false;
        }

        /// <summary>
        /// 启用 MeshRenderer 渲染，并允许其正常投射阴影
        /// </summary>
        private void EnableRendererKeepShadows()
        {
            _renderer.shadowCastingMode = ShadowCastingMode.On;
        }

        /// <summary>
        /// 禁用物体本身的渲染，但仍然保留阴影投射  
        /// 常用于远距离或被遮挡物体的性能优化。
        /// </summary>
        private void DisableRendererKeepShadows()
        {
            _renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }
    }
}
