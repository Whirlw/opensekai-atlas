# 材质包导入：流程、教训与后续功能设计建议

> 目的：把「把画好的材质包整体贴回工程并验证」这件事沉淀成可复用流程，并把 2026-11-08 一天内踩到的坑逐条固定下来，作为将来做**一键材质包导入功能**的设计依据。
> 适用读者：要改美术素材的人、要写导入工具的人、要在真机上验收素材的人。
> 前置条件：读过 [编辑器菜单](editor-menus.md)、[Android 构建](android-build.md)；本机 Unity 6000.6.7f1 与 adb 可用。
> 最后更新：2026-11-12

## 概述

本项目的美术资源被两种完全不同的机制消费，混淆这两者会产生「明明导入了却还是旧图」的错觉：

| 类别 | 存放 | 运行时来源 | 导入方式 |
| --- | --- | --- | --- |
| **图集切片** | 打包图集 `Assets/Graphics/Textures/sactx-*.png` | 图集纹理（打包进 Player 或 bundle） | 按精灵矩形**贴像素** |
| **散图** | 各种 `.png`（转场、音符皮肤、特效、背景…） | 文件本体 | **整文件覆盖** |

同一次「全量导入」必须同时走两条路径，缺一条就会有一半素材停留在旧版。

## 结论速览（每次导入照做）

```powershell
# 0) 画包目录（作者产出）镜像进工程（工具读的是工程内镜像）
$src='E:\Projects\NewSkin\paint-kit'; $dst='E:\DeepseekHarness\OpenSekai\LocalPacks\paint-kit'
foreach ($d in @('new_hud','new_effects','new_notes','new_backgrounds')) {
  robocopy (Join-Path $src $d) (Join-Path $dst $d) /MIR /NJH /NJS /NDL /NC /NS /NP | Out-Null
}
Copy-Item "$src\manifest_new_hud.csv" "$dst\manifest_new_hud.csv" -Force

# 1) 散图（递归！）
powershell -File Tools\apply-paint-kit.ps1 -KitRoot "$dst" -Apply

# 2) 图集切片
powershell -File Tools\apply-atlas-art.ps1 -Apply
powershell -File Tools\compare-atlas-art.ps1     # differ 里应只剩整纹理/内边距那几张

# 3) 让 Unity 落盘 → 重打 bundle → 出包
#    unity_eval: AssetDatabase.Refresh(ForceSynchronousImport)
#    unity_eval: ExecuteMenuItem("OpenSekai/AssetBundles/Build Asset Bundles And Info")
#    unity command build run --target Android --outputPath Builds/<name>/OpenSekaiAtlas.apk --confirm
#    adb install -r … ; adb shell monkey -p io.github.opensekai -c android.intent.category.LAUNCHER 1
```

三条必须看的输出行：

| 工具 | 期望输出 | 含义 |
| --- | --- | --- |
| `apply-paint-kit.ps1` | `copied=N same=M missed=K mismatch=2` | `missed` 里应只剩预览图/对比图 |
| `apply-atlas-art.ps1` | `painted=N wholeTexture=M noSprite=K sizeDiff=0 shadowed=S atlases=T` | `noSprite` 是「整图」类，正常；`shadowed` 是同一精灵的多个画包文件，正常应只剩替身 |
| `verify-hud-art.ps1` | 生成对比图，左画包 / 右图集 | 逐张必须一致 |

最近一次全量导入（2026-11-12，`0d153e68`+`a564828b` 之后）的实测输出，可作参照：`apply-paint-kit.ps1` 为 `copied=17 same=112 missed=200 mismatch=0`（`missed` 是画包里还没进工程的新素材与预览图），`apply-atlas-art.ps1` 为 `painted=168 wholeTexture=6 noSprite=44 sizeDiff=0 shadowed=0 atlases=21`——`shadowed` 归零是因为镜像里 15 个程序生成的 `*_0.png` 替身已删除（`number_48px_shadow_0`、`skillEffect_text_0` 是作者自己的文件名，保留），随后又按精灵名为 9 个未覆盖的白色底片重新生成了替身。

