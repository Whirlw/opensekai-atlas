# 构建宏

> 目的：列出工程里会改变编译结果的宏，说明各自打开后会多出什么代码、怎么开关。
> 适用读者：开发者、打包人员。
> 前置条件：无。
> 最后更新：2026-11-02

## 概述

编译宏定义在 `ProjectSettings/ProjectSettings.asset` 的 `scriptingDefineSymbols` 里，**按平台分别设置**。
当前默认只有 `DOTWEEN`。其余宏都是可选的开发开关，不进正式包。

## 核心内容

| 宏 | 作用 | 默认 | 打开后多出的内容 |
| --- | --- | --- | --- |
| `DOTWEEN` | 补间库 | **开** | 引擎层大量 UI/动画代码依赖它，关掉会编译失败 |
| `OPENSEKAI_PERF_HUD` | 真机帧率 HUD | 关 | `Assets/OpenSekai/App/Diagnostics/PerformanceHud.cs` 整体编入；右上角 IMGUI + `[PerfHud]` 日志 |
| `OPENSEKAI_VIDEO_EXPORT` | 已删除 | 关 | 无：视频导出模块已删除（备份目录也已清理），该宏不再有任何作用 |

历史上还有过 `UNITY_EDITOR` 之类的内置宏与 CRIWARE 的宏，那些由 Unity/插件自动注入，不需要手配。

### 开关方式

编辑器的三种方式等价：

```text
1. Project Settings → Player → Other Settings → Scripting Define Symbols
2. 直接改 ProjectSettings.asset 的 scriptingDefineSymbols（先关 Unity）
3. 通过构建 API 设置（自动化时用）
```

多平台分别配置时不要漏：`Android` 与 `Standalone` 各自有一份。

### 与构建规则的关系

- 权限剥离（`Assets/OpenSekai/Editor/RecordingBuildRules.cs`）现在**始终生效**，不再包在
  `#if !OPENSEKAI_VIDEO_EXPORT` 里。
- Android 的 `IPostGenerateGradleAndroidProject` 只在构建时执行，不影响编辑器编译。

## 示例

临时打一个带性能 HUD 的包：

```text
Player Settings → Scripting Define Symbols:
  DOTWEEN;OPENSEKAI_PERF_HUD
→ 出包 → 装到真机 → adb logcat -s Unity | Select-String \[PerfHud\]
→ 采完记得改回 DOTWEEN
```

## 常见问题与故障排查

- **宏写错导致满屏 `error CS`**：例如把 `DOTWEEN` 删掉，`Assets/Scripts` 里大量 `using DG.Tweening` 失败。
- **加了宏但 HUD 不出现**：宏是按平台的，确认改的是目标平台那一份，并且重新出过包。
- **`OPENSEKAI_VIDEO_EXPORT` 定义了也没用**：模块已删除，属预期。

## 相关文档

- [真机性能](../operations/android-performance.md)
- [Android 构建](../operations/android-build.md)
- [ADR-0003 删除视频导出](../decisions/0003-drop-ui-packages.md)
