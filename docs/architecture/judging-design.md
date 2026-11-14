# 判定规则设计

> 目的：把「一个音符凭什么被判定、由谁判、什么时候判、判错会怎样」写成一份可以对照代码验收的完整规则。
> 适用读者：改判定、改音符视觉、排查「明明按了却没判」的人。
> 前置条件：读过[打歌运行时](live-runtime.md)、[判定窗口](../reference/judging.md)。
> 最后更新：2026-11-13

本文只描述**现行实现**。数值的唯一来源是 `LiveConfig`（判定表）与 `LiveBundleBuildData.asset`（轨道容差、划键距离）；本文与它们不一致时以代码为准，并应当把本页改掉。

---

## 一、总原则

1. **一个音符只被判定一次。** 每个音符自带唯一的结果槽：`NoteBase.JudgeInfo`（`Result` / `Description`）。`LongNote` 的头部与尾部各有独立槽位，同一条长条的「头」与「尾」是两次独立判定。
2. **判定由三条通道分工完成。** 按下（press）、抬起（release）、常驻（resident）各自只回答一类问题，互不代劳：
    - **按下通道**：这次按下判不判得了这个**瞬间键**（单键、划键的按下部分、普通长条的头）。一次按下最多结算一个音符。
    - **抬起通道**：只判**长条的普通尾**（末端是普通音符，`ReleaseTail`）。一指一帧只了结一条，窄车道优先。
    - **常驻通道**：不需要"事件"的持续状态与到期——条身是否被按住、条内 combo 点、自己判定的尾、头与尾的自有窗口、超时 Miss，以及划键的多帧滑动累计（一次按下说不完，见第八节）。
    一个音符只归其中一条通道回答（分工见第二节，细则见第七、八节）。
3. **判定的输入是「触摸样本」+「结果表」。** 位置决定这个音符**收不收得到**输入，时间决定**判成什么**，两者不互相替代。
4. **视觉与判定解耦。** 头/尾在屏幕上停在哪、何时消失，都由表现层决定（`LongNoteView`、`LivePresentationBridge.TickTailKeys`），判定不看这些状态；反过来，判定也不改变几何。
5. **没有判定权的音符不判。** `HasJudgment == false` 的音符（隐藏连接点、隐藏摩擦点、装饰条）永远不产生结果，也不会断连击。

---

## 二、帧内时序

一帧按固定顺序驱动，视图永远在最后：`LiveLogic.OnUpdate` 推进时钟并走音符（音符状态），`LiveLogic.OnInput` 收触摸并跑判定（输入），视图在这一帧的判定全部落定之后才前进一次。
`OnUpdate` 自己不再刷新视图——曾经它刷新一次、输入趟再刷新一次，同一帧里视图被推进两遍，画出来的可能是两个状态拼在一起。

```
OnUpdate(scoreInfoTime, gameTime)
  clock.Sample ........................ 得到本帧 MusicScoreInfo
  NoteFrame.UpdateNotes()
      Notes.TickHeads(now) ............ 头的自有窗口：到期自己 Miss
      register 未结算的头候选 ......... 头的候选只要头还开着就一直注册（不看条是否已结束）
      Notes.TickTails(frame) .......... 自判定的尾：自己的时钟拍一次
      WalkNotes(ScanNote) ............. 走音符：生成/推进/生成视图
      WalkHighSpeedNotes(...)

OnInput()
  Input.CollectTouches() .............. 原始触摸 → InputTmp（含车道解析）
  Scheduler.Run()
      1 按下通道 ....................... CollectCandidates → SortPressCandidates → EvaluateJudgment
                                         只收 Began；一根手指一帧只花一次按下
      2 抬起通道 ....................... JudgeLifts：只收 Ended / Canceled
                                         只判 ReleaseTail，一帧一指只了结一条
      3 常驻通道 ....................... JudgeDueComboPoints → DriveIndependentNotes → RefreshSustainedHolds
                                         条内点、条身、自判尾、按住刷新、超时
      4 EndFrame() ..................... 忘掉结束的触摸，清空本帧输入
      5 TickViews() .................... 视图与尾 key 回收：本帧最后一步
```

