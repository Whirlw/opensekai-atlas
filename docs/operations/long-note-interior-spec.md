# 长条：中间点与判定归属（2026-11-10 定稿）

> 目的：把「长条的中间点从哪来、谁来判、重叠怎么办」定成一句话能说清的规则，
> 替换掉旧的「按指分配（归属）」机制。适用于 `Assets/Game/Runtime/Sekai/Live/**`。
> 适用读者：改判定/长条的人。
> 前置条件：读过 `architecture/live-runtime.md`、`reference/judging.md`。

## 一、结论（三条规则）

1. **没有归属。** 长条不参与「按指分配 / 候选名额」：一条长条既不吃掉某根手指的帧，
   也不会因为「这条已经被别的指头按住」而拒绝另一根指头。
2. **中间点按全局 8 分音符网格生成。** 除头部与尾部之外，只要落在**当前 BPM 段的 8 分音符线上**、
   且在这条长条的跨度之内，就生成一个中间点。网格取自 BPM 段本身，不是每条长条各自等分。
3. **中间点可以完全重叠，重复判定是正常的。** 两条长条在同一时刻各有一个中间点，
   一次按下把它们都判掉，这不是 bug。

## 二、现状对照（2026-11-10 实测）

| 环节 | 现状 | 与目标差距 |
|---|---|---|
| 生成 | `RuntimeChartConverter.AddComboNote`（`RuntimeChartConverter.cs:339`）用 `LongNoteComboBeat`（工程内取值 **8**）把**每一小节**等分为 8 份，`i / beat` 为小节号、`i % beat / beat` 为小节内比例，经 `MusicScore.GenerateNoteMusicScoreInfo` 换算成时间 | 与本规格**基本一致**：4/4 下每小节 8 份即 8 分音符；BPM 变化时每小节按段内 BPM 换算，仍在网格上。待确认项：非 4/4 小节与 BPM 段首不在小节线时是否仍等分 |
| 判定入口 | `LongNote.ConnectionNoteJudgment`（`LongNote.cs:1383`）要求长条自身处于 `InputBegan / Input / Release`，并只在该长条被某次 `Judgment` 调用时遍历中间点 | 与本规格**不一致**：中间点被判定的前提变成了「这条长条正被按着」，且需要一个「按到这条长条」的调用；不按就永远不判 |
| 名额 | 按下通道（`LiveCandidatePipeline`）把长条当候选，占掉手指当帧的唯一名额，长条判定后 `ClosesSlotAfterPress` 关槽 | 与本规格**冲突**：长条必须完全不进按下通道 |
| 重叠 | 旧机制用候选归属消歧 | 本规格**取消**消歧：各判各的 |

## 三、改造步骤

1. **长条退出按下通道**：`LiveCandidatePipeline.IsPressStepNote` 对 `LongNote` 一律返回 false；
   按下通道只保留普通键、普通划键（按下那一步）、普通头。`JudgeInfo`/候选相关分支随后删除。
2. **中间点改为每帧按存在判定**：在 `LiveHoldCoordinator.Refresh` 的逐条循环里，
   对每条未 `Done` 的长条、每个活跃触摸，调用中间点判定（现有 `ConnectionNoteJudgment` 放宽：
   去掉 `InputBegan/Input/Release` 的前置条件，改为「不处于 `Done`」即可）。
   一次按下同时判掉多条长条同一时刻的中间点，属预期行为。
3. **头部与尾部**：任何在车道内的手指都可以判；只有「头未判且窗口内」时存在一次竞争，
   窗口一过就定局（不引入归属）。
4. **清理**：删除 `ShouldReserveHold`、`HeldRank`、`ClosesSlotAfterPress`、`PickUpNotBeginInputNotes`
   及 `_connectionNoteList` 这一套；相关测试按新规格重写（重叠长条允许双判，不再断言「只判一次」）。

## 四、验收

- 单元测试：重叠长条同一时刻的中间点被一次按下判成两次（断言两条都拿到结果）。
- 单元测试：未被按下的长条，其网格中间点在手指扫过车道时仍被判定。
- 单元测试：划键穿过被按住的长条时仍能被判定（长条不再占名额）。
- 真机：Test13（划键穿过长条）、两条重叠长条同时按住、长条中途换手，三处现象符合预期。