## 七条硬规则

### R1 精灵名 ≠ 资产文件名

`Assets/Graphics/Sprites/` 里大多数文件与精灵同名，但**评级字母 `B`/`C`/`S` 的资产文件叫哈希名**，只有 `m_Name` 是 `B`/`C`/`S`。

- 按文件名匹配 → 这三个永远「找不到」，导入报告不会报错，只是静默留在旧图。
- 正确做法：扫每个 `.asset` 前 20 行读 `m_Name`，建 `名字 → 路径` 索引（`Tools/apply-atlas-art.ps1` 已实现）。
- 同类风险：大小写、`_pr`/`_rbw`/`_wh` 后缀、`number_48px_1_wh` 与 `number_combo_154px_1_pr` 名字相近但不同图。

### R2 素材目录必须递归

`new_notes` 的结构是分层的：

```
new_notes/                     8 个
new_notes/textures/            4 个
new_notes/textures/noteskin/   29 个   ← 五种 among 全在这里
```

`Get-ChildItem -File`（非递归）会整层跳过，且不报错。**任何导入工具都必须 `-Recurse`**，并在输出里报告「本次处理的目录清单」，否则漏层无法察觉。

### R3 工程内镜像必须与画包同步

编辑器工具（`PaintKit`）读取的是 `LocalPacks/paint-kit/`，**不是**作者的 `E:\Projects\NewSkin\paint-kit`。这个镜像曾经停留在几小时前：`new_hud` 是 06:44 的快照，而 `new_effects`/`new_notes`/`new_backgrounds` **整个目录都不存在**。

后果：工具照常打印 `applied 157`，实际把**旧图**贴回去，观感上就是「素材回退」。

规则：**导入前先镜像，镜像后用 `LastWriteTime` 与文件数核对**（见流程第 0 步）。长期方案是把镜像路径做成参数，禁止工具隐式回落到某个固定目录。

### R4 `PaintKit` 的 apply 不可信

实测：`[PaintKit] applied 157, wrong size 0, not painted 0, failed 0` 之后，图集像素**没有变化**（`btn_stop` 仍是白圆、`bg_scoreGauge` 仍是灰块、`judge_perfect` 仍是青绿）。

它还只写 3 个图集，而 HUD 实际分布在 **27 个图集**里。

- 现状：`PaintKit` 只用于**导出**（`1. Export` / `3.` / `4.`），**不要用它的 apply**。
- 替代：`Tools/apply-atlas-art.ps1`（按精灵矩形贴像素，见 R5）与 `Tools/apply-paint-kit.ps1`（散图）。
- 通用教训：**任何「导入成功」都必须有像素级证据**，不能只看日志。

### R5 图集切片要按「矩形 + 纹理 GUID」定位

正确来源是精灵资产本身，而不是清单里的坐标列或猜测：

```yaml
# Assets/Graphics/Sprites/<x>.asset
m_Rect: {x, y, width, height}          # 图集内的左下原点矩形
m_RD.texture: {guid: <图集纹理 guid>}   # 指向 sactx-*.png
```

贴合公式（PNG 是左上原点）：`top = atlasHeight - (rectY + rectH)`，矩形即 `(rectX, top, rectW, rectH)`。

写入用 `CompositingMode = SourceCopy`，否则半透明像素会与旧图叠加，看起来「脏」。

### R6 尺寸不一致要走明确策略

- 差 ≤ 2px：缩放到框内（矩形取整导致的 1px 差很常见，例如精灵矩形 `32×33.97`）。
- 差 > 2px：**报出来**，当前实现是仍按框缩放并在输出列出清单，交人判断。

目前已知 6 个属于此类：`mask_2dmode_floor_grd`、`lane_base`、`bg_2dmode_window_nml`、`judgeLine`、`bg_2dmode_window_mirror`、`label_grd_h40_dbl`。

