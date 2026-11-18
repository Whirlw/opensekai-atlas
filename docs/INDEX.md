# 文档索引

按目录列出 docs 下全部 Markdown。归档、历史设计和变更记录反映当时状态；计划中的待办应先对照当前代码核实。

## 入口

- [Open Sekai Atlas 文档](README.md)

## 快速开始（getting-started/）

- [快速开始](getting-started/README.md)
- [环境与运行](getting-started/build-and-run.md)
- [这是什么](getting-started/what-is-this.md)

## 使用指南（guides/）

- [使用指南](guides/README.md)
- [打一首歌](guides/play-a-chart.md)
- [Android 社区谱面下载](guides/server-chart-downloads.md)

## 架构（architecture/）

- [README.md](architecture/README.md)
- [AssetBundle 与清单](architecture/asset-bundles.md)
- [音频](architecture/audio.md)
- [曲包与谱面数据](architecture/chart-package.md)
- [首页、开屏与品牌标](architecture/home-and-brand.md)
- [服务器页面](architecture/server-screen.md)
- [中间点的四种形态与判定（契约）](architecture/interior-point-kinds.md)
- [判定规则设计](architecture/judging-design.md)
- [打歌运行时](architecture/live-runtime.md)
- [长条：中间点与判定归属（2026-11-10 定稿）](architecture/long-note-interior-spec.md)
- [模块划分](architecture/modules.md)
- [NextRUSH+ 原生谱面加载](architecture/native-next-rush.md)
- [开局数据契约](architecture/play-session.md)
- [页面系统](architecture/screen-system.md)
- [Sonolus 谱面下载接入调研](architecture/sonolus-download.md)

## 运维与构建（operations/）

- [运维与构建](operations/README.md)
- [Android 构建](operations/android-build.md)
- [Android 延迟基线采集](operations/android-latency-p0.md)
- [Android 日志采集](operations/android-logs.md)
- [真机性能](operations/android-performance.md)
- [素材来源清单](operations/asset-provenance.md)
- [音频外部音源包](operations/audio-replacement.md)
- [判定音与演出音的素材来源建议（CC0 / CC BY）](operations/audio-sourcing.md)
- [音效创作与交付说明](operations/audio-spec.md)
- [真机验收清单（判定相关）](operations/device-acceptance-checklist.md)
- [编辑器菜单](operations/editor-menus.md)
- [材质包导入：流程、教训与后续功能设计建议](operations/material-pack-import.md)
- [自己画一套音符皮肤](operations/note-skin-authoring.md)
- [notes.png 约束（音符本体图集）](operations/notes-atlas-spec.md)
- [发布前的素材来源总检](operations/pre-release-asset-audit.md)
- [发布（GitHub 分发）](operations/release.md)
- [截图工具（素材快速预览）](operations/screenshot-tools.md)
- [测试](operations/testing.md)
- [故障排查](operations/troubleshooting.md)

## 开发计划（plans/）

- [开发计划](plans/README.md)
- [OpenSekai Android 延迟优化计划](plans/android-latency-optimization-plan.md)
- [NextRUSH+ 完整谱面支持计划](plans/next-rush-support-plan.md)

## 参考（reference/）

- [参考手册](reference/README.md)
- [audio-credits.md](reference/audio-credits.md)
- [构建宏](reference/build-defines.md)
- [builtin-symbols.md](reference/builtin-symbols.md)
- [字体](reference/fonts.md)
- [术语表](reference/glossary.md)
- [判定窗口](reference/judging.md)
- [设置项](reference/live-settings.md)

## 设计决策（decisions/）

- [ADR-0001：去掉谱面编辑器，只保留打歌](decisions/0001-drop-chart-editor.md)
- [0002-json-ab-catalog.md](decisions/0002-json-ab-catalog.md)
- [ADR-0003：摘掉 uPalette / SoftMask / UI Particle 与视频导出](decisions/0003-drop-ui-packages.md)
- [0004-cumulative-scoring.md](decisions/0004-cumulative-scoring.md)
- [ADR-0005：全新重构，不为历史数据保留兼容](decisions/0005-no-legacy-compat.md)
- [0006 Android 是唯一的发行平台](decisions/0006-android-only.md)
- [0007 时间网格只有一种：一小节 = 1920 tick = 四拍](decisions/0007-one-bar-is-four-beats.md)
- [README.md](decisions/README.md)

## 代码规范（coding-standards/）

