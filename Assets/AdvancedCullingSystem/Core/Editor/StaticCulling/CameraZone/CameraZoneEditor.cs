using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace NGS.AdvancedCullingSystem.Static
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(CameraZone))]
    public class CameraZoneEditor : Editor
    {
        protected new CameraZone target
        {
            get
            {
                return base.target as CameraZone;
            }
        }

        private static bool DrawGizmo = true;
        private static BinaryTreeDrawer TreeDrawer;

        private BoxBoundsHandle _boundsHandle;


        private void OnEnable()
        {
            _boundsHandle = new BoxBoundsHandle();

            if (TreeDrawer == null)
            {
                TreeDrawer = new BinaryTreeDrawer();
                TreeDrawer.Color = Color.white;
            }
        }

        public override void OnInspectorGUI()
        {
            if (target.VisibilityTree != null)
            {
                DrawGizmo = EditorGUILayout.Toggle("Draw Gizmo", DrawGizmo);

                EditorGUILayout.HelpBox("Cells Count : " + target.CellsCount, MessageType.None);

                if (GUILayout.Button("Clear"))
                    target.ClearVisibilityTree();
            }
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        private static void OnDrawGizmos(CameraZone cameraZone, GizmoType gizmoType)
        {
            if (!DrawGizmo)
                return;

            if (cameraZone.VisibilityTree != null)
            {
                TreeDrawer.DrawTreeGizmos(cameraZone.VisibilityTree.Root);
            }
        }

        private void OnSceneGUI()
        {
            if (target.VisibilityTree != null)
                return;

            Transform transform = target.transform;

            _boundsHandle.center = transform.position;
            _boundsHandle.size = transform.localScale;

            _boundsHandle.DrawHandle();

            transform.position = _boundsHandle.center;
            transform.localScale = _boundsHandle.size;
        }
    }
}
