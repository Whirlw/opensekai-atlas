# 删掉 owner 机制，并把普通尾的抬手判定改成独立池子

状态：2026-11-10。`main = 2375dc5`，EditMode `534 / 530 通过 / 0 失败 / 4 跳过`（另有既有的 4 条 Ignore）。
前序：[judgement-input-split](judgement-input-split.md)、[live-judging-remaining](live-judging-remaining.md)、[head-decoupling](head-decoupling.md)。

本文档是自包含任务书：接手者不需要读历史对话，只需按第 3 节的四步做，每步单独提交、单独跑全量、单独写报告。

---

## 1. 目标

两条，互有依赖，必须按顺序做：

1. **彻底去掉「归属（owner）」**：长条不再记住「我是被哪根手指按住的」。判断「有没有手指在条上」只保留一个布尔，任何手指都能接住、续上、抬手。
2. **普通尾的抬手判定改成独立池子**：抬手事件发生时，每根抬手手指从「自己车道内、窗口内、且尚未结束的条」里领**至多一条**，重叠的按车道中心距离归一；不再出现「一次抬手把同车道所有条一起结束」。

第 2 条依赖第 1 条：现在「谁抬手算释放」正是靠 owner 判断的（见 2.3），owner 不先拆掉，尾池子没有落点。

---

## 2. 现状（精确行号，全部已核对）

### 2.1 一个字段，五个用途

`Assets/Game/Runtime/Sekai/Live/LongNote.cs`

| 行 | 内容 |
| --- | --- |
| `:19` | `private int _holdingTouchId = -1;` |
| `:485` | `public int HoldingTouchId => _holdingTouchId;` |
| `:491` | `public bool HasFinger => _holdingTouchId >= 0;` |
| `:508` | `public bool WasTouched => _holdingTouchId >= 0;` |
| `:515` | `public bool IsHeldByAnotherFinger(int fingerId, float currentTime)` |
| `:520` | `internal static bool ShouldIgnoreTouch(int holdingTouchId, int fingerId, NoteState state, float lastInputTime, float currentTime, float toleranceSeconds)` |
| `:557` | `KeepAlive(float musicTime)`（`lastInputTime = Mathf.Max(lastInputTime, musicTime)`） |
| `:1258` | `RecordHoldingInput(int fingerId, float musicTime)` → 写 `_holdingTouchId` |
| `:1344` | `AssignHoldingInput(int fingerId)` → 写 `_holdingTouchId` |
| `:1488` | 复位 `_holdingTouchId = -1` |

按机制归类的 37 处引用（已用脚本统计，5 个文件）：

| # | 机制 | 处数 | 位置 |
| --- | --- | --- | --- |
| 1 | 名额保留（死代码） | 6 | `LiveCandidatePipeline.cs:438`、`:502`、`:518`（`ShouldReserveHold`）、`:524`（`IsReservedForAnotherInput`）、`:527`、`:537`；`LiveLogic.cs:770` 转发 |
| 2 | 抢同一音符时 owner 优先（死代码） | 3 | `LiveCandidatePipeline.cs:373-375`（`inputHolds`/`otherInputHolds`）、`:551`（`IsHeldByAnotherFinger`）+ `:293`（`HeldRank` 用 `HoldingTouchId >= 0`） |
| 3 | 每帧把 owner 配回条 | 6 | `LiveHoldCoordinator.cs:57`（`AssignContinuingHolds`）、`:80`、`:105`（`AssignHoldingInput`）、`:280`、`:302`、`:328`；调用点 `LiveCandidatePipeline.cs:403`、`LiveLogic.cs:739` |
| 4 | 别人碰过就忽略 | 5 | `LongNote.cs:515`、`:520`；用户 `LongNoteHead.cs:68` |
| 5 | 字段本体与派生属性 | 17 | 见 2.1 表 |

### 2.2 第 1 步的实际结果（2026-11-11 更正）

原判断「机制 1、2 是纯死代码、删除不改变任何行为」**只对了一半**：长条的**头**在未判定时仍是按下候选（`IsPressStep` 对 `bar.IsOrdinaryHead && !Head.IsSettled` 为真），所以 `IsReservedForAnotherInput` 对「一指先按住、头尚未判」的场景是**有效**的。

