# LongNoteHead 拆分报告

日期：2026-11-10。代码基线 `f210572`；实际起点 `a3e09fb`，两者仅两份任务文档不同，开工时工作区干净。

## 第一阶段：零行为变化的结构迁移

新增 `Assets/Game/Runtime/Sekai/Live/LongNoteHead.cs`，由每条 LongNote 惰性持有，保留同一对象直到条被回收。它不是 NoteBase，不进入候选集，不拥有独立生命周期。

| 搬走了什么（原行号） | 搬到哪里 | 留在原处的是什么 |
| --- | --- | --- |
| Judgment 563–620 的判定流程 | LongNoteHead.Judgment | LongNote.Judgment 一层委托；生命周期状态 setter |
| 头部计算、结果修正、按下记账调用 | LongNoteHead.Judgment | JudgeInfo、持有者、_headTouched、_pressedThisFrame、lastInputTime 的写入接口 |
| OffsetJudgeTime 清零的调度（600） | LongNoteHead 调用 ClearHeadJudgeOffset | offset 实际写入仍由所属 LongNote 完成，符合 SingleTimeSourceTests |
| Last 后头部 Miss 条件（305–311） | LongNoteHead.PublishHeadMiss | Last → Release 转移；NotifyHeadMiss 依次发伤害、连击、判定通知 |
| 窗口关闭结算（373–384） | LongNoteHead.SettleExpiredHead | Excute 中原调用位置；关闭条件、先置位再通知的顺序不变 |
| _headSettled（1314） | LongNoteHead 私有字段 | ResetNote 在原复位位置调用 _head?.Reset() |
| ConnectionNoteJudgment 循环（1397–1417） | LongNoteHead.ConnectionNoteJudgment | protected 委托入口，供 RefreshHeldInput 和摩擦条按原路径调用 |
| child 尾键时间门及判定入口（603–615） | LongNoteHead.Judgment | child 绑定、Done 转移和结束通知；Coordinator 的 TailBehaviour sweep 不变 |
| 不迁移的生命周期 | LongNote | 全部状态机、KeepAlive、宽限计数、ShowsAsReleased、ExpireTail、HasFinger、HoldingTouchId、AssignHoldingInput、视图通知 |
| 不扩展的行为 | 原实现 | IsJudgment、AutoJudgment、派生类专用判定、JudgeDueComboPoints、候选集及 Coordinator 调用点 |

回调核对：头部首次命中仍先调用结果修正，再写结果，再走原虚状态 setter；setter 内仍依次判定、连击、分数、伤害，且回调发生在 state 赋值之前。两种 Miss 路径仍不调用结果修正和分数通知，仍依次伤害、连击、判定。委托外层没有新增通知；PublishResult 未改动。child 判定短路顺序、重入父条结束行为均保留。

## ResetNote 复位清单

| LongNoteHead 字段 | 性质 | ResetNote 行为 |
| --- | --- | --- |
| _headSettled | 唯一可变私有状态 | LongNote.ResetNote 的 _head?.Reset() 将其置 false |
| _bar | readonly 所属条引用 | 保留，不是跨曲缓存；不清除绑定，不创建替代条 |

没有其他缓存。_headTouched 仍在 LongNote，紧邻 Head.Reset 调用复位。Reset 不清除 NoteList、尾键绑定或回调。新增复用测试覆盖：首次命中并关闭窗口 → Reset → 同对象未命中时重新结算 Miss → Reset → 同对象再次命中，且不遗留 Miss。

## 编译与全量测试

使用 Unity 6000.6.7f1 自带 dotnet.exe / csc.dll 和最新 Bee rsp，Game、Tests.EditMode 编译均 exit 0，无 error CS。首次新增文件尚未导入时，将新源文件作为额外输入；导入后的最终检查直接使用最新 rsp。

按 editor focus → ForceSynchronousImport → CleanBuildCache 执行。Refresh 可能触发域重载，因此 Refresh 和 CleanBuildCache 分两次调用，避免后一个请求被中断。第一阶段最终两个 ScriptAssemblies DLL 时间戳均更新到 UTC 11:54:39 后才启动全量。

| 运行 | testcasecount / total | passed | failed | skipped | XML（仓库根 TestResults，已忽略） |
| --- | --- | --- | --- | --- | --- |
| 初次运行，旧程序集，非可信基线 | 510 / 510 | 468 | 38 | 4 | head-split-baseline.xml |
| 清缓存重编后的实测基线 | 510 / 510 | 506 | 0 | 4 | head-split-baseline-fresh.xml |
| 第一阶段最终 | 516 / 516 | 512 | 0 | 4 | head-split-phase1-green.xml |
| 第二阶段试验（已撤回） | 516 / 516 | 484 | 28 | 4 | head-split-phase2.xml |
| 撤回第二阶段后最终全量 | 516 / 516 | 512 | 0 | 4 | head-split-final.xml |

