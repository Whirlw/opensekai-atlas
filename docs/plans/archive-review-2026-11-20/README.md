# 代码审查过程归档（2026-11-20）

这里放的是 [OpenSekai 代码审查最终方案](../code-review-2026-11-20-final.md) 的**过程文档**。结论已全部并入最终方案，**不要以本目录的文件作为执行依据**——其中多处结论后来被推翻，只有在追溯「为什么某个判断被改掉」时才需要读。

## 阅读顺序

审查（36 个独立子智能体，12 模块 × 3 视角）：

1. [`code-review-2026-11-20.md`](code-review-2026-11-20.md) — 首轮报告，12 模块 × 3 审查员的合并结果。

第一轮复核（第三方逐条核实首轮报告，并回炉原答辩人）：

2. [`code-review-2026-11-20-verification.md`](code-review-2026-11-20-verification.md) — 第三方对首轮报告的逐条复核。
3. [`code-review-2026-11-20-rebuttal.md`](code-review-2026-11-20-rebuttal.md) — 原答辩人的逐条答辩（75 行裁决表在此）。
4. [`code-review-2026-11-20-rebuttal-verification.md`](code-review-2026-11-20-rebuttal-verification.md) — 第三方对答辩的复核。

后续轮次（每轮都是「对方复核 → 原答辩人再对账」）：

5. [`code-review-2026-11-20-round2.md`](code-review-2026-11-20-round2.md) + [`…-round2-verification.md`](code-review-2026-11-20-round2-verification.md)
6. [`code-review-2026-11-20-round3.md`](code-review-2026-11-20-round3.md) + [`…-round3-verification.md`](code-review-2026-11-20-round3-verification.md) — 排期表在此成形。
7. [`code-review-2026-11-20-round4.md`](code-review-2026-11-20-round4.md) + [`…-round4-verification.md`](code-review-2026-11-20-round4-verification.md)
8. [`code-review-2026-11-20-round5.md`](code-review-2026-11-20-round5.md) + [`…-round5-verification.md`](code-review-2026-11-20-round5-verification.md)
9. [`code-review-2026-11-20-round6.md`](code-review-2026-11-20-round6.md) — 最后一轮的逐模块结案对账。

## 已知的坑

- **序号不能跨文档照抄**：首轮报告 M03 的第 4 项是「`.oskp` 同步导入」，扫描分离问题是第 5 项；后续文档里的「第 4 项」指的是本轮序号。
- **行号会漂移**：首轮报告在多轮编辑后整体上移了 4 行。
- **数字改过多处**：MAC 数量级、扫描次数、`PaintKit` 重导入次数、`.cs` 文件数都改过，只在最终方案里作准。
