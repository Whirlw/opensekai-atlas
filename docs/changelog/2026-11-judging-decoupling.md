# 2026-11 输入判定收口：中间点窗口、划尾延迟、头体解耦

时间：2026-11-10
相关任务书：[judgement-input-split](../operations/judgement-input-split.md)、[interior-point-window](../operations/interior-point-window.md)、[head-decoupling](../operations/head-decoupling.md)

## 一句话

三个真机顽固问题的收口：**中间点按自己的窗口判**、**划键提前划不再多一个 Miss**、**摩擦条按上去不再整批涨连击**。

## 问题与真因

| 现象 | 真因 | 结果 |
| --- | --- | --- |
| 划键提前划会多爆一个 Miss，连击归 1 | 延迟划尾的三帧倒数从「离开按住态」开始，而 `TickSwipeTail` 把 `Last`/`Release` 当成池残留，第一帧就把延迟标记清掉；同时尾键自己的超时 `NoteBase.Excute (State >= Last)` 在等待期间就把它判成 Miss | `aad9160` 让倒数在 `Last`/`Release` 期间继续；`01b7954` 加 `JudgementHeldBack` 押后尾键超时（后被 `211ce06` 回退）；最终由 `eac5b99` 收口 |
| 提前划之后仍然多一个 Miss（只有普通头的长条） | `LongNoteHead.PublishHeadMiss()` 只判「是不是可判的普通头」，不判「头部是否已经判过」，于是条体离开 `Last` 时把已判 JustPerfect 的头部改写成 Miss | `eac5b99`：`PublishHeadMiss` 加 `JudgeInfo.result == None` 门禁；`RestoreHeldState` 回到 `Input` 而不是 `InputBegan` |
| friction 开头的长条，手指一按上去连击整批飞涨 | 长条的 `State` setter 在 `Scrolling/Last → InputBegan` 时直接发 `onJudgment / onUpdateCombo / onUpdateScore / onDamage`；而被按住的条每帧 refresh、松手又回到宽限，任何一次状态回溯都重发一次。头部结果因此没有「一次性」契约 | `6238145`：setter 里的四个回调整段删除；头部自持结果、`Settle` 一次性发布（先置位再回调）；摩擦/隐藏摩擦条的直接写结果路径改走同一入口 |
| 过期中间点被晚按整批补判 | `LongNoteHead.ConnectionNoteJudgment` 的循环对所有中间点无时间门禁，过期点停在 `Last` 等一次按下 | `9352bb7`：按帧时钟、`[-125 ms, +141.7 ms]` 窗口判定，窗口外不判不补 |

## 被否决的尝试（不要重复）

| 补丁 | 为什么不行 |
| --- | --- |
| `9814956` / `94330df` / `ffc42cc` 归属类补丁（长条换手/持握优先） | 都在「候选归属」上打转，没触及状态发布 |
| 中间点加 `IsJudgment` 门禁 + 不动测试 | 14 条契约测试固化了「按住就判」，直接红 |
| 把长条体移出候选集 | `LongNote.Judgment` 一帧做五件事，换入口必然双判或漏判 |
| `01b7954` + `121fc41` | 真机「都没改善」——因为真正重复发布的地点是 `State` setter |

## 现在的语义（判定层）

- **长条体状态只表示外观**：`Wait / Scrolling / InputBegan / Input / Last / Release / Done` 驱动颜色、三帧宽限和生命周期，**不发布任何判定**。
- **头部是一次性音符**：`LongNoteHead.Settle()` 写结果并发布一次，`_headPublished` 先置位再回调；已判结果不会被宽限期结束、尾部判定或重复按下改写。
- **中间点**：只在自己的窗口内、由帧时钟判定；两条的中间点重叠时各判一次，不占按下名额。
- **尾部**：`ITailBehaviour` 的延迟划尾语义保留（提前 125 ms 内的有效滑动，判定延后到松手/离道/到尾时间后 50 ms）。
- **发布顺序**：`onDamage → onUpdateCombo → onJudgment → onUpdateScore`；超时 Miss 省略 score 回调（保持原约定）。

## 验证

- EditMode：`522 / 518 通过 / 0 失败 / 4 跳过`（新增 `InteriorPointWindowTests` 2 条、`HeadDecouplingTests` 4 条）。
- 真机（PLQ110）：friction 连击逐点增长；提前划只出 1 个判定；普通长条按住/换手/尾判无回归。
- 交付包：`Builds/head-decoupled-2026-11-10`（79.59 MB）。

## 教训

- 「同一件事有多个真源」在这套判定里反复出现：`State` 既是外观又是发布开关，就会每帧重发。**状态表达外观，音符表达结果**，这条界线画清之后两个 bug 一起消失。
- 时间相关的判定必须明确「以哪个时钟为准」。触摸事件自带的 `musicTime` 是按下那一刻，不等于当前帧；中间点窗口用帧时钟，划尾倒数用帧计数，两者不可混用。
- 契约测试会固化错误行为：14 条用例用触摸戳 0 配 0.125 s 的点，等于把「无窗口」写死。改契约时必须同时改时钟基准，且断言不许放松。
