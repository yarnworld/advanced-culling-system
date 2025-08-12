using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    /// <summary>
    /// 剔除方法枚举
    /// FullDisable：完全隐藏
    /// KeepShadows：隐藏物体，但保留阴影
    /// </summary>
    public enum CullingMethod { FullDisable, KeepShadows }

    /// <summary>
    /// Source 类型枚举
    /// SingleMesh：单个 MeshRenderer
    /// LODGroup：LODGroup 对象
    /// Custom：自定义 Target
    /// </summary>
    public enum SourceType { SingleMesh, LODGroup, Custom } 

    /// <summary>
    /// 动态剔除的 Source 配置组件
    /// 负责选择策略（Renderer/LODGroup/Custom），准备剔除数据并绑定到 DC_Controller
    /// </summary>
    [DisallowMultipleComponent]
    public class DC_SourceSettings : MonoBehaviour
    {
        /// <summary>
        /// 当前是否准备好进行剔除
        /// </summary>
        public bool ReadyForCulling
        {
            get
            {
                return _strategy != null && _strategy.ReadyForCulling;
            }
        }

        /// <summary>
        /// 剔除层
        /// </summary>
        public int CullingLayer
        {
            get
            {
                return DC_Controller.GetCullingLayer();
            }
        }

        /// <summary>
        /// Source 类型（SingleMesh / LODGroup / Custom）
        /// </summary>
        public SourceType SourceType
        {
            get
            {
                return _sourceType;
            }
            set
            {
                if (value == _sourceType)
                    return;

                _sourceType = value;

                OnSourceTypeChanged();
            }
        }
        
        [field: SerializeField]
        /// <summary>
        /// Controller ID
        /// </summary>
        public int ControllerID { get; set; }

        [field: SerializeField]
        /// <summary>
        /// 是否不兼容（例如缺少组件）
        /// </summary>
        public bool IsIncompatible { get; private set; }

        [field: SerializeField]
        /// <summary>
        /// 不兼容原因
        /// </summary>
        public string IncompatibilityReason { get; private set; }

        [SerializeField]
        private SourceType _sourceType;

        [SerializeReference]
        private IDC_SourceSettingsStrategy _strategy; // 当前策略（Renderer/LODGroup/Custom）


        /// <summary>
        /// 编辑器 Reset 时自动检测 Source 类型并检查兼容性
        /// </summary>
        private void Reset()
        {
            DetectSourceType();
            CheckCompatibility();
        }

        /// <summary>
        /// Awake 初始化策略
        /// </summary>
        private void Awake()
        {
            if (_strategy == null)
                CreateStrategy();
        }

        /// <summary>
        /// Start 时检查兼容性，准备剔除，并绑定到 DC_Controller
        /// </summary>
        private void Start()
        {
            try
            {
                if (!CheckCompatibility())
                {
                    enabled = false;
                    return;
                }

                if (!_strategy.ReadyForCulling)
                    _strategy.PrepareForCulling();

                // 添加对象到 DC_Controller
                DC_Controller.GetById(ControllerID).AddObjectForCulling(
                    _strategy.CreateCullingTarget(),
                    _strategy.GetColliders());

                // SourceSettings 完成初始化后销毁自身
                Destroy(this);
            }
            catch (Exception ex)
            {
                IsIncompatible = true;
                IncompatibilityReason = ex.Message + ex.StackTrace;
            }
        }

        /// <summary>
        /// 获取策略对象
        /// </summary>
        public T GetStrategy<T>() where T : IDC_SourceSettingsStrategy
        {
            return (T)_strategy;
        }

        /// <summary>
        /// 获取当前包围盒
        /// </summary>
        public bool TryGetBounds(ref Bounds bounds)
        {
            if (_strategy == null)
                return false;

            return _strategy.TryGetBounds(ref bounds);
        }

        /// <summary>
        /// 检查兼容性
        /// </summary>
        public bool CheckCompatibility()
        {
            if (_strategy == null)
                CreateStrategy();

            IsIncompatible = !_strategy.CheckCompatibilityAndGetComponents(out string reason);
            IncompatibilityReason = reason;

            return !IsIncompatible;
        }

        /// <summary>
        /// Editor 模式下 Bake 数据
        /// </summary>
        public void Bake()
        {
            if (Application.isPlaying)
            {
                Debug.Log("'Bake' can only be called in editor mode");
                return;
            }

            if (_strategy != null && _strategy.ReadyForCulling)
                _strategy.ClearData();

            if (CheckCompatibility())
                _strategy.PrepareForCulling();
        }

        /// <summary>
        /// Editor 模式下清理已 Bake 的数据
        /// </summary>
        public void ClearBakedData()
        {
            if (Application.isPlaying)
            {
                Debug.Log("'ClearBakedData' can only be called in editor mode");
                return;
            }

            _strategy?.ClearData();
        }

        /// <summary>
        /// 自动检测 Source 类型
        /// </summary>
        private void DetectSourceType()
        {
            if (GetComponent<LODGroup>() != null)
                SourceType = SourceType.LODGroup;

            else if (GetComponent<MeshRenderer>() != null)
                SourceType = SourceType.SingleMesh;

            else
                SourceType = SourceType.Custom;
        }

        /// <summary>
        /// SourceType 变更时清理原策略并重新创建策略
        /// </summary>
        private void OnSourceTypeChanged()
        {
            if (_strategy != null && _strategy.ReadyForCulling)
                _strategy.ClearData();

            CreateStrategy();
            CheckCompatibility();
        }

        /// <summary>
        /// 根据 SourceType 创建对应策略
        /// </summary>
        private void CreateStrategy()
        {
            if (SourceType == SourceType.SingleMesh)
            {
                _strategy = new DC_RendererSourceSettingsStrategy(this);
            }
            else if (SourceType == SourceType.LODGroup)
            {
                _strategy = new DC_LODGroupSourceSettingsStrategy(this);
            }
            else if (SourceType == SourceType.Custom)
            {
                _strategy = new DC_CustomSourceSettingsStrategy(this);
            }
            else
                throw new NotSupportedException();
        }
    }
}
