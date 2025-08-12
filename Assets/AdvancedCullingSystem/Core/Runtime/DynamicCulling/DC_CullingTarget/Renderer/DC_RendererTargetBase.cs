using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    /// <summary>
    /// 单个 Renderer 剔除目标基类
    /// 实现 ICullingTarget 接口，是 Renderer 剔除系统的抽象基础类
    /// </summary>
    public abstract class DC_RendererTargetBase : ICullingTarget
    {
        /// <summary>
        /// 对应的 GameObject，来自 Renderer
        /// </summary>
        public GameObject GameObject
        {
            get
            {
                return Renderer.gameObject;
            }
        }

        /// <summary>
        /// Renderer 的包围盒，用于剔除计算
        /// </summary>
        public Bounds Bounds
        {
            get
            {
                return Renderer.bounds;
            }
        }

        /// <summary>
        /// 当前 Renderer 实例
        /// 受保护，子类可访问
        /// </summary>
        protected Renderer Renderer { get; private set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="renderer">需要剔除的 Renderer</param>
        public DC_RendererTargetBase(Renderer renderer)
        {
            Renderer = renderer;
        }

        /// <summary>
        /// 设置对象为不可见状态
        /// 子类必须实现
        /// </summary>
        public abstract void MakeInvisible();

        /// <summary>
        /// 设置对象为可见状态
        /// 子类必须实现
        /// </summary>
        public abstract void MakeVisible();
    }
}