# 架构

> 目的：说明这套代码为什么这样分层、数据从曲包流到画面经过哪些环节，供改代码的人定位。
> 适用读者：开发者。
> 前置条件：读过 [这是什么](../getting-started/what-is-this.md)。
> 最后更新：2026-11-03

## 概述

代码分三层：

1. **新代码层** `Assets/OpenSekai/` —— 曲库界面、开局服务、谱面读取、包管理、设置存储。按职责分 `App / Charts /
   Infrastructure / Live / Editor`。
2. **引擎层** `Assets/Scripts/Assembly-CSharp/` —— 反编译得到的原游戏逻辑（页面系统、live 判定、音频、UI 组件）。
   本次重构没有重写它，只删掉与编辑器相关的部分。
3. **编辑器/工具层** `Assets/Editor/`、`Assets/OpenSekai/Editor/` —— AssetBundle 构建、场景校验菜单，不进播放器。

## 详细说明

| 文档 | 内容 |
| --- | --- |
| [模块划分](modules.md) | 目录职责、依赖方向、`Assets` 下剩余代码的归属 |
| [曲包与谱面数据](chart-package.md) | `.oskp` 结构、`ChartDocument`、兼容旧类型名的 binder |
| [打歌运行时](live-runtime.md) | 场景切换、时钟、输入回填、判定、计分、结算 |
| [开局数据契约](play-session.md) | `PlaySession` / `RuntimeChartDefinition`，以及它们与 `FreeLiveBootData` 的关系 |
| [页面系统](screen-system.md) | `ScreenManager` 注册表、7 个可达页面、为什么只剩 7 个 |
| [AssetBundle 与清单](asset-bundles.md) | 15 个白名单 bundle、JSON 清单、`StreamingAssets` 平铺 |
| [音频](audio.md) | `CriAtomShim`、`AcbDecoder`、为什么时钟不用 `dspTime` |
| [轻量化方案（历史设计稿）](player-lite-plan.md) | 重构前的目标结构与阶段划分，末尾列出与现状的偏差 |

## 相关文档

- [ADR 索引](../decisions/README.md)
- [参考手册](../reference/README.md)
- [运维与构建](../operations/README.md)
