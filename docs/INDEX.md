# 文档索引

> 目的：全量登记 `docs/` 下的每一篇文档，给出标题、路径、摘要与最后更新，作为查找与维护的单一清单。
> 适用读者：所有人，尤其是维护文档的人。
> 前置条件：无。
> 最后更新：2026-11-02

## 概述

规则：新增、改名、删除文档都要改这张表。摘要写「这篇解决什么问题」，不写内容目录。
「状态」列：`现行` = 与当前代码一致；`历史` = 描述已删除或已过时的代码，仅供参考。

## 入口

| 标题 | 路径 | 摘要 | 最后更新 | 状态 |
| --- | --- | --- | --- | --- |
| OpenSekai 文档 | [README.md](README.md) | 文档总入口：项目是什么、怎么读、按任务找文档 | 2026-11-02 | 现行 |
| 文档索引 | [INDEX.md](INDEX.md) | 本文件，全量清单 | 2026-11-02 | 现行 |

## 快速开始 `getting-started/`

| 标题 | 路径 | 摘要 | 最后更新 | 状态 |
| --- | --- | --- | --- | --- |
| 快速开始 | [getting-started/README.md](getting-started/README.md) | 本目录导航与最小验证路径 | 2026-11-02 | 现行 |
| 这是什么 | [getting-started/what-is-this.md](getting-started/what-is-this.md) | 当前形态、有什么/没什么、对外契约、关键数字 | 2026-11-02 | 现行 |
| 环境与运行 | [getting-started/build-and-run.md](getting-started/build-and-run.md) | Unity 版本、构建场景、首次打开、验证判据（221 音符测试谱） | 2026-11-03 | 现行 |

## 使用指南 `guides/`

| 标题 | 路径 | 摘要 | 最后更新 | 状态 |
| --- | --- | --- | --- | --- |
| 使用指南 | [guides/README.md](guides/README.md) | 本目录导航 | 2026-11-02 | 现行 |
| 打一首歌 | [guides/play-a-chart.md](guides/play-a-chart.md) | 曲包位置、三种导入方式、开局/暂停/结算/历史 | 2026-11-02 | 现行 |

## 架构 `architecture/`

| 标题 | 路径 | 摘要 | 最后更新 | 状态 |
| --- | --- | --- | --- | --- |
| 架构 | [architecture/README.md](architecture/README.md) | 三层划分与本目录导航 | 2026-11-02 | 现行 |
| 模块划分 | [architecture/modules.md](architecture/modules.md) | 目录职责、依赖方向、已移出工程的备份清单 | 2026-11-02 | 现行 |
| 曲包与谱面数据 | [architecture/chart-package.md](architecture/chart-package.md) | `.oskp` 目录结构、manifest 字段、ChartDocument 读取链路 | 2026-11-02 | 现行 |
| 打歌运行时 | [architecture/live-runtime.md](architecture/live-runtime.md) | 开局六步、时钟、输入回填、计分、结算与暂停 | 2026-11-02 | 现行 |
| 开局数据契约 | [architecture/play-session.md](architecture/play-session.md) | PlaySession / RuntimeChartDefinition 的字段、构造与和 FreeLiveBootData 的关系 | 2026-11-03 | 现行 |
| 页面系统 | [architecture/screen-system.md](architecture/screen-system.md) | ScreenManager 注册表、7 个可达页面、曲库页为何独立 | 2026-11-02 | 现行 |
| AssetBundle 与清单 | [architecture/asset-bundles.md](architecture/asset-bundles.md) | 15 个白名单 bundle、JSON 清单、产物与加载 | 2026-11-02 | 现行 |
| 音频 | [architecture/audio.md](architecture/audio.md) | 曲包音频、CriAtomShim、为何不用 dspTime | 2026-11-02 | 现行 |
| 轻量化方案（索引） | [architecture/player-lite-plan.md](architecture/player-lite-plan.md) | 设计稿入口：6 个分册导航 + 现状对照表（依赖批次已完成） | 2026-11-03 | 现行（含历史设计） |
| 方案 01 结论与边界 | [architecture/plan/01-goals-and-boundaries.md](architecture/plan/01-goals-and-boundaries.md) | 推荐结论、当时的工程起点、产品边界 | 2026-11-02 | 历史设计 |
| 方案 02 目标结构 | [architecture/plan/02-target-structure.md](architecture/plan/02-target-structure.md) | 模块划分、最小数据契约、asmdef 取舍 | 2026-11-02 | 历史设计 |
| 方案 03 谱面兼容 | [architecture/plan/03-chart-compatibility.md](architecture/plan/03-chart-compatibility.md) | 旧 JSON 与 SUS 的兼容要求与等价验证 | 2026-11-02 | 历史设计 |
| 方案 04 启动与 Live | [architecture/plan/04-startup-pages-live.md](architecture/plan/04-startup-pages-live.md) | 启动链、页面收敛、Live 会话、时间与输入 | 2026-11-02 | 历史设计 |
| 方案 05 资源与数据 | [architecture/plan/05-resources-deps-data.md](architecture/plan/05-resources-deps-data.md) | 资源裁剪、依赖分批、构建规则、用户数据 | 2026-11-02 | 历史设计 |
| 方案 06 阶段与验收 | [architecture/plan/06-phases-and-acceptance.md](architecture/plan/06-phases-and-acceptance.md) | 阶段与退出条件、待改清单、验收、风险 | 2026-11-02 | 历史设计 |

