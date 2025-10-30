using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using NGS.AdvancedCullingSystem.Dynamic;
using NGS.AdvancedCullingSystem.Static;

namespace NGS.AdvancedCullingSystem.Editor
{
    /// <summary>
    /// Scene-wide diagnostic view for validating culling setup and runtime state.
    /// </summary>
    public sealed class CullingVisualizationWindow : EditorWindow
    {
        private bool _drawSceneOverlay = true;
        private bool _drawZoneBounds = true;
        private bool _drawDynamicCameras = true;
        private bool _autoRefresh = true;
        private Vector2 _scroll;

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
            EditorGUILayout.LabelField("Culling Visualization", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("用于检查裁剪相机、Camera Zone、烘焙状态和动态射线命中率。运行游戏后可查看实时数据。", MessageType.Info);

            _autoRefresh = EditorGUILayout.ToggleLeft("Auto refresh", _autoRefresh);
            _drawSceneOverlay = EditorGUILayout.ToggleLeft("Draw scene overlay", _drawSceneOverlay);
            _drawZoneBounds = EditorGUILayout.ToggleLeft("Draw Camera Zone bounds", _drawZoneBounds);
            _drawDynamicCameras = EditorGUILayout.ToggleLeft("Draw dynamic camera labels", _drawDynamicCameras);

            if (GUILayout.Button("Select all culling cameras"))
                SelectCullingCameras();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawDynamicCameras();
            EditorGUILayout.Space(8);
            DrawStaticCameras();
            EditorGUILayout.Space(8);
            DrawZones();
            EditorGUILayout.EndScrollView();
        }

        private void DrawDynamicCameras()
        {
            EditorGUILayout.LabelField("Dynamic Culling Cameras", EditorStyles.boldLabel);
            DC_Camera[] cameras = FindObjectsOfType<DC_Camera>(true);
            if (cameras.Length == 0)
            {
                EditorGUILayout.HelpBox("No DC_Camera found in the loaded scenes.", MessageType.Warning);
                return;
            }

            foreach (DC_Camera cullingCamera in cameras)
            {
                if (cullingCamera == null)
                    continue;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(cullingCamera.name, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Active", cullingCamera.IsCullingActive ? "Yes" : "No");
                    EditorGUILayout.LabelField("Raycasts", cullingCamera.LastRaycastCount.ToString());
                    EditorGUILayout.LabelField("Last hit ratio", (cullingCamera.LastRayHitRatio * 100f).ToString("0.0") + "%");
                    Rect rect = GUILayoutUtility.GetRect(18f, 18f);
                    EditorGUI.ProgressBar(rect, cullingCamera.LastRayHitRatio, "Ray hit ratio");
                }
            }
        }

        private void DrawStaticCameras()
        {
            EditorGUILayout.LabelField("Static Culling Cameras", EditorStyles.boldLabel);
            StaticCullingCamera[] cameras = FindObjectsOfType<StaticCullingCamera>(true);
            if (cameras.Length == 0)
            {
                EditorGUILayout.HelpBox("No StaticCullingCamera found in the loaded scenes.", MessageType.Warning);
                return;
            }

            foreach (StaticCullingCamera camera in cameras)
            {
                if (camera == null)
                    continue;

                EditorGUILayout.LabelField(camera.name, camera.HasVisibilityTree ? "Baked tree ready" : "Missing baked tree");
            }
        }

        private void DrawZones()
        {
            EditorGUILayout.LabelField("Camera Zones", EditorStyles.boldLabel);
            List<CameraZone> zones = CameraZone.Instances;
            if (zones == null || zones.Count == 0)
            {
                EditorGUILayout.HelpBox("No CameraZone is registered at runtime. Enter Play Mode to inspect runtime instances.", MessageType.Info);
                return;
            }

            foreach (CameraZone zone in zones)
            {
                if (zone != null)
                    EditorGUILayout.LabelField(zone.name, zone.VisibilityTree != null ? "Tree ready" : "Not baked");
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
