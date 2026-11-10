# 任务 B 完成报告

日期：2026-11-10。父提交 6a3e7e3；工作分支 codex/long-note-head-split，推送目标 origin HEAD:main。

## 结果

全量先达到 **532 / 528 passed / 0 failed / 4 skipped**；按先前要求再补“双驱计数”和“Moved 不判普通键”两条，最终 **534 / 530 / 0 / 4**。相对 C 基线增加5条通过用例，跳过仍为4。

## 文件及理由

路径相对仓库根目录。全部编辑使用代码块锚点，每次修改后检查 diff --stat，没有按行号替换或整文件重写。

| 文件 | 改动与理由 |
| --- | --- |
| Assets/Game/Runtime/Sekai/Live/LongNote.cs | 拆出 DriveJudgmentFrame，集中头部结算、持有记录、帧标记、偏移清零及内部点/尾键推进；Judgment 保持虚方法入口。持有写入使用帧时间；KeepAlive/RecordHoldingInput 取 Max，防止旧时间回写。 |
| Assets/Game/Runtime/Sekai/Live/LongNoteHead.cs | TryJudge 只处理头部资格和一次性结果，条体驱动移回 LongNote；发布顺序与窗口不变。 |
| Assets/Game/Runtime/Sekai/Core/Live/LiveCandidatePipeline.cs | Collect 只收未判普通头、非装饰普通键、普通划键按下步，且只接收 Began。先按车道中心距离、相等按 fingerId 分配共享候选，再按时间排序并各保留最早一项，避免尚未出结果的划键让旧调度继续判第二项。 |
| Assets/Game/Runtime/Sekai/Core/Live/LiveLogic.cs | 独立驱动条体及其它非按下音符；所有 LongNote 子类先扫描触摸，已有 owner 只用 owner，无 owner 选车道内最近者、相等取小 fingerId；每条只驱动一次，统一调用虚方法 Judgment。旧等级排序、剪枝及 pickup 不再进入主流程。 |
| Assets/Tests/EditMode/Live/LiveLogicInputTests.cs | 新增双指同键一次、双指分键各一次、移动外来指不提名持有条、Moved 不判普通键；原测试断言未改。 |
| Assets/Tests/EditMode/Live/LongHoldPipelineTests.cs | 迁移 stage 脚手架和获准的 pickup 三处旧容器断言；新增双驱计数。保留原调用次数、最终 holder、时间等其余断言。 |
| docs/operations/live-judging-task-b-report.md | 本报告，包含失败、授权迁移、逐步验收和剩余人工验收项。 |

LiveHoldCoordinator.cs 相对父提交差异为空。FlickNote/FrictionNote/FrictionLongNote/FrictionHideLongNote 以及谱面转换实现均未修改；临时诊断代码已撤销。旧静态等级/预留辅助入口保留供既有契约测试，主判定流程使用新按下排序。

## 分步门禁

| 步骤 / 证据（TestResults/） | total | passed | failed | skipped |
| --- | ---: | ---: | ---: | ---: |
| 最早 B1 纯驱动拆分，live-judging-b1.xml | 529 | 525 | 0 | 4 |
| 本轮先只改虚入口，b-virtual-targeted.xml（完整前缀 live-judging-） | 2 | 2 | 0 | 0 |
| 再统一 owner：指定8条加普通条2条，live-judging-b-owner-eight.xml | 10 | 10 | 0 | 0 |
| 三组定向，pickup 仍为旧断言，live-judging-b-owner-three.xml | 83 | 75 | 4 | 4 |
| 获准迁移 pickup 后三组，live-judging-b-pickup-three.xml | 83 | 79 | 0 | 4 |
| 首轮完整目标，live-judging-b-full532.xml | 532 | 528 | 0 | 4 |
| 补两条护栏后的最终全量，live-judging-b-final534.xml | 534 | 530 | 0 | 4 |

首次完整目标达成后，最终还明确限制每指候选为排序后的首项；最终534条覆盖此最终行为。csc + 最新 OpenSekai.Game.rsp 自检退出码0、无 error CS，最终日志 live-judging-b-final-csc.log。每次先 editor focus，再 Refresh + RequestScriptCompilation(CleanBuildCache)，确认 DLL 更新后才测试；最终游戏 DLL 为17:44:00 UTC，含两条新增护栏的测试 DLL 为17:42:56 UTC（最后只改生产逻辑时测试 DLL 无需再生成）。

## 单变量修复前后

