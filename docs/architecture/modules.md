# 模块划分

> 目的：给出目录与职责的对应表，说明新旧代码的边界在哪、依赖朝哪边指。
> 适用读者：开发者。
> 前置条件：读过 [架构总览](README.md)。
> 最后更新：2026-11-02

## 概述

判断一个文件该放哪，只看一条：**它是不是「打开曲库 → 打歌 → 回曲库」这条路径的一部分**。
是 → `Assets/OpenSekai/`；不是但被它会用到 → `Assets/Scripts/Assembly-CSharp/`（引擎层）；只在编辑器里跑 →
`Assets/*/Editor/`。

## 核心内容

### 新代码层 `Assets/OpenSekai/`

| 目录 | 职责 | 代表文件 |
| --- | --- | --- |
| `App/Bootstrap/` | 应用入口：EventSystem、字体、音量、帧率、页面注册 | `LibraryEntryPoint.cs`（107 行） |
| `App/Playback/` | 开局服务、请求/结果模型、会话上下文 | `PlayService.cs`（212）、`PlayResult.cs`（158）、`PlaySessionContext.cs`（40） |
| `App/UI/` | 曲库页与它的浮层 | `LibraryScreen.cs`（815）、`LibrarySettingsPanel.cs`（348）、`LibraryResultPanel.cs`（187） |
| `App/Scenes/` | 首场景 `Library.unity` | — |
| `Charts/` | 运行时可用的谱面模型与 JSON 读写 | `ChartDocument.cs`、`ChartNote.cs`、`ChartTimeline.cs`（366）、`Serialization/ChartJson.cs`（74） |
| `Charts/Formats/Sus/` | SUS 文本格式的数据类型 | `NoteInfo.cs`、`BpmInfo.cs` 等 7 个 |
| `Infrastructure/Packages/` | 曲包发现、读取、导入、导出 | `SongPackageManager.cs`（756）、`SongPackageStorage.cs`（136） |
| `Infrastructure/Charts/` | 谱面文件读取 | `ChartFileReader.cs`（47） |
| `Infrastructure/Settings/` | 设置模型与存储 | `LiveSettingData.cs`（272）、`LiveSettingsStore.cs`（52） |
| `Infrastructure/Audio/` | 曲包内音频解码 | `PackageAudio.cs`（138） |
| `Live/Conversion/` | 谱面 → 运行时音符/事件 | `RuntimeChartConverter.cs`（624）、`SusRuntimeConverter.cs`（1275）、`RuntimeChartImporter.cs`（172） |
| `Editor/` | 只在编辑器里跑的校验与构建规则 | `LibrarySceneSetup.cs`、`LibraryPlayCheck.cs`、`RecordingBuildRules.cs` |

### 引擎层 `Assets/Scripts/Assembly-CSharp/`

这部分是既有工程遗留，全部编在一个程序集（除 3 个 asmdef：`CRIMW` Runtime/Editor、`ZString`）。

| 路径 | 内容 |
| --- | --- |
| `Sekai/Core/Live/` | `SoloLiveController.cs`（424 行）、`LiveLogic.cs`（1524 行）、`ScoreLogic.cs`、`BaseLiveController.cs` |
| `Sekai/`（根） | `ScreenManager`、`SceneManager`、`NativeInput`、`AssetBundleManager`、`LiveTransitioner`、`LiveOutUIController` |
| `Sekai/UI/` | 通用 UI 组件（`UIPartsMarquee`、按钮特效等） |
| `Sekai/MusicScoreMaker/Common/` | 曲库仍在用的存储层：`CustomMusicScoreEntry/Storage/Manifest/PlayHistory` |
| `CP/` | 工具：`FramerateUtility`、`BinarySerializer`（现在是 Newtonsoft JSON） |
| `CriWare/` | `CriAtomShim`（遮蔽官方 CRI 类型的本地实现） |

### 依赖方向

```text
App/UI  →  App/Playback  →  Infrastructure/*  →  Charts/*
                                   ↓
                        引擎层（SceneManager / SoloLiveController / AssetBundleManager）
```

反向依赖只有一处：`Sekai/MusicScoreMaker/Common/CustomMusicScoreEntry` 暴露曲包路径与音频，
供 `Infrastructure/Packages` 使用。改动时不要把新代码塞回 `Sekai/MusicScoreMaker/`。

### 已移出工程的部分（备份目录已于 2026-11-03 删除，源码见重构前的 git 提交）

| 备份目录 | 内容 |
| --- | --- |
| `editor-tree-2026-11-01/` | 谱面编辑器 527 个脚本 |
| `editor-assets-2026-11-01/` | 编辑器画面 prefab、12 个编辑器 `ScreenLayerData`、编辑器场景 |
| `legacy-library-ui-2026-11-01/` | 旧曲库页 `ScreenLayerCustomMusicScoreManager`（3409 行） |
| `screens-registry-2026-11-01/` | 未被引用的页面注册表与 213 个 `ScreenLayerData` |
| `video-export-2026-11-02/` | 录屏/视频导出 15 个脚本 |
| `messagepack-2026-11-02/` | 标注备份、formatter、NuGet DLL |
| `palette-2026-11-02/` | uPalette 生成的 17 个脚本与 PaletteStore |
| `uiparticle-2026-11-02/` | SoftMask/UI Particle 的最初拷贝 |
| `recording-seam-2026-11-02/` | 录屏接口缝 `LiveRecordingHooks.cs` |
| `module-readmes-2026-11-02/` | `Assets/` 下 5 份过期模块 README（内容已吸收进 [曲包与谱面数据](chart-package.md) 等文档） |
| `ffmpeg-2026-11-02/` | 185.3 MB ffmpeg 二进制 |

备份目录 `_removed/` 已删除；`.gitignore` 仍保留 `/_removed/` 规则，将来再建备份时不会误提交。

## 示例

新增一个「曲库页按钮」应该动哪些文件：

```text
1. App/UI/LibraryScreen.cs          → 加按钮与回调
2. App/UI/LibraryXxxPanel.cs        → 如果要做浮层，单独一个文件
3. App/Playback/PlayService.cs      → 只有涉及开局流程才改
4. 编辑器验证：OpenSekai/Library/Verify Running Scene 仍应 valid:true
```

## 常见问题与故障排查

- **新代码引用了 `Sekai.MusicScoreMaker.Ingame.*`**：该命名空间已随编辑器删除，编译必然失败。
- **想知道某文件属于哪一层**：先看它在不在 `Assets/OpenSekai/` 下，再看它是否被 `App/UI` 直接引用。

## 相关文档

- [曲包与谱面数据](chart-package.md)
- [打歌运行时](live-runtime.md)
- [ADR-0001 去掉谱面编辑器](../decisions/0001-drop-chart-editor.md)
