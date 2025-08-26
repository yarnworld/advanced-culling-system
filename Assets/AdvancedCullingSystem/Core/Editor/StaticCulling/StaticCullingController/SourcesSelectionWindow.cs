using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace NGS.AdvancedCullingSystem.Static
{
    public class SourcesSelectionWindow : EditorWindow
    {
        private static GUIStyle TitleLabelStyle;
        private static GUIStyle HeaderLabelStyle;
        private static GUIStyle ButtonGUIStyle;

        private StaticCullingController _controller;
        private string[] _tabNames;
        private ITab[] _tabs;
        private int _tabIndex = 0;


        public void Initialize(StaticCullingController controller)
        {
            _controller = controller;
        }

        private void OnEnable()
        {
            name = "Objects Selection";

            _tabNames = new string[] { "Cameras", "Renderers", "LODGroups", "Lights", "Custom" };
            _tabs = new ITab[] 
            {
                new CamerasTab(),
                new RenderersTab(),
                new LODGroupsTab(),
                new LightsTab(),
                new CustomTab()
            };

            foreach (var tab in _tabs)
                tab.Refresh();
        }

        private void OnGUI()
        {
            if (_controller == null)
                Close();

            if (Application.isPlaying)
            {
                Close();
                Debug.Log("Objects selection not available in runtime");
            }

            if (TitleLabelStyle == null)
                CreateGUIStyles();

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Objects Selection", TitleLabelStyle);

            EditorHelper.DrawSeparatorLine(1, 2);

            EditorGUILayout.Space();

            _tabIndex = GUILayout.Toolbar(_tabIndex, _tabNames, ButtonGUIStyle);

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            bool sceneChanged = false;

            _tabs[_tabIndex].OnInspectorGUI(ref sceneChanged);

            if (sceneChanged)
            {
                foreach (var tab in _tabs)
                    tab.Refresh();

                EditorUtility.SetDirty(_controller);
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

        private void DrawCustomTab()
        {
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox("To set a custom CullingSource you need to manually attach " +
                "the 'StaticCullingSource' script to the object, specify the 'SourceType' as Custom, " +
                "and then configure the 'StaticCullingSource' component.", MessageType.Info);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Custom Sources : 124", HeaderLabelStyle);

            EditorGUILayout.Space();

            EditorGUILayout.Toggle("DrawGizmo", true);

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();

            GUILayout.Button("Clear All", ButtonGUIStyle);
            GUILayout.Button("Clear Selected", ButtonGUIStyle);

            EditorGUILayout.EndHorizontal();

            GUILayout.Button("Select", ButtonGUIStyle);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Not Valid Custom Sources : 124", HeaderLabelStyle);

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical();

            EditorGUILayout.BeginHorizontal();

            GUILayout.Button("Print", ButtonGUIStyle);
            GUILayout.Button("Select", ButtonGUIStyle);

            EditorGUILayout.EndHorizontal();

            GUILayout.Button("Clear All", ButtonGUIStyle);

            EditorGUILayout.EndVertical();
        }


        private interface ITab
        {
            void Refresh();

            void OnInspectorGUI(ref bool sceneChanged);
        }

        private abstract class TabTemplate : ITab
        {
            protected abstract string SourceName { get; }
            protected abstract bool CanExistsNotValidSources { get; }
            protected abstract bool ShowOnlyStaticToggle { get; }
            protected abstract bool ShowSelectButton { get; }
            protected abstract bool ShowVerifyButton { get; }
            
            protected bool AssignOnlyStatic { get; private set; }
            protected int SourcesCount { get; private set; }
            protected int NotValidSourcesCount { get; private set; }


            public virtual void Refresh()
            {
                SourcesCount = 0;
                NotValidSourcesCount = 0;

                foreach (var go in FindObjectsOfType<GameObject>())
                {
                    if (ContainsSource(go))
                    {
                        SourcesCount++;

                        if (CanExistsNotValidSources && GetValidationError(go) != "")
                            NotValidSourcesCount++;
                    }
                }
            }

            public virtual void OnInspectorGUI(ref bool sceneChanged)
            {
                BeforeOnGUI();

                EditorGUILayout.LabelField(SourceName + " : " + SourcesCount, HeaderLabelStyle);

                EditorGUILayout.Space();
                EditorGUILayout.Space();

                if (ShowVerifyButton)
                {
                    if (GUILayout.Button("Verify Sources", ButtonGUIStyle))
                        VerifySources(ref sceneChanged);

                    EditorGUILayout.Space();
                }

                BeforeDrawContent();

                if (ShowOnlyStaticToggle)
                    AssignOnlyStatic = EditorGUILayout.Toggle("Assign Only Static", AssignOnlyStatic);

                EditorGUILayout.Space();

                DrawSourcesButtons(ref sceneChanged);

                if (CanExistsNotValidSources)
                {
                    EditorGUILayout.Space();

                    EditorGUILayout.LabelField("Not Valid " + SourceName + " : " + NotValidSourcesCount,
                        HeaderLabelStyle);

                    DrawNotValidSourcesButtons(ref sceneChanged);
                }

                AfterOnGUI();
            }


            protected virtual void BeforeOnGUI()
            {

            }

            protected virtual void BeforeDrawContent()
            {

            }

            protected virtual void DrawSourcesButtons(ref bool sceneChanged)
            {
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.BeginVertical();

                if (GUILayout.Button("Assign Auto", ButtonGUIStyle))
                    AssignAutoButtonClick(ref sceneChanged);

                if (GUILayout.Button("Clear All", ButtonGUIStyle))
                    ClearAllButtonClick(ref sceneChanged);

                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginVertical();

                if (GUILayout.Button("Assign Selected", ButtonGUIStyle))
                    AssignSelectedButtonClick(ref sceneChanged);

                if (GUILayout.Button("Clear Selected", ButtonGUIStyle))
                    ClearSelectedButtonClick(ref sceneChanged);

                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();

                if (ShowSelectButton)
                {
                    if (GUILayout.Button("Select", ButtonGUIStyle))
                        SelectButtonClick(ref sceneChanged);
                }
            }

            protected virtual void DrawNotValidSourcesButtons(ref bool sceneChanged)
            {
                EditorGUILayout.Space();
                EditorGUILayout.Space();

                EditorGUILayout.BeginVertical();

                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Print", ButtonGUIStyle))
                    NotValidPrintButtonClick(ref sceneChanged);

                if (GUILayout.Button("Select", ButtonGUIStyle))
                    NotValidSelectButtonClick(ref sceneChanged);

                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Clear All", ButtonGUIStyle))
                    NotValidClearButtonClick(ref sceneChanged);

                EditorGUILayout.EndVertical();
            }

            protected virtual void AfterOnGUI()
            {
                
            }


            protected abstract bool ContainsSource(GameObject go);

            protected abstract bool CanAssignTo(GameObject go);

            protected abstract void AssignSourceTo(GameObject go);

            protected abstract void ClearSourceFrom(GameObject go);

            protected abstract bool ValidateSource(GameObject go, out string error);

            protected abstract string GetValidationError(GameObject go);


            protected void VerifySources(ref bool sceneChanged)
            {
                foreach (var go in FindObjectsOfType<GameObject>().Where(go => ContainsSource(go)))
                    ValidateSource(go, out string error);

                sceneChanged = true;
            }

            protected void AssignAutoButtonClick(ref bool sceneChanged)
            {
                int count = 0;

                foreach (var go in FindObjectsOfType<GameObject>())
                {
                    if (ContainsSource(go))
                        continue;

                    if (AssignOnlyStatic && !go.isStatic)
                        continue;

                    if (go.GetComponent<StaticCullingSource>() != null)
                        continue;

                    if (go.GetComponent<CullingTarget>() != null)
                        continue;

                    if (CanAssignTo(go))
                    {
                        AssignSourceTo(go);

                        sceneChanged = true;

                        count++;
                    }
                }

                Debug.Log("Assigned " + count + " new sources");
            }

            protected void AssignSelectedButtonClick(ref bool sceneChanged)
            {
                GameObject[] gos = Selection.gameObjects
                    .SelectMany(go => go.GetComponentsInChildren<Transform>()
                    .Select(t => t.gameObject))
                    .ToArray();

                int count = 0;

                foreach (var go in gos)
                {
                    if (ContainsSource(go))
                        continue;

                    if (AssignOnlyStatic && !go.isStatic)
                        continue;

                    if (go.GetComponent<StaticCullingSource>() != null)
                        continue;

                    if (go.GetComponent<CullingTarget>() != null)
                        continue;

                    if (CanAssignTo(go))
                    {
                        AssignSourceTo(go);

                        sceneChanged = true;

                        count++;
                    }
                }

                Debug.Log("Assigned " + count + " new sources");
            }

            protected void ClearAllButtonClick(ref bool sceneChanged)
            {
                int count = 0;

                foreach (var go in FindObjectsOfType<GameObject>())
                {
                    if (ContainsSource(go))
                    {
                        ClearSourceFrom(go);

                        sceneChanged = true;

                        count++;
                    }
                }

                Debug.Log("Cleared " + count + " sources");
            }

            protected void ClearSelectedButtonClick(ref bool sceneChanged)
            {
                GameObject[] gos = Selection.gameObjects
                   .SelectMany(go => go.GetComponentsInChildren<Transform>()
                   .Select(t => t.gameObject))
                   .ToArray();

                int count = 0;

                foreach (var go in gos)
                {
                    if (ContainsSource(go))
                    {
                        ClearSourceFrom(go);

                        sceneChanged = true;

                        count++;
                    }
                }

                Debug.Log("Cleared " + count + " sources");
            }

            protected void SelectButtonClick(ref bool sceneChanged)
            {
                GameObject[] gos = FindObjectsOfType<GameObject>()
                    .Where(go => ContainsSource(go))
                    .ToArray();

                Selection.objects = gos;
            }

            protected void NotValidPrintButtonClick(ref bool sceneChanged)
            {
                foreach (var go in FindObjectsOfType<GameObject>())
                {
                    if (ContainsSource(go))
                    {
                        string error = GetValidationError(go);

                        if (error == "")
                            continue;

                        Debug.Log(go.name + " : " + error);
                    }
                }
            }

            protected void NotValidClearButtonClick(ref bool sceneChanged)
            {
                int count = 0;

                foreach (var go in FindObjectsOfType<GameObject>())
                {
                    if (ContainsSource(go))
                    {
                        if (!ValidateSource(go, out string error))
                        {
                            ClearSourceFrom(go);

                            sceneChanged = true;

                            count++;
                        }
                    }
                }

                Debug.Log("Cleared " + count + " not valid sources");
            }

            protected void NotValidSelectButtonClick(ref bool sceneChanged)
            {
                List<UnityEngine.Object> notValidGos = new List<UnityEngine.Object>();

                foreach (var go in FindObjectsOfType<GameObject>())
                {
                    if (ContainsSource(go))
                    {
                        string error = GetValidationError(go);

                        if (error != "")
                            notValidGos.Add(go);
                    }
                }

                Selection.objects = notValidGos.ToArray();
            }
        }

        private class CamerasTab : TabTemplate
        {
            protected override string SourceName
            {
                get
                {
                    return "Cameras";
                }
            }    
            protected override bool ShowOnlyStaticToggle
            {
                get
                {
                    return false;
                }
            }
            protected override bool CanExistsNotValidSources
            {
                get
                {
                    return false;
                }
            }
            protected override bool ShowVerifyButton
            {
                get
                {
                    return false;
                }
            }
            protected override bool ShowSelectButton
            {
                get
                {
                    return true;
                }
            }

            protected override void BeforeDrawContent()
            {
                StaticCullingCamera.DrawGizmo = EditorGUILayout.Toggle("Draw Gizmo",
                    StaticCullingCamera.DrawGizmo);
            }

            protected override bool ContainsSource(GameObject go)
            {
                return go.GetComponent<StaticCullingCamera>() != null;
            }

            protected override bool CanAssignTo(GameObject go)
            {
                return go.activeInHierarchy && go.GetComponent<Camera>() != null;
            }

            protected override void AssignSourceTo(GameObject go)
            {
                go.AddComponent<StaticCullingCamera>();
            }

            protected override void ClearSourceFrom(GameObject go)
            {
                DestroyImmediate(go.GetComponent<StaticCullingCamera>());
            }


            protected override string GetValidationError(GameObject go)
            {
                throw new NotSupportedException();
            }

            protected override bool ValidateSource(GameObject go, out string error)
            {
                throw new NotSupportedException();
            }
        }

        private class RenderersTab : TabTemplate
        {
            protected override string SourceName
            {
                get
                {
                    return "Renderers";
                }
            }
            protected override bool ShowOnlyStaticToggle
            {
                get
                {
                    return true;
                }
            }
            protected override bool CanExistsNotValidSources
            {
                get
                {
                    return true;
                }
            }
            protected override bool ShowVerifyButton
            {
                get
                {
                    return true;
                }
            }
            protected override bool ShowSelectButton
            {
                get
                {
                    return false;
                }
            }


            protected override void BeforeDrawContent()
            {
                StaticCullingSource.DrawGizmoRenderers = EditorGUILayout.Toggle("Draw Gizmo", 
                    StaticCullingSource.DrawGizmoRenderers);
            }

            protected override void AssignSourceTo(GameObject go)
            {
                StaticCullingSource source = go.AddComponent<StaticCullingSource>();
                source.SourceType = SourceType.MeshRenderer;
            }

            protected override bool CanAssignTo(GameObject go)
            {
                MeshRenderer renderer = go.GetComponent<MeshRenderer>();

                return go.activeInHierarchy && renderer != null && renderer.enabled && 
                    go.GetComponentInParent<LODGroup>() == null;
            }

            protected override void ClearSourceFrom(GameObject go)
            {
                DestroyImmediate(go.GetComponent<StaticCullingSource>());
            }

            protected override string GetValidationError(GameObject go)
            {
                return go.GetComponent<StaticCullingSource>().ValidationError;
            }

            protected override bool ContainsSource(GameObject go)
            {
                StaticCullingSource source = go.GetComponent<StaticCullingSource>();

                return source != null && source.SourceType == SourceType.MeshRenderer;
            }

            protected override bool ValidateSource(GameObject go, out string error)
            {
                StaticCullingSource source = go.GetComponent<StaticCullingSource>();

                bool isValid = source.Validate();

                error = source.ValidationError;

                return isValid;
            }
        }

        private class LODGroupsTab : TabTemplate
        {
            protected override string SourceName
            {
                get
                {
                    return "LODGroups";
                }
            }
            protected override bool CanExistsNotValidSources
            {
                get
                {
                    return true;
                }
            }
            protected override bool ShowOnlyStaticToggle
            {
                get
                {
                    return true;
                }
            }
            protected override bool ShowSelectButton
            {
                get
                {
                    return false;
                }
            }
            protected override bool ShowVerifyButton
            {
                get
                {
                    return true;
                }
            }


            protected override void BeforeDrawContent()
            {
                StaticCullingSource.DrawGizmoLODGroups = EditorGUILayout.Toggle("Draw Gizmo",
                    StaticCullingSource.DrawGizmoLODGroups);
            }

            protected override bool ValidateSource(GameObject go, out string error)
            {
                StaticCullingSource source = go.GetComponent<StaticCullingSource>();

                bool isValid = source.Validate();

                error = source.ValidationError;

                return isValid;
            }

            protected override string GetValidationError(GameObject go)
            {
                return go.GetComponent<StaticCullingSource>().ValidationError;
            }

            protected override bool ContainsSource(GameObject go)
            {
                StaticCullingSource source = go.GetComponent<StaticCullingSource>();

                return source != null && source.SourceType == SourceType.LODGroup;
            }

            protected override bool CanAssignTo(GameObject go)
            {
                return go.activeInHierarchy && go.GetComponent<LODGroup>() != null;
            }

            protected override void AssignSourceTo(GameObject go)
            {
                StaticCullingSource source = go.AddComponent<StaticCullingSource>();
                source.SourceType = SourceType.LODGroup;
            }

            protected override void ClearSourceFrom(GameObject go)
            {
                DestroyImmediate(go.GetComponent<StaticCullingSource>());
            }
        }

        private class LightsTab : TabTemplate
        {
            protected override string SourceName
            {
                get 
                {
                    return "Lights";
                }
            }
            protected override bool CanExistsNotValidSources
            {
                get
                {
                    return true;
                }
            }
            protected override bool ShowOnlyStaticToggle
            {
                get 
                {
                    return false;
                }
            }
            protected override bool ShowSelectButton
            {
                get
                {
                    return true;
                }
            }
            protected override bool ShowVerifyButton
            {
                get
                {
                    return true;
                }
            }


            protected override void BeforeOnGUI()
            {
                EditorGUILayout.HelpBox("To avoid errors, you can only add PointLights in this window. " +
                    "To add other light sources - you need to manually " +
                    "attach 'StaticCullingSource' script " +
                    "to your Light, then specify SourceType as 'Light' " +
                    "and set up the bounding box.", MessageType.Info);

                EditorGUILayout.Space();
            }

            protected override void BeforeDrawContent()
            {
                StaticCullingSource.DrawGizmoLights = EditorGUILayout.Toggle("Draw Gizmo",
                    StaticCullingSource.DrawGizmoLights);
            }

            protected override bool ValidateSource(GameObject go, out string error)
            {
                StaticCullingSource source = go.GetComponent<StaticCullingSource>();

                bool isValid = source.Validate();

                error = source.ValidationError;

                return isValid;
            }

            protected override string GetValidationError(GameObject go)
            {
                return go.GetComponent<StaticCullingSource>().ValidationError;
            }

            protected override bool ContainsSource(GameObject go)
            {
                StaticCullingSource source = go.GetComponent<StaticCullingSource>();

                return source != null && source.SourceType == SourceType.Light;
            }

            protected override bool CanAssignTo(GameObject go)
            {
                Light light = go.GetComponent<Light>();

                return light != null && light.type == LightType.Point;
            }

            protected override void AssignSourceTo(GameObject go)
            {
                StaticCullingSource source = go.AddComponent<StaticCullingSource>();
                source.SourceType = SourceType.Light;
            }

            protected override void ClearSourceFrom(GameObject go)
            {
                DestroyImmediate(go.GetComponent<StaticCullingSource>());
            }
        }

        private class CustomTab : TabTemplate
        {
            protected override string SourceName
            {
                get
                {
                    return "Custom";
                }
            }
            protected override bool CanExistsNotValidSources
            {
                get
                {
                    return true;
                }
            }
            protected override bool ShowOnlyStaticToggle
            {
                get
                {
                    return false;
                }
            }
            protected override bool ShowSelectButton
            {
                get
                {
                    return true;
                }
            }
            protected override bool ShowVerifyButton
            {
                get
                {
                    return true;
                }
            }


            protected override void BeforeOnGUI()
            {
                EditorGUILayout.HelpBox("To set a custom CullingSource you need to manually attach " +
                "the 'StaticCullingSource' script to the object, specify the 'SourceType' as Custom, " +
                "and then configure the 'StaticCullingSource' component.", MessageType.Info);

                EditorGUILayout.Space();
            }

            protected override void BeforeDrawContent()
            {
                StaticCullingSource.DrawGizmoCustom = EditorGUILayout.Toggle("Draw Gizmo",
                    StaticCullingSource.DrawGizmoCustom);
            }

            protected override void DrawSourcesButtons(ref bool sceneChanged)
            {
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Clear All", ButtonGUIStyle))
                    ClearAllButtonClick(ref sceneChanged);

                if (GUILayout.Button("Clear Selected", ButtonGUIStyle))
                    ClearSelectedButtonClick(ref sceneChanged);

                EditorGUILayout.EndHorizontal();

                if (ShowSelectButton)
                {
                    if (GUILayout.Button("Select", ButtonGUIStyle))
                        SelectButtonClick(ref sceneChanged);
                }
            }

            protected override bool ContainsSource(GameObject go)
            {
                StaticCullingSource source = go.GetComponent<StaticCullingSource>();

                return source != null && source.SourceType == SourceType.Custom;
            }

            protected override bool CanAssignTo(GameObject go)
            {
                throw new NotSupportedException();
            }

            protected override void AssignSourceTo(GameObject go)
            {
                throw new NotSupportedException();
            }

            protected override void ClearSourceFrom(GameObject go)
            {
                DestroyImmediate(go.GetComponent<StaticCullingSource>());
            }

            protected override string GetValidationError(GameObject go)
            {
                return go.GetComponent<StaticCullingSource>().ValidationError;
            }

            protected override bool ValidateSource(GameObject go, out string error)
            {
                StaticCullingSource source = go.GetComponent<StaticCullingSource>();

                bool isValid = source.Validate();

                error = source.ValidationError;

                return isValid;
            }
        }
    }
}
