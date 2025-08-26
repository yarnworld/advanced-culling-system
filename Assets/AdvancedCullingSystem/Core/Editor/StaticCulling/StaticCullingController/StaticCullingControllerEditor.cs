using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.EditorTools;

namespace NGS.AdvancedCullingSystem.Static
{
    [CustomEditor(typeof(StaticCullingController))]
    public class StaticCullingControllerEditor : Editor
    {
        protected new StaticCullingController target
        {
            get
            {
                return (StaticCullingController)base.target;
            }
        }

        private static GUIStyle TitleLabelStyle;
        private static GUIStyle HeaderLabelStyle;
        private static GUIStyle ButtonGUIStyle;

        private bool _sceneContainsBakedData;


        [MenuItem("Tools/NGSTools/Advanced Culling System/Static")]
        private static void CreateController()
        {
            GameObject go = new GameObject("StaticCullingController");

            go.AddComponent<StaticCullingController>();

            Selection.activeGameObject = go;
        }

        private void OnEnable()
        {
            CreateLayerIfNotExist();

            Refresh();
        }

        public override void OnInspectorGUI()
        {
            if (TitleLabelStyle == null)
                CreateGUIStyles();

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Static Culling", TitleLabelStyle);

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Step 1. Objects Selection", HeaderLabelStyle);
            EditorHelper.DrawSeparatorLine(1, 2);

            DrawStep1();

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Step 2. Scene Partitioning", HeaderLabelStyle);
            EditorHelper.DrawSeparatorLine(1, 2);

            DrawStep2();

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Step 3. Camera Zones", HeaderLabelStyle);
            EditorHelper.DrawSeparatorLine(1, 2);

            DrawStep3();

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Step 4. Baking", HeaderLabelStyle);
            EditorHelper.DrawSeparatorLine(1, 2);

            DrawStep4();
        }


        private void Refresh()
        {
            _sceneContainsBakedData = false;

            if (FindObjectsOfType<CullingTarget>().Length > 0)
            {
                _sceneContainsBakedData = true;
                return;
            }

            foreach (var zone in FindObjectsOfType<CameraZone>())
            {
                if (zone.VisibilityTree != null && zone.VisibilityTree.CullingTargets != null)
                {
                    _sceneContainsBakedData = true;
                    return;
                }
            }
        }

        private void CreateLayerIfNotExist()
        {
             string layer = StaticCullingPreferences.LayerName;

            if (!LayersHelper.IsLayerExist(layer))
            {
                LayersHelper.CreateLayer(layer);
                LayersHelper.DisableCollisions(LayerMask.NameToLayer(layer));
            }
        }

        private void CreateGUIStyles()
        {
            TitleLabelStyle = new GUIStyle();
            TitleLabelStyle.fontSize = 24;
            TitleLabelStyle.fontStyle = FontStyle.Bold;
            TitleLabelStyle.alignment = TextAnchor.MiddleCenter;
            TitleLabelStyle.normal.textColor = Color.white;

            HeaderLabelStyle = new GUIStyle();
            HeaderLabelStyle.fontSize = 17;
            HeaderLabelStyle.fontStyle = FontStyle.Bold;
            HeaderLabelStyle.alignment = TextAnchor.MiddleLeft;
            HeaderLabelStyle.normal.textColor = Color.white;

            ButtonGUIStyle = new GUIStyle(GUI.skin.button);
            ButtonGUIStyle.fontSize = 12;
            ButtonGUIStyle.fixedHeight = 24;
            ButtonGUIStyle.margin = new RectOffset(5, 5, 5, 5);
            ButtonGUIStyle.border = new RectOffset(0, 0, 0, 0);
            ButtonGUIStyle.padding = new RectOffset(5, 5, 5, 5);
        }

        private void DrawStep1()
        {
            EditorGUILayout.HelpBox("Add cameras and objects to be culled", MessageType.None);

            if (GUILayout.Button("Open Selection Tool", ButtonGUIStyle))
            {
                EditorWindow.GetWindow<SourcesSelectionWindow>().Initialize(target);
            }
        }

        private void DrawStep2()
        {
            EditorGUILayout.HelpBox("Partition the scene into cells of optimal size. " +
                "If a cell is visible, all objects inside this cell will be enabled. " +
                "The more cells - the more objects will be culled, " +
                "but the longer the baking time will be.", MessageType.None);

            target.DrawGeometryTreeGizmo = EditorGUILayout.Toggle("Draw Gizmo",
                target.DrawGeometryTreeGizmo);

            EditorGUI.BeginChangeCheck();

            target.GeometryTreeDepth = EditorGUILayout.IntSlider("Partition", 
                target.GeometryTreeDepth, 7, 20);

            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(target);

            EditorGUILayout.Space();

            if (GUILayout.Button("Update", ButtonGUIStyle))
                target.CreatePreviewGeometryTree();
        }

        private void DrawStep3()
        {
            EditorGUILayout.HelpBox("Select the areas where cameras can be located during gameplay. " +
                "Divide these areas into cells. " +
                "The smaller the cell, the more accurate the objects will be culled, " +
                "but the longer the baking time.", MessageType.None);

            target.DrawCameraZones = EditorGUILayout.Toggle("Draw Gizmo", target.DrawCameraZones);

            EditorGUI.BeginChangeCheck();

            target.CellSize = EditorGUILayout.FloatField("Cell Size", target.CellSize);

            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(target);

            EditorGUILayout.HelpBox("Total Cells Count : " + target.TotalCellsCount, MessageType.None);

            EditorGUILayout.Space();

            if (GUILayout.Button("Update", ButtonGUIStyle))
                target.CreatePreviewCameraZones();

            if (GUILayout.Button("Open Selection Tool", ButtonGUIStyle))
            {
                var window = EditorWindow.GetWindow<CameraZonesSelectionWindow>();
                window.Initialize(target);
            }
        }

        private void DrawStep4()
        {
            EditorGUILayout.HelpBox("Check the settings in each step again. " +
                "Specify the number of rays per unit and press bake.", MessageType.None);

            EditorGUI.BeginChangeCheck();

            target.RaysPerUnit = EditorGUILayout.FloatField("Rays Per Unit", target.RaysPerUnit);
            target.MaxRaysPerSource = EditorGUILayout.IntField("Max Rays Per Source", target.MaxRaysPerSource);

            EditorGUILayout.Space();

            if (GUILayout.Button("Bake", ButtonGUIStyle))
            {
                target.Bake();
                Refresh();
            }

            EditorGUI.BeginDisabledGroup(!_sceneContainsBakedData);

            if (GUILayout.Button("Clear", ButtonGUIStyle))
            {
                target.Clear();
                Refresh();
            }

            EditorGUI.EndDisabledGroup();

            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(target);
        }
    }
}
