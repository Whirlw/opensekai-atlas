# AssetBundle 与清单

> 目的：说明运行时资源（音符、打击特效、音效、shader、菜单音）怎么打、怎么列清单、怎么被加载。
> 适用读者：开发者（改资源、改打包、排查「资源加载不到」）。
> 前置条件：读过 [模块划分](modules.md)。
> 最后更新：2026-11-11

## 概述

打包使用白名单与显式 `AssetBundleBuild[]`。0.3.0 发布包包含 10 个 bundle，产物落在
`Assets/StreamingAssets/data`，清单是 JSON（`AssetBundleInfo.bytes`），运行时由 `AssetBundleManager` 读清单并按需加载。

## 核心内容

### 白名单与实际产物

`Assets/Game/Editor/OpenSekaiAssetBundleBuildPipeline.cs` 的 `LightweightBundleWhitelist`：

| bundle | 内容 |
| --- | --- |
| `effect_asset/live/tap_effect/0`、`/1` | 两套打击特效 |
| `effect_asset/live/result/default` | 结算特效 |
| `live/2dmode/background/default` | 2D 背景 |
| `live/sound/live_se/default` | live 音效 |
| `live/tap_se/custom01`、`custom02` | 两套打击音 |
| `shader/live`、`shader/particles`、`shader/sandbox` | shader 变体 |
| `sound/menu/menu_common` | 仍在白名单中，0.3.0 清单无对应条目 |

两套音符皮肤 prefab 位于 `Assets/Game/Resources/live/note/`。内置判定和演出音 WAV 位于 `Assets/StreamingAssets/sound/`。

重构前是 `AssetDatabase.GetAllAssetBundleNames()` 全量打包（473 个带标签资源 / 15 个 bundle 名）。
现在按白名单组 `AssetBundleBuild[]` 传给 `BuildPipeline.BuildAssetBundles` ——
**只过滤名字数组不会限制实际打包内容，必须传 `AssetBundleBuild[]`**。

### 清单：`AssetBundleInfo.bytes`

- 位置：`Assets/StreamingAssets/data/AssetBundleInfo.bytes`
- 类型：`Assets/Game/Runtime/Sekai/AssetBundleInfo.cs`（`version` + `Dictionary<string, AssetBundleElement> bundles`），
  元素类型 `AssetBundleElement`（`bundleName` / `cacheFileName` / `hash` / `crc` / `fileSize` / `dependencies` / `paths` / …）
- 序列化：`CP/BinarySerializer.cs`，2026-11-02 起是 **Newtonsoft JSON**（此前是 MessagePack），大小 53,874 → 55,150 B
- 字段名与旧 MessagePack 的 key 完全一致，因此 JSON 直接沿用字段名，不需要 `[JsonProperty]`

### 产物与打包位置

```text
Assets/StreamingAssets/data/
  AssetBundleInfo.bytes
  <bundle 文件的 hash 目录>/*
```

0.3.0 发布 APK 的 `assets/data/` 有 10 个 bundle 和一份清单。核对其他构建时，比较清单与实际 bundle 文件。

### 运行时加载

`Assets/Game/Runtime/Sekai/AssetBundleManager.cs`：读清单 → 按 `bundleName` 找 `AssetBundleElement` →
从 `StreamingAssets/data`（或平台对应路径）加载 → 按 `paths` 取具体资源。
`AssetBundleElement` 里的 `dependencies` 决定依赖加载顺序。

## 示例

重新打包（编辑器菜单）：

```text
OpenSekai / AssetBundles / Build Asset Bundles And Info
```

注意菜单名是 `AssetBundles`（连写），写成 `Asset Bundles` 会提示菜单项不存在。

核对产物：

```powershell
(Get-ChildItem Assets/StreamingAssets/data -Recurse -File | Measure-Object).Count   # 期望 42
Get-Content Assets/StreamingAssets/data/AssetBundleInfo.bytes -TotalCount 1          # 期望以 {"version":"2.0.0" 开头
```

## 常见问题与故障排查

- **APK 里没有 `assets/data`**：出包前没跑打包菜单，`StreamingAssets/data` 是空的。
- **清单是旧的**：跑一次上面的菜单；清单不会随代码编译自动更新。
- **加载到旧资源**：清掉 `Library/Bee/Android/Prj/IL2CPP/Gradle` 后重出包，Unity 会复用旧 APK（见
  [Android 构建](../operations/android-build.md) 的坑 3）。
- **改了字段名后读不出来**：清单字段名属于对外契约，见 [ADR-0005](../decisions/0005-no-legacy-compat.md)。

## 相关文档

- [ADR-0002 清单改 JSON](../decisions/0002-json-ab-catalog.md)
- [Android 构建](../operations/android-build.md)
- [曲包与谱面数据](chart-package.md)
