using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    /// <summary>
    /// 剔除目标观察者
    /// 负责绑定单个 ICullingTarget 与 DC_Source 的关系，并在对象销毁时自动移除
    /// </summary>
    public class DC_CullingTargetObserver : MonoBehaviour
    {
        // 所属的 DC_Source
        private DC_Source _source;

        // 被观察的剔除目标
        private ICullingTarget _target;

        /// <summary>
        /// 初始化观察者
        /// 将 DC_Source 和 ICullingTarget 关联起来
        /// </summary>
        /// <param name="source">所属的 DC_Source</param>
        /// <param name="target">要观察的剔除目标</param>
        public void Initialize(DC_Source source, ICullingTarget target)
        {
            _source = source;
            _target = target;
        }

        /// <summary>
        /// 当 GameObject 被销毁时自动移除剔除目标
        /// 避免残留引用导致内存泄漏或错误剔除
        /// </summary>
        private void OnDestroy()
        {
            CullingDiagnostics.UnregisterTarget(gameObject.GetInstanceID());

            // 如果场景未加载完成，直接返回
            if (!gameObject.scene.isLoaded)
                return;

            // 从 DC_Source 中移除当前剔除目标
            _source?.RemoveCullingTarget(_target);
        }
    }
}
