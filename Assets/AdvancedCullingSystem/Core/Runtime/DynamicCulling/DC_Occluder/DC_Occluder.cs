using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    /// <summary>
    /// 遮挡类型枚举
    /// 用于标识 Occluder 的类型
    /// </summary>
    public enum OccluderType { Collider, Mesh, LODGroup, Terrain }

    /// <summary>
    /// 动态剔除遮挡物组件
    /// 负责将场景中的物体标记为遮挡物，并创建对应的 Collider 用于射线剔除
    /// </summary>
    public class DC_Occluder : MonoBehaviour
    {
        /// <summary>
        /// 当前遮挡物类型
        /// </summary>
        [field: SerializeField]
        public OccluderType OccluderType { get; set; }

        // 缓存包围盒
        private Bounds? _bounds;

        // 对应剔除层
        private int _layer;

        /// <summary>
        /// 尝试获取遮挡物包围盒
        /// </summary>
        /// <param name="bounds">输出的包围盒</param>
        /// <returns>是否成功获取包围盒</returns>
        public bool TryGetBounds(ref Bounds bounds)
        {
            if (_bounds != null)
            {
                bounds = _bounds.Value;
                return true;
            }

            // 根据 Occluder 类型获取对应包围盒
            if (OccluderType == OccluderType.Collider)
            {
                if (TryGetComponent(out Collider collider))
                {
                    _bounds = bounds = collider.bounds;
                    return true;
                }
            }
            else if (OccluderType == OccluderType.Mesh)
            {
                if (TryGetComponent(out MeshRenderer renderer))
                {
                    _bounds = bounds = renderer.bounds;
                    return true;
                }
            }
            else
            {
                if (TryGetComponent(out LODGroup group))
                {
                    LOD[] lods = group.GetLODs();

                    // 获取 LODGroup 第一个有效 Renderer 的包围盒
                    for (int i = 0; i < lods.Length; i++)
                    {
                        foreach (var renderer in lods[i].renderers)
                        {
                            if (renderer != null)
                            {
                                _bounds = bounds = renderer.bounds;
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Reset 方法，用于 Inspector 初始化 OccluderType
        /// </summary>
        private void Reset()
        {
            if (GetComponent<MeshRenderer>() != null)
                OccluderType = OccluderType.Mesh;

            else if (GetComponent<LODGroup>() != null)
                OccluderType = OccluderType.LODGroup;

            else if (GetComponent<Terrain>() != null)
                OccluderType = OccluderType.Terrain;
        }

        /// <summary>
        /// 启动时根据类型创建对应的遮挡物
        /// </summary>
        private void Start()
        {
            _layer = DC_Controller.GetCullingLayer();

            switch (OccluderType)
            {
                case OccluderType.Collider:
                    gameObject.layer = _layer;
                    break;
                case OccluderType.Mesh:
                    CreateMeshOccluder();
                    break;
                case OccluderType.LODGroup:
                    CreateLODGroupOccluder();
                    break;
                case OccluderType.Terrain:
                    CreateTerrainOccluder();
                    break;
            }
        }

        /// <summary>
        /// 为 Mesh 创建对应的 MeshCollider 用于遮挡
        /// </summary>
        private void CreateMeshOccluder()
        {
            MeshFilter filter = GetComponent<MeshFilter>();

            if (filter == null || filter.sharedMesh == null)
            {
                Debug.Log(gameObject.name + " unable to create occluder, mesh not found");
                return;
            }

            CreateCollider(gameObject, filter.sharedMesh);
        }

        /// <summary>
        /// 为 LODGroup 创建对应的 MeshCollider 遮挡
        /// </summary>
        private void CreateLODGroupOccluder()
        {
            LODGroup group = GetComponent<LODGroup>();

            if (group == null)
            {
                Debug.Log(gameObject.name + " unable to create occluder, LODGroup not found");
                return;
            }

            LOD lod = group.GetLODs()[0];

            foreach (var renderer in lod.renderers)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();

                if (filter != null && filter.sharedMesh != null)
                    CreateCollider(renderer.gameObject, filter.sharedMesh);
            }
        }

        /// <summary>
        /// 为 Terrain 创建对应的 TerrainCollider 遮挡
        /// </summary>
        private void CreateTerrainOccluder()
        {
            TerrainCollider srcCollider = GetComponent<TerrainCollider>();

            if (srcCollider == null)
            {
                Debug.Log("Unable to create occluder, TerrainCollider not found");
                return;
            }

            GameObject colliderGO = new GameObject("DC_Occluder");
            colliderGO.layer = _layer;
            colliderGO.transform.parent = transform;
            colliderGO.transform.localPosition = Vector3.zero;
            colliderGO.transform.localRotation = Quaternion.identity;
            colliderGO.transform.localScale = Vector3.one;

            TerrainCollider destCollider = colliderGO.AddComponent<TerrainCollider>();
            destCollider.terrainData = srcCollider.terrainData;
        }

        /// <summary>
        /// 创建 MeshCollider 用于遮挡
        /// </summary>
        /// <param name="go">目标 GameObject</param>
        /// <param name="mesh">Mesh</param>
        private void CreateCollider(GameObject go, Mesh mesh)
        {
            GameObject colliderGO = new GameObject("DC_Collider");
            Transform colliderTransform = colliderGO.transform;

            colliderGO.layer = _layer;
            colliderTransform.parent = go.transform;
            colliderTransform.localPosition = Vector3.zero;
            colliderTransform.localEulerAngles = Vector3.zero;
            colliderTransform.localScale = Vector3.one;
            
            MeshCollider collider = colliderGO.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
        }
    }
}
