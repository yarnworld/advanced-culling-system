using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using NGS.AdvancedCullingSystem.Dynamic;
using NGS.AdvancedCullingSystem.Static;
using NGS.AdvancedCullingSystem;

namespace NGS.AdvancedCullingSystem.Editor
{
    /// <summary>
    /// 用于检查裁剪配置和运行时状态的全场景诊断窗口。
    /// </summary>
    public sealed class CullingVisualizationWindow : EditorWindow
    {
        private bool _drawSceneOverlay = true;
        private bool _drawZoneBounds = true;
        private bool _drawDynamicCameras = true;
        private bool _autoRefresh = true;
        private bool _showHistory = true;
        private Vector2 _scroll;
        private readonly List<string> _validationMessages = new List<string>();

        [MenuItem("Tools/NGSTools/Advanced Culling System/Visualization")]
        private static void Open()
        {
            GetWindow<CullingVisualizationWindow>("Culling Visualization");
        }

        [MenuItem("Tools/NGSTools/Advanced Culling System/Log Current Diagnostics")]
        private static void LogCurrentDiagnostics()
        {
            Debug.Log("[ACS-DIAGNOSTICS] " + BuildJsonReport(false));
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += DrawSceneOverlay;
            EditorApplication.update += RepaintWhenNeeded;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawSceneOverlay;
            EditorApplication.update -= RepaintWhenNeeded;
        }

        private void RepaintWhenNeeded()
        {
            if (_autoRefresh)
                Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("裁剪可视化", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("用于检查裁剪相机、Camera Zone、烘焙状态和动态射线命中率。运行游戏后可查看实时数据。", MessageType.Info);

            _autoRefresh = EditorGUILayout.ToggleLeft("自动刷新", _autoRefresh);
            _drawSceneOverlay = EditorGUILayout.ToggleLeft("显示场景叠加层", _drawSceneOverlay);
            _drawZoneBounds = EditorGUILayout.ToggleLeft("显示 Camera Zone 边界", _drawZoneBounds);
            _drawDynamicCameras = EditorGUILayout.ToggleLeft("显示动态相机标签", _drawDynamicCameras);
            _showHistory = EditorGUILayout.ToggleLeft("显示性能历史", _showHistory);

            if (GUILayout.Button("选择所有裁剪相机"))
                SelectCullingCameras();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawDynamicCameras();
            DrawPerformanceDiagnostics();
            EditorGUILayout.Space(8);
            DrawStaticCameras();
            EditorGUILayout.Space(8);
            DrawZones();
            DrawValidation();
            EditorGUILayout.EndScrollView();
        }

        private void DrawPerformanceDiagnostics()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("性能诊断", EditorStyles.boldLabel);

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("尚未采样：请进入 Play Mode。编辑模式下的零值不是性能结果。", MessageType.Warning);
                return;
            }

            if (!CullingDiagnostics.HasSamples)
            {
                EditorGUILayout.HelpBox("正在等待第一帧诊断数据。", MessageType.Info);
                return;
            }