已按计划删除（`4908d6b`）：`IsReservedForAnotherInput` 及其两个调用点、`ShouldReserveHold` 的 pipeline 实现（`LiveLogic` 侧转发保留但恒 `false`）、`ResolveSharedCandidates` 的 owner 分支、`CandidateOwnership.Loser(bool,bool,float,float)` 重载。全量 **530 / 526 / 0 / 4**（原 534 减 4 条被删的 owner 用例），无回归。

结论：删掉之后「第二根手指也能提名一个已被别人按住、头还没判的条」——这在「彻底不要 owner」的目标下是**期望行为**（头只会判一次），但现有测试没有覆盖它。第 3 步应补一条护栏：*一指按住、头未判时，第二指在同一车道按下 → 头只判一次，条继续被推进*。

任务 B（`2806c92`）把长条移出了按下通道：`LiveCandidatePipeline.IsPressStep` 对 `LongNote` 一律 false。于是机制 1、2 对长条已经没有作用对象；而普通键没有 owner 概念，这两处对它们也从未生效。**结论：机制 1、2 是纯死代码，删除不改变任何行为。**

### 2.3 owner 现在还活着的地方（不能盲删）

- **抬手判定**：`Assets/Game/Runtime/Sekai/Core/Live/LiveHoldCoordinator.cs:213`
  ```csharp
  if (input.Touch.touchId == held.HoldingTouchId
      || OpenSekai.Shared.Live.HoldSweep.TouchesLane(input.Lane, held.JudgeLaneStart, held.JudgeLaneEnd))
  {
      releasedInside = true;
  }
  ```
  注意这是 `||`：owner 抬手**或**任意手指在该车道抬手都算。也就是说「一次抬手结束所有同车道条」的根源不只是 owner，还有后面那个车道条件；且 `CoverHoldsWithAnyFinger`（`:171`）是**逐条**循环、没有名额概念。
- **每帧喂条**：`AssignContinuingHolds`（`:57`）同时承担「每条每帧只被一根手指推进一次」。**删 owner 时必须保留这个额度**，否则内部点会被判两遍（历史上出现过连击飞涨）。
- **头部 Miss 保护**：`LongNoteHead.cs:68` 用 `IsHeldByAnotherFinger` 判断「别人碰过就不算 Miss」，语义其实是「有没有手指」，改成布尔即可。
- **条体驱动挑选**：`LiveLogic.cs:708 PickBarInput`，`:716` 是 owner 分支（任务 B 新增），其余是最近车道 + fingerId 平手。

### 2.4 相关但**不改**的部分

- `LiveLogic.Judgment()` 流水线（`LiveLogic.cs:624-635`）：
  `_connectionNoteList.Clear()` → `JudgeDueComboPoints()` → `AllocateInputJudgableEachNotes()` → `DriveIndependentNotes()` → `SortPressCandidates()` → `RefreshSustainedHolds()` → `EvaluateJudgment()` → 两个 Tick。
- 划尾（`SwipeTail`）、自判尾（`SelfJudgedTail`）、静默尾（`SilentTail`）的语义**不动**；只有普通尾（`ReleaseTail`，`Assets/Game/Runtime/Sekai/Live/Tails/ReleaseTail.cs:15`）改由池子派发。
- `NoteState` 枚举（`Wait / Scrolling / InputBegan / Input / Release / Last / Done`）**不加不减**。

---

## 3. 四步执行

### 第 1 步：删死代码（零行为变化）

删除：

- `LiveCandidatePipeline.cs`：`ShouldReserveHold`（`:518`）、`IsReservedForAnotherInput`（`:524`）及其两个调用点（`:438`、`:502`）；`inputHolds`/`otherInputHolds`（`:373-375`）与它们参与的 tie-break（保留纯距离比较）；`:293` `HeldRank` 里 `held.HoldingTouchId >= 0` 的分支（`HeldRank` 本身是否还需要，见下）。
- `LiveLogic.cs:770` 的 `ShouldReserveHold` 转发。
- 测试：`Assets/Tests/EditMode/Live/LongHoldPipelineTests.cs:121-123`（3 条 `ShouldReserveHold` 用例）、`Assets/Tests/EditMode/Live/LongHoldOwnershipTests.cs` 里的 `ShouldIgnoreTouch` 用例留到第 2 步。

关于 `CandidateOrder.HeldRank`（`Assets/Game/Runtime/Shared/Live/CandidateOrder.cs`）：长条已不进按下通道，`HeldRank` 只剩普通键/划键在用，它们在构造上等于 `TouchRank`。**先只删 `HoldingTouchId >= 0` 分支、保留常量**，等全量绿了再评估是否整个删掉（避免一次动两处）。

