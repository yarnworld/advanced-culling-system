using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    public partial class DC_Camera : MonoBehaviour
    {
        // 射线在视口中的分布方式
        // Halton：低差异序列
        // R2：R2 准随机分布（更均匀，常用于采样）
        public enum DistributionMethod { Halton, R2 }

        // 相机射线工具类（仅供 DC_Camera 内部使用）
        private static class DC_CameraUtil
        {
            // 缓存表：相机参数 -> 射线方向数组
            // 用于避免在相机参数未变化时重复计算射线方向
            private static Dictionary<DC_CameraSettings, Vector3[]> _rayDirsTable;

            // R2 分布使用的两个常量（基于无理数 g）
            private static double _r2a1;
            private static double _r2a2;


            // 在域重载 / 子系统注册时调用
            // 用于清空缓存，避免旧数据污染
            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
            private static void ReloadDomain()
            {
                _rayDirsTable?.Clear();
            }

            // 静态构造函数
            // 初始化缓存表与 R2 分布参数
            static DC_CameraUtil()
            {
                _rayDirsTable = new Dictionary<DC_CameraSettings, Vector3[]>();

                // R2 分布使用的常数 g（塑性常数）
                double g = 1.32471795724474602596;

                _r2a1 = 1.0 / g;
                _r2a2 = 1.0 / (g * g);
            }

            // 根据相机参数生成射线方向数组（相机空间）
            public static Vector3[] GetRaysDirections(Camera camera, DistributionMethod distribution, int fovAddition)
            {
                // 生成当前相机参数快照，用于缓存 Key
                DC_CameraSettings settings = new DC_CameraSettings(camera);

                // 如果缓存中已有对应配置，直接返回
                if (_rayDirsTable.TryGetValue(settings, out Vector3[] result))
                    return result;

                // 缓存原始 FOV
                float cameraFov = camera.fieldOfView;

                // 相机世界矩阵的逆矩阵（用于将世界方向转回相机空间）
                Matrix4x4 cameraInvTransform = camera.transform.localToWorldMatrix.inverse;

                // 射线数量与屏幕分辨率相关（每 8 个像素生成 1 条射线）
                int count = (Screen.width * Screen.height) / 8;
                Vector3[] dirs = new Vector3[count];

                // 临时扩大相机 FOV，减少视锥边缘漏判
                camera.fieldOfView = cameraFov + fovAddition;

                for (int i = 0; i < count; i++)
                {
                    Vector2 viewPoint;

                    // 根据分布方式生成视口坐标
                    if (distribution == DistributionMethod.Halton)
                        viewPoint = new Vector2(HaltonSequence(i, 2), HaltonSequence(i, 3));
                    else
                        viewPoint = R2Distribution(i);

                    // 从视口坐标生成射线
                    Ray ray = camera.ViewportPointToRay(viewPoint);

                    // 将射线方向转换到相机本地空间并保存
                    dirs[i] = cameraInvTransform.MultiplyVector(ray.direction);
                }

                // 恢复原始 FOV
                camera.fieldOfView = cameraFov;

                // 缓存结果
                _rayDirsTable.Add(settings, dirs);

                return dirs;
            }


            // Halton 低差异序列生成函数
            // 用于生成 [0,1) 区间内的准随机数
            private static float HaltonSequence(int index, int b)
            {
                float res = 0f;
                float f = 1f / b;

                int i = index;

                while (i > 0)
                {
                    res = res + f * (i % b);
                    i = Mathf.FloorToInt(i / b);
                    f = f / b;
                }

                return res;
            }

            // R2 准随机分布生成函数
            // 相比 Halton，在二维采样上更均匀
            private static Vector2 R2Distribution(int index)
            {
                float x = (float)((0.5 + _r2a1 * index) % 1);
                float y = (float)((0.5 + _r2a2 * index) % 1);

                return new Vector2(x, y);
            }
        }
    }
}