## 运维 `operations/`

| 标题 | 路径 | 摘要 | 最后更新 | 状态 |
| --- | --- | --- | --- | --- |
| 运维与构建 | [operations/README.md](operations/README.md) | 本目录导航 | 2026-11-02 | 现行 |
| Android 构建 | [operations/android-build.md](operations/android-build.md) | 工具链、Player Settings、三个坑、体积取舍、核对命令 | 2026-11-02 | 现行 |
| 发布（GitHub 分发） | [operations/release.md](operations/release.md) | 版本号与 tag、公开 keystore、签名校验、SHA-256、release notes 模板、覆盖安装 | 2026-11-03 | 现行 |
| 真机性能 | [operations/android-performance.md](operations/android-performance.md) | 165 Hz 配置、HUD 采集、结果表、系统限制 | 2026-11-02 | 现行 |
| 编辑器菜单 | [operations/editor-menus.md](operations/editor-menus.md) | 三个菜单组逐条说明与 `[LibraryCheck]` 字段 | 2026-11-02 | 现行 |
| 故障排查 | [operations/troubleshooting.md](operations/troubleshooting.md) | 症状→原因→处理：资产库、meta、编译、MCP、adb | 2026-11-02 | 现行 |
| 测试 | [operations/testing.md](operations/testing.md) | EditMode/PlayMode 两层、桥接菜单与 CLI、覆盖范围与待补 | 2026-11-03 | 现行 |
| 触摸源抽象任务书 | [operations/touch-source-brief.md](operations/touch-source-brief.md) | 给外部执行者的自包含任务书：三步实现、约束、跑法、验收标准与复核清单 | 2026-11-03 | 现行 |
| 双长条断连任务书 | [operations/long-hold-brief.md](operations/long-hold-brief.md) | 给外部执行者的自包含任务书：现象、三次失败尝试、每帧判定管线、复现步骤、验收标准与报告格式 | 2026-11-03 | 现行 |
| 判定内核纯函数化任务书 | [operations/judgment-core-brief.md](operations/judgment-core-brief.md) | 四个任务的现状、约束、验收与报告格式 | 2026-11-04 | 现行 |
| 长条第三轮交接 | [operations/long-hold-v3-handoff.md](operations/long-hold-v3-handoff.md) | 十节逐条记录：断连、抬起尾判、划键长条结束点、装饰条（`GuideNote`）尾判与定位方法 | 2026-11-06 | 现行 |
| 判定通道与解耦收尾 | [operations/judgment-channel-brief.md](operations/judgment-channel-brief.md) | 任务 B 的执行计划：WP0–WP7 工作包、判定通道表、硬性约束与验收 |
| 尾键生命周期 | [operations/tail-key-lifetime-brief.md](operations/tail-key-lifetime-brief.md) | 任务书：尾键显示/回收离开 `State`，收敛成纯函数 `TailKeyLifetime` |
| 素材来源清单 | [operations/asset-provenance.md](operations/asset-provenance.md) | 各类素材的来源与许可、同 GUID 重画手法、覆盖核对命令 | 2026-11-09 | 现行 |
| 音符皮肤制作 | [operations/note-skin-authoring.md](operations/note-skin-authoring.md) | 自己画一套音符皮肤：目录结构、必须一致的像素尺寸表、应用与出包流程 | 2026-11-08 | 现行 |
| 材质包导入 | [operations/material-pack-import.md](operations/material-pack-import.md) | 把画好的材质包贴回工程：两条路径、六条硬规则、九个已踩的坑、像素级校验、以及做一键导入功能的设计建议 | 2026-11-08 | 现行 |
| 发布前素材总检 | [operations/pre-release-asset-audit.md](operations/pre-release-asset-audit.md) | 推 GitHub 前的素材来源检查：三条审计命令、图集覆盖率实测、复检清单 | 2026-11-09 | 现行 |
| 音频外部音源包 | [operations/audio-replacement.md](operations/audio-replacement.md) | 42 个 wav 的目录与加载链、cue 清单、音频规格、导入与验收 | 2026-11-09 | 现行 |
| 音频来源建议 | [operations/audio-sourcing.md](operations/audio-sourcing.md) | 判定音与演出音的选源建议：自制合成优先、CC0 来源清单、CC BY 的署名义务、cue→取材映射 | 2026-11-09 | 现行 |
| 音频需求表 | [operations/audio-spec.md](operations/audio-spec.md) | 对外交付的自包含需求书：42 个 wav 的逐条规格、两套皮肤差异规则、自检命令、交付结构与验收清单 | 2026-11-09 | 现行 |
| Unity 6 升级清单 | [operations/unity6-upgrade.md](operations/unity6-upgrade.md) | 2022.3.62f3 → 6000.6.7f1：现状对照、准备项、API 迁移点、执行顺序、验收与回滚 | 2026-11-02 | 现行 |

