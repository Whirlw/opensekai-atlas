# 发布前的素材来源总检

> 目的：在把仓库推到 GitHub、把 APK 发出去之前，用可复现的方式回答两个问题——**每一类素材的来源与许可是否清楚**、**图集是否都已被画包覆盖**。这篇给出检查方法、当前结论与剩余缺口。
> 适用读者：准备发版的人、接手素材维护的人。
> 前置条件：读过 [素材来源清单](asset-provenance.md)、[材质包导入](material-pack-import.md)、[音频外部音源包](audio-replacement.md)、[音频来源建议](audio-sourcing.md)。
> 最后更新：2026-11-12

## 结论速览

| 类别 | 状态 | 证据 |
| --- | --- | --- |
| 代码 | **MIT**，无第三方源码 | 全仓 `.cs` 为本项目自写；`Assets/Vendor` 仅保留可按许可分发的第三方运行库 |
| 字体 | **OFL** | `Assets/Game/Resources/Fonts` 为 Source Han Sans SC（OFL-1.1），已随附许可 |
| 音频 | **全自制 CC0** | 42 个 wav（`Assets/StreamingAssets/sound`），`docs/reference/audio-credits.md` 42/42 行，`Tools/audit-audio-provenance.ps1` 报 `AUDIT OK`；工程内 `.acb` 数量 **0** |
| 音符皮肤两套 | **自制** | `Assets/Game/Resources/live/note/custom01|custom02`，来自画包 `new_notes` / `new_notes_v2`，两套哈希不同 |
| HUD / 判定字 / 特效 / 背景 | **自制** | 加载背景与通用背景为程序生成；图集精灵画包覆盖 **152 / 153**，剩 1 个（`bg_base_circle_h48_wh`）由同名带后缀的画包文件覆盖，属统计口径问题而非缺口（见下表脚注） |
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

## 当前覆盖（153 精灵 / 152 已覆盖 / 1 口径例外）

| 图集 | 精灵 | 画包覆盖 | 未覆盖 | 其中未被引用 |
| --- | ---: | ---: | ---: | ---: |
| JudgeAtlas | 9 | 9 | 0 | 0 |
| CommonAtlas（第 0 页） | 8 | 8 | 0 | 0 |
| CommonAtlas（第 1 页） | 52 | 51 | 1 | 0 |
| LiveAtlas | 84 | 84 | 0 | 0 |
| **合计** | **153** | **152** | **1** | **0** |

读法：剩的 1 个是 `bg_base_circle_h48_wh`。它的画包文件叫 `bg_base_circle_h48_wh_1.png`（作者按精灵资产文件名命名），而统计口径按**精灵名**匹配，所以算作未覆盖；`apply-atlas-art.ps1` 会经文件名回退找到它并正常贴图，实测该精灵的图集像素与画包一致。要消掉这一行，把画包文件改名成 `bg_base_circle_h48_wh.png` 即可。

2026-11-12 补齐记录：原先 9 个未覆盖的通用控件（`badge_attention_rd`、`bg_base_circle_h48_wh`、`bg_base_r8/r10/r16_wh`、`btn_circle_h56/h80_wh`、`icon_menu_wh`、`icon_triangle_wh`）都是**白色可染底片**（圆角矩形/圆/方块），已用 `Tools/draw-ui-standins.ps1` 按精灵名重新生成为替身并贴进图集；生成清单留在 `docs/reference/remaining-uncovered-sprites.csv`。这类形状本来就没有细节可言，替身与原图在视觉上等价。

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
