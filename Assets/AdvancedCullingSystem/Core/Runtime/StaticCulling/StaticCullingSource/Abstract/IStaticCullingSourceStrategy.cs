using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Static
{
    /// <summary>
    /// 静态剔除「Source 策略接口」
    /// 
    /// 该接口用于抽象不同类型的“剔除源”（Source），
    /// 例如：MeshRenderer、Collider、LODGroup、自定义体积等。
    /// 
    /// 每一种 Source 都通过实现该接口，参与静态剔除的 Baking 过程，
    /// 并最终生成一个或多个 CullingTarget 供运行时使用。
    /// </summary>
    public interface IStaticCullingSourceStrategy
    {
        /// <summary>
        /// 校验当前 Source 是否合法、可用于静态剔除
        /// 
        /// 通常用于 Baking 前的完整性检查，例如：
        /// - 必要组件是否存在
        /// - 参数是否配置正确
        /// - 是否处于非法状态（被禁用、缺失引用等）
        /// 
        /// </summary>
        /// <param name="errorMessage">
        /// 当校验失败时，返回具体的错误信息，用于编辑器提示
        /// </param>
        /// <returns>
        /// true  表示校验通过  
        /// false 表示校验失败
        /// </returns>
        bool Validate(out string errorMessage);

        /// <summary>
        /// 尝试获取该 Source 在世界空间中的包围盒（Bounds）
        /// 
        /// 该 Bounds 是静态剔除系统进行空间划分、分块、
        /// 以及生成剔除数据的基础几何信息。
        /// 
        /// 注意：
        /// - 这里使用 TryGet 语义，说明并非所有 Source
        ///   都一定能成功提供 Bounds
        /// </summary>
        /// <param name="bounds">
        /// 输出参数，成功时返回计算得到的包围盒
        /// </param>
        /// <returns>
        /// true  表示成功获取 Bounds  
        /// false 表示获取失败
        /// </returns>
        bool TryGetBounds(out Bounds bounds);

        /// <summary>
        /// 根据当前 Source 的数据，创建一个对应的 CullingTarget
        /// 
        /// CullingTarget 是静态剔除系统在运行时真正使用的对象，
        /// Source 只在 Baking 阶段存在。
        /// 
        /// 该方法通常在 Baking 阶段被调用，
        /// 将编辑器侧的数据转换为运行时可用的数据结构。
        /// </summary>
        /// <returns>
        /// 新创建的 CullingTarget 实例
        /// </returns>
        CullingTarget CreateCullingTarget();

        /// <summary>
        /// 在 Baking 开始前的准备阶段调用
        /// 
        /// 用于执行一些仅在 Baking 期间需要的操作，例如：
        /// - 缓存组件引用
        /// - 计算临时数据
        /// - 关闭/冻结某些运行时逻辑
        /// 
        /// 该方法通常只在编辑器环境下调用
        /// </summary>
        void PrepareForBaking();

        /// <summary>
        /// 在 Baking 完成后的清理阶段调用
        /// 
        /// 用于释放或还原 PrepareForBaking 中创建的临时状态，例如：
        /// - 清理缓存
        /// - 还原组件状态
        /// - 释放临时数据
        /// 
        /// 确保 Baking 不会对场景或对象留下副作用
        /// </summary>
        void ClearAfterBaking();
    }
}
