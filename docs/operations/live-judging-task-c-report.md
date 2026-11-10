# 任务 C 报告：护栏发现同段计算错误，停止并回退

日期：2026-11-10。基线为任务 A 提交 a1b6b47，已 push origin HEAD:main；全量 524 / 520 / 0 / 4。

## 修改范围与理由

- 仅在 Assets/Tests/EditMode/Live/NoteSpeedRatioTests.cs 新增 38 行、5 个用例，构造速度 1 / 2 / 0.5 的三段（边界进度 0 / 2 / 4）。既有断言未改。
- 覆盖音符位于两处边界、跨第二和第三段的加权平均、同段区间、空数组回退；每项均注释期望算式。
- 没有修改任何生产代码。由于发现 bug，新增测试已回退，补丁保存在 TestResults/live-judging-c-reverted.patch。
- 本报告是当前唯一未提交文件；C 未创建完成提交，B 未开始。

## 失败证据

全量 EditMode：**529 / 524 passed / 1 failed / 4 skipped**。新增 5 条中 4 条通过，唯一失败为：

`NoteSpeedRatioTests.TravelWhollyInsideTheSecondSegmentUsesItsSpeed`

输入：事件 (0,1)、(2,2)、(4,0.5)；currentProgress=2.25、noteProgress=3.75、fallbackSpeedRatio=9。

期望：(1.5 × 2) / 1.5 = **2**。实际：**9**。

源码定位：Assets/Game/Runtime/Shared/Live/NoteSpeedRatio.cs 的 `else if (i > 0)` 分支中，第一次遇到进度 4 的事件时，`hasRange` 仍为 false，且 4 > 3.75，于是直接返回 fallbackSpeedRatio（父提交行 65–67）。因此同段区间没有返回该段速度；当回退值恰好等于段速度时会掩盖此问题。本次特意使用不同回退值，未放宽断言。

## 验证与工具

- 指定 csc + Library/Bee/artifacts 最新 OpenSekai.Game.rsp：退出码 0，没有 error CS，日志 TestResults/live-judging-c-csc.log。
- 先 editor focus，再 Refresh + RequestScriptCompilation(CleanBuildCache)。仅测试源码发生变化，测试 DLL 从 16:53:49 UTC 更新到 16:57:01 UTC；游戏 DLL 保持 16:53:46 UTC，生产源码未变。确认后才运行全量。
- 全量结果：TestResults/live-judging-c.xml。失败补丁及 XML 位于忽略目录，未提交。
- 回退测试 DLL 更新到 16:58:58 UTC，随后全量恢复 **524 / 520 / 0 / 4**，见 TestResults/live-judging-c-rollback.xml。Assets 差异为空，任务 A 保持完整。

## 未完成与建议

C 未通过验收；未修改生产、未提交失败测试。按任务书“测试证明 bug 先报告，不顺手修生产”的要求停止，等待确认同段应使用段速度后的修复授权。建议修复该分支后重新应用护栏并跑全量，再继续 B。未做真机验收。

## 已修复

收到单分支修复授权后，仅修改 NoteSpeedRatio.cs 的 `else if (i > 0)` 内 `segmentProgress > noteProgress` 分支：按当前段速度累计非零区间，置 hasRange 并结束遍历。其它分支，包括末尾的近零区间与 !hasRange 回退，均未修改。原来的回退只有恰好等于段速度时才掩盖错误，否则变速谱音符会使用错误滚动速度。

原样恢复上述 5 条护栏，断言未放宽。新增 5/5 通过，NoteSpeedRatioTests 整类 14/14 通过；全量 **529 / 525 / 0 / 4**，结果为 TestResults/live-judging-c-fix-speed.xml、live-judging-c-fix.xml。csc 自检退出码 0，无 error CS；先 focus 再重编，游戏/测试 DLL 分别更新至 17:04:52 / 17:04:55 UTC，确认后执行测试。

额外只读检查结论：对有序、有限的事件和 noteProgress > currentProgress 的区间，末次遍历令 segmentProgress = noteProgress，必然进入区间累计并置 hasRange=true；在最后一段之后会用 scoreInfos[i-1] 的最后速度。该场景不会误入 !hasRange 分支。只读调用相同三段事件：current=5、note=10、fallback=9 返回 **0.5**；current=3、note=6 返回 **1**，分别符合最后段速度和加权 (1×2+2×0.5)/3。未发现该指定场景的第二个问题，没有修改该分支。

C 已达到代码与 EditMode 验收要求；真机变速谱验收尚未执行。先提交并推送 C，再开始 B。
