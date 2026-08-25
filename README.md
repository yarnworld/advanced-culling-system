# Advanced Culling System

Unity 2022.3 项目，提供动态裁剪（Dynamic Culling）与静态裁剪（Static Culling）两套可组合的可见性优化方案，适合用于减少大型场景中的渲染和对象更新开销。

## 项目内容

- 动态裁剪：基于相机、遮挡物、Renderer、LODGroup 和自定义目标进行运行时裁剪。
- 静态裁剪：通过几何树、可见性树和 Baker 预计算场景可见性。
- Unity Editor 工具：提供 Source、Controller、Camera、Camera Zone 和 Chunk 管理器的 Inspector 与选择工具。
- 教程场景：包含动态裁剪、实例对象、自定义目标、静态裁剪、自定义目标和透明度场景示例。

## 环境要求

- Unity `2022.3.48f1c1`
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

打开 Unity 菜单 `Tools/NGSTools/Advanced Culling System/Visualization`，可以查看动态裁剪相机的射线命中率、静态裁剪相机的烘焙状态以及 Camera Zone 的场景边界。进入 Play Mode 后，窗口会自动刷新运行时数据；Scene 视图会显示相机标签、区域烘焙状态和命中率颜色提示。

窗口中的性能诊断区域会聚合最近一帧的动态裁剪数据，包括动态相机数量、射线数量、命中数量、射线检测耗时和命中率，并保留最近 120 帧的命中率曲线。绿色曲线表示射线命中率变化；命中率很低通常说明场景遮挡较少或射线预算偏高，命中率很高则需要重点观察裁剪收益和误剔除风险。

点击 `Run validation` 可以检查当前已加载场景中的静态裁剪相机和 Camera Zone 是否缺少可见性树或尚未完成烘焙。建议在提交场景前运行一次检查，并结合 Unity Profiler、Frame Debugger 和 Rendering Statistics 判断裁剪是否真正降低了 CPU、Render Thread 或 GPU 开销。

## Git 忽略内容

Unity 的 `Library/`、`Logs/`、`obj/` 和 `UserSettings/` 等生成目录不纳入版本控制。
