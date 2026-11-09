# 这是什么

> 目的：一句话说清这个仓库现在是什么形态，避免新人按「还有谱面编辑器」的旧印象去读代码。
> 适用读者：新成员、开发者、使用者。
> 前置条件：无。
> 最后更新：2026-11-02

## 概述

本仓库是 Project Sekai 玩法的一个**学习/研究向复刻**，现在处于一次「轻量播放器」重构之后的状态：
**只保留打歌（曲库 → 加载 → live → 结算 → 返回曲库）**，谱面编辑器已整块移除。

- 引擎：Unity **2022.3.62f3**，URP 14
- 构建场景：`Library`（首场景）→ `TransitionBlank` → `Core`
- 入口：`Assets/OpenSekai/App/Bootstrap/LibraryEntryPoint.cs` → `LibraryScreen`

## 核心内容

### 现在有什么

| 能力 | 位置 | 说明 |
| --- | --- | --- |
| 曲库首页 | `Assets/OpenSekai/App/UI/LibraryScreen.cs` | 代码构建的 UGUI 页面，含封面、手动/自动入口 |
| 曲包导入 | `Assets/OpenSekai/App/UI/LibraryImport.cs` | ZIP / 文件夹（桌面用 StandaloneFileBrowser，移动端用 NativeFilePicker） |
| 设置面板 | `Assets/OpenSekai/App/UI/LibrarySettingsPanel.cs` | 9 项：判定延迟、流速、同时押线、帧率、音量、音符皮肤、打击音、打击特效 |
| 加载浮层 | `Assets/OpenSekai/App/UI/LibraryLoadingOverlay.cs` | 开局进度文案 + 取消 |
| 打歌运行时 | `Assets/Scripts/Assembly-CSharp/Sekai/Core/Live/` | `SoloLiveController` / `LiveLogic` / `ScoreLogic` |
| 结果面板 | `Assets/OpenSekai/App/UI/LibraryResultPanel.cs` | 分数、评级、连击、判定、再玩一次 / 返回曲库 |
| 暂停层 | `Assets/OpenSekai/App/UI/LivePauseOverlay.cs` | 继续 / 重开 / 退出到曲库 |
| 成绩历史 | `Sekai/MusicScoreMaker/Common/CustomMusicScorePlayHistoryStorage.cs` | JSONL，含 `chartHash` / `rulesVersion` / `sessionId` |

### 现在没有的

- 谱面编辑器（`Assets/Scripts/Assembly-CSharp/Sekai/MusicScoreMaker/{Ingame,OutGame}` 已移出工程，
  备份目录 `_removed/` **已于 2026-11-03 删除**，源码可从重构前的 git 提交取回）
- 录屏 / 视频导出（`OPENSEKAI_VIDEO_EXPORT` 构型与预留接口都已删除）
- 依赖：MessagePack、uPalette、SoftMask、UI Particle、NuGetForUnity 全部摘除

## 详细说明

### 对外契约只有两个

1. **曲包格式** `.oskp`：`manifest.json` + `score.json` + 音频 + 封面。用户自己导入的曲包会长期存在，字段不能随便改。
2. **AssetBundle 清单字段名**：`AssetBundleInfo` 的 `version` / `bundles` 与每个 bundle 的元数据字段。

除此之外**不承诺向后兼容**：旧编辑器的工程文件、旧曲库界面、旧页面注册表、旧历史记录一律不迁移。
这条立场的来源见 [ADR-0005](../decisions/0005-no-legacy-compat.md)。

### 数字（2026-11-02）

- `Assets` 下 `.cs`：**909**（重构起点 1,437）
- Unity 包：39 个 `com.*`（11 个真实包 + 28 个内置模块）
- Android APK：**123.9 MB**（ARM64、IL2CPP、Low 剥离、仅 `INTERNET` 权限）
- 真机打歌：165 Hz 下 p95 帧时 **6.04 ms**

## 示例

上图是重构后的曲库页：左侧封面缩略图、右侧手动/自动入口，头部 4 个按钮（导入 ZIP / 导入文件夹 / 设置 / 刷新）。

判断「我打开的是不是重构后的工程」：

```text
存在：Assets/OpenSekai/App/Scenes/Library.unity     → 是
不存在：Assets/Sekai/MusicScoreMaker/Ingame/        → 是（已移出）
```

## 常见问题与故障排查

- **看不到曲库页**：`Library.unity` 的入口组件必须是 `LibraryEntryPoint`，不是 `MusicScoreMakerEntryPoint`（后者已删除）。
- **想找编辑器时代的说明**：见 [历史文档](../reference/legacy/README.md)，那些内容描述的是已删除的代码。

## 相关文档

- [环境与运行](build-and-run.md)
- [打一首歌](../guides/play-a-chart.md)
- [模块划分](../architecture/modules.md)
- [ADR-0005 不背历史包袱](../decisions/0005-no-legacy-compat.md)