三个通道各自从本帧的原始样本里取自己认得的相位（按下取 `Began`、抬起取 `Ended`/`Canceled`），先判完再交给下一个；常驻通道拿剩下的一切。抬起的候选在它自己的通道里选好（`_liftTargets` 带帧号），常驻通道只读这份结果，不会再把同一根手指的抬起判一次。

自动播放没有输入趟：`LiveLogic.OnAutoInput` 在自动判定之后调用同一个 `TickViews`，视图同样在最后。

**三条通道与上面五步的对应关系**（一个音符只由其中一条回答）：

| 通道 | 落在哪一步 | 回答的问题 | 典型音符 |
| --- | --- | --- | --- |
| 按下 press | 1 | 这次按下能不能判掉一个瞬间键 | 单键、普通长条的头 |
| 抬起 release | 2 | 只判长条的**普通尾**（末端是普通音符） | `ReleaseTail` |
| 常驻 resident | 3 | 每帧持续的状态与到期 | 条身、内部点、摩擦/隐藏尾、头尾的自有窗口 |

---

## 三、输入层

### 3.1 触摸 → 车道

| 步骤 | 实现 | 规则 |
| --- | --- | --- |
| 归一化 | `TouchDeduplication` | 同一 `touchId` 的时间戳重复只处理一次；时间倒退丢弃；`Ended` 之后同 id 不再处理 |
| 坐标系 | `InputSystemTouchSource` | `Camera.ScreenToWorldPoint` 得到世界坐标 |
| 车道解析 | `TouchLane.Resolve` | 返回**小数**车道（2.5 = 2、3 之间）；超出外侧容差返回 `-1`（Outside）。只看 x，不看 y |
| y 轴门（唯一一道） | `TouchLane.IsAboveTheLine` | 只在触摸**出生**（`Began` 样本）时问一次：高出判定线超过 `_judgmentOffsetY` 的触摸被记进 `_touchesAboveTheLine`，此后它所有样本都算 Outside。落在线上的手指后来划到线以上，仍然是有效输入 |
| 判定时机 | `LiveLogic.ResolveInputMusicTime` | 默认「触摸时刻」模式：`touch.time` 转音乐时间，晚到量限制在 `[-250 ms, 0]`；旧式模式为逐帧近似 |

`-1` 车道的触摸不会进入任何候选：这是唯一一种「按了但什么都没发生、日志里也没有 grab」的路径，诊断见 `rawTouch` trace。

**y 轴只在输入口存在一次。** 原始样本进来时算好车道，此后所有通道（按下 / 抬起 / 常驻）都读同一个 `InputTmp.Lane`，`ConvertLaneBy` 只是把这条车道交出去，不再为某个音符重算——曾经的第二个 y 门只给「向左的划键」开，导致左划能在判定线以上继续读位移、右划不行，左右不对称。

### 3.2 轨道容差

`JudgeLaneStart/End = laneStart − offset .. laneEnd + offset`，`offset` 来自配置资产，按类别选：

| 类别 | 容差 |
| --- | --- |
| 单键（`Normal`） | 1.25 轨 |
| 长条（`Long` / `FrictionLong` / `FrictionHideLong`） | 1.5 轨 |
| 划键（`Flick` / `FrictionFlick`） | 1.5 轨 |

**长条的「头」沿用长条自身的 1.5**（这是刻意的：头与条同一条轨道）。这条容差曾被误改成单键的 1.25，导致短条头部抢不到/抢多了邻近车道的按下，已回退。

---

## 四、音符类型与判定权