## 决策记录 `decisions/`

| 标题 | 路径 | 摘要 | 最后更新 | 状态 |
| --- | --- | --- | --- | --- |
| ADR 索引 | [decisions/README.md](decisions/README.md) | ADR 写什么、怎么写、列表 | 2026-11-02 | 现行 |
| ADR-0001 去掉谱面编辑器 | [decisions/0001-drop-chart-editor.md](decisions/0001-drop-chart-editor.md) | 为什么整块移出编辑器、代价与遗留 | 2026-11-02 | 现行 |
| ADR-0002 清单改 JSON | [decisions/0002-json-ab-catalog.md](decisions/0002-json-ab-catalog.md) | 为什么放弃 MessagePack、两阶段实施 | 2026-11-02 | 现行 |
| ADR-0003 摘 UI 依赖与视频导出 | [decisions/0003-drop-ui-packages.md](decisions/0003-drop-ui-packages.md) | uPalette/SoftMask/UIParticle/视频导出为何删、代价 | 2026-11-02 | 现行 |
| ADR-0004 计分累积摊分 | [decisions/0004-cumulative-scoring.md](decisions/0004-cumulative-scoring.md) | 全连不满分的根因、公式、RulesVersion=2 | 2026-11-02 | 现行 |
| ADR-0005 不背历史包袱 | [decisions/0005-no-legacy-compat.md](decisions/0005-no-legacy-compat.md) | 哪些不迁移、哪两个对外契约必须稳定 | 2026-11-02 | 现行 |
| ADR-0006 只发 Android | [decisions/0006-android-only.md](decisions/0006-android-only.md) | 为什么砍掉桌面 Player、删了哪些桌面分支 | 2026-11-03 | 现行 |

## 参考 `reference/`

| 标题 | 路径 | 摘要 | 最后更新 | 状态 |
| --- | --- | --- | --- | --- |
| 参考手册 | [reference/README.md](reference/README.md) | 本目录导航 | 2026-11-02 | 现行 |
| 设置项 | [reference/live-settings.md](reference/live-settings.md) | 面板 9 项与 `LiveSettingData` 字段、存储与生效时机 | 2026-11-02 | 现行 |
| 构建宏 | [reference/build-defines.md](reference/build-defines.md) | `DOTWEEN`、`OPENSEKAI_PERF_HUD` 等宏的作用与开关 | 2026-11-02 | 现行 |
| 术语表 | [reference/glossary.md](reference/glossary.md) | 曲包/谱面/AB/判定/规则版本等名词 | 2026-11-02 | 现行 |
| 字体 | [reference/fonts.md](reference/fonts.md) | 两套字体的角色与许可、TMP 资产与图集、重新生成步骤 | 2026-11-02 | 现行 |
| 内置符号字符 | [reference/builtin-symbols.md](reference/builtin-symbols.md) | 文案里的 ★♪※ 等符号统计与思源黑体覆盖情况 | 2026-11-02 | 现行 |


