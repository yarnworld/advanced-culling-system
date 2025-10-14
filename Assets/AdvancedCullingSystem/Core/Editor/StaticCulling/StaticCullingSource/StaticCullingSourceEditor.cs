using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace NGS.AdvancedCullingSystem.Static
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(StaticCullingSource))]
    public class StaticCullingSourceEditor : Editor
    {
        protected new StaticCullingSource target
        {
            get
            {
                return base.target as StaticCullingSource;
            }
        }

        private SerializedProperty _validationErrorProp;
        private SerializedProperty _sourceTypeProp;
        private SerializedProperty _strategyProp;
        private BoxBoundsHandle _boundsHandle;


        public void OnEnable()
        {
            _boundsHandle = new BoxBoundsHandle();

            _validationErrorProp = serializedObject.FindProperty("_validationError");
            _sourceTypeProp = serializedObject.FindProperty("_sourceType");
            _strategyProp = serializedObject.FindProperty("_strategy");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawHelpBoxes();

            if (DrawProperties())
                ApplyModifiedProperties();

            EditorGUILayout.Space();

            serializedObject.Update();

            DrawStrategyProperties();

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();

            if (GUILayout.Button("Verify"))
            {
                foreach (var target in targets)
                {
                    StaticCullingSource current = target as StaticCullingSource;

                    if (current.Validate())
                        Debug.Log(current.gameObject.name + " is valid");
                    else
                        Debug.Log(current.gameObject.name + " not valid");
                }
            }    
        }

        private void OnSceneGUI()
        {
            SourceType type = target.SourceType;

            Bounds localBounds = default;
 
            if (type == SourceType.Light)
                localBounds = ((LightStaticCullingSourceStrategy)target.Strategy).LocalBounds;

            else if (type == SourceType.Custom)
                localBounds = ((CustomStaticCullingSourceStrategy)target.Strategy).LocalBounds;

            else
                return;

            _boundsHandle.center = target.transform.position + localBounds.center;
            _boundsHandle.size = localBounds.size;

            _boundsHandle.DrawHandle();

            localBounds.center = _boundsHandle.center - target.transform.position;
            localBounds.size = _boundsHandle.size;

            if (type == SourceType.Light)
                ((LightStaticCullingSourceStrategy)target.Strategy).LocalBounds = localBounds;

            else if (type == SourceType.Custom)
                ((CustomStaticCullingSourceStrategy)target.Strategy).LocalBounds = localBounds;
        }


        private void DrawHelpBoxes()
        {
            if (!_validationErrorProp.hasMultipleDifferentValues)
            {
                string validationError = _validationErrorProp.stringValue;

                if (validationError != "")
                    EditorGUILayout.HelpBox(validationError, MessageType.Warning);
            }
        }

        private bool DrawProperties()
        {
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(_sourceTypeProp);

            return EditorGUI.EndChangeCheck();
        }

        private void ApplyModifiedProperties()
        {
            foreach (var target in targets)
            {
                StaticCullingSource current = target as StaticCullingSource;

                if (!_sourceTypeProp.hasMultipleDifferentValues)
                    current.SourceType = (SourceType) _sourceTypeProp.enumValueIndex;
            }
        }


        private void DrawStrategyProperties()
        {
            if (_sourceTypeProp.hasMultipleDifferentValues)
                return;

            SourceType type = (SourceType) _sourceTypeProp.enumValueIndex;

            if (type == SourceType.MeshRenderer)
                DrawMeshRendererSourceStrategy();

            else if (type == SourceType.LODGroup)
                DrawLODGroupSourceStrategy();

            else if (type == SourceType.Light)
                DrawLightSourceStrategy();

            else if (type == SourceType.Custom)
                DrawCustomSourceStrategy();
            else
                throw new System.NotSupportedException();
        }

        private void DrawMeshRendererSourceStrategy()
        {
            SerializedProperty cullingMethodProp = _strategyProp.FindPropertyRelative("_cullingMethod");
            SerializedProperty isOccluderProp = _strategyProp.FindPropertyRelative("_isOccluder");

            EditorGUILayout.PropertyField(cullingMethodProp);
            EditorGUILayout.PropertyField(isOccluderProp);
        }

        private void DrawLODGroupSourceStrategy()
        {
            SerializedProperty isOccluderProp = _strategyProp.FindPropertyRelative("_isOccluder");
            SerializedProperty cullingMethodProp = _strategyProp.FindPropertyRelative("_cullingMethod");

            EditorGUILayout.PropertyField(cullingMethodProp);
            EditorGUILayout.PropertyField(isOccluderProp);
        }

        private void DrawLightSourceStrategy()
        {
            
        }

        private void DrawCustomSourceStrategy()
        {
            SerializedProperty isOccluderProp = _strategyProp.FindPropertyRelative("_isOccluder");
            SerializedProperty onVisibleProp = _strategyProp.FindPropertyRelative("_onVisible");
            SerializedProperty onInvisibleProp = _strategyProp.FindPropertyRelative("_onInvisible");

            EditorGUILayout.PropertyField(isOccluderProp);

            if (!isOccluderProp.hasMultipleDifferentValues && isOccluderProp.boolValue)
            {
                SerializedProperty collidersProp = _strategyProp.FindPropertyRelative("_colliders");

                EditorGUILayout.PropertyField(collidersProp);
            }

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(onVisibleProp);
            EditorGUILayout.PropertyField(onInvisibleProp);
        }
    }
}
