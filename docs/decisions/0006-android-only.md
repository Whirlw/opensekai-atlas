# 0006 Android 是唯一的发行平台

- 状态：已采纳
- 日期：2026-11-03
- 相关：[构建与运行](../getting-started/build-and-run.md)、[Android 构建](../operations/android-build.md)

## 背景

这个项目早期带有一整套桌面分支持：`StandaloneFileBrowser` 的 Windows/macOS/Linux
后端与 `Ookii.Dialogs.dll`、`ScreenConfig` 里的窗口尺寸记忆与全屏切换、`LiveLogic` 的鼠标回落判定、
`BackupService` 的桌面临时目录分支，以及文档里 Windows Player 的构建与验收步骤。

但这实际上是一个**触屏音游**：判定靠多指触摸，UI 全部按 9:16 竖屏设计，性能基线是 120/165 Hz 的手机屏。
桌面 Player 从来没有被真正验收过——每次跑构建产物都卡在沙箱与 `%LOCALAPPDATA%` 上，桌面基线一直缺失。

## 决定

- **发行平台只有 Android**（IL2CPP、仅 ARM64、minSdk 26、targetSdk 36）。
- Windows/macOS/Linux 只作为**开发时的编辑器宿主**：编辑器里的鼠标回落、编辑器文件对话框仍然保留，因为
  调试要用它们。
- 与桌面 Player 绑定的东西全部删除或标注为不做：
  - `Assets/Vendor/StandaloneFileBrowser` 整包（含 `Ookii.Dialogs.dll` 与三个平台后端），编辑器改用
    `UnityEditor.EditorUtility` 的文件/文件夹/保存对话框；
  - `ScreenConfig` 的窗口尺寸记忆、全屏切换与 `_standaloneRestoreSize`；
  - `BaseLiveController` 里对应的 `CaptureStandaloneRestoreSize()` 调用；
  - `BackupService`、`LiveLogic` 里 `UNITY_STANDALONE` 的分支（`UNITY_EDITOR` 保留）。
- 计划文档（`architecture/plan/04-*`、`06-*`）里的「Windows／Android 双平台验收」按历史记录保留，但不再执行。

## 影响

- 少一个平台意味着构建清单、发布说明、性能基线、回归门禁都只需要一套。
- 桌面上不再能双击运行 `OpenSekaiAtlas.exe`；想体验请装 APK 或用编辑器 Play。
- 编辑器的文件对话框只在 Windows/macOS/Linux 编辑器可用；一旦哪天有非编辑器宿主需要导包，要重新实现
  `LibraryImport` 的 `#else` 分支（该分支现在直接返回 `null`，会走移动端的默认目录回退）。

## 备选方案

- **保留 Windows Player 但标「未验收」**：会把一个从没测过的产物留在发布面上，且每次重构都要为它维护分支，
  收益为零，否决。
- **保留 StandaloneFileBrowser 只在编辑器用**：编辑器已有原生对话框，多留一个 3.2 MB 的第三方包不划算，否决。
