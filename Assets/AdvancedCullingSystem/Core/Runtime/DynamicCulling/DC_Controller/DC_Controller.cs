using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    /// <summary>
    /// 动态剔除控制器
    /// 负责管理场景中的剔除对象、摄像机以及射线检测的全局逻辑
    /// </summary>
    public class DC_Controller : MonoBehaviour
    {
        // 静态字典，用于根据 ControllerID 获取控制器实例
        private static Dictionary<int, DC_Controller> _controllersDic;

        // 静态字典，用于存储所有可被射线命中的物体（Collider → IHitable）
        private static Dictionary<Collider, IHitable> _hitablesDic;

        // ControllerID 属性，外部可读写
        public int ControllerID
        {
            get { return _controllerID; }
            set { _controllerID = value; }
        }

        // 剔除对象的生命周期，外部可读写，最小值为0.1秒
        public float ObjectsLifetime
        {
            get { return _objectsLifetime; }
            set { _objectsLifetime = Mathf.Max(0.1f, value); }
        }

        // 是否在组内合并对象，只能在初始化前设置
        public bool MergeInGroups
        {
            get { return _mergeInGroups; }
            set
            {
                if (_sourcesProvider != null)
                {
                    Debug.Log("You can set 'MergeInGroups' option only before initialized");
                    return;
                }
                _mergeInGroups = value;
            }
        }

        // 空间划分的单元格大小，只能在初始化前设置
        public float CellSize
        {
            get { return _cellSize; }
            set
            {
                if (_sourcesProvider != null)
                {
                    Debug.Log("You can set 'Cell Size' option only before initialized");
                    return;
                }
                _cellSize = Mathf.Max(value, 0.1f);
            }
        }

        // 是否绘制Gizmos调试图形
        public bool DrawGizmos { get; set; }

        [SerializeField]
        private int _controllerID; // 控制器ID

        [SerializeField, Min(0.1f)]
        private float _objectsLifetime = 2f; // 默认对象寿命

        [SerializeField]
        private bool _mergeInGroups = true; // 默认合并分组

        [SerializeField]
        private float _cellSize = 10f; // 默认网格单元格大小

        // 对象提供者接口，决定剔除对象的存储和组织方式
        private IDC_SourcesProvider _sourcesProvider;

        // 二叉树绘制器，用于Gizmos可视化
        private BinaryTreeDrawer _treeDrawer;

        /// <summary>
        /// 当域重新加载时（如热重载），重置静态字典
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ReloadDomain()
        {
            _controllersDic = null;
            _hitablesDic = null;
        }

        /// <summary>
        /// 初始化控制器实例
        /// </summary>
        private void Awake()
        {
            // 初始化控制器字典
            if (_controllersDic == null)
                _controllersDic = new Dictionary<int, DC_Controller>();

            // 初始化可命中物体字典
            if (_hitablesDic == null)
            {
                _hitablesDic = new Dictionary<Collider, IHitable>();
                // 场景卸载时清理空Collider
                SceneManager.sceneUnloaded += (s) => ClearEmptyHitables();
            }

            // 添加当前控制器到静态字典
            if (!_controllersDic.ContainsKey(_controllerID))
                _controllersDic.Add(_controllerID, this);
            else
                Debug.Log("DynamicCullingController with id : " + _controllerID + " already exists!");

            // 根据是否合并分组，选择不同的数据提供者
            if (_mergeInGroups)
            {
                _sourcesProvider = new DC_SourcesTree(_cellSize);
                _treeDrawer = new BinaryTreeDrawer();
            }
            else
            {
                _sourcesProvider = new DC_SingleSourcesProvider();
            }
        }

        /// <summary>
        /// 控制器销毁时，从静态字典移除
        /// </summary>
        private void OnDestroy()
        {
            _controllersDic.Remove(_controllerID);
        }

        /// <summary>
        /// 绘制Gizmos用于可视化二叉树结构
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!DrawGizmos)
                return;

            if (_treeDrawer == null)
                return;

            DC_SourcesTree tree = _sourcesProvider as DC_SourcesTree;
            if (tree.Root == null)
                return;

            _treeDrawer.Color = Color.white;
            _treeDrawer.DrawTreeGizmos(tree.Root);
        }

        /// <summary>
        /// 给指定相机添加动态剔除摄像机组件
        /// </summary>
        public DC_Camera AddCamera(Camera camera, int raysPerFrame)
        {
            if (camera.TryGetComponent(out DC_Camera cullingCamera))
            {
                Debug.Log(camera.name + " already has DynamicCullingCamera component");
            }
            else
            {
                // 动态添加 DC_Camera 组件
                cullingCamera = camera.gameObject.AddComponent<DC_Camera>();
                cullingCamera.SetRaysCount(raysPerFrame);
            }

            return cullingCamera;
        }

        /// <summary>
        /// 将单个 MeshRenderer 对象加入动态剔除
        /// </summary>
        public DC_SourceSettings AddObjectForCulling(MeshRenderer renderer, 
            CullingMethod cullingMethod = CullingMethod.FullDisable)
        {
            DC_SourceSettings settings = renderer.gameObject.AddComponent<DC_SourceSettings>();
            settings.ControllerID = _controllerID;
            settings.SourceType = SourceType.SingleMesh;
            settings.GetStrategy<DC_RendererSourceSettingsStrategy>().CullingMethod = cullingMethod;
            return settings;
        }

        /// <summary>
        /// 将 LODGroup 对象加入动态剔除
        /// </summary>
        public DC_SourceSettings AddObjectForCulling(LODGroup lodGroup, 
            CullingMethod cullingMethod = CullingMethod.FullDisable)
        {
            DC_SourceSettings settings = lodGroup.gameObject.AddComponent<DC_SourceSettings>();
            settings.ControllerID = _controllerID;
            settings.SourceType = SourceType.LODGroup;
            settings.GetStrategy<DC_LODGroupSourceSettingsStrategy>().CullingMethod = cullingMethod;
            return settings;
        }

        /// <summary>
        /// 将自定义 ICullingTarget 对象及其 Collider 集合加入动态剔除
        /// </summary>
        public void AddObjectForCulling(ICullingTarget cullingTarget, IEnumerable<Collider> colliders)
        {
            DC_Source source = _sourcesProvider.GetSource(cullingTarget);
            source.Lifetime = _objectsLifetime;
            source.transform.parent = transform;

            DC_CullingTargetObserver observer = cullingTarget.GameObject.AddComponent<DC_CullingTargetObserver>();
            observer.Initialize(source, cullingTarget);

            // 将 Collider 与源对象绑定到静态可命中字典
            foreach (var collider in colliders)
                _hitablesDic.Add(collider, source);
        }

        /// <summary>
        /// 根据 ControllerID 获取控制器实例
        /// </summary>
        public static DC_Controller GetById(int id)
        {
            return _controllersDic[id];
        }

        /// <summary>
        /// 获取动态剔除使用的 Layer
        /// </summary>
        public static int GetCullingLayer()
        {
            return LayerMask.NameToLayer(GetCullingLayerName());
        }

        /// <summary>
        /// 获取动态剔除使用的 Layer 名称
        /// </summary>
        public static string GetCullingLayerName()
        {
            return "ACSCulling";
        }

        /// <summary>
        /// 清理空的 Collider，防止字典中残留无效引用
        /// </summary>
        public static void ClearEmptyHitables()
        {
            List<Collider> keys = new List<Collider>(_hitablesDic.Count);
            List<IHitable> values = new List<IHitable>(_hitablesDic.Count);

            foreach (var keyValue in _hitablesDic)
            {
                if (keyValue.Key == null)
                    continue;

                keys.Add(keyValue.Key);
                values.Add(keyValue.Value);
            }

            _hitablesDic.Clear();

            for (int i = 0; i < keys.Count; i++)
                _hitablesDic.Add(keys[i], values[i]);
        }

        /// <summary>
        /// 获取所有可命中物体的只读字典
        /// </summary>
        public static IReadOnlyDictionary<Collider, IHitable> GetHitables()
        {
            return _hitablesDic;
        }
    }
}
