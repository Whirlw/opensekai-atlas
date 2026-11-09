# Open Sekai Atlas

Project Sekai 玩法的学习/研究向复刻工程。当前形态是**轻量播放器**：曲库 → 加载 → 打歌 → 结算 → 回曲库。
谱面编辑器、抽卡、旧页面体系都已整块移除。

**仓库里不含任何原作素材**：代码为 MIT，字体为 OFL（思源黑体），音频、音符皮肤、HUD、判定特效、加载背景全部为自制或 CC0 资源。
来源与替换进度见 [素材来源清单](docs/operations/asset-provenance.md)。

## 开发环境

- Unity: **6000.6.7f1**（Unity 6；从 2022.3 升级，过程见 [升级记录](docs/changelog/2026-11-unity6-upgrade.md)）
- 渲染管线: URP **17.6.0**
- 输入: **仅新版 Input System**（`activeInputHandler = 1`）
- 目标平台: Android（IL2CPP，仅 ARM64，minSdk 26 / targetSdk 36）
- 当前包体: **79.65 MB**（0.3.0）

## 快速开始

1. 用 Unity 6000.6.7f1 打开本目录
2. 打开 `Assets/Game/Scenes/Library.unity`，按 Play
3. 菜单 `OpenSekai/Library/Verify Running Scene` 应输出 `[LibraryCheck] "valid":true`
4. 菜单 `OpenSekai/Library/Play Local Baseline Automatically` 应跑出一首 `1,000,000 / S`

完整步骤见 [环境与运行](docs/getting-started/build-and-run.md)。

## 目录说明

- `Assets/Game/Runtime/`：游戏代码，按 `Sekai`（引擎层）/ `OpenSekai`（本仓库页面）/ `Platform` / `Shared` / `CustomMusicScoreManager` 组织，两个 asmdef 单向依赖。
- `Assets/Game/Scenes/`：`Library.unity`（曲库）、`Core.unity`（打歌）、`TransitionBlank.unity`。
- `Assets/Game/Resources/`：运行时 `Resources` 加载的字体、音符皮肤、界面数据。
- `Assets/Game/AssetBundles/resources/`：AssetBundle 源资源。
- `Assets/StreamingAssets/data/`：打包后的 AssetBundle 与 `AssetBundleInfo.bytes`（构建产物）。
- `Assets/StreamingAssets/sound/`：自制的 42 个判定/演出音 wav 与 `manifest.csv`，启动时注册为外部音源。
- `Assets/Graphics/`：图集、精灵、UI 资源。
- `Tools/`：构建、素材导入与审计脚本（`apply-atlas-art.ps1`、`audit-audio-provenance.ps1`、`verify-resource-paths.ps1` 等）。

## 文档

文档全部在 [`docs/`](docs/README.md)，入口是 [docs/README.md](docs/README.md)，全量清单是 [docs/INDEX.md](docs/INDEX.md)。

| 你想做什么 | 先读 |
| --- | --- |
| 了解项目现状 | [这是什么](docs/getting-started/what-is-this.md) |
| 把工程跑起来 | [环境与运行](docs/getting-started/build-and-run.md) |
| 导入曲包打一首 | [打一首歌](docs/guides/play-a-chart.md) |
| 改代码前了解结构 | [架构总览](docs/architecture/README.md) |
| 出 Android 包 | [Android 构建](docs/operations/android-build.md) |
| 换素材（皮肤/HUD/音频） | [素材包导入](docs/operations/material-pack-import.md) |
| 查某个设置项 | [设置项](docs/reference/live-settings.md) |
| 遇到报错 | [故障排查](docs/operations/troubleshooting.md) |
| 知道某个取舍的原因 | [ADR 索引](docs/decisions/README.md) |

## 与上游的差异

本仓库修改自 [kamcdev/OpenSekai_Community](https://github.com/kamcdev/OpenSekai_Community)，上游已归档停止维护。主要差异：

- 升级到 Unity 6 + URP 17.6，仅新版 Input System
- 高帧率支持（跟随屏幕 / 120 / 144 / 165 / 185 / 240），见 [真机性能](docs/operations/android-performance.md)
- 轻量化重构：删除谱面编辑器与抽卡、收窄页面注册表、AB 白名单、摘除依赖
- 音视频与美术全部替换为自制/CC0；音频改为散装 wav + 运行时注册，不再依赖 CRI ACB
- 包体 117.05 MB → **79.65 MB**

## 资源说明

- **代码**：MIT（见 [LICENSE](LICENSE)）。
- **字体**：Source Han Sans SC，SIL Open Font License 1.1，许可见 `Assets/Game/Resources/Fonts/OFL.txt`。
- **音频**：本项目自制的 42 个 wav（CC0-1.0），逐条署名在 [audio-credits.md](docs/reference/audio-credits.md)。
- **美术**：音符皮肤、HUD、判定特效、加载背景均为自制或程序生成，来源见 [素材来源清单](docs/operations/asset-provenance.md)。

仓库**不包含**原作（Project Sekai）的任何素材。若你在本地用原作资源试玩，那是你自己的行为，不在本仓库的分发范围内。