初次运行的 38 失败与已回退实验一致；重编后恢复基线。基线重编时 Bee 测试 DLL 已变新，但 ScriptAssemblies 中同内容测试 DLL 保留旧时间戳；第一阶段两个 ScriptAssemblies DLL 均已实际变新。

第一阶段中途发现并修复两处问题：源码约束禁止部件直接访问条的 .OffsetJudgeTime，改为 owner 清零接口；新增测试误把既有绑定角色假定为 Combo，改为捕获绑定后的原始角色并验证保持不变。未修改任何已有测试。

### 九个红线类（第一阶段全量 XML 中核对）

| 类 | total | passed | failed | skipped |
| --- | --- | --- | --- | --- |
| LongHoldPipelineTests | 77 | 77 | 0 | 0 |
| LongHoldOwnershipTests | 7 | 7 | 0 | 0 |
| LongNoteGraceTests | 3 | 3 | 0 | 0 |
| LiveLogicInputTests | 30 | 30 | 0 | 0 |
| LiveLogicCharacterizationTests | 5 | 5 | 0 | 0 |
| NotePressDispositionTests | 7 | 7 | 0 | 0 |
| FlickEndBehaviourTests | 36 | 36 | 0 | 0 |
| TailReleaseLaneTests | 2 | 2 | 0 | 0 |
| DecorationTailJudgmentTests | 4 | 0 | 0 | 4 |

DecorationTailJudgmentTests 保持基线的四条跳过，不将跳过记为通过。
撤回第二阶段后的最终全量中，九个类的数字与上表完全一致。

## 新增测试及断言审计

仅新增 LongNoteHeadTests.cs 及 Unity meta；已有测试文件零修改、零删除。相对 f210572，Assets/Tests 下删除行数为 0。

| 新增测试 | 守护行为 |
| --- | --- |
| HeadResultAndCallbacksAreVisibleInThePressFrameOnlyOnce | 同帧结果、结果修正及回调顺序、回调可见旧状态和清零前 offset；续持不重复通知 |
| ResetAllowsTheSameHeadToSettleAndBeHitAgain | 同对象跨次复用，结算标记复位、Miss 清除、绑定保持 |
| HeadMissKeepsItsOriginalNotificationOrder(false/true) | 窗口关闭及 Last 两种 Miss 的通知顺序、次数，不增加分数或结果修正通知 |
| SustainedRefreshKeepsOffsetAndInteriorParentWithoutRepublishingHead | 续持不清零 offset，内部点父条及角色保持、只结算一次 |
| TailEntryRetainsTheTimeGateAndShortCircuitOrder | 用音乐时间而非未刷新的尾键 offset 门控；IsJudgment 失败不调 Judgment；Done 后早退 |

共 6 个新增用例。没有修改或删除任何已有断言，也没有放宽已有标准。

## 真机三张谱

本轮未运行 Android 真机；以下全部待验，EditMode 结果不能代替真机验收。

| 验证项目 | 普通 4/4 | BOF 长条密集 | 变速 |
| --- | --- | --- | --- |
| 按住不断、换手不断 | 待验 | 待验 | 待验 |
| 松手先灰、3 帧后消失 | 待验 | 待验 | 待验 |
| 宽限期内回来能接住 | 待验 | 待验 | 待验 |
| 重叠长条内部点各自增长 | 待验 | 待验 | 待验 |
| 划键反划封顶 Great、松手后尾判上屏 | 待验 | 待验 | 待验 |

## 第二阶段

第一阶段提交 `2d3eb0e`：零行为变化，迁移成员和新增测试如上。

第二阶段独立提交 `da11063`：CoverHoldsWithAnyFinger 收集每条被覆盖长条的第一个合格活跃输入，在原 TailBehaviour sweep / ExpireTail 之后调用一次 Head.Judgment；Collect 跳过 LongNote。原 RefreshHeldInput 调用点未替换，未重复此前直接替换 TryRefreshFromOwner 的实验。

双程序集编译通过，但全量为 `516 / 484 / 28 / 4`，不满足门禁。失败分布：LongHoldPipelineTests 20 条、LiveLogicInputTests 7 条、FlickEndBehaviourTests 1 条。包括重叠内部点 12 档、候选归属保留 4 档、重叠普通键 2 档、新手指 2 档、交叉换手 4 档、移动长条 3 档以及未按过头部的覆盖保护。完整用例及断言差异保存在 head-split-phase2.xml。

