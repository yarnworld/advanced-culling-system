# Advanced Culling System 与 Unity/开源裁剪方案对比研究

> 调研日期：2026-08-26
>
> 对比基线：本仓库 `master`、Unity 2022.3 官方文档及各项目官方 GitHub README/源码。
>
> 证据规则：只采用一手来源；“未发现”表示在所列 README、文档和源码入口中没有找到可核对实现或结果，不等于作者从未在仓库外实现。

> 实施更新：本轮调研后已补充 300 帧帧时/CPU/GPU/渲染统计、真实目标裁剪率、JSON/CSV 导出和同几何 ACS Off/On 基准。本文保留尚未完成的正确性 Oracle、固定轨迹、多轮 Player Build 和 Unity 原生烘焙 A/B，避免把单轮 Editor 数据扩大为通用结论。

## 1. 结论摘要

Advanced Culling System（下称 ACS）不是 Unity 原生 Occlusion Culling 的等价替代品，而是“运行时物理射线采样 + 离线单元可见集”的混合系统：动态后端每帧从相机向视锥内发射有限数量的 `RaycastCommand`，命中对象后刷新可见生命周期；静态后端从 Camera Zone 的可见性单元中心向几何树节点和目标包围盒采样射线，将结果烘焙进 Visibility Tree。[动态相机源码](https://github.com/yarnworld/advanced-culling-system/blob/master/Assets/AdvancedCullingSystem/Core/Runtime/DynamicCulling/DC_Camera/DC_Camera.cs) [动态目标生命周期](https://github.com/yarnworld/advanced-culling-system/blob/master/Assets/AdvancedCullingSystem/Core/Runtime/DynamicCulling/DC_Source/Abstraction/DC_Source.cs) [静态烘焙源码](https://github.com/yarnworld/advanced-culling-system/blob/master/Assets/AdvancedCullingSystem/Core/Runtime/StaticCulling/StaticCullingBaker/VisibilityComputingProcess.cs)

相对 Unity 原生方案，ACS 的主要价值是运行时 Collider 变化可以参与动态射线遮挡，并能控制 Renderer、LODGroup、Light 与自定义回调，还提供保留阴影的目标策略；Unity 2022.3 原生方案依赖编辑器烘焙的 cell/PVS，动态对象可被静态对象遮挡但不能充当遮挡物。[Unity 2022.3 原理与限制](https://docs.unity3d.com/cn/2022.3/Manual/OcclusionCulling.html) [ACS 动态控制器](https://github.com/yarnworld/advanced-culling-system/blob/master/Assets/AdvancedCullingSystem/Core/Runtime/DynamicCulling/DC_Controller/DC_Controller.cs) [ACS 静态目标类型](https://github.com/yarnworld/advanced-culling-system/tree/master/Assets/AdvancedCullingSystem/Core/Runtime/StaticCulling/CullingTargets)

当前诊断已能输出帧时/P95、CPU/GPU Frame Timing、Batches、SetPass、Triangles/Vertices、可见/剔除目标和 JSON/CSV，并完成一组同几何 ACS Off/On 单轮 Editor 基准。仍不能据此声称 ACS 普遍快于 Unity 原生或其他项目，因为尚缺 Player Build 多轮测试、固定相机轨迹、Render Thread、内存以及误剔除 Oracle。[诊断聚合源码](https://github.com/yarnworld/advanced-culling-system/blob/master/Assets/AdvancedCullingSystem/Core/Runtime/Common/Diagnostics/CullingDiagnostics.cs) [可视化窗口源码](https://github.com/yarnworld/advanced-culling-system/blob/master/Assets/AdvancedCullingSystem/Core/Editor/Visualization/CullingVisualizationWindow.cs) [原始基准结果](../benchmarks/dynamic-base-2026-08-26.json)

与三个代表性开源项目相比，ACS 位于中间层：它比 Vision 更接近自有遮挡算法，比早期 GPU 包围盒原型更完整地覆盖编辑器烘焙和多种目标，但与 Unity Virtual Mesh 的 GPU 驱动、深度金字塔、两阶段遮挡、cluster LOD、间接绘制和页面流送仍有完整渲染后端的差距。[Vision](https://github.com/mackysoft/Vision) [Unity GPU Based Occlusion Culling](https://github.com/przemyslawzaworski/Unity-GPU-Based-Occlusion-Culling) [Unity Virtual Mesh](https://github.com/Unity-Technologies/com.unity.virtualmesh)

## 2. 研究对象

1. **mackysoft/Vision**：Unity `CullingGroup` 的组件化封装，代表“复用引擎可见性与距离带，只提供易用 API/编辑器”的轻量路线。[Vision README](https://github.com/mackysoft/Vision#readme)
2. **przemyslawzaworski/Unity-GPU-Based-Occlusion-Culling**：通过包围盒代理、early depth/stencil、UAV 与 CPU 回读控制 Renderer，代表早期“屏幕空间 GPU 查询 + GameObject 开关”路线。[项目 README](https://github.com/przemyslawzaworski/Unity-GPU-Based-Occlusion-Culling/blob/master/Readme.md)
3. **Unity-Technologies/com.unity.virtualmesh**：Unity 官方开源参考实现，包含虚拟几何、meshlet/cluster LOD、深度金字塔、两阶段 GPU 遮挡、间接绘制和页面流送；官方明确它是实验性参考代码而非生产就绪产品。[Virtual Mesh README](https://github.com/Unity-Technologies/com.unity.virtualmesh#readme) [实现文档](https://github.com/Unity-Technologies/com.unity.virtualmesh/blob/main/Documentation~/implementation.md)

## 3. 总体能力矩阵

符号：✅ 有明确实现或官方说明；△ 部分支持/依赖外部系统；❌ 官方明确不支持或源码未实现。

| 维度 | Unity 2022.3 原生 | ACS（本仓库） | Vision | GPU Based OC | Unity Virtual Mesh |
| --- | --- | --- | --- | --- | --- |
| 粒度 | Renderer、烘焙 cell/PVS | Renderer、LODGroup、Light、自定义目标；静态 cell + target | BoundingSphere + 回调 | 注册目标的包围盒代理，最终开关 Renderer | meshlet/cluster 与 memory page |
| 算法 | CPU 查询编辑器烘焙的 cell 可见性 | 动态：低差异视锥射线 + Physics；静态：Geometry Tree 分层射线 + Visibility Tree | CullingGroup 的视锥、静态遮挡与距离带 | early depth/stencil 可见像素写 UAV，CPU 回读 | GPU 深度金字塔、两阶段遮挡、层级 LOD、indirect draw/dispatch |
| 烘焙 | 必须烘焙静态遮挡数据 | 动态运行时计算；静态必须烘焙 | 自身不烘焙；静态遮挡依赖 Unity 数据 | 不需要 | 网格预处理为自定义页面与 AssetBundle |
| 动态对象作遮挡物 | ❌；只能作 occludee | △ Collider/Layer 配置正确时可影响射线 | ❌；CullingGroup 不考虑动态潜在遮挡物 | ✅ 声明支持静态/动态目标；变形 Bounds 待办 | ❌ 当前只支持静态不透明网格 |
| 任意逻辑回调 | ❌ 主要作用于 Renderer 绘制 | ✅ 自定义目标；静态还覆盖 Light | ✅ 可见性/距离状态回调 | △ 主要开关 Renderer | ❌ 目标是自定义渲染后端 |
| 多相机 | ✅ 每个 Camera 查询 | △ 每个 `DC_Camera` 独立发射；缺少明确的跨相机状态合并契约 | △ 每 Camera 一个 Group，自行合并 | ❌ README 明确默认结果不正确 | △ 主相机组件；未给通用多相机保证 |
| 阴影 | 引擎处理 | ✅ 完全禁用或 `ShadowsOnly` | △ 用户回调决定 | △ README 明确有消失阴影问题 | △ 方向光最多 4 cascades；附加灯无阴影 |
| 管线耦合 | 引擎内置 | 低，依赖 Physics、Renderer、Jobs | 低，依赖 CullingGroup | 高，仅验证 Unity 2018.4 DX11 | 很高，要求 Unity 6.3+、URP、Vulkan、Render Graph |
| 可视化 | cell、volume、visibility lines、portals | 命中率、相机标签、Zone 边界/烘焙状态、配置检查、可选射线 | Scene 中编辑距离/球体，运行时颜色 | Debug 包围盒和 GIF | Debug shader、overdraw/triangle view、Frame Debugger pass |
| 性能诊断 | 可视化并建议结合 Stats/Overdraw | 300 帧平均/P95、CPU/GPU、渲染统计、目标裁剪率、JSON/CSV；已有单轮同几何 A/B | README 无采样或基准结果 | README 无帧时/渲染统计 | Bake 处理统计和 Frame Debugger；无通用 A/B 表 |

矩阵来源：[Unity 遮挡剔除](https://docs.unity3d.com/cn/2022.3/Manual/OcclusionCulling.html) [Unity OC 窗口](https://docs.unity3d.com/cn/2022.3/Manual/occlusion-culling-window.html) [Unity CullingGroup](https://docs.unity3d.com/ja/2022.3/Manual/CullingGroupAPI.html) [ACS 动态源码](https://github.com/yarnworld/advanced-culling-system/blob/master/Assets/AdvancedCullingSystem/Core/Runtime/DynamicCulling/DC_Camera/DC_Camera.cs) [ACS 静态 Baker](https://github.com/yarnworld/advanced-culling-system/blob/master/Assets/AdvancedCullingSystem/Core/Runtime/StaticCulling/StaticCullingBaker/StaticCullingBaker.cs) [Vision README](https://github.com/mackysoft/Vision#readme) [GPU 原型 README](https://github.com/przemyslawzaworski/Unity-GPU-Based-Occlusion-Culling/blob/master/Readme.md) [Virtual Mesh README](https://github.com/Unity-Technologies/com.unity.virtualmesh#readme)

## 4. Unity 原生 Occlusion Culling

### 能力、算法与运行方式

Unity 2022.3 默认做视锥剔除；启用 Occlusion Culling 后，编辑器将场景划分为 cell，生成 cell 内几何体及相邻 cell 的可见性数据并尽可能合并 cell。运行时将数据载入内存，每个启用该功能的 Camera 在 CPU 上查询可见集。[Unity 官方原理](https://docs.unity3d.com/cn/2022.3/Manual/OcclusionCulling.html)

它适合房间—走廊等由实体几何分隔的小区域；查询本身有 CPU 成本，烘焙数据也占内存，因此最可能在过度绘制造成 GPU 瓶颈时获益，并非所有场景都会变快。[Unity 官方适用条件](https://docs.unity3d.com/cn/2022.3/Manual/OcclusionCulling.html#when-to-use-occlusion-culling)

静态 GameObject 可烘焙为 occluder/occludee；动态 GameObject 只能在运行时作为 occludee，不能作为 occluder。运行时生成的几何不能进入既有烘焙数据，这是 ACS 动态后端相对原生方案最明确的切入点。[Unity 动态对象说明](https://docs.unity3d.com/cn/2022.3/Manual/occlusion-culling-dynamic-gameobjects.html)

### 可视化与诊断

官方窗口有 Object、Bake、Visualization 页签，可显示 view volume、当前 cell/细分、visibility lines 和 portals；Smallest Occluder、Smallest Hole、Backface Threshold 直接体现精度、时间与数据大小的权衡。[Unity OC 窗口](https://docs.unity3d.com/cn/2022.3/Manual/occlusion-culling-window.html)

原生优势是“可见性结构可解释”：能看到 Camera 所在 cell、portal 连通和可见对象。ACS 当前只显示 Camera Zone 外框与是否有 Visibility Tree，没有显示当前节点、节点关系、该 cell 的可见目标集或目标被判定不可见的原因。[Unity 可视化](https://docs.unity3d.com/cn/2022.3/Manual/occlusion-culling-window.html#visualization-tab) [ACS 窗口](https://github.com/yarnworld/advanced-culling-system/blob/master/Assets/AdvancedCullingSystem/Core/Editor/Visualization/CullingVisualizationWindow.cs)

### 与 ACS 的可核对差异

- **原生更强**：成熟的 cell/PVS 查询、多 Camera 引擎集成、可解释的 cell/portal/visibility line，且无需每帧大量 Physics 射线。[Unity 原理](https://docs.unity3d.com/cn/2022.3/Manual/OcclusionCulling.html)
- **ACS 更灵活**：运行时 Collider 可改变遮挡关系，目标不局限于 Renderer，还能控制 Light 和自定义逻辑；可见生命周期可缓和一次采样未命中的抖动。[ACS 控制器](https://github.com/yarnworld/advanced-culling-system/blob/master/Assets/AdvancedCullingSystem/Core/Runtime/DynamicCulling/DC_Controller/DC_Controller.cs) [自定义目标](https://github.com/yarnworld/advanced-culling-system/blob/master/Assets/AdvancedCullingSystem/Core/Runtime/DynamicCulling/DC_CullingTarget/Custom/DC_CustomTarget.cs) [Light 目标](https://github.com/yarnworld/advanced-culling-system/blob/master/Assets/AdvancedCullingSystem/Core/Runtime/StaticCulling/CullingTargets/LightCullingTarget.cs)
- **ACS 风险更高**：有限射线不是对屏幕覆盖的保守解析判定。小物体、细缝、快速转向或采样不足时，可见目标可能未被任何射线命中；生命周期只延迟隐藏，不能证明无误剔除。这是依据采样与超时源码作出的推论。[射线采样](https://github.com/yarnworld/advanced-culling-system/blob/master/Assets/AdvancedCullingSystem/Core/Runtime/DynamicCulling/DC_Camera/DC_Camera.cs) [生命周期](https://github.com/yarnworld/advanced-culling-system/blob/master/Assets/AdvancedCullingSystem/Core/Runtime/DynamicCulling/DC_Source/Abstraction/DC_Source.cs)

## 5. 开源项目一：mackysoft/Vision

Vision 不实现新遮挡算法，而是把 `CullingGroup` 包装为组件、行为和事件；支持距离带、状态回调及无需编码即可启停 Renderer 的工具。[Vision README](https://github.com/mackysoft/Vision#readme)

底层 `CullingGroup` 只接受 BoundingSphere；一个 Group 只支持一个 Camera，多 Camera 需分别建组并合并；可见性基于视锥和静态遮挡，动态对象不作潜在遮挡物；结果在 Camera culling 时异步更新，修改场景后不能立即请求新状态。[Unity CullingGroup 官方说明](https://docs.unity3d.com/ja/2022.3/Manual/CullingGroupAPI.html)

Vision 可在 Scene View 调整距离阈值和包围球半径，Play Mode 用颜色表示可见性；不移动目标可设为 Static，避免每帧更新包围球 Transform。[Vision 使用说明](https://github.com/mackysoft/Vision#2-attach-the-cullingtargetbehaviour)

官方 README 没有给出 Profiler 指标、帧时、draw call、误判率或 A/B 结果。因此“High performance”只是项目自述，不能作为可复现性能结论。[Vision 特性](https://github.com/mackysoft/Vision#why-vision-)

与 ACS 相比：ACS 能用动态 Physics 场景参与遮挡并拥有自己的静态可见树；Vision 复用引擎 culling，避免 ACS 每帧自建大量射线和同帧等待 Job。Vision 的距离带则是 ACS 明显缺失的维度，可把二值可见性扩展为近、中、远质量等级，服务 AI、动画、音频和粒子。[ACS 动态相机](https://github.com/yarnworld/advanced-culling-system/blob/master/Assets/AdvancedCullingSystem/Core/Runtime/DynamicCulling/DC_Camera/DC_Camera.cs) [Vision 距离带](https://github.com/mackysoft/Vision#3-distances)

## 6. 开源项目二：Unity GPU Based Occlusion Culling

该项目为目标绘制特殊透明包围盒，以 `[earlydepthstencil]` 提前做深度测试；可见像素向 `RWStructuredBuffer` 写值，C# 通过 `ComputeBuffer.GetData` 回读并开关 MeshRenderer。它无需 Unity 原生静态烘焙，并面向静态/动态目标及 AssetBundle/StreamingAssets Prefab。[项目算法说明](https://github.com/przemyslawzaworski/Unity-GPU-Based-Occlusion-Culling/blob/master/Readme.md)

项目提供 Debug 包围盒和开关前后 GIF，但没有帧时、GPU 时间、draw call 或测试矩阵。README 明确指出默认多相机结果不正确、阴影会消失、变形物体 Bounds 更新待办，并仅验证 Unity 2018.4、DX11、Forward/Deferred，可能不兼容 LWRP/HDRP。[项目限制](https://github.com/przemyslawzaworski/Unity-GPU-Based-Occlusion-Culling/blob/master/Readme.md#to-do)

它的屏幕空间深度测试比 ACS 稀疏相机射线更接近“物体是否贡献可见像素”，但同步 `ComputeBuffer.GetData` 有 GPU→CPU 同步风险；ACS 不依赖管线和 GPU 回读，却把负载放在 Physics/CPU，并在 `LateUpdate` 同帧 `Complete()`。[GPU 原型](https://github.com/przemyslawzaworski/Unity-GPU-Based-Occlusion-Culling/blob/master/Readme.md) [ACS 同帧等待](https://github.com/yarnworld/advanced-culling-system/blob/master/Assets/AdvancedCullingSystem/Core/Runtime/DynamicCulling/DC_Camera/DC_Camera.cs)

ACS 已覆盖 Renderer、LODGroup、自定义目标与保留阴影，工程完整性更高；该原型的启示是把屏幕空间可见性做成独立 Provider。若做 GPU 后端，应让结果留在 GPU 驱动 BRG/Indirect，只异步回读聚合统计，避免每帧强制同步。[ACS 目标目录](https://github.com/yarnworld/advanced-culling-system/tree/master/Assets/AdvancedCullingSystem/Core/Runtime/DynamicCulling/DC_CullingTarget)

## 7. 开源项目三：Unity Virtual Mesh

Virtual Mesh 将静态网格离线拆为每组 64 三角形的 meshlet，递归合并、简化形成 cluster LOD，再序列化为页面；运行时通过 URP Renderer Feature、Compute Shader、GraphicsBuffer、indirect draw/dispatch、深度金字塔和两阶段 GPU 遮挡完成 LOD 与裁剪，页面请求通过 AsyncGPUReadback 交给 CPU Jobs 流式加载。[实现文档](https://github.com/Unity-Technologies/com.unity.virtualmesh/blob/main/Documentation~/implementation.md)

它与 ACS 的 GameObject/Renderer 粒度不同：ACS 最终切换 Renderer/Light/回调，Virtual Mesh 接管几何组织、LOD、流送、批处理和绘制，是渲染后端而不只是可见性判断器。[ACS Renderer 目标](https://github.com/yarnworld/advanced-culling-system/blob/master/Assets/AdvancedCullingSystem/Core/Runtime/DynamicCulling/DC_CullingTarget/Renderer/DC_RendererTarget.cs) [Virtual Mesh package](https://github.com/Unity-Technologies/com.unity.virtualmesh/blob/main/package.json)

项目提供 Debug shader，可透明叠加检查 overdraw，也可切到 triangle view 检查 LOD；README 要求用 Frame Debugger 确认阴影、深度金字塔等 Render Feature pass。[Virtual Mesh Troubleshooting](https://github.com/Unity-Technologies/com.unity.virtualmesh#troubleshooting)

限制也很严格：官方声明不生产就绪；面向 Unity 6.3+、URP、Vulkan、Render Graph，HDRP 未测试；只支持静态不透明网格，透明排序不保证；单场景烘焙、DX12、iOS和更多性能优化仍在开发。[项目状态](https://github.com/Unity-Technologies/com.unity.virtualmesh#project-status) [Setup/Baking](https://github.com/Unity-Technologies/com.unity.virtualmesh#setup)

ACS 相比它的核心差距是没有深度金字塔、当前/前帧历史、GPU 批量 AABB/cluster 测试、indirect draw 和 cluster LOD；可视化也只到相机命中率和 Zone 外框，不能显示真实 overdraw、三角形 LOD 或具体渲染 pass。[ACS 可视化](https://github.com/yarnworld/advanced-culling-system/blob/master/Assets/AdvancedCullingSystem/Core/Editor/Visualization/CullingVisualizationWindow.cs) [Virtual Mesh 实现](https://github.com/Unity-Technologies/com.unity.virtualmesh/blob/main/Documentation~/implementation.md)

ACS 仍有独立价值：它适用于 Unity 2022.3、非 URP 专属、GameObject 逻辑裁剪和动态 Physics 遮挡。合理结构是保留 CPU Ray/PVS 后端，再新增可选 URP Hi-Z 后端，而非整体改成 GPU-only。

## 8. 当前可视化与性能诊断的源码审计

### 当前实际输出

每个 `DC_Camera` 记录 Camera 数、配置射线数、Collider 命中数，以及从 `Update` 生成命令前到 `LateUpdate` 完成结果处理的墙钟区间；目标实现会在可见状态变化时向诊断器报告真实状态。隐藏的运行时驱动每帧采集 `unscaledDeltaTime` 与 `FrameTimingManager`，聚合器保存最近 300 帧并计算平均帧时、P95、CPU/GPU、命中率、裁剪率和状态切换数。[记录点](https://github.com/yarnworld/advanced-culling-system/blob/master/Assets/AdvancedCullingSystem/Core/Runtime/DynamicCulling/DC_Camera/DC_Camera.cs) [聚合器](https://github.com/yarnworld/advanced-culling-system/blob/master/Assets/AdvancedCullingSystem/Core/Runtime/Common/Diagnostics/CullingDiagnostics.cs)

窗口同时输出 Unity Rendering Statistics，并绘制命中率、裁剪率和帧时曲线；支持 JSON 摘要、CSV 历史导出和机器可读 Console 日志。编辑模式明确显示“未采样”，避免把缺失数据误读成零。[窗口源码](https://github.com/yarnworld/advanced-culling-system/blob/master/Assets/AdvancedCullingSystem/Core/Editor/Visualization/CullingVisualizationWindow.cs)

### 为什么这还不是具体性能结果

1. **“射线耗时”不是纯 Raycast Job 时间。** 起点在填充命令前，终点在 `LateUpdate` 的 `Complete()`、遍历命中并回调后；它混合命令构造、帧内间隔、调度/等待和结果处理，不能等同 Profiler 的 Physics 或 Worker 时间。[计时代码](https://github.com/yarnworld/advanced-culling-system/blob/master/Assets/AdvancedCullingSystem/Core/Runtime/DynamicCulling/DC_Camera/DC_Camera.cs)
2. **“命中率”不是剔除率。** 只要 `RaycastHit.collider != null` 就增加 `hitCount`，之后才检查 Collider 是否属于 `IHitable`；命中遮挡 Collider 也会提高命中率，却不代表目标被保留或 Renderer 状态改变。[命中代码](https://github.com/yarnworld/advanced-culling-system/blob/master/Assets/AdvancedCullingSystem/Core/Runtime/DynamicCulling/DC_Camera/DC_Camera.cs)
3. **基线还不完整。** 已有同几何 ACS Off/On、预热、300 帧、JSON/CSV 和环境元数据，但仍缺固定运动轨迹、重复轮次与 Player Build。[基准原始数据](../benchmarks/dynamic-base-2026-08-26.json)
4. **终端收益指标仍有缺口。** 已采集整体 CPU/GPU frame time、Batches、SetPass、Triangles/Vertices 和可见目标；仍缺独立 Main/Render Thread、GC/内存、阴影 caster 和分阶段 Profiler Marker。
5. **没有正确性指标。** 无 ACS Off 参考结果，也没有 false positive（多画）与 false negative（误剔除），所以不能证明快速转向、小目标、透明、反射、多相机和阴影场景安全。
6. **静态 Bake 不可审计。** Baker 有可取消进度和错误字符串，但未记录 cell/节点数、总射线数、耗时、峰值内存、资产大小、压缩率和输入失效原因。[StaticCullingBaker](https://github.com/yarnworld/advanced-culling-system/blob/master/Assets/AdvancedCullingSystem/Core/Runtime/StaticCulling/StaticCullingBaker/StaticCullingBaker.cs)

### 同步点风险

动态后端在 `Update` 调度 `RaycastCommand.ScheduleBatch`，同一帧 `LateUpdate` 无条件 `_jobHandle.Complete()`。Worker 有一段并行窗口，但任务未完成时主线程仍会等待；默认每 Camera 每帧 1500 条射线，多 Camera 基本按 Camera 数增加。[动态相机源码](https://github.com/yarnworld/advanced-culling-system/blob/master/Assets/AdvancedCullingSystem/Core/Runtime/DynamicCulling/DC_Camera/DC_Camera.cs)

## 9. 可核对差距清单

| 优先级 | 差距 | 当前证据 | 达标判据 |
| --- | --- | --- | --- |
| P0 | ACS Off/On 基准仅完成第一阶段 | 已有同几何、预热、300 帧、单轮 Editor JSON | 增加固定轨迹、Player Build、多轮 Median/P95、Render Thread 与内存 |
| P0 | 命中率语义仍不够细 | 已分离真实 `culledTargets` 与状态切换，但任意 Collider 命中仍合计 | 继续拆分 `occluderHits`、`targetHits`、`uniqueVisibleTargets` |
| P0 | 无误剔除验证 | 无参考可见集 | 同帧保守参考结果，记录 false negative；发布基准要求为 0 |
| P0 | 同帧 `Complete()` 可能卡主线程 | Update 调度、LateUpdate Complete | Profiler Marker 分段；实现 N/N+1 流水线后比较等待 P95 |
| P1 | 静态树不可解释 | 只画 Zone Bounds/烘焙状态 | 显示当前 cell、查询容差、可见/不可见目标颜色、射线路径和原因 |
| P1 | 缺少距离/质量层级 | 主要是二值状态 | 引入 distance band 与统一 VisibilityState，供 Renderer/LOD/AI/Audio 消费 |
| P1 | 多相机契约不清 | Camera 各自射线，目标共享生命周期 | “任一 Camera 可见即可见”的合并器、目标 camera bitmask、反射/小地图测试 |
| P1 | Bake 不可审计/增量 | 只有进度、错误和最终树 | Bake Report 记录输入哈希、参数、版本、耗时、射线数、节点/cell 数、大小 |
| P2 | 无屏幕空间/GPU 后端 | 仅 Physics + PVS | URP Hi-Z：深度金字塔、AABB 测试、异步统计、无同步 GPU 回读 |
| P2 | 无 cluster/indirect | Renderer.enabled 粒度 | 仅在 10k+ 对象基准证明收益后引入 BRG/Indirect，不替换通用后端 |

## 10. “具体结果”应如何输出

README 不应只放窗口截图，而应放同机、同分辨率、同轨迹的实测表。当前已经完成第一组同几何静止视角结果：ACS Off 平均/P95 为 `11.01/12.44 ms`，ACS Dynamic 为 `2.58/3.14 ms`，Batches 从 `6291` 降至 `297`，实际裁剪 `24854/24960` 个目标，射线批次墙钟区间平均 `0.341 ms`。[完整原始数据](../benchmarks/dynamic-base-2026-08-26.json)

这只是单轮 Editor 参考值；下表其余覆盖面尚未运行的项目继续明确标为“待测”，不填推测数字。

| 场景 | 模式 | Main Thread ms（Median/P95） | Render Thread ms | GPU ms | Batches | Triangles | Physics/Raycast ms | 可见/总目标 | False Negative |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 室内高遮挡 | Unity OC Off + ACS Off | 待测 | 待测 | 待测 | 待测 | 待测 | 0 | 待测 | 基线 |
| 室内高遮挡 | Unity OC On | 待测 | 待测 | 待测 | 待测 | 待测 | 0 | 待测 | 待测 |
| 室内高遮挡 | ACS Dynamic | 待测 | 待测 | 待测 | 待测 | 待测 | 待测 | 待测 | 待测 |
| 室内高遮挡 | ACS Static | 待测 | 待测 | 待测 | 待测 | 待测 | 0 | 待测 | 待测 |
| 开放低遮挡 | ACS Dynamic | 待测 | 待测 | 待测 | 待测 | 待测 | 待测 | 待测 | 待测 |
| 动态遮挡物 | Unity OC On / ACS Dynamic | 待测 | 待测 | 待测 | 待测 | 待测 | 待测 | 待测 | 待测 |

最少覆盖室内高遮挡、开放低遮挡、大量动态遮挡、快速相机运动四类场景，每类至少 1k/10k 目标。Unity 官方明确说明收益依赖场景，低遮挡时额外计算可能抵消收益，因此不能只发布一个最顺利的室内样例。[Unity 适用条件](https://docs.unity3d.com/cn/2022.3/Manual/OcclusionCulling.html#when-to-use-occlusion-culling)

## 11. 推荐实施顺序

### v0.2：让结果可信

1. 修正诊断语义：拆分命令生成、Job 等待、结果处理 Marker；分离 occluder hit、target hit、unique visible、culled、state change。
2. 新增 Benchmark Runner：固定轨迹、ACS/原生开关矩阵、预热、重复运行、CSV/JSON 与截图导出。
3. 新增正确性 Oracle：以 ACS Off 的可见 Renderer/屏幕采样作参考，先保证误剔除为零。
4. README 只引用自动生成结果，避免手工数字失真。

### v0.3：让可视化解释算法

1. 动态模式按目标命中、遮挡物命中、未命中、即将超时、已剔除着色，并支持单目标查看最近射线和 Camera 来源。
2. 静态模式显示当前 Visibility Tree cell、tolerance、节点层级、可见目标连线和 Bake 参数。
3. 时间轴同屏显示 Raycast/Wait/Apply、可见目标数、Batches、GPU ms，而非只画命中率。
4. Bake Report 持久化输入哈希和统计，可比较两次 Bake 的时间、大小和可见集变化。

### v0.4：扩展算法后端

1. 将动态 Job 改成跨帧流水线，以保守滞后和相机速度扩张 Bounds 控制误剔除。
2. 加入 CullingGroup/Distance Band Provider，服务 AI、动画、音频与远距离对象。
3. 为 URP 做可选 Hi-Z Provider；优先在 GPU 驱动 BRG/Indirect，只异步回读统计。
4. cluster/meshlet 做独立实验包，不与 Unity 2022.3 通用 GameObject 后端强耦合；Virtual Mesh 表明这需要完整 bake、streaming、shader 与 render feature 体系。[Virtual Mesh 实现](https://github.com/Unity-Technologies/com.unity.virtualmesh/blob/main/Documentation~/implementation.md)

## 12. 最终定位

ACS 最有竞争力的方向不是复刻 Unity 原生 Umbra，也不是立即复刻 Virtual Mesh，而是成为**可插拔、可解释、可验证的 GameObject 可见性框架**：原生后端负责成熟静态 Renderer 遮挡；ACS Static PVS 负责自定义目标和区域逻辑；ACS Dynamic Ray Provider 负责运行时 Collider；CullingGroup Provider 负责距离带；可选 URP Hi-Z 负责大规模 GPU 实例；统一 VisibilityState、性能采集和正确性 Oracle 负责合并并证明收益。

在完成 P0 基准前，本仓库能核对的结论是“已实现两种裁剪后端和基础遥测”，不能核对的结论是“它比 Unity 原生或开源方案更快”。这正是下一阶段最应该补齐的差距。
