# 长条头彻底解耦（C 方案）

> 目标（用户，2026-11-13）：**长条头在判定上彻底脱离长条**。头是一个独立的判定实体，
> 不再经由条的 `Judgment` 入口；条身的 Result 只表示**条身 + 中间点**；尾键保持已经解耦的独立结果。

## 1. 目标模型

一条长条拆成四个独立实体：

| 实体 | 谁在判 | 结果属于谁 | 生命周期由谁决定 |
| --- | --- | --- | --- |
| 头 | 自己（候选集里的一个音符） | 头自己 | 自己的窗口（按下 / 窗口关闭） |
| 条身 | 不判；只提供按住/灰亮与渲染 | 条身自己（结束时的结算） | `Wait → Scrolling → … → Done` |
| 中间点（`LongHoldCombo`） | 各自节点（`JudgeDueComboPoints`） | 各点自己 | 各自时间 |
| 尾键 | 尾键自己（`NoteBase`，已是真音符） | 尾键自己 | `LongNoteTail` 只决定何时结算 |

三条不变式：

- **H**：头的结论只由「窗口内被按到」和「自己的窗口关闭」决定；条的状态、尾的结束、条被回收都不改它。
- **B**：头结算不写条的状态机（不把条推进 `InputBegan`/`Done`），只写自己的结果。
- **T**：尾的结算不改头的结果（`HeadDecouplingTests.AnEarlySwipeAddsNoMiss` 已钉住）。

## 2. 分三步落地（每步全量 EditMode 绿、可独立回退）

### 第 ① 步：头按自己的窗口结算（不依赖条的生命周期）

- 新增每帧头扫描：条第一次被处理时把头登记到 `LiveNoteSession`，之后**即使条已经 Done 也继续扫描**，直到头结算。
- `LongNoteHead.SettleExpiredHead(t)` 自己判 Miss（`t - 条时间 > LiveConfig.JudgeTime`），不再调 `_bar.LeavePlayState()`。
- `LongNote.LeavePlayState()` 删掉：条离开播放状态不再替头发 Miss（`ForceTerminate` / `Last→Release` / `FinishSilently` 三条路径都不再碰到头）。
- 行为变化（有意）：条被尾提前结束时，头不再立刻补 Miss，而是等自己的窗口关闭；反过来，条 Done 之后窗口内按到头仍然算数（第 ③ 步补上判定入口后生效）。

### 第 ② 步：头的结果独立存储与发布

- `LongNoteHead` 持有自己的 `JudgeInfo`/`Result`，发布走自己的回调（不再写 `LongNote.JudgeInfo`）。
- 条的 `JudgeInfo` 只表示条身（结束时的结算），不再被头/尾覆盖。
- 总连击数与计分对账：`CalculateTotalComboCount` 现在把「条 + 尾」各算一次，要改成「头 + 条 + 尾 + 中间点」各一次。

### 第 ③ 步：头进候选集

- 头成为 `NoteBase` 的派生对象（自己的 `MusicScoreInfo`、车道、`IsJudgment`、`Judgment`），进 `LiveCandidatePipeline`，不再经由 `LongNote.Judgment`。
- 条的 `HasJudgment` 对头不再有意义；`LiveInputRouter` 的注册与候选排序按头自己的身份走。
- 这一层与 2026-11-10 第二阶段（`da11063`，28 红）同一量级，必须单独提交、可回退。

## 3. 验收

- 每步：EditMode 全量 0 失败；九个红线类（见 `long-note-head-split-report.md` §5.2）数字不变或有说明。
- 头相关用例改写只允许**语义解释**，不允许为了让红的变绿而放宽断言。

## 4. 红线：只动判定，不动视觉（用户，2026-11-13）

- 条的视觉状态机 `Wait → Scrolling → InputBegan/Input → Last → Release → Done`、`ShowsAsReleased`、
  `Body.HeadTouched`（决定过线后是「立刻灰」还是「3 帧宽限」）**一律不改**。
- 头的判定可以独立，但**判定特效仍然发生在条上**：`Head.Settle` 继续写 `LongNote.JudgeInfo`，
  这样 `LivePresentationBridge` / `TapEffectView` 看到的还是原来那个对象、原来那一帧。
- 因此「头的结果不再写在条上」（第 ② 步）只有在头拥有自己的视觉身份之后才能做；在那之前，
  条的 `Result` 同时承担「头的判定结果」和「视觉特效的触发值」两件事，这是有意保留的过渡态。

## 5. 进度（2026-11-13）

### 第 ① 步：头的结论由自己的窗口给出（已提交 `9103e301`）

- `LongNoteHead.SettleExpiredHead` 自己判 Miss；`LongNote.LeavePlayState()` 及其三处调用
  （`Last→Release`、`ForceTerminate`、`FinishSilently`）删除，`State` 转移一处未改。
- 头登记进 `LiveNoteSession.TickHeads`，条已经 `Done` 也继续走时钟。
- 用例改写：`HeadDecouplingTests.AnEndedBarLeavesItsHeadToItsOwnWindow`（原 `ABarEndedBefore…`）、
  `AnEndingAskedTwiceStillPublishesTheHeadOnce`、`APressOnAFinishedBarChangesNothing`、
  `LongNoteHeadTests.HeadMissKeepsItsNotificationOrder`。
- 全量 EditMode **610 / 604 通过 / 0 失败 / 6 跳过**。

### 第 ② 步：头的判定有自己的身份（本次）

- `LongNoteHead.Identity`：每根条一个 `NoteBase`，携带**头自己的** `JudgeInfo`，只用于判定侧。
- `Settle` 的发布拆成两半：
  - `onDamage` / `onUpdateCombo` / `onUpdateScore` 传 **Identity** —— 计分、连击、扣血从此回答的是
    「头的结果」，不是「条的 `Result`」；
  - `onJudgment` 仍传**条** —— 判定特效、弹字、视图都还打在原来那个对象、原来那一帧；
  - `LongNote.JudgeInfo` 继续写同一个结果，仅作为视觉镜像（红线 §4）。
- `Reset` 一并复位 Identity（池复用不会带回上一次的头结果）。
- 新增用例 `LongNoteHeadTests.TheHeadScoresThroughItsOwnIdentity`：三个计分回调拿到的是 Identity、
  不是条，`Identity.Result` 与条上的镜像同为 JustPerfect；`onJudgment` 仍然收到条。
- 全量 EditMode **611 / 605 通过 / 0 失败 / 6 跳过**。

### 第 ③ 步：头进候选集

- **③a（本次，已做）**：头有自己的判定入口 `LongNoteHead.Judgment(ref touch, lane)`；
  `LongNote.Judgment` 只转发到它，不再直接进 `DriveJudgmentFrame`。行为逐帧不变（同一个
  `DriveJudgmentFrame`），但入口从此在头上——候选集将来直接调这一个口即可。
  新增 `LongNoteHeadTests.TheHeadEntryAnswersThePress`。
- **③b（未做）**：把 `LiveNoteFrame.ScanNote` 的注册对象从条换成头（`Identity` 需要一个带
  `IsJudgment`/`Judgment` 的派生类型），并处理与 `LiveHoldCoordinator`（按 `LongNote` 过滤
  `JudgmentNotes`）的共存。这条链路上带着判定特效与视图查找，**会动到视觉**，按 §4 的红线
  需要先定「头自己的视觉身份」或「特效继续借用条对象」的桥，再动手。2026-11-10 的第二阶段
  （`da11063`，28 红）就是死在这一层。

