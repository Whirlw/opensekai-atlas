# 字体

目的：说明项目里用到的字体、它们各自的角色、许可，以及如何重新生成 TMP 字体资产。
适用读者：需要改 UI 文案、替换字体或排查「文字空白 / 方块 / 大小异常」的开发者。
前置条件：Unity 6000.6.7f1，项目已打开一次（`Library/` 已生成）。
> 最后更新：2026-11-12

## 概述

项目自带两套字体：

| 用途 | 字体 | 位置 | 许可 |
| --- | --- | --- | --- |
| 曲库页面（`LibraryScreen`） | Noto Sans SC 子集 | `Assets/Game/Resources/Fonts/NotoSansSC-UI.ttf` | SIL OFL 1.1（`OFL.txt` 同目录） |
| 打歌与其余 UI | 思源黑体 Source Han Sans SC（Medium / Bold） | `Assets/Game/Resources/Fonts/SourceHanSansSC-{Medium,Bold}.otf` | SIL OFL 1.1 |

TMP 侧有 4 个字体资产（`Assets/Graphics/Resources/font/`）：

- `SourceHanSansSC-Medium SDF_Base` / `SourceHanSansSC-Bold SDF_Base`：绝大多数 `TMP_Text` 的 `m_fontAsset`
- `SourceHanSansSC-Medium SDF_Dynamic` / `SourceHanSansSC-Bold SDF_Dynamic`：作为前两者的 fallback

四个资产都是 **Dynamic** 图集（2048×2048，point size 35，padding 5）。第一次用到某个字时 TMP 才把字形烘进图集，所以新克隆仓库里它们的图集通常是 1×1 占位，这属于正常状态。

一个例外：`SourceHanSansSC-Medium SDF_Dynamic` 现在**预烘了省略号 U+2026**（图集 2048×2048、文件约 8.4 MB）。原因见下面「省略号为什要预烘」。

## 快速开始

改文案不需要动字体：只要字符落在思源黑体的覆盖范围内，运行时自动烘字。

排查「文字不显示」：

1. 编辑器里跑一次 `OpenSekai/Library/Play Local Baseline Automatically`，看结算面板是否有字。
2. 若结算面板正常、只有某个 prefab 的文本空白，检查该 `TMP_Text` 的 `m_sharedMaterial` 是否指向了一个旧字体图集（应为 `{fileID: 0}`，让 TMP 用字体资产自己的材质）。
3. 若全部空白，检查 `FontAssetManager` 里的四个 `Resources.Load` 路径是否与 `Assets/Graphics/Resources/font/` 下的资产名一致。

## 详细说明

### 为什么从 Rodin 换成了思源黑体

此前的日文字体随包分发有许可风险，而且没有简体中文覆盖。Unity 6 升级时 `TMP_FontAsset.ClearFontAssetData` 还会在「字体资产没有可用图集像素」的播放器包里触发空指针崩溃（`FontAssetManager.SetupBuiltinFontAsset` → `ClearAtlasTextures`）。换成 OFL 的思源黑体后，字体可以随包分发，也可以随时重烘。

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

## 跑过游戏后变脏的字体资产

TMP 会在运行时把 kerning、连字和字形按需烘进资产文件本身。这四份资产被 prefab 和材质按 GUID 引用，所以必须留在仓库里，但内容一般只应是最初的冷状态（1×1 占位图集、空的特性表）。

实测（2026-11-12）：`SourceHanSansSC-Medium SDF_Dynamic.asset` 跑过一次后从 6,516 字节涨到约 967 KB——2,316 条 kerning 对、113 条连字替换，共 35,222 行 YAML，而字形表仍是空的；`SourceHanSansSC-Medium SDF_Base.asset` 同时丢掉一条连字记录。提交这种缓存会让仓库和安装包都变大，diff 无法审阅、无法合并，换台机器内容还不一样。

### 省略号为什么要预烘

TMP 的 `TextOverflowModes.Ellipsis` 只做 `HasCharacter('…')`，不会自己去加字形。曲库页的字体是运行时解析的，真机实测：给运行时 `CreateFontAsset` 出来的资产调 `TryAddCharacters("…")` **只写进了 character 行、没写进 glyph**（日志 `has=True chars=1`），0.8 秒后 TMP 仍把 20 个标签降级成截断。所以改成让资产自带这个字形：

