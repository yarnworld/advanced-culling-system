using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using NGS.AdvancedCullingSystem.Static;

namespace NGS.AdvancedCullingSystem.Tutorial
{
    public class DisabledTargetGizmo : MonoBehaviour
    {
        private CullingTarget _target;

        private void Awake()
        {
            _target = GetComponent<CullingTarget>();

            if (_target == null)
                enabled = false;
        }

        private void OnDrawGizmos()
        {
            if (_target == null)
                return;

            if (_target.enabled)
                return;

            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(_target.Bounds.center, _target.Bounds.size);
        }
    }
}
