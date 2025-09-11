using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Static
{
    /// <summary>
    /// 可见性数据接口
    /// 提供统一方法，用于将一组 CullingTarget 标记为可见
    /// </summary>
    public interface IVisibilityData
    {
        /// <summary>
        /// 将对应索引的所有目标标记为可见
        /// </summary>
        /// <param name="allTargets">场景中所有的 CullingTarget</param>
        void SetVisible(CullingTarget[] allTargets);
    }

    /// <summary>
    /// 基础可见性数据类
    /// 内部保存目标索引数组，用于标记哪些 CullingTarget 可见
    /// </summary>
    public class VisibilityData : IVisibilityData
    {
        /// <summary>
        /// 目标索引数组
        /// 对应 CullingTarget 数组的索引
        /// </summary>
        [SerializeField]
        private int[] _indexes;

        /// <summary>
        /// 构造函数
        /// 将外部索引集合转换为数组
        /// </summary>
        /// <param name="indexes">目标索引集合</param>
        public VisibilityData(ICollection<int> indexes)
        {
            _indexes = indexes.ToArray();
        }

        /// <summary>
        /// 将内部索引对应的 CullingTarget 标记为可见
        /// </summary>
        /// <param name="allTargets">场景中所有 CullingTarget</param>
        public void SetVisible(CullingTarget[] allTargets)
        {
            for (int i = 0; i < _indexes.Length; i++)
                allTargets[_indexes[i]].SetVisible();
        }
    }

    /// <summary>
    /// 紧凑型可见性数据类
    /// 用 ushort 代替 int 存储索引，减少内存占用
    /// </summary>
    public class CompactVisibilityData : IVisibilityData
    {
        /// <summary>
        /// 使用 ushort 保存目标索引
        /// 支持最多 65535 个目标
        /// </summary>
        [SerializeField]
        private ushort[] _indexes;

        /// <summary>
        /// 构造函数
        /// 将外部索引集合转换为 ushort 数组
        /// </summary>
        /// <param name="indexes">目标索引集合</param>
        public CompactVisibilityData(ICollection<int> indexes)
        {
            _indexes = indexes.Select(i => (ushort)i).ToArray();
        }

        /// <summary>
        /// 将内部索引对应的 CullingTarget 标记为可见
        /// </summary>
        /// <param name="allTargets">场景中所有 CullingTarget</param>
        public void SetVisible(CullingTarget[] allTargets)
        {
            for (int i = 0; i < _indexes.Length; i++)
                allTargets[_indexes[i]].SetVisible();
        }
    }
}
