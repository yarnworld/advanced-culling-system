using System;
using UnityEngine;

namespace NGS.AdvancedCullingSystem
{
    /// <summary>
    /// 裁剪系统运行时诊断数据。只保存最近一段时间的聚合结果，避免调试信息无限增长。
    /// </summary>
    public static class CullingDiagnostics
    {
        public const int HistoryLength = 120;

        private static readonly FrameSample[] _history = new FrameSample[HistoryLength];
        private static int _historyIndex = -1;
        private static int _currentFrame = -1;

        private static int _cameraCount;
        private static int _raycastCount;
        private static int _hitCount;
        private static float _raycastMilliseconds;

        /// <summary>最近一帧的聚合数据。</summary>
        public static FrameSample Current { get; private set; }

        /// <summary>历史采样数量。</summary>
        public static int SampleCount { get; private set; }

        /// <summary>按时间顺序读取历史采样。</summary>
        public static FrameSample GetHistory(int index)
        {
            if (index < 0 || index >= SampleCount)
                return default;

            int first = (SampleCount == HistoryLength) ? (_historyIndex + 1) % HistoryLength : 0;
            return _history[(first + index) % HistoryLength];
        }

        /// <summary>记录一个动态裁剪相机在本帧的诊断数据。</summary>
        public static void RecordDynamicCamera(int frame, int raycasts, int hits, float milliseconds)
        {
            EnsureFrame(frame);
            _cameraCount++;
            _raycastCount += raycasts;
            _hitCount += hits;
            _raycastMilliseconds += milliseconds;
            Current = BuildSample(frame);
        }

        /// <summary>清空诊断历史。</summary>
        public static void Clear()
        {
            Array.Clear(_history, 0, _history.Length);
            _historyIndex = -1;
            _currentFrame = -1;
            SampleCount = 0;
            Current = default;
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
            _raycastMilliseconds = 0f;
        }

        private static FrameSample BuildSample(int frame)
        {
            return new FrameSample(
                frame,
                _cameraCount,
                _raycastCount,
                _hitCount,
                _raycastMilliseconds
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
            Clear();
        }

        /// <summary>一帧动态裁剪诊断采样。</summary>
        public readonly struct FrameSample
        {
            public readonly int Frame;
            public readonly int CameraCount;
            public readonly int RaycastCount;
            public readonly int HitCount;
            public readonly float RaycastMilliseconds;

            public float HitRatio => RaycastCount <= 0 ? 0f : (float)HitCount / RaycastCount;

            public FrameSample(int frame, int cameraCount, int raycastCount, int hitCount, float raycastMilliseconds)
            {
                Frame = frame;
                CameraCount = cameraCount;
                RaycastCount = raycastCount;
                HitCount = hitCount;
                RaycastMilliseconds = raycastMilliseconds;
            }
        }
    }
}
