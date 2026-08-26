# Advanced Culling System

Unity 2022.3 项目，提供动态裁剪（Dynamic Culling）与静态裁剪（Static Culling）两套可组合的可见性优化方案，适合用于减少大型场景中的渲染和对象更新开销。

## 项目内容

- 动态裁剪：基于相机、遮挡物、Renderer、LODGroup 和自定义目标进行运行时裁剪。
- 静态裁剪：通过几何树、可见性树和 Baker 预计算场景可见性。
- Unity Editor 工具：提供 Source、Controller、Camera、Camera Zone 和 Chunk 管理器的 Inspector 与选择工具。
- 教程场景：包含动态裁剪、实例对象、自定义目标、静态裁剪、自定义目标和透明度场景示例。

## 环境要求

- Unity `2022.3.62f3c1`
- Windows（项目当前使用的开发环境）

首次用 Unity Hub 打开项目后，等待 Package Manager 和 Asset Database 完成导入即可。

## 目录结构

```text
Assets/AdvancedCullingSystem/
├── Core/Runtime      # 运行时裁剪逻辑
├── Core/Editor       # Unity 编辑器扩展和烘焙工具
└── Tutorials         # 教程脚本、材质和示例场景
```

## 教程场景

示例场景位于 `Assets/AdvancedCullingSystem/Tutorials/Scenes/`：

1. DynamicCulling Base
2. DynamicCulling Instanced Objects
3. DynamicCulling Custom Targets
4. StaticCulling Base
5. StaticCulling Custom Targets
6. StaticCulling Transparency

## 使用建议

动态裁剪适合对象和相机持续变化的场景；静态裁剪适合布局稳定、可以接受预烘焙时间的场景。正式项目中建议先在教程场景中验证裁剪边界、阴影和透明材质行为，再接入生产场景。

## 可视化调试

打开 Unity 菜单 `Tools/NGSTools/Advanced Culling System/Visualization`。进入 Play Mode 后，窗口会同时显示：

- 最近 300 帧的平均帧时与 P95、CPU/GPU Frame Timing；
- 射线数、命中率和射线批次墙钟耗时；
- 已注册目标、可见目标、已裁剪目标、真实裁剪率和状态切换数；
- Batches、SetPass、Triangles、Vertices；
- 命中率（绿色）、裁剪率（蓝色）、帧时（橙色）时间线。

窗口支持将摘要导出为 JSON、将逐帧历史导出为 CSV。编辑器菜单 `Tools/NGSTools/Advanced Culling System/Log Current Diagnostics` 会输出带 `[ACS-DIAGNOSTICS]` 前缀的机器可读 JSON，适合 Unity MCP、CI 或外部脚本采集。未进入 Play Mode 时窗口明确显示“未采样”，不会再把缺失数据画成 0。

需要特别注意：射线命中率不等于裁剪率。命中率描述采样射线碰到 Collider 的比例；裁剪率描述 ACS 实际隐藏的目标比例。判断收益应优先看裁剪率、帧时和 Rendering Statistics。

## 可复现实测结果

项目提供一组内容逐字节相同的 A/B 场景：

- `Scene.unity`：Unity 默认视锥裁剪，ACS 关闭；
- `Scene 1.unity`：相同几何，在加载后自动启用 ACS Dynamic，`1500 rays/frame + FullDisable`。

2026-08-26 在 Unity `2022.3.62f3c1` Editor Play Mode、Game View 静止相机下，预热后采集最近 300 帧。测试机为 Windows 11、Intel Core Ultra 7 265、RTX 5060、64 GB RAM。以下是同一编辑器会话中的单轮参考结果：

| 指标 | ACS 关闭 | ACS Dynamic | 变化 |
| --- | ---: | ---: | ---: |
| 平均帧时 | 11.01 ms | 2.58 ms | -76.5% |
| P95 帧时 | 12.44 ms | 3.14 ms | -74.7% |
| CPU Frame Timing | 11.01 ms | 2.58 ms | -76.5% |
| GPU Frame Timing | 5.99 ms | 0.26 ms | -95.6% |
| Batches | 6,291 | 297 | -95.3% |
| SetPass | 59 | 50 | -15.3% |
| Triangles | 78,268 | 6,340 | -91.9% |
| Vertices | 156,542 | 12,686 | -91.9% |
| 射线批次墙钟耗时 | 0 | 0.341 ms | +0.341 ms |
| 目标（可见 / 总数） | 未注册 | 106 / 24,960 | 裁剪 24,854（99.58%） |

