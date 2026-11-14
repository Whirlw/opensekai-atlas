# 判定窗口

> 本页只列**数值**。判定权归属、通道分工、长条四段结构与不变量见[判定规则设计](../architecture/judging-design.md)。

本页记的是**当前实现的判定窗口**。改这里的数字等于改手感，提交时必须同时改本页。

## 数据表

判定窗口按**帧**配置（`Sekai.Live.LiveConfig`），`CalcFrameToTime(frame)` 固定按 60 fps 换算（`frame / 60f`），与实际帧率无关。八个数依次是 Perfect / Great / Good / Bad 的**早侧**与**晚侧**。

| 音符 | Perfect（早/晚） | 换算 | Great | Good | Bad |
| --- | --- | --- | --- | --- | --- |
| 普通单键 `Normal` | 2.5 / 2.5 | ±41.7 ms，宽 83.3 ms | 5 / 5 | 6.5 / 6.5 | 7.5 / 7.5 |
| 关键单键 `Normal_Critical` | 3.3 / 3.3 | ±55.0 ms，宽 110 ms | 4.5 / 4.5 | 6.5 / 6.5 | 7.5 / 7.5 |
| 普通划键 `Flick` | 2.5 / 2.5 | 宽 83.3 ms | 6.5 / 7.5 | 7 / 8 | 7.5 / 8.5 |
| 关键划键 `Flick_Critical` | 3.5 / 3.5 | ±58.3 ms，宽 116.7 ms | 6.5 / 7.5 | 7 / 8 | 7.5 / 8.5 |
| 长条结束 `Long_End` | 3.5 / 4 | 宽 125 ms | 6.5 / 8 | 7.5 / 8.5 | 7.5 / 8.5 |
| `Friction` / `Friction_Flick` | 复制对应的 `Flick` 表 | — | — | — | — |
| `Friction_Long` / `Friction_Long_End` | 分别复制对应的 `Long` / `Long_End` 表 | — | — | — | — |

