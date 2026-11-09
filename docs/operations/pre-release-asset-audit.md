# 发布前的素材来源总检

> 目的：在把仓库推到 GitHub、把 APK 发出去之前，用可复现的方式回答一个问题——**包里还有没有原作的素材**。这篇给出检查方法、当前结论与剩余缺口。
> 适用读者：准备发版的人、接手素材替换的人。
> 前置条件：读过 [素材来源清单](asset-provenance.md)、[材质包导入](material-pack-import.md)、[音频素材替换](audio-replacement.md)、[音频来源建议](audio-sourcing.md)。
> 最后更新：2026-11-09

## 结论速览

| 类别 | 状态 | 证据 |
| --- | --- | --- |
| 代码 | **MIT**，无第三方源码 | 全仓 `.cs` 为本项目自写；`Assets/Vendor` 仅保留 Unity 官方与 OFL 资源 |
| 字体 | **OFL** | `Assets/Game/Resources/Fonts` 为 Source Han Sans SC（OFL-1.1），已随附许可 |
| 音频 | **已换完**，全自制 CC0 | 42 个 wav（`ShipsStreamingAssets/sound`），`docs/reference/audio-credits.md` 42/42 行，`Tools/audit-audio-provenance.ps1` 报 `AUDIT OK`；工程内 `.acb` 数量 **0** |
| 音符皮肤两套 | **已换完** | `Assets/Game/Resources/live/note/custom01|custom02`，来自画包 `new_notes` / `new_notes_v2`，两套哈希不同 |
| HUD / 判定字 / 特效 / 背景 | **主体已换**，仍有缺口 | 图集切片 108 个由画包覆盖；**93 个精灵仍是原作**（下表）；6 张加载背景 + 通用背景已换成程序生成图 |
| 已删除的原作残留 | 见「本轮删除」 | — |

APK：**84.26 MB**（本轮清理前 117.05 MB，−32.8 MB）。

## 检查方法（三条命令）

```powershell
# 1) 素材分组与判定表（人工维护的判定列）
powershell -File Tools\audit-asset-provenance.ps1

# 2) 音频逐文件合规
powershell -File Tools\audit-audio-provenance.ps1      # 期望结尾 AUDIT OK

# 3) 图集里还有多少精灵是原作 / 其中多少根本没人引用
powershell -File Tools\audit-atlas-coverage.ps1
powershell -File Tools\audit-unused-atlas-sprites.ps1
```

判定口径：

- **画包覆盖** = `LocalPacks/paint-kit/{new_hud,new_effects,new_notes,new_notes_v2,new_backgrounds}` 里存在同名 png。有同名文件即视为已重绘（导入流程会把它们贴进图集）。
- **未被引用** = 全仓 `*.prefab/*.unity/*.mat/*.asset` 里都搜不到该精灵的 GUID。这类精灵不渲染，删掉不影响游戏，但仍是原作的字节。

## 当前缺口（93 个原作精灵）

| 图集 | 精灵 | 画包覆盖 | 仍是原作 | 其中未被引用 |
| --- | ---: | ---: | ---: | ---: |
| CommonAtlas（两张，共用名） | 70 | 1 | 69 | 4 |
| GachaAtlas（两张） | 16 | 0 | 16 | 0 |
| HeaderTitleAtlas | 1 | 0 | 1 | 0 |
| LiveAtlas | 105 | 98 | 7 | 5 |
| JudgeAtlas | 9 | 9 | 0 | 0 |
| **合计** | **201** | **108** | **93** | **9** |

读法：CommonAtlas 的 69 个是通用 UI 图标（`eff_btn_circle_*`、`gauge_*`、`icon_*` 等），GachaAtlas 属于抽卡页（本工程没有该玩法），HeaderTitleAtlas 是标题字。

处置顺序建议：

1. **先削功能**：GachaAtlas（16 个）与 HeaderTitleAtlas（1 个）对应的页面在本工程并不存在，先确认没有预制体引用，再删图集（连同精灵）。删完它们自然从缺口里消失。
2. **再补画包**：CommonAtlas 里仍在用的通用控件，按 `new_hud` 的命名规则补进画包（同名即可），跑一次 `apply-atlas-art.ps1` 就覆盖。
3. 9 个「未被引用」的精灵可直接删（删前用 `audit-unused-atlas-sprites.ps1` 复核一次）。

## 本轮删除

| 项 | 体积 | 理由 |
| --- | ---: | --- |
| `Assets/Graphics/Resources/maimusicinfo.ogg` + `PlayMaimaiMusicInfoSe()` | 130 KB | 来自 **maimai** 的音效，与 Project Sekai 无关；功能默认关闭，属于遗留 |
| `MusicScoreMakerAtlas` + 149 个精灵 | 0.91 MB | 谱面编辑器已整体移除，精灵 0 引用 |
| `FrameAtlas` + 5 个精灵（`cardFrame_S_*`） | 0.23 MB | 卡框，0 引用 |
| 4 个 ACB（custom01/custom02/se_live_default/se_live_common） | 1.41 MB | 由自制 wav 包取代 |
| 6 张 longLoading_bg_*.png + g_common_color.png | 12.7 + 3.9 MB → 0.4 MB | 原作是 Project Sekai 各队插画与通用背景，用 Tools/replace-original-backgrounds.ps1 换成程序生成图（尺寸/文件名不变） |
| 3 张 MenuCommon 音频表 | 9.35 MB | UI 音从未触发 |

## 复检清单（每次发版跑一遍）

1. `git status --porcelain` 只剩 Unity 编辑器噪音文件（`UniversalRenderPipelineGlobalSettings`、`GraphicsSettings`、`codecoverage`、`ProjectAuditorSettings`、`ai.assistant`）。
2. 上面四条命令全部通过；`audit-atlas-coverage.ps1` 的「仍是原作」等于 0 或与本文表格一致（说明没有新增未覆盖素材）。
3. `Assets/**` 下没有 `.acb`、`.acb.bytes`、`.ogg`（除自己产出）、`.mp4`。
4. `LocalPacks/` 不进 git（`.gitignore` 已忽略），但发版机需要它才能复现素材。
5. 出包后装机，打一首歌，确认 HUD / 判定字 / 两套特效 / 两套音符皮肤 / 结算横幅都正常。

## 已知例外（不在替换范围内）

- `Assets/Vendor/*` 中的第三方运行库（如 ZString、Unity 官方包）按各自许可分发，不属于「原作素材」。
- 用户自建曲包（`CustomMusicScores`）与用户导入的封面图属于用户数据，不进仓库。
