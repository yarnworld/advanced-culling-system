using NGS.AdvancedCullingSystem.Dynamic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Tutorial
{
    /// <summary>
    /// 为动态裁剪基准场景创建可重复的运行时配置。
    /// 仅当加载 Scene 1.unity 时才运行；控制器、目标配置和裁剪相机都在播放模式中临时创建，
    /// 因此基准场景可与 Scene.unity 保持逐字节相同，停止播放后也不会污染几千个对象。
    /// </summary>
    public static class DynamicCullingBenchmarkBootstrap
    {
        private const int RaysPerFrame = 1500;
        private const string BenchmarkScenePath =
            "Assets/AdvancedCullingSystem/Tutorials/Scenes/1. DynamicCulling Base/Scene 1.unity";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartBenchmarkScene()
        {
            // Scene.unity 是未启用 ACS 的基线；Scene 1.unity 使用完全相同的几何内容并自动启用 ACS。
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != BenchmarkScenePath)
                return;

            Camera benchmarkCamera = Camera.main;
            GameObject geometryRoot = GameObject.Find("Geometry");

            if (benchmarkCamera == null || geometryRoot == null)
            {
                Debug.LogError("[ACS 基准] 缺少 MainCamera 或 Geometry，无法启动动态裁剪基准。");
                return;
            }

            GameObject controllerObject = new GameObject("[ACS Benchmark Controller]");
            DC_Controller controller = controllerObject.AddComponent<DC_Controller>();

            // 只扫描 Geometry 子树，避免把地面和场景辅助对象误算为裁剪收益。
            controller.AssignSourcesFast(geometryRoot, CullingMethod.FullDisable);
            controller.AddCamera(benchmarkCamera, RaysPerFrame);

            Debug.Log($"[ACS 基准] 已启动：每帧 {RaysPerFrame} 条射线，模式 FullDisable。");
        }
    }
}
