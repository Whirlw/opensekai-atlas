# 运维与构建

> 目的：收录「把工程变成可分发产物、并在真机/桌面上验证」相关的文档。
> 适用读者：开发者、打包与测试人员。
> 前置条件：读过 [环境与运行](../getting-started/build-and-run.md)。
> 最后更新：2026-11-03

## 概述

本目录只讲操作与排障，不讲设计。设计原因见 [架构](../architecture/README.md) 与 [ADR](../decisions/README.md)。

## 详细说明

| 文档 | 解决的问题 |
| --- | --- |
| [Android 构建](android-build.md) | 工具链、Player Settings、出包步骤、三个必踩的坑、体积取舍 |
| [发布（GitHub 分发）](release.md) | 版本号与 tag、公开 keystore、校验和、release notes 模板、覆盖安装注意事项 |
| [真机性能](android-performance.md) | 165 Hz 配置、帧率采集、已知的系统级限制 |
| [编辑器菜单](editor-menus.md) | 每个菜单做什么、什么时候用 |
| [故障排查](troubleshooting.md) | Unity 卡死/资产库损坏/MCP 断线/adb 截图等 |
| [测试](testing.md) | 两层测试、桥接菜单与 unity test、报告位置、待补清单 |
| [触摸源抽象任务书](touch-source-brief.md) | 交给外部执行者的自包含任务书：三步实现、约束、跑法、验收标准 |
| [双长条断连任务书](long-hold-brief.md) | 交给外部执行者的自包含任务书：现象、失败尝试、判定管线、验收标准 |
| [判定内核纯函数化任务书](judgment-core-brief.md) | 交给外部执行者的自包含任务书：四个任务的现状、约束、验收与报告格式 |
| [判定通道与解耦收尾](judgment-channel-brief.md) | 任务 B 的执行计划：通道枚举、三个尾实现的绞杀式替换、视图离开 State、池卫生与时间单一真源；§5.2「三条通道不要混用」、§5.3「类别在转换层被改写」、§5.4「抬手归属」是 2026-11-07 下午三个故障的定稿结论 |
| [尾键生命周期任务书](tail-key-lifetime-brief.md) | 交给外部执行者的自包含任务书：把尾键显示/回收从 `State` 与六个开关里解耦，收敛成纯函数 |
| [素材来源清单](asset-provenance.md) | 发布任何二进制之前的第三方素材清单（约 36 MB 待换）、同 GUID 重画的手法、替换顺序与进度表；数字用 `Tools/audit-asset-provenance.ps1` 刷新 |
| [材质包导入](material-pack-import.md) | 把画好的材质包贴回工程：图集切片与散图两条路径、六条硬规则、已踩的九个坑、像素级校验、一键导入功能的设计建议 |
| [音频素材替换](audio-replacement.md) | 7 个 ACB 负载（约 11 MB / 359 个 cue）的清单与加载点、音频规格；路线 A 散装 wav + 运行时注册，路线 B 重做 ACB |
| [音频来源建议](audio-sourcing.md) | 判定音/演出音该去哪找：自制合成优先，CC0（Kenney / Freesound / Wikimedia）次之，CC BY 需署名；附 cue→取材映射与合规自检 |
| [音频需求表](audio-spec.md) | 对外交付用的自包含需求书：42 个 wav 的逐条规格（时长/声道/音色/响度）、两套皮肤差异规则、自检命令与交付结构 |
| [Unity 6 升级清单](unity6-upgrade.md) | 升级到 6000.6.7f1 的现状对照、准备、执行与验收 |
| [Unity 6 升级提示词](unity6-upgrade-brief.md) | 交给外部执行者的自包含提示词与交付格式 |

## 相关文档

- [文档总入口](../README.md)
- [全量索引](../INDEX.md)
- [AssetBundle 与清单](../architecture/asset-bundles.md)
| [音符皮肤制作](note-skin-authoring.md) | 自己画一套音符皮肤：`custom01`/`custom02` 目录结构、像素尺寸硬约束表、`-From` 导入校验与出包步骤 |
