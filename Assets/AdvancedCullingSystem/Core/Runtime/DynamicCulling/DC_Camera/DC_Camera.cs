using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    // 该组件必须挂在 Camera 上
    [RequireComponent(typeof(Camera))]
    public partial class DC_Camera : MonoBehaviour
    {
        // 每帧用于可见性检测的射线数量
        [SerializeField]
        private int _raysCount = 1500;

        // 射线在视锥内的分布方式（例如均匀、R2 分布等）
        [SerializeField]
        private DistributionMethod _raysDistribution = DistributionMethod.R2;

        [Space]

        // 在原相机 FOV 基础上额外扩展的角度，用于减少边缘漏判
        [Range(0, 90)]
        [SerializeField]
        private int _fovAddition = 5;

        // 是否自动检测相机参数变化（分辨率 / FOV / 远裁剪面）
        [SerializeField]
        private bool _autoCheckChanges = false;

#if UNITY_EDITOR

        [Space]
        // 编辑器下是否绘制调试射线
        [SerializeField]
        private bool _DEBUG_RAYS = false;

#endif

        // Collider -> IHitable 的映射表
        // 用于在射线命中后，快速找到对应的“可命中对象”
        private IReadOnlyDictionary<Collider, IHitable> _hitablesDic;

        // Unity Camera 组件引用
        private Camera _camera;

        // 缓存的相机参数（宽高 / FOV / farPlane）
        private DC_CameraSettings _settings;

        // 是否需要更新相机设置
        private bool _updateSettings;

        // 是否需要更新射线数量
        private bool _updateRaysCount;

        // 即将应用的新射线数量
        private int _newRaysCount;

        // 单位球面上的射线方向数组（相机空间）
        private Vector3[] _rayDirs;

        // RaycastCommand 数组（用于 Job 批量射线检测）
        private NativeArray<RaycastCommand> _rayCommands;

        // Raycast 结果数组
        private NativeArray<RaycastHit> _rayHits;

        // 当前射线批次的 Job 句柄
        private JobHandle _jobHandle;

        // 剔除系统使用的物理层 Mask
        private int _layerMask;

        // 当前射线索引（循环使用射线方向）
        private int _currentRay;

        // 相机是否处于启用状态
        private bool _cameraEnabled;

        /// <summary>Number of raycasts that hit a culling collider in the last completed batch.</summary>
        public int LastRayHitCount { get; private set; }

        /// <summary>Number of raycasts processed in the last completed batch.</summary>
        public int LastRaycastCount => _raysCount;

        /// <summary>Whether this culling camera was active during the last update.</summary>
        public bool IsCullingActive => _cameraEnabled;

        /// <summary>Current ray hit ratio, useful for diagnostics and tuning.</summary>
        public float LastRayHitRatio => _raysCount <= 0 ? 0f : (float)LastRayHitCount / _raysCount;


        private void Awake()
        {
            // 缓存 Camera 组件
            _camera = GetComponent<Camera>();

            // 初始化射线数量
            _newRaysCount = _raysCount;
        }

        private void Start()
        {
            // 从 DC_Controller 获取所有可被命中的对象
            _hitablesDic = DC_Controller.GetHitables();

            // 获取动态剔除使用的 LayerMask
            _layerMask = LayerMask.GetMask(DC_Controller.GetCullingLayerName());

            // 启动时强制更新一次配置
            _updateRaysCount = true;
            _updateSettings = true;
        }

        private void Update()
        {
            // 相机必须启用，且 GameObject 处于激活状态
            _cameraEnabled = _camera.enabled && gameObject.activeInHierarchy;

            if (!_cameraEnabled)
                return;

            // 根据标志位更新射线数量 / 相机参数
            UpdateIfNeeded();

            int totalCount = _rayDirs.Length;
            float distance = _settings.farPlane;

            // 射线起点：相机世界坐标
            Vector3 origin = _camera.transform.position;

            // 相机本地到世界的矩阵，用于将方向转换到世界空间
            Matrix4x4 matrix = _camera.transform.localToWorldMatrix;

            // 每帧发射 _raysCount 条射线
            for (int i = 0; i < _raysCount; i++)
            {            
                _rayCommands[i] = UnityAPI.NewRaycastCommand(
                    origin,
                    matrix.MultiplyVector(_rayDirs[_currentRay]), // 将射线方向从相机空间转换到世界空间
                    distance,
                    _layerMask
                );

                // 循环使用射线方向数组
                _currentRay++;

                if (_currentRay >= totalCount)
                    _currentRay = 0;
            }

            // 使用 Job 系统批量调度 Raycast
            _jobHandle = RaycastCommand.ScheduleBatch(_rayCommands, _rayHits, 1);
        }

        private void LateUpdate()
        {
            if (!_cameraEnabled)
                return;

            // 等待 Raycast Job 执行完成
            _jobHandle.Complete();

            #region DEBUG_RAYS_REGION
#if UNITY_EDITOR

            // 编辑器下调试射线
            if (_DEBUG_RAYS)
            {
                for (int i = 0; i < _raysCount; i++)
                {
                    Collider collider = _rayHits[i].collider;

                    if (collider != null)
                    {
                        // 命中后通知对应的 IHitable
                        if (_hitablesDic.TryGetValue(collider, out IHitable hitable))
                            hitable.OnHit();
                    }

                    // 绘制调试射线
                    Debug.DrawLine(_rayCommands[i].from, _rayHits[i].point, Color.green);
                }

                return;
            }

#endif
            #endregion

            int hitCount = 0;

            // 正常模式下，仅处理命中逻辑（不绘制射线）
            for (int i = 0; i < _raysCount; i++)
            {
                Collider collider = _rayHits[i].collider;

                if (collider != null)
                {
                    hitCount++;
                    if (_hitablesDic.TryGetValue(collider, out IHitable hitable))
                        hitable.OnHit();
                }
            }

            LastRayHitCount = hitCount;
        }

        private void OnDestroy()
        {
            // 释放 NativeArray，避免内存泄漏
            if (_rayCommands.IsCreated)
                _rayCommands.Dispose();

            if (_rayHits.IsCreated)
                _rayHits.Dispose();
        }


        // 外部调用：标记相机参数发生变化
        public void CameraSettingsChanged()
        {
            _updateSettings = true;
        }

        // 外部调用：设置新的射线数量
        public void SetRaysCount(int count)
        {
            _updateRaysCount = true;
            _newRaysCount = count;
        }


        // 检查相机参数是否发生变化
        private bool IsCameraSettingsChanged()
        {
            if (_settings.width != _camera.pixelWidth)
                return true;

            if (_settings.height != _camera.pixelHeight)
                return true;

            if (_settings.fov != _camera.fieldOfView)
                return true;

            if (_settings.farPlane != _camera.farClipPlane)
                return true;

            return false;
        }

        // 根据标志位决定是否更新配置
        private void UpdateIfNeeded()
        {
            // 自动检测相机参数变化
            if (!_updateSettings && _autoCheckChanges)
            {
                if (IsCameraSettingsChanged())
                    _updateSettings = true;
            }

            // 更新相机设置
            if (_updateSettings)
            {
                UpdateCameraSettings();
                _updateSettings = false;
            }

            // 更新射线数量
            if (_updateRaysCount)
            {
                UpdateRaysCount(_newRaysCount);
                _updateRaysCount = false;
            }
        }

        // 重新分配射线相关的 NativeArray
        private void UpdateRaysCount(int count)
        {
            if (_rayCommands.IsCreated)
                _rayCommands.Dispose();

            if (_rayHits.IsCreated)
                _rayHits.Dispose();

            _rayCommands = new NativeArray<RaycastCommand>(count, Allocator.Persistent);
            _rayHits = new NativeArray<RaycastHit>(count, Allocator.Persistent);

            _raysCount = _newRaysCount;
        }

        // 更新相机相关参数 & 射线方向
        private void UpdateCameraSettings()
        {
            // 根据相机参数生成射线方向（相机空间）
            _rayDirs = DC_CameraUtil.GetRaysDirections(
                _camera,
                _raysDistribution,
                _fovAddition
            );

            // 缓存当前相机参数
            _settings = new DC_CameraSettings(_camera);
        }
    }
}