## 代码规范 `coding-standards/`

| 标题 | 路径 | 摘要 | 最后更新 | 状态 |
| --- | --- | --- | --- | --- |
| 代码规范 | [coding-standards/README.md](coding-standards/README.md) | 总入口：出处、速查表、分册导航、与项目现状的对照 | 2026-11-02 | 现行 |
| 基本原则 | [coding-standards/01-principles.md](coding-standards/01-principles.md) | KISS、YAGNI、渐进式、一致性四条取舍标准 | 2026-11-02 | 现行 |
| 样式样例 | [coding-standards/02-style-sample.md](coding-standards/02-style-sample.md) | 完整 C# 示例 + 逐段导读 + public 字段的例外说明 | 2026-11-02 | 现行 |
| 命名规范 | [coding-standards/03-naming.md](coding-standards/03-naming.md) | 各元素命名规则表、判断顺序、易踩点 | 2026-11-02 | 现行 |
| 事件规范 | [coding-standards/04-events.md](coding-standards/04-events.md) | 声明/触发/订阅三段式与事件参数打包 | 2026-11-02 | 现行 |
| 特性与序列化 | [coding-standards/05-attributes-serialization.md](coding-standards/05-attributes-serialization.md) | 一行一个特性、`private`+`[SerializeField]`、禁止 public 字段 | 2026-11-02 | 现行 |
| 区域与 .editorconfig | [coding-standards/06-regions-editorconfig.md](coding-standards/06-regions-editorconfig.md) | 手动分区的成员顺序、Rider 导出 `.editorconfig` 三步 | 2026-11-02 | 现行 |
| 设计实践 | [coding-standards/07-design-practices.md](coding-standards/07-design-practices.md) | 单一职责、DRY（3 次）、注释规范、UI Toolkit BEM | 2026-11-02 | 现行 |
| 代码陷阱 | [coding-standards/08-code-smells.md](coding-standards/08-code-smells.md) | 五条 Code Smell 的症状/原因/改法 + Review 清单 | 2026-11-02 | 现行 |
| 工具与扩展资源 | [coding-standards/09-tooling-and-resources.md](coding-standards/09-tooling-and-resources.md) | 脚本模板命名、单元测试、权威参考、架构文章 | 2026-11-02 | 现行 |
| Data 与 Model | [coding-standards/10-odin-toolkits.md](coding-standards/10-odin-toolkits.md) | 静态数据 vs 运行时状态、变更通知、判断流程 | 2026-11-02 | 现行 |
| 外部资料整理 | [coding-standards/references/README.md](coding-standards/references/README.md) | 9 份外链的可得性说明与摘录索引 | 2026-11-02 | 现行 |
| 电子书整理 | [coding-standards/references/unity-csharp-style-guide-ebook.md](coding-standards/references/unity-csharp-style-guide-ebook.md) | Unity 6 版 C# 风格指南电子书的章节骨架与可核实要点 | 2026-11-02 | 现行 |
| 风格指南仓库 | [coding-standards/references/unity-code-style-guide-repo.md](coding-standards/references/unity-code-style-guide-repo.md) | Unity-Code-Style-Guide 仓库的规则条款摘录 | 2026-11-02 | 现行 |
| .NET 设计指南 | [coding-standards/references/dotnet-design-guidelines.md](coding-standards/references/dotnet-design-guidelines.md) | 微软命名/类型/成员设计准则摘录 | 2026-11-02 | 现行 |
| C# 编码约定 | [coding-standards/references/csharp-coding-conventions.md](coding-standards/references/csharp-coding-conventions.md) | 微软 C# 通用编码约定摘录 | 2026-11-02 | 现行 |
| 脚本模板 | [coding-standards/references/unity-script-templates.md](coding-standards/references/unity-script-templates.md) | `Assets/ScriptTemplates` 命名规则与占位符 | 2026-11-02 | 现行 |
| 测试框架 | [coding-standards/references/unity-test-framework.md](coding-standards/references/unity-test-framework.md) | EditMode/PlayMode 差异与 asmdef 配置 | 2026-11-02 | 现行 |
| CSS 命名 | [coding-standards/references/css-naming-conventions.md](coding-standards/references/css-naming-conventions.md) | BEM 的 Block/Element/Modifier 与反例 | 2026-11-02 | 现行 |
| 可扩展架构 | [coding-standards/references/architect-code-that-scales.md](coding-standards/references/architect-code-that-scales.md) | Unity 博客：模块边界与依赖方向 | 2026-11-02 | 现行 |
| 视频演讲 | [coding-standards/references/from-pong-to-15-person-project.md](coding-standards/references/from-pong-to-15-person-project.md) | From Pong to 15-person project 的元信息与可得性 | 2026-11-02 | 现行 |