| 运行时类型 | `Category` 语义 | 有判定 | 由谁判 | 输入 |
| --- | --- | --- | --- | --- |
| `NormalNote` | 单键 | 是 | 按下通道 | 按下（`Began`） |
| `FlickNote` | 划键 | 是 | 常驻通道（多帧累计后自判） | 每帧净位移 + 方向 |
| `FrictionNote` | 摩擦 | 是 | 常驻通道 | 按住并在轨道上蹭 |
| `FrictionFlickNote` | 摩擦划键 | 是 | 常驻通道 | 按住 + 位移 + 方向 |
| `LongNote`（普通条） | 长条 | 头/尾各一次 | 头：按下通道；尾：抬手或划键 | 见第六节 |
| `FrictionLongNote` | 摩擦长条 | 头/尾 | 头：常驻；尾：自判 | 按住 |
| `FrictionHideLongNote` | 隐藏摩擦条 | 尾 | 尾：自判 | — |
| `LongHoldCombo` | 条内 combo 点 | 是 | 常驻（`JudgeDueComboPoints`） | 任意手指在该轨道 |
| `ConnectionNote` | 连接点 | 否 | — | — |
| `HiddenConnectionNote` / `FrictionHideNote` | 隐藏点 | 否 | — | — |
| `GuideNote` | 装饰条 | 否 | — | — |
| `GuideEndNote` | 装饰条末端（**真音符**） | 是 | 独立候选，走自己的通道 | 按类型 |

要点：

- **装饰条不判、也不接输入**；它末端的音符是真音符，以独立候选身份参与判定。
- **隐藏点不判**：`HasJudgment == false`，它只提供几何与计时锚点。

---

## 五、判定窗口

判定表按**帧**配置，`CalcFrameToTime(frame) = frame / 60f` 固定 60 fps 换算，与实际帧率无关。八个数是 Perfect/Great/Good/Bad 的早侧与晚侧。

| 音符 | Perfect（早/晚） | Great | Good | Bad | 早界 | 晚界 |
| --- | --- | --- | --- | --- | --- | --- |
| `Normal` | 2.5 / 2.5 | 5 / 5 | 6.5 / 6.5 | 7.5 / 7.5 | 125 ms | 125 ms |
| `Normal_Critical` | 3.3 / 3.3 | 4.5 / 4.5 | 6.5 / 6.5 | 7.5 / 7.5 | 125 ms | 125 ms |
| `Flick` | 2.5 / 2.5 | 6.5 / 7.5 | 7 / 8 | 7.5 / 8.5 | 125 ms | 141.7 ms |
| `Flick_Critical` | 3.5 / 3.5 | 6.5 / 7.5 | 7 / 8 | 7.5 / 8.5 | 125 ms | 141.7 ms |
| `Long`（头） | 2.5 / 2.5 | 5 / 5 | 6.5 / 6.5 | 7.5 / 7.5 | 125 ms | 125 ms |
| `Long_End`（尾） | 3.5 / 4 | 6.5 / 8 | 7.5 / 8.5 | 7.5 / 8.5 | 125 ms | 141.7 ms |
| `Friction*` | 抄自 `Flick*`（`CopyJudgeFrame`） | — | — | — | — | — |

**时间偏移的符号**：全工程统一用 `note.time − touch.time`（正 = 早）。`IsJudgmentTiming(type, offset)` 判定 `−晚界 ≤ offset ≤ 早界`。

> 头部的偏移曾经写成 `touch.time − note.time`（符号反了），于是「早一点」被当成「晚一点」、「晚一点」又落在早侧边界外，表现为早/晚都判不上。现在头部与 `NormalNote` 用同一个符号、同一个门。

**Miss 的时机**：

| 情形 | 何时判 Miss |
| --- | --- |
| 单键 / 划键 / 摩擦 | 越过该类型的晚界后先进入 `Last`，随后 Done 并发布 Miss（发布时刻略晚于窗口，日志里能看到 `t` 与 `now` 差 0.18 s 左右） |
| 长条的**头** | `LongNoteHead.SettleExpiredHead`：`now − head.time > JudgeTime`（= 长条头的晚界 125 ms） |
| 长条的**尾**（抬手/划键） | `LongNoteTail.ExpireTail` 判 `IsPastTailWindow` 之后 `FailTail()` |
| **自己判定的尾** | 它自己的时钟：会话 `TickTails` 每帧 `Excute`，超时由 `NoteBase` 判 Miss |
| 条内 combo 点 | 它自己的窗口结束（`JudgeDueComboPoints` 不再有机会） |

