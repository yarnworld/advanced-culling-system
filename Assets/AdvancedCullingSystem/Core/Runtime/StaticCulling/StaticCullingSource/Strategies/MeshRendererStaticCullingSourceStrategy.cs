using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NGS.AdvancedCullingSystem.Static
{
    /// <summary>
    /// MeshRenderer 类型的静态剔除 Source 策略
    /// 
    /// 用于最常见的场景对象：
    /// - 普通 MeshRenderer + MeshFilter
    /// 
    /// 该策略既可以作为被剔除对象，
    /// 也可以在合适条件下作为遮挡体（Occluder）
    /// </summary>
    public class MeshRendererStaticCullingSourceStrategy : IStaticCullingSourceStrategy
    {
        /// <summary>
        /// Source 所属的上下文对象（宿主 GameObject）
        /// </summary>
        [SerializeField]
        private GameObject _context;

        /// <summary>
        /// 关联的 MeshRenderer 组件
        /// </summary>
        [SerializeField]
        private MeshRenderer _renderer;

        /// <summary>
        /// 关联的 MeshFilter 组件
        /// </summary>
        [SerializeField]
        private MeshFilter _filter;

        /// <summary>
        /// Baking 阶段动态创建的 MeshCollider
        /// 用于作为遮挡体参与剔除计算
        /// </summary>
        [SerializeField]
        private MeshCollider _collider;

        /// <summary>
        /// 剔除方式
        /// （例如整体剔除、按 Renderer 等）
        /// </summary>
        [SerializeField]
        private CullingMethod _cullingMethod;

        /// <summary>
        /// 是否作为遮挡体（Occluder）
        /// </summary>
        [SerializeField]
        private bool _isOccluder;


        /// <summary>
        /// 构造函数
        /// 
        /// 如果已存在 MeshRendererCullingTarget，
        /// 则复用其 IsOccluder 配置；
        /// 否则根据材质是否全部为透明材质
        /// 自动推断是否可作为遮挡体
        /// </summary>
        public MeshRendererStaticCullingSourceStrategy(GameObject context)
        {
            _context = context;

            MeshRendererCullingTarget target = context.GetComponent<MeshRendererCullingTarget>();

            if (target != null)
                _isOccluder = target.IsOccluder;
            else
                _isOccluder = !AllMaterialsIsTransarent(context);
        }

        /// <summary>
        /// 校验 MeshRenderer Source 是否合法
        /// </summary>
        public bool Validate(out string errorMessage)
        {
            _renderer = _context.GetComponent<MeshRenderer>();

            if (_renderer == null)
            {
                errorMessage = "MeshRenderer not found";
                return false;
            }

            _filter = _context.GetComponent<MeshFilter>();

            if (_filter == null)
            {
                errorMessage = "MeshFilter not found";
                return false;
            }

            if (_filter.sharedMesh == null)
            {
                errorMessage = "Mesh not found";
                return false;
            }

            errorMessage = "";
            return true;
        }

        /// <summary>
        /// 获取 MeshRenderer 在世界空间中的包围盒
        /// </summary>
        public bool TryGetBounds(out Bounds bounds)
        {
            // 确保 Renderer 已缓存
            if (_renderer == null)
            {
                if ((_renderer = _context.GetComponent<MeshRenderer>()) == null)
                {
                    bounds = default(Bounds);
                    return false;
                }
            }

            bounds = _renderer.bounds;
            return true;
        }

        /// <summary>
        /// 创建运行时使用的 MeshRendererCullingTarget
        /// </summary>
        public CullingTarget CreateCullingTarget()
        {
            var cullingTarget = _context.AddComponent<MeshRendererCullingTarget>();

            cullingTarget.Bounds = _renderer.bounds;
            cullingTarget.CullingMethod = _cullingMethod;
            cullingTarget.IsOccluder = _isOccluder;

            return cullingTarget;
        }

        /// <summary>
        /// Baking 前准备阶段
        /// 
        /// 如果该 MeshRenderer 可作为遮挡体，
        /// 则基于其 Mesh 创建临时 MeshCollider
        /// </summary>
        public void PrepareForBaking()
        {
            if (!_isOccluder)
                return;

            Mesh mesh = _filter.sharedMesh;

            GameObject colliderGo = new GameObject("SC_Collider");

            colliderGo.layer = StaticCullingPreferences.Layer;
            colliderGo.transform.parent = _context.transform;
            colliderGo.transform.localPosition = Vector3.zero;
            colliderGo.transform.localEulerAngles = Vector3.zero;
            colliderGo.transform.localScale = Vector3.one;

            _collider = colliderGo.AddComponent<MeshCollider>();
            _collider.sharedMesh = mesh;
        }

        /// <summary>
        /// Baking 完成后的清理阶段
        /// 
        /// 删除临时创建的 MeshCollider
        /// </summary>
        public void ClearAfterBaking()
        {
            if (_collider != null)
                UnityEngine.Object.DestroyImmediate(_collider.gameObject);
        }


        /// <summary>
        /// 判断当前 MeshRenderer 的所有材质
        /// 是否全部为透明材质
        /// </summary>
        private bool AllMaterialsIsTransarent(GameObject context)
        {
            MeshRenderer renderer = context.GetComponent<MeshRenderer>();

            if (renderer == null)
                return false;

            Material[] materials = renderer.sharedMaterials;

            if (materials == null || materials.Length == 0)
                return false;

            bool allTransparent = true;

            foreach (var material in materials)
            {
                if (material == null)
                    continue;

                // 透明物体通常不适合作为遮挡体
                if (material.renderQueue != (int)RenderQueue.Transparent)
                    allTransparent = false;
            }

            return allTransparent;
        }
    }
}
