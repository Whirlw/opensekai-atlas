# 任务 A 完成报告

日期：2026-11-10。父提交 d600552；工作分支 codex/long-note-head-split，推送目标 origin HEAD:main。

## 结果

全量 EditMode：**524 / 520 passed / 0 failed / 4 skipped**，较原基线增加两条通过用例。FlickEndBehaviourTests 实际 36 个用例，36/36 通过（不是预估的 12）；第三轮 LiveLogicInputTests 已 30/30 通过。最终所有 524 条含两组定向测试。

## 文件与理由

路径均相对仓库根目录。

| 文件 | 改动与理由 |
| --- | --- |
| Assets/Game/Runtime/Sekai/Live/LongNote.cs | 普通条体不再写 InputBegan/Input，持有门禁读 HasFinger；恢复亮色为 Scrolling，避免重复生成回调；WasHoldingWhenTerminated 由 HasFinger 推导。自动演奏以头部 Auto 结果推进。保留具体摩擦条自身输入状态。 |
| Assets/Game/Runtime/Sekai/Live/LongNoteHead.cs | 头部仍以 IsSettled 一次性发布；移除写条体输入阶段，持有记录后恢复亮色；中间点入口保留 Last/Release。 |
| Assets/Game/Runtime/Sekai/Core/Live/LiveHoldCoordinator.cs | 仅 HasContinuingHold 一处返回表达式改为 WasTouched && (HasFinger &#124;&#124; Last &#124;&#124; Release)，使 Scrolling 的持有条仍参与续持匹配。其它方法无差异。 |
| Assets/Game/Runtime/Sekai/Core/Live/LiveCandidatePipeline.cs | 排序和运行时预留以 HasFinger 识别持有，兼容条体输入状态退役。 |
| Assets/Game/Runtime/Sekai/Core/Live/LiveLogic.cs | 中间点记忆门禁为 bar != null && (HasFinger &#124;&#124; Last &#124;&#124; Release)；装饰条以已有结果记住已播放头特效，避免再写 InputBegan。 |
| Assets/Game/Runtime/Sekai/LongTapEffectView.cs | 条体持有效果读 HasFinger。 |
| Assets/Tests/EditMode/Live/LongNoteGraceTests.cs | 三项联合断言助手；新增 HeldBodyNeverEntersAnInputPhase、FingerPresenceTracksAssignmentAndReset；迁移 HeldBar 构造。 |
| Assets/Tests/EditMode/Live/LongNoteHeadTests.cs、HeadDecouplingTests.cs、LongHoldPipelineTests.cs、LiveLogicInputTests.cs、TailReleaseLaneTests.cs | 仅按下表替换获准的条体状态断言，保留具体音符断言；LongHoldPipelineTests 另迁移获准构造。 |
| Assets/Tests/EditMode/Live/FlickEndBehaviourTests.cs | 只迁移清单内 11 处持有态构造，无任何断言变化。 |
| docs/operations/live-judging-task-a-report.md | 本报告，记录修改、授权、验收与失败历史。 |

## 11 处获准断言替换

新断言“联合检查”均指同时检查 HasFinger == true、ShowsAsReleased == false、State 不属于 InputBegan/Input。保留原诊断消息；LiveLogicInputTests 的 FrictionLongNote/子类分支保持原断言。头部结果、发布次数、FlickNote/FrictionNote/FrictionLongNote/LongHoldCombo 自身状态断言均未改。

| 测试名 | 原断言 | 新断言 | 为什么必须改 |
| --- | --- | --- | --- |
| LongNoteGraceTests.cs: AFingerReturningInsideTheGraceTakesTheBarBack | Assert.That(bar.State, Is.EqualTo(NoteState.InputBegan).Or.EqualTo(NoteState.Input), "a finger that arrives inside the grace picks the bar back up"); | 联合检查 | 原断言把条体持有事实等同输入阶段，与退役目标冲突。 |
| LongNoteHeadTests.cs: HeadResultAndCallbacksAreVisibleInThePressFrameOnlyOnce | Assert.That(bar.State, Is.EqualTo(NoteState.InputBegan)); | 联合检查 | 原断言把条体持有事实等同输入阶段，与退役目标冲突。 |
| LongNoteHeadTests.cs: ResetAllowsTheSameHeadToSettleAndBeHitAgain | Assert.That(bar.State, Is.EqualTo(NoteState.InputBegan)); | 联合检查 | 原断言把条体持有事实等同输入阶段，与退役目标冲突。 |
| HeadDecouplingTests.cs: ARerefreshKeepsTheBarLitWithoutRepublishing | Assert.That(bar.State == NoteState.InputBegan &#124;&#124; bar.State == NoteState.Input, Is.True); | 联合检查 | 原断言把条体持有事实等同输入阶段，与退役目标冲突。 |
| LongHoldPipelineTests.cs: IsHeld_follows_the_finger_not_the_state（初始） | Assert.That(bar.State == NoteState.InputBegan &#124;&#124; bar.State == NoteState.Input, Is.True); | 联合检查 | 原断言把条体持有事实等同输入阶段，与退役目标冲突。 |
| LongHoldPipelineTests.cs: IsHeld_follows_the_finger_not_the_state（时间过期后） | Assert.That(bar.State == NoteState.InputBegan &#124;&#124; bar.State == NoteState.Input, Is.True, "the state lags one refresh behind, which is why heldness is asked separately"); | 联合检查 | 原断言把条体持有事实等同输入阶段，与退役目标冲突。 |
| LongHoldPipelineTests.cs: GreatHeadStillKeepsTheHoldAlive | Assert.That(bar.State == NoteState.Input &#124;&#124; bar.State == NoteState.InputBegan, Is.True, "A Great head must keep the bar held, not drop it."); | 联合检查 | 原断言把条体持有事实等同输入阶段，与退役目标冲突。 |
| LongHoldPipelineTests.cs: MissedHeadDoesNotBlockCatchingTheHoldUpMidBody | Assert.That(note.State == NoteState.InputBegan &#124;&#124; note.State == NoteState.Input, Is.True); | 联合检查 | 原断言把条体持有事实等同输入阶段，与退役目标冲突。 |
| LiveLogicInputTests.cs: AuthoredCrossingAllowsFollowingOrExchangingFingers | Assert.That(notes[i].State, Is.EqualTo(NoteState.InputBegan).Or.EqualTo(NoteState.Input), $"frame={frame} time={time} note={notes[i].Id} lane={notes[i].LaneStartF}..{notes[i].LaneEndF} holder={notes[i].HoldingTouchId}"); | 联合检查，摩擦条仍走原断言 | 原断言把条体持有事实等同输入阶段，与退役目标冲突。 |
| LiveLogicInputTests.cs: ContinuousInputKeepsBothMovingHoldsThroughOverlap | Assert.That(note.State, Is.EqualTo(NoteState.InputBegan).Or.EqualTo(NoteState.Input), $"frame={frame} note={note.Id}"); | 联合检查，摩擦条仍走原断言 | 原断言把条体持有事实等同输入阶段，与退役目标冲突。 |
| TailReleaseLaneTests.cs: HoldUntilTailTime（共用前置条件） | Assert.That(_bar.State, Is.EqualTo(NoteState.Input), "the bar is still held when the release lands"); | 联合检查 | 原断言把条体持有事实等同输入阶段，与退役目标冲突。 |

## 13 处获准构造迁移

行号仅用于审计父提交位置；实际编辑均使用唯一代码块锚点，没有按行号替换。已存在的 RecordHoldingInput 保留原手指与时间；只在原本没有持有记录的五处增加 (1, 1f)。表中状态设置和记录按语义并列，已有记录在文件中的原顺序保留。除此之外未改触摸注入。

| 文件:原行 / 场景 | 原构造 | 新构造 | 场景 |
| --- | --- | --- | --- |
| FlickEndBehaviourTests.cs:702 / ARealSwipeOnADecorativeBarTailIsJudged | bar.SetStateUnnotice(NoteState.InputBegan); | bar.SetStateUnnotice(NoteState.Scrolling); bar.RecordHoldingInput(1, 1f); | 装饰条按下后真实划尾 |
| FlickEndBehaviourTests.cs:834 / AnUntouchedFrictionFlickTailIsGradedMiss | bar.SetStateUnnotice(NoteState.InputBegan); | bar.SetStateUnnotice(NoteState.Scrolling); bar.RecordHoldingInput(1, 1f); | 未碰摩擦划尾超时 Miss |
| FlickEndBehaviourTests.cs:864 / AMissedTailStillReportsItsJudgement | bar.SetStateUnnotice(NoteState.InputBegan); | bar.SetStateUnnotice(NoteState.Scrolling); bar.RecordHoldingInput(1, 1f); | 尾键 Miss 仍发布判定 |
| FlickEndBehaviourTests.cs:898 / AMissedTailShowsItsCharacterEvenWhenTheBarIsTheOneThatFailedIt | bar.SetStateUnnotice(NoteState.InputBegan); | bar.SetStateUnnotice(NoteState.Scrolling); bar.RecordHoldingInput(1, 1f); | 尾键 Miss 仍显示角色 |
| FlickEndBehaviourTests.cs:1108 / OnlyTheTailRidesAlongWithTheBar | bar.SetStateUnnotice(NoteState.InputBegan); | bar.SetStateUnnotice(NoteState.Scrolling); bar.RecordHoldingInput(1, 1f); | 只有尾键跟随条体 |
| FlickEndBehaviourTests.cs:1138 / ASwipeInTheMiddleOfTheBarIsNotRememberedForItsTail | bar.SetStateUnnotice(NoteState.InputBegan); bar.RecordHoldingInput(1, 0.9f); | bar.SetStateUnnotice(NoteState.Scrolling); bar.RecordHoldingInput(1, 0.9f); | 条中段滑动不预存为尾划 |
| FlickEndBehaviourTests.cs:1168 / ASwipeInsideTheEarlyWindowIsStillRemembered | bar.SetStateUnnotice(NoteState.InputBegan); bar.RecordHoldingInput(1, 1.9f); | bar.SetStateUnnotice(NoteState.Scrolling); bar.RecordHoldingInput(1, 1.9f); | 尾部提前窗口内滑动被记住 |
| FlickEndBehaviourTests.cs:1202 / AnEarlySwipeWaitsForTheReleaseAndThenThreeFrames | bar.SetStateUnnotice(NoteState.InputBegan); bar.RecordHoldingInput(1, 0.9f); | bar.SetStateUnnotice(NoteState.Scrolling); bar.RecordHoldingInput(1, 0.9f); | 提前划等待释放及三帧 |
| FlickEndBehaviourTests.cs:1260 / AFrictionFlickTailIsJudgedByItselfAndTheBarJustDetaches | bar.SetStateUnnotice(NoteState.InputBegan); bar.RecordHoldingInput(1, 1.0f); | bar.SetStateUnnotice(NoteState.Scrolling); bar.RecordHoldingInput(1, 1.0f); | 摩擦划尾自判并分离条体 |
| FlickEndBehaviourTests.cs:1302 / AnUnsettledFrictionFlickTailEndsTheBarOnlyWhenItsWindowCloses | bar.SetStateUnnotice(NoteState.InputBegan); bar.RecordHoldingInput(1, 1.0f); | bar.SetStateUnnotice(NoteState.Scrolling); bar.RecordHoldingInput(1, 1.0f); | 未结算摩擦划尾到窗口关闭才结束 |
| FlickEndBehaviourTests.cs:1342 / AQuickFlickReportedOnlyAsAnEndedSampleStillFinishesTheBar | bar.SetStateUnnotice(NoteState.InputBegan); bar.RecordHoldingInput(1, 0.9f); | bar.SetStateUnnotice(NoteState.Scrolling); bar.RecordHoldingInput(1, 0.9f); | Ended 样本携带快速划动 |
| LongHoldPipelineTests.cs:503 / RubbedBarTakesAnyFingerAndClearsOwnerOnReset | note.SetStateUnnotice(NoteState.Input); note.RecordHoldingInput(1, 0f); | note.SetStateUnnotice(NoteState.Scrolling); note.RecordHoldingInput(1, 0f); | 普通/隐藏摩擦条接受其它手指并在复位清持有者 |
| LongNoteGraceTests.cs:67 / HeldBar | bar.SetStateUnnotice(NoteState.Input); bar.RecordHoldingInput(finger, 0f); | bar.SetStateUnnotice(NoteState.Scrolling); bar.RecordHoldingInput(finger, 0f); | 宽限测试共用已持有条体 |

## 验证与失败历史

- csc + 最新 Library/Bee/artifacts/**/OpenSekai.Game.rsp：退出码 0，无 error CS，记录 TestResults/live-judging-a4-csc.log。
- 先 editor focus，再 Refresh + RequestScriptCompilation(CleanBuildCache)。最终游戏 DLL 16:53:46 UTC、测试 DLL 16:53:49 UTC，均新于回退基线，之后才跑测试。
- 最终定向：TestResults/live-judging-a4-flick.xml，36 / 36 / 0 / 0。最终全量：live-judging-a4.xml，524 / 520 / 0 / 4。三个既有关键宽限/HasFinger 测试及两个新增用例均通过。
- 第一轮全量 524 / 517 / 3 / 4；第二轮定向 30 / 27 / 3 / 0。三条均为交叉换手中间点 Last/None；只读追踪确认 HasFinger 为真，撤回“touch id 恒假”的猜测。两轮均回退并复测基线 522 / 518 / 0 / 4。
- 第三轮仅获准修正 HasContinuingHold 后，定向 30 / 30 / 0 / 0，证明三条回归解决；全量 524 / 519 / 1 / 4，剩装饰尾划单例。该测试只设 InputBegan、holder=-1、HasFinger=false，直接调用 sweep，未进入 owner 刷新；RefreshHeldInput 日志次数 0，无中间点，尾键 Wait/None。第三轮同样回退并恢复基线。
- 本轮按最新授权迁移 13 处构造后全绿，未继续调整生产行为。前三轮补丁、XML 与只读诊断保存在 TestResults/live-judging-a*-*；该目录被忽略，不随提交发布。所有临时日志已撤销。
- LiveHoldCoordinator 仅获准方法有差异；FlickNote.cs、FrictionNote.cs、FrictionLongNote.cs、FrictionHideLongNote.cs、NoteState 枚举均无差异。_holdingTouchId 的写入仍仅为初始化、RecordHoldingInput、AssignHoldingInput、ResetNote，没有新增第二份持有真源。

## 剩余事项

A 代码与 EditMode 验收完成；真机手感验收未执行。接下来按顺序独立执行 C，再执行 B，分别报告、提交、push。
