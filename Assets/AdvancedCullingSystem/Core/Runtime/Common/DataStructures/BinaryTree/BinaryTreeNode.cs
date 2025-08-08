using UnityEngine;

namespace NGS.AdvancedCullingSystem
{
    /// <summary>
    /// 二叉空间树的基础节点抽象类。
    /// 
    /// 该类用于表示空间二叉树中的一个节点，
    /// 每个节点对应一个三维空间包围盒（Bounds），
    /// 并通过左右子节点继续细分空间。
    /// 
    /// BinaryTreeNode 只关心：
    /// - 空间范围（Bounds）
    /// - 是否为叶子节点
    /// - 子节点访问接口
    /// 
    /// 具体的数据存储方式、左右子节点的实现
    /// 由派生类负责。
    /// </summary>
    public abstract class BinaryTreeNode
    {
        /// <summary>
        /// 当前节点所代表空间的中心点
        /// </summary>
        public Vector3 Center
        {
            get
            {
                return _bounds.center;
            }
        }

        /// <summary>
        /// 当前节点所代表空间的尺寸
        /// </summary>
        public Vector3 Size
        {
            get
            {
                return _bounds.size;
            }
        }

        /// <summary>
        /// 当前节点对应的三维空间包围盒
        /// </summary>
        public Bounds Bounds
        {
            get
            {
                return _bounds;
            }
        }

        /// <summary>
        /// 当前节点是否拥有子节点
        /// 
        /// 在该实现中：
        /// - 只要左子节点存在，即认为该节点已被细分
        /// </summary>
        public bool HasChilds
        {
            get
            {
                return GetLeft() != null;
            }
        }

        /// <summary>
        /// 当前节点是否为叶子节点
        /// 
        /// 叶子节点通常用于直接存储空间数据，
        /// 不再继续进行空间细分
        /// </summary>
        public bool IsLeaf
        {
            get
            {
                return _isLeaf;
            }
        }

        /// <summary>
        /// 当前节点对应的空间包围盒
        /// </summary>
        [SerializeField]
        private Bounds _bounds;

        /// <summary>
        /// 是否为叶子节点的标记
        /// </summary>
        [SerializeField]
        private bool _isLeaf;

        /// <summary>
        /// 构造一个二叉树节点
        /// </summary>
        /// <param name="center">节点空间包围盒的中心点</param>
        /// <param name="size">节点空间包围盒的尺寸</param>
        /// <param name="isLeaf">是否为叶子节点</param>
        public BinaryTreeNode(Vector3 center, Vector3 size, bool isLeaf)
        {
            _bounds = new Bounds(center, size);
            _isLeaf = isLeaf;
        }

        /// <summary>
        /// 获取当前节点的左子节点
        /// </summary>
        /// <returns>左子节点</returns>
        public abstract BinaryTreeNode GetLeft();

        /// <summary>
        /// 获取当前节点的右子节点
        /// </summary>
        /// <returns>右子节点</returns>
        public abstract BinaryTreeNode GetRight();
    }
}
