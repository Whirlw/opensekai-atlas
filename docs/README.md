# OpenSekai 文档

> 目的：文档总入口。说明这个项目是什么、该按什么顺序读、每类问题去哪个目录找。
> 适用读者：所有人（新成员优先读「先读这三篇」）。
> 前置条件：无。
> 最后更新：2026-11-02

## 概述

这是一个 Project Sekai 玩法的学习/研究向复刻工程，当前处于「轻量播放器」形态：
**只有曲库 → 加载 → 打歌 → 结算 → 回曲库这条链路，谱面编辑器已整块移除**。

- 本分支引擎：Unity 6000.6.7f1，URP 17.6.0。编辑器与 Android 真机门禁均已通过（测试谱 221 音符，结算 `1,000,000 / S`），APK 约 113–119 MB；详见 [升级清单](operations/unity6-upgrade.md) 与 [执行记录](changelog/2026-11-unity6-upgrade.md)。
- 分发方式是纯开源：只从 GitHub 发布 APK 与源码，不上架应用商店，因此不需要 Play 专用配置（AAB、Play Core、商店签名流程）。
- 构建场景：`Assets/Game/Scenes/Library.unity` → `Assets/Game/Scenes/TransitionBlank.unity` → `Assets/Game/Scenes/Core.unity`
- 目录：`Assets/Game/`（运行时与编辑器代码、场景、曲包资源）／`Assets/Graphics/`（美术资源）／`Assets/Vendor/`（第三方代码）／`Assets/Plugins/`、`Assets/StreamingAssets/`、`Assets/TextMesh Pro/`

## 快速开始

先读这三篇，够你把工程跑起来并打一首歌：

1. [这是什么](getting-started/what-is-this.md) —— 现在有什么、没有什么
2. [环境与运行](getting-started/build-and-run.md) —— Unity 版本、场景、验证判据
3. [打一首歌](guides/play-a-chart.md) —— 曲包放哪、怎么导入、结算看什么

只想验证「工程没坏」：

```text
Play → 菜单 OpenSekai/Library/Verify Running Scene → [LibraryCheck] "valid":true
     → 菜单 OpenSekai/Library/Play Local Baseline Automatically → 1,000,000 / S
```

## 文档地图

```text
docs/
  README.md                  ← 你在读的总入口
  INDEX.md                   全量索引（标题 / 路径 / 摘要 / 最后更新）
  getting-started/           安装、首次运行、验证判据
  guides/                    使用指南（打歌流程）
  architecture/              模块、数据流、关键设计
  operations/                构建、真机性能、编辑器菜单、故障排查
  decisions/                 ADR：为什么这样取舍
  reference/                 设置项、宏、术语；legacy/ 放历史文档
  coding-standards/          C# / Unity 代码规范（命名、事件、序列化、Review 清单）
  changelog/                 按时间排的施工记录
  images/                    文档引用的截图与证据图
```

### 按「我想做什么」找

| 我想…… | 去这里 |
| --- | --- |
| 把工程跑起来 | [环境与运行](getting-started/build-and-run.md) |
| 导入一首歌并打 | [打一首歌](guides/play-a-chart.md) |
| 改曲库界面 / 加面板 | [模块划分](architecture/modules.md) + [编辑器菜单](operations/editor-menus.md) |
| 改判定、计分、时间轴 | [打歌运行时](architecture/live-runtime.md) + [ADR-0004](decisions/0004-cumulative-scoring.md) |
| 改曲包格式、读谱面 | [曲包与谱面数据](architecture/chart-package.md) |
| 打 AB 包、出 APK | [AssetBundle 与清单](architecture/asset-bundles.md) + [Android 构建](operations/android-build.md) |
| 查某个设置项 | [设置项](reference/live-settings.md) |
| 写新代码 / 起名 / Code Review | [代码规范](coding-standards/README.md) |
| 排查「为什么打不开/卡住」 | [故障排查](operations/troubleshooting.md) |
| 知道某个改动为什么这样做 | [ADR 索引](decisions/README.md) 或 [重构日志](changelog/2026-11-refactor-log.md) |

## 文档约定

- 一篇只讲一件事；长内容拆成多篇并互相链接。
- 每篇开头固定四行：目的、适用读者、前置条件、最后更新。
- 代码、命令、配置一律放代码块并标注语言。
- 链接一律相对路径，且必须指向真实存在的文件。
- 不确定的内容写 `TODO: 待确认`，不臆测接口与参数。
- 新增或改动文档后同步更新 [INDEX.md](INDEX.md)；影响项目入口时再改根 `README.md` 的链接。
- **缺工具就说，不要硬凑**：做某一步工作时如果需要新的工具、插件或软件（编译器、SDK、命令行工具、Unity 包等），立刻告诉用户去装，并说明装什么、哪个版本、大概多大；不要在缺工具的情况下用半成品替代方案糊过去。装好后把结论记进本文档的「本机已装工具」。

## 本机已装工具

清单只记「工作流真的会用到」的东西；版本随手更新。

| 工具 | 版本 | 用途 |
| --- | --- | --- |
| Unity | 6000.6.7f1；2022.3.62f3 保留 | 新版本 Android Build Support、SDK build-tools 36.0.0、NDK 27.2.12479018、OpenJDK 已装；旧版用于回滚 |
| Unity CLI | 1.0.0-beta.15 | 通过 Pipeline 0.9.1-exp.1 控制 Unity 6（`com.coplaydev.unity-mcp` 已于 2026-11-02 移除） |
| .NET SDK | 8.0.427 | 跑 `Tools/RoslynRename`（语义级改名的 Roslyn 小工具） |
| adb | 随 Unity 的 Android SDK | 真机安装、截图、logcat 验证 |
| git | 系统自带 | 升级分支 `upgrade/unity6`；原分支 `codex/player-lite-refactor` 保留 |

`dotnet build` 需要写 `%APPDATA%\NuGet` 与 `~/.dotnet`，在沙箱里要用提权模式跑；工具编译一次后直接用 `Tools/RoslynRename/bin/Debug/net8.0/RoslynRename.exe`。

## 常见问题与故障排查

- 文档与代码冲突：以代码为准，并在 [INDEX.md](INDEX.md) 里标注该文件为「历史」。
- 找不到想要的内容：先看 [INDEX.md](INDEX.md) 的摘要列。

## 相关文档

- [全量索引](INDEX.md)
- [这是什么](getting-started/what-is-this.md)
- [架构总览](architecture/README.md)
- [ADR 索引](decisions/README.md)
- [故障排查](operations/troubleshooting.md)
