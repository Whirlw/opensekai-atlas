# ADR-0001：去掉谱面编辑器，只保留打歌

> 目的：说明为什么把整个编辑器树移出工程，以及这样做的代价。
> 适用读者：开发者。
> 前置条件：读过 [架构总览](../architecture/README.md)。
> 最后更新：2026-11-02

## 状态

已生效（2026-11-01）。

## 背景

编辑器（`Sekai/MusicScoreMaker/Ingame`、`OutGame`）与打歌共用一套类型：
`MusicScoreMakerData` 既是编辑器文档模型，也是 live 的输入。结果是：

- 打歌路径必须携带编辑器的撤回栈、事件分发、MessagePack 标注等大量状态
- 编辑器 527 个脚本、上千个 prefab/资产全部编在同一个 `Assembly-CSharp` 里
- 只要保留编辑器，任何「面向播放器」的裁剪都会被它的依赖拖住

## 决定

编辑器整块移出工程（当时移到仓库根 `_removed/`，该备份目录已于 2026-11-03 清理，源码在重构前的 git 提交里）：

- 527 个编辑器脚本 + 场景 + 编辑器画面 prefab + 12 个编辑器 `ScreenLayerData`
- 旧曲库页 `ScreenLayerCustomMusicScoreManager`（3409 行）
- 依赖编辑器模型的工具：`DataExtractionValidation`、`LegacyChartBridge`、`LegacySusConversionOracle` 等

打歌改走 `Assets/OpenSekai/` 的新链路：`ChartFileReader` → `ChartDocument` → `RuntimeChartConverter`。

## 后果

**收益**

- `Assets` 下 `.cs` 从 1437 降到 909
- 打歌路径不再出现任何编辑器类型；`Sekai.MusicScoreMaker` 只剩 5 个存储层文件
- 页面注册表可以从 218 条裁到 7 条

**代价**

- 不能在工程内做谱面编辑；曲包必须来自外部工具或已有文件
- 旧 JSON 里的编辑器类型名只能靠 `ChartJson` 的 binder 兼容映射（只映类型名，不映模型）

**遗留**

- 删编辑器后暴露的「丢失脚本」引用共 97 个 GUID，全部是历史遗留（基线里就没有对应 `.meta`），
  不是本次删除造成，因此没有做批量 Strip。

## 被否决的替代方案

| 方案 | 否决原因 |
| --- | --- |
| 保留编辑器但用 asmdef 隔离 | 编辑器与打歌共用 `MusicScoreMakerData`，隔离要先拆模型，工作量与直接删相当 |
| 只删编辑器入口、保留代码 | 打歌仍被迫加载编辑器类型，裁剪没有意义 |

## 相关文档

- [模块划分](../architecture/modules.md)
- [ADR-0005 不背历史包袱](0005-no-legacy-compat.md)