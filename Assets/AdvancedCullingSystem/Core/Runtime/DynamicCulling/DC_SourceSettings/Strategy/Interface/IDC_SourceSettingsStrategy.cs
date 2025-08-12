using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    /// <summary>
    /// SourceSettings 策略接口
    /// 定义每种 SourceSettings 对象需要实现的剔除逻辑操作
    /// 使用策略模式实现不同类型对象（Renderer、LODGroup 等）的剔除行为
    /// </summary>
    public interface IDC_SourceSettingsStrategy
    {
        /// <summary>
        /// 是否已经准备好进行剔除
        /// </summary>
        bool ReadyForCulling { get; }

        /// <summary>
        /// 检查与场景对象的兼容性，并获取不兼容原因
        /// </summary>
        /// <param name="incompatibilityReason">不兼容原因</param>
        /// <returns>true=兼容，false=不兼容</returns>
        bool CheckCompatibilityAndGetComponents(out string incompatibilityReason);

        /// <summary>
        /// 准备剔除，例如缓存渲染组件、计算 Bounds 等
        /// </summary>
        void PrepareForCulling();

        /// <summary>
        /// 清理数据，例如释放缓存引用
        /// </summary>
        void ClearData();

        /// <summary>
        /// 获取剔除目标的包围盒
        /// </summary>
        /// <param name="bounds">输出包围盒</param>
        /// <returns>true=获取成功，false=失败</returns>
        bool TryGetBounds(ref Bounds bounds);

        /// <summary>
        /// 创建具体的剔除目标
        /// </summary>
        /// <returns>ICullingTarget 对象</returns>
        ICullingTarget CreateCullingTarget();

        /// <summary>
        /// 获取所有参与剔除的 Collider
        /// </summary>
        /// <returns>Collider 集合</returns>
        IEnumerable<Collider> GetColliders();
    }
}