门禁：全量应仍 `534 / 530 / 0 / 4`。不装机。

### 第 2 步：把「哪个手指」换成两个布尔

`LongNote`：

```csharp
private bool _fingerOnIt;      // 本帧有手指在条的车道内
private bool _everTouched;     // 曾经有过

public bool HasFinger => _fingerOnIt;
public bool WasTouched => _everTouched;
internal void MarkTouched()                 // 替代 RecordHoldingInput 的「记 id」职责
internal void SetFingerOnIt(bool on)        // 由协调器/独立通道每帧写
```

- `RecordHoldingInput(fingerId, musicTime)` → 改名/改为 `Touch(float musicTime)`：只写 `lastInputTime = Mathf.Max(lastInputTime, musicTime)` 与 `_everTouched = true`，**不记 id**。
- `AssignHoldingInput`（`:1344`）删除。
- `HoldingTouchId`（`:485`）保留为只读调试属性，返回 `_fingerOnIt ? 0 : -1`（**不再暴露真实 id**），第 4 步之后再删。
- `IsHeldByAnotherFinger` / `ShouldIgnoreTouch`：删除；`LongNoteHead.cs:68` 的用法改成「本帧有没有手指在该车道」，即 `!_bar.HasFinger`。
- `KeepAlive` 保持时间单调（`Mathf.Max`）。

测试迁移（不改断言语义，只换表达）：

| 文件 | 处数 | 改法 |
| --- | --- | --- |
| `FlickEndBehaviourTests.cs` | 11 | `bar.RecordHoldingInput(1, t)` → `bar.Touch(t)` |
| `LongHoldPipelineTests.cs` | 9 断言 + 2 构造 | `HoldingTouchId == 1` → `HasFinger`/`WasTouched`；`:554-560` 的换手用例改成「换手后仍被推进、`lastInputTime` 前进」 |
| `LiveLogicInputTests.cs` | 4 | 同上；诊断字符串 `holder=` → `finger=`（改为 `HasFinger`） |
| `LiveLogicCharacterizationTests.cs` | 1 | 同上 |
| `LongNoteGraceTests.cs` | 4 | `:128` 反射写 `_holdingTouchId` → 调 `SetFingerOnIt(false)`；`:154-158` 断言改 `HasFinger` |
| `LongNoteHeadTests.cs` / `LongNoteViewLifetimeTests.cs` / `TailReleaseLaneTests.cs` | 3 | `HoldingTouchId == 1` → `HasFinger` |

门禁：全量绿。**装机**，重点验：长条按住、换手（第二根手指接住）、松手变灰 50 ms 后消失。

### 第 3 步：每帧喂条改成「按车道覆盖」

`LiveHoldCoordinator.AssignContinuingHolds`（`:57`）现在是「按 owner 把输入配回条」，改成：

```
每帧：
  for 每根输入 i（未结束）:
      找它车道内、窗口内、未结束的条里离它最近的一条（等距取 time 更早的）:
          把它配给这根输入（保持「每条每帧至多被一根手指推进」）
      若这条已被别的输入领走 → 比较距离，近者得
```

不记身份，配额仍存在。`LiveHoldCoordinator` 里的 `:80`、`:105`、`:280`、`:302`、`:328` 全部改读布尔；`LiveLogic.PickBarInput`（`:708`）删掉 `:716` 的 owner 分支，只保留「最近车道 + fingerId 平手」。

门禁：全量绿。**装机**，重点验：重叠的摩擦条/隐藏条（内部点不得重复判、连击不得飞涨）、交叉换手、两指按同一条。

### 第 4 步：抬手判定改车道 + 普通尾池子

a) `CoverHoldsWithAnyFinger`（`LiveHoldCoordinator.cs:171`）：

- `releasedInside` 去掉 `input.Touch.touchId == held.HoldingTouchId` 这一支，只保留车道判断；
- `ReleaseTail` 的派发从「每条各自 `TryFinish`」中摘除（`ReleaseTail.TryFinish` 改为 `return false;`，`AcceptsRelease` 保留作为谓词），swipe / self / silent 三条路径原样保留。

b) 新增 `LiveLogic.FinishReleaseTails()`，插在 `Judgment()` 的 `RefreshSustainedHolds();`（`LiveLogic.cs:631`）之后：

