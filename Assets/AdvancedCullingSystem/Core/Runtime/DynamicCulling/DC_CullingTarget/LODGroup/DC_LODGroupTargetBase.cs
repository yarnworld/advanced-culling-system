using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    /// <summary>
    /// LODGroup 剔除目标基类
    /// 实现 ICullingTarget 接口，是 LODGroup 剔除系统的抽象基础类
    /// </summary>
    public abstract class DC_LODGroupTargetBase : ICullingTarget
    {
        /// <summary>
        /// 对应的 GameObject，来自 LODGroup
        /// </summary>
        public GameObject GameObject 
        { 
            get 
            {
                return Group.gameObject;
            } 
        }

        /// <summary>
        /// LODGroup 的包围盒，用于剔除计算
        /// </summary>
        public Bounds Bounds { get; private set; }

        /// <summary>
        /// 当前 LODGroup 实例
        /// 受保护，子类可访问
        /// </summary>
        protected LODGroup Group { get; private set; }

        /// <summary>
        /// 当前 LODGroup 下的所有 Renderer
        /// 受保护，子类可访问
        /// </summary>
        protected Renderer[] Renderers { get; private set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="group">LODGroup 对象</param>
        /// <param name="renderers">LODGroup 下的所有 Renderer</param>
        /// <param name="bounds">LODGroup 包围盒</param>
        public DC_LODGroupTargetBase(LODGroup group, Renderer[] renderers, Bounds bounds)
        {
            Group = group;
            Renderers = renderers;
            Bounds = bounds;
        }

        /// <summary>
        /// 设置对象为可见状态
        /// 子类必须实现
        /// </summary>
        public abstract void MakeVisible();

        /// <summary>
        /// 设置对象为不可见状态
        /// 子类必须实现
        /// </summary>
        public abstract void MakeInvisible();
    }
}