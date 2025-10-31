# Advanced Culling System 深化调研

## 当前项目画像

本项目基于 Unity 2022.3，当前约有 80 个 C# 脚本，其中 59 个运行时脚本、17 个编辑器脚本和 4 个教程脚本。已有动态裁剪、静态裁剪、可见性树、几何树、RaycastCommand/Jobs、LODGroup/Renderer/自定义目标、烘焙工具和 6 个教程场景。

## 同类项目观察

| 项目 | 代表能力 | 对本项目的启发 |
| --- | --- | --- |
| [Unity-Technologies/com.unity.virtualmesh](https://github.com/Unity-Technologies/com.unity.virtualmesh) | GPU 驱动虚拟几何、三角形簇 LOD、深度金字塔、两阶段 GPU 遮挡裁剪、Bake 和 Debug View | 从 GameObject 级裁剪继续推进到 GPU/Cluster 级裁剪；增加深度金字塔、调试视图和跨平台能力矩阵 |
| [Unity-Technologies/Graphics GPU culling 文档](https://github.com/Unity-Technologies/Graphics/blob/master/Packages/com.unity.render-pipelines.high-definition/Documentation~/gpu-culling.md) | 使用当前帧和上一帧深度进行 GPU 遮挡判断，并强调 Frame Debugger、Rendering Statistics、Rendering Debugger | 建立上一帧可见性、滞后帧和调试指标，避免只看对象开关而无法证明渲染收益 |
| [Unity-GPU-Based-Occlusion-Culling](https://github.com/przemyslawzaworski/Unity-GPU-Based-Occlusion-Culling) | 面向静态与动态物体的 GPU 遮挡原型，关注 Forward、资源加载和阴影裁剪 | 增加动态物体、资源包/Addressables、阴影策略和平台限制的明确测试 |
| [mackysoft/Vision](https://github.com/mackysoft/Vision) | 将 Unity CullingGroup 封装成易用组件，支持编辑器、距离/可见性事件和 OpenUPM | 增加 CullingGroup 适配层，把“可见性”扩展到 AI、动画、音频、粒子和对象激活，而不只控制 Renderer |
| [Unity-DepthAPI](https://github.com/oculus-samples/Unity-DepthAPI) | 硬遮挡与软遮挡两种模式，强调深度来源、视觉稳定性和平台前置条件 | 对 XR/移动端单独做能力分级，不把屏幕空间深度、射线遮挡和传统静态烘焙混成一个开关 |

## 最值得深化的方向

### P0：先把收益证明做扎实

1. 增加自动 Benchmark 场景：1k/10k/100k 对象、低/高遮挡率、动态/静态、不同相机移动速度。
2. 每帧记录 CPU culling、Physics/Raycast、Render Thread、GPU frame time、SetActive/Renderer 开关数量、可见率和误剔除数。
3. 做 Before/After 对照并把结果写入 README，使用 Unity Profiler、Frame Debugger 和 Rendering Statistics 验证。
4. 增加裁剪正确性测试：物体刚进入视锥、快速转身、透明物体、阴影、反射相机、多个相机和 LOD 切换。

### P1：提升运行时算法质量

1. 将动态裁剪从同步等待 Raycast Job 改成完整的多帧流水线，允许上一批结果延迟一帧应用。
2. 增加可配置的可见性滞后、预测包围盒、相机速度阈值和“宁可多画不可误剔除”的安全边界。
3. 为 Renderer、LODGroup、粒子、Animator、NavMeshAgent、音频和自定义脚本提供统一的 Visibility State API。
4. 增加多相机、Scene additive、Addressables/AssetBundle 动态注册和对象池生命周期支持。

### P1：向 SRP/GPU 方向扩展

1. 先支持 URP Renderer Feature：深度预处理、Hi-Z/Depth Pyramid 和 Compute Shader AABB frustum/occlusion test。
2. 再支持 Graphics.DrawMeshInstancedIndirect 或 BatchRendererGroup，把批量绘制与裁剪结果连接起来。
3. 对静态场景增加 cluster/meshlet 数据烘焙，而不是只保存 GameObject/Renderer 级可见性。
4. 以 GPU Virtual Mesh 的思路增加 Debug Pass：可见、被遮挡、视锥外、等待数据、LOD 层级使用不同颜色显示。

### P1：把 Editor 变成可交付工具

1. 用 ScriptableObject 保存 Bake Asset、版本号、场景 GUID、输入哈希、平台和参数，支持增量烘焙与失效检测。
2. 增加 Bake 进度、取消、错误列表、耗时、内存峰值、节点数和压缩后大小统计。
3. 增加可见性预览窗口：按 Camera Zone 查询对象，支持反查“某对象在哪些区域可见”。
4. 增加一键验证：检测未注册 Renderer、Bounds 过小、动态对象误参与静态烘焙、透明材质、多个活动相机和层设置冲突。

### P2：工程化与社区可用性

1. 把 `Assets/AdvancedCullingSystem` 包化，补齐 `package.json`、程序集版本、CHANGELOG、LICENSE 和 Samples~ 结构，支持 Git URL/OpenUPM 安装。
2. 增加 asmdef Editor/Runtime 测试、PlayMode 测试、Burst/Jobs 安全检查和最小 CI。
3. 明确支持矩阵：Built-in/URP/HDRP、Unity 2022 LTS/Unity 6、Windows/Android/iOS/Quest、Forward/Deferred。
4. README 增加架构图、安装步骤、最小示例、性能表、限制、FAQ、截图/GIF 和 API 文档入口。

## 建议的下一版里程碑

### v0.2：可证明

Benchmark + Profiling HUD + 正确性测试 + Bake 统计 + 透明/阴影/多相机限制说明。

### v0.3：可集成

统一 Visibility API + Addressables/对象池支持 + 增量 Bake Asset + URP Renderer Feature 原型。

### v0.4：可扩展

Hi-Z GPU occlusion + indirect rendering 示例 + GPU 调试视图 + 移动端/XR 基准场景。

## 结论

项目已经具备继续深化的基础，但不建议单纯继续堆类和教程场景。最有价值的顺序是：先用 Benchmark 证明现有 CPU/Job 方案在不同遮挡率下的收益，再以统一 Visibility API 和可复现 Bake Asset 提升可用性，最后把 URP/GPU 路线作为独立后端接入。这样可以同时形成技术亮点、性能证据和开源项目的可信度。
