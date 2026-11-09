# 2026-11 发布前清理：素材全替换、去抽卡、体积 117 → 79.7 MB

> 目的：记录发 GitHub 之前这一轮清理做了什么、每件事的判据，以及「以后加素材时要守住什么」。
> 适用读者：要往仓库里放素材或删功能的人。
> 前置条件：读过 [素材来源清单](../operations/asset-provenance.md) 与 [发布前素材总检](../operations/pre-release-asset-audit.md)。
> 最后更新：2026-11-09

## 概述

仓库从「能跑但带着原作素材」变成「可以公开」：音频、音符皮肤、HUD、判定特效、加载背景全部换成自制或 CC0 资源；上游的抽卡、谱面编辑器、旧页面体系一并删掉。APK 从 117.05 MB 降到 79.65 MB。

## 素材替换

| 类别 | 做法 | 判据 |
| --- | --- | --- |
| 音频 | 42 个自制 CC0 wav 放进 `StreamingAssets/sound/<bundle>/<cue>.wav`，启动时按 `manifest.csv` 注册为外部 cue sheet，覆盖同名 ACB；随后删掉 4 个 ACB | 真机日志四行 `external pack: 13/13/12/4 cues`；`Tools/audit-audio-provenance.ps1` 报 `AUDIT OK` |
| 音符皮肤 | 两套 `noteskin` 分目录导入（`new_notes` → custom01、`new_notes_v2` → custom02） | 两套 `notes.png` 哈希不同，可在设置里切换对比 |
| HUD / 判定特效 | 画包 PNG 按精灵矩形贴进 27 个图集 | `Tools/audit-atlas-coverage.ps1` 报 0 张仍是原作 |
| 加载背景 | 6 张单元插画 + 通用背景改为程序生成（同名同尺寸） | 生成脚本 `Tools/replace-original-backgrounds.ps1` 自带 1:1 校验 |

替换链路的关键教训写在 [素材包导入](../operations/material-pack-import.md)：编辑器里那个 `PaintKit` apply 会打印「applied 157」却不动像素，实际生效的是 `Tools/apply-atlas-art.ps1`。

## 功能删除

- 抽卡：29 个 `Gacha*` / `MasterGacha*` 脚本 + header 里 8 个 gauge 子物体 + 2 个 gauge prefab。它没有入口，但一直被 header 引用着。
- 谱面编辑器、旧页面体系、`ScreenLayer*` 之外的全部旧 UI。
- 菜单 UI 音（297 cue / 9.5 MB）：曲库页与 HUD 从不调用 `PlaySE`，属于移植时照抄原版初始化留下的死负载。

## 体积

| 阶段 | APK |
| --- | --- |
| 清理前 | 117.05 MB |
| 删菜单音频 | 96.78 MB |
| 删 ACB + 换 wav | 97.12 MB |
| 删死美术 / 换背景 | 81.16 MB |
| 现在的候选包 | **79.65 MB** |

## 守住的东西

- `Tools/verify-resource-paths.ps1`：18 处 `Resources.Load("...")` 全部能解析，缺一个就报错。这条是因为删 `ScreenLayerLiveLoading` 曾把打歌入口打断才加的。
- `Tools/audit-dead-assets.ps1`：识别「按名字加载」的存活路径（`Resources.Load` 字符串、`MenuScreenType` 枚举、`EditorBuildSettings` 场景、asmdef/link.xml/AndroidManifest 这类机制文件）。仅凭「零 GUID 引用」删文件在这个项目里是错的。
- `Tools/audit-audio-provenance.ps1`：42 个 wav 必须都能在 `docs/reference/audio-credits.md` 找到署名行。

## 未做

- 两套音符皮肤只差波形与音高，判定音在真机上是否「听起来不一样」还没做 AB 主观评价。
- 曲库虚拟列表在 100+ 曲包时的滚动性能未实测。
- 判定延迟补偿（把打击音提前 N 毫秒）没做：音效是因果的，没点到就不该响，提前播放会破坏这条语义。
