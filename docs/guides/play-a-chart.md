# 打一首歌

> 目的：从「手上有一首自制谱」到「看到结算」，把曲库、加载、live、暂停、结算、历史这条链路讲全。
> 适用读者：使用者、测试者。
> 前置条件：已能按 Play 进到曲库页（见 [环境与运行](../getting-started/build-and-run.md)）。
> 最后更新：2026-11-02

## 概述

曲库页是唯一的入口。曲包放在用户数据目录下的 `CustomMusicScores/`，每个子目录是一个曲包；
页面启动时扫描该目录，列表里每一行对应一首歌，左侧是封面缩略图，右侧是「手动」「自动」两个按钮。

## 核心内容

### 曲包放哪

```text
%USERPROFILE%\AppData\LocalLow\OpenSekai\Open Sekai Atlas\CustomMusicScores\
  Arcana_Eden_jkxmwei881xx\
    manifest.json      曲名、难度、等级等元数据
    score.json         谱面（音符/事件）
    audio.ogg          音轨
    jacket.jpg         封面（可选）
  PlayHistory.jsonl    打歌历史（由游戏写入，不在曲包目录内）
```

字段定义见 [曲包格式](../architecture/chart-package.md)。

### 三种进入方式

| 方式 | 操作 | 备注 |
| --- | --- | --- |
| 导入 ZIP | 头部「导入 ZIP」，选一个 `.oskp` 或 `.zip` | 桌面用 StandaloneFileBrowser，移动端用系统文件选择器 |
| 导入文件夹 | 头部「导入文件夹」 | **移动端隐藏**（`LibraryImport.SupportsFolderPick` 为 false） |
| 手动放入 | 直接把曲包目录拷进上面的 `CustomMusicScores/`，点「刷新」 | 最稳，任何平台都能用 |

导入失败时页面底部状态栏会给出中文原因（例如「无法读取所选文件。」），不会静默失败。

### 管理曲包：更多 / 排序

每行最左边是「更多」，点开是一张浮层卡片，三个按钮从上到下：

| 按钮 | 行为 |
| --- | --- |
| 导出 ZIP | 把曲包打包成 `.oskp`。桌面弹保存对话框（默认文件名取曲名）；移动端没有保存对话框，写到 `CustomMusicScores/Exports/<曲名>.oskp`，状态栏给出完整路径 |
| 删除曲包 | 首次点击只把按钮改成「再点一次确认删除」，第二次点击才真正删除并刷新列表 |
| 取消 | 关闭浮层 |

头部「排序：标题」每点一次循环切换：`标题 → 等级（降序，同等级按标题）→ 最近（按目录修改时间降序）`，
切换后立即重排当前列表。

### 开局

- 点「手动」：正常打歌，判定按输入计算。
- 点「自动」：AUTO 模式，音符全部自动命中，用于验证谱面与特效。

点下去后出现加载浮层（标题 + 进度文案 + 取消）。文案依次是「正在读取谱面...」「正在加载音频...」
「正在开始游玩...」（自动模式是「正在开始自动游玩...」）；点「取消」走 `playbackCts.Cancel()`，回到曲库。

### live 中的暂停

左上角暂停按钮 → 浮层「暂停 / 曲名」+ 三个按钮：

- 继续：回到 live
- 重开：从头开始同一首
- 退出到曲库：按**中途退出**结算（不是失败），历史不写入

### 结算

结果面板显示：曲名、`(自动|手动) · Lv.N · 原因`、大分数、评级、最大连击/总音符、判定分布、两个按钮。

| 原因 | 触发 |
| --- | --- |
| 完成 | 谱面跑完且未失败 |
| 中途退出 | 暂停里选「退出到曲库」 |
| 失败 | 生命值耗尽 |
| 加载失败 | 曲包读不出来，面板只显示失败原因，不显示分数 |

「再玩一次」用同一曲包重开；「返回曲库」回列表并刷新。

### 自动模式的三个特殊表现

这不是 bug，是引擎既有行为：

1. **连击恒为 0**：`ScoreLogic.UpdateCombo` 里 `effectiveResult < NoteResult.Great` 就把连击清零，而 `NoteResult.Auto`
   低于这个门槛。
2. **判定计数全 0**：`UpdateNoteResult` 对 Auto 只累加 `autoCount`，不填 JustPerfect/Perfect/Great 等。
3. **不写历史**：历史只在「手动 + 完成 + 自制谱」时追加。

因此自动打歌的满分是 `1,000,000`（累积摊分后严格成立），但面板上显示的是「总音符 2019 · 自动判定 2019」而不是各级判定。

## 详细说明

### 分数怎么算

单个音符的基分不是 `1,000,000 / 音符数`，而是按**累积摊分**：第 i 个音符（0 基）拿
`round(1e6 * (i+1) / n) - round(1e6 * i / n)`，所以 n 个音符全打满时总和严格等于 1,000,000。
判定倍率：JustPerfect/Auto = 1.0，Perfect = 0.9，Great = 0.7，Good = 0.5。

细节与修复原因见 [ADR-0004](../decisions/0004-cumulative-scoring.md)。

### 历史记录

写盘位置与曲包同级的 `PlayHistory.jsonl`，每行一条 JSON，包含分数、判定分布、清关类型、
`chartHash`（谱面文件 SHA-256）、`rulesVersion`（当前 2）、`sessionId`。

## 示例

导入一首歌并自动验证：

```text
1. 把曲包目录拷进 CustomMusicScores/
2. 曲库页点「刷新」→ 行出现，标题与 Lv 正确
3. 点「自动」→ 加载浮层 → live 开始
4. 打完 → 结果面板 1,000,000 / S / 完成
```

## 常见问题与故障排查

- **导入后列表里没有**：确认目录层级是 `CustomMusicScores/<任意目录>/manifest.json`，不是再多套一层。
- **点开局后停在加载浮层**：看状态栏文案停在哪个阶段；「正在加载音频...」卡住通常是音频文件缺失或格式不支持。
- **结算显示「加载失败」**：曲包元数据或谱面 JSON 解析失败，面板上的 `Message` 会给出原因。
- **手动打歌分数不满 1,000,000 但全是 Perfect**：Perfect 倍率是 0.9，满分要求 JustPerfect。

## 相关文档

- [曲包格式](../architecture/chart-package.md)
- [打歌运行时](../architecture/live-runtime.md)
- [计分修正](../decisions/0004-cumulative-scoring.md)
- [设置项说明](../reference/live-settings.md)
