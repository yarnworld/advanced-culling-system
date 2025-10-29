using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Static
{
    /// <summary>
    /// 静态剔除相机组件
    /// 负责根据相机位置更新可见性树（VisibilityTree）
    /// </summary>
    [DisallowMultipleComponent] // 不允许同一 GameObject 添加多个此组件
    [RequireComponent(typeof(Camera))] // 必须附加 Camera 组件
    public class StaticCullingCamera : MonoBehaviour
    {
        [SerializeField]
        private bool _drawCells; // 是否在编辑器中绘制 Cell 边界

        [Range(0, 1)]
        [SerializeField]
        private Vector2 _tolerance; // 可见性容差（用于 SetVisible 的参数）

        private VisibilityTree _tree; // 当前相机所在的可见性树

        /// <summary>Whether the camera has a usable baked visibility tree.</summary>
        public bool HasVisibilityTree => _tree != null && _tree.Root != null;

        /// <summary>Current tolerance used when querying the visibility tree.</summary>
        public Vector2 Tolerance => _tolerance;

        /// <summary>
        /// 重置组件默认值
        /// </summary>
        private void Reset()
        {
            _tolerance = Vector2.one; // 默认容差为 1
        }

        /// <summary>
        /// 启动时查找最近的可见性树
        /// </summary>
        private void Start()
        {
            if (CameraZone.Instances.Count == 0) // 如果场景中没有 CameraZone
            {
                Debug.Log("StaticCullingCamera : Not found Camera Zones in scene");
                enabled = false; // 禁用组件
                return;
            }

            _tree = FindNearestVisibilityTree(); // 查找最近的可见性树

            if (_tree == null) // 如果找不到
            {
                Debug.Log("StaticCullingCamera : Can't find nearest CameraZone");
                enabled = false; // 禁用组件
                return;
            }
        }

        /// <summary>
        /// 每帧更新相机位置的可见性
        /// </summary>
        private void Update()
        {
            Vector3 point = transform.position;

            // 如果当前可见性树不存在或相机不在根节点包围盒内，则重新查找
            if (_tree == null || !_tree.Root.Bounds.Contains(point))
            {
                _tree = FindNearestVisibilityTree();

                if (_tree == null)
                    return;
            }
            
            // 更新当前相机位置的可见性
            _tree.SetVisible(point, _tolerance);
        }

        /// <summary>
        /// 查找相机当前位置所在的最近可见性树
        /// </summary>
        private VisibilityTree FindNearestVisibilityTree()
        {
            if (CameraZone.Instances.Count == 0)
                return null;

            Vector3 point = transform.position;

            foreach (var zone in CameraZone.Instances)
            {
                if (zone == null)
                    continue;

                VisibilityTree tree = zone.VisibilityTree;

                if (tree == null || tree.CullingTargets == null)
                    continue;

                if (tree.Root.Bounds.Contains(point))
                    return tree; // 找到包含相机位置的树
            }

            return null;
        }

#if UNITY_EDITOR

        public static bool DrawGizmo; // 编辑器中是否绘制 Gizmo

        /// <summary>
        /// 编辑器中绘制相机位置的 Gizmo
        /// </summary>
        private void OnDrawGizmos()
        {
            if (DrawGizmo)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(transform.position, Vector3.one); // 绘制单位立方体表示相机位置
            }
        }

        /// <summary>
        /// 编辑器中选中对象时绘制 Cell 边界
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!_drawCells || _tree == null)
                return;
            
            _tree.DrawCellsGizmo(transform.position, _tolerance); // 绘制 Cell 可见性边界
        }

#endif
    }
}
