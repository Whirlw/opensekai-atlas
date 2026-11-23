# 打歌运行时

> 目的：说明从点击「手动/自动」到返回曲库或服务器之间，代码里发生了什么，尤其是时间与计分这两块容易改错的地方。
> 适用读者：开发者（改判定、改时间轴、改结算）。
> 前置条件：读过 [曲包与谱面数据](chart-package.md)。
> 最后更新：2026-11-20

## 概述

曲库和服务器同属 `Library` 场景。两者调用 `PlayService.StartAsync` 准备数据，之后在 `Core` 场景由 `SoloLiveController` 驱动演奏。完成或退出时发布结果，切回 `Library` 场景并恢复开局来源。判定和计分由 `LiveLogic` 与 `ScoreLogic` 按演奏时钟推进。

## 核心内容

### 开局与返回

1. `PlayService` 检查场景、加载界面及请求是否有效。
2. 读取已安装曲包的数据，或使用 Sonolus 提供的内存曲包。通过 `RuntimeChartLoader` 或 `NextRushChartLoader` 生成运行时谱面。
3. 记录谱面哈希，注册磁盘文件或内存字节中的音频，使用设置快照组装 `PlaySession`。
4. 通过 `LegacyLiveBootAdapter` 发布 `FreeLiveBootData` 并进入 `LiveLoading`。已安装的曲包会携带写入本地成绩所需的曲包键。
5. 结算或取消回到发起页面。服务器开局保留服务器详情，支持重开或保存临时曲包。

取消或失败会返回对应的状态与提示。 [服务器缓存与安装](server-screen.md)

### 开场、跳过和结算

`LiveTimelineSpan` 统一计算手动和自动演奏时序。时间从曲目信息页退场起算，通常在可听音乐起点和首键中较早的时刻前 2 秒开始。首键入场需要更长时间时会再提前。前奏可跳到首键前 2 秒。尾奏从末键后 1 秒可跳过，并立即触发结算特效。自然结算在可听音乐终点和末键中较晚的时刻之后 1 秒触发。按钮仅在对应的可跳过区间内出现。 [音乐偏移](native-next-rush.md#音乐偏移与演奏时序)

### 时钟

| 量 | 来源 | 单位 |
| --- | --- | --- |
| `currentGameTime` | `Time.realtimeSinceStartupAsDouble`（`BaseLiveController.cs:208`） | 秒 |
| 音乐时间 | 音频解码进度（`PackageAudio` / `AcbDecoder`），经 `fillerSec` 延迟后传入 | 秒 |
| 每帧推进 | `SoloLiveController` 在 `LateUpdate` 调 `liveLogic.OnUpdate(musicTime - fillerSec, currentGameTime)` | 秒 |

判定窗口本身是**以帧定义**的常量：`CalcFrameToTime(frame) = frame / 60`，Fast/Late 的正中区间为 ±1/120 秒。
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
- 判定倍率：Perfect/Auto 1.0、Great 0.7、Good 0.5
- 连击清零规则：`effectiveResult < NoteResult.Great` 时 `combo = 0`
- 百万制评级线为 C 900,000、B 950,000、A 975,000、S 990,000。左上角分数条在各线之间线性填充，到达评级线时分别填至 45%、60%、75%、90%，在 1,000,000 分填满。

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
