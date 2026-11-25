# notes.png 约束（音符本体图集）

> 目的：把 `notes.png` 必须满足的几何约束一次写清楚，做新皮肤时照着画就能和判定线、车道严丝合缝。
> 适用读者：画音符皮肤的美术、改音符着色器或尺寸代码的开发者。
> 前置条件：读过 [音符皮肤制作](note-skin-authoring.md)（目录结构与导入流程）。
> 最后更新：2026-11-15

## 概述

`notes.png` 是 7 个音符本体切片的图集（点按、长条、滑键、摩擦键等的「那一块板」；箭头、连接点等在别的贴图里）。它的布局被三处代码和资源同时依赖，任何一处对不上，音符就会在屏幕上错位：

| 依赖方 | 依赖什么 | 位置 |
| --- | --- | --- |
| 切片资源 | 每个切片的像素矩形、九宫格边界、PPU、轴心 | `Assets/Game/Resources/live/note/custom0N/**/notes_0.asset` … `notes_6.asset` |
| 音符着色器 | 图集是 7 格、每格 188 px、PPU 108、切片在格内垂直居中 | `Assets/Game/BuiltinAssets/assetbundle/resources/tutorial/shader/live/note/note-slide.shader` |
| 尺寸代码 | 音符本体距切片左右边缘各 55 px | `LiveUtility.CalcSpriteRendererSize`（`NoteBodyInsetUnits`） |

下文把每条约束标为：

- **硬约束**：违反了音符一定错位，除非按「§7 联动修改」同步改代码或资源。
- **推荐**：违反了不会错位，但会出现裁切、拉伸变形等观感问题。

两套皮肤（`custom01` / `custom02`）的切片、导入设置、预制体到切片的对应完全相同，下文对两者都适用。

## 1. 画布与切片（硬约束）

- 画布 **354 × 1316**，PNG，RGBA。
- 纵向等分为 **7 格，每格 188 px**，自上而下是格 0～6（注意是**自上而下**：`notes_0` 在图片最顶端）。
- 格 k 占图片第 `188k` ～ `188k+187` 行（行号从图片顶端 0 开始，下同）。其中：
  - 切片 `notes_k` 只取第 `188k+1` ～ `188k+186` 行（高 186），宽取满 354；
  - 每格首尾两行（`188k` 与 `188k+187`）是**隔离行，必须全透明**，否则双线性采样会把相邻格的颜色渗进来。
- 每个切片：PPU **108**，轴心在切片正中（格内第 94 行、第 177 列），九宫格边界**左右各 100 px、上下 0**。

```text
x:   0        55      100                 254      299       354
     |---------|-------|-------------------|--------|---------|
     |  辉光   | 端帽  |   中段（横向拉伸）  |  端帽  |  辉光   |
     |         |<------------- 本体（板面）-------------->|        |
     |<- 左边界 100（不拉伸）->|                   |<- 右边界 100（不拉伸）->|
```

## 2. 每个切片用在哪

| 切片 | 格（自上而下） | 用于的预制体 | 当前皮肤的样子 |
| --- | --- | --- | --- |
| `notes_0` | 0 | `NormalCrtNote`、`LongCrtNote`、`FlickCrtNote` | 金色，宽板 |
| `notes_1` | 1 | `LongNote` | 青绿，宽板 |
| `notes_2` | 2 | `NormalNote` | 紫→蓝，宽板 |
| `notes_3` | 3 | `FlickNote` | 粉色，宽板 |
| `notes_4` | 4 | `FrictionNote`、`FrictionLongNote` | 青绿，细板 |
| `notes_5` | 5 | `FrictionCrtNote`、`FrictionLongCrtNote`、`FrictionFlickCrtNote` | 金色，细板 |
| `notes_6` | 6 | `FrictionFlickNote` | 粉色，细板 |

连接点（`ConnectionNote` / `ConnectionCrtNote`）用的是 `noteskin/notes_long_among*.png`，不在本图集里。

## 3. 横向：九宫格与本体宽度

游戏按音符跨几条车道设置切片宽度，只拉伸中段，两侧 100 px 的边界保持原样：

```text
切片宽度（单位）= 车道数 × 1.19 + 2 × 55 / 108
```

1.19 是一条车道的宽度（`LiveConfig.widthX`）。所以**本体正好覆盖它所在的车道**的前提是：

- **硬约束：本体（板面）的左右两端分别在 x = 55 与 x = 299**（切片边缘向内 55 px，左右对称）。这个 55 与 `LiveUtility.NoteBodyInsetUnits` 是同一个数。
- **硬约束：本体两端必须落在不拉伸的边界区内**（0～100 与 254～354），否则端点会随音符宽度移动。
- 推荐：端帽的细节（斜角、高光竖线等）画在 55～100 与 254～299 之间，这两段永远按原样显示。
- 推荐：中段 100～254 会被横向拉伸到任意长度，只放**横向均匀**的内容：竖直方向的渐变、横贯的线条。星星、文字、点状纹理在这里会被拉成长条。
- 辉光可以画到本体以外（当前最远到 x = 31～322），屏幕上会溢出到相邻车道，这是有意的。

## 4. 纵向：贴合判定线

判定线在屏幕上的高度是固定的。实测（DEINOS PHAINEIN，2800 × 1272，音符恰在判定时刻）换算回格内坐标，判定线的外沿覆盖**格内第 50.2 ～ 133.0 行**，中心约在第 91.6 行，比切片轴心（第 94 行）高约 2.4 行。

