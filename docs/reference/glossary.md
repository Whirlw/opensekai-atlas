# 术语表

> 目的：统一文档里出现的名词，避免「谱面/曲包/关卡」混用造成歧义。
> 适用读者：所有人。
> 前置条件：无。
> 最后更新：2026-11-02

## 概述

按主题分组，每条给出**在本仓库里的确切含义**与对应代码位置。

## 核心内容

### 数据与资源

| 术语 | 含义 | 位置 |
| --- | --- | --- |
| 曲包（package） | 一个目录，含 `manifest.json` / `score.json` / 音频 / 封面 | `Assets/Game/Runtime/OpenSekai/Infrastructure/Packages/` |
| `.oskp` | 曲包的打包扩展名（ZIP 结构），导入时解开 | `LibraryImport` / `SongPackageManager` |
| 谱面（chart） | `score.json` 的内容：音符 + 事件 + 时间信息 | `Assets/Game/Runtime/OpenSekai.Core/Charts/ChartDocument.cs` |
| 音符（note） | 谱面里的单个可打击对象 | `Charts/ChartNote.cs` |
| 事件（event） | 谱面里的非音符对象（BPM、流速等） | `Charts/ChartEvent.cs` |
| AB / AssetBundle | 运行时按需加载的资源包 | `AssetBundleManager`、`Assets/StreamingAssets/data` |
| 清单（catalog） | `AssetBundleInfo.bytes`，记录 bundle 名、hash、依赖 | `Sekai/AssetBundleInfo.cs` |
| 皮肤（skin） | 音符外观套装，当前 0/1 两套 | `live/note/custom01|02` |

### 运行与判定

| 术语 | 含义 | 位置 |
| --- | --- | --- |
| live | 一次打歌会话（从进入 Core 场景到结算） | `Sekai/Core/Live/SoloLiveController.cs` |
| AUTO / 自动模式 | 全部音符自动命中，用于验证 | `FreeLiveBootData.IsAuto` |
| 判定（judgement） | JustPerfect / Perfect / Great / Good / Bad / Miss / Auto | `NoteResult` |
| 判定窗口 | 以帧定义的容差，`CalcFrameToTime(frame) = frame/60` | `LiveLogic` |
| 音乐时间 | 按音频进度换算的秒数，判定与谱面推进都用它 | `BaseLiveController.currentAudioLatencyMusicTimeMs` |
| 回填（input rewind） | 把触摸时间戳折算回判定时刻 | `LiveLogic.ComputeInputMusicTime` |
| 规则版本（RulesVersion） | 计分/判定口径的编号，当前 2 | `App/Playback/PlaySessionContext.cs` |
| 会话（session） | 一次开局的标识，12 位十六进制 | 同上 |

### 工程与流程

| 术语 | 含义 |
| --- | --- |
| 引擎层 | `Assets/Game/Runtime/Sekai/`，页面系统、live、音频等底层运行时逻辑 |
| 新代码层 | `Assets/Game/Runtime/OpenSekai/`，本次重构新增的代码 |
| `_removed/` | 重构期间的备份目录（已删除，2026-11-03）；需要旧源码时从重构前的 git 提交取回 |
| 页面（ScreenLayer） | `ScreenManager` 管理的 UI 页面单元 |
| `[LibraryCheck]` | `OpenSekai/Library/Verify Running Scene` 打出的自检日志 |
| 轻量构型 | 只有 `DOTWEEN` 宏的默认构建（无 HUD、无视频导出） |

## 常见问题与故障排查

- **「曲包」和「谱面」混用**：曲包是目录/压缩包，谱面是其中的 `score.json`。
- **「关卡」**：本仓库文档不用这个词，一律用「曲包」或「谱面」。

## 相关文档

- [曲包与谱面数据](../architecture/chart-package.md)
- [打歌运行时](../architecture/live-runtime.md)
- [设置项](live-settings.md)
