using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Static
{
    /// <summary>
    /// 几何树节点（GeometryTreeNode）  
    /// 表示 GeometryTree 中的一个空间节点，
    /// 用于存储该空间区域内关联的 CullingTarget，
    /// 并通过左右子节点构成二叉空间划分结构。
    /// </summary>
    public class GeometryTreeNode : BinaryTreeNode
    {
        /// <summary>
        /// 节点索引  
        /// 通常用于调试、序列化或运行时快速定位节点。
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// 标记当前节点是否为空节点  
        /// 在构建几何树时，如果有剔除目标落入该节点，
        /// 则会被标记为非空。
        /// </summary>
        public bool IsEmpty { get; set; }

        /// <summary>
        /// 左子节点（空间划分的一侧）
        /// </summary>
        public GeometryTreeNode Left { get; private set; }

        /// <summary>
        /// 右子节点（空间划分的另一侧）
        /// </summary>
        public GeometryTreeNode Right { get; private set; }

        /// <summary>
        /// 当前节点中包含的所有剔除目标（只读）
        /// 外部系统仅能读取，不能直接修改。
        /// </summary>
        public IReadOnlyList<CullingTarget> CullingTargets
        {
            get
            {
                return _targets;
            }
        }

        /// <summary>
        /// 用于去重的剔除目标集合  
        /// 保证同一个 CullingTarget 不会被重复添加到节点中。
        /// </summary>
        private HashSet<CullingTarget> _targetsSet;

        /// <summary>
        /// 实际存储剔除目标的列表  
        /// 用于顺序遍历和运行时访问。
        /// </summary>
        private List<CullingTarget> _targets;


        /// <summary>
        /// 构造函数  
        /// 创建一个几何树节点，并初始化其空间范围信息。
        /// </summary>
        /// <param name="center">节点所表示空间区域的中心点</param>
        /// <param name="size">节点所表示空间区域的尺寸</param>
        /// <param name="isLeaf">是否为叶子节点</param>
        public GeometryTreeNode(Vector3 center, Vector3 size, bool isLeaf) 
            : base(center, size, isLeaf)
        {
            // 初始状态下认为该节点为空
            IsEmpty = true;
        }

        /// <summary>
        /// 获取左子节点（BinaryTreeNode 抽象接口实现）
        /// </summary>
        public override BinaryTreeNode GetLeft()
        {
            return Left;
        }

        /// <summary>
        /// 获取右子节点（BinaryTreeNode 抽象接口实现）
        /// </summary>
        public override BinaryTreeNode GetRight()
        {
            return Right;
        }

        /// <summary>
        /// 设置当前节点的左右子节点  
        /// 在构建 GeometryTree 时由 GeometryTree 调用。
        /// </summary>
        /// <param name="left">左子节点</param>
        /// <param name="right">右子节点</param>
        public void SetChilds(GeometryTreeNode left, GeometryTreeNode right)
        {
            Left = left;
            Right = right;
        }

        /// <summary>
        /// 向当前节点中添加一个剔除目标  
        /// 内部使用 HashSet 去重，确保目标唯一性。
        /// </summary>
        /// <param name="target">需要添加的 CullingTarget</param>
        public void AddCullingTarget(CullingTarget target)
        {
            // 延迟初始化，只有在真正需要存储数据时才创建集合
            if (_targetsSet == null)
            {
                _targetsSet = new HashSet<CullingTarget>();
                _targets = new List<CullingTarget>();
            }

            // 如果该目标此前未被添加过，则加入列表
            if (_targetsSet.Add(target))
                _targets.Add(target);
        }
    }
}
