# 架构决策记录（ADR）

> 目的：记录「为什么不这样做」的决策与代价，避免后来者反复推翻。
> 适用读者：开发者。
> 前置条件：读过 [架构总览](../architecture/README.md)。
> 最后更新：2026-11-02

## 概述

本目录只放**已经生效**的决策，一条一个文件，编号递增、不复用。每条包含：背景、决定、后果（含代价）、
被否决的替代方案。改主意时不要删旧记录，新建一条并把旧的标成「被取代」。

## 详细说明

| 编号 | 标题 | 状态 |
| --- | --- | --- |
| [0001](0001-drop-chart-editor.md) | 去掉谱面编辑器，只保留打歌 | 已生效 |
| [0002](0002-json-ab-catalog.md) | AssetBundle 清单从 MessagePack 改为 JSON | 已生效 |
| [0003](0003-drop-ui-packages.md) | 摘掉 uPalette / SoftMask / UI Particle 与视频导出 | 已生效 |
| [0004](0004-cumulative-scoring.md) | 计分改为累积摊分，满判严格等于 1,000,000 | 已生效 |
| [0005](0005-no-legacy-compat.md) | 全新重构，不为历史数据保留兼容 | 已生效 |
| [0006](0006-android-only.md) | Android 是唯一的发行平台 | 已生效 |

## 相关文档

- [架构总览](../architecture/README.md)
- [重构日志](../changelog/2026-11-refactor-log.md)
