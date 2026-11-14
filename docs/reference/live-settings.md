# 设置项

> 目的：把「设置面板上的 9 行」与「`LiveSettingData` 的字段」对应起来，说明落盘位置与生效时机。
> 适用读者：开发者、使用者。
> 前置条件：无。
> 最后更新：2026-11-02

## 概述

设置有两个来源：`LiveSettingData`（走 `LiveSettingsStore` 存 JSON）与 `ApplicationLocalSettings`（本地音量等）。
曲库设置面板只暴露其中 9 项，其余字段由 live 逻辑读取，界面上没有入口。

## 核心内容

### 判定追踪

设置中的「测试模式」控制追踪总开关，顶部「追踪选项」可分别选择原始触摸、按下事件、常驻通道、抬起事件、判定结果、长条持握、长条尾键、划动、对象回收、画面表现和画面逐帧。选项立即保存并生效；关闭总开关保留已选分类。首次使用默认只选择判定结果，全部分类关闭时不会记录分类日志。原始触摸、常驻通道和画面逐帧记录频繁，排查时按需启用。开启测试模式时，异常规则检查仍会记录。日志写入设备持久化目录的 `logs` 文件夹。

「追踪选项 → 性能 HUD」独立控制帧率和耗时显示，默认关闭，立即生效并记住选择；关闭时停止采样及性能日志，不依赖测试模式总开关。

「追踪选项 → Last 长条标红」默认关闭；开启后处于 Last 状态的长条条身显示为红色，离开该状态或关闭选项后恢复原有颜色。此选项独立于测试模式总开关。

按下追踪记录分配前的候选列表、分配后的选择和实际处理结果；抬起追踪记录满足车道和时间条件的候选、因其他手指被跳过的候选、最终选择和结算结果。列表包含音符身份、时间和车道，空列表表示没有符合初步条件的候选。

### 面板上的 9 项

| 面板行 | 字段 | 取值 | 生效时机 |
| --- | --- | --- | --- |
| 判定延迟 | `TimingAdjustData` | 秒，步进 0.01 | 下次开局 |
| 音符流速 | `NoteSpeed` | 步进 0.1 | 下次开局 |
| 同时押线 | `UseSimultaneousPushingLine` | 开 / 关 | 下次开局（构建运行时谱面时读取） |
| 帧率 | `FrameRateLimit` | 跟随屏幕 / 120 / 144 / 165 / 185 / 240 | **立即**（`FramerateUtility.ApplyFrameRateLimit`） |
| 音乐音量 | `ApplicationLocalSettings.LiveVolume` | 0–1 | **立即**（`SoundManager.SetupVolume`） |
| 音效音量 | 同上（另一个通道） | 0–1 | **立即** |
| 音符皮肤 | `NoteSkinIndex` | 0 / 1 → `live/note/custom01|02` | 下次开局 |
| 打击音 | `NoteSeIndex` | 0 / 1 → `custom01|02` | 下次开局 |
| 打击特效 | `NoteEffect` | 0 / 1 → `effect_asset/live/tap_effect/0|1` | 下次开局 |

面板底部有一行提示：「皮肤、打击音与打击特效在下一次开局时生效。」

### 其余常用字段（无界面入口）

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Brightness` | float | 画面亮度 |
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

`Assets/Game/Runtime/Sekai/Live/LiveConfig.cs` 的 `LoadOptionData()`（259–274 行）**每次开局**读盘：

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

改判定延迟的等效代码（面板内部就是这么做的）：

```csharp
var settings = LiveSettingsStore.Shared.Load();
settings.TimingAdjustData = value;
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
