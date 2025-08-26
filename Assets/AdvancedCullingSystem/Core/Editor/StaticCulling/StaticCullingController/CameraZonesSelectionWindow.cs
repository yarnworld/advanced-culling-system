using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace NGS.AdvancedCullingSystem.Static
{
    public class CameraZonesSelectionWindow : EditorWindow
    {
        private StaticCullingController _controller;

        private static GUIStyle TitleLabelStyle;
        private static GUIStyle HeaderLabelStyle;
        private static GUIStyle ButtonGUIStyle;


        public void Initialize(StaticCullingController controller)
        {
            _controller = controller;
        }

        private void OnGUI()
        {
            if (_controller == null)
                Close();

            if (Application.isPlaying)
            {
                Debug.Log("Can't open window in runtime");
                Close();
            }
            
            if (TitleLabelStyle == null)
                CreateGUIStyles();

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Camera Zones", TitleLabelStyle);
            EditorHelper.DrawSeparatorLine(1, 2);

            EditorGUILayout.Space();

            IReadOnlyList<CameraZone> cameraZones = _controller.CameraZones;

            if (cameraZones != null)
            {
                int i = 0;
                while (i < cameraZones.Count)
                {
                    CameraZone zone = cameraZones[i];

                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.ObjectField(cameraZones[i], typeof(CameraZone), false);

                    if (GUILayout.Button("Select"))
                        Select(zone);

                    if (GUILayout.Button("Clear"))
                        Clear(zone);

                    if (GUILayout.Button("Delete"))
                        Delete(zone);

                    EditorGUILayout.EndHorizontal();

                    i++;
                }
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Create New", ButtonGUIStyle))
                CreateNew();

            if (GUILayout.Button("Add Selected", ButtonGUIStyle))
                AddSelected();
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

        private void Select(CameraZone zone)
        {
            Selection.activeObject = zone.gameObject;
        }

        private void Clear(CameraZone zone)
        {
            _controller.RemoveCameraZone(zone);
        }

        private void Delete(CameraZone zone)
        {
            _controller.RemoveCameraZone(zone);

            DestroyImmediate(zone.gameObject);
        }

        private void CreateNew()
        {
            GameObject go = new GameObject("Camera Zone");
            go.transform.localScale = Vector3.one * 10;

            CameraZone zone = go.AddComponent<CameraZone>();

            _controller.AddCameraZone(zone);
        }

        private void AddSelected()
        {
            int count = 0;

            foreach (var go in Selection.gameObjects)
            {
                CameraZone[] zones = go.GetComponentsInChildren<CameraZone>();

                if (zones == null)
                    continue;

                foreach (var zone in zones)
                {
                    if (_controller.AddCameraZone(zone))
                        count++;
                }
            }

            Debug.Log("Added " + count + " new camera zones");
        }
    }
}
