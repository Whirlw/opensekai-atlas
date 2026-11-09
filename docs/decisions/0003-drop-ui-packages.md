# ADR-0003：摘掉 uPalette / SoftMask / UI Particle 与视频导出

> 目的：记录四个第三方依赖为什么被删、删的时候付出什么代价。
> 适用读者：开发者。
> 前置条件：读过 [模块划分](../architecture/modules.md)。
> 最后更新：2026-11-02

## 状态

已生效（2026-11-02）。

## 背景

四个依赖都不是「被大量使用」，而是「被一两个文件用到」：

| 依赖 | 实际使用面 |
| --- | --- |
| uPalette | 只提供 `ColorEntry` 枚举；62 个颜色码本来就硬编码在 `Sekai/PaletteUtility.cs` 的 switch 里 |
| SoftMask | 只在 `Sekai/UI/UIPartsMarquee.cs` 里有一个字段与两处调用，**没有任何 prefab 挂过该组件** |
| UI Particle | 只被 `Sekai/UI/CustomUIParticle.cs`（17 行，仅加 `SetScale`）继承，挂在 3 个加载/切场 prefab |
| 视频导出（`OPENSEKAI_VIDEO_EXPORT`） | 模块能编译，但入口挂在已删除的旧曲库页上，**全仓没有任何 `new VideoGenerationBootData`** |

## 决定

1. **uPalette**：把 `ColorEntry` 枚举搬成项目内文件 `Assets/Scripts/Assembly-CSharp/Sekai/ColorEntry.cs`，
   删 4 个文件的 `using uPalette.Generated;`，删包。
2. **SoftMask**：删 `UIPartsMarquee` 里的 `softMask` 字段与两处使用（保留 `RectMask2D` 分支），删包。
3. **UI Particle**：删 `CustomUIParticle.cs`，从 3 个 prefab 里逐块摘掉该组件，删包。
4. **视频导出**：整块删除（当时先移到 `_removed/video-export-2026-11-02/`，该目录已于 2026-11-03 清理；这段代码从未进过提交，无法从历史取回），`RecordingBuildRules` 的权限剥离不再包在
   `#if !OPENSEKAI_VIDEO_EXPORT` 里（始终生效）；核心侧预留的「录屏接口缝」随后也删掉。

## 后果

**收益**：包从 43 降到 39（再往前是 56）；`Assets` 下少 17 个 uPalette 生成脚本、1 个 UI 组件、
15 个视频导出脚本；不再维护一个「能编译但点不到」的模块。

**代价**

- 加载页与切场特效里的粒子装饰消失（布局与功能不受影响）
- 视频导出功能彻底没有；仓库根 185 MB 的 `ffmpeg/` 随之下岗，已从工作区删除（更早的提交里仍可取回）

**踩坑（已写入红线）**

1. 摘包必须在 Unity 关闭时改 manifest，或用 Package Manager API —— 运行时手改会让 `SourceAssetDB`
   留下指向已删文件的记录，下次启动直接卡在弹窗，只能删 `Library/ArtifactDB`、`SourceAssetDB` 重建（约 6 分钟）。
2. `.meta` 文件必须以换行结尾，否则重建资产库时会报 `Parser Failure`。

## 被否决的替代方案

| 方案 | 否决原因 |
| --- | --- |
| 保留 SoftMask（"以后可能用到"） | 没有任何 prefab 挂它，属于纯死代码 |
| 保留视频导出到「以后再接入口」 | 入口随旧曲库页一起删了，留着只是编译期负担与隐私权限风险 |
| 用另一个 UI 粒子库替换 | 只有 3 个装饰性 prefab 用它，直接摘掉比替换便宜 |

## 相关文档

- [重构日志 §14](../changelog/2026-11-refactor-log.md)
- [故障排查](../operations/troubleshooting.md)
