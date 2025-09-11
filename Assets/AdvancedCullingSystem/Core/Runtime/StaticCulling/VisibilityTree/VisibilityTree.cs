using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Static
{
    /// <summary>
    /// 可视性二叉树类
    /// 基于 BinaryTree 泛型，实现对场景 CullingTarget 的空间划分与可见性计算
    /// </summary>
    public class VisibilityTree : BinaryTree<VisibilityTreeNode, Vector3>
    {
        /// <summary>
        /// 树中管理的所有 CullingTarget 对象
        /// </summary>
        [field: SerializeField]
        public CullingTarget[] CullingTargets { get; private set; }

        /// <summary>
        /// 构造函数，初始化二叉树，指定每个 Cell 的大小
        /// </summary>
        /// <param name="cellSize">树节点单元格大小</param>
        public VisibilityTree(float cellSize) 
            : base(cellSize)
        {
        }

        /// <summary>
        /// 设置树中管理的所有剔除目标
        /// </summary>
        /// <param name="targets">CullingTarget 数组</param>
        public void SetTargets(CullingTarget[] targets)
        {
            CullingTargets = targets;
        }

        /// <summary>
        /// 优化二叉树，去除子节点重复数据
        /// </summary>
        public void Optimize()
        {
            Optimize(RootInternal);
        }

        /// <summary>
        /// 应用树节点上的可见性数据到目标对象
        /// </summary>
        public void Apply()
        {
            ApplyData(RootInternal);
        }

        /// <summary>
        /// 根据指定点（通常是相机位置）和容差范围，标记可见的节点
        /// </summary>
        /// <param name="point">相机位置</param>
        /// <param name="tolerance">可见范围半径(x,z)及高度(y)</param>
        public void SetVisible(Vector3 point, Vector2 tolerance)
        {
            Bounds cameraBounds = new Bounds(point, new Vector3(tolerance.x, tolerance.y, tolerance.x));
            SetVisibleInternal(RootInternal, cameraBounds);
        }

        /// <summary>
        /// 绘制当前可见范围内的树节点的 Gizmo，用于 Editor 可视化
        /// </summary>
        /// <param name="point">相机位置</param>
        /// <param name="tolerance">可见范围半径</param>
        public void DrawCellsGizmo(Vector3 point, Vector2 tolerance)
        {
            Bounds cameraBounds = new Bounds(point, new Vector3(tolerance.x, tolerance.y, tolerance.x));

            // 绘制相机可视区域
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(cameraBounds.center, cameraBounds.size);

            DrawCellsGizmoInternal(RootInternal, cameraBounds);
        }

        /// <summary>
        /// 内部递归函数：根据相机范围标记节点及其子节点可见
        /// </summary>
        private void SetVisibleInternal(VisibilityTreeNode node, Bounds cameraBounds)
        {
            if (!node.Bounds.Intersects(cameraBounds))
                return;

            node.SetVisible();

            if (node.HasChilds)
            {
                SetVisibleInternal(node.Left, cameraBounds);
                SetVisibleInternal(node.Right, cameraBounds);
            }
        }

        /// <summary>
        /// 内部递归函数：优化节点，去掉子节点重复数据
        /// </summary>
        private void Optimize(VisibilityTreeNode current)
        {
            if (current.IsLeaf)
                return;

            if (current.HasChilds)
            {
                Optimize(current.Left);
                Optimize(current.Right);
            }

            current.RemoveDuplicatesFromChilds();
        }

        /// <summary>
        /// 内部递归函数：应用节点数据到对应目标对象
        /// </summary>
        private void ApplyData(VisibilityTreeNode current)
        {
            current.ApplyData();

            if (current.HasChilds)
            {
                ApplyData(current.Left);
                ApplyData(current.Right);
            }
        }

        /// <summary>
        /// 内部递归函数：绘制树节点的 Gizmo，可视化节点边界
        /// </summary>
        private void DrawCellsGizmoInternal(VisibilityTreeNode node, Bounds cameraBounds)
        {
            if (!node.Bounds.Intersects(cameraBounds))
                return;

            if (node.IsLeaf)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireCube(node.Center, node.Size);
                return;
            }

            if (node.HasChilds)
            {
                DrawCellsGizmoInternal(node.Left, cameraBounds);
                DrawCellsGizmoInternal(node.Right, cameraBounds);
            }
        }

        /// <summary>
        /// 重写 BinaryTree 的抽象方法：根据点生成包围盒
        /// </summary>
        protected override Bounds GetBounds(Vector3 point)
        {
            return new Bounds(point, Vector3.one * 0.1f);
        }

        /// <summary>
        /// 重写 BinaryTree 的抽象方法：创建树节点
        /// </summary>
        protected override VisibilityTreeNode CreateNode(Vector3 center, Vector3 size, bool isLeaf)
        {
            return new VisibilityTreeNode(this, center, size, isLeaf);
        }

        /// <summary>
        /// 设置父节点与左右子节点的关系
        /// </summary>
        protected override void SetChildsToNode(VisibilityTreeNode parent, VisibilityTreeNode leftChild, VisibilityTreeNode rightChild)
        {
            parent.SetChilds(leftChild, rightChild);
        }

        /// <summary>
        /// 将数据添加到节点（此类中未实现，保留接口）
        /// </summary>
        protected override void AddDataToNode(VisibilityTreeNode node, Vector3 point)
        {
            // 空实现，数据由其他模块填充
        }
    }
}