- [基本原则](coding-standards/01-principles.md)
- [样式样例（完整单体示例）](coding-standards/02-style-sample.md)
- [命名规范](coding-standards/03-naming.md)
- [事件规范](coding-standards/04-events.md)
- [特性与序列化](coding-standards/05-attributes-serialization.md)
- [区域设置与 .editorconfig](coding-standards/06-regions-editorconfig.md)
- [设计实践：职责、复用、注释、UI 命名](coding-standards/07-design-practices.md)
- [常见代码陷阱（Code Smell）](coding-standards/08-code-smells.md)
- [工具与扩展资源](coding-standards/09-tooling-and-resources.md)
- [Data 与 Model 的职责划分](coding-standards/10-odin-toolkits.md)
- [动画入口规范](coding-standards/11-animation.md)
- [C# 代码规范（Unity）](coding-standards/README.md)
- [外部资料整理](coding-standards/references/README.md)
- [项目变大时怎么组织代码（How to architect code as your project scales）](coding-standards/references/architect-code-that-scales.md)
- [常见 C# 代码约定（Microsoft .NET 文档团队 coding-conventions）整理](coding-standards/references/csharp-coding-conventions.md)
- [CSS 命名规范（BEM）整理](coding-standards/references/css-naming-conventions.md)
- [.NET 框架设计准则（Microsoft Framework Design Guidelines）整理](coding-standards/references/dotnet-design-guidelines.md)
- [Unite Berlin 2018：From Pong to 15 person project（视频）](coding-standards/references/from-pong-to-15-person-project.md)
- [Unity 官方示例 C# 代码风格指南（Unity-Code-Style-Guide 仓库）整理](coding-standards/references/unity-code-style-guide-repo.md)
- [Unity 官方电子书《使用 C# 风格指南编写简洁且可扩展的游戏代码》（Unity 6 版）](coding-standards/references/unity-csharp-style-guide-ebook.md)
- [自定义 Unity 脚本模板（Script Templates）](coding-standards/references/unity-script-templates.md)
- [Unity Test Framework（UTF）手册整理](coding-standards/references/unity-test-framework.md)

## 变更记录（changelog/）

- [2026-11 输入判定收口：中间点窗口、划尾延迟、头体解耦](changelog/2026-11-judging-decoupling.md)
- [2026-11 判定轮：名额、协议、时钟、通道](changelog/2026-11-judgment-round.md)
- [2026-11 曲库管理能力与缺失脚本清理](changelog/2026-11-library-actions.md)
- [2026-11 变速段内的音符取了上一段的速度](changelog/2026-11-note-speed-gap.md)
- [2026-11 重构日志（摘要）](changelog/2026-11-refactor-log.md)
- [2026-11 发布前清理：素材整理、去抽卡、体积 117 → 79.7 MB](changelog/2026-11-release-cleanup.md)
- [Unity 6 升级执行记录](changelog/2026-11-unity6-upgrade.md)
- [变更记录](changelog/README.md)

## 历史设计（history/）

- [方案：结论、起点与边界](history/plan/01-goals-and-boundaries.md)
- [方案：目标结构与数据契约](history/plan/02-target-structure.md)
- [方案：谱面兼容](history/plan/03-chart-compatibility.md)
- [方案：启动、页面、Live 与时间](history/plan/04-startup-pages-live.md)
- [方案：资源、依赖与用户数据](history/plan/05-resources-deps-data.md)
- [方案：阶段、清单、验收与风险](history/plan/06-phases-and-acceptance.md)
- [轻量化重构方案（设计稿索引）](history/player-lite-plan.md)

## 历史归档（archive/）

- [历史归档](archive/README.md)
- [Android acceptance probes, November 2026](archive/android-acceptance-2026-11/README.md)
- [判定与长条任务归档](archive/judging-2026-11/README.md)
- [自动播放先于音符行走判定（DEINOS PHAINEIN 掉血）](archive/judging-2026-11/autoplay-frame-order.md)
- [交接（判定解耦）](archive/judging-2026-11/handoff-2026-11-11.md)
- [head-decoupling-report.md](archive/judging-2026-11/head-decoupling-report.md)
- [长条头与条体解耦（交给外部实现者的任务书）](archive/judging-2026-11/head-decoupling.md)
- [interior-point-window-report.md](archive/judging-2026-11/interior-point-window-report.md)
- [中间点的判定窗口（交给外部实现者的任务书）](archive/judging-2026-11/interior-point-window.md)
- [judgement-input-split.md](archive/judging-2026-11/judgement-input-split.md)
- [judgment-channel-brief.md](archive/judging-2026-11/judgment-channel-brief.md)
- [判定内核纯函数化：交给外部执行者的任务书](archive/judging-2026-11/judgment-core-brief.md)
- [live-judging-remaining.md](archive/judging-2026-11/live-judging-remaining.md)
- [任务 A 完成报告](archive/judging-2026-11/live-judging-task-a-report.md)
- [任务 B 完成报告](archive/judging-2026-11/live-judging-task-b-report.md)
- [任务 C 报告：护栏发现同段计算错误，停止并回退](archive/judging-2026-11/live-judging-task-c-report.md)
- [LiveLogic 现状依赖图与分层拆分计划](archive/judging-2026-11/live-logic-refactor.md)
- [双长条断连：交给外部执行者的任务书](archive/judging-2026-11/long-hold-brief.md)
- [长条第三轮交接（2026-11-03，接手先读此文件）](archive/judging-2026-11/long-hold-v3-handoff.md)
- [长条头彻底解耦（C 方案）](archive/judging-2026-11/long-note-head-full-decoupling.md)
- [long-note-head-split-report.md](archive/judging-2026-11/long-note-head-split-report.md)
- [任务：把长条的「判定」从条上拆下来（部件 1）](archive/judging-2026-11/long-note-head-split.md)
- [owner-removal-and-release-tail.md](archive/judging-2026-11/owner-removal-and-release-tail.md)
- [条尾三类：谁影响谁](archive/judging-2026-11/tail-classes.md)
- [tail-key-lifetime-brief.md](archive/judging-2026-11/tail-key-lifetime-brief.md)
- [… 按下方三步实现 …](archive/judging-2026-11/touch-source-brief.md)
- [Open Sekai Atlas 结构重整计划书](archive/restructuring-2026-11/restructure-plan-2026-11.md)
- [Unity 6 升级归档](archive/unity6-upgrade/README.md)
- [Android 项目从 Unity 2022.3.62f3 升级至 Unity 6000.6.7f1 详细指南](archive/unity6-upgrade/Unity-2022.3.62f3-to-6000.6.7f1-Android-升级指南.md)
- [unity6-upgrade-brief.md](archive/unity6-upgrade/unity6-upgrade-brief.md)
- [Unity 6 升级清单](archive/unity6-upgrade/unity6-upgrade.md)
