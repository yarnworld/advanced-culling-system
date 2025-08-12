using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    /// <summary>
    /// 基于二叉树实现的源提供者
    /// 每个节点保存一定范围内的剔除目标(ICullingTarget)
    /// 用于空间分区管理多个剔除目标，提高动态剔除效率
    /// </summary>
    public class DC_SourcesTree : BinaryTree<DC_SourcesTreeNode, ICullingTarget>, IDC_SourcesProvider
    {
        // 最近一次被修改的源对象
        private DC_Source _lastModifiedSource;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="nodeSize">树节点大小，用于空间划分</param>
        public DC_SourcesTree(float nodeSize) 
            : base(nodeSize)
        {

        }

        /// <summary>
        /// 根据剔除目标获取对应的源对象
        /// 若目标尚未加入树中，则会添加
        /// </summary>
        /// <param name="cullingTarget">剔除目标</param>
        /// <returns>对应的 DC_Source 对象</returns>
        public DC_Source GetSource(ICullingTarget cullingTarget)
        {
            _lastModifiedSource = null;

            Add(cullingTarget);

            return _lastModifiedSource;
        }

        /// <summary>
        /// 内部递归添加剔除目标到树节点
        /// </summary>
        protected override void AddInternal(DC_SourcesTreeNode node, ICullingTarget data, int depth)
        {
            if (node.IsLeaf)
            {
                AddDataToNode(node, data);
                return;
            }

            if (!node.HasChilds)
                GrowTreeDown(node, data, depth + 1);

            if (Intersects(node.Left, data))
                AddInternal(node.Left, data, depth + 1);
            else
                AddInternal(node.Right, data, depth + 1);

        }

        /// <summary>
        /// 获取剔除目标的包围盒
        /// </summary>
        protected override Bounds GetBounds(ICullingTarget data)
        {
            return data.Bounds;
        }

        /// <summary>
        /// 创建二叉树节点
        /// </summary>
        protected override DC_SourcesTreeNode CreateNode(Vector3 center, Vector3 size, bool isLeaf)
        {
            return new DC_SourcesTreeNode(center, size, isLeaf);
        }

        /// <summary>
        /// 设置节点的左右子节点
        /// </summary>
        protected override void SetChildsToNode(DC_SourcesTreeNode parent, DC_SourcesTreeNode leftChild, DC_SourcesTreeNode rightChild)
        {
            parent.SetChilds(leftChild, rightChild);
        }

        /// <summary>
        /// 将剔除目标加入叶子节点
        /// 并记录最后修改的源对象
        /// </summary>
        protected override void AddDataToNode(DC_SourcesTreeNode node, ICullingTarget data)
        {
            node.AddCullingTarget(data);
            _lastModifiedSource = node.Source;
        }
    }
}
