# 判定窗口

本页记的是**当前实现**与**原版数据**的对照。改这里的数字等于改手感，提交时必须同时改本页。

## 数据表

判定窗口按**帧**配置（`Sekai.Live.LiveConfig`），`CalcFrameToTime(frame)` 固定按 60 fps 换算（`frame / 60f`），与实际帧率无关。八个数依次是 Perfect / Great / Good / Bad 的**早侧**与**晚侧**。

| 音符 | Perfect（早/晚） | 换算 | Great | Good | Bad |
| --- | --- | --- | --- | --- | --- |
| 普通单键 `Normal` | 2.5 / 2.5 | ±41.7 ms，宽 83.3 ms | 5 / 5 | 6.5 / 6.5 | 7.5 / 7.5 |
| 关键单键 `Normal_Critical` | 3.3 / 3.3 | ±55.0 ms，宽 110 ms | 4.5 / 4.5 | 6.5 / 6.5 | 7.5 / 7.5 |
| 普通划键 `Flick` | 2.5 / 2.5 | 宽 83.3 ms | 6.5 / 7.5 | 7 / 8 | 7.5 / 8.5 |
| 关键划键 `Flick_Critical` | 3.5 / 3.5 | ±58.3 ms，宽 116.7 ms | 6.5 / 7.5 | 7 / 8 | 7.5 / 8.5 |
| 长条结束 `Long_End` | 3.5 / 4 | 宽 125 ms | 6.5 / 8 | 7.5 / 8.5 | 7.5 / 8.5 |
| 摩擦系 `Friction*` | 由 `CopyJudgeFrame` 抄自 `Flick*` | — | — | — | — |

两条边界规则：早于 **125 ms**（7.5 帧）一律无效；晚于 **125 ms** 单键与长条起点 MISS，其它音符晚于 **141.7 ms**（8.5 帧）MISS。

## 与原版的对照

本表与 Project SEKAI 判定范围的逆向分析逐项吻合（83.3 / 110 / 116.7 / 125 / 125 / 141.7 全部对上），说明这些帧数是原版数据，不是估算值。测试 `Assets/Tests/EditMode/Live/JudgeFrameTableTests.cs` 把换算与窗口宽度钉死。

参考文章（第三方，未随仓库分发，放在 `docs/reference/external/`，该目录已被 `.gitignore` 忽略）：

1. 音游判定原理详解——从触摸屏幕到判定音符
2. 音符判定机制详解——单键、划键、长条
3. 音符判定范围详解——不同音符的不同判定区间

## 与原版不同之处（可在设置里切换）

判定时机有两种模式，曲库「设置 → 判定时机」切换，写入 `LiveSettingData.UseLegacyJudgeTiming`，下次开局生效：

| 模式 | 取值 | 行为 |
| --- | --- | --- |
| 触摸时刻（默认） | `false` | 用触摸事件自身的时间戳，延迟扣到 `[-250 ms, 0 ms]`，即 `LiveLogic.ComputeInputMusicTime` |
| 下一帧（旧式） | `true` | 复刻原反编译公式 `(touch.time - gameTime) × 1/60 + frameTime`，延迟被缩小 60 倍，等价于按当前帧判定 |

旧式不是「更接近原版」——它把秒当成了帧。保留它只是为了让习惯那种手感的人不用改代码。两种模式的数值差异由 `Assets/Tests/EditMode/Live/JudgeFrameTableTests.cs` 里的 `LegacyTimingShrinksLatencyByOneFrame` / `LegacyTimingDoesNotClampButSampledTimingDoes` 固定。

另外两处机制**已经实现**，数值来自 `Assets/Graphics/Resources/live/config/LiveBundleBuildData.asset`（原版配置数据），不是硬编码：

| 项 | 取值 | 用法 |
| --- | --- | --- |
| 单键轨道容差 `_normalNoteOffsetX` | 1.25 轨 | `JudgeLaneStart = laneStart − offset`、`JudgeLaneEnd = laneEnd + offset`，判定时看触摸轨道是否落在区间内 |
| 长条 / 划键容差 `_longNoteOffsetX` / `_flickNoteOffsetX` | 1.5 轨 | 同上，按 `LiveUtility.GetLaneOffset(category)` 选择 |
| 划键滑动距离 `_flickDistance` | 0.0833 英寸/帧 | 触摸位移先按 1/60 s 归一化（`touchDelta /= Time.deltaTime / 0.016667f`），再要求 `sqrMagnitude > _flickDistance²`；经 `Screen.dpi / 2.54` 换成像素 |
| 划键方向 | 点积 ≥ 0 | `requiredDirection` 为 `(-1,1)` / `(1,1)` / 上；`Vector2.Dot(touchDelta.normalized, requiredDirection.normalized) >= 0`（±90°，合计 180° 范围） |

与原版对照只剩一处数值口径：`0.0833 in/帧 × 60 = 5 in/s ≈ 12.7 cm/s`，而文章写的是「5 cm/s」，相差约 2.5 倍，像是单位表述差异（5 in/s vs 5 cm/s），**待确认**——若确定要按文章改，只需改 `_flickDistance` 一处，但那是手感变更。

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

判据（`LiveLogic.UpdateJudgmentNoteArray`）：能不能自己判，决定谁进判定列表。

```csharp
bool barAnswersInput = (note.HasJudgment && !note.IsDecoration) || note is FrictionHideLongNote;
if (barAnswersInput) _judgmentNoteList.Add(note);
if (!barAnswersInput && tailNote != null) _judgmentNoteList.Add(tailNote);
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

## 长条尾巴怎么松开

普通尾的判定属于**抬手**事件，判据是「松手发生在条内」：

- 松手的手指必须是这根条的**持握者**（`HoldingTouchId`），或者落点在条的车道范围内；
- 车道范围用的是 `JudgeLaneStart/JudgeLaneEnd`，即 `lane ± JudgeTuning.LongLaneOffset`（**左右各 1.5 轨**），与单键的 1.25 轨不同；
- 任何**别的手指**在别处抬起，都不构成本条条的松手，也不许判它的尾。

回归用例：`Assets/Tests/EditMode/Live/TailReleaseLaneTests.cs`。

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
