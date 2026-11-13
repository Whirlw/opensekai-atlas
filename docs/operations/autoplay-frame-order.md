# 自动播放先于音符行走判定（DEINOS PHAINEIN 掉血）

状态：已修复 · EditMode `617 / 611 通过 / 0 失败 / 6 跳过`

## 1. 现象

自动播放 DEINOS PHAINEIN 不是满分，还会掉血；掉血集中在曲中（friction 段，约第 78–79 小节），
表现为「本来没有中间点的长条突然掉血」。

## 2. 测量（EditMode，真实谱面 `Tools/fixtures/deinos-phainein/score.json`）

同样一段自动播放，只改帧间隔：

| 帧间隔 | 掉血 | 分数 | miss | 生命 |
|---|---|---|---|---|
| 1/60 s（不丢帧） | 无 | 1,000,000 | 0 | 1000 |
| 1/120、1/144、1/30 s | 无 | 1,000,000 | 0 | 1000 |
| 每 5 s 丢一帧 150 ms | 3 | 998,514 | 3 | 1000 → 760 |
| 每 5 s 丢一帧 300 ms | 23 | 988,597 | 23 | → 0 |
| 每 5 s 丢一帧 500 ms | 45 | 977,691 | 45 | → 0 |

所以不是谱面数据错，也不是帧率高低：**只要某一帧跨过了音符的判定窗口，就会掉血**。

## 3. 根因

一帧内的顺序是：

1. `LiveLogic.OnUpdate` → `LiveNoteFrame.UpdateNotes` → 逐个 `note.Excute(...)`。
   `Excute` 把「已经越过自己窗口」的音符判成 Miss（`NormalNote`、`LongNoteHead.SettleExpiredHead`、
   `LongHoldCombo.Excute` 的 `Last` 分支都是这样）。
2. 控制器随后才调用 `LiveLogic.OnAutoInput()`（`SoloLiveController.OnUpdate`）。

丢帧的那一帧里，窗口在两次 `OnUpdate` 之间就关死了：第 1 步先判 Miss，第 2 步的自动判定已经没有东西可判。
丢 150 ms 就够（`LiveConfig.JudgeTime` 更窄），所以设备上 DEINOS 一卡就掉血。

## 4. 修复

`LiveLogic.OnUpdate` 在音符行走之前，先跑这一帧的自动判定：

```csharp
if (Session.IsAuto)
{
    Session.AdvanceAutoJudgment(currentFrameInfo, skipDecoration: true);
}
NoteFrame.UpdateNotes();
```

- `Session.IsAuto` 在 `LiveSessionState.Bind` 里由 `BootData.IsAuto` 得出，是「这一局没人打」这一件事的名字。
- 装饰音符被排除在这一趟之外：它们的特效由行走里的 `DecorationJudgment` 播完再置 Done，
  让自动判定抢先判掉会把特效吞掉。装饰音符本来就不掉血，等行走之后再判和以前完全一样。
- 控制器那一帧的 `OnAutoInput()` 保留，已经判过的音符会被跳过，重复调用是空操作。

## 5. 没有改的东西

- **第 78–79 小节那三个中间点是真的。** 实测三根 friction 条的条体正好压在 8 分线上：
  头 `149985` tick、中间点 `150000` tick、尾 `150015` tick（190 BPM，15 tick ≈ 10 ms）。
  中间点落在条体内部，生成逻辑没错，自动播放也确实把它判成 Auto（`LongHoldCombo#15933 / #15948 / #15963`）。
  掉血不是「多加了中间点」，而是上面那条顺序问题。
- 中间点的生成规则、判定窗口、长条与头的判定语义都没有改动。

## 6. 验证

- 新增 `Assets/Tests/EditMode/Live/AutoplayFrameSkipTests.cs`：
  1. DEINOS 整曲、每 5 s 丢 150 ms 一帧：0 miss、1000 生命、1,000,000、`autoCount == totalComboCount`；
  2. 单音符被一帧跳过窗口仍是 Auto；
  3. 长条头被一帧跳过窗口仍归自动播放。
- A/B（同一段跑两遍，只改判定顺序）：旧顺序 3 miss（`LongHoldCombo#15248 / #15249 / #15250`），新顺序 0 miss。
- 全量 EditMode：617 / 611 通过 / 0 失败 / 6 跳过。

## 7. 回退

`LiveLogic.OnUpdate` 里去掉那段 `if (Session.IsAuto)`，并把 `Session.AdvanceAutoJudgment` 的
`skipDecoration` 参数一起撤掉即可；`Session.IsAuto` 只是 `Bind` 读 `BootData.IsAuto`，不影响其它路径。