## 变更记录 `changelog/`

| 标题 | 路径 | 摘要 | 最后更新 | 状态 |
| --- | --- | --- | --- | --- |
| 变更记录 | [changelog/README.md](changelog/README.md) | 本目录导航与记录规范 | 2026-11-02 | 现行 |
| 发布前清理 | [changelog/2026-11-release-cleanup.md](changelog/2026-11-release-cleanup.md) | 素材全替换、去抽卡、体积账与三条守卫脚本 | 2026-11-09 | 现行 |
| 2026-11 判定轮 | [changelog/2026-11-judgment-round.md](changelog/2026-11-judgment-round.md) | 名额/协议/时钟/通道拆分、Guide 真因、尾键纯函数 | 2026-11-07 | 历史 |
| 2026-11 重构日志 | [changelog/2026-11-refactor-log.md](changelog/2026-11-refactor-log.md) | 本轮施工日志（§1–§24，含 Timeline 摘除、清理与规范化进度、版本号与粒子网格、Android 单平台、加载动画；§11 剩余工作已回填状态） | 2026-11-03 | 现行 |
| 2026-11 曲库管理 | [changelog/2026-11-library-actions.md](changelog/2026-11-library-actions.md) | 曲库导出/删除/排序的实现与验证；缺失脚本 361→141 | 2026-11-02 | 现行 |
| Unity 6 升级执行记录 | [changelog/2026-11-unity6-upgrade.md](changelog/2026-11-unity6-upgrade.md) | Unity 6 修复、AB/编辑器门禁、真机门禁与启动崩溃修复；§6 的 7 条 TODO 已回填状态 | 2026-11-03 | 已合并 | |

## 图片 `images/`

| 标题 | 路径 | 摘要 | 最后更新 | 状态 |
| --- | --- | --- | --- | --- |
| 截图与证据图 | images/README.md | 每张图证明什么、被哪篇文档引用 | 2026-11-02 | 现行 |

## 维护须知

1. 新增文档：先在本文件对应分组的表里加一行，再写正文。
2. 删除文档：从本文件删行，并检查是否有其他文档链接到它。
3. 改名/移动：本文件与其他文档里的链接一起改，然后跑一次链接检查（见下）。
4. 新增目录：目录里必须有 `README.md`，并在本文件里加上该目录的分组表。

链接检查（相对路径是否都指向真实文件）：

```powershell
Get-ChildItem -Recurse -Filter *.md | ForEach-Object {
  $dir = Split-Path $_.FullName
  Select-String -Path $_.FullName -Pattern '\]\(([^)#]+\.md)(#[^)]*)?\)' -AllMatches | ForEach-Object {
    foreach ($m in $_.Matches) {
      $target = Join-Path $dir $m.Groups[1].Value
      if (-not (Test-Path $target)) { "BROKEN: $($_.Path) -> $($m.Groups[1].Value)" }
    }
  }
}
```

## 相关文档

- [文档总入口](README.md)
- [架构总览](architecture/README.md)
- [故障排查](operations/troubleshooting.md)

| Unity 6 升级提示词 | [operations/unity6-upgrade-brief.md](operations/unity6-upgrade-brief.md) | 给外部执行者的完整提示词：约束、顺序、验收、已知坑、交付格式 | 2026-11-02 | 现行 |
| **交接入口** | [operations/handoff-2026-11-11.md](operations/handoff-2026-11-11.md) | 现状、未做完的事、工具链、红线、文档地图——接手先看这篇 | 2026-11-11 | **现行** |
| 条尾三类 | [operations/tail-classes.md](operations/tail-classes.md) | 条与尾谁影响谁：三类定义、唯一违规处、三步执行顺序、已踩过的坑 | 2026-11-11 | 现行 |

| 判定窗口 | [reference/judging.md](reference/judging.md) | 判定帧数表、判定时机、已知差异 | 2026-11-03 | 现行 |