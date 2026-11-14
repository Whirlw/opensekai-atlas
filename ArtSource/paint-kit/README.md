# Paint kit 素材源

本目录保存 OpenSekai 的素材成品和主要生成代码，位于 Unity 的 Assets 外。`new_hud`、`new_effects`、`new_backgrounds`、`new_notes`、`new_notes_v2` 是导入源；运行时素材仍在 `Assets`。预览、对比图和旧版备份不纳入这里。

`new_hud/judgeLine.png` 是手绘成品，`tools/judgeline.aep` 是对应的 After Effects 工程。HUD 生成器明确跳过该 PNG，包括全量生成和单独指定时；修改时请使用 AEP 并手动导出。不要用旧目录中的生成器覆盖它。

## 导入与校验

在项目根目录执行（不加 `-Apply` 仅检查）：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/apply-paint-kit.ps1 -Apply
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/apply-atlas-art.ps1 -Apply
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/compare-atlas-art.ps1
```

随后在 Unity 中刷新资源并按需重建 AssetBundle 和构建。检查脚本报告中的未匹配、尺寸差异和像素差异；参见 `docs/operations/material-pack-import.md`。编辑器的旧 PaintKit apply 存在已记录的问题，不要用于导入。

## 生成素材

`tools/render_*.ps1` 使用 Windows PowerShell、System.Drawing 和部分 WPF 组件。字体直接使用 `Assets/Game/Resources/Fonts` 中的思源黑体。生成器直接读取素材清单、代码中的尺寸和轮廓坐标，以及 `tools/data` 中的结算文字切片表。旧的 `hud/`、`effects/` 和 `backgrounds/` 参考目录可以省略。背景、轨道、渐变和转场直接由代码绘制。HUD 中的功能遮罩和手绘判定线直接保存在 `new_hud/`。运行生成器会覆盖对应的其他成品，运行前请先保存修改并审查结果；局部生成可使用脚本支持的 `-Only` 参数。

素材由程序生成、手工绘制或基于参考形状制作，发布前仍应核实输入素材及 AEP 工程内引用的素材来源与许可。`new_notes` 两套成品暂未发现完整的生成代码，因此保留成品。不要以导入报告中的未匹配数量为依据直接删除素材：图集切片和散图采用不同的导入路径。