---

## 六、长条的四段结构

长条不是「一个音符」，是四段各有归属的东西：

| 段 | 实体 | 有判定 | 谁判 | 判定窗口 |
| --- | --- | --- | --- | --- |
| **头** | `LongNoteHead` + 候选 `LongNoteHeadNote` | 仅**普通头**（`IsOrdinaryHead && HasJudgment && !IsSkip`） | 按下通道 | `Long` 表，±125 ms |
| **身** | `LongNote.Body`（`LongNoteBody`） | 无 | — | 只负责「是否被按住」与刷新 |
| **内部点** | `LongHoldCombo` | 有 | 常驻（`JudgeDueComboPoints`） | 自己的窗口 |
| **尾** | `LongNoteTail` + 末端音符 | 取决于末端音符 | 见下表 | `Long_End` / 自判 |

### 6.1 头的规则（最重要的一条）

- **头是一个独立的瞬间判定键**，和单键同构：按下、窗口内、按表评级。
- 头**不共享条的生命周期**：条已经结束、已被回收、已经灰掉，都不影响头在它自己窗口内的判定。头只看两件事：自己有没有结果（`IsSettled`）、这次触摸是否在轨道与窗口内。
- 头的候选**从头开着就一直注册**（`LiveNoteFrame` 用 `Notes.PendingHeads` 注册，而不是靠音符遍历）。遍历只走「还在播放的条」，而超短条（1 tick）在头之后几毫秒就结束——靠遍历注册会让头在剩余 125 ms 窗口里彻底失去候选，之后按下的那一下必然 Miss。
- 单纯按住不算按下：头只认 `Began`。手指提前按在轨道上、一直不放，不会判头，也不会扣血；头到期由自己发布 Miss。
- 头**不写条的结果**：头的结论记在 `LongNoteHead` 自己的槽位与它的 identity 上，条身的 `Result` 是另一件事。

### 6.2 尾的行为选择

`TailBehaviours.For(tail)` 一次性选出行为，四种：

| 末端音符 | 行为 | 判定方式 | 条要不要等它 |
| --- | --- | --- | --- |
| `NormalNote`（普通尾） | `ReleaseTail` | **抬手**结算：`JudgeTailAsRelease` 用 `Long_End` 表评分 | 不等，抬手即结束 |
| `FlickNote`（划键尾） | `SwipeTail` | 条把滑动转交末端（「长条传递」）：窗口内检测到 `≥ 5 cm/s` 的滑动后，在「滑出车道 / 抬手 / 到达尾时间」三者最早者之后 **50 ms** 结算 | 不等 |
| `FrictionNote` / `FrictionFlickNote` / `HiddenConnectionNote` / `FrictionHideNote` | `SelfJudgedTail` | **完全由末端自己判**：会话每帧 tick 它，它按自己的规则结算或超时 Miss | 不等；条只读结论，不写结论 |

尾的三条不变量：

1. **条不替自判定的尾写结果**；条只 `DetachStandaloneTail` 把自己结束掉。
2. **自判定的尾有自己的时钟**（`LiveNoteSession.TickTails`），条先结束也不会孤儿化它。
3. **无判定的末端不产生 Miss**（隐藏点）：条的结束不能借它的名义记一次断连击。

### 6.3 条的结束

条结束的**唯一条件**是它自己的时间到了（`ExpireTail` → `IsPastTailWindow`），外加一条兜底：

> **条不在自己头的窗口关闭前离开。** 短条（1 tick）在 `+2 ms` 就到了尾部窗口，但它的头还有 125 ms 的窗口；此时条必须继续存活，否则头没有对象可判。

条结束的方式有三种，都必须不产生额外结果：`FinishSilently`（末端无判定）、`DetachStandaloneTail`（末端自判定）、`FailTail`（末端需要条来判而没判成）。

