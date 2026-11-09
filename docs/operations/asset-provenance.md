# 素材来源清单与替换进度

> 目的：这份项目在发布任何二进制之前，**仓库里不能带有第三方权利方的素材**。代码是 MIT，字体是 OFL，其余贴图与音效必须在发布前换成自制或可商用素材。本文是这个工作的唯一进度表：每换掉一批就更新这里，数字用小节末尾的命令重新生成，不要凭记忆写。

## 怎么读这张表

- **判定**只有三种：`MIT`（本项目代码）、`OFL`（可随包分发的开源字体）、`原作`（来自其他作品、发布前必须换掉）。
- **状态**：`待换` / `占位已换` / `已换`。「占位」指几何/渐变风格的自制占位图——满足发布要求（不含他人素材），但不是最终美术。
- 替换手法统一为**同 GUID 重画**：只覆盖 PNG 字节，保留 `.meta`、`.asset`（切片矩形、九宫格边界、pivot）、GUID 与 prefab 引用。所以任何一批替换都不需要改 prefab、不需要改代码，也不引入引用丢失。

## 当前清单

运行 `powershell -NoProfile -ExecutionPolicy Bypass -File Tools/audit-asset-provenance.ps1` 的输出即为本表：

| 资源组 | 路径 | 文件 | 体积 | 判定 | 状态 |
| --- | --- | --- | --- | --- | --- |
| 音符皮肤 custom01 | `Assets/Game/Resources/live/note/custom01` | 89 | 1.48 MB | 原作 | 占位已生成，未落盘 |
| 音符皮肤 custom02 | `Assets/Game/Resources/live/note/custom02` | 89 | 1.56 MB | 原作 | 占位已生成，未落盘 |
| live 打击音 acb | `Assets/Game/AssetBundles/resources/startapp/live/sound` | 3 | 0.84 MB | 已换（自制 CC0 wav） | 待换 |
| tap 音效 acb | `Assets/Game/AssetBundles/resources/startapp/live/tap_se` | 4 | 0.54 MB | 已换（自制 CC0 wav） | 待换 |
| live 背景 2dmode | `Assets/Game/AssetBundles/resources/startapp/live/2dmode` | 2 | 1.5 MB | 原作 | 待换 |
| 加载背景 longLoading | `Assets/Graphics/Resources/loading` | 15 | 12.72 MB | 原作 | 待换 |
| HUD / UI 大图 | `Assets/Graphics/Resources/screen` | 15 | 6.01 MB | 原作 | 待换 |
| 通用背景 commonbg | `Assets/Graphics/Resources/commonbg` | 1 | 6.67 MB | 原作 | 待换 |
| live UI 预制 | `Assets/Graphics/Resources/live` | 10 | 3.82 MB | 原作 | 待换 |
| HUD sprite | `Assets/Graphics/Sprites` | 391 | 1.65 MB | 原作 | 待换 |
| 字体 | `Assets/Game/Resources/Fonts` | 4 | 33.32 MB | OFL | 保留 |
| 代码 | `Assets/Game/Runtime` | 796 | 2.12 MB | MIT | 保留 |

合计需要替换约 **36 MB**；字体 33 MB 与代码 2 MB 不需要动。

## 替换顺序与理由

1. **音符皮肤**（2 套 / 66 张 PNG / 3 MB）——每首歌都在眼前、体量最小、只涉及贴图。工具：`Tools/replace-note-skins.ps1`（`-Set custom01|custom02|all`，默认只出预览到 `Temp/skin-preview`，加 `-Apply` 才覆盖仓库文件）。
2. **HUD sprite**（391 张 / 1.65 MB）——分数条、血条、暂停/跳过键、判定字、数字、轨道。由 `FrontUIView.prefab` / `FrontMultiUIView.prefab` 引用，同样可以同 GUID 重画。
3. **打击音与 tap 音**（7 个 acb / 1.38 MB）——acb 是 CRI 打包格式，本机没有 CRI 工具，因此不走「重打 acb」，而是在 live 开始时用 `SoundManager.RegisterExternalAudioClip(bundle, cue, clip)` 注册普通 `AudioClip`，把这个 bundle 的 cue 顶掉。
4. **live 背景 / 2dmode 背景**（5.3 MB）——换成自制可循环背景。
5. **UI 大图**（`screen` / `loading` / `commonbg` / `live` UI，约 29 MB）——这批会随着界面重做一起消失，**不要单独重画**；重做时直接放自制图。
6. **`NOTICE.md`**——发布前必须写清楚：代码 MIT；字体 Noto Sans SC / Source Han Sans SC 为 OFL；其余贴图与音效为自制或 CC0，并列出清单。

## 已经做过的事

- 2026-11-08 `0c292d4`：删掉 `ScreenLayerLiveLoading` 右下角的原作下载表盘（18 个 transform）。同款表盘还挂在 `ScreenLayerLoading`、`ScreenLayerNetworkIndicator`、`ScreenLayerAssetRemoving` 上，这三个在本项目里没有调用路径，属待清理死资产。
- 2026-11-08：`Tools/replace-note-skins.ps1` 完成并通过预览验证（同 GUID 重画，切片与九宫格不变）。

## 重新生成数字

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/audit-asset-provenance.ps1
```

输出直接替换上面「当前清单」表格；状态列请手动维护（脚本不知道你是否已经替换过）。

## 相关文档

- [发布流程](release.md)
- [项目索引](../INDEX.md)
