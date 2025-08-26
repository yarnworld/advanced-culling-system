using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace NGS.AdvancedCullingSystem.Static
{
    [CustomEditor(typeof(StaticCullingCamera))]
    public class StaticCullingCameraEditor : Editor
    {
        private static bool ShowFrustum;

        protected new StaticCullingCamera target
        {
            get
            {
                return base.target as StaticCullingCamera;
            }
        }

        private SerializedProperty _drawCellsProp;
        private SerializedProperty _toleranceProp;
        private Camera _camera;


        private void OnEnable()
        {
            _drawCellsProp = serializedObject.FindProperty("_drawCells");
            _toleranceProp = serializedObject.FindProperty("_tolerance");
            _camera = target.GetComponent<Camera>();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();

            ShowFrustum = EditorGUILayout.Toggle("Show Frustum", ShowFrustum);

            if (EditorGUI.EndChangeCheck())
                ResetSceneCamerasCullingMatrices();

            
            EditorGUILayout.PropertyField(_drawCellsProp);


            Vector2 tolerance = _toleranceProp.vector2Value;

            EditorGUILayout.LabelField("Tolerance");

            EditorGUI.indentLevel++;

            tolerance.x = EditorGUILayout.Slider("X", tolerance.x, 0, 10);
            tolerance.y = EditorGUILayout.Slider("Y", tolerance.y, 0, 10);

            EditorGUI.indentLevel--;

            _toleranceProp.vector2Value = tolerance;


            serializedObject.ApplyModifiedProperties();
        }

        private void OnSceneGUI()
        {
            if (!Application.isPlaying)
                return;

            if (!ShowFrustum)
                return;

            foreach (var camera in SceneView.GetAllSceneCameras())
                camera.cullingMatrix = _camera.cullingMatrix;
        }

        private void OnDisable()
        {
            ResetSceneCamerasCullingMatrices();
        }


        private void ResetSceneCamerasCullingMatrices()
        {
            foreach (var camera in SceneView.GetAllSceneCameras())
                camera.ResetCullingMatrix();
        }
    }
}