---

## 七、按下通道

### 7.1 谁能进候选

`LiveCandidatePipeline.IsPressStep` 是唯一入口，只有两类：

1. `NormalNote` 且 `Category == Normal`（单键）；
2. `LongNoteHeadNote` 且 `IsAnswerable`（可答且**未结算**的头）。

**划键不在其中。** 一次按下说不完一次划动：`FlickNote` 要跨帧累计位移与方向，所以它由常驻通道逐帧驱动（第八节），不占按下通道的单槽。这条曾经写成「`FlickNote` 且尚未完成首次动作」，但支撑它的 `NoteBase.FinishFirstAction` 恒为 true、没有任何 override，那个分支从未成立——按下通道从来没有收过划键。

**条身、内部点、隐藏点、装饰条永远不进入按下候选。** 曾经的 `PickUp` / `TryAddConnectionCandidate` 两条路径会把整条 `LongNote` 塞进候选，已删除——它们会让一次按下被「一条已经判完的条」吃掉。

### 7.2 一个音符的按下准入

| 检查 | 规则 |
| --- | --- |
| 相位 | 必须是 `Began`（按住、滑动都不算「按下」） |
| 车道 | 触摸车道落在该音符的 `JudgeLaneStart..JudgeLaneEnd` |
| 时间 | `IsJudgmentTiming(type, note.time − touch.time)` |
| 单槽 | 一根手指一帧只有一个候选（见 7.3） |
| 一次触摸一个音符 | 同一 `touchId` 已经判过某个音符时，另一个音符要再判它必须满足「评级差于 Great」或「距上次 ≥ 1/6 s」 |

### 7.3 单槽与归属

`SortPressCandidates` 做两件事：

1. **共享归属**：同一条音符被两根手指同时够到时，给更近的那根；距离相同给先来的。
2. **单槽**：每根手指只保留一个候选，按「时间最早」排序后取第一个。

实际判定循环（`EvaluateJudgment` → `JudgmentSchedule.Run`）的语义是：**按顺序让每个候选都有机会，遇到第一个真正「消费」了这次按下（产生结果或进入 Done）的才停**。因此单槽只是上限，真正的「一次按下判一个」由 consume 规则保证。

### 7.4 抬起的归属

抬起通道的作用面很窄：**它只判长条的普通尾**，也就是末端音符是普通音符、由 `ReleaseTail` 负责的那种。划键尾走「条把滑动转交末端」，摩擦/摩擦划键/隐藏尾由末端自己判——这三类都不经过抬起通道，抬手对它们只意味着"手指离开了条"（`NotifyLaneRelease`），不构成一次判定。

`LiveHoldCoordinator` 自己维护「一指一帧只了结一条条」：

- 只处理 `Ended` / `Canceled` 的触摸；
- `PickLiftTarget` **窄车道优先**，加宽范围只作兜底：抬手属于手指真正站着的那条条，不因为邻居范围更宽就判给邻居；
- 抬手的评分走 `Long_End` 表；早于窗口的抬手不评分（`JudgeTailAsRelease` 自己检查窗口），只是把条置为释放态。

---

## 八、常驻通道

常驻通道每帧驱动**所有**条与自判定尾，与按下无关。它负责：

| 职责 | 实现 |
| --- | --- |
| 条身是否被按住 | `LongNoteBody` + `RecordHoldingInput` |
| 条内 combo 点结算 | `JudgeDueComboPoints`：点到期时，任意落在其轨道内的手指都能结算它；两条重叠的点各判各的 |
| 长条结束（超时） | `LongNoteTail.ExpireTail` |
| 自判定尾的时钟 | `LiveNoteSession.TickTails` |
| 头的自有窗口 | `LongNoteSession.TickHeads` |

**越界手指不入条**：`LongNote.DriveJudgmentFrame` 先检查车道，越界直接拒绝（`grabRejected` trace）。这条守卫是从真机日志里量出来的——没有它时约 86% 的 `grab` 是越界手指，条被不属于它的手指按住，结尾也会被别处的抬手判掉。

