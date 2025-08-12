using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Static
{
    /// <summary>
    /// 静态剔除 Source 类型枚举
    /// 
    /// 用于标识当前 GameObject
    /// 应该使用哪一种 SourceStrategy
    /// </summary>
    public enum SourceType { MeshRenderer, LODGroup, Light, Custom }

    /// <summary>
    /// StaticCullingSource
    /// 
    /// 静态剔除系统中「Source 层」的统一入口组件。
    /// 
    /// 该组件本身不关心具体剔除逻辑，
    /// 只负责：
    /// - 自动识别对象类型
    /// - 创建对应的 SourceStrategy
    /// - 管理校验、Baking 生命周期
    /// - 提供 Gizmo 可视化
    /// </summary>
    [DisallowMultipleComponent]
    public class StaticCullingSource : MonoBehaviour
    {
        /// <summary>
        /// 当前 Source 的类型
        /// 
        /// 修改该值会自动重新创建 Strategy 并重新校验
        /// </summary>
        public SourceType SourceType
        {
            get
            {
                return _sourceType;
            }
            set
            {
                _sourceType = value;

                OnSourceTypeChanged();
            }
        }

        /// <summary>
        /// Source 校验失败时的错误信息
        /// </summary>
        public string ValidationError
        {
            get
            {
                return _validationError;
            }
        }

        /// <summary>
        /// Baking 阶段生成的 CullingTarget
        /// 
        /// 这是运行时真正参与剔除的对象
        /// </summary>
        public CullingTarget CullingTarget
        {
            get
            {
                return _target;
            }
        }

        /// <summary>
        /// 当前使用的 SourceStrategy
        /// </summary>
        public IStaticCullingSourceStrategy Strategy
        {
            get
            {
                return _strategy;
            }
        }

        [SerializeField]
        private SourceType _sourceType;

        [SerializeField]
        private string _validationError;

        [SerializeField]
        private CullingTarget _target;

        /// <summary>
        /// Source 使用的策略实例
        /// 
        /// 使用 SerializeReference
        /// 以支持接口序列化和多态
        /// </summary>
        [SerializeReference]
        private IStaticCullingSourceStrategy _strategy;


        /// <summary>
        /// Unity Reset 回调
        /// 
        /// 当组件第一次添加或 Reset 时：
        /// - 自动识别 SourceType
        /// - 创建对应 Strategy
        /// - 立即进行校验
        /// </summary>
        private void Reset()
        {
            AutoDetectSourceType();
            CreateStrategy();
            Validate();
        }

        /// <summary>
        /// 根据当前 GameObject 上的组件
        /// 自动推断 SourceType
        /// </summary>
        private void AutoDetectSourceType()
        {
            if (GetComponent<MeshRenderer>() != null)
            {
                _sourceType = SourceType.MeshRenderer;
                return;
            }

            if (GetComponent<LODGroup>() != null)
            {
                _sourceType = SourceType.LODGroup;
                return;
            }

            if (GetComponent<Light>() != null)
            {
                _sourceType = SourceType.Light;
                return;
            }

            // 如果以上都不匹配，则作为 Custom Source
            _sourceType = SourceType.Custom;
        }

        /// <summary>
        /// 当 SourceType 被修改时调用
        /// 
        /// 负责：
        /// - 清空校验错误
        /// - 重新创建 Strategy
        /// - 重新校验
        /// </summary>
        private void OnSourceTypeChanged()
        {
            _validationError = "";

            CreateStrategy();
            Validate();
        }

        /// <summary>
        /// 根据 SourceType 创建对应的 Strategy 实例
        /// </summary>
        private void CreateStrategy()
        {
            if (_sourceType == SourceType.MeshRenderer)
                _strategy = new MeshRendererStaticCullingSourceStrategy(gameObject);

            else if (_sourceType == SourceType.LODGroup)
                _strategy = new LODGroupStaticCullingSourceStrategy(gameObject);

            else if (_sourceType == SourceType.Light)
                _strategy = new LightStaticCullingSourceStrategy(gameObject);

            else if (_sourceType == SourceType.Custom)
                _strategy = new CustomStaticCullingSourceStrategy(gameObject);
        }


        /// <summary>
        /// 校验当前 Source 是否可用于静态剔除
        /// </summary>
        public bool Validate()
        {
            if (_strategy == null)
            {
                AutoDetectSourceType();
                CreateStrategy();
            }

            _validationError = "";

            return _strategy.Validate(out _validationError);
        }

        /// <summary>
        /// 尝试获取 Source 的世界空间包围盒
        /// </summary>
        public bool TryGetBounds(out Bounds bounds)
        {
            return _strategy.TryGetBounds(out bounds);
        }

        /// <summary>
        /// Baking 开始前调用
        /// 
        /// 负责：
        /// - 校验 Source
        /// - 创建 CullingTarget
        /// - 调用 Strategy 的 Baking 准备逻辑
        /// </summary>
        public void PrepareForBaking()
        {
            if (_validationError != "")
            {
                if (!Validate())
                    throw new Exception("StaticCullingSource::" + gameObject.name + " has validation errors");
            }

            _target = _strategy.CreateCullingTarget();
            
            _strategy.PrepareForBaking();
        }

        /// <summary>
        /// Baking 完成后调用
        /// 
        /// 用于清理 Strategy 在 Baking 阶段创建的临时数据
        /// </summary>
        public void ClearAfterBaking()
        {
            _strategy.ClearAfterBaking();
        }


#if UNITY_EDITOR

        /// <summary>
        /// 是否绘制不同 Source 类型的 Gizmo
        /// （编辑器调试用）
        /// </summary>
        public static bool DrawGizmoRenderers;
        public static bool DrawGizmoLODGroups;
        public static bool DrawGizmoLights;
        public static bool DrawGizmoCustom;

        /// <summary>
        /// Unity Gizmo 回调
        /// 
        /// 根据 SourceType 绘制不同颜色的 Bounds
        /// </summary>
        private void OnDrawGizmos()
        {
            if (_sourceType == SourceType.MeshRenderer && DrawGizmoRenderers)
                DrawGizmo(Color.blue);

            else if (_sourceType == SourceType.LODGroup && DrawGizmoLODGroups)
                DrawGizmo(Color.yellow);

            else if (_sourceType == SourceType.Light && DrawGizmoLights)
                DrawGizmo(Color.white);

            else if (_sourceType == SourceType.Custom && DrawGizmoCustom)
                DrawGizmo(Color.green);
        }

        /// <summary>
        /// 绘制 Source 的包围盒 Gizmo
        /// 
        /// 若校验失败，则使用红色提示
        /// </summary>
        private void DrawGizmo(Color color)
        {
            if (!TryGetBounds(out Bounds bounds))
                return;

            if (ValidationError != "")
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }
            else
            {
                Gizmos.color = color;
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }
        }
#endif
    }
}
