using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Utils
{
    /// <summary>
    /// 提供 Collider 复制和属性拷贝的静态工具类
    /// 用于 StaticCulling 系统中生成独立 Collider 以供烘焙或剔除使用
    /// </summary>
    public static class ColliderUtils
    {
        /// <summary>
        /// 复制一个 Collider，生成新的 GameObject 并附加相同类型的 Collider
        /// </summary>
        /// <param name="original">原始 Collider</param>
        /// <returns>复制的 Collider</returns>
        public static Collider Duplicate(Collider original)
        {
            if (original == null)
                throw new ArgumentNullException("ColliderUtils::Duplicate 'original' collider is null");

            // 创建新的 GameObject 并命名
            GameObject newObj = new GameObject("SC_Collider");

            // 根据原 Collider 类型添加相同组件
            Collider newCollider = (Collider)newObj.AddComponent(original.GetType());

            // 复制属性
            CopyColliderProperties(original, newCollider);

            return newCollider;
        }

        /// <summary>
        /// 将原 Collider 的通用属性及特定类型属性复制到目标 Collider
        /// </summary>
        /// <param name="original">原始 Collider</param>
        /// <param name="copy">目标 Collider</param>
        public static void CopyColliderProperties(Collider original, Collider copy)
        {
            // 通用属性
            copy.isTrigger = original.isTrigger;
            copy.sharedMaterial = original.sharedMaterial;

            // 根据 Collider 类型拷贝特定属性
            if (original is BoxCollider)
            {
                CopyBoxColliderProperties((BoxCollider)original, (BoxCollider)copy);
            }
            else if (original is SphereCollider)
            {
                CopySphereColliderProperties((SphereCollider)original, (SphereCollider)copy);
            }
            else if (original is CapsuleCollider)
            {
                CopyCapsuleColliderProperties((CapsuleCollider)original, (CapsuleCollider)copy);
            }
            else if (original is MeshCollider)
            {
                CopyMeshColliderProperties((MeshCollider)original, (MeshCollider)copy);
            }
            else if (original is TerrainCollider)
            {
                CopyTerrainColliderProperties((TerrainCollider)original, (TerrainCollider)copy);
            }
            else
            {
                Debug.Log(string.Format("ColliderUtils::CopyColliderProperties {0} type not implemented", original.GetType()));
            }
        }

        /// <summary>
        /// 复制 BoxCollider 特有属性
        /// </summary>
        private static void CopyBoxColliderProperties(BoxCollider original, BoxCollider copy)
        {
            copy.center = original.center;
            copy.size = original.size;
        }

        /// <summary>
        /// 复制 SphereCollider 特有属性
        /// </summary>
        private static void CopySphereColliderProperties(SphereCollider original, SphereCollider copy)
        {
            copy.center = original.center;
            copy.radius = original.radius;
        }

        /// <summary>
        /// 复制 CapsuleCollider 特有属性
        /// </summary>
        private static void CopyCapsuleColliderProperties(CapsuleCollider original, CapsuleCollider copy)
        {
            copy.center = original.center;
            copy.height = original.height;
            copy.radius = original.radius;
            copy.direction = original.direction;
        }

        /// <summary>
        /// 复制 MeshCollider 特有属性
        /// </summary>
        private static void CopyMeshColliderProperties(MeshCollider original, MeshCollider copy)
        {
            copy.sharedMesh = original.sharedMesh;
            copy.convex = original.convex;
            copy.cookingOptions = original.cookingOptions;
        }

        /// <summary>
        /// 复制 TerrainCollider 特有属性
        /// </summary>
        private static void CopyTerrainColliderProperties(TerrainCollider original, TerrainCollider copy)
        {
            copy.terrainData = original.terrainData;
        }
    }
}
