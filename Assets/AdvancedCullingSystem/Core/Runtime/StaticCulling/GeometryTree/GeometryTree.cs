using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Static
{
    /// <summary>
    /// 几何空间树（Geometry Tree）  
    /// 用于将场景中的 CullingTarget 按空间位置组织成二叉空间划分结构，
    /// 以支持高效的静态剔除与可见性查询。
    /// </summary>
    public class GeometryTree : BinaryTree<GeometryTreeNode, CullingTarget>
    {
        /// <summary>
        /// 所有参与静态剔除的 CullingTarget 列表（只读）
        /// 通常在构建 GeometryTree 时一次性注入。
        /// </summary>
        public IReadOnlyList<CullingTarget> CullingTargets { get; private set; }

        /// <summary>
        /// 当前 GeometryTree 中已创建的节点数量  
        /// 用于调试、统计或性能分析。
        /// </summary>
        public int NodesCount { get; private set; }

        /// <summary>
        /// 构造函数  
        /// 根据传入的剔除目标数组与最大树深度，
        /// 构建用于静态剔除的几何二叉树结构。
        /// </summary>
        /// <param name="targets">参与剔除的所有 CullingTarget</param>
        /// <param name="maxDepth">几何树允许的最大深度</param>
        public GeometryTree(CullingTarget[] targets, int maxDepth) :
            base(targets, maxDepth)
        {
            CullingTargets = targets;
        }

        /// <summary>
        /// 创建一个新的几何树节点  
        /// 在 BinaryTree 构建过程中被调用。
        /// </summary>
        /// <param name="center">节点所代表空间区域的中心点</param>
        /// <param name="size">节点所代表空间区域的尺寸</param>
        /// <param name="isLeaf">是否为叶子节点</param>
        /// <returns>新创建的 GeometryTreeNode</returns>
        protected override GeometryTreeNode CreateNode(Vector3 center, Vector3 size, bool isLeaf)
        {
            // 统计节点数量
            NodesCount++;

            return new GeometryTreeNode(center, size, isLeaf);
        }

        /// <summary>
        /// 获取某个剔除目标的包围盒  
        /// 用于在构建几何树时进行空间划分与归属判断。
        /// </summary>
        /// <param name="target">剔除目标</param>
        /// <returns>该目标的世界空间 Bounds</returns>
        protected override Bounds GetBounds(CullingTarget target)
        {
            return target.Bounds;
        }

        /// <summary>
        /// 向节点中添加数据的内部逻辑  
        /// 在添加目标前，将节点标记为“非空”。
        /// </summary>
        protected override void AddInternal(GeometryTreeNode node, CullingTarget data, int depth)
        {
            // 当前节点至少包含一个剔除目标
            node.IsEmpty = false;

            // 继续执行基类的添加逻辑（递归分配到子节点）
            base.AddInternal(node, data, depth);
        }

        /// <summary>
        /// 将剔除目标直接添加到指定节点中  
        /// 通常在叶子节点或达到最大深度时调用。
        /// </summary>
        /// <param name="node">几何树节点</param>
        /// <param name="target">剔除目标</param>
        protected override void AddDataToNode(GeometryTreeNode node, CullingTarget target)
        {
            node.AddCullingTarget(target);
        }

        /// <summary>
        /// 为父节点设置左右子节点  
        /// 用于构建二叉空间划分结构。
        /// </summary>
        /// <param name="parent">父节点</param>
        /// <param name="left">左子节点</param>
        /// <param name="right">右子节点</param>
        protected override void SetChildsToNode(GeometryTreeNode parent, GeometryTreeNode left, GeometryTreeNode right)
        {
            parent.SetChilds(left, right);
        }
    }
}