- **宽板（格 0～3）**：板面顶边在第 **50** 行，板面连同下方的厚度（挤出）一起在第 **133** 行结束。当前是板面 50～128、厚度 5 px 到 133。
- **细板（格 4～6）**：第 72～116 行（板面 72～112、厚度 4 px），与判定线同中心，不必占满判定线高度。
- 推荐：辉光整体留在格内第 26～152 行左右（当前最远就是这个范围）。超出切片（第 1～186 行）的部分会被直接切掉，形成硬边。
- 换算：在 2800 × 1272 下 1 单位 = 127.2 px，格内 1 px ≈ 屏幕 1.18 px。

纵向位置只影响美术，不被代码读取，所以严格说不是硬约束；但偏离上面的数字，音符就会高出或低于判定线（这正是改这份文档之前的问题：旧版板面在 54～139 行，底边比判定线低 3 px）。

## 5. 透视由着色器负责（硬约束）

- **按正视图画**：板面画成方方正正的矩形。透视（上窄下宽、越靠边越斜）由 `note-slide.shader` 计算，把音符每一点放到从消失点 (0, 5.46) 发出的车道射线上。贴图里预先画斜，屏幕上就会斜两次。
- 着色器从纹理坐标反推「这一点在音符中心以上多高」，用的就是 §1 的布局：`frac(uv.y × 7)`、每格 188 px、PPU 108、切片在格内居中。图集换成别的格数、格高或 PPU，透视就会全错（见 §7）。
- 着色器在每个像素上做透视校正的纹理插值，所以贴图里的直线在屏幕上仍是直线。若看到线条在音符中间折一下，说明在用旧着色器。

## 6. 颜色与透明度

- **直通 alpha（非预乘）**：着色器自己做 `rgb × alpha`。导出时不要预乘，透明处的 RGB 无所谓但 alpha 必须为 0。
- **游戏不染色**：顶点颜色被用来传数据（车道中心、宽度、进度），不参与着色；材质 `_Color` 为白色。颜色就是你画的颜色。
- 导入设置（`notes.png.meta`，两套相同）：sRGB、双线性过滤、无 mipmap、最大 2048。不要改。
- 两套音符共用 `ArtSource/Tools/Notes.cs` 里的本体尺寸，各自绘制板面和标记。`ArtSource/Tools/verify.ps1` 会分别检查两套图集的边界与拉伸区。

## 7. 联动修改：想改某个数时还要改哪里

| 想改的东西 | 必须同步修改 |
| --- | --- |
| 本体内缩 55 px | `LiveUtility.cs` 的 `NoteBodyInsetUnits` 和 `ArtSource/Tools/Notes.cs` 的 `Left`、`Right` |
| 格数 7、格高 188、PPU 108 | `note-slide.shader` 的 `AtlasCells` / `CellPixels` / `PixelsPerUnit`；两套皮肤共 14 个 `notes_N.asset` 的 `m_Rect` 与 PPU；`LiveUtility.cs` 的 `NoteSpriteHeight`（= 切片高 / PPU） |
| 九宫格边界 100 px | 两套皮肤共 14 个 `notes_N.asset` 的 `m_Border`；内缩 55 必须仍小于它 |
| 板面纵向位置/高度 | `Notes.cs` 中 `Make` 设置的 `Top`、`FaceHeight` 和 `Depth`，然后按 §8 截图核对 |
| 车道几何（消失点、判定线、车道宽） | `note-slide.shader` 的 `LaneDepth`、`ArtSource/Tools/Hud.cs` 的 `HalfWidth`、`Tools/fit-lane-sprite-meshes.ps1` 及 `ArtSource/Tools/Effects.cs` 的判定线特效框 |

## 8. 怎么验证

用 [截图工具](screenshot-tools.md) 拍两个点按恰在判定线上的时刻（DEINOS PHAINEIN 谱面）：

```powershell
powershell -File Tools/capture-screens.ps1 -Live 64.097,66.939 -Package "<DEINOS PHAINEIN 曲包目录>" -OutDir TestResults/screenshots/notes-check
```

- 64.097 s：车道 1～5 的 5 道点按（紫蓝，`notes_2`）。在 y = 1008 那一行，本体左端应在 x ≈ 646，右端在 x ≈ 1400。
- 66.939 s：车道 5～11 的 7 道点按。在 y = 1008 那一行，本体两端应在 x ≈ 1249 与 x ≈ 2304。
- 两者的板面上下沿应落在判定线外沿 y ≈ 958 与 y ≈ 1056.5。
- 本体两端应与分道线平行，误差在 1～2 px 内。

想精确量，可以临时把格 2 清空，只画几条 1 px 标记线：第 50 / 94 / 133 行横线，x = 55 / 299 竖线（红），x = 100 / 254 竖线（绿）。导入后截图，按颜色找线的位置，量完再用生成器恢复原图。

## 9. 生成器

在项目根目录运行 `powershell -NoProfile -ExecutionPolicy Bypass -File ArtSource/Tools/render.ps1 -Group Notes,NotesV2 -Only notes.png`，即可重画两套皮肤的本体图集。运行 `ArtSource/Tools/verify.ps1` 核对尺寸和切片，再用 `ArtSource/Import-All.bat` 全量导入。

手绘新皮肤时不必用生成器，但画完的图要满足 §1～§6。

## 相关文档

- [音符皮肤制作](note-skin-authoring.md)
- [截图工具](screenshot-tools.md)
- [材质包导入](material-pack-import.md)
- [打歌运行时](../architecture/live-runtime.md)
