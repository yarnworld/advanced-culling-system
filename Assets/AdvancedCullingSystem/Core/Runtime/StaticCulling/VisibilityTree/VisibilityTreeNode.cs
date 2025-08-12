using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Static
{
    /// <summary>
    /// 可视性树节点类
    /// 每个节点可以保存可见性数据，并与左右子节点形成二叉树结构
    /// </summary>
    public class VisibilityTreeNode : BinaryTreeNode
    {
        /// <summary>
        /// 左子节点
        /// </summary>
        public VisibilityTreeNode Left
        {
            get { return _left; }
        }

        /// <summary>
        /// 右子节点
        /// </summary>
        public VisibilityTreeNode Right
        {
            get { return _right; }
        }

        [SerializeReference]
        private VisibilityTree _tree;  // 所属可视性树

        [SerializeReference]
        private VisibilityTreeNode _left;  // 左子节点

        [SerializeReference]
        private VisibilityTreeNode _right;  // 右子节点

        [SerializeReference]
        private IVisibilityData _visibilityData;  // 当前节点的可见性数据（Compact 或标准）

        private HashSet<int> _uniqTargets;  // 当前节点唯一可见目标索引集合

        /// <summary>
        /// 构造函数
        /// </summary>
        public VisibilityTreeNode(VisibilityTree tree, Vector3 center, Vector3 size, bool isLeaf) 
            : base(center, size, isLeaf)
        {
            _tree = tree;
        }

        /// <summary>
        /// 获取左子节点（重写基类方法）
        /// </summary>
        public override BinaryTreeNode GetLeft()
        {
            return Left;
        }

        /// <summary>
        /// 获取右子节点（重写基类方法）
        /// </summary>
        public override BinaryTreeNode GetRight()
        {
            return Right;
        }

        /// <summary>
        /// 设置左右子节点
        /// </summary>
        public void SetChilds(VisibilityTreeNode left, VisibilityTreeNode right)
        {
            _left = left;
            _right = right;
        }

        /// <summary>
        /// 添加可见目标索引到当前节点
        /// 使用 HashSet 避免重复
        /// </summary>
        public void AddVisibleCullingTarget(int targetIndex)
        {
            if (_uniqTargets == null)
                _uniqTargets = new HashSet<int>();

            _uniqTargets.Add(targetIndex);
        }

        /// <summary>
        /// 去除子节点重复目标
        /// 将子节点重复目标合并到当前节点，并从子节点中移除
        /// 优化存储，减少重复计算
        /// </summary>
        public void RemoveDuplicatesFromChilds()
        {
            if (!HasChilds)
                return;

            HashSet<int> leftTargets = Left._uniqTargets;
            HashSet<int> rightTargets = Right._uniqTargets;

            if (leftTargets == null || rightTargets == null)
                return;

            // 找出左右子节点都包含的目标，提升到父节点
            foreach (var target in leftTargets)
            {
                if (rightTargets.Contains(target))
                {
                    AddVisibleCullingTarget(target);
                }
            }

            // 从子节点中移除父节点已包含的目标
            if (_uniqTargets != null)
            {
                foreach (var target in _uniqTargets)
                {
                    leftTargets.Remove(target);
                    rightTargets.Remove(target);
                }
            }
        }

        /// <summary>
        /// 将 HashSet 目标转换为可见性数据对象
        /// 自动选择 CompactVisibilityData 或 VisibilityData
        /// </summary>
        public void ApplyData()
        {
            if (_uniqTargets == null || _uniqTargets.Count == 0)
                return;

            // 如果索引超过 65535，则使用普通 int 数组存储，否则使用 ushort 紧凑存储
            if (_uniqTargets.Any(t => t >= 65535))
                _visibilityData = new VisibilityData(_uniqTargets);
            else
                _visibilityData = new CompactVisibilityData(_uniqTargets);
        }

        /// <summary>
        /// 标记当前节点对应的目标可见
        /// </summary>
        public void SetVisible()
        {
            if (_visibilityData == null)
                return;

            try
            {
                _visibilityData.SetVisible(_tree.CullingTargets);
            }
            catch(MissingReferenceException)
            {
                Debug.Log("Looks like some of baked objects was destroyed. Rebake scene");
            }
        }
    }
}
