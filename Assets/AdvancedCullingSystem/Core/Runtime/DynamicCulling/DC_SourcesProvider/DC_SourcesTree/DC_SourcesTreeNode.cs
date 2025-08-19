using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    /// <summary>
    /// DC_SourcesTree 的节点类
    /// 每个节点对应一个空间区域，可存放一个或多个剔除目标
    /// </summary>
    public class DC_SourcesTreeNode : BinaryTreeNode
    {
        /// <summary>
        /// 左子节点
        /// </summary>
        public DC_SourcesTreeNode Left { get; private set; }

        /// <summary>
        /// 右子节点
        /// </summary>
        public DC_SourcesTreeNode Right { get; private set; }

        /// <summary>
        /// 当前节点对应的源对象 (MultiSource)
        /// 一个节点可管理多个 ICullingTarget
        /// </summary>
        public DC_MultiSource Source { get; private set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="center">节点中心坐标</param>
        /// <param name="size">节点尺寸</param>
        /// <param name="isLeaf">是否为叶子节点</param>
        public DC_SourcesTreeNode(Vector3 center, Vector3 size, bool isLeaf)
            : base(center, size, isLeaf)
        {

        }

        /// <summary>
        /// 获取左子节点
        /// </summary>
        public override BinaryTreeNode GetLeft()
        {
            return Left;
        }

        /// <summary>
        /// 获取右子节点
        /// </summary>
        public override BinaryTreeNode GetRight()
        {
            return Right;
        }

        /// <summary>
        /// 设置左右子节点
        /// </summary>
        public void SetChilds(DC_SourcesTreeNode left, DC_SourcesTreeNode right)
        {
            Left = left;
            Right = right;
        }

        /// <summary>
        /// 将剔除目标添加到节点的 MultiSource
        /// 如果节点还没有 MultiSource，会自动创建
        /// </summary>
        public void AddCullingTarget(ICullingTarget cullingTarget)
        {
            if (Source == null)
                Source = new GameObject("DC_MultiSource").AddComponent<DC_MultiSource>();

            Source.SetCullingTarget(cullingTarget);
        }
    }
}