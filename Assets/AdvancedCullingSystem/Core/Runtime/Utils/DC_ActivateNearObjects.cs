using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NGS.AdvancedCullingSystem.Dynamic;

namespace NGS.AdvancedCullingSystem.Utils
{
    /// <summary>
    /// 动态激活靠近玩家/指定位置的对象
    /// 配合 Dynamic Culling 系统使用，可在大场景中按需激活对象，提高运行时性能
    /// </summary>
    public class DC_ActivateNearObjects : MonoBehaviour
    {
        [SerializeField]
        private bool _drawGizmos = false; // 是否在编辑器中绘制可视化范围

        [Space]

        [Min(0.1f)]
        [SerializeField]
        private float _radius = 20f; // 激活检测半径

        [Min(1)]
        [SerializeField]
        private int _maxObjectsCount = 100; // 每次检测的最大对象数量，防止物理检测开销过大

        private IReadOnlyDictionary<Collider, IHitable> _hitablesDic; // 存储可激活对象的字典
        private int _layer; // 检测的 LayerMask
        private Collider[] _hits; // 缓存物理检测结果，避免每帧 GC

        /// <summary>
        /// 初始化逻辑
        /// 获取动态剔除系统管理的可激活对象字典，并初始化检测 Layer 和缓存数组
        /// </summary>
        private void Start()
        {
            _hitablesDic = DC_Controller.GetHitables();
            _layer = LayerMask.GetMask(DC_Controller.GetCullingLayerName());
            _hits = new Collider[_maxObjectsCount];
        }

        /// <summary>
        /// 每帧执行动态检测
        /// 查询指定半径内的 Collider，并调用 IHitable.OnHit() 激活对象
        /// </summary>
        private void LateUpdate()
        {
            // 高效查询范围内 Collider，不产生 GC
            int hitsCount = Physics.OverlapSphereNonAlloc(transform.position, _radius, _hits, _layer);

            for (int i = 0; i < hitsCount; i++)
            {
                Collider collider = _hits[i];

                // 如果 Collider 对应 IHitable 对象，则调用 OnHit 激活
                if (_hitablesDic.TryGetValue(collider, out IHitable hitable))
                    hitable.OnHit();
            }
        }

        /// <summary>
        /// 编辑器中绘制检测半径范围，用于调试
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!_drawGizmos)
                return;
            
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position, _radius);
        }
    }
}