还有 2 个**拒绝导入**的：`tex_transition_01.png`（画包 1024×1011 / 工程 1024×1024）、`tex_transition_03.png`（502×512 / 512×512）。这两个需要作者按工程尺寸重导，或明确同意缩放。

### R7 一个精灵只能有一个画包文件

- 画包里同时存在 `label_autolive.png`（真图）与 `label_autolive_0.png`（替身）时，两者都能经文件名索引落到**同一个**精灵资产上。旧版 `apply-atlas-art.ps1` 按名字遍历、后写的赢，于是替身盖掉了真图——`AUTO LIVE` 标牌因此显示成一块纯色。
- 现行实现先按精灵归并再挑一个：**与精灵 `m_Name` 同名的文件优先**，其余的记进 `shadowed` 一起列出来。一个精灵只贴一次。
- 替身只在真图缺失时才有意义。真图到位后替身要改名或删掉；否则它只是被"同名优先"挡住，报告里会一直挂着。

## 已踩过的坑（现象 → 根因 → 处置）

| 现象 | 根因 | 处置 |
| --- | --- | --- |
| 只有 2D 背景、SCORERANK 底板是新图，其余全旧 | 这两个是**散图**；图集那条路径压根没生效（R4） | 改用 `apply-atlas-art.ps1` |
| 评级字母 B/C/S 一直是旧图 | 精灵名与文件名不同（R1） | 按 `m_Name` 建索引 |
| 五种 `among` 音符一直是旧图 | 素材在子目录，脚本不递归（R2） | `-Recurse` |
| 「素材回退到旧版本」 | 工程内镜像是旧快照（R3） | 导入前镜像 + 核对时间戳 |
| COMBO 数字 `1` 左下角缺一块，只有设备上出现 | 精灵资产里**烘焙了紧致网格**（`m_RD.m_SubMeshes.vertexCount: 10`，是旧字形的轮廓多边形）。改 import 的 `spriteMeshType` 无效，因为网格随资产序列化 | 绘制端用 `SpriteDrawMode.Sliced` 自建矩形：`renderer.size = sprite.rect.size / sprite.pixelsPerUnit`（`Assets/Game/Runtime/Shared/UI/SpriteQuad.cs`） |
| 同上的修复后，**第一局**数字尺寸异常（AP 偏小、光圈偏大） | 只在「首次可见」时才规范化渲染器，之前沿用预制体里残留的 `size` | 在 `ComboView.Setup()` 里对三组渲染器一次性规范化 |
| AP 数字变小、灰色光圈变大 | 用 `sprite.bounds.size`（紧致网格外框，比切片小）当尺寸 | 改用 `sprite.rect.size / pixelsPerUnit` |
| 修完仍怀疑是图集压缩（ASTC 6×6 块混色） | 不是本次根因；换 RGBA32 后现象不变 | 记录备查：ASTC 6×6 会跨块混色，切片离块边界近时边缘可能受影响；但本案是网格问题 |
| 判定特效只有第一套是新的 | 画包里 `all` 与 `all_v2` 是**两套**，第二套对应 `tap_effect/1` | 映射规则见下节 |
| 画包里的 `tex_transition_01/03` 比工程小（1024×1011 / 502×512 vs 1024×1024 / 512×512） | 这两张在工程里是 **Sprite** 导入（`textureType: 8`、`spriteMode: 1`），导出时按精灵 rect 裁掉了边缘 | 以工程 PNG 为准：把工程原件拷进画包再重画，不要用导出件当底图 |
| `judgeLine` 永远和底下的线对不上，轮廓画得一模一样也没用 | 它是**独立纹理**（1608×114），精灵 rect 只取中间 1583×84（去掉了 13/15 px 内边距）。画包给的是整张纹理，按 rect 粘贴就被压缩、线条错位 | 画包尺寸等于纹理尺寸时按**整纹理替换**，不做矩形贴合（`apply-atlas-art.ps1` 的 wholeTexture 分支，同类还有 `lane_base`、`bg_2dmode_window_*`、`mask_2dmode_floor_grd`、`label_grd_h40_dbl`） |
| 打歌界面右下角的 `AUTO LIVE` 标牌是**纯色薄荷块** | 画包里同时有真图 `label_autolive.png` 与程序生成的替身 `label_autolive_0.png`，两者指向同一精灵；旧版按名字遍历、后写的替身赢了，真图从没进过图集（R7）。同批被替身盖掉的还有评级字母 `B`/`C`/`S`、`gauge_base_h16_dbl`、`bg_base_wh`、`bg_base_ingame_wh` | 按矩形重贴真图 + 改成「同名优先」；用 `compare-atlas-art.ps1` 逐格比对可当场发现 |

