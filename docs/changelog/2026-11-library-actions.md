# 2026-11 曲库管理能力与缺失脚本清理

> 目的：记录给曲库页补「导出 ZIP / 删除曲包 / 排序」三件事，以及顺手清掉历史缺失脚本组件的过程。
> 适用读者：开发者。
> 前置条件：了解曲库页结构与 `SongPackageManager` 的职责（见 [架构/模块](../architecture/modules.md)）。
> 最后更新：2026-11-02

## 概述

曲库页此前只能导入和开局，导出与删除没有入口（`SongPackageManager.ExportZip/DeleteEntry` 早已实现，
但没有任何 UI 调用）。这一轮把这些能力接上，并补了一个排序开关；另外把播放时刷屏的
`The referenced script ... is missing!` 警告从 361 处引用清到 141 处，播放时不再出现。

## 曲库管理

### 做了什么

- 新增 [LibraryActionsPanel.cs](../../Assets/Game/Runtime/OpenSekai/App/UI/LibraryActionsPanel.cs)：每行「更多」打开的浮层卡片，
  三个按钮「导出 ZIP / 删除曲包 / 取消」。删除是两段式：第一次点击把按钮文字改成「再点一次确认删除」，
  第二次才执行，避免误触。
- `LibraryImport` 增加 `PickExportDestination(defaultFileName)`：桌面走 `StandaloneFileBrowser.SaveFilePanel`
  （扩展名 `oskp`），移动端返回 null，由调用方回退到 `CustomMusicScores/Exports/<曲名>.oskp`。
- `LibraryScreen` 增加：
  - 行内「更多」按钮（88×52，位于「手动」左侧），标题/副标题右边界相应让出空间；
  - `sortMode` 与头部「排序：标题 / 等级 / 最近」循环按钮，`Refresh()` 用 `CompareItems` 排序；
  - `ExportPackage`（`SongPackageManager.Shared.ExportZip`）与 `DeletePackage`（`DeleteEntry` + 刷新），
    失败都落到底部状态栏。

### 为什么

重构后曲库成为唯一入口，不能在列表里删除或导出会让用户必须手工去数据目录操作；
排序则在曲包变多后才有意义，但实现成本很低，一起做掉。

### 怎么验证的

- 编辑器 Play：曲库页显示「排序：标题」与每行「更多」；点击前的浮层通过临时在 `Start()` 里调用
  `Show(...)` 截图确认布局（标题/提示/三个按钮位置正确，行内按钮无重叠），验证后已删除该临时代码。
- 导出与删除调用的是既有 `SongPackageManager` 方法，本轮未做端到端点击验证（桌面会弹系统保存对话框、
  删除是不可逆操作），只确认接线与错误处理路径。

## 缺失脚本清理

### 现象

进入 live 后 Unity 持续打印 `The referenced script (Unknown) on this Behaviour is missing!`。
根因是反编译工程的历史遗留：30 个 prefab 共 361 处 `m_Script` 指向的 GUID 在整个 `Assets` 里
（以及上游 HEAD 里）都不存在，与本次重构删除无关。

### 做了什么

临时脚本 `MissingScriptCleanup`（菜单 `OpenSekai/Dev/Report Missing Scripts` / `Strip Missing Scripts`）：
先按「.meta 里出现过的 GUID 集合」比对 prefab YAML 找缺失引用，再用
`PrefabUtility.LoadPrefabContents` + 对**所有子物体**逐个 `GameObjectUtility.RemoveMonoBehavioursWithMissingScript`
+ `SaveAsPrefabAsset` 删除。

- 结果：361 → 141 处，31 → 22 个 prefab；两轮后收敛（第三轮 `stripped assets=0`）。
- 剩下 141 处位于嵌套 prefab 实例/缺失实例上，`RemoveMonoBehavioursWithMissingScript` 触不到，
  需要逐块改 YAML；本轮**没有**继续做（风险与收益不匹配）。
- 编辑器 Play 实测：整个会话 `referenced script` 警告 0 条，live 画面与 HUD 正常。

### 教训

- 第一次用 PowerShell 做 YAML 块删除时，`-match` 连续两次匹配后 `$Matches` 只保留最后一次的捕获组，
  结果把 126 个 prefab 的 1858 个组件块全删了；靠 `git checkout -- <prefab>` 全量还原才恢复。
  **批量改 YAML 必须逐条断言，或改用 Unity 的 Prefab API。**
- `GameObjectUtility.RemoveMonoBehavioursWithMissingScript` 只处理传入的那一个 GameObject，不递归。

## 相关文档

- [打一首歌](../guides/play-a-chart.md)
- [曲包格式](../architecture/chart-package.md)
- [重构日志](2026-11-refactor-log.md)