| 修复 | 修改前证据 | 修改后证据 |
| --- | --- | --- |
| 独立通道调用虚方法 | overlapping tap 的 JudgmentCalls 为0；直接调用 DriveJudgmentFrame 绕过 HeldNote.Judgment 覆写 | 仅换入口后两条都通过，原计数断言均为1；随后新增双驱测试验证按下帧1次、下一输入帧累计2次。 |
| 统一所有条体 owner 挑选 | 摩擦/隐藏条非嵌套 frame68、嵌套 frame144，note1 holder 从2变1；另一条仍为1 | 四条摩擦/隐藏测试均通过，最终 holder 各为 Id+1，即1/2；普通条两参数和全量 authored crossing 同时通过，无新抖动。 |

## 为什么活体时间必须单调

“这一帧仍在按住”的活体时间只能前进。触摸可能携带几百毫秒前的按下时间，缓存/回放帧也可能旧于条体已处理帧。KeepAlive 的原注释要求活条绝不能用过去的时间刷新；Invariant 原本只告警，直接赋值仍允许倒退。本次保留 Invariant，以 Max 在 KeepAlive 和 RecordHoldingInput 两处实施约束；ResetNote 的生命周期复位不变。未修改 FrictionLongNote.Judgment 的具体音符按下语义。

此前只改 DriveJudgmentFrame 的帧时间参数，4条仍红；只读采样发现全局/logic帧0、条体缓存帧1，独立驱动后 lastInput1，RefreshSustainedHolds 后变0。两处 Max 后，普通持有条在驱动及刷新前后均保持1→1；四条 LastTime 断言全部通过。摩擦条测试会主动老化时间：frame68 为0.1333333→1.133333，frame144 为1.4→2.4，时间正常前进但当时 holder 仍2→1，因此必须另修 owner，不能把归属错误归因于时间戳。完整八条采样保存在 live-judging-b-monotonic-targeted.xml、live-judging-b-monotonic-probe.json。

## Stage 与 pickup 断言授权审计

所有变更限于 LongHoldPipelineTests 的脚手架和 PickupPreservesBothHoldersRegardlessOfInputOrder，reverse/crossed 参数保持原样；Allocate 先于 Evaluate 的顺序未改。

| 旧 stage | 新 stage / 实际入口 |
| --- | --- |
| OverlapJudgmentSorting | SortPressCandidates；辅助方法转调 _candidates.SortPressCandidates，复用原 ConvertLaneBy 委托。 |
| LeaveNotesFastGeneration + PickUpNotBeginInputNotes | DriveIndependentNotes，只调用一次。 |
| 旧 pickup 隐含归属阶段 | 显式 AssignContinuingHolds，映射现有 AssignContinuingHoldInputs，随后独立驱动。 |

上轮三个旧 private 方法实际仍存在，并未引起 NRE；它们只是已退出主流程。没有把环境并发测试覆盖当作产品问题。

| 原断言（父提交位置，仅供审计） | 获准的新契约断言 | 理由 |
| --- | --- | --- |
| 原96行：_connectionNoteList 等价于 {a,b} | 分配后遍历所有输入，CandidateNotes 不含 a 或 b | 已判条体不再是按下候选。 |
| 原100行：first.CandidateNotes 等于 [a] | 独立驱动后再次确认所有候选不含两条，a/b 的 holder 不同且 HasFinger 均为真 | 直接验证归属及无按下竞争，不再依赖旧容器。 |
| 原101行：second.CandidateNotes 等于 [b] | a/b.LastTime 都为1（1e-4 容差）；Evaluate 后原有更严格1e-5、各一次调用、holder1/2断言全部保留 | 验证两条都真实续持，保留原有精确行为约束。 |

此夹具的 crossed 只交换两指在同一车道范围内的位置，两指仍可覆盖各自原条，故保留原 holder1/2 的精确断言；实际谱面换手由 authored crossing 参数族验证。

## 失败历史与剩余事项

- 初版独立通道全量532/508/20/4：包含摩擦owner错误、时间回写、旧候选断言。未放宽断言，整轮回退至529/525/0/4。
- 仅迁移 stage 后三组83/63/16/4，失败未降；只读追踪确认 pickup 在新stage前即断言旧容器，时间从1退0，非NRE；回退并复测基线。
- 单行帧时间替换后4/0/4/0；两处单调写入后8/2/6/0，时间已修但暴露虚调用计数0与holder错误；均按约先采证据再回退。本轮分别修复并逐步通过上表门禁。
- 完整历史及诊断、失败补丁保存在忽略目录 TestResults/live-judging-b-report-history.md、live-judging-b-retry*-*、live-judging-b-monotonic-*，不随提交发布。
- 代码与 EditMode 验收完成；真机普通谱、长条密集谱、变速谱尚未执行，按任务书交由验收方复核，不将其写成已通过。