### 两套判定特效的映射

| 画包文件 | 目标 |
| --- | --- |
| `live_tap_effect_0_tex_note_common_all.png` | `tap_effect/0/textures/tex_note_common_all.png` |
| `live_tap_effect_0_tex_note_common_all_v2.png` | `tap_effect/0/.../tex_note_common_all_v2.png` **与** `tap_effect/1/.../tex_note_common_all_v2.png` |
| `live_tap_effect_0_tex_note_common_lane.png` | `tap_effect/0/.../tex_note_common_lane.png` |
| `live_tap_effect_0_tex_note_tail_01_v2.png` / `_tex_ove_damage_01.png` | 两套都要 |

`tap_effect/1` 只有 3 个文件（`all_v2` / `tail_01_v2` / `ove_damage_01`），没有 `all` 与 `lane`。

## 校验与验收

### 像素级（导入后立刻做）

`Tools/verify-hud-art.ps1` 生成对比图：左列画包原图、右列图集实际像素。抽查项固定包含 `btn_stop`、`bar_lifeGauge_gn`、`bg_scoreGauge`、`judge_perfect`、`number_combo_154px_1_pr`、`number_48px_4_wh`。

`Tools/compare-atlas-art.ps1` 是数值版，覆盖画包里**每一张**图，不只是抽查的那几张：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/compare-atlas-art.ps1
# kit=225 match=168 differ=6 noSprite=44 noRect=0 shadowed=7
```

`differ` 的基线是 2 个，都是**整纹理替换/内边距裁剪**：`judgeLine`（画包给整张 1608×114，精灵 rect 只取中间 1583×84）与 `bg_2dmode_window_mirror`（画包 800×800，rect 542×526）。**出现别的名字就意味着那一格还是旧图**——`AUTO LIVE` 那次就是这样查出来的。`shadowed` 列的是同一个精灵的多个画包文件（见 R7），正常应为 0 或只剩作者按资产文件名命名的那几个。

### 真机验收清单

1. 曲库界面：背景、封面、按钮
2. 打歌：SCORE/LIFE 面板、暂停键、COMBO 数字（普通 + AP）、判定文字、两套判定特效、五种 among 音符、两套音符皮肤
3. 结算：横幅、评级字母、SCORERANK 底板
4. 转场（若已处理那两个尺寸不符的文件）

## 做「材质包导入功能」时的设计建议

如果要把这套流程做成产品功能（编辑器窗口或游戏内），按下面几条设计，可以避免本次全部事故。

### 1. manifest 是唯一真源，且必须自描述

每条记录至少包含：`源文件`、`目标类型`（atlas-sprite / whole-texture）、`目标资产`（**用 GUID，不用路径字符串**）、`矩形来源`（只允许「从精灵资产读」，不允许清单里手写坐标）、`尺寸策略`（strict / fit / scale）。

清单里手写 rect 迟早与图集不同步；本次事故里清单的 `texture` 列整列为空，工具于是「成功」地什么也没做。

### 2. 禁止静默跳过

任何「找不到目标 / 尺寸不符 / 无权限」都必须进入**结构化报告**并在界面高亮，不能只 `Debug.Log` 一行，更不能计入 `applied`。

这次三个坑（B/C/S、among、镜像过期）全部是「不报错但没做」。

### 3. 幂等 + 可回滚

- 导入前把将被覆盖的文件备份到 `LocalPacks/revision-backups/<timestamp>/`（作者侧已有类似习惯）。
- 每个目标文件记录 `导入前 hash / 导入后 hash`，导入结束打印「实际变化 N 个」。
- 提供 `-Restore <backup>`。

### 4. 导入结果要能自检

工具自身跑一遍「读回目标 → 与源对比」并给出差异表（就是本文件的 `verify-hud-art.ps1`）。**没有读回校验的导入工具等于没有导入**。

### 5. 两类资产分开建模

图集切片与散图的失败模式完全不同（前者错在定位，后者错在路径/递归），混在一个函数里会导致「一半成功」难以定位。建议两个 pass，各自独立报告。

### 6. 精灵绘制不要依赖烘焙网格

新画的图必须能被完整绘制。凡是可能被重画的精灵，统一 `SpriteDrawMode.Sliced` + `size = rect.size / ppu`（见 `SpriteQuad.DrawAsRectangle`），并在 `Setup()` 里一次性规范化，别等「首次可见」。

### 7. 需要的回归测试

- 精灵名索引：给出几个「文件名 ≠ m_Name」的样例，断言能定位。
- 递归：素材放在两层子目录里，断言被处理。
- 矩形越界：rect 超出纹理必须失败而不是裁掉。
- 尺寸阈值：1px 差通过、10px 差进报告。
- 图集 import 设置：`spriteMeshType` 允许值断言（`Assets/Tests/EditMode/Graphics/LiveAtlasSpriteSettingsTests.cs` 已有先例）。
- 绘制尺寸：给定 sprite，断言 `Sliced` 下的世界尺寸等于 `rect.size / ppu`。

## 工具清单

| 工具 | 职责 | 备注 |
| --- | --- | --- |
| `Tools/apply-paint-kit.ps1` | 散图整文件覆盖（递归、按叶子名匹配、尺寸校验） | `-KitRoot` 必填工程内镜像 |
| `Tools/apply-atlas-art.ps1` | 图集切片贴像素（按 `m_Name` 索引、按 rect+guid 定位、SourceCopy；同名画包文件优先，其余进 `shadowed`） | 权威路径 |
| `Tools/compare-atlas-art.ps1` | 逐格比对：画包每张图 vs 图集里那一格的实际像素 | 查出「贴过但其实没进图集」的格子 |
| `Tools/verify-hud-art.ps1` | 画包 vs 图集逐张对比图 | 导入后必跑 |
| `Tools/replace-note-skins.ps1` | 早期音符皮肤替换（`-Set` / `-Restore`） | 已被上面两个脚本覆盖 |
| `OpenSekai/Paint Kit/1,3,4` | 导出画包 | 只用于导出 |

## 常见问题排查（现象 → 先查什么）

| 现象 | 先查 |
| --- | --- |
| 某几个素材没变 | 名字索引（R1）、目录层级（R2）、是否在 23 个「整图」名单里 |
| 全都没变 | 镜像是否同步（R3）、`AssetDatabase.Refresh` 是否执行、bundle 是否重打 |
| 变了但设备上是旧图 | 是否只重打了 bundle 没重出 APK；是否装了旧包（看 `adb shell dumpsys package` 的 `lastUpdateTime`） |
| 边缘缺角 / 被切 | 精灵烘焙网格（改用 Sliced）、rect 是否贴边、ASTC 块边界 |
| 颜色发脏 / 半透明叠加 | 是否用了 `SourceOver` 而不是 `SourceCopy` |
| 第一局异常、重开正常 | 是否依赖「首次可见」才初始化尺寸 |
