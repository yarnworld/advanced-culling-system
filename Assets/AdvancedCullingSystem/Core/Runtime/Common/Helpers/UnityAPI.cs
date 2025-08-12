using System.Runtime.CompilerServices;
using UnityEngine;

namespace NGS.AdvancedCullingSystem
{
    /// <summary>
    /// Unity API 辅助类，封装不同 Unity 版本的兼容性调用
    /// </summary>
    public static class UnityAPI
    {
        /// <summary>
        /// 查找场景中所有类型为 T 的对象
        /// 根据 Unity 版本使用不同的 API 以保证兼容性
        /// </summary>
        /// <typeparam name="T">继承自 UnityEngine.Object 的类型</typeparam>
        /// <returns>场景中所有 T 类型对象数组</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] FindObjectsOfType<T>() where T : Object
        {
#if UNITY_2022_2_OR_NEWER
            // Unity 2022.2 及以上版本使用新的 API
            return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
            // 旧版本使用旧 API
            return Object.FindObjectsOfType<T>();
#endif
        }

        /// <summary>
        /// 查找场景中任意一个类型为 T 的对象
        /// 根据 Unity 版本使用不同的 API 以保证兼容性
        /// </summary>
        /// <typeparam name="T">继承自 UnityEngine.Object 的类型</typeparam>
        /// <returns>场景中找到的任意一个 T 类型对象，未找到返回 null</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T FindObjectOfType<T>() where T : Object
        {
#if UNITY_2022_2_OR_NEWER
            // Unity 2022.2 及以上版本使用新的 API
            return Object.FindAnyObjectByType<T>();
#else
            // 旧版本使用旧 API
            return Object.FindObjectOfType<T>();
#endif
        }

        /// <summary>
        /// 创建一个 RaycastCommand 命令（用于 Job / ECS 线程射线检测）
        /// 根据 Unity 版本使用不同的构造方式以保证兼容性
        /// </summary>
        /// <param name="origin">射线起点</param>
        /// <param name="direction">射线方向</param>
        /// <param name="distance">射线长度，默认 5000</param>
        /// <param name="layerMask">射线检测层级掩码，默认 -1（所有层）</param>
        /// <returns>构造好的 RaycastCommand 对象</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RaycastCommand NewRaycastCommand(Vector3 origin, Vector3 direction, float distance = 5000, int layerMask = -1)
        {
#if UNITY_2022_2_OR_NEWER
            // Unity 2022.2 及以上版本使用新的 QueryParameters 构造
            return new RaycastCommand(origin, direction, new QueryParameters(layerMask), distance);
#else
            // 旧版本直接使用 RaycastCommand 的旧构造
            return new RaycastCommand(origin, direction, distance, layerMask);
#endif
        }
    }
}
