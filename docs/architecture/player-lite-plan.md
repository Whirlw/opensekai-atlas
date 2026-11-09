# 轻量化重构方案（设计稿索引）

> 目的：这是「去掉谱面编辑器、只保留打歌」重构的设计稿入口。原文已按章节拆成 6 个分册，本页负责导航与现状对照。
> 适用读者：开发者。
> 前置条件：先读 [这是什么](../getting-started/what-is-this.md) 与 [架构总览](README.md)。
> 最后更新：2026-11-02

## 概述

这份方案写于 2026-11-01，当时谱面编辑器还在工程里。它描述的是**目标**（模块怎么分、阶段怎么走、验收看什么），
不是当前实现的说明书。当前行为请看 [架构](README.md) 下的各篇。

方案已经大部分落地；少数条目没有实现或偏离了设计，集中在下面的「现状对照」。

## 分册地图

| 分册 | 内容 | 对应原文 |
| --- | --- | --- |
| [结论、起点与边界](plan/01-goals-and-boundaries.md) | 推荐结论、当时的工程起点、产品边界（为什么只保留打歌） | §1–§3 |
| [目标结构与数据契约](plan/02-target-structure.md) | 模块划分、最小数据契约、是否第一轮上 asmdef | §4 |
| [谱面兼容](plan/03-chart-compatibility.md) | 为什么兼容最重要、旧 JSON 与 SUS 的等价验证要求 | §5 |
| [启动、页面、Live 与时间](plan/04-startup-pages-live.md) | 启动链、页面收敛、Live 会话轻量化、时间与输入处理 | §6–§8 |
| [资源、依赖与用户数据](plan/05-resources-deps-data.md) | 资源裁剪、依赖分批、构建规则、用户数据迁移 | §9–§10 |
| [阶段、清单、验收与风险](plan/06-phases-and-acceptance.md) | 分阶段实施与退出条件、待改清单、验收标准、风险控制 | §11–§14 与附录 |

## 现状对照（2026-11-02 核对）

设计稿与当前代码的差异，按「设计条目 → 现状」列出。**这是清单，不是判断**：要不要补齐，需要另行决定。

| 设计条目 | 现状 | 证据 |
| --- | --- | --- |
| §4.1 最小数据契约（`PlaySession` / `RuntimeChartDefinition`） | 未实现，**暂不计划做** | `Assets/Game/Runtime/OpenSekai/App/Playback/` 下是 `PlayRequest` / `PlayStartResult` / `PlayService` / `PlaySessionContext`（会话上下文，不是会话对象），外加 `LegacyLiveBootAdapter`；运行时谱面直接是 `ChartDocument`。引入 `PlaySession` 要同时改 Live 启动链与 `SoloLiveController`，收益只是"多一层抽象"，风险大于收益 |
| §5.2 谱面 DTO 抽离 | 未按该节执行，数据层直接改用 `ChartDocument` | `ChartFileReader` 返回 `ChartDocument`；`ChartJson` 里只剩旧类型名 binder |
| §5.3 旧 JSON 不能靠改名字解决 | 已用 binder 映射类型名解决 | `Assets/OpenSekai/Charts/Serialization/ChartJson.cs:37`、`:67` |
| §6.2 页面收敛到 7 个 | 已完成 | `Resources/screen/prefabs` 7 个 prefab；`EntryScreenLayers.asset` 7 条 |
| §6.4 清除返回编辑器的整条链 | 已完成 | `SoloLiveController.OnExit` 只认 `ReturnScene ?? Library` |
| §7.4 热路径两处问题 | 已修复 | `LiveLogic.ComputeInputMusicTime`；`OnInput` 不再 `ToArray()` |
| §8.3 不立即替换音频后端 | 遵守 | 仍是 `AcbDecoder` 全量解码 + `Stopwatch` 时钟 |
| §9.3 构建规则跟着改 | 已完成 | `OpenSekaiAssetBundleBuildPipeline.CreateLightweightBuilds` + `RecordingBuildRules` 权限剥离 |
| §9.4 第三方依赖分批 | 已完成 | 已删：uPalette、SoftMask、UI Particle、MessagePack（含 NuGetForUnity）、Timeline、视频导出、`com.coplaydev.unity-mcp`；`Packages/manifest.json` 只剩 URP／uGUI／TMP／Input System／UniTask／Native File Picker 与应用必需的内置模块 |
| §13.4.7 真机性能达标 | 部分完成 | 真机 165 Hz、p95 6.04 ms 已采；Windows 产物本机跑不起来，桌面基线缺失 |

## 常见问题与故障排查

- **想看某节的原文**：按上表的「对应原文」找分册；正文按原章节顺序保留，只做了章节拆分与开头四行。
- **分册与当前代码冲突**：以 [架构](README.md) 下的现行文档为准；冲突点记到本页的「现状对照」。

## 相关文档

- [架构总览](README.md)
- [ADR-0001 去掉谱面编辑器](../decisions/0001-drop-chart-editor.md)
- [重构日志 2026-11](../changelog/2026-11-refactor-log.md)