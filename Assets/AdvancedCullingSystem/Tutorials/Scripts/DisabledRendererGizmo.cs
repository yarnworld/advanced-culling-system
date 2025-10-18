using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Tutorial
{
    public class DisabledRendererGizmo : MonoBehaviour
    {
        private Renderer _target;

        private void Awake()
        {
            _target = GetComponent<Renderer>();

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
            Gizmos.DrawWireCube(_target.bounds.center, _target.bounds.size);
        }
    }
}
