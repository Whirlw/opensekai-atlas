# 素材来源清单与替换进度

> 目的：这份项目在发布任何二进制之前，**仓库里不能带有第三方权利方的素材**。代码是 MIT，字体是 OFL，其余贴图与音效必须在发布前换成自制或可商用素材。本文是这个工作的唯一进度表：每换掉一批就更新这里，数字用小节末尾的命令重新生成，不要凭记忆写。
>
> **现状（2026-11-09，0.3.0）：替换完成。** 仓库与 APK 里都不含原作素材，`Tools/audit-audio-provenance.ps1` 输出 `AUDIT OK`，`Tools/audit-atlas-coverage.ps1` 显示图集里已无未替换的精灵。下面保留历史清单与做法，供追溯。

## 怎么读这张表

- **判定**只有三种：`MIT`（本项目代码）、`OFL`（可随包分发的开源字体）、`原作`（来自其他作品、发布前必须换掉）。
- **状态**：`待换` / `占位已换` / `已换`。「占位」指几何/渐变风格的自制占位图——满足发布要求（不含他人素材），但不是最终美术。
- 替换手法统一为**同 GUID 重画**：只覆盖 PNG 字节，保留 `.meta`、`.asset`（切片矩形、九宫格边界、pivot）、GUID 与 prefab 引用。所以任何一批替换都不需要改 prefab、不需要改代码，也不引入引用丢失。
- 图集里的精灵走 `Tools/apply-atlas-art.ps1`：按精灵 `m_Rect` 与 `m_RD.texture` 的 GUID 定位到真正的图集 PNG，再用 `CompositingMode.SourceCopy` 贴进去。**不要用编辑器里的 `OpenSekai/Paint Kit/2. Apply painted sprites`** —— 它会打印 `applied 157, failed 0` 却不动像素。

## 当前清单

运行 `powershell -NoProfile -ExecutionPolicy Bypass -File Tools/audit-asset-provenance.ps1` 的输出即为本表：

| 资源组 | 路径 | 文件 | 体积 | 判定 | 状态 |
| --- | --- | --- | --- | --- | --- |
| 音符皮肤 custom01 | `Assets/Game/Resources/live/note/custom01` | 89 | 1.55 MB | 已换（画包 `new_notes`） | 已换 |
| 音符皮肤 custom02 | `Assets/Game/Resources/live/note/custom02` | 89 | 1.36 MB | 已换（画包 `new_notes_v2`） | 已换 |
| 判定 / 演出音 | `Assets/StreamingAssets/sound` | 42 | 2.99 MB | 已换（自制 CC0-1.0 wav） | 已换 |
| 原 CRI 音频负载 | 全仓 | 0 | 0 | 已删除 | 已换 |
| live 背景 2dmode | `Assets/Game/AssetBundles/resources/startapp/live/2dmode` | 2 | 2.72 MB | 已换（画包 `new_backgrounds`） | 已换 |
| 加载背景 longLoading | `Assets/Graphics/Resources/loading` | 0 | 0 | 已删除（页面不存在） | 已换 |
| HUD / UI 大图 | `Assets/Graphics/Resources/screen` | 8 | 6.01 MB | 已换 / 未引用者已删 | 已换 |
| 通用背景 commonbg | `Assets/Graphics/Resources/commonbg` | 0 | 0 | 已删除 | 已换 |
| live UI 预制 | `Assets/Graphics/Resources/live` | 10 | 3.82 MB | 已换（画包 `new_effects`） | 已换 |
| HUD sprite | `Assets/Graphics/Sprites` | 176 | 1.04 MB | 已换（画包 `new_hud` + 程序化替身） | 已换 |
| 图集精灵 | `Assets/Graphics/Textures/sactx-*.png` | 153 | 5.2 MB | 已换 | 已换 |
| 字体 | `Assets/Game/Resources/Fonts` | 4 | 33.32 MB | OFL | 保留 |
| 代码 | `Assets/Game/Runtime` | 796 | 2.12 MB | MIT | 保留 |

合计需要替换的部分已全部完成；字体 33 MB 与代码 2 MB 按许可保留。

## 替换顺序与理由