            CullingDiagnostics.FrameSample sample = CullingDiagnostics.Current;
            CullingDiagnostics.Summary summary = CullingDiagnostics.GetSummary();
            EditorGUILayout.LabelField("帧号", sample.Frame.ToString());
            EditorGUILayout.LabelField("动态相机数", sample.CameraCount.ToString());
            EditorGUILayout.LabelField("射线数量", sample.RaycastCount.ToString());
            EditorGUILayout.LabelField("射线命中数", sample.HitCount.ToString());
            EditorGUILayout.LabelField("射线批次延迟", sample.RaycastMilliseconds.ToString("0.000") + " ms");
            Rect ratioRect = GUILayoutUtility.GetRect(18f, 18f);
            EditorGUI.ProgressBar(ratioRect, sample.HitRatio, "命中率 " + (sample.HitRatio * 100f).ToString("0.0") + "%");

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("注册目标", sample.TargetCount.ToString());
            EditorGUILayout.LabelField("实际可见目标", sample.VisibleTargetCount.ToString());
            EditorGUILayout.LabelField("实际裁剪目标", sample.CulledTargetCount.ToString());
            EditorGUILayout.LabelField("本帧显隐切换", sample.VisibilityChangeCount.ToString());
            Rect cullRect = GUILayoutUtility.GetRect(18f, 18f);
            EditorGUI.ProgressBar(cullRect, sample.CullRatio, "实际裁剪率 " + (sample.CullRatio * 100f).ToString("0.0") + "%");

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("当前帧耗时", sample.FrameMilliseconds.ToString("0.00") + " ms");
            EditorGUILayout.LabelField("平均帧耗时", summary.AverageFrameMilliseconds.ToString("0.00") + " ms");
            EditorGUILayout.LabelField("P95 帧耗时", summary.P95FrameMilliseconds.ToString("0.00") + " ms");
            EditorGUILayout.LabelField("CPU/GPU 帧耗时", FormatTiming(summary.AverageCpuFrameMilliseconds) + " / " + FormatTiming(summary.AverageGpuFrameMilliseconds));
            EditorGUILayout.LabelField("Batches / SetPass", UnityStats.batches + " / " + UnityStats.setPassCalls);
            EditorGUILayout.LabelField("Triangles / Vertices", UnityStats.triangles + " / " + UnityStats.vertices);
            EditorGUILayout.HelpBox("射线命中率表示采样射线撞到 Collider 的比例，不等于对象裁剪率。判断收益应优先看实际裁剪率、帧时间、Batches 和 Triangles。", MessageType.Info);

