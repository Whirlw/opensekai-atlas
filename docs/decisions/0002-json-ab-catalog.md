# ADR-0002：AssetBundle 清单从 MessagePack 改为 JSON

> 目的：说明为什么只为一份 54 KB 的清单放弃 MessagePack，并给出改造范围与风险控制。
> 适用读者：开发者。
> 前置条件：读过 [AssetBundle 与清单](../architecture/asset-bundles.md)。
> 最后更新：2026-11-02

## 状态

已生效（2026-11-02）。

## 背景

全仓真正被 MessagePack 序列化的只有 `AssetBundleInfo.bytes` 一个文件（53,874 B）：

- `CP/BinarySerializer.cs`（33 行）包了 `MessagePackSerializer`，只有两个调用点：
  `Assets/Editor/OpenSekaiAssetBundleBuildPipeline.cs` 写、`Sekai/AssetBundleManager.cs` 读
- 另有一个手写的 `AssetBundleInfoMessagePackFormatter`（290 行）与 `MessagePackSerializerEx`（0 调用者）
- 121 个类的 1,115 条 `[MessagePackObject]`/`[Key]`/`[IgnoreMember]` 标注没有任何序列化用途
- 代价：6 个 NuGet DLL（约 1.0 MB）、`packages.config`、NuGetForUnity 包、MessagePack 包

## 决定

分两阶段摘除，先换格式、后删依赖：

1. **阶段一**：`BinarySerializer` 改用 Newtonsoft JSON；
   `AssetBundleInfo` / `AssetBundleElement` 去掉全部 MessagePack 标注与回调接口；
   删 formatter 与 `EnsureRegistered()`；重出清单并验证 AB 加载。
2. **阶段二**：批量删 1,115 条标注；用 Package Manager 摘掉 `com.github.messagepack-csharp` 与
   `com.github-glitchenzo.nugetforunity`；搬走 `Assets/Packages` 与 `packages.config`。

关键细节：

- JSON 设置必须带 `ObjectCreationHandling.Replace`，否则反序列化会往构造函数里已 `new` 的字典**追加**
- `AssetBundleElement` 的 key 名与字段名本来就一致，因此不需要 `[JsonProperty]`
- 保留 10 行的本地 shim `MessagePack/IMessagePackSerializationCallbackReceiver.cs`：约 28 个类实现它，
  删掉 `using` 会集体 `CS0246`

## 后果

**收益**：清单 53,874 → 55,150 B（+2.4%）；包从 41 个降到 39 个；少 1 MB DLL 与一个 NuGet 工具链。

**代价**：清单变成可读文本，文件略大；`AssetBundleElement` 的字段名从此是契约（见 ADR-0005 的例外）。

**风险控制**：先只改格式、真机验证 AB 加载通过，再动标注与 DLL —— 出问题时能立刻区分是格式还是依赖。

## 验证

上图是摘掉 MessagePack 之后真机上的一次自动打歌：音符、长条、打击特效、Fever 光束都正常，
说明 AB 清单以 JSON 形式被正确读取。

## 被否决的替代方案

| 方案 | 否决原因 |
| --- | --- |
| 手写二进制编解码 | 为 54 KB 的清单维护 ~120 行序列化代码，收益不抵维护成本 |
| 只删 Analyzer/SourceGenerator DLL | 省约 340 KB，但 MessagePack 运行时依赖还在，摘不干净 |

## 相关文档

- [AssetBundle 与清单](../architecture/asset-bundles.md)
- [重构日志 §15](../changelog/2026-11-refactor-log.md)