```
lifting = 本帧 phase ∈ {Ended, Canceled} 的输入
若 lifting 为空 → return（普通尾只在抬手这一帧派发）
contenders = _judgmentNoteList 里
    State != Done && TailBehaviour.AcceptsRelease && !TailBehaviour.AcceptsSwipe
    && IsTailJudgeableAt(currentFrameInfo.time)
对每个 contender：
    claimer = 在 lifting 里、车道命中、且未被其他条占用的输入中，车道中心距离最近者（等距 fingerId 小）
    若 claimer 已领满（每根手指每次抬手至多一条）→ 跳过
    held.JudgeTailAsRelease(currentFrameInfo.time)
未被领到的 contender 不动，由既有 ExpireTail 超窗路径处理（与「抬手判失败」区分）
```

注意实现顺序：**先按距离把条分配给手指，再执行判定**，不要边遍历边判（否则先遍历到的条会吃掉手指）。

门禁：全量绿。**装机三谱**：普通谱（尾判时序）、长条密集 BOF（重叠尾不得一次全判）、变速谱（尾窗口）。

### 第 5 步（可选，最后做）：删掉残留

第 4 步之后 `HoldingTouchId` 已无读者，单独一个提交删除该属性与 `_fingerOnIt ? 0 : -1` 的过渡实现。

---

## 4. 红线

1. 不改 `NoteState` 枚举值。
2. 不动划尾 `SwipeTail`、自判尾 `SelfJudgedTail`、静默尾 `SilentTail` 的判定语义；`FlickEndBehaviourTests` 必须保持绿。
3. 不动 `DriveIndependentNotes` / `SortPressCandidates` / `JudgeDueComboPoints` 的通道划分（任务 B 刚收口）。
4. 不为了「让测试变绿」放宽断言；断言要改只能改成**更强的**契约（例如「条体不进候选集 + 各自 HasFinger + LastTime 前进」）。
5. 每步单独提交、单独跑全量；不允许把四步合成一个提交。

---

## 5. 环境与工具链

| 项 | 值 |
| --- | --- |
| 仓库 | `E:\DeepseekHarness\OpenSekai`（Unity 6000.6.7f1） |
| 编译自检 | `& 'C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Data\DotNetSdk\dotnet.exe' 'C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll' "@<最新 Library\Bee\artifacts\**\OpenSekai.Game.rsp>" -nologo`，过滤 `error CS` |
| Unity 重编 | 先 `unity_command editor focus`，再 `unity_eval`：`AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport)` + `CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache)`；否则请求会被吞 |
| 重编落地判据 | `Library\ScriptAssemblies\OpenSekai.Game.dll` / `OpenSekai.Tests.EditMode.dll` 的 LastWriteTime 变新 |
| 跑测试 | `unity_eval`: `return OpenSekai.EditorTools.TestRunnerBridge.Run("EditMode", "TestResults/<名字>.xml");`，解析 `total/passed/failed/skipped` |
| 出包 | `unity command build run --target Android --outputPath "Builds/<名字>-2026-11-10/OpenSekaiAtlas.apk" --confirm --result-only` |
| 装机 | adb `C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe`，设备 `3B164A00Y5600000`，包 `io.github.opensekai` |
| 取证 | `adb pull /storage/emulated/0/Android/data/io.github.opensekai/files/logs/live-*.log`（`LiveTrace` 分类含 `Tail`/`Hold`） |

**编辑纪律**：用锚定的整块替换，禁止按行号改（历史上三次把括号改坏）；每次改完立刻 `git diff --stat`，行数应是个位数到几十。

---

## 6. 每步交付的报告必须写

1. `git diff --stat` 的输出与「只碰了允许的文件」的确认。
2. 全量数字（前后各一次）。
3. 哪些测试被迁移、迁移前后的断言对比（证明没有放宽）。
4. 真机结果（第 2/3/4 步）：哪三谱、看了什么、是否通过。
5. 没做的事与理由。

---

## 附：为什么 owner 一直没被删干净

前几轮重构删的都是「后果」而不是「原因」：

- 部件 1（头体解耦）删掉了「头部靠条体状态发判定」；
- 部件 2（中间点）删掉了中间点占名额；
- 部件 4（按下通道只收三类）把长条移出候选集，于是机制 1、2 失去作用对象，但代码留下了。

而「记住是哪个手指」这个字段同时被用来表达「有没有手指」(`HasFinger`)、「抬手算不算释放」(`touchId == HoldingTouchId`)、「别人碰过就不算 Miss」，所以它没能随同一起消失。这一步就是把它连根拔掉。