这些失败说明结构迁移本身还不能使新的调度顺序等价于原有候选/归属及派生类入口；这是从失败分布作出的推断。本轮不改断言、不改 JudgeDueComboPoints，也不继续改调度来追绿。

已以 `7e365f2` 独立 revert 第二阶段。撤回后 Game/Tests 源码与第一阶段提交逐字一致，重新双程序集编译通过、重编并全量实跑得到 `516 / 512 / 0 / 4`。最终 Coordinator、Collect、视图层和 Shared 容差无净变化。

后两轮测试源未改，Unity 在 ScriptAssemblies 中保留了相同内容测试 DLL 的第一阶段时间戳 `11:54:39 UTC`，没有人为改时间戳。最终清缓存重编的 Bee Game / Tests DLL 时间戳为 `11:59:32 / 11:59:35 UTC`；分别与 ScriptAssemblies 中对应 DLL 的 SHA-256 完全一致。这一事实单独记录，不声称后两轮两个 ScriptAssemblies 时间戳都再次变新。

最终 DLL SHA-256：

- Game：`B78366E82A8E59FE358A638099B949E5AA96BB58258C74C7BF3B47D7763B1AFC`
- Tests.EditMode：`DEE249FC9966D6758E9977E39D0C695280AD8AE5A61D0FFD21B351A690391673`

最终只保留第一阶段。第二阶段未交付启用；真机仍全部待验。

## 验收记录（独立复核，2026-11-10）

验收方：本侧。复跑环境与实现者不同进程，全部命令重跑一遍。

### 已通过

| 验收项 | 结果 |
| --- | --- |
| git log 粒度 | 2d3eb0e 第一阶段、da11063 第二阶段、7e365f2 独立 revert、57479d9 报告：四项齐全 |
| 第二阶段可独立回退 | git diff 2d3eb0e..HEAD -- Assets **为空**，源码与第一阶段逐字一致 |
| Assets/Tests 只增不减 | git diff --stat f210572..HEAD -- Assets/Tests = 158 insertions(+), 0 deletions |
| Shared / Coordinator / Pipeline / LiveLogic / 视图层 | git diff f210572..HEAD -- 上述路径 **全部为空**：未越界改动 |
| 回调面 | PublishResult 未动；onDamage→onUpdateCombo→onJudgment 三条整体搬进 NotifyHeadMiss，顺序与次数不变；onUpdateResult 经 HeadResultUpdater 透传，仍先修正再写 JudgeInfo |
| 委托单层 | LongNote.cs:305/367/548/1350 四个入口各一次委托；HeadResultUpdater、SetHeadJudgeInfo、SetHeadJudgmentState、ClearHeadJudgeOffset 全为纯赋值，无新增通知 |
| ResetNote 复位 | LongNote.cs:1468 _head?.Reset()，Reset() 只清 _headSettled；_headTouched 仍在条上同处复位 |
| 编译 | OpenSekai.Game / OpenSekai.Tests.EditMode 均无 error CS（复核时重跑 csc） |
| EditMode 全量（复核实跑） | 516 / 512 通过 / 0 失败 / 4 跳过，TestResults/accept-a.xml |
| 九个红线类（复核实跑） | 77/77、7/7、3/3、30/30、5/5、7/7、36/36、2/2、DecorationTailJudgmentTests 4 跳过（与基线一致） |
| 新增 LongNoteHeadTests（复核实跑） | 6 / 6 通过 |

复核数字与实现者报告（§编译与全量测试、§九个红线类）逐项一致。

### 未通过 / 待验

| 项 | 状态 |
| --- | --- |
| 真机三张谱（§5.3） | **待验**。验收包已出：Builds/head-split-2026-11-10/OpenSekaiAtlas.apk（79.59 MB，12:15:18，buildId uild_cbf01684df4c，Succeeded）。安装时 db devices 为空，设备未连接，未能装机 |
| 第二阶段 | 未交付启用（da11063 已 revert）。实现者记录的 516 / 484 / 28 / 4 与既知的双判/漏判机制一致，本次不深究 |

### 结论

第一阶段（零行为变化的结构拆分）**通过验收**，可作为后续工作的基线。
部件 1 的剩余部分（让长条离开候选集）仍未完成，且第二阶段的失败再次确认：这需要处理调度顺序，而不是换入口。

