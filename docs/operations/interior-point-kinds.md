# 中间点的四种形态与判定（契约）

状态：已实现 · EditMode `633 / 627 通过 / 0 失败 / 6 跳过`

一根长条身上可以出现四种中间点，只有三种判定，特效只看「有没有菱形」。

| # | 名称 | 数据 | 改变条形状 | 显示菱形 | 判定 |
|---|---|---|---|---|---|
| 1 | 自动中间点 | 转换器生成、`IsSkip = true` 的 `LongHoldCombo` | 否 | 否 | **是** |
| 2 | 预设中间点 | 谱面里的 `Connection`（`ConnectionNote`） | 是 | 是 | **是** |
| 3 | 隐藏中间点 | 谱面里的 `HiddenConnection`（`HiddenConnectionNote`），或 `IsSkip` 的 `ConnectionNote` | 是 | 否 | **否** |
| 4 | 添加中间点 | 谱面里的 `LongHoldCombo`（`IsSkip = false`） | 否 | 是 | **是** |

「改变形状」= 在 `LineNoteList` 里，条身要按它的位置折；1 和 4 只挂在 `NoteList` 上，所以不折。

## 判定规则

1. **只看单根条**：`LongNote.JudgedPoints` 给出这根条要判的中间点 = 1 + 2 + 4。
   判定入口只有两处，都走这个列表：`LongNoteHead.ConnectionNoteJudgment`（手指按住）和
   `LongNote.ConnectionNoteAutoJudgment`（自动播放）。
2. **同一位置只判一次**：同一根条里两个中间点落在同一时刻，`JudgedPoints` 只保留一个。
3. **两根条同一位置各判一次**：列表是每根条自己的，互不影响。
4. **隐藏点不判、也不掉血**：既不在 `JudgedPoints` 里，过期时也按 `Pass` 收场
   （`ConnectionNote` 的 `IsSkip` 走隐藏分支，`Terminate` 不给它 JustPerfect，
   `PublishResult` 不发声）。
5. **计分口径一致**：`LiveUtility.CalculateTotalComboCount` 用同一个谓词
   （`LongNote.IsJudgedInteriorPoint`）计数，隐藏点不占份额，所以一次全 Auto 仍是 1,000,000。

## 特效规则

有菱形才有特效，没菱形就没有：

- 判定表现仍由 `JudgmentScheduler.OnJudgmentNote` 驱动，`IsSkip` 的点不进表现层（1、3 因此安静）；
- 2（`ConnectionNote`）和 4（可见的 `LongHoldCombo`）会走 `TapEffectView`：
  `NoteCategory.Connection` 与 `NoteCategory.Combo` 现在都播放 `Connection()` 那个菱形特效；
- 隐藏点的 `PublishResult` 为空，根本到不了表现层。

## 落地位置

| 关注点 | 位置 |
|---|---|
| 什么算要判的中间点 | `LongNote.IsJudgedInteriorPoint` / `LongNote.JudgedPoints` |
| 按条去重 | `LongNote.BuildJudgedPoints`（按毫秒时刻去重） |
| 手指按住时的判定 | `LongNoteHead.ConnectionNoteJudgment` |
| 自动播放时的判定 | `LongNote.ConnectionNoteAutoJudgment` |
| 隐藏点的静默 | `ConnectionNote.Excute/Terminate/PublishResult` 的 `IsSkip` 分支 |
| 计分口径 | `LiveUtility.CalculateTotalComboCount` |
| 菱形特效 | `TapEffectView.Excute` 的 `Connection` / `Combo` 分支 |
| 自动点的生成避让 | `RuntimeChartConverter.OccupiedGridIndices`（本条自己的头、尾、连接点、添加点） |

## 测试

`Assets/Tests/EditMode/Live/InteriorPointJudgingTests.cs`：
三种形态都判、隐藏不判、同位只判一次、两跟条各判一次、自动播放只回答判定点。

回归：`AutoplayFrameSkipTests`（DEINOS 整曲丢帧仍 1,000,000）、
`RuntimeChartConverterTests`（生成避让自己的位置、别人的位置不避让）。
