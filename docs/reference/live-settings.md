# 设置项

> 目的：将设置界面与对应字段关联起来，说明落盘位置与生效时机。
> 适用读者：开发者、使用者。
> 前置条件：无。
> 最后更新：2026-11-23

## 概述

设置有两个来源：`LiveSettingData`（走 `LiveSettingsStore` 存 JSON）与 `ApplicationLocalSettings`（本地音量等）。
从首页、曲库或服务器页右上角打开同一套设置。面板分为判定与延迟、画面、声音、外观和开发。

## 核心内容

### 判定追踪

设置首页的「测试模式」是诊断总开关。开启后显示「追踪选项」，可选择原始触摸、按下事件、常驻通道、抬起事件、判定结果、长条持握、长条尾键、划动、对象回收、画面表现、画面逐帧和音频同步。关闭测试模式会隐藏入口并停用所有诊断，重新开启后恢复已保存的子项选择。首次使用默认只选择判定结果。全部分类关闭时，开启测试模式仍会记录异常规则检查。日志写入设备持久化目录的 `logs` 文件夹。

「性能 HUD」控制帧率和耗时显示，默认关闭，关闭时停止采样及性能日志。

「Last 长条标红」默认关闭。开启后，处于 Last 状态的长条条身显示为红色，离开该状态或关闭选项后恢复原有颜色。

「延迟采样」在下一段打歌开始时启动。「打击音混音采样」随延迟采样启动。关闭测试模式会结束正在进行的采样并保存已采集的数据。

按下追踪记录分配前的候选列表、分配后的选择和实际处理结果；抬起追踪记录满足车道和时间条件的候选、因其他手指被跳过的候选、最终选择和结算结果。列表包含音符身份、时间和车道，空列表表示没有符合初步条件的候选。

### 常用设置

| 面板行 | 字段 | 取值 | 生效时机 |
| --- | --- | --- | --- |
| 音频延迟 | `AudioLatencyMs` | 毫秒，正值让背景音乐提前播放 | 下次开局 |
| 画面延迟 | `VisualLatencyMs` | 毫秒，正值让音符提前到线 | 下次开局 |
| 判定延迟 | `JudgeOffsetMs` | 毫秒，正值让手动判定窗口推迟 | 下次开局 |
| 判定时机 / 多押分配 | `UseLegacyJudgeTiming` / `UseFramePressMatching` | 旧判定时机会锁定为逐帧分配 | 下次开局 |
| 音符流速 | `NoteSpeed` | 步进 0.1 | 下次开局 |
| 同时押线 | `UseSimultaneousPushingLine` | 开 / 关 | 下次开局（构建运行时谱面时读取） |
| 显示 FAST / LATE | `ShowFastLate` | 默认关。开启后，GREAT、GOOD、BAD 上方显示 FAST（偏早）或 LATE（偏晚） | 下次开局 |
| 显示滑反提示 | `ShowFlickMiss` | 默认关。有方向的 Flick 判为滑反时，在 GREAT 上方显示滑反图标。旧存档的两个开关均继承原快慢提示设置 | 下次开局 |
| 帧率 | `FrameRateLimit` | 跟随屏幕 / 120 / 144 / 165 / 185 / 240 | **立即**（`FramerateUtility.ApplyFrameRateLimit`） |
| 音乐音量 | `ApplicationLocalSettings.LiveVolume` | 0–1 | **立即**（`SoundManager.SetupVolume`） |
| 音效音量 | `ApplicationLocalSettings.LiveVolume.Se` | 0–100% | 立即 |
| 界面音量 | `ApplicationLocalSettings.UiVolume` | 0–100% | 立即 |
| 响度平均 | `ApplicationLocalSettings.LoudnessNormalization` | 用于试听和演奏 | 正在播放的试听立即生效 |
| 2D MV 亮度 | `MovieBrightness` | 0–100%，每步 10%，默认 100% | 下次开局 |
| 音符皮肤、打击音与特效 | 跳转行 | 打开资源包页面 | — |

音符皮肤、打击音和打击特效的两套内置资源已移至资源包页面，由 `MaterialPackState.Builtin` 保存（套装编号 1 / 2）。首次读取缺少该字段的资源包索引时，会从下面三个旧字段导入一次。之后设置面板不再写入这三个字段，见 [资源包页面](../architecture/resource-pack-screen.md)。

| 旧字段 | 取值 | 对应资源 |
| --- | --- | --- |
| `NoteSkinIndex` | 0 / 1 | `live/note/custom01|02` |
| `NoteSeIndex` | 历史字段 | 内置打击音统一使用 `live/tap_se/live_se_key` |
| `NoteEffect` | 0 / 1 | `effect_asset/live/tap_effect/0|1` |

### 其余常用字段（无界面入口）

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Brightness` | float | 背景亮度 |
| `LaneTransparent` | float | 轨道透明度 |
| `NoteAlpha` / `GuideAlpha` / `JudgeLineAlpha` | float | 各类元素透明度 |
| `UseCutIn` | bool | 是否播放 cut-in |
| `UseVibration` | bool | 打击震动 |
| `UseAllPerfectEffect` | bool | AP 特效 |
| `IsMirror` | bool | 镜像谱面 |
| `FeverEffectTypeIndex` | int | Fever 特效类型 |
| `LiveMode` | enum | `High3D / Default3D / Mode2D / Low / OriginalMV` |

### 存储位置

| 数据 | 位置 | 写入方 |
| --- | --- | --- |
| `LiveSettingData` | `persistentDataPath` 下的设置 JSON | `LiveSettingsStore.Shared.Save` |
| 本地音量等 | `ApplicationLocalSettings`，`SaveToStorage()` | 面板的音量行 |

### 读取时机

`LiveConfig` 的 `LoadOptionData()`**每次开局**读盘。之后 `BaseLiveController.Setup` 用本局快照 `BootData.LiveSettingData` 中的三个索引（开局时由资源包页的内置套装写入）覆盖音符和打击特效的资源包名；打击音加载也读取这份快照：

```csharp
SetNoteSkinAssetBundleName(Mathf.Clamp(liveSettingData.NoteSkinIndex, 0, 1));
SetNoteSeName(liveSettingData.NoteSeIndex);
SetNoteEffectName(liveSettingData.NoteEffect);
SetFeverEffectName(liveSettingData.FeverEffectTypeIndex);
```

取名规则：`live/note/custom{i+1:00}`、`live/note_preview/custom{i+1:00}`、`custom{i+1:00}`、
`effect_asset/live/tap_effect/{i}`、`effect_asset/live/fever/{i}`。前三个都在 15 个 AB 白名单里；
`fever` 不在白名单，所以面板没有做 Fever 选项。

## 示例

修改判定偏移的等效代码（面板内部就是这么做的）：

```csharp
var settings = LiveSettingsStore.Shared.Load();
settings.JudgeOffsetMs = TimingCompensation.ClampJudgeOffset(value);
LiveSettingsStore.Shared.Save(settings);
```

## 常见问题与故障排查

- **改了皮肤没变化**：皮肤在 `LoadOptionData()` 里读盘，必须重新开局。
- **帧率选项没效果**：确认 `FrameRateLimit` 不是 0（跟随屏幕）；Android 上还要看系统是否允许该刷新率。
- **设置没保存**：`Save()` 失败会打日志；检查 `persistentDataPath` 是否可写。

## 相关文档

- [打歌运行时](../architecture/live-runtime.md)
- [AssetBundle 与清单](../architecture/asset-bundles.md)
- [真机性能](../operations/android-performance.md)
