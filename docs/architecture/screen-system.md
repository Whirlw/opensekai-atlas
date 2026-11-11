# 页面系统

> 目的：说明引擎自带的 `ScreenManager` 页面栈怎么工作、现在还剩哪些页面、曲库页为什么不在这个系统里。
> 适用读者：开发者（加页面、改页面栈行为、排查「界面没出来」）。
> 前置条件：读过 [模块划分](modules.md)。
> 最后更新：2026-11-02

## 概述

页面系统是既有工程留下的基础设施：一张注册表（`EntryScreenLayers.asset`）把「页面类型枚举」映射到
「`ScreenLayerData` 资产」，每个资产再按名字去 `Resources/Screen/Prefabs/` 找 prefab。
本仓库把注册表从 218 条裁到 7 条，只留下真正会被推入的页面。

## 核心内容

### 加载与推送

```text
ScreenManager.LoadEntryScreenLayers()                 Sekai/ScreenManager.cs
  Resources.Load<ScreenLayerDataList>("Screen/SceneEntry/EntryScreenLayers")
    → 遍历 Entries，写入 screenMap[entry.ScreenType]
PushUIScreen(type)  →  screenMap 查不到 → LogError("Not Found ScreenLayer;")
                    →  查到 → ScreenLayerData.LoadScreenLayerPrefab()
                             Resources.Load<GameObject>("Screen/Prefabs/" + name)（失败再试小写路径）
```

- 页面栈是 `LimitedStack(16)`，超过上限会**静默丢弃最老的页面**。
- `ScreenLayer` 的生命周期动画是空壳：`PlayStartAnimation()` / `PlayExitAnimation()` 直接返回 `Done()`。

### 现存的 7 个页面

| 页面 | 触发方 |
| --- | --- |
| `Header` | `LibraryEntryPoint.AddScreen` |
| `TouchEffect` | 同上 |
| `InsertNoti` | 同上 |
| `LiveLoading` | `PlayService.StartAsync` 里 `PushUIScreen(LiveLoading, false)` |
| `Loading` | `ScreenManager` 内部 |
| `NetworkIndicator` | `ScreenManager` 内部 |
| `AssetRemoving` | `ScreenManager` 内部 |

`Resources/screen/prefabs` 里正好 7 个 prefab，与上表一一对应；其余 213 个 `ScreenLayerData` 与 5 个未被引用的
`sceneentry` 里多余的条目已删（备份目录后来也清理了，需要时从重构前的 git 提交取回）。裁剪依据：`PushUIScreen` 只查 `screenMap`，
未知类型只打日志，不会崩，所以裁剪是安全的。

### 曲库页不在页面系统里

`LibraryScreen` 自己建 Canvas 与相机（`LibraryScreenCamera`，`ScreenSpaceCamera` + `cullDepth 100`），
不走 `ScreenLayerData`，只通过 `LibraryPageRegistry` 对外暴露 `ILibraryPage`（`IsReady` 等）。
这样做的原因：曲库页是全新写的，不需要旧的 prefab/注册表链路，也能被相机截图工具直接渲染。

`PlayService.IsLibraryAlive()` 的判定顺序因此是：先看 `LibraryPageRegistry.Current`，为空才回退到旧的
`IsActiveScreen(MenuScreenType.MusicScoreMakerTop)`（该页面已删除，回退分支实际不会命中）。

### 场景枚举

`Sekai.SceneManager.Scene.Library = 10` 追加在旧枚举末尾，没有重排既有值。

## 示例

加一个新页面（例如「曲包详情」）需要的四步：

```text
1. 在 Resources/Screen/Prefabs/ 放 prefab（名字与 ScreenLayerData 的 name 一致）
2. 建一个 ScreenLayerData 资产，name 与 prefab 同名
3. 在 EntryScreenLayers.asset 的 Entries 里加一条（ScreenType + ScreenLayerData 引用）
4. 代码里 PushUIScreen(<ScreenType>) —— 枚举值必须先在 MenuScreenType 里定义
```

如果新页面像曲库页一样是代码构建的，就别进这套系统，走 `LibraryPageRegistry` 的模式。

## 常见问题与故障排查

- **日志 `Not Found ScreenLayer;`**：注册表没这条，或 prefab 名字对不上。
- **推了页面但没显示**：检查 prefab 的 Canvas 渲染模式；`ScreenSpaceOverlay` 不会被任何相机渲染（截图工具也拍不到）。
- **页面被莫名顶掉**：栈上限 16，`LimitedStack` 会丢最老的。

## 相关文档

- [模块划分](modules.md)
- [AssetBundle 与清单](asset-bundles.md)
- [重构日志 2026-11](../changelog/2026-11-refactor-log.md)
