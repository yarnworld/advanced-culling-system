using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    /// <summary>
    /// 动态剔除源基类
    /// 负责管理剔除目标的生命周期、被射线命中事件以及超时逻辑
    /// 实现 IHitable 接口，供 DC_Camera 调用 OnHit()
    /// </summary>
    public abstract class DC_Source : MonoBehaviour, IHitable
    {
        /// <summary>
        /// 源对象的生命周期（秒）
        /// 当时间超过该值时，将触发 OnTimeout 并禁用自身
        /// </summary>
        public float Lifetime
        {
            get
            {
                return _lifetime;
            }
            set
            {
                _lifetime = Mathf.Max(0.01f, value); // 最小生命周期 0.01 秒
            }
        }

        // 内部存储的生命周期
        private float _lifetime;

        // 当前计时，用于判断是否超时
        private float _currentTime;

        /// <summary>
        /// Unity 每帧更新
        /// 累计时间并检测是否超时
        /// </summary>
        private void Update()
        {
            try
            {
                _currentTime += Time.deltaTime;

                if (_currentTime > _lifetime)
                {
                    // 生命周期到期触发处理
                    OnTimeout();
                    enabled = false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(ex.Message + ex.StackTrace);
                enabled = false;
            }
        }

        /// <summary>
        /// 当射线命中该源时调用
        /// DC_Camera 每帧射线检测命中时触发
        /// </summary>
        public void OnHit()
        {
            try
            {
                enabled = true;  // 激活组件
                _currentTime = 0; // 重置计时器
                OnHitInternal();  // 由子类实现具体处理逻辑
            }
            catch (Exception ex)
            {
                Debug.LogError(ex.Message + ex.StackTrace);
                enabled = false;
            }
        }

        /// <summary>
        /// 手动启用源对象
        /// 相当于超时重置 + 激活
        /// </summary>
        public void Enable()
        {
            OnTimeout();  // 触发超时逻辑
            enabled = true;
        }

        /// <summary>
        /// 手动禁用源对象
        /// 相当于命中处理 + 禁用
        /// </summary>
        public void Disable()
        {
            OnHitInternal(); // 触发命中逻辑
            enabled = false;
        }

        /// <summary>
        /// 设置剔除目标
        /// 由子类实现，将 ICullingTarget 与当前源关联
        /// </summary>
        /// <param name="target">剔除目标</param>
        public abstract void SetCullingTarget(ICullingTarget target);

        /// <summary>
        /// 移除剔除目标
        /// </summary>
        /// <param name="target">剔除目标</param>
        public abstract void RemoveCullingTarget(ICullingTarget target);

        /// <summary>
        /// 内部处理命中逻辑，由子类实现
        /// </summary>
        protected abstract void OnHitInternal();

        /// <summary>
        /// 内部处理超时逻辑，由子类实现
        /// </summary>
        protected abstract void OnTimeout();
    }
}
