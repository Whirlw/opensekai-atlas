# 字体

目的：说明项目里用到的字体、它们各自的角色、许可，以及如何重新生成 TMP 字体资产。
适用读者：需要改 UI 文案、替换字体或排查「文字空白 / 方块 / 大小异常」的开发者。
前置条件：Unity 6000.6.7f1，项目已打开一次（`Library/` 已生成）。
最后更新：2026-11-02

## 概述

项目自带两套字体：

| 用途 | 字体 | 位置 | 许可 |
| --- | --- | --- | --- |
| 曲库页面（`LibraryScreen`） | Noto Sans SC 子集 | `Assets/Game/Resources/Fonts/NotoSansSC-UI.ttf` | SIL OFL 1.1（`OFL.txt` 同目录） |
| 打歌与其余 UI | 思源黑体 Source Han Sans SC（Medium / Bold） | `Assets/Game/Resources/Fonts/SourceHanSansSC-{Medium,Bold}.otf` | SIL OFL 1.1 |

TMP 侧有 4 个字体资产（`Assets/Graphics/Resources/font/`）：

- `SourceHanSansSC-Medium SDF_Base` / `SourceHanSansSC-Bold SDF_Base`：绝大多数 `TMP_Text` 的 `m_fontAsset`
- `SourceHanSansSC-Medium SDF_Dynamic` / `SourceHanSansSC-Bold SDF_Dynamic`：作为前两者的 fallback

四个资产都是 **Dynamic** 图集（2048×2048，point size 35，padding 5）。第一次用到某个字时 TMP 才把字形烘进图集，所以新克隆仓库里它们的图集是 1×1 占位，这属于正常状态。

## 快速开始

改文案不需要动字体：只要字符落在思源黑体的覆盖范围内，运行时自动烘字。

排查「文字不显示」：

1. 编辑器里跑一次 `OpenSekai/Library/Play Local Baseline Automatically`，看结算面板是否有字。
2. 若结算面板正常、只有某个 prefab 的文本空白，检查该 `TMP_Text` 的 `m_sharedMaterial` 是否指向了一个旧字体图集（应为 `{fileID: 0}`，让 TMP 用字体资产自己的材质）。
3. 若全部空白，检查 `FontAssetManager` 里的四个 `Resources.Load` 路径是否与 `Assets/Graphics/Resources/font/` 下的资产名一致。

## 详细说明

### 为什么从 Rodin 换成了思源黑体

原项目的 FOT-RodinNTLG Pro 是日文字体，随包分发有许可风险，而且没有简体中文覆盖。Unity 6 升级时 `TMP_FontAsset.ClearFontAssetData` 还会在「字体资产没有可用图集像素」的播放器包里触发空指针崩溃（`FontAssetManager.SetupBuiltinFontAsset` → `ClearAtlasTextures`）。换成 OFL 的思源黑体后，字体可以随包分发，也可以随时重烘。

### 图集尺寸与指标

- 2048×2048，point size 35，padding 5，SDFAA，Dynamic + multiAtlas
- `m_FaceInfo` 的行高/升降部按被替换的 Rodin 资产对齐（lineHeight 70、ascent 30.8、descent −4.2…），避免换代后行距与基线位置漂移
- 一个 2048 图集在 35pt 下大约容纳 1600 个字形；用满后 TMP 会开新页

### 材质

`Assets/Graphics/Resources/font/` 下还有若干「带描边 / 带阴影」的 TMP 材质子集（`musicdifficulty/`、`scenario/`、`musicscoremaker/`）。它们由对应字重的主材质克隆而来，`_MainTex` 指向该字重字体资产的图集纹理。重新烘字后需要重新克隆一次（见下）。

### 重新生成字体资产

1. 用 `Tools/subset-ui-font.ps1` 或直接放 OTF 到 `Assets/Game/Resources/Fonts/`。
2. 菜单 `OpenSekai/Dev/Fonts/Build Source Han Sans Assets` 生成 4 个资产（同一路径重建会保留 GUID，引用不会断）。
3. 若行高不对，用 SerializedObject 把 `m_FaceInfo` 的 `m_LineHeight` / `m_AscentLine` / `m_DescentLine` 设成目标值。
4. 材质子集：以新字体资产的主材质为模板 `new Material(target.material)`，`CopyPropertiesFromMaterial(old)` 后把 `_MainTex` 指到 `target.atlasTextures[0]`，覆盖原 `.mat`。

## 示例

`FontAssetManager.SetupBuiltinFontAsset()` 在启动时把 Base 资产与 Dynamic 资产串成 fallback 链：

```csharp
ClearPrimaryFontAssetData(_baseFontDB);
ClearFallbackFontAsset(_baseFontDB);
AddFallbackFontAsset(_baseFontDB, _dynamicFontDB);
```

`ClearPrimaryFontAssetData` 走 `SafeClearFontAssetData`：只有图集边长大于 1 时才真正调用 `ClearFontAssetData`，否则跳过——1×1 占位图集正是播放器崩溃的原因。

## 常见问题

**为什么新克隆仓库里字体资产只有 6 KB？**
因为图集是 1×1 占位，字形按需烘焙。跑一次游戏后编辑器里的资产会变大，这是正常的。

**换成思源黑体后正文看起来偏小 / 行距不同？**
先确认 `m_FaceInfo.m_LineHeight` 是否等于 70（与被替换的 Rodin 对齐）；再确认 `m_PointSize` 是 35。两者任一不对都会让换行与基线整体偏移。

**能不能只用一套字体？**
曲库页面仍用 Noto Sans SC 子集（1.4 MB，只覆盖 GB2312 一级字与符号），因为它是被 `Resources.Load` 直接加载的 TTF。要合并的话需要把曲库也改成加载 TMP 资产。

## 相关文档

- [界面与页面系统](../architecture/screen-system.md)
- [打歌运行时](../architecture/live-runtime.md)
- [Unity 6 升级清单](../operations/unity6-upgrade.md)
- [2026-11 重构日志](../changelog/2026-11-refactor-log.md)
