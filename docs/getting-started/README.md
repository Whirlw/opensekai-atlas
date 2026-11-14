# 快速开始

> 目的：让第一次接触本仓库的人在三篇文档内跑起来——知道这是什么、把工程打开跑通、打一首歌。
> 适用读者：新成员、开发者。
> 前置条件：已克隆仓库；装有 Unity 2022.3.62f3（见 [环境与运行](build-and-run.md)）。
> 最后更新：2026-11-02

## 概述

本目录只放「最短路径」文档，不展开设计与取舍。想了解为什么这样拆分，去 [架构](../architecture/README.md)；
想知道某个配置项的含义，去 [参考](../reference/README.md)。

阅读顺序：

1. [这是什么](what-is-this.md) —— 项目定位、当前能力和明确不做的事
2. [环境与运行](build-and-run.md) —— Unity 版本、场景、首次启动、出包
3. [打一首歌](../guides/play-a-chart.md) —— 导入曲包、开局、结算

## 详细说明

| 文档 | 解决的问题 |
| --- | --- |
| [这是什么](what-is-this.md) | 这个仓库现在是什么、还剩什么、不做什么 |
| [环境与运行](build-and-run.md) | 用什么 Unity、开哪个场景、怎么验证跑通 |
| [打一首歌](../guides/play-a-chart.md) | 曲包从哪来、怎么导入、打完看到什么 |

## 示例

最小验证路径（编辑器内，不需要任何外部曲包）：

```text
1. Unity 打开 Assets/Game/Scenes/Library.unity
2. 按 Play
3. 菜单 OpenSekai/Library/Play Local Baseline Automatically
4. 等自动打歌跑完，应看到结果面板 1,000,000 / S / 完成
```

## 常见问题与故障排查

- 按 Play 后画面全白、看不到界面：确认打开的是 `Library.unity` 而不是空场景；曲库页自带 `LibraryScreenCamera`。
- 菜单点不动、Unity 卡住：见 [故障排查](../operations/troubleshooting.md)。

## 相关文档

- [文档总入口](../README.md)
- [全量索引](../INDEX.md)
- [架构总览](../architecture/README.md)
- [运维与构建](../operations/README.md)