            if (_showHistory)
                DrawHistoryGraph();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("导出 JSON 摘要"))
                    ExportJson();
                if (GUILayout.Button("导出 CSV 历史"))
                    ExportCsv();
                if (GUILayout.Button("清空历史"))
                    CullingDiagnostics.Clear();
            }
        }

        private void DrawHistoryGraph()
        {
            Rect rect = GUILayoutUtility.GetRect(0f, 90f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.08f, 0.08f, 0.08f));
            Handles.BeginGUI();
            Vector3 previousHit = Vector3.zero;
            Vector3 previousCull = Vector3.zero;
            Vector3 previousFrame = Vector3.zero;
            bool hasPrevious = false;
            for (int i = 0; i < CullingDiagnostics.SampleCount; i++)
            {
                CullingDiagnostics.FrameSample sample = CullingDiagnostics.GetHistory(i);
                float x = rect.x + rect.width * (i + 1) / Mathf.Max(1f, CullingDiagnostics.HistoryLength);
                Vector3 hitPoint = new Vector3(x, rect.yMax - Mathf.Clamp01(sample.HitRatio) * rect.height, 0f);
                Vector3 cullPoint = new Vector3(x, rect.yMax - Mathf.Clamp01(sample.CullRatio) * rect.height, 0f);
                Vector3 framePoint = new Vector3(x, rect.yMax - Mathf.Clamp01(sample.FrameMilliseconds / 33.33f) * rect.height, 0f);
                if (hasPrevious)
                {
                    Handles.color = new Color(0.2f, 0.9f, 0.4f, 1f);
                    Handles.DrawLine(previousHit, hitPoint);
                    Handles.color = new Color(0.2f, 0.65f, 1f, 1f);
                    Handles.DrawLine(previousCull, cullPoint);
                    Handles.color = new Color(1f, 0.6f, 0.15f, 1f);
                    Handles.DrawLine(previousFrame, framePoint);
                }
                previousHit = hitPoint;
                previousCull = cullPoint;
                previousFrame = framePoint;
                hasPrevious = true;
            }
            Handles.EndGUI();
            GUI.Label(new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, 18f), "绿：命中率  蓝：裁剪率  橙：帧耗时（33.3ms 满刻度）");
        }

        private static string FormatTiming(float milliseconds)
        {
            return milliseconds > 0.001f ? milliseconds.ToString("0.00") + " ms" : "不可用";
        }

        private static string BuildJsonReport(bool prettyPrint)
        {
            CullingDiagnostics.Summary summary = CullingDiagnostics.GetSummary();
            DiagnosticReport report = new DiagnosticReport
            {
                generatedAtUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                scene = SceneManager.GetActiveScene().path,
                sampleCount = summary.SampleCount,
                averageFrameMilliseconds = summary.AverageFrameMilliseconds,
                p95FrameMilliseconds = summary.P95FrameMilliseconds,
                averageCpuFrameMilliseconds = summary.AverageCpuFrameMilliseconds,
                averageGpuFrameMilliseconds = summary.AverageGpuFrameMilliseconds,
                averageRaycastBatchMilliseconds = summary.AverageRaycastMilliseconds,
                averageRayHitRatioPercent = summary.AverageHitRatio * 100f,
                averageCullRatioPercent = summary.AverageCullRatio * 100f,
                targetCount = summary.TargetCount,
                visibleTargetCount = summary.VisibleTargetCount,
                culledTargetCount = summary.CulledTargetCount,
                batches = UnityStats.batches,
                setPassCalls = UnityStats.setPassCalls,
                triangles = UnityStats.triangles,
                vertices = UnityStats.vertices
            };
            return JsonUtility.ToJson(report, prettyPrint);
        }

        private static void ExportJson()
        {
            string path = EditorUtility.SaveFilePanel("保存裁剪诊断摘要", Application.dataPath, "culling-diagnostics.json", "json");
            if (!string.IsNullOrEmpty(path))
                File.WriteAllText(path, BuildJsonReport(true), new UTF8Encoding(false));
        }

        private static void ExportCsv()
        {
            string path = EditorUtility.SaveFilePanel("保存裁剪诊断历史", Application.dataPath, "culling-diagnostics.csv", "csv");
            if (string.IsNullOrEmpty(path))
                return;

            StringBuilder csv = new StringBuilder();
            csv.AppendLine("frame,frame_ms,cpu_frame_ms,gpu_frame_ms,cameras,raycasts,hits,hit_ratio,raycast_batch_ms,targets,visible,culled,cull_ratio,visibility_changes");
            for (int i = 0; i < CullingDiagnostics.SampleCount; i++)
            {
                CullingDiagnostics.FrameSample sample = CullingDiagnostics.GetHistory(i);
                csv.Append(sample.Frame).Append(',')
                    .Append(sample.FrameMilliseconds.ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
                    .Append(sample.CpuFrameMilliseconds.ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
                    .Append(sample.GpuFrameMilliseconds.ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
                    .Append(sample.CameraCount).Append(',').Append(sample.RaycastCount).Append(',').Append(sample.HitCount).Append(',')
                    .Append(sample.HitRatio.ToString("0.0000", CultureInfo.InvariantCulture)).Append(',')
                    .Append(sample.RaycastMilliseconds.ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
                    .Append(sample.TargetCount).Append(',').Append(sample.VisibleTargetCount).Append(',').Append(sample.CulledTargetCount).Append(',')
                    .Append(sample.CullRatio.ToString("0.0000", CultureInfo.InvariantCulture)).Append(',')
                    .Append(sample.VisibilityChangeCount).AppendLine();
            }
            File.WriteAllText(path, csv.ToString(), new UTF8Encoding(false));
        }

        [Serializable]
        private sealed class DiagnosticReport
        {
            public string generatedAtUtc;
            public string unityVersion;
            public string scene;
            public int sampleCount;
            public float averageFrameMilliseconds;
            public float p95FrameMilliseconds;
            public float averageCpuFrameMilliseconds;
            public float averageGpuFrameMilliseconds;
            public float averageRaycastBatchMilliseconds;
            public float averageRayHitRatioPercent;
            public float averageCullRatioPercent;
            public int targetCount;
            public int visibleTargetCount;
            public int culledTargetCount;
            public int batches;
            public int setPassCalls;
            public int triangles;
            public int vertices;
        }

        private void DrawValidation()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("配置检查", EditorStyles.boldLabel);
            if (GUILayout.Button("运行配置检查"))
                ValidateScene();

            foreach (string message in _validationMessages)
                EditorGUILayout.HelpBox(message, message.StartsWith("通过") ? MessageType.Info : MessageType.Warning);
        }

        private void ValidateScene()
        {
            _validationMessages.Clear();
            foreach (StaticCullingCamera camera in FindObjectsOfType<StaticCullingCamera>(true))
            {
                if (!camera.HasVisibilityTree)
                    _validationMessages.Add("未通过：静态裁剪相机「" + camera.name + "」没有可用的 Visibility Tree。");
            }

            foreach (CameraZone zone in FindObjectsOfType<CameraZone>(true))
            {
                if (zone.VisibilityTree == null)
                    _validationMessages.Add("未通过：Camera Zone「" + zone.name + "」尚未烘焙。");
            }

            if (_validationMessages.Count == 0)
                _validationMessages.Add("通过：当前已加载场景未发现明显的静态裁剪配置问题。");
        }

        private void DrawDynamicCameras()
        {
            EditorGUILayout.LabelField("动态裁剪相机", EditorStyles.boldLabel);
            DC_Camera[] cameras = FindObjectsOfType<DC_Camera>(true);
            if (cameras.Length == 0)
            {
                EditorGUILayout.HelpBox("当前已加载场景中没有找到 DC_Camera。", MessageType.Warning);
                return;
            }

            foreach (DC_Camera cullingCamera in cameras)
            {
                if (cullingCamera == null)
                    continue;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(cullingCamera.name, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("是否激活", cullingCamera.IsCullingActive ? "是" : "否");
                    EditorGUILayout.LabelField("射线数量", cullingCamera.LastRaycastCount.ToString());
                    EditorGUILayout.LabelField("最近命中率", (cullingCamera.LastRayHitRatio * 100f).ToString("0.0") + "%");
                    Rect rect = GUILayoutUtility.GetRect(18f, 18f);
                EditorGUI.ProgressBar(rect, cullingCamera.LastRayHitRatio, "射线命中率");
                }
            }
        }

        private void DrawStaticCameras()
        {
            EditorGUILayout.LabelField("静态裁剪相机", EditorStyles.boldLabel);
            StaticCullingCamera[] cameras = FindObjectsOfType<StaticCullingCamera>(true);
            if (cameras.Length == 0)
            {
                EditorGUILayout.HelpBox("当前已加载场景中没有找到 StaticCullingCamera。", MessageType.Warning);
                return;
            }

            foreach (StaticCullingCamera camera in cameras)
            {
                if (camera == null)
                    continue;

                EditorGUILayout.LabelField(camera.name, camera.HasVisibilityTree ? "可见性树已就绪" : "缺少可见性树");
            }
        }

        private void DrawZones()
        {
            EditorGUILayout.LabelField("Camera Zone", EditorStyles.boldLabel);
            List<CameraZone> zones = CameraZone.Instances;
            if (zones == null || zones.Count == 0)
            {
                EditorGUILayout.HelpBox("当前没有注册 CameraZone。进入运行模式后可检查运行时实例。", MessageType.Info);
                return;
            }

            foreach (CameraZone zone in zones)
            {
                if (zone != null)
                    EditorGUILayout.LabelField(zone.name, zone.VisibilityTree != null ? "可见性树已就绪" : "尚未烘焙");
            }
        }

        private void SelectCullingCameras()
        {
            List<UnityEngine.Object> selection = new List<UnityEngine.Object>();
            selection.AddRange(FindObjectsOfType<DC_Camera>(true));
            selection.AddRange(FindObjectsOfType<StaticCullingCamera>(true));
            Selection.objects = selection.ToArray();
        }

        private void DrawSceneOverlay(SceneView sceneView)
        {
            if (!_drawSceneOverlay)
                return;

            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

            if (_drawZoneBounds)
            {
                foreach (CameraZone zone in FindObjectsOfType<CameraZone>(true))
                {
                    if (zone == null)
                        continue;

                    Color color = zone.VisibilityTree != null ? new Color(0.1f, 0.8f, 1f, 0.8f) : new Color(1f, 0.5f, 0.1f, 0.8f);
                    using (new Handles.DrawingScope(color, zone.transform.localToWorldMatrix))
                        Handles.DrawWireCube(Vector3.zero, Vector3.one);

                    Handles.Label(zone.transform.position, zone.name + (zone.VisibilityTree != null ? " [Baked]" : " [Not baked]"));
                }
            }

            if (_drawDynamicCameras)
            {
                foreach (DC_Camera camera in FindObjectsOfType<DC_Camera>(true))
                {
                    if (camera == null)
                        continue;

                    Color color = Color.Lerp(Color.green, Color.red, camera.LastRayHitRatio);
                    Handles.color = color;
                    Handles.DrawWireDisc(camera.transform.position, camera.transform.forward, 0.5f);
                    Handles.Label(camera.transform.position, camera.name + "  hit " + (camera.LastRayHitRatio * 100f).ToString("0") + "%");
                }
            }
        }
    }
}
