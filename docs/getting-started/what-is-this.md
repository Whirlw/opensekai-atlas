# 这是什么

> 目的：介绍当前版本的玩家功能和项目入口。
> 适用读者：新成员、开发者、使用者。
> 前置条件：无。
> 最后更新：2026-11-20

## 概述

Open Sekai Atlas 是开源下落式音游，Android 应用显示为 OpenSekai。首页可进入曲库和服务器，游玩 Open Sekai 曲包或浏览 Sonolus 谱面。打歌素材可在设置中切换，首页资源包卡片仍为预告。项目基于上游代码进行大规模重构与扩展。 [cubeww/OpenSekai](https://github.com/cubeww/OpenSekai)

- 引擎：Unity **6000.6.7f1**，URP 17.6.0
- 构建场景：`Library`（首场景）→ `TransitionBlank` → `Core`
- 入口：`Assets/Game/Runtime/OpenSekai/App/Bootstrap/LibraryEntryPoint.cs` → `LibraryScreen`

## 核心内容

### 现在有什么

| 能力 | 位置 | 说明 |
| --- | --- | --- |
| 曲库首页 | `Assets/Game/Runtime/OpenSekai/App/UI/LibraryScreen.cs` | 代码构建的 UGUI 页面，含封面、手动/自动入口 |
| 曲包导入 | `Assets/Game/Runtime/OpenSekai/App/UI/LibraryImport.cs` | ZIP / 文件夹（桌面用 StandaloneFileBrowser，移动端用 NativeFilePicker） |
| 设置面板 | `Assets/Game/Runtime/OpenSekai/App/UI/LibrarySettingsPanel.cs` | 流速、判定延迟、帧率、亮度、音量和打歌素材 |
| 加载浮层 | `Assets/Game/Runtime/OpenSekai/App/UI/LibraryLoadingOverlay.cs` | 开局进度文案 + 取消 |
| 打歌运行时 | `Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/` | `SoloLiveController` / `LiveLogic` / `ScoreLogic` |
| 结算页 | `Assets/Game/Runtime/OpenSekai/App/UI/Result/ResultPage.cs` | 曲库与服务器共用的全屏页：分数、评级、FC/AP、判定、本次成绩分与 Ranking Score 变化、再玩一次 / 返回来源页 |
| 暂停层 | `Assets/Game/Runtime/Shared/UI/LivePauseOverlay.cs` | 继续 / 重开 / 退出到曲库 |
| 成绩历史 | `Assets/Game/Runtime/OpenSekai/App/Library/Storage/CustomMusicScorePlayHistoryStorage.cs` | JSONL，含 `chartHash` / `rulesVersion` / `sessionId` |

### 现在没有的

- 谱面编辑器已移出工程。历史实现可从重构前的 Git 提交取回。
- 录屏 / 视频导出（`OPENSEKAI_VIDEO_EXPORT` 构型与预留接口都已删除）
- 依赖：MessagePack、uPalette、SoftMask、UI Particle、NuGetForUnity 全部摘除

## 详细说明

### 对外契约只有两个

1. **曲包格式** `.oskp`：`manifest.json` + `score.json` + 音频 + 封面。用户自己导入的曲包会长期存在，字段不能随便改。
2. **AssetBundle 清单字段名**：`AssetBundleInfo` 的 `version` / `bundles` 与每个 bundle 的元数据字段。

除此之外**不承诺向后兼容**：旧编辑器的工程文件、旧曲库界面、旧页面注册表、旧历史记录一律不迁移。
这条立场的来源见 [ADR-0005](../decisions/0005-no-legacy-compat.md)。

### 发行包

0.6.0 Android 发布包为 **95,176,430 字节（90.77 MiB）**，支持 ARM64。 [0.6.0 发布页](https://github.com/Whirlw/opensekai-atlas/releases/tag/v0.6.0) · [Android 构建](../operations/android-build.md)

## 示例

从首页进入曲库后，可以导入曲包、选择谱面并开始手动或自动演奏。

判断「我打开的是不是重构后的工程」：

```text
存在：Assets/Game/Scenes/Library.unity     → 是
```

## 常见问题与故障排查

- **看不到曲库页**：`Library.unity` 的入口组件必须是 `LibraryEntryPoint`，不是 `MusicScoreMakerEntryPoint`（后者已删除）。
- **想找旧版说明**：旧版实现文档已随重构移除；需要追溯时从 git 历史取回。

## 相关文档

- [环境与运行](build-and-run.md)
- [打一首歌](../guides/play-a-chart.md)
- [模块划分](../architecture/modules.md)
- [ADR-0005 不背历史包袱](../decisions/0005-no-legacy-compat.md)
