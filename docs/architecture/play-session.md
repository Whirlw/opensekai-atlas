# 开局数据契约

> 目的：说明「点一下开始」之后，曲库那一侧究竟把什么交给了 live 场景，以及这份数据在哪里成型。
> 适用读者：要改开局流程、加新玩法或排查「开局失败」的开发者。
> 前置条件：读过 [曲包与谱面数据](chart-package.md)、[打歌运行时](live-runtime.md)。
> 最后更新：2026-11-03

## 概述

开局分两半：`OpenSekai.App` 的曲库侧负责**把曲包变成一次会话**，`Sekai` 的引擎侧只认
`FreeLiveBootData`。两边之间只有一个对象——`PlaySession`，它的核心是一份
`RuntimeChartDefinition`（运行时谱面）。

```text
SongPackage ──ChartFileReader.Read──▶ ChartDocument
                                          │ RuntimeChartConverter.Convert
                                          ▼
                                   RuntimeChartDefinition
                                   （MusicScore + 总音符数）
                                          │
PlayRequest + 设置快照 ─────────────▶ PlaySession ──LegacyLiveBootAdapter──▶ FreeLiveBootData
```

## 快速开始

- 想在代码里手工起一次打歌：构造 `PlayRequest`，调 `PlayService.Instance.StartAsync(request, token)`，
  不要自己拼 `FreeLiveBootData`。
- 想读「这次打歌有多少音符、多长」：`PlaySession.Chart.TotalNoteCount` / `Chart.TicksMax`，不要再去
  `LiveUtility.CalculateTotalComboCount(score)`。
- 想知道当前设置的快照：`PlaySession.Settings`（一份已经和设置存储解耦的 `LiveSettingData` 副本）。

## 详细说明

### `RuntimeChartDefinition`

| 成员 | 含义 |
| --- | --- |
| `Document` | 读盘得到的 `ChartDocument`（tick 单位）。哈希与将来的重转换都用它。 |
| `Score` | 转换后的 `Sekai.Live.MusicScore`，`LiveLogic` 直接吃这一份。 |
| `TotalNoteCount` | `LiveUtility.CalculateTotalComboCount(Score)` 的结果，计分与结算面板都用它。 |
| `HasPlayableNotes` | 同时要求文档里有音符、且转换后总数大于 0，避免「文档有、转换后为 0」的假阳性。 |
| `TicksMax` | 谱面最后一拍的 tick，用于估算时长与调试。 |

`Create(document, score)` 是唯一的构造入口，`RuntimeChartConverter.Convert` 只在 `PlaySession.Create`
里被调用一次——之前 `PlayService` 自己转、`LibraryPlayCheck` 再转一次的分叉就是这样消失的。

### `PlaySession`

持有 `Request`（模式、返回场景）、`Package`（曲包与 manifest）、`Chart`、`BundleConfig`
（转换时用的 `LiveBundleBuildData`，排查用）、`Settings`（`PlaybackSettingsSnapshot.ToLegacyData()`
产出的独立副本，之后改设置不影响这一局）。

`PlaySession.Create` 会做参数校验并抛 `ArgumentNullException`，把「哪一步少了东西」提前到开局阶段，
而不是在 live 场景里空引用。

### 与历史上下文的关系

`PlaySession` **不**持有会话 id / 谱面哈希 / 规则版本——那些在
`OpenSekai.App.Playback.PlaySessionContext` 里，因为它还要被历史写入与结算面板读取，生命周期比一次
live 更长（跨场景）。两者名字相近但职责不同：

- `PlaySession`：一局的输入（曲包、谱面、设置、请求）。
- `PlaySessionContext`：一局的身份（`SessionId`、`ChartHash`、`RulesVersion`）。

## 示例

```csharp
// 曲库侧（PlayService.StartAsync 内部，省略错误处理）
ChartDocument document = ChartFileReader.Read(entry);
PlaybackSettingsSnapshot settings = LiveSettingsStore.Shared.CaptureSnapshot();
LiveBundleBuildData config = Resources.Load<LiveBundleBuildData>(LiveConfig.ConfigBundleNamePath);
PlaySession session = PlaySession.Create(request, entry, document, config, settings);
UserDataManager.Instance.FreeLiveBootData = LegacyLiveBootAdapter.Create(session);
```

## 常见问题

- **为什么还要 `LegacyLiveBootAdapter`？** 引擎侧只认 `FreeLiveBootData`（它还带 `MasterMusic`、
  `MasterMusicDifficulty` 等引擎侧类型）。适配器的职责就是把会话翻译成那套结构；等哪天 live 侧改成吃
  `PlaySession`，这个类就可以删掉。
- **为什么 `RuntimeChartDefinition` 不放在 `Sekai` 里？** 它同时需要 `ChartDocument`（`OpenSekai.Charts`）
  和 `MusicScore`（`Sekai`）。放 `OpenSekai.Charts` 时依赖方向是「新代码 → 引擎」，符合现有的
  `OpenSekai.App → OpenSekai.Game` 关系；反过来会把引擎拖进谱面格式。
- **它能序列化吗？** 不能，也不该。它握着 `MusicScore` 与资源引用，只在一次进程内有效。

## 相关文档

- [打歌运行时](live-runtime.md)
- [曲包与谱面数据](chart-package.md)
- [模块划分](modules.md)
