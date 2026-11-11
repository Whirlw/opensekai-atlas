# 模块划分

> 目的：给出目录与职责的对应表，说明新旧代码的边界在哪、依赖朝哪边指。
> 适用读者：开发者。
> 前置条件：读过 [架构总览](README.md)。
> 最后更新：2026-11-11

## 概述

运行时的依赖方向是 `OpenSekai.App → OpenSekai.Game`。编辑器程序集可调用两者，入口位于 `Assets/Game/Editor/`。

| 程序集 | 路径 | 职责与入口 |
| --- | --- | --- |
| OpenSekai.App | `Assets/Game/Runtime/OpenSekai/App/Bootstrap/` | `LibraryEntryPoint` 装配曲库入口 |
| OpenSekai.App | `Assets/Game/Runtime/OpenSekai/App/UI/` | `LibraryScreen` 和导入、设置、结算面板 |
| OpenSekai.App | `Assets/Game/Runtime/OpenSekai/App/Playback/` | `PlayService` 和 `LegacyLiveBootAdapter` 装配开局数据 |
| OpenSekai.App | `Assets/Game/Runtime/OpenSekai/Charts/` | `ChartDocument`、`ChartTimeline` 和 `ChartJson` |
| OpenSekai.App | `Assets/Game/Runtime/OpenSekai/Infrastructure/` | `ChartFileReader`、`SongPackageManager` 和曲库导入 |
| OpenSekai.App | `Assets/Game/Runtime/OpenSekai/Live/Conversion/` | `RuntimeChartConverter` 和 `RuntimeChartImporter` 组装运行时谱面 |
| OpenSekai.Game | `Assets/Game/Runtime/Sekai/` | `ScreenManager`、场景切换、Live 控制器、视图和音符 |
| OpenSekai.Game | `Assets/Game/Runtime/Shared/` | 设置、计分、触摸计算、SUS 转换、曲包模型与存储 |
| OpenSekai.Game | `Assets/Game/Runtime/Platform/` | 平台适配、CRI 音频 shim 和序列化兼容接口 |
| OpenSekai.Editor | `Assets/Game/Editor/` | Android 构建、AssetBundle 构建、资源检查和场景校验 |

构建场景在 `Assets/Game/Scenes/`。依次为 `Library.unity`、`TransitionBlank.unity` 和 `Core.unity`。

### 已移出工程的部分（备份目录已于 2026-11-03 删除，源码见重构前的 git 提交）

| 备份目录 | 内容 |
| --- | --- |
| `editor-tree-2026-11-01/` | 谱面编辑器 527 个脚本 |
| `editor-assets-2026-11-01/` | 编辑器画面 prefab、12 个编辑器 `ScreenLayerData`、编辑器场景 |
| `legacy-library-ui-2026-11-01/` | 旧曲库页 `ScreenLayerCustomMusicScoreManager`（3409 行） |
| `screens-registry-2026-11-01/` | 未被引用的页面注册表与 213 个 `ScreenLayerData` |
| `video-export-2026-11-02/` | 录屏/视频导出 15 个脚本 |
| `messagepack-2026-11-02/` | 标注备份、formatter、NuGet DLL |
| `palette-2026-11-02/` | uPalette 生成的 17 个脚本与 PaletteStore |
| `uiparticle-2026-11-02/` | SoftMask/UI Particle 的最初拷贝 |
| `recording-seam-2026-11-02/` | 录屏接口缝 `LiveRecordingHooks.cs` |
| `module-readmes-2026-11-02/` | `Assets/` 下 5 份过期模块 README（内容已吸收进 [曲包与谱面数据](chart-package.md) 等文档） |
| `ffmpeg-2026-11-02/` | 185.3 MB ffmpeg 二进制 |

备份目录 `_removed/` 已删除；`.gitignore` 仍保留 `/_removed/` 规则，将来再建备份时不会误提交。

## 示例

新增一个「曲库页按钮」应该动哪些文件：

```text
1. App/UI/LibraryScreen.cs          → 加按钮与回调
2. App/UI/LibraryXxxPanel.cs        → 如果要做浮层，单独一个文件
3. App/Playback/PlayService.cs      → 只有涉及开局流程才改
4. 编辑器验证：OpenSekai/Library/Verify Running Scene 仍应 valid:true
```

## 常见问题与故障排查

- **新代码引用了 `Sekai.MusicScoreMaker.Ingame.*`**：该命名空间已随编辑器删除，编译必然失败。
- **想知道某文件属于哪一层**：查看包含它的最近一层 `.asmdef`。

## 相关文档

- [曲包与谱面数据](chart-package.md)
- [打歌运行时](live-runtime.md)
- [ADR-0001 去掉谱面编辑器](../decisions/0001-drop-chart-editor.md)
