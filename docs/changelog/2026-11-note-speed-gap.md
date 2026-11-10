# 2026-11 变速段内的音符取了上一段的速度

状态：2026-11-10。这个 bug 与同期的判定重构无关，是被重构时顺手写的护栏测试抓出来的既有缺陷。

## 现象

谱面中途改流速（1.0 → 2.0 → 0.5）时，落在「第一段之后、最后一段之前」的音符滚动速度取的是**上一段**的值，只有最后一个段落之后的音符是对的。表现是变速谱里音符之间的间距与实际速度不一致——单看某个音符位置对，连起来看疏密错。

## 真因

`Assets/Game/Runtime/Shared/Live/NoteSpeedRatio.cs` 的 `Resolve(scoreInfos, currentProgress, noteProgress, fallbackSpeedRatio)` 逐段累加，遇到「音符落在本段内」时应当把本段剩余部分算进加权和再收尾。原实现提前 `return fallbackSpeedRatio;`：

```csharp
if (noteProgress <= scoreInfo.bar + scoreInfo.barProgress)
{
    return fallbackSpeedRatio;   // ← 区间内，但这一段的累计还没打开
}
```

于是「本段就是音符所在段」这一支永远拿不到加权结果，回退到调用方传入的 fallback（即上一段的速度）。

## 修法

在该分支里把本段剩余部分计入，再收尾：

```csharp
accumulated += (noteProgress - currentProgress) * scoreInfos[i - 1].speedRatio;
hasRange = true;
break;
```

`Assets/Game/Runtime/Sekai/Core/Live/...` 之外无改动；行为只在「音符落在有速度事件的段落内部」时变化，段首/段尾/单段谱面结果不变。

## 护栏

新增 5 条 EditMode 用例（`Assets/Tests/EditMode/Live/NoteSpeedRatioTests.cs`），其中 1 条在修前是红的。手算复核：段 `(0,1) 1.0`、`(2,2) 2.0`、`(4,0.5)`，`currentProgress = 2.25`、`noteProgress = 3.75`、`fallback = 9`，正确结果是 2.0 而不是 9。

## 教训

- 这类「回退到 fallback」的分支最容易吞掉正确值：`fallback` 是给「完全不在任何区间内」用的，不该在区间内当短路。
- 它是**既有 bug**，不是重构引入的；写护栏测试（任务 C 本来只要求加测试、不改生产代码）才把它逼出来。护栏的价值就在这里：约定好了「只加测试」，红了就是生产代码错了。

## 相关

- 任务书：[live-judging-remaining](../operations/live-judging-remaining.md) 的任务 C。
- 同期的判定解耦：[2026-11-judging-decoupling](2026-11-judging-decoupling.md)。
