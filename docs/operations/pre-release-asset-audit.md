# 发布前的素材来源总检

> 目的：在把仓库推到 GitHub、把 APK 发出去之前，用可复现的方式回答两个问题——**每一类素材的来源与许可是否清楚**、**图集是否都已被画包覆盖**。这篇给出检查方法、当前结论与剩余缺口。
> 适用读者：准备发版的人、接手素材维护的人。
> 前置条件：读过 [素材来源清单](asset-provenance.md)、[材质包导入](material-pack-import.md)、[音频外部音源包](audio-replacement.md)、[音频来源建议](audio-sourcing.md)。
> 最后更新：2026-11-09

## 结论速览

| 类别 | 状态 | 证据 |
| --- | --- | --- |
| 代码 | **MIT**，无第三方源码 | 全仓 `.cs` 为本项目自写；`Assets/Vendor` 仅保留可按许可分发的第三方运行库 |
| 字体 | **OFL** | `Assets/Game/Resources/Fonts` 为 Source Han Sans SC（OFL-1.1），已随附许可 |
| 音频 | **全自制 CC0** | 42 个 wav（`Assets/StreamingAssets/sound`），`docs/reference/audio-credits.md` 42/42 行，`Tools/audit-audio-provenance.ps1` 报 `AUDIT OK`；工程内 `.acb` 数量 **0** |
| 音符皮肤两套 | **自制** | `Assets/Game/Resources/live/note/custom01|custom02`，来自画包 `new_notes` / `new_notes_v2`，两套哈希不同 |
| HUD / 判定字 / 特效 / 背景 | **自制** | 加载背景与通用背景为程序生成；图集精灵画包覆盖 **144 / 153**，9 个通用控件待补（下表） |
| 已删除的遗留资源 | 见「本轮删除」 | — |

APK：**84.26 MB**（本轮清理前 117.05 MB，−32.8 MB）。

## 检查方法（三条命令）

```powershell
# 1) 素材分组与来源表（人工维护的许可列）
powershell -File Tools\audit-asset-provenance.ps1

# 2) 音频逐文件合规
powershell -File Tools\audit-audio-provenance.ps1      # 期望结尾 AUDIT OK

# 3) 图集覆盖率：多少精灵已进画包 / 其中多少根本没人引用
powershell -File Tools\audit-atlas-coverage.ps1
powershell -File Tools\audit-unused-atlas-sprites.ps1
```

判定口径：

- **画包覆盖** = `LocalPacks/paint-kit/{new_hud,new_effects,new_notes,new_notes_v2,new_backgrounds}` 里存在同名 png。有同名文件即视为已重绘（导入流程会把它们贴进图集）。
- **未被引用** = 全仓 `*.prefab/*.unity/*.mat/*.asset` 里都搜不到该精灵的 GUID。这类精灵不渲染，删掉不影响游戏。
- 这两项都是**覆盖率统计**，不用于判断版权；版权归口以 `asset-provenance.md` 的来源表与 `audio-credits.md` 的逐条署名为准。

## 当前覆盖（153 精灵 / 144 已覆盖 / 9 待补）

| 图集 | 精灵 | 画包覆盖 | 未覆盖 | 其中未被引用 |
| --- | ---: | ---: | ---: | ---: |
| JudgeAtlas | 9 | 9 | 0 | 0 |
| CommonAtlas（第 0 页） | 8 | 8 | 0 | 0 |
| CommonAtlas（第 1 页） | 52 | 43 | 9 | 0 |
| LiveAtlas | 84 | 84 | 0 | 0 |
| **合计** | **153** | **144** | **9** | **0** |

读法：未覆盖的 9 个都在 CommonAtlas，是通用 UI 图标（`eff_btn_circle_*`、`gauge_*`、`icon_*` 等）。它们全部仍被 prefab 引用，必须补齐而不是删除。

处置顺序建议：

1. **先补画包**：按 `new_hud` 的命名规则把 9 个通用控件补进画包（同名即可），跑一次 `apply-atlas-art.ps1` 就覆盖。
2. **再核对未引用项**：当前未被引用数为 0；若以后出现非零，用 `audit-unused-atlas-sprites.ps1` 复核后删除。
3. **最后跑全量命令**，确认表格回到「未覆盖 0」。

## 本轮删除

| 项 | 体积 | 理由 |
| --- | ---: | --- |
| `Assets/Graphics/Resources/maimusicinfo.ogg` + `PlayMaimaiMusicInfoSe()` | 130 KB | 另一款游戏的音效，功能默认关闭，属于遗留 |
| `MusicScoreMakerAtlas` + 149 个精灵 | 0.91 MB | 谱面编辑器已整体移除，精灵 0 引用 |
| `FrameAtlas` + 5 个精灵（`cardFrame_S_*`） | 0.23 MB | 卡框，0 引用 |
| 4 个 ACB（custom01/custom02/se_live_default/se_live_common） | 1.41 MB | 由自制 wav 包取代 |
| 6 张 `longLoading_bg_*.png` + `bg_common_color.png` | 12.7 + 3.9 MB → 0.4 MB | 换成程序生成的同尺寸背景 |
| 3 张 MenuCommon 音频表 | 9.35 MB | UI 音从未触发 |

## 复检清单（每次发版跑一遍）

1. `git status --porcelain` 只剩 Unity 编辑器噪音文件（`UniversalRenderPipelineGlobalSettings`、`GraphicsSettings`、`codecoverage`、`ProjectAuditorSettings`、`ai.assistant`）。
2. 上面四条命令全部通过；`audit-atlas-coverage.ps1` 的「未覆盖」等于 0 或与本文表格一致（说明没有新增未覆盖素材）。
3. `Assets/**` 下没有 `.acb`、`.acb.bytes`、`.ogg`（除自己产出）、`.mp4`。
4. `LocalPacks/` 不进 git（`.gitignore` 已忽略），但发版机需要它才能复现素材。
5. 出包后装机，打一首歌，确认 HUD / 判定字 / 两套特效 / 两套音符皮肤 / 结算横幅都正常。

## 已知例外（不在覆盖统计内）

- `Assets/Vendor/*` 中的第三方运行库（如 ZString、Unity 包）按各自许可分发。
- 用户自建曲包（`CustomMusicScores`）与用户导入的封面图属于用户数据，不进仓库。
