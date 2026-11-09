# 自己画一套音符皮肤

替换打歌音符的外观不需要改代码、不需要改 prefab、也不需要碰任何 `.asset` / `.meta`。
皮肤的每一个像素都来自一批固定尺寸的 PNG，把它们按同样的文件名与尺寸放回去即可。

## 1. 目录结构

```
<你的皮肤目录>\
  custom01\
    textures\notes.png
    textures\longNoteLine.png
    textures\simultaneousLine.png
    textures\traceLine_eff.png
    textures\noteskin\notes_flick_arrow_01.png
    ...
  custom02\
    ...（同上，33 个文件）
```

`custom01` / `custom02` 就是设置里的「样式 1 / 样式 2」。只想换一套就只放 `custom01`。
没有提供的文件保持现状，不会有半个皮肤的问题。

## 2. 尺寸必须一致

尺寸不是建议，是硬约束：切图用的 `m_Rect` 是**像素坐标**，写在一个独立的 `.asset` 里，
图片尺寸一变，切片就会切到错的位置，而且运行期没有任何代码能修复这件事。

两套皮肤尺寸完全相同：

| 文件 | 尺寸 |
|---|---|
| `textures\notes.png` | 354 × 1316（7 个切片，每片 354 × 186，自下而上） |
| `textures\longNoteLine.png` | 306 × 16 |
| `textures\simultaneousLine.png` | 10 × 40 |
| `textures\traceLine_eff.png` | 306 × 224（材质贴图，无切片） |
| `noteskin\notes_flick_arrow_0N.png` | 144×158 / 188×174 / 248×194 / 312×216 / 374×236 / 436×258 |
| `noteskin\notes_flick_arrow_0N_diagonal.png` | 176×160 / 228×182 / 298×212 / 376×242 / 444×270 / 514×300 |
| `noteskin\notes_flick_arrow_crtcl_0N(.diagonal).png` | 同上一行（关键音符用同一尺寸） |
| `noteskin\notes_friction_among_crtcl.png` | 104 × 112 |
| `noteskin\notes_friction_among_flick.png` | 104 × 112 |
| `noteskin\notes_friction_among_long.png` | 104 × 112 |
| `noteskin\notes_long_among.png` | 158 × 160 |
| `noteskin\notes_long_among_crtcl.png` | 158 × 160 |

`notes.png` 的七个切片自下而上是：

0. 长条头（带端帽）
1. 长条身
2. 长条尾
3. 普通键
4. 摩擦键（细）
5. 滑动键
6. 装饰/引导键

切片矩形与九宫格边界固定在 `textures\*.asset` 里（例如 `notes_0.asset` 的 `m_Rect` /
`m_Border`），画的时候按「这一片大致占满自己的 354 × 186」来构图就不会错位。

## 3. 图片要求

- **格式**：PNG，32 位带 alpha。背景必须透明。
- **颜色**：游戏**不做颜色染色**，颜色就是你画的颜色；不同键型靠切片区分，别指望运行时改色。
- **斜向箭头方向**：`*_diagonal` 指向**左上**（不是右上），且箭尖要是最高点，否则眼睛会读成反向。
- **不要放原作的素材**：仓库对外发布，原作美术不能进来。自己画的、CC0 的、买断授权的都可以。

## 4. 应用

```powershell
cd E:\DeepseekHarness\OpenSekai

# 先干跑一次：只检查尺寸是否匹配，不复制
# （尺寸不对的文件会被列出来并跳过，不会写坏仓库）
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\replace-note-skins.ps1 -Set all -From <你的皮肤目录>
```

输出 `copied <文件名>` 表示尺寸对、已复制；`size mismatch` 表示尺寸不对、已跳过。
最后一行给出汇总：`== imported 66, size-mismatched 0, skipped 0`。

然后把图片读进编辑器（Auto Refresh 是关的）：

```csharp
// unity_eval
foreach (var g in UnityEditor.AssetDatabase.FindAssets("t:Texture2D", new[]{"Assets/Game/Resources/live/note"})) {
    var p = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
    if (p.EndsWith(".png")) UnityEditor.AssetDatabase.ImportAsset(p, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
}
UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceSynchronousImport);
```

出包：

```powershell
unity command build run --target Android --outputPath "Builds/<名字>/OpenSekaiAtlas.apk" --confirm
```

## 5. 相关

- 生成器（占位图就是这么画出来的）：`Tools/replace-note-skins.ps1`，`-Apply` 落盘 / 默认只出预览。
- 原作备份（仅本机，`LocalPacks/` 已被 git 忽略）：`LocalPacks/original-skin`，
  `-Restore` 可一键还原原作用于对照。
- 素材来源与合规清单：`docs/operations/asset-provenance.md`。
