# 素材来源清单

> 目的：登记仓库里每一类素材的来源与许可，发布任何二进制之前用它确认仓库不含无授权素材。代码是 MIT，字体是 OFL，其余贴图与音效必须是自制、CC0 或买断授权。每次增删素材都更新这里，数字用文末命令重新生成，不要凭记忆写。
>
> **现状（2026-11-09，0.3.0）**：代码 MIT、字体 OFL，音频 / 音符皮肤 / HUD / 判定特效 / 背景均为自制或 CC0。`Tools/audit-audio-provenance.ps1` 输出 `AUDIT OK`。

## 判定口径

- **来源只有三类**：`MIT`（本项目代码）、`OFL`（可随包分发的开源字体）、`自制` / `CC0`（音频与美术）。
- **状态**：`现行` / `保留` / `已删`。任何新增素材都必须先在这里登记来源与许可。
- 替换美术时统一用**同 GUID 重画**：只覆盖 PNG 字节，保留 `.meta`、`.asset`（切片矩形、九宫格边界、pivot）、GUID 与 prefab 引用。所以换图不需要改 prefab、不需要改代码，也不引入引用丢失。
- 图集里的精灵走 `Tools/apply-atlas-art.ps1`：按精灵 `m_Rect` 与 `m_RD.texture` 的 GUID 定位到真正的图集 PNG，再用 `CompositingMode.SourceCopy` 贴进去。**不要用编辑器里的 `OpenSekai/Paint Kit/2. Apply painted sprites`** —— 它会打印 `applied 157, failed 0` 却不动像素。

## 当前清单

下表是当前清单的摘要；最新数字以 `Tools/audit-asset-provenance.ps1` 的输出为准：

| 资源组 | 路径 | 文件 | 体积 | 来源 / 许可 | 状态 |
| --- | --- | --- | --- | --- | --- |
| 音符皮肤 custom01 | `Assets/Game/Resources/live/note/custom01` | 89 | 1.55 MB | 自制（画包 `new_notes`） | 现行 |
| 音符皮肤 custom02 | `Assets/Game/Resources/live/note/custom02` | 89 | 1.36 MB | 自制（画包 `new_notes_v2`） | 现行 |
| 判定 / 演出音 | `Assets/StreamingAssets/sound` | 42 | 2.99 MB | 自制 CC0-1.0 wav | 现行 |
| CRI 音频负载 | 全仓 | 0 | 0 | 已删除 | 已删 |
| live 背景 2dmode | `Assets/Game/AssetBundles/resources/startapp/live/2dmode` | 2 | 2.72 MB | 自制（画包 `new_backgrounds`） | 现行 |
| 加载背景 longLoading | `Assets/Graphics/Resources/loading` | 0 | 0 | 已删除（页面不存在） | 已删 |
| HUD / UI 大图 | `Assets/Graphics/Resources/screen` | 8 | 6.01 MB | 自制 | 现行 |
| 通用背景 commonbg | `Assets/Graphics/Resources/commonbg` | 0 | 0 | 已删除 | 已删 |
| live UI 预制 | `Assets/Graphics/Resources/live` | 10 | 3.82 MB | 自制（画包 `new_effects`） | 现行 |
| HUD sprite | `Assets/Graphics/Sprites` | 176 | 1.04 MB | 自制（画包 `new_hud` + 程序化替身） | 现行 |
| 图集精灵 | `Assets/Graphics/Textures/sactx-*.png` | 153 | 5.2 MB | 自制 / CC0 | 现行 |
| 字体 | `Assets/Game/Resources/Fonts` | 4 | 33.32 MB | OFL | 保留 |
| 代码 | `Assets/Game/Runtime` | 796 | 2.12 MB | MIT | 保留 |

字体 33 MB 与代码 2 MB 按许可保留；其余素材均为自制或 CC0。

## 维护规则

1. **新增美术**：先画进画包目录，再走 `Tools/apply-atlas-art.ps1` 或 `Tools/replace-note-skins.ps1`；源文件与许可记进本表。不要直接引用来源不明的贴图、prefab 或 shader。
2. **新增音频**：按 `StreamingAssets/sound/<bundle>/<cue>.wav` 落盘，并在 `docs/reference/audio-credits.md` 加一行署名（自制 / CC0 / 作者 / 日期 / 处理说明）。`Tools/audit-audio-provenance.ps1` 会检查 42 条署名是否齐全。
3. **新增第三方代码**：只允许放进 `Assets/Vendor/`，并在本表登记包名、版本与许可；不要改写成项目自有代码。
4. **发布前**：跑下面三条命令，全部通过才推 GitHub / 出 APK。

## 复查用的三条命令

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/audit-asset-provenance.ps1   # 分组体积与许可
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/audit-audio-provenance.ps1   # 音频应有 42 条署名
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/audit-atlas-coverage.ps1     # 图集还有多少精灵未被画包覆盖
```

前两条应分别给出分组表与 `AUDIT OK`；第三条的「未覆盖」列应为 0 或与预期一致，遇到非零先确认是脚本按 `m_Name` 匹配造成的假阳性（见 `material-pack-import.md` 的坑表），再看像素。

## 相关文档

- [发布流程](release.md)
- [素材包导入](material-pack-import.md)
- [音频外部音源包](audio-replacement.md)
- [发布前素材总检](pre-release-asset-audit.md)
- [项目索引](../INDEX.md)
