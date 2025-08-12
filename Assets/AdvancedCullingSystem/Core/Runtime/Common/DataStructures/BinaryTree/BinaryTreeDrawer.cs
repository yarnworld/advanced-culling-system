using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem
{
    /// <summary>
    /// 二叉空间树的可视化绘制工具类。
    /// 
    /// 该类主要用于在 Unity 场景视图（Scene View）中，
    /// 通过 Gizmos 将 BinaryTree 的空间划分结构绘制出来，
    /// 以便开发者调试和验证空间切分是否正确。
    /// 
    /// 通常用于：
    /// - 调试高级剔除系统的空间划分结果
    /// - 观察二叉树（KD-Tree / BSP）节点分布
    /// - 分析 CellSize、MaxDepth 等参数是否合理
    /// </summary>
    public class BinaryTreeDrawer
    {
        /// <summary>
        /// Gizmos 绘制时使用的颜色
        /// </summary>
        public Color Color { get; set; }

        /// <summary>
        /// 从指定根节点开始，递归绘制整棵二叉树的 Gizmos 边框
        /// </summary>
        /// <param name="root">二叉树的根节点</param>
        public void DrawTreeGizmos(BinaryTreeNode root)
        {
            // 若节点为空，则终止递归
            if (root == null)
                return;

            // 获取当前节点所代表的空间包围盒
            Bounds bounds = root.Bounds;

            // 设置 Gizmos 绘制颜色
            Gizmos.color = Color;

            // 绘制当前节点对应的空间边界（线框立方体）
            Gizmos.DrawWireCube(bounds.center, bounds.size);

            // 递归绘制左子节点
            DrawTreeGizmos(root.GetLeft());

            // 递归绘制右子节点
            DrawTreeGizmos(root.GetRight());
        }
    }
}