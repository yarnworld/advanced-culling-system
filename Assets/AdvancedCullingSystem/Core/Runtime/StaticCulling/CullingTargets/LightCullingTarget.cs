using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Static
{
    /// <summary>
    /// 灯光剔除目标  
    /// 用于将 Unity 的 Light 组件纳入静态剔除系统管理，
    /// 根据剔除结果动态控制灯光的启用与禁用。
    /// </summary>
    public class LightCullingTarget : CullingTarget
    {
        /// <summary>
        /// 当前物体上的 Light 组件引用
        /// </summary>
        private Light _light;

        /// <summary>
        /// Unity 生命周期函数  
        /// 在对象初始化时获取并缓存 Light 组件，
        /// 避免在剔除过程中频繁调用 GetComponent 带来的性能开销。
        /// </summary>
        private void Awake()
        {
            _light = GetComponent<Light>();
        }

        /// <summary>
        /// 当剔除系统判定该目标“可见”时调用  
        /// 启用 Light 组件，使灯光参与场景照明计算。
        /// </summary>
        protected override void MakeVisible()
        {
            _light.enabled = true;
        }

        /// <summary>
        /// 当剔除系统判定该目标“不可见”时调用  
        /// 禁用 Light 组件，减少无效灯光带来的性能消耗。
        /// </summary>
        protected override void MakeInvisible()
        {
            _light.enabled = false;
        }
    }
}