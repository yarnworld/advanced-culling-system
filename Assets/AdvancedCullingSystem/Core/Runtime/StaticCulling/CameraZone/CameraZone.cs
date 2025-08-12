using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Static
{
    /// <summary>
    /// CameraZone 类：用于静态剔除系统中的相机区域管理
    /// 每个 CameraZone 维护一个 VisibilityTree，用于细分区域的可见性管理
    /// </summary>
    public class CameraZone : MonoBehaviour
    {
        /// <summary>
        /// 所有 CameraZone 实例的静态列表，便于全局访问
        /// </summary>
        public static List<CameraZone> Instances { get; private set; } = new List<CameraZone>();
        //public static List<CameraZone> Instances { get; private set; }

        /// <summary>
        /// 该 CameraZone 对应的可视性树，用于管理区域内的可见性格子
        /// </summary>
        [field: SerializeReference]
        public VisibilityTree VisibilityTree { get; private set; }

        /// <summary>
        /// 当前 CameraZone 内格子的总数量
        /// </summary>
        [field: SerializeField]
        public int CellsCount { get; private set; }

        /// <summary>
        /// Unity 域重新加载时清空 CameraZone 静态列表
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ReloadDomain()
        {
            if (Instances != null)
                Instances.Clear();
        }

        /// <summary>
        /// Awake 生命周期回调，将当前实例添加到静态列表
        /// </summary>
        private void Awake()
        {
            if (Instances == null)
                Instances = new List<CameraZone>();

            Instances.Add(this);
        }

        /// <summary>
        /// OnDestroy 生命周期回调，将当前实例从静态列表移除
        /// </summary>
        private void OnDestroy()
        {
            Instances.Remove(this);
        }

        /// <summary>
        /// 创建可视性树（VisibilityTree），将区域细分为若干个格子
        /// </summary>
        /// <param name="cellSize">每个格子的边长</param>
        /// <returns>创建成功返回 true，否则 false</returns>
        public bool CreateVisibilityTree(float cellSize)
        {
            if (cellSize < 0.01f)
            {
                Debug.Log("无法创建 VisibilityTree，cellSize 太小: " + cellSize);
                return false;
            }

            // 如果已有可视性树，先清空
            if (VisibilityTree != null)
                ClearVisibilityTree();

            Vector3 position = transform.position;
            Vector3 size = transform.lossyScale;

            // 保证尺寸为正值
            size.x = Mathf.Abs(size.x);
            size.y = Mathf.Abs(size.y);
            size.z = Mathf.Abs(size.z);

            // 计算每个轴上的格子数量
            int countX = Mathf.CeilToInt(size.x / cellSize);
            int countY = Mathf.CeilToInt(size.y / cellSize);
            int countZ = Mathf.CeilToInt(size.z / cellSize);

            if (countX == 0 || countY == 0 || countZ == 0)
            {
                Debug.Log("无法创建 VisibilityTree，格子数量为零");
                return false;
            }

            try
            {
                // 创建 VisibilityTree 实例
                VisibilityTree = new VisibilityTree(cellSize);

                // 计算起始位置（区域最小角点）
                Vector3 start = (position - size / 2);

                // 遍历每个格子位置，将其添加到 VisibilityTree
                for (int x = 0; x < countX; x++)
                {
                    for (int y = 0; y < countY; y++)
                    {
                        for (int z = 0; z < countZ; z++)
                        {
                            Vector3 offset = new Vector3()
                            {
                                x = cellSize * x,
                                y = cellSize * y,
                                z = cellSize * z
                            };

                            VisibilityTree.Add(start + offset);
                        }
                    }
                }

                // 更新格子总数
                CellsCount = countX * countY * countZ;
            }
            catch(Exception ex)
            {
                Debug.Log("无法创建 VisibilityTree，原因: " + ex.Message + ex.StackTrace);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 清空可视性树
        /// </summary>
        public void ClearVisibilityTree()
        {
            VisibilityTree = null;
            CellsCount = 0;
        }
    }
}
