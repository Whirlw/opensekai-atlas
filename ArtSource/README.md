# 美术素材

成品平铺存放在本目录下的 `Notes`、`NotesV2`、`Effects`、`Backgrounds` 和 `HUD`，两套音符分别对应工程中的 custom01 和 custom02。不要在分类目录下建立子目录；导入只读取这一层的 PNG。

双击 `Import-All.bat` 即可从这些固定目录全量导入散图和图集。检查窗口中的未匹配和尺寸不符报告；导入后在 Unity 刷新资源并按需重建 AssetBundle。详见 `docs/operations/material-pack-import.md`。

在 Windows PowerShell 里运行 `Tools/NotesV2/draw_remaining.ps1 -DamageOnly`，用现有 Friction 的绘图函数生成紫色键身 `NotesV2/notes_damage.png`。两套皮肤的 Damage 共用 custom02 的 `textures/notes_damage.png`。

`SampleCovers` 是截图和宣传片用的虚构曲目封面，不导入工程。用 `node Tools/SampleCovers/render_covers.js` 重画（需要 Edge）。只画几何图形，不临摹已有的封面、标志或角色。

生成脚本、清单、源代码和 AEP 工程位于 `Tools`。运行生成器会覆盖相应成品，请先保存手工修改。轨道家族（`HUD/lane_base.png`、`lane_line.png`、`lane_mask.png`、`judgeLine.png`）由 `Tools/AstralHud.cs` 按代码里的车道几何绘制（消失点 y 5.46、判定 y -2.96、12 道 × 1.19，见 `LaneHalfWidth`），在 Windows PowerShell 里运行 `Tools/render_hud.ps1 -Only judgeLine.png,lane_base.png,lane_line.png,lane_mask.png`；`lane_mask.png` 的上下渐隐从现有图读回保留，只重画左右边；`Tools/judgeline.aep` 只作旧版参考。判定特效里的八边形框（`Tools/render_effects.ps1 -FramesOnly`）按同一格车道对齐。预览图保存在对应工具目录。
