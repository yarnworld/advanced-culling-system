using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
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
            CullingDiagnostics.FrameSample sample = CullingDiagnostics.Current;
            EditorGUILayout.LabelField("帧号", sample.Frame.ToString());
            EditorGUILayout.LabelField("动态相机数", sample.CameraCount.ToString());
            EditorGUILayout.LabelField("射线数量", sample.RaycastCount.ToString());
            EditorGUILayout.LabelField("射线命中数", sample.HitCount.ToString());
            EditorGUILayout.LabelField("射线耗时", sample.RaycastMilliseconds.ToString("0.000") + " ms");
            Rect ratioRect = GUILayoutUtility.GetRect(18f, 18f);
            EditorGUI.ProgressBar(ratioRect, sample.HitRatio, "命中率 " + (sample.HitRatio * 100f).ToString("0.0") + "%");

            if (_showHistory)
                DrawHistoryGraph();

            if (GUILayout.Button("清空性能历史"))
                CullingDiagnostics.Clear();
        }

        private void DrawHistoryGraph()
        {
            Rect rect = GUILayoutUtility.GetRect(0f, 90f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.08f, 0.08f, 0.08f));
            Handles.BeginGUI();
            Handles.color = new Color(0.2f, 0.9f, 0.4f, 1f);
            Vector3 previous = Vector3.zero;
            bool hasPrevious = false;
            for (int i = 0; i < CullingDiagnostics.SampleCount; i++)
            {
                CullingDiagnostics.FrameSample sample = CullingDiagnostics.GetHistory(i);
                float x = rect.x + rect.width * (i + 1) / Mathf.Max(1f, CullingDiagnostics.HistoryLength);
                float y = rect.yMax - Mathf.Clamp01(sample.HitRatio) * rect.height;
                Vector3 point = new Vector3(x, y, 0f);
                if (hasPrevious)
                    Handles.DrawLine(previous, point);
                previous = point;
                hasPrevious = true;
            }
            Handles.EndGUI();
            GUI.Label(new Rect(rect.x + 4f, rect.y + 4f, 180f, 18f), "绿色：射线命中率历史");
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
            List<Object> selection = new List<Object>();
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
