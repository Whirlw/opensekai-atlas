# 架构

> 目的：说明这套代码为什么这样分层、数据从曲包流到画面经过哪些环节，供改代码的人定位。
> 适用读者：开发者。
> 前置条件：读过 [这是什么](../getting-started/what-is-this.md)。
> 最后更新：2026-11-11

## 概述

运行时由两个程序集组成。

- `OpenSekai.App` 位于 `Assets/Game/Runtime/OpenSekai/`，负责曲库入口、页面、开局和谱面载入。
- `OpenSekai.Game` 覆盖 `Assets/Game/Runtime/` 中 App 之外的运行时代码，负责页面栈、判定、音频、资源加载和共享数据。
- `OpenSekai.Editor` 位于 `Assets/Game/Editor/`，提供 AssetBundle 构建、场景校验和测试菜单。

## 详细说明

| 文档 | 内容 |
| --- | --- |
| [模块划分](modules.md) | 目录职责、依赖方向、`Assets` 下剩余代码的归属 |
| [曲包与谱面数据](chart-package.md) | `.oskp` 结构、`ChartDocument`、兼容旧类型名的 binder |
| [打歌运行时](live-runtime.md) | 场景切换、时钟、输入回填、判定、计分、结算 |
| [开局数据契约](play-session.md) | `PlaySession` / `RuntimeChartDefinition`，以及它们与 `FreeLiveBootData` 的关系 |
| [页面系统](screen-system.md) | `ScreenManager` 的三个注册页面与独立的曲库页 |
| [AssetBundle 与清单](asset-bundles.md) | 11 个白名单条目、0.3.0 的 10 个实际 bundle 与 JSON 清单 |
| [音频](audio.md) | 曲包音频、外部 WAV、`CriAtomShim` 和播放时钟 |
| [轻量化方案（历史设计稿）](../history/player-lite-plan.md) | 第一轮播放器精简的设计与阶段划分 |
| [结构重整计划（2026-11）](restructure-plan-2026-11.md) | 结构基线、程序集与目录迁移、测试和发布验收 |

## 相关文档

- [ADR 索引](../decisions/README.md)
- [参考手册](../reference/README.md)
- [运维与构建](../operations/README.md)