原始机器可读结果见 [`docs/benchmarks/dynamic-base-2026-08-26.json`](docs/benchmarks/dynamic-base-2026-08-26.json)。这组数据证明该高遮挡静止视角下“减少渲染工作量的收益大于射线成本”，但它不是跨机器结论，也没有覆盖移动相机、误剔除、开放场景或 Player Build；正式性能结论仍应使用 Profiler、固定轨迹和多轮 Median/P95。

## 与 Unity 原生裁剪的区别

| 维度 | Unity 原生 Occlusion Culling | Advanced Culling System |
| --- | --- | --- |
| 核心算法 | 编辑器烘焙 cell/PVS，运行时由 Camera 查询 | 动态 Physics 射线采样；静态 Geometry/Visibility Tree 烘焙 |
| 动态对象作遮挡物 | 不支持；动态对象只能作为被遮挡物 | Collider 和 Layer 正确时可参与动态遮挡 |
| 控制目标 | 主要是 Renderer 绘制 | Renderer、LODGroup、Light、自定义回调，可保留阴影 |
| 运行成本 | 查询烘焙数据，额外内存；不需要每帧 Physics 射线 | 动态模式每帧产生射线并可能在 `LateUpdate` 等待 Job |
| 正确性特征 | 成熟、保守、引擎级多相机集成 | 有限采样可能漏掉小目标或快速运动，需要生命周期和误剔除测试 |
| 当前可视化 | cell、portal、visibility line | 裁剪率/命中率/帧时/渲染统计；静态树解释仍较弱 |

因此 ACS 不是 Unity 原生方案的“更快替代品”。它更适合动态遮挡物、非 Renderer 逻辑目标，或希望把动态射线与自定义静态 PVS 组合起来的项目；稳定室内几何仍应优先实测 Unity 原生烘焙方案。

## 与开源方案的区别和差距

| 项目 | 路线 | ACS 的优势 | ACS 的主要差距 |
| --- | --- | --- | --- |
| [mackysoft/Vision](https://github.com/mackysoft/Vision) | 包装 Unity `CullingGroup` | 自有动态遮挡与静态可见树，目标类型更丰富 | 缺少距离带和统一 VisibilityState；射线成本更高 |
| [GPU Based Occlusion Culling](https://github.com/przemyslawzaworski/Unity-GPU-Based-Occlusion-Culling) | GPU 包围盒深度测试后回读 CPU | 支持 LODGroup、自定义目标、阴影策略和较完整编辑器工作流 | 没有真正的屏幕空间可见性后端；Physics 采样不如像素判定直接 |
| [Unity Virtual Mesh](https://github.com/Unity-Technologies/com.unity.virtualmesh) | Hi-Z、cluster/meshlet、GPU 间接绘制与流送 | Unity 2022.3/GameObject/非 URP 专属，接入成本低 | 没有 Hi-Z、GPU 批量测试、cluster LOD、Indirect 和页面流送 |

当前最关键的后续工作是：固定相机轨迹与多轮 Benchmark Runner、误剔除 Oracle、多相机可见性合并、动态目标原因着色、静态树当前 cell/可见集可视化，以及把同帧 `Complete()` 改为可验证的跨帧流水线。完整源码调研和优先级见 [`docs/research/culling-system-comparison.md`](docs/research/culling-system-comparison.md)。

点击 `Run validation` 可以检查静态裁剪相机和 Camera Zone 是否缺少可见性树或尚未完成烘焙。提交场景前建议运行一次，并结合 Unity Profiler、Frame Debugger 和 Rendering Statistics 验证实际收益。

## Git 忽略内容

Unity 的 `Library/`、`Logs/`、`obj/` 和 `UserSettings/` 等生成目录不纳入版本控制。
