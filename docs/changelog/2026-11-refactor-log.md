# 2026-11 重构日志（摘要）

清死代码与轻量化重构的逐日流水，原本有 48 KB。**完整过程只保留在本地**（`LocalPacks/archive/docs/changelog/2026-11-refactor-log.md`），仓库里只留这份摘要：提交历史本身已经记录了每一步。

## 这一段做了什么

| 方向 | 结果 | 关键提交 |
| --- | --- | --- |
| 页面注册表 230 → 7 | 只保留曲库与打歌两个场景真正用到的层 | — |
| 删除谱面编辑器 | `MusicScoreMaker` 场景、Ingame/OutGame 527 脚本、1214 项资产 | — |
| 曲库功能内置 | `LibraryScreen` + 设置面板 + 结算面板替换旧曲库 UI | — |
| 计分改累积摊分 | 任意音符数总分恰为 1,000,000（`RulesVersion` 2） | — |
| Android-only | 删除 `StandaloneFileBrowser` 与 Windows 导出路径（ADR-0006） | — |
| 依赖清理 | 视频导出、uPalette、SoftMask、UI Particle、MessagePack、Timeline、CRI 未用资源 | — |
| 判定链路重写 | 判定链路拆成纯函数并单测：`JudgeWindow`、`ScoreGauge`、`FlickGesture`、`TouchLane` 等 | — |
| 素材替换 | 音符皮肤、HUD、判定特效、音频全部换为自制/CC0（见 `operations/pre-release-asset-audit.md`） | — |

## 为什么不再逐条记录

流水日志的价值在过程，而过程在 git 历史里逐提交可见；仓库里保留摘要，既省体积也不误导后来者去读一份已经过期的流水。

## 相关

- [2026-11 Unity 6 升级](2026-11-unity6-upgrade.md)
- [2026-11 判定轮次](2026-11-judgment-round.md)
- [素材来源清单](../operations/asset-provenance.md)