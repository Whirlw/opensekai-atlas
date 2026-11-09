# 编辑器菜单

> 目的：把工程里可用的编辑器菜单列全，说明每个菜单做什么、什么时候该用。
> 适用读者：开发者、测试人员。
> 前置条件：工程能在编辑器里正常编译（见 [环境与运行](../getting-started/build-and-run.md)）。
> 最后更新：2026-11-02

## 概述

菜单分三组：`OpenSekai/Library/*`（场景与流程校验）、`OpenSekai/Dev/*`（构建规则自检）、
`OpenSekai/*`（AssetBundle 打包）。CRIWARE 的菜单来自官方插件，与本次重构无关。

## 核心内容

### `OpenSekai/Library/*`

| 菜单 | 什么情况下用 | 输出 |
| --- | --- | --- |
| `Create or Update Scene` | 首次搭场景，或场景外壳需要重建 | 只在目标不存在时复制 UI 外壳；当前场景有未保存改动时拒绝执行 |
| `Begin Startup Check` | Play 模式下窗口没焦点、需要后台推进时 | 临时允许后台运行，停止 Play 后恢复 |
| `Verify Running Scene` | 每次改完曲库/入口，验证是否还正常 | `[LibraryCheck] {...}` 一行 JSON |
| `Play Local Baseline Automatically` | 不开外部曲包，用内置 64 音符样本验证打歌 | 直接调 `PlayService.StartAsync`，跑完出结果面板 |
| `Play Local Baseline Manually` | 同上，但走手动判定 | — |
| `Retire Local Playback Check` | 退出上一条菜单开的打歌 | 按「中途退出」结算 |
| `Verify Direct Play Failures` | 改过开局失败分支后回归 | 依次验证缺文件、坏谱面、取消、重复请求、失败清理 |

`[LibraryCheck]` 的字段含义：

| 字段 | 期望 |
| --- | --- |
| `valid` | `true` |
| `scene` / `route` | `Library` |
| `libraryEntries` | `1`（一个 `LibraryEntryPoint`） |
| `editorEntries` | `0`（不再有 `MusicScoreMakerEntryPoint`） |
| `lightweightLibraryScreens` | `1` |
| `eventSystems` | `1` |
| `isStartingPlayback` | `false` |

### `OpenSekai/Dev/*`

| 菜单 | 用途 |
| --- | --- |
| `Verify Recording Permission Rule` | 断言权限剥离规则不删 `tools:node="remove"` 标记；期望 `plainDeclarations=0 removeMarkers=2` |

### `OpenSekai/AssetBundles/*`

| 菜单 | 用途 |
| --- | --- |
| `Build Asset Bundles And Info` | 按 15 个白名单 bundle 打包到 `Assets/StreamingAssets/data`，并重写 `AssetBundleInfo.bytes` |

菜单名是 `AssetBundles`（连写）。写成 `Asset Bundles` 会提示菜单项无效。

## 示例

日常改完曲库页的验证组合：

```text
1. 等编译（控制台无 error CS）
2. Play
3. OpenSekai/Library/Verify Running Scene   → [LibraryCheck] "valid":true
4. OpenSekai/Library/Play Local Baseline Automatically → 1,000,000 / S
5. Stop
```

## 常见问题与故障排查

- **菜单点不动 / 没反应**：Unity 正在编译或处于 `is_changing` 状态，等编译完成再点。
- **`Play Local Baseline*` 报样本缺失**：内置样本不在预期目录，菜单会给出明确提示而不会创建曲包。
- **`Verify Running Scene` 报 `valid:false`**：先看 `scene` 与 `libraryEntries` 两个字段，通常是场景里多了/少了入口对象。

## 相关文档

- [环境与运行](../getting-started/build-and-run.md)
- [AssetBundle 与清单](../architecture/asset-bundles.md)
- [故障排查](troubleshooting.md)
