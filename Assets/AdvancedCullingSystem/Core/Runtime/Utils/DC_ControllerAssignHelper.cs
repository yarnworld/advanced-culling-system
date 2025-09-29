using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    /// <summary>
    /// DC_Controller 分配源对象的辅助静态类
    /// 提供自动扫描 LODGroup 和 MeshRenderer 并注册到 DC_Controller 的功能
    /// </summary>
    public static class DC_ControllerAssignHelper
    {
        /// <summary>
        /// 自动为 DC_Controller 分配剔除源对象
        /// 会扫描 parent 下所有 LODGroup 和 MeshRenderer，并注册到 DC_Controller
        /// 默认剔除方法为 KeepShadows
        /// </summary>
        public static void AssignSources(this DC_Controller controller, GameObject parent = null, CullingMethod cullingMethod = CullingMethod.KeepShadows)
        {
            LODGroup[] groups = FindLODGroups(parent);
            MeshRenderer[] renderers = FindMeshRenderers(parent);

            ProcessLODGroups(controller, groups, cullingMethod, out HashSet<Renderer> lodRenderers);
            ProcessMeshRenderers(controller, renderers, lodRenderers, cullingMethod);
        }

        /// <summary>
        /// 快速版本的自动分配源对象，不做重复渲染器排除处理
        /// 适合场景初始化快速扫描
        /// </summary>
        public static void AssignSourcesFast(this DC_Controller controller, GameObject parent = null, CullingMethod cullingMethod = CullingMethod.KeepShadows)
        {
            LODGroup[] groups = FindLODGroups(parent);
            MeshRenderer[] renderers = FindMeshRenderers(parent);

            ProcessLODGroupsFast(controller, groups, cullingMethod);
            ProcessMeshRenderersFast(controller, renderers, cullingMethod);
        }

        /// <summary>
        /// 查找 parent 下所有 LODGroup
        /// parent 为 null 时查找全场景
        /// </summary>
        private static LODGroup[] FindLODGroups(GameObject parent)
        {
            if (parent == null)
                return UnityAPI.FindObjectsOfType<LODGroup>();
            else
                return parent.GetComponentsInChildren<LODGroup>();
        }

        /// <summary>
        /// 查找 parent 下所有 MeshRenderer
        /// parent 为 null 时查找全场景
        /// </summary>
        private static MeshRenderer[] FindMeshRenderers(GameObject parent)
        {
            if (parent == null)
                return UnityAPI.FindObjectsOfType<MeshRenderer>();
            else
                return parent.GetComponentsInChildren<MeshRenderer>();
        }

        /// <summary>
        /// 处理 LODGroup 注册到 DC_Controller，并收集其包含的 Renderer
        /// </summary>
        private static void ProcessLODGroups(DC_Controller controller, LODGroup[] groups, CullingMethod cullingMethod, out HashSet<Renderer> lodRenderers)
        {
            lodRenderers = new HashSet<Renderer>();

            foreach (var group in groups)
            {
                LOD[] lods = group.GetLODs();

                foreach (var lod in lods)
                {
                    foreach (var renderer in lod.renderers)
                        lodRenderers.Add(renderer); // 收集所有 LOD 下的 Renderer
                }

                if (!CheckLODGroup(group))
                    continue;

                controller.AddObjectForCulling(group, cullingMethod); // 添加 LODGroup 到 DC_Controller
            }
        }

        /// <summary>
        /// 快速处理 LODGroup 注册，不收集 Renderer
        /// </summary>
        private static void ProcessLODGroupsFast(DC_Controller controller, LODGroup[] groups, CullingMethod cullingMethod)
        {
            foreach (var group in groups)
            {
                if (!CheckLODGroup(group))
                    continue;

                controller.AddObjectForCulling(group, cullingMethod);
            }
        }

        /// <summary>
        /// 检查 LODGroup 是否有效，可用于剔除不需要处理的对象
        /// </summary>
        private static bool CheckLODGroup(LODGroup group)
        {
            if (!group.gameObject.activeInHierarchy)
                return false;

            if (group.GetComponent<DC_IgnoreByAssign>() != null)
                return false;

            if (group.GetComponent<DC_SourceSettings>() != null)
                return false;

            if (group.GetComponent<DC_Occluder>() != null)
                return false;

            return true;
        }

        /// <summary>
        /// 处理 MeshRenderer 注册，排除属于 LODGroup 的 Renderer
        /// </summary>
        private static void ProcessMeshRenderers(DC_Controller controller, MeshRenderer[] renderers, HashSet<Renderer> lodRenderers, CullingMethod cullingMethod)
        {
            foreach (var renderer in renderers)
            {
                if (!CheckMeshRenderer(renderer))
                    continue;

                if (lodRenderers.Contains(renderer))
                    continue; // 已经被 LODGroup 管理，跳过

                controller.AddObjectForCulling(renderer, cullingMethod);
            }
        }

        /// <summary>
        /// 快速处理 MeshRenderer 注册，不排除 LODGroup 内的 Renderer
        /// </summary>
        private static void ProcessMeshRenderersFast(DC_Controller controller, MeshRenderer[] renderers, CullingMethod cullingMethod)
        {
            foreach (var renderer in renderers)
            {
                if (!CheckMeshRenderer(renderer))
                    continue;

                if (renderer.GetComponentInParent<LODGroup>() != null)
                    continue; // 属于 LODGroup 的 Renderer 跳过

                controller.AddObjectForCulling(renderer, cullingMethod);
            }
        }

        /// <summary>
        /// 检查 MeshRenderer 是否有效，可用于排除不需要处理的对象
        /// </summary>
        private static bool CheckMeshRenderer(MeshRenderer renderer)
        {
            if (!renderer.gameObject.activeInHierarchy)
                return false;

            if (renderer.GetComponent<DC_IgnoreByAssign>() != null)
                return false;

            if (renderer.GetComponent<DC_SourceSettings>() != null)
                return false;

            if (renderer.GetComponent<DC_Occluder>() != null)
                return false;

            return true;
        }
    }
}
