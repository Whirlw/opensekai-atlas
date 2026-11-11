# 环境与运行

> 目的：给出把工程跑起来、并在编辑器里验证一次完整打歌流程的最小步骤。
> 适用读者：开发者、新成员。
> 前置条件：已克隆仓库，机器上装有 Unity Hub。
> 最后更新：2026-11-11

## 概述

工程是标准的 Unity 项目，没有自定义构建脚本入口之外的前置步骤。首次打开只需两步：用对的 Unity 版本打开，
等资源导入和脚本编译完成；之后按 Play 就会进曲库页。

## 核心内容

| 项目 | 值 | 出处 |
| --- | --- | --- |
| Unity 版本 | `6000.6.7f1` | `ProjectSettings/ProjectVersion.txt` |
| 渲染管线 | URP 17.6.0 | `Packages/manifest.json` |
| 脚本后端 | 编辑器用 Mono；Android 用 IL2CPP | `ProjectSettings/ProjectSettings.asset` |
| 版本号 | `0.3.0`（versionCode 84） | 同上 |
| Android SDK | minSdk 26，targetSdk 随构建环境选择（0.3.0 发布包为 36） | 同上 |
| 构建场景（按序） | `Assets/Game/Scenes/Library.unity` → `Assets/Game/Scenes/TransitionBlank.unity` → `Assets/Game/Scenes/Core.unity` | `ProjectSettings/EditorBuildSettings.asset` |
| 首场景入口组件 | `Assets/Game/Runtime/OpenSekai/App/Bootstrap/LibraryEntryPoint.cs` | 场景内挂载 |

## 详细说明

### 首次打开

1. Unity Hub 添加本目录，选择 **6000.6.7f1** 打开。
2. 等待包解析与脚本编译。控制台最后一条编译时间之后不应再出现 `error CS`。
3. 打开 `Assets/Game/Scenes/Library.unity`，按 Play。

### 验证「跑通了」的三条判据

```text
① 编辑模式，场景 Library 的入口对象上只有 LibraryEntryPoint（没有 MusicScoreMakerEntryPoint，该类已删除）
② Play 后日志出现：[LibraryScreen] CJK font: NotoSansSC-UI
③ 菜单 OpenSekai/Library/Verify Running Scene 打出的 [LibraryCheck] 里 "valid":true
```

`[LibraryCheck]` 的真实样本：

```json
[LibraryCheck] {"valid":true,"scene":"Library","route":"Library","frameCount":835,"libraryEntries":1,
"editorEntries":0,"lightweightLibraryScreens":1,"eventSystems":1,"isStartingPlayback":false}
```

### 出包

- Android：见 [Android 构建](../operations/android-build.md)。这是唯一的发行平台。
- Windows 只作为开发时的编辑器宿主，不再产出 Player 构建（见 [决策 0006](../decisions/0006-android-only.md)）

## 示例

完整跑一遍自动打歌（不需要外部曲包，用仓库内合成样本）：

```text
1. 进入 Play 前，可用菜单 OpenSekai/Dev/Install Test Chart 把 Tools/fixtures/baseline_tap 装进玩家目录
2. Play
3. 菜单 OpenSekai/Library/Play Local Baseline Automatically（优先使用已安装的同 ID 曲包，随后查找仓库样本）
4. 等待约 60 秒（221 音符 / 120 BPM / 9 秒前奏）
5. 结果面板显示 1,000,000 / S / 总音符 221 · 自动判定 221 / 完成
```

## 常见问题与故障排查

- **控制台有 `error CS`**：先看行号是否在最后一次 `CompileScripts` 之后；`Editor.log` 里会残留历史错误行。
  完整日志在 `%LOCALAPPDATA%\Unity\Editor\Editor.log`。
- **Play 后画面全白**：曲库页是 `ScreenSpaceCamera` 画布，用 `LibraryScreenCamera` 渲染；空场景或旧场景会白屏。
- **资产库损坏 / 启动弹 `Opening file ... 系统找不到指定的路径`**：见 [故障排查](../operations/troubleshooting.md)。

## 相关文档

- [这是什么](what-is-this.md)
- [打一首歌](../guides/play-a-chart.md)
- [编辑器菜单](../operations/editor-menus.md)
- [Android 构建](../operations/android-build.md)
