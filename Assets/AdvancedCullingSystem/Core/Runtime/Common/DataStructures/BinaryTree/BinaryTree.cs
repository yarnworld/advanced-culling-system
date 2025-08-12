using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NGS.AdvancedCullingSystem
{
    /// <summary>
    /// 通用的三维空间二叉树结构（Binary Space Partition Tree）。
    /// 
    /// 该结构主要用于高级剔除系统（Advanced Culling System），
    /// 通过对三维空间进行递归二分划分，将空间数据（TData）
    /// 挂载到对应的树节点（TNode）中，从而加速：
    /// - 可见性剔除（如视锥体剔除）
    /// - 空间查询
    /// - 大规模场景管理
    /// 
    /// 特点：
    /// 1. 支持动态向上扩展（GrowTreeUp），以容纳超出当前空间范围的对象
    /// 2. 支持按最大轴向递归向下细分（GrowTreeDown），类似 KD-Tree
    /// 3. 通过最大深度与最小单元尺寸限制，防止无限递归
    /// </summary>
    /// <typeparam name="TNode">二叉树节点类型，必须继承 BinaryTreeNode</typeparam>
    /// <typeparam name="TData">被管理的空间数据类型</typeparam>
    public abstract class BinaryTree<TNode, TData> where TNode : BinaryTreeNode
    {
        /// <summary>
        /// 允许的最大树高度，
        /// 防止树无限向上扩展导致内存溢出或程序崩溃
        /// </summary>
        private const int MAX_HEIGHT = 42;

        /// <summary>
        /// 对外暴露的根节点（只读）
        /// </summary>
        public BinaryTreeNode Root
        {
            get
            {
                return RootInternal;
            }
        }

        /// <summary>
        /// 实际使用的根节点（内部使用，可序列化）
        /// </summary>
        [field: SerializeReference]
        protected TNode RootInternal { get; private set; }

        /// <summary>
        /// 当前二叉树的最大深度（高度）
        /// </summary>
        [field: SerializeField]
        public int Height { get; private set; }

        /// <summary>
        /// 最小空间单元尺寸，
        /// 用于限制叶子节点的最小体积
        /// </summary>
        [field : SerializeField]
        public float CellSize { get; private set; }

        /// <summary>
        /// 允许的最大递归深度（-1 表示不限制）
        /// </summary>
        private int _maxDepth = -1;

        /// <summary>
        /// 使用最小单元尺寸创建一棵空的空间二叉树
        /// </summary>
        /// <param name="cellSize">最小空间单元尺寸</param>
        public BinaryTree(float cellSize)
        {
            // 防止 CellSize 过小导致精度或性能问题
            CellSize = Mathf.Max(cellSize, 0.1f);
        }

        /// <summary>
        /// 使用一组初始数据构建空间二叉树
        /// </summary>
        /// <param name="datas">需要管理的空间数据集合</param>
        /// <param name="maxDepth">树允许的最大深度</param>
        public BinaryTree(IList<TData> datas, int maxDepth)
        {
            if (maxDepth <= 1)
                throw new ArgumentException("Max depth should be greater than 1");

            _maxDepth = maxDepth;

            // 计算所有数据的整体包围盒
            Vector3 min = Vector3.one * float.MaxValue;
            Vector3 max = Vector3.one * float.MinValue;

            foreach (var data in datas)
            {
                Bounds dBounds = GetBounds(data);
                Vector3 dMin = dBounds.min;
                Vector3 dMax = dBounds.max;

                min.x = Mathf.Min(min.x, dMin.x);
                min.y = Mathf.Min(min.y, dMin.y);
                min.z = Mathf.Min(min.z, dMin.z);

                max.x = Mathf.Max(max.x, dMax.x);
                max.y = Mathf.Max(max.y, dMax.y);
                max.z = Mathf.Max(max.z, dMax.z);
            }

            // 创建能够完全包住所有数据的根节点
            RootInternal = CreateNode(
                min + ((max - min) / 2),
                max - min + Vector3.one * 0.01f,
                false
            );

            Height = 1;

            // 将所有数据插入树中
            foreach (var data in datas)
                Add(data);
        }

        /// <summary>
        /// 向空间二叉树中添加一个数据对象
        /// </summary>
        public void Add(TData data)
        {
            // 若树尚未初始化，则以该对象创建根节点
            if (Root == null)
            {
                RootInternal = CreateNode(
                    GetBounds(data).center,
                    Vector3.one * CellSize,
                    true
                );
                Height = 1;
            }

            // 若当前根节点无法包含该对象，则向上扩展树
            if (!Includes(RootInternal, data))
                GrowTreeUp(data);

            // 从根节点开始递归插入
            AddInternal(RootInternal, data, 1);
        }

        /// <summary>
        /// 扩展根节点，使其能够包含目标对象
        /// 会创建一个新的父节点，将原根节点作为子节点
        /// </summary>
        private TNode ExpandRoot(TNode root, TData target)
        {
            Bounds rootBounds = root.Bounds;
            Bounds targetBounds = GetBounds(target);

            Vector3 parentCenter = Vector3.zero;
            Vector3 parentSize = Vector3.zero;
            Vector3 childCenter = Vector3.zero;

            bool rootIsLeft = false;

            // 判断在哪个轴向上需要扩展
            for (int i = 0; i < 3; i++)
            {
                if (targetBounds.min[i] < rootBounds.min[i])
                {
                    parentSize = rootBounds.size;
                    parentSize[i] *= 2;

                    parentCenter = rootBounds.center;
                    parentCenter[i] -= rootBounds.size[i] / 2;

                    childCenter = rootBounds.center;
                    childCenter[i] -= rootBounds.size[i];
                    break;
                }

                if (targetBounds.max[i] > rootBounds.max[i])
                {
                    parentSize = rootBounds.size;
                    parentSize[i] *= 2;

                    parentCenter = rootBounds.center;
                    parentCenter[i] += rootBounds.size[i] / 2;

                    childCenter = rootBounds.center;
                    childCenter[i] += rootBounds.size[i];

                    rootIsLeft = true;
                    break;
                }
            }

            // 创建新的父节点与子节点
            TNode parent = CreateNode(parentCenter, parentSize, false);
            TNode child = CreateNode(childCenter, rootBounds.size, root.IsLeaf);

            // 设置新父节点的左右子节点
            if (rootIsLeft)
                SetChildsToNode(parent, RootInternal, child);
            else
                SetChildsToNode(parent, child, RootInternal);

            return parent;
        }

        /// <summary>
        /// 递归向上扩展树，直到根节点能够完全包含目标对象
        /// </summary>
        protected void GrowTreeUp(TData target)
        {
            // 防止无限扩展导致崩溃
            if (Height > MAX_HEIGHT)
            {
                Debug.Log(
                    "Increasing the binary tree can lead to memory overflow and crashes. " +
                    "Please make sure you are not trying to add infinite objects such as " +
                    "Skybox, fog, or water."
                );
                return;
            }

            if (Includes(RootInternal, target))
                return;

            RootInternal = ExpandRoot(RootInternal, target);
            Height++;

            GrowTreeUp(target);
        }

        /// <summary>
        /// 将指定节点向下细分为左右两个子节点
        /// </summary>
        protected void GrowTreeDown(TNode node, TData target, int depth)
        {
            if (node.HasChilds)
                throw new Exception("GrowTreeDown::" + depth + " node already has childs");

            Bounds nodeBounds = node.Bounds;
            Vector3 offset;
            Vector3 size;

            // 选择最长轴进行切分
            if (nodeBounds.size.x >= nodeBounds.size.y && nodeBounds.size.x >= nodeBounds.size.z)
            {
                offset = new Vector3(nodeBounds.size.x / 4, 0, 0);
                size = new Vector3(nodeBounds.size.x / 2, nodeBounds.size.y, nodeBounds.size.z);
            }
            else if (nodeBounds.size.y >= nodeBounds.size.x && nodeBounds.size.y >= nodeBounds.size.z)
            {
                offset = new Vector3(0, nodeBounds.size.y / 4, 0);
                size = new Vector3(nodeBounds.size.x, nodeBounds.size.y / 2, nodeBounds.size.z);
            }
            else
            {
                offset = new Vector3(0, 0, nodeBounds.size.z / 4);
                size = new Vector3(nodeBounds.size.x, nodeBounds.size.y, nodeBounds.size.z / 2);
            }

            // 判断是否达到叶子节点条件
            bool isLeafs =
                (depth == _maxDepth) ||
                (size.x <= CellSize && size.y <= CellSize && size.z <= CellSize);

            TNode left = CreateNode(nodeBounds.center - offset, size, isLeafs);
            TNode right = CreateNode(nodeBounds.center + offset, size, isLeafs);

            SetChildsToNode(node, left, right);

            if (isLeafs)
            {
                if (Height < depth)
                    Height = depth;

                if (CellSize == 0)
                    CellSize = Mathf.Min(size.x, size.y, size.z);

                return;
            }

            // 仅对与目标对象相交的子节点继续递归
            if (Intersects(left, target))
                GrowTreeDown(left, target, depth + 1);

            if (Intersects(right, target))
                GrowTreeDown(right, target, depth + 1);
        }

        /// <summary>
        /// 判断数据对象是否与节点的包围盒相交
        /// </summary>
        protected bool Intersects(TNode node, TData data)
        {
            return node.Bounds.Intersects(GetBounds(data));
        }

        /// <summary>
        /// 判断节点是否完全包含数据对象
        /// </summary>
        protected bool Includes(TNode node, TData data)
        {
            return node.Bounds.Contains(GetBounds(data));
        }

        /// <summary>
        /// 递归将数据插入到合适的子节点中
        /// </summary>
        protected virtual void AddInternal(TNode node, TData data, int depth)
        {
            if (node.IsLeaf)
            {
                AddDataToNode(node, data);
                return;
            }

            if (!node.HasChilds)
                GrowTreeDown(node, data, depth + 1);

            TNode left = (TNode)node.GetLeft();
            TNode right = (TNode)node.GetRight();

            if (Intersects(left, data))
                AddInternal(left, data, depth + 1);

            if (Intersects(right, data))
                AddInternal(right, data, depth + 1);
        }

        /// <summary>
        /// 获取数据对象的世界空间包围盒
        /// </summary>
        protected abstract Bounds GetBounds(TData data);

        /// <summary>
        /// 创建一个新的二叉树节点
        /// </summary>
        protected abstract TNode CreateNode(Vector3 center, Vector3 size, bool isLeaf);

        /// <summary>
        /// 为父节点设置左右子节点
        /// </summary>
        protected abstract void SetChildsToNode(TNode parent, TNode leftChild, TNode rightChild);

        /// <summary>
        /// 将数据对象添加到指定节点中
        /// </summary>
        protected abstract void AddDataToNode(TNode node, TData data);
    }
}
