# 打歌运行时

> 目的：说明从点击「手动/自动」到回到曲库之间，代码里发生了什么，尤其是时间与计分这两块容易改错的地方。
> 适用读者：开发者（改判定、改时间轴、改结算）。
> 前置条件：读过 [曲包与谱面数据](chart-package.md)。
> 最后更新：2026-11-02

## 概述

一条会话跨两个场景：曲库（`Library`）里调 `PlayService.StartAsync` 组装启动数据，切到 `Core` 场景由
`SoloLiveController` 驱动；打完在 `PreExit` 发布结果，`OnExit` 请求切回曲库。判定、计数、计分都在
`LiveLogic` / `ScoreLogic` 里，按「音乐时间」推进，而不是按帧。

## 核心内容

### 开局的六个步骤

`Assets/Game/Runtime/OpenSekai/App/Playback/PlayService.cs`（212 行）：

1. 校验曲库页还活着、`LiveLoading` 页面已注册（`IsLibraryAlive` + `TryGetScreenLayerData`）
2. 读曲包 → 检查 `score.json` / 音频存在 → `ChartFileReader.Read` → `chart.HasPlayableNotes`
3. `PlaySessionContext.Begin(SHA-256(score.json))` 记录本次会话的谱面哈希
4. `PackageAudio.RegisterAudioAsync` 解码音频
5. `LiveSettingsStore.CaptureSnapshot()` 取设置 → `RuntimeChartConverter.Convert` 生成运行时谱面 →
   `LegacyLiveBootAdapter.Create` 组装 `FreeLiveBootData`
6. 设置 `UserDataManager.FreeLiveBootData`、`PlayMode = SoloLive`，`PushUIScreen(LiveLoading)`，交给场景切换

任何一步失败都返回 `PlayStartStatus.Failed` + 中文原因，页面据此弹「加载失败」结果面板。

### 时钟

| 量 | 来源 | 单位 |
| --- | --- | --- |
| `currentGameTime` | `Time.realtimeSinceStartupAsDouble`（`BaseLiveController.cs:208`） | 秒 |
| 音乐时间 | 音频解码进度（`PackageAudio` / `AcbDecoder`），经 `fillerSec` 延迟后传入 | 秒 |
| 每帧推进 | `SoloLiveController` 在 `LateUpdate` 调 `liveLogic.OnUpdate(musicTime - fillerSec, currentGameTime)` | 秒 |

判定窗口本身是**以帧定义**的常量：`CalcFrameToTime(frame) = frame / 60`，JustPerfect 窗口 `1/60` 秒。
也就是说窗口换算依赖这条 60 fps 的基准，而不是当前渲染帧率。

### 输入时间回填

触摸事件自带的时间戳是「秒」，与当前帧的音乐时间不是同一时刻，必须回推：

```csharp
// Assets/Game/Runtime/Sekai/Core/Live/LiveLogic.cs
public static float ComputeInputMusicTime(double frameMusicTime, double frameGameTime, double inputTime)
```

规则：`deltaSeconds = inputTime - frameGameTime`；正向夹到 `0`（不能判到未来），负向夹到 `-0.25s`
（太旧的事件丢弃）；返回 `frameMusicTime + deltaSeconds`。

历史实现是 `(time - currentGameTime) * TargetFrameTime + currentFrameInfo.time`，把秒乘以 1/60 当帧用，
100 ms 的真实延迟会被算成约 1.7 ms。修复记录见 [重构日志 2026-11](../changelog/2026-11-refactor-log.md) 第 4 节。

### 计分

`Assets/Game/Runtime/Sekai/Core/Live/ScoreLogic.cs`：

- 每音符基分按累积摊分：第 i 个音符（0 基）拿 `round(1e6*(i+1)/n) - round(1e6*i/n)`
- 判定倍率：JustPerfect/Auto 1.0、Perfect 0.9、Great 0.7、Good 0.5
- 连击清零规则：`effectiveResult < NoteResult.Great` 时 `combo = 0`
- 评级线（`ScoreGaugeCalculator`）：S ≥ 0.9、A ≥ 0.75、B ≥ 0.6、C ≥ 0.45（占 1,000,000 的比例）

口径版本记在 `PlaySessionContext.RulesVersion`，当前 **2**。1 = 旧的 `floor(1e6/n)`，2 = 累积摊分。

### 结算与返回

`SoloLiveController`：

- `PreExit`：`TryAppendPlayHistory()` + `PublishPlayResultForLibrary()`（写入单槽 `PlayResultHandoff`）
- `OnExit`：`SceneManager.RequestScene(ReturnScene ?? Library)`
- 结果类型：完成 / 失败 / 中途退出 / 加载失败；中途退出走 `OnRetireByMySelf`，不写历史

### 暂停

曲库流程下 `ShowPauseDialog` 走 `LivePauseOverlay`（代码构建的 `ScreenSpaceOverlay` 画布），
提供继续 / 重开 / 退出到曲库。退出即「中途退出」结算。

## 示例

想验证「输入时间没算错」，用 100 ms 延迟的触摸事件：

```text
frameMusicTime = 12.4s, frameGameTime = 100.0s, inputTime = 99.9s
旧公式: (99.9 - 100.0) * (1/60) + 12.4 = 12.3983  ← 延迟被当成 1.7ms
新公式: 12.4 + (99.9 - 100.0)          = 12.3      ← 正确回退 100ms
```

## 常见问题与故障排查

- **自动模式连击一直是 0**：`NoteResult.Auto` 低于 `Great` 门槛，`UpdateCombo` 会清零。属既有行为。
- **判分比预期松/紧**：先确认改的是帧常量还是秒；窗口定义在 `CalcFrameToTime`。
- **打完不回曲库**：看 `FreeLiveBootData.ReturnScene` 是否为 `Library`；`PlayService` 只接受该值。

## 相关文档

- [计分修正 ADR](../decisions/0004-cumulative-scoring.md)
- [曲包与谱面数据](chart-package.md)
- [音频](audio.md)
- [打歌历史与结果](../guides/play-a-chart.md)
