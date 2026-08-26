using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem
{
    /// <summary>
    /// 裁剪系统运行时诊断数据。除射线数据外，还记录真实帧时间、目标可见状态和显隐切换次数。
    /// 所有数据只用于诊断，不参与裁剪决策。
    /// </summary>
    public static class CullingDiagnostics
    {
        public const int HistoryLength = 300;

        private static readonly FrameSample[] _history = new FrameSample[HistoryLength];
        private static readonly Dictionary<int, bool> _targetStates = new Dictionary<int, bool>();
        private static readonly FrameTiming[] _frameTimings = new FrameTiming[1];

        private static int _historyIndex = -1;
        private static int _currentFrame = -1;
        private static int _cameraCount;
        private static int _raycastCount;
        private static int _hitCount;
        private static int _visibleTargetCount;
        private static int _visibilityChangeCount;
        private static float _raycastMilliseconds;
        private static float _frameMilliseconds;
        private static float _cpuFrameMilliseconds;
        private static float _gpuFrameMilliseconds;

        /// <summary>最近一帧的聚合数据。</summary>
        public static FrameSample Current { get; private set; }

        /// <summary>已经完成的历史采样数量。</summary>
        public static int SampleCount { get; private set; }

        /// <summary>是否已经收到至少一帧有效运行时采样。</summary>
        public static bool HasSamples => SampleCount > 0 || _currentFrame >= 0;

        /// <summary>按时间顺序读取历史采样。</summary>
        public static FrameSample GetHistory(int index)
        {
            if (index < 0 || index >= SampleCount)
                return default;

            int first = SampleCount == HistoryLength ? (_historyIndex + 1) % HistoryLength : 0;
            return _history[(first + index) % HistoryLength];
        }

        /// <summary>记录一个动态裁剪相机在本帧的射线数据。</summary>
        public static void RecordDynamicCamera(int frame, int raycasts, int hits, float milliseconds)
        {
            EnsureFrame(frame);
            _cameraCount++;
            _raycastCount += raycasts;
            _hitCount += hits;
            _raycastMilliseconds += milliseconds;
            Current = BuildSample(frame);
        }

        /// <summary>
        /// 报告一个裁剪目标的真实状态。重复报告相同状态不会增加切换次数。
        /// targetId 应使用目标 GameObject 的 InstanceID。
        /// </summary>
        public static void ReportTargetState(int targetId, bool visible)
        {
            EnsureFrame(Time.frameCount);

            if (_targetStates.TryGetValue(targetId, out bool previous))
            {
                if (previous != visible)
                {
                    _targetStates[targetId] = visible;
                    _visibleTargetCount += visible ? 1 : -1;
                    _visibilityChangeCount++;
                }
            }
            else
            {
                _targetStates.Add(targetId, visible);
                if (visible)
                    _visibleTargetCount++;
            }

            Current = BuildSample(Time.frameCount);
        }

        /// <summary>移除已经销毁或退出当前场景的目标，防止诊断计数残留。</summary>
        public static void UnregisterTarget(int targetId)
        {
            if (!_targetStates.TryGetValue(targetId, out bool visible))
                return;

            _targetStates.Remove(targetId);
            if (visible)
                _visibleTargetCount--;

            EnsureFrame(Time.frameCount);
            Current = BuildSample(Time.frameCount);
        }

        /// <summary>清空历史曲线，但保留已经注册的目标状态。</summary>
        public static void Clear()
        {
            Array.Clear(_history, 0, _history.Length);
            _historyIndex = -1;
            _currentFrame = -1;
            SampleCount = 0;
            Current = default;
        }

        /// <summary>计算当前历史窗口的统计摘要，包括平均值与 P95 帧时间。</summary>
        public static Summary GetSummary()
        {
            int count = SampleCount;
            if (count <= 0)
                return default;

            float frameTotal = 0f;
            float cpuTotal = 0f;
            float gpuTotal = 0f;
            float raycastTimeTotal = 0f;
            float hitRatioTotal = 0f;
            float cullRatioTotal = 0f;
            float[] frameTimes = new float[count];

            for (int i = 0; i < count; i++)
            {
                FrameSample sample = GetHistory(i);
                frameTimes[i] = sample.FrameMilliseconds;
                frameTotal += sample.FrameMilliseconds;
                cpuTotal += sample.CpuFrameMilliseconds;
                gpuTotal += sample.GpuFrameMilliseconds;
                raycastTimeTotal += sample.RaycastMilliseconds;
                hitRatioTotal += sample.HitRatio;
                cullRatioTotal += sample.CullRatio;
            }

            Array.Sort(frameTimes);
            int p95Index = Mathf.Clamp(Mathf.CeilToInt(count * 0.95f) - 1, 0, count - 1);
            FrameSample latest = GetHistory(count - 1);
            return new Summary(
                count,
                frameTotal / count,
                frameTimes[p95Index],
                cpuTotal / count,
                gpuTotal / count,
                raycastTimeTotal / count,
                hitRatioTotal / count,
                cullRatioTotal / count,
                latest.TargetCount,
                latest.VisibleTargetCount,
                latest.CulledTargetCount
            );
        }

        private static void EnsureFrame(int frame)
        {
            if (_currentFrame == frame)
                return;

            if (_currentFrame >= 0)
                Push(BuildSample(_currentFrame));

            _currentFrame = frame;
            _cameraCount = 0;
            _raycastCount = 0;
            _hitCount = 0;
            _visibilityChangeCount = 0;
            _raycastMilliseconds = 0f;
            _frameMilliseconds = 0f;
            _cpuFrameMilliseconds = 0f;
            _gpuFrameMilliseconds = 0f;
        }

        private static void RecordFrameTiming(int frame, float frameMilliseconds)
        {
            EnsureFrame(frame);
            _frameMilliseconds = frameMilliseconds;

            FrameTimingManager.CaptureFrameTimings();
            uint count = FrameTimingManager.GetLatestTimings(1, _frameTimings);
            if (count > 0)
            {
                _cpuFrameMilliseconds = (float)_frameTimings[0].cpuFrameTime;
                _gpuFrameMilliseconds = (float)_frameTimings[0].gpuFrameTime;
            }

            Current = BuildSample(frame);
        }

        private static FrameSample BuildSample(int frame)
        {
            int targetCount = _targetStates.Count;
            return new FrameSample(
                frame,
                _cameraCount,
                _raycastCount,
                _hitCount,
                _raycastMilliseconds,
                _frameMilliseconds,
                _cpuFrameMilliseconds,
                _gpuFrameMilliseconds,
                targetCount,
                _visibleTargetCount,
                targetCount - _visibleTargetCount,
                _visibilityChangeCount
            );
        }

        private static void Push(FrameSample sample)
        {
            _historyIndex = (_historyIndex + 1) % HistoryLength;
            _history[_historyIndex] = sample;
            SampleCount = Mathf.Min(SampleCount + 1, HistoryLength);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnDomainReload()
        {
            _targetStates.Clear();
            _visibleTargetCount = 0;
            Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateDriver()
        {
            GameObject driver = new GameObject("[ACS Diagnostics]");
            driver.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(driver);
            driver.AddComponent<CullingDiagnosticsDriver>();
        }

        [DefaultExecutionOrder(32000)]
        private sealed class CullingDiagnosticsDriver : MonoBehaviour
        {
            private void LateUpdate()
            {
                RecordFrameTiming(Time.frameCount, Time.unscaledDeltaTime * 1000f);
            }
        }

        /// <summary>一帧动态裁剪诊断采样。</summary>
        public readonly struct FrameSample
        {
            public readonly int Frame;
            public readonly int CameraCount;
            public readonly int RaycastCount;
            public readonly int HitCount;
            public readonly float RaycastMilliseconds;
            public readonly float FrameMilliseconds;
            public readonly float CpuFrameMilliseconds;
            public readonly float GpuFrameMilliseconds;
            public readonly int TargetCount;
            public readonly int VisibleTargetCount;
            public readonly int CulledTargetCount;
            public readonly int VisibilityChangeCount;

            public float HitRatio => RaycastCount <= 0 ? 0f : (float)HitCount / RaycastCount;
            public float CullRatio => TargetCount <= 0 ? 0f : (float)CulledTargetCount / TargetCount;

            public FrameSample(int frame, int cameraCount, int raycastCount, int hitCount,
                float raycastMilliseconds, float frameMilliseconds, float cpuFrameMilliseconds,
                float gpuFrameMilliseconds, int targetCount, int visibleTargetCount,
                int culledTargetCount, int visibilityChangeCount)
            {
                Frame = frame;
                CameraCount = cameraCount;
                RaycastCount = raycastCount;
                HitCount = hitCount;
                RaycastMilliseconds = raycastMilliseconds;
                FrameMilliseconds = frameMilliseconds;
                CpuFrameMilliseconds = cpuFrameMilliseconds;
                GpuFrameMilliseconds = gpuFrameMilliseconds;
                TargetCount = targetCount;
                VisibleTargetCount = visibleTargetCount;
                CulledTargetCount = culledTargetCount;
                VisibilityChangeCount = visibilityChangeCount;
            }
        }

        /// <summary>历史窗口统计摘要。</summary>
        public readonly struct Summary
        {
            public readonly int SampleCount;
            public readonly float AverageFrameMilliseconds;
            public readonly float P95FrameMilliseconds;
            public readonly float AverageCpuFrameMilliseconds;
            public readonly float AverageGpuFrameMilliseconds;
            public readonly float AverageRaycastMilliseconds;
            public readonly float AverageHitRatio;
            public readonly float AverageCullRatio;
            public readonly int TargetCount;
            public readonly int VisibleTargetCount;
            public readonly int CulledTargetCount;

            public Summary(int sampleCount, float averageFrameMilliseconds, float p95FrameMilliseconds,
                float averageCpuFrameMilliseconds, float averageGpuFrameMilliseconds,
                float averageRaycastMilliseconds, float averageHitRatio, float averageCullRatio,
                int targetCount, int visibleTargetCount, int culledTargetCount)
            {
                SampleCount = sampleCount;
                AverageFrameMilliseconds = averageFrameMilliseconds;
                P95FrameMilliseconds = p95FrameMilliseconds;
                AverageCpuFrameMilliseconds = averageCpuFrameMilliseconds;
                AverageGpuFrameMilliseconds = averageGpuFrameMilliseconds;
                AverageRaycastMilliseconds = averageRaycastMilliseconds;
                AverageHitRatio = averageHitRatio;
                AverageCullRatio = averageCullRatio;
                TargetCount = targetCount;
                VisibleTargetCount = visibleTargetCount;
                CulledTargetCount = culledTargetCount;
            }
        }
    }
}