1. **音符皮肤**（2 套 / 66 张 PNG / 3 MB）——每首歌都在眼前、体量最小、只涉及贴图。工具：`Tools/replace-note-skins.ps1`（`-Set custom01|custom02|all`，默认只出预览到 `Temp/skin-preview`，加 `-Apply` 才覆盖仓库文件）。
2. **HUD sprite**（391 张 / 1.65 MB）——分数条、血条、暂停/跳过键、判定字、数字、轨道。由 `FrontUIView.prefab` / `FrontMultiUIView.prefab` 引用，同样可以同 GUID 重画。
3. **打击音与 tap 音**（7 个 acb / 1.38 MB）——acb 是 CRI 打包格式，本机没有 CRI 工具，因此不走「重打 acb」，而是在 live 开始时用 `SoundManager.RegisterExternalAudioClip(bundle, cue, clip)` 注册普通 `AudioClip`，把这个 bundle 的 cue 顶掉。**已落地的做法**：42 个 wav 放 `Assets/StreamingAssets/sound/<bundle>/<cue>.wav`，`manifest.csv` 列清单，`SoundManager.LoadExternalSoundPack()` 启动时异步读取并注册；外部包会覆盖同名 bundle，`LoadSoundBundle` 命中 `ContainsKey` 后直接返回，原 ACB 不再加载。导入工具 `Tools/import-audio-pack.ps1`。4 个 ACB 与 3 套菜单音已删除。
4. **live 背景 / 2dmode 背景**（5.3 MB）——换成自制可循环背景。
5. **UI 大图**（`screen` / `loading` / `commonbg` / `live` UI，约 29 MB）——这批会随着界面重做一起消失，**不要单独重画**；重做时直接放自制图。**最终结果**：`loading`、`commonbg`、`screen` 三个目录整体删除（页面已不存在），`live` 目录的贴图随画包替换。
6. **`NOTICE.md`**——发布前必须写清楚：代码 MIT；字体 Source Han Sans SC 为 OFL；其余贴图与音效为自制或 CC0，并列出清单。**已写进 `README.md` 的「资源说明」小节**，42 条音频署名在 `docs/reference/audio-credits.md`。

## 已经做过的事

- 2026-11-08 `0c292d4`：删掉 `ScreenLayerLiveLoading` 右下角的原作下载表盘（18 个 transform）。同款表盘还挂在 `ScreenLayerLoading`、`ScreenLayerNetworkIndicator`、`ScreenLayerAssetRemoving` 上，这三个在本项目里没有调用路径，属待清理死资产。**后续**：这些页面整体删除，四个 prefab 均已移除。
- 2026-11-08：`Tools/replace-note-skins.ps1` 完成并通过预览验证（同 GUID 重画，切片与九宫格不变）。
- 2026-11-09：两套音符皮肤分别落到 `custom01` / `custom02`（`apply-paint-kit.ps1` 的 `new_notes` / `new_notes_v2` 两组独立映射）。
- 2026-11-09：HUD / 判定 / 音符图集共 27 个纹理按切片替换；61 个仍为原作的通用 UI 图元（按钮底、进度条、圆形遮罩、等级字母）用 `Tools/draw-ui-standins.ps1` 生成白色替身贴入——运行时会按皮肤染色，所以只需形状与 alpha。
- 2026-11-09：6 张 `longLoading_bg_*` 与 `bg_common_color` 曾用 `Tools/replace-original-backgrounds.ps1` 程序化生成，随后连同页面一起删除。
- 2026-11-09：`maimusicinfo.ogg`（另一个游戏的音效，功能开关默认关闭）与其播放代码删除；`ProjectSekai.acf`（CRI 配置）删除；测试谱面封面换成自绘。
- 2026-11-09：`Tools/audit-audio-provenance.ps1` 输出 `expected=42 recorded=42 pending=0` / `AUDIT OK`。

## 复查用的三条命令

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/audit-asset-provenance.ps1   # 分组体积与判定
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/audit-audio-provenance.ps1   # 音频应有 42 条署名
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/audit-atlas-coverage.ps1     # 图集里还有没有原作精灵
```

前两条应分别给出 `AUDIT OK` 与「已换」分组；第三条若仍报「仍是原作」，先确认是不是脚本按 `m_Name` 匹配造成的假阳性（见 `material-pack-import.md` 的坑表），再看像素。

## 相关文档

- [发布流程](release.md)
- [素材包导入](material-pack-import.md)
- [音频素材替换](audio-replacement.md)
- [发布前素材总检](pre-release-asset-audit.md)
- [项目索引](../INDEX.md)