- `SourceHanSansSC-Medium SDF_Dynamic` 在编辑期烘入 U+2026，`LibraryUi.ResolveCjkFont()` 优先用它，不再依赖运行时光栅化。
- 这条预烘是**有意为之**，因此 `Tools/generated-assets.txt` 里它的期望大小是 8,395,651 字节而不是 6,516。改动它只需重烘（`-UpdateGeneratedAssets`），不要"还原"回冷状态——还原会让省略号缺陷复现。
- `ResourceCatalogTests.TheShippedProjectFontCarriesTheEllipsisGlyph` 守住这一点：读资产、断言 `HasCharacter('…')` 且 `glyphTable` 非空（只读，不改资产）。
- 这份资产的 **Clear Dynamic Data on Build 是关的**（`m_ClearDynamicDataOnBuild: 0`）。TMP 自带的 `TMP_PreBuildProcessor` 每次出包前都会清空勾了这一项的动态字体资产的字符表和字形表——以前每次装机都把预烘的省略号清掉、写回磁盘，打出来的包里也就没有它。其余三份是冷状态，保持勾选。

### 不再需要手动还原（2026-11-14 起）

`Assets/Game/Editor/GeneratedAssetSaveGuard.cs` 让清单里的文件在编辑器里保持提交时的样子：

- **不写盘**：`OnWillSaveAssets` 把这些路径从每次保存里拿掉。Play 模式里 TMP 烘的字形、`FontAssetManager` 启动时的清空与重建，都只发生在内存里。
- **丢弃内存改动**：回到编辑模式时、以及每次出包前（顺序 −100，早于 TMP 的预构建步骤），把这些资产从内存卸载、下次访问时从文件重新读，所以包里装的是提交的版本，而不是这台机器刚烘的缓存。
- **有意重烘**：在 `GeneratedAssetSaveGuard.AllowSaves()` 范围内保存不受限。`FontAssetRebuilder.BuildAssets` 已经包在里面；别的重烘脚本也要这样包。

`GeneratedAssetSaveGuardTests` 守住这三点。下面的还原脚本和检查闸门照旧保留，作为最后一道防线（例如在编辑器外改了文件）。

受管清单只有一处：[`Tools/generated-assets.txt`](../../Tools/generated-assets.txt)（sha256 + 字节数 + 路径）。两道闸门：

```powershell
# 资产在磁盘上变脏（有了保存拦截后一般不会发生）：还原（只会动真正脏了的条目）
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/restore-generated-assets.ps1

# 确实要重新烘（菜单 OpenSekai/Dev/Fonts/Build Source Han Sans Assets 之后）
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/verify-structure.ps1 -UpdateGeneratedAssets
```

`Tools/verify-structure.ps1` 发现任一条目与清单不符就返回 1，CI 的 `structure` 作业也会跑它，所以误提交会被拦住。第二条命令把新哈希写回清单，**清单要和字体资产一起提交**——这样"重新烘过字"是一次可见的改动，而不是混在别的提交里。要新增受管资产，往清单里加一行路径即可。

## 常见问题

**为什么新克隆仓库里字体资产只有 6 KB？**
因为图集是 1×1 占位，字形按需烘焙。跑一次游戏后编辑器里的资产会变大，这是正常的，但**不要把变大的版本提交**，见上一节。

**换成思源黑体后正文看起来偏小 / 行距不同？**
先确认 `m_FaceInfo.m_LineHeight` 是否等于 70（与被替换的 Rodin 对齐）；再确认 `m_PointSize` 是 35。两者任一不对都会让换行与基线整体偏移。

**能不能只用一套字体？**
可以，现在就是这么做的：曲库页与打歌都走思源黑体。`LibraryUi.ResolveCjkFont()` 的优先级是 ①项目内预烘的 `SourceHanSansSC-Medium SDF_Dynamic`（自带省略号）②`ResourceCatalog.MediumTtfFont` 在运行时建的 TMP 资产 ③OS 已装字体 ④`Bold SDF_Dynamic` 后备。2026-11 之前第 ① 项指向 `Fonts/NotoSansSC-UI`——一个仓库里并不存在的 Noto 子集，导致那条分支恒返回 null、曲库一直在走后备。

## 相关文档

- [界面与页面系统](../architecture/screen-system.md)
- [打歌运行时](../architecture/live-runtime.md)
- [Unity 6 升级清单](../operations/unity6-upgrade.md)
- [2026-11 重构日志](../changelog/2026-11-refactor-log.md)