`IsJudgmentTiming` 的外边界是开区间。单键和普通长条头的输入窗口为早晚各 **125 ms**，普通划键的晚界为 **141.7 ms**。实际发布 Miss 的时机见[判定规则设计](../architecture/judging-design.md#五判定窗口)，条内尾部评分见下文。

单键和普通长条头先按触摸样本时间处理本帧输入，再按当前音乐时间结算超时。摩擦和摩擦划键分别使用 `Friction` 和 `Friction_Flick` 窗口，独立键与条尾使用同一套规则。

## 数据来源与测试

这八组帧数与项目早期整理的判定分析逐项吻合（83.3 / 110 / 116.7 / 125 / 125 / 141.7 全部对上），数值已固化在 `LiveConfig` 与测试里。早期分析资料未随仓库分发。测试 `Assets/Tests/EditMode/Live/JudgeFrameTableTests.cs` 把换算与窗口宽度钉死。

## 判定时机的两种模式（可在设置里切换）

判定时机有两种模式，曲库「设置 → 判定时机」切换，写入 `LiveSettingData.UseLegacyJudgeTiming`，下次开局生效：

| 模式 | 取值 | 行为 |
| --- | --- | --- |
| 触摸时刻（默认） | `false` | 用触摸事件自身的时间戳，映射偏移限制在 `[-250 ms, 0 ms]`，见 `LiveLogic.ResolveInputMusicTime` |
| 下一帧（旧式） | `true` | 沿用旧式公式 `(touch.time - gameTime) × 1/60 + frameTime`，延迟被缩小 60 倍，等价于按当前帧判定 |

旧式把秒当成了帧，不要把它当作更精确的模式。保留它只是为了让习惯那种手感的人不用改代码。两种模式的数值差异由 `Assets/Tests/EditMode/Live/JudgeFrameTableTests.cs` 里的 `LegacyTimingShrinksLatencyByOneFrame` / `LegacyTimingDoesNotClampButSampledTimingDoes` 固定。

另外两处机制**已经实现**，数值来自 `Assets/Graphics/Resources/live/config/LiveBundleBuildData.asset`（配置资产），不是硬编码：

| 项 | 取值 | 用法 |
| --- | --- | --- |
| 单键轨道容差 `_normalNoteOffsetX` | 1.25 轨 | `JudgeLaneStart = laneStart − offset`、`JudgeLaneEnd = laneEnd + offset`，判定时看触摸轨道是否落在区间内 |
| 长条 / 划键容差 `_longNoteOffsetX` / `_flickNoteOffsetX` | 1.5 轨 | 同上，按 `LiveUtility.GetLaneOffset(category)` 选择 |
| 划键滑动距离 `_flickDistance` | 0.0328 英寸/帧（**5 cm/s**） | 触摸位移先按 1/60 s 归一化，再要求 `sqrMagnitude > _flickDistance²`；经 `Screen.dpi / 2.54` 换成像素 |
| 划键方向 | 点积 ≥ 0 | `requiredDirection` 为 `(-1,1)` / `(1,1)` / 上；`Vector2.Dot(touchDelta.normalized, requiredDirection.normalized) >= 0`（±90°，合计 180° 范围） |

数值口径已定：划键距离现在是 **5 cm/s**（`JudgeTuning.ShippedFlickDistanceInches = 5/2.54/60`，资产 `_flickDistance = 0.032808` 英寸/帧），即先前分析里的「5 cm/s」；资产里曾经是 5 in/s ≈ 12.7 cm/s。

判定区间与滑动的分工：**时间**决定 Perfect/Great/Good/Bad，**轨道与滑动**决定这个音符收不收得到输入（`FlickNote.JudgmentFlick` 要求 `distance && trueDirection` 同时成立）。

## 装饰条（Guide）怎么判

谱面里「只画一条轨道、末端才是真音符」的长条不是靠 `isDecoration` 标志识别的——`score.json` 里没有那个字段。它们是 `category 9 / noteBaseType 10`，转换后是 `GuideNote`：`HasJudgment == false`、`IsDecoration == false`。

结论只有一条：**装饰条不判、也不接输入；它末端的音符是真音符，以独立候选身份参与判定。**

| 末端音符 | 末端行为 | 玩家动作 |
| --- | --- | --- |
| `NormalNote` | 正常按下判定 | 按下（`Began`） |
| `FlickNote` | 自判划键 | 一帧内净位移达 `_flickDistance` 且方向正确 |
| `FrictionFlickNote` | 自判划键 | 同上 |
| `FrictionNote` | 自判摩擦 | 按住在轨道上蹭 |
| `FrictionHideLongNote` | 无判定（隐藏点由父条驱动） | 无 |

判据（`LiveCandidatePipeline.Register`）：能不能自己判，决定谁进判定列表。

```csharp
bool barAnswersInput = (note.HasJudgment && !note.IsDecoration) || note is FrictionHideLongNote;
if (barAnswersInput) judgmentNotes.Add(note);
if (!barAnswersInput && tailNote != null) judgmentNotes.Add(tailNote);
```

真实谱面驱动用例：`Assets/Tests/EditMode/Live/DecorationTailFromChartTests.cs`（夹具 `Tools/fixtures/deco-tail-test10/score.json`）。设计与教训见 [判定通道与解耦收尾](../operations/judgment-channel-brief.md) §5.1 与 §11。

## 摩擦与隐藏（Friction / FrictionHide）

这两类在谱面里是两个**不同的** `category`，运行时也必须保持不同——2026-11-07 之前转换器把它们合并成同一个「无判定载体」，代价是摩擦既没有判定、也没有声音、也没有尾键（见 [判定通道与解耦收尾](../operations/judgment-channel-brief.md) §5.3）。

| 谱面类别 | 运行时类型 | 有判定 | 有声音 | 说明 |
| --- | --- | --- | --- | --- |
| `FrictionLong` | `FrictionLongNote` | 是 | 是（按住 `se_live_trace` 循环音） | 能擦的条，起手与尾巴都要能听出来 |
| `FrictionHideLong` | `FrictionHideLongNote` | 否 | 否 | 隐藏点的载体，只负责收持握输入驱动内部标记 |
| `Friction` | `FrictionNote` | 是 | 是（打到就一声 `se_live_trace`） | 单音符或长条末端 |
| `FrictionHide` | `FrictionHideNote` | 否 | 否 | 半拍隐藏标记 |

一次性的打击音只对**有判定的**摩擦音符成立；载体上的摩擦是全静音的，`TapEffectView` 对 `FrictionHide*` 类别显式 `return`。

## 长条尾巴怎么判

普通尾在抬手阶段结算。

- 每根抬手的手指，收集「抬手落在**尾键自己的判定车道内**、且尾在判定时间窗口内」的条；
- 一根抬手手指最多结束**一条**：取尾时间最早的，同时间取尾键车道中心最近的；
- 车道范围用尾键的 `JudgeLaneStart/JudgeLaneEnd`，普通尾与单键同为 1.25 轨容差。
- 用当前帧音乐时间减去尾键时间走 `LongEndJudgement`。Perfect、Great、Good 的早侧边界为 58.3 / 108.3 / 125 ms，晚侧为 66.7 / 133.3 / 141.7 ms，包含边界。
- 手指滑出车道会触发条身丢手宽限。

划键尾在常驻阶段接收按触摸 ID 路由的位移。快速划出车道时，结束样本可用同一触摸的上一采样帧车道归属。早划在触发手指离轨、结束或到达尾时间后的第三个游戏帧评分。尾时间及之后的有效划动可立即评分。

摩擦尾和摩擦划键尾使用独立音符的类型和视图，并沿用长条的谱面流速倍率。条身在几何终点结束，尾键持续到自身命中或超时 Miss。

普通尾和划键尾也可在条身到达几何终点后继续显示，直到命中或超时 Miss。普通尾仍可在晚窗内抬手，划键尾的三帧倒计时可跨越条身终点。隐藏尾只有几何锚点。

条身丢手进入 `Last`（灰），宽限按 `HoldGraceSeconds = 3/60 s ≈ 50 ms` 比较音乐时间。期间有手指回到车道则恢复按住态，否则进入 `Release`。

回归用例：`Assets/Tests/EditMode/Live/TailReleaseLaneTests.cs`、`Assets/Tests/EditMode/Live/HoldQuotaTests.cs`。

> 头、体、尾的解耦仍未收尾，待办见 [`operations/tail-classes.md`](../operations/tail-classes.md) §十二/§十三。

## 谁会黏在判定线上

两条规则要分开，混在一起就会两边都错：

| 对象 | 普通条（`Category == Long`） | 摩擦 / Guide 条 |
|---|---|---|
| **条的身体**（`NoteLineView` 网格） | 止于判定线 | **也止于判定线**（身体永远从这条线开始，不分类型） |
| **条头那颗键**（`LongNoteView`） | 停在线上下不来（那是它的位置） | 没人碰过时**流下判定线**，读作「漏了，过去了」 |

判据是 `LongNote.KeepsHeadOnTheLine`（= `IsOrdinaryHead`），只被条头键消费（`LongNoteView` → `NoteLineProgress.BarKey`）。身体走 `NoteLineProgress.Body`，**永远**夹在 1，不做类型判断。

身体不能放行的原因不只是观感：`startProgress` 一旦超过 1，`splitCount = ceil((start - end) * 100)` 会超过网格缓冲 `SplitMeshBuffer` 的 `SplitMeshCount + 10 = 110` 格，循环在中途停住，**尾键之前那一截就画不出来**。

回归用例 `Assets/Tests/EditMode/Live/NoteLineProgressTests.cs`（断言数值，不是断言意图）。

## 相关文档

- [谱面与曲包](../architecture/chart-package.md)
- [Live 运行时](../architecture/live-runtime.md)
- [项目索引](../INDEX.md)