---

## 九、结果发布与计分

结果槽写入后，`NoteBase.PublishResult()` 依次触发：

```
onJudgment → onUpdateCombo → onUpdateScore → onDamage
```

| 约定 | 说明 |
| --- | --- |
| 只发布一次 | `State = Done` 的转换只接受一次；重复结束被吞掉 |
| 头用 identity 计分 | 普通头借用条作为视觉对象，但结果记在头的 identity 上，条的 `Result` 不参与头的计分 |
| 装饰 / 跳过不发布 | `IsSkip`（内部 combo 点）与 `IsDecoration` 的音符不发连击与分数 |
| Miss 也发 combo | 断连击来自「结果发布」，不是来自视觉效果；谁发布结果谁断连击 |

---

## 十、表现层的不变量（判定不看它，但它们不能反噬判定）

| 不变量 | 位置 |
| --- | --- |
| 头的 key 停不停在判定线，只由 `KeepsHeadOnTheLine` 决定（普通头停、摩擦头与 guide 走） | `LongNoteView` |
| 条的 key 在「条结束」或「末端标准时间到」时立即消失 | `LongNoteView.Move` |
| 尾的 key 在「自身出结果」或「自身时间到」时立即消失，不再滑出 | `LivePresentationBridge.TickTailKeys` |
| 条结束不回收尾的贴图所有权：尾自己释放自己的视图（无判定的尾没有贴图） | `NoteBase.OnUnSpawnNote` |
| 重开一局必须清空 tick 列表（头/尾）与遍历游标 | `LiveNoteSession.Reset` |

---

## 十一、自动播放

`IsAuto` 时判定不经过输入：`LiveTimeline.AdvanceAutoJudgment` 按时间给音符写 `Auto` 结果，并跳过装饰。自动播放走的是同一套结果槽与发布回调，因此计分路径与手动一致。

---

## 十二、可以写成断言的不变量

1. 一个音符的结果槽只会从 `None` 变成某个具体结果一次。
2. 一根手指一帧最多消费一次按下。
3. 头的结果与条的状态无关：条 Done 之后、头窗口内按下仍然判得上。
4. 自判定尾的结果与条的状态无关：条 Done 之后尾仍然按时钟结算或 Miss。
5. 条不早于自己头的窗口结束。
6. 越界手指不能驱动条、不能判头、不能被 hold 记录。
7. 没有判定的音符永远不会产生结果。
8. 任何「按了但没判」都必须能在日志里找到一次 `rawTouch`，或一次落在 `-1` 车道的记录。

---

## 十三、不覆盖范围

- **计分公式**（权重、gauge、rank）不在本文范围，见 `docs/decisions/0004-cumulative-scoring.md`。
- **谱面转换**（`score.json` → 运行时音符）见 `docs/architecture/chart-package.md`。
- **音频与时钟漂移**不影响判定规则本身：判定只用音乐时间，时钟校正见 `docs/architecture/audio.md`。

---

## 附：与本文对应的回归用例

| 主题 | 用例 |
| --- | --- |
| 判定窗口与换算 | `JudgeFrameTableTests`、`JudgeWindowTests`、`JudgeTuningTests` |
| 头的独立性 | `LongNoteHeadTests`、`HeadDecouplingTests` |
| 同时按下两条条（含超短条） | `SimultaneousBarHeadsTests` |
| 越界手指 | `OutOfLaneFingerTests` |
| 按下通道只收按下步 | `LiveLogicInputTests`、`HiddenCarrierJudgingTests`、`NotePressDispositionTests` |
| 自己判定的尾 | `TailBehaviourContractTests`、`FrictionFlickTailArmingTests`、`LongNoteViewLifetimeTests` |
| 短条与头的窗口 | `ShortBarHeadWindowTests` |
| 贴图生命周期 | `LongNoteKeyLifetimeTests`、`TailKeyRetirementTests` |
| 车道与抬起归属 | `TouchLaneTests`、`TailReleaseLaneTests` |
