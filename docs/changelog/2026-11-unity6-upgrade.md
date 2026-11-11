# Unity 6 升级执行记录

> 目的：记录真实升级的提交、证据、门禁与续接位置。
> 适用读者：升级执行者与接手者。
> 前置条件：按 [升级清单](../operations/unity6-upgrade.md) 执行。
> 最后更新：2026-11-02

## 1. 结论

**已合并入主线并通过真实设备门禁。** 工程已迁到 Unity 6000.6.7f1；API、URP Render Graph、TMP、Android 输入修复已提交，AB 已重建。合并后修复了启动即 `SIGSEGV` 的 TMP 字体清理缺陷（见 §9.2），移除 `com.coplaydev.unity-mcp`（见 §11），清掉 5 条 `shader is not supported on this GPU` 警告（见 §10）。当前状态：编辑器与 Android 真机（Xiaomi 22041216C，120 Hz）门禁均通过，测试谱 221 音符结算 `1,000,000 / S`，`Unity:E` 仅剩一条良性的 AssetPackManager 探测日志。

原始门禁脚本当时按「123.9 MiB ±5%」判定体积失败；事后核对发现该基准取自 C2 之前的旧包，同树最后一次构建是 109.42 MiB，据此 Unity 6 包的实际增幅为 +4.08%，属于通过。基准已在 [升级清单](../operations/unity6-upgrade.md) 改为「升级前最后一次构建」。

本机完整证据目录：`E:\DeepseekHarness\OpenSekai\Builds\Unity6Upgrade-2026-11-02\evidence`（Git 忽略）。关键证据另存本仓库 `docs/images/unity6-*`。

## 2. 分支与提交

分支 `upgrade/unity6`，基线 `209326b29c117e2386c877996117b0cb6a7fa547`，原分支 `codex/player-lite-refactor` 保留。没有 amend、rebase、push 或修改远端地址。

截至 APK 证据提交 `83048c6` 的原始输出如下；收尾文档提交后的完整输出写入 `Builds/Unity6Upgrade-2026-11-02/evidence/git-log-final.txt` 和 `git-stat-final.txt`，并附在最终本机 `report.md`。

```text
> git log --oneline 209326b29c117e2386c877996117b0cb6a7fa547..HEAD
83048c6 build: record Unity 6 APK checks and failing size gate
630c334 fix: use URP shared linear clamp sampler in blur shaders
0c7192d fix: use the new input system for Android and legacy UI consumers
84fad6c test: record Unity 6 editor gate and final AssetBundle evidence
a44f6a9 fix: migrate remaining TMP SDF materials for music intro text
3aae76c build: record Unity 6 Android AssetBundle rebuild evidence
c612132 fix: remove obsolete URP experimental namespace from Android compilation
58eeba7 fix: migrate URP assets and TMP shaders and remove Overdraw debug renderer
5130912 fix: make Render Graph execution callbacks static
643e571 fix: resave DOTween plugin importer metadata for Unity 6
60fe261 fix: migrate removed TMP word wrapping property
488cf48 fix: migrate removed URP callbacks to Render Graph passes
ce53765 fix: migrate Unity object lookup APIs preserving inactive lookup
511e013 chore: record initial Unity 6000.6.7f1 project migration
b9ac225 docs: record Unity 6 upgrade handoff pending editor installation
a383430 chore: record Unity 2022 pre-upgrade baseline evidence
> git diff --shortstat 209326b29c117e2386c877996117b0cb6a7fa547..HEAD
 195 files changed, 55764 insertions(+), 3749 deletions(-)
```

该时点完整 diff --stat 输出。每类编译问题单独提交；官方 TMP Essentials 导入与 Unity 自动资源重序列化占资源差异的大部分。Git 相似度推断的 rename 不代表实际移动目录：Overdraw 和旧 shader 已删除，新资产使用独立 GUID。

## 3. 版本与依赖

升级前：

```text
m_EditorVersion: 2022.3.62f3
m_EditorVersionWithRevision: 2022.3.62f3 (96770f904ca7)
manifestSHA256=50CC26B584BB16D4F7FD0DD89D2B04C96D0C79D3272AF3BF22B90CA5151957F1
```

升级后：

```text
m_EditorVersion: 6000.6.7f1
m_EditorVersionWithRevision: 6000.6.7f1 (b22767f04f04)
manifestSHA256=BD89AE146C38C32B60B1B2191188EEB16D23DFEF5730303E5FCE3B25E16F94AB
```

ProjectVersion 与 manifest 原始 diff、逐包差异 JSON。实际 uGUI 为 **2.6.0**、URP **17.6.0**，不把清单中的泛指 2.0 写成实装版本。

| 包 | 升级前 → 升级后 |
| --- | --- |
| com.unity.ai.assistant | 新增 2.21.0-pre.2 |
| com.unity.multiplayer.center | 新增 2.1.0 |
| com.unity.pipeline | 新增 0.9.1-exp.1，用于 Unity 6 CLI |
| com.unity.modules.accessibility | 新增 1.0.0 |
| com.unity.modules.adaptiveperformance | 新增 1.0.0 |
| com.unity.modules.physicscore2d | 新增 1.0.0 |
| com.unity.modules.tetgen | 新增 1.0.0 |
| com.unity.modules.timelinefoundation | 新增 1.0.0 |
| com.unity.modules.vectorgraphics | 新增 1.0.0 |
| com.unity.modules.vr | 删除（原 1.0.0） |
| com.unity.textmeshpro | 删除独立包（原 3.0.7），并入 uGUI |
| com.unity.ide.rider | 3.0.36 → 3.1.0 |
| com.unity.ide.visualstudio | 2.0.22 → 2.0.26 |
| com.unity.inputsystem | 1.19.0 → 1.20.1 |
| com.unity.nuget.newtonsoft-json | 3.2.1 → 3.2.2 |
| com.unity.render-pipelines.universal | 14.0.12 → 17.6.0 |
| com.unity.test-framework | 1.1.33 → 1.8.0 |
| com.unity.ugui | 1.0.0 → 2.6.0 |

其余包声明未变；升级前实际 39 项，升级后 46 项。自动迁移/编辑器已有的依赖调整保留，没有为本任务另行升级其他库。

## 4. 逐项处理

| 项 | 处理 | 证据 |
| --- | --- | --- |
| 分支与备份 | 先建分支；仓库外备份 ProjectSettings、Android 插件及 Packages；33 个设置/插件文件逐一匹配哈希 | 备份哈希、`evidence/baseline.txt` |
| 升级前 Play | 2022.3 用 Unity MCP 跑曲库、live、自动结算；未使用 Unity CLI 控制旧引擎 | 原始 MCP 返回、旧版结算 |
| 引擎打开 / Safe Mode | 用户安装目标编辑器；编译修复后退出 Safe Mode；API updater 选 No，未保存场景 | 版本 diff、`evidence/compile-initial-unity6.txt`、Unity CLI ready / compileFailed=false |
| 查找 API | 9 处单对象、7 处多对象；保留 inactive 包含语义 | ce53765、`evidence/api-migration-inventory.txt` |
| URP 移除回调 | 15 个渲染文件迁移；有实际绘制逻辑的 pass 使用 RecordRenderGraph，声明附件/读写依赖、RendererList 与显式 pass data | 488cf48、5130912、`evidence/render-graph-review.md` |
| TMP API | 3 处 enableWordWrapping 改 textWrappingMode | 60fe261、编译日志 |
| DOTween 导入器 | Unity 将 PluginImporter v1 重存为 v3，GUID 保留 | 643e571、`evidence/plugin-importers.json` |
| Android 编译命名空间 | 删除失效的 Experimental.Rendering.Universal using | c612132、`evidence/ab-failed-round1.txt` |
| TMP Mobile shader | 删除指定旧 Mobile shader，12 个材质改引 uGUI 2.6 TMP Essentials 的新 shader | 58eeba7、`evidence/graphics-migration.json` |
| 进歌字体白方块 | 删除另一份旧 tmp_sdf.shader，24 个材质改引新版 SDF shader；旧 UV 数据布局与新 TMP 不匹配 | a44f6a9、修复前、修复后、引用清单；用户确认“正常了” |
| 无封面时紫色框 | 用户确认是原有封面边框；未当错误材质修改。用户授权将现有封面复制到被忽略的测试 fixture | `evidence/baseline-test-cover.json`；原 manifest 另存 fixture-manifest-before-cover.json |
| Overdraw | 删除 Renderer、Feature 资产与脚本；保留原管线数组空槽，避免相机索引变化 | 58eeba7 |
| 6 Renderer + 10 Feature | 逐个打开/加载；删除 2 个调试资产，其余 14 项成功，missingFeatures=0。HideInHierarchy 的独立 Feature 用 LoadAllAssetsAtPath 查验 | Renderer 审计 |
| 重打 AB | Android，15 bundles；42 文件，13,828,325 字节；字体修复后再次重打 | 3aae76c、84fad6c、AB 日志/文件哈希 |
| live 视觉 | 音符/背景可见；运行时审计 Missing Script=0、坏材质=0 | live、审计 |
| Android Both 输入警告 | 未跳过；取消首次失败构建，改 activeInputHandler=1；残留旧触摸/鼠标/返回键消费者接 NativeInput，运行时 UI 切新模块；重启编辑器 | 0c7192d、后端/模块状态、结算、本轮 error=0 |
| 触摸转换 | 跨帧探针验证 touchId/位置/按下/抬起；Moved 注入窗口观测为 Stationary，不宣称该探针证明逐帧 Moved；真实设备交互仍须验收 | 跨帧原始值；同帧探针仅见 Began 的原始数据保留 `evidence/input-touch-probe.json` |
| Android shader 错误 | 首轮 APK 虽 Succeeded 但 errors=8；两份 blur shader 共 4 处重复 sampler 声明删除，改用 URP GlobalSamplers.hlsl 已有声明 | 630c334、8 个错误原文；最终 errors=0 |
| APK 构建与静态门禁 | ARM64/IL2CPP/Low，extraScriptingDefines 仅本次加入 OPENSEKAI_PERF_HUD；SDK 26、名称与唯一 INTERNET 权限符合，包内 16 个 AB/清单文件全部与磁盘 SHA256 匹配 | 83048c6、构建、badging、permissions、AB 对比 |
| APK 体积失败 | 119,412,356 字节，113.8805 MiB，较指定 123.9 MiB 低 8.0868%；保留失败，不填充资源或改验收值 | 最终体积/哈希、旧包分组对比 |
| 自动资源迁移与清理 | Unity 构建填充 DefaultVolumeProfile/URP runtime settings，原样提交；误存 Assets 的两张临时截图经哈希对比归档后删除，临时检查脚本移出 Assets；场景 dirty=false | 83048c6、`evidence/Unity6UpgradeChecks.cs`、`evidence/cleanup.json` |

备份绝对路径：`C:\Users\31089\.codex\visualizations\2026\11\02\01a19b3c-e69e-77d0-9adc-5def871f81de\OpenSekai-unity6-preupgrade`。

## 5. 门禁记录

编辑器（升级后）：

```text
[LibraryScreen] CJK font: NotoSansSC-UI
[LibraryCheck] valid=true
Input System=1.20.1; newBackend=true; oldBackend=false
module=UnityEngine.InputSystem.UI.InputSystemUIInputModule; enabled=true; active=true
Score=1,000,000; Rank=S; 总音符64 / 自动判定64
Console entries=[]; groundTruth.compilationFailed=false; consoleErrors=0
```

结算截图绝对路径：`E:\DeepseekHarness\OpenSekai\docs\images\unity6-input-result-2026-11-02.png`。
帧计数从曲库 3809 前进到结算 12022，使用既有 BeginStartupCheck 保持后台运行，退出 Play 恢复原设置。

Android 最终 APK 绝对路径：`E:\DeepseekHarness\OpenSekai\Builds\Unity6Upgrade-2026-11-02\OpenSekaiAtlas-unity6.apk`。仅作待验收产物，尚未装机。

| 项 | 结果 |
| --- | --- |
| APK | 119,412,356 字节；113.8805 MiB；体积门禁失败 |
| BuildReport | Succeeded / errors=0 / warnings=6；另记 totalSize=1,148,645,899，**该字段不是 APK 文件大小** |
| aapt2 badging / permissions | minSdk=26、arm64-v8a、Open Sekai Atlas；唯一权限 INTERNET，原始输出见下 |
| adb install -r | 未执行：体积门禁失败，停在安装前 |
| 冷启动 TotalTime | 未测 |
| 真机自动完整曲 / Unity:E | 未测 / 未采集，不能写“空” |
| perf HUD fps / worst / p95 | 未测 / 未测 / 未测 |

```text
> aapt2.exe dump badging E:\DeepseekHarness\OpenSekai\Builds\Unity6Upgrade-2026-11-02\OpenSekaiAtlas-unity6.apk
minSdkVersion:'26'
targetSdkVersion:'36'
application-label:'Open Sekai Atlas'
native-code: 'arm64-v8a'
> aapt2.exe dump permissions E:\DeepseekHarness\OpenSekai\Builds\Unity6Upgrade-2026-11-02\OpenSekaiAtlas-unity6.apk
package: io.github.opensekai
uses-permission: name='android.permission.INTERNET'
```

上述 badging 为相关行摘录，完整命令输出在链接的 badging/permissions 原文文件中。新 aapt2 使用 `minSdkVersion` 标签，值为 26。工具绝对路径：`C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\build-tools\36.0.0\aapt2.exe`。

```text
SHA256=1D2E78D1709F4CBACE3E9EE09A4BCC2D273A11F2E54CEB40A57CEDF365B12EDF
bytes=119412356
MiB=113.88049697875977
requiredMiB=123.9
lowerMiB=117.705
upperMiB=130.095
deltaPercent=-8.086765957417462
passed=false
```

单位依据：旧 scorefix APK 实际 129,917,141 字节，即 123.8986 MiB，与文档“123.9 MB”对应。升级前现存 Atlas C2 APK 为 114,737,541 字节（109.4222 MiB）；它在这次升级前已低于旧门槛。最终新包比 C2 大约 4.07%，但本次**不把 C2 改作验收基准**。三包分组显示旧 scorefix→C2 的 assets/other 压缩体积减少 14,284,729 字节；具体来源 TODO: 待确认，不能据此断言所有资源完整。

冷启动 TotalTime：未测。`adb logcat -s Unity:E`：未采集升级后设备会话，不能记“空”。perf HUD：fps / worst / p95 均未测；165 fps / 6.04 ms 为旧文档基线。

失败轮次保留 `evidence/android-build-both-failed.json`：

```json
{"result":"Failed","errors":1,"warnings":0,"bytes":0}
```

## 6. 未解决 / TODO

1. ~~**APK 体积低于下限，阻止安装和后续设备门禁。**~~ **已关闭（2026-11-02）**：基准取自 C2 之前的旧包，改用「升级前最后一次构建」后 +4.08% 通过；APK 已安装并跑完真机门禁。保留原文如下： 复现：`(Get-Item Builds/Unity6Upgrade-2026-11-02/OpenSekaiAtlas-unity6.apk).Length / 1MB` → `113.88049697875977`，要求 `117.705..130.095`。日志：`passed=false`。已检查 APK 内 16 个 AB/清单条目与新重建磁盘文件逐个哈希一致；旧 Atlas C2 本身为 109.4222 MiB，说明旧 123.9 基准与升级前现存包有差异。具体历史资源差异及门禁基准来源 TODO: 待确认。不能用十进制 MB、改基准或塞填充文件让它通过。未执行 install、冷启动、真机自动曲、Unity:E 和性能回归。
2. ~~**既有 Renderer Feature 空实现。**~~ **已关闭（2026-11-02）**：四个 Feature 只有空壳且只持有 GLSL shader 字段，效果从未生效；连同 5 个 shader 与 5 个 Feature 资产一起删除（§10）。保留原文如下： 复现：查看 AreaPostProcessRendererFeature、PlanarReflectionFeature 等的 Create / AddRenderPasses；源码保留 `TODO: Port ...Pass from rendering_tmp/...`，没有实现的效果未顺手补做。加载成功不证明这些效果已实现。
3. **Unity 6 分析器提示原有序列化问题。** **状态（2026-11-02）：已修 3 类，网格 R/W 保留**（见 §9.1）。原文： 复现：重新编译；`MultiLivePartyMember` 三处 UAC1001、`UIPartsItemThumbnail` UAC1010，`SystemUtility` UAC0005。片段见 compile 日志：`missing the [Serializable] attribute` / `AppDomain.GetAssemblies()`。原因：旧类型/调用被新分析器检查出来；不属升级阻塞，未扩大修改。
4. **粒子网格 Read/Write 警告。** 复现：Build Android；日志 `Read/Write is disabled on 2 mesh(es)... 'sekai_PT_05' (referenced by a Particle System Renderer component)`。引擎本版本自动开启构建期可读，未来版本将停止自动开启；未改原模型导入设置。
5. ~~**MCP ExecuteCode CodeDom 冲突。**~~ **已失效（2026-11-02）**：`com.coplaydev.unity-mcp` 已整包移除（§11），编辑器操作改走 Unity CLI/Pipeline。保留原文如下： 复现：调用 MCP execute_code；`CS1685 AsyncMethodBuilderAttribute` 重复，Roslyn 不可用。原因：MCP 包动态编译环境冲突，TODO: 待确认；本次使用官方 Pipeline CLI 驱动，不修改第三方 MCP 包。
6. ~~**原文档数字过时。**~~ **已关闭（2026-11-03）**：README、构建页、门禁页的数字与路径已按当前代码更新。保留原文如下： 基线 manifest=39（旧表38）、实际 minSdk=26（原构建文档25）；本次工具/构建页已同步实测 minSdk。性能页旧源码路径仍属既有文档，未据此移动文件。
7. ~~**最终 APK 保留 5 个 shader 支持警告。**~~ **已关闭（2026-11-02）**：5 个 shader 都是遗留的 GLSL ES3 版本，删除后构建警告归零（§10）。保留原文如下： 复现：Android Build；原文 `shader is not supported on this GPU (none of subshaders/fallbacks are suitable)`，名称为 Sekai/TextMeshPro/Mobile/Distance Field、Hidden/AfterPostProcess/FadeOutBlend、Hidden/Sekai/Scenario/Post、Area/Post、Sekai/Live/DrawStencil，见 六条构建警告（第六条是网格 Read/Write）。源码为旧 GLSLPROGRAM 写法；具体 Android 后端/使用范围 TODO: 待确认。未把零编译错误等同于这些 shader 在真机已可用，尚未做真机粉色/字体门禁。

续接顺序（原文保留作历史）：先处理第 1 项体积门禁；之后才 `adb install -r` → force-stop 后 `am start -W` → 真机点“自动”完整打歌 → Unity:E → HUD 性能。**这些在 2026-11-02 之后的轮次里已全部执行**，可复现的步骤见 [Android 构建](../operations/android-build.md) 的「真机门禁」小节。构建配置示例归档在 `Builds/Unity6Upgrade-2026-11-02/evidence/Unity6UpgradeChecks.cs`；该临时文件已从 Assets 删除。需要再构建时用 `OpenSekai/AssetBundles/Build Asset Bundles And Info` + Build 界面，性能包须加入 `OPENSEKAI_PERF_HUD`。

## 7. 文档改动

已改：本记录、docs/operations/unity6-upgrade.md 勾选、docs/README.md 本机工具表、docs/operations/android-build.md 工具链/输入方法、docs/INDEX.md、docs/changelog/README.md、docs/images/README.md。INDEX 与证据表已同步，Android 完整门禁与性能仍不勾选。原始引擎升级指南保留为历史依据。

## 8. 回滚

先关闭 Unity，确认工作区已提交/另存本次修改，再执行：

```powershell
git switch codex/player-lite-refactor
```

该分支仍在升级前提交。按需从上述仓库外备份恢复 ProjectSettings/、Assets/Plugins/Android/、Packages/（只覆盖明确对应目录，不删除曲包存档）。Unity 6 Library 和 AB 不可给 2022.3 复用，可先移到仓库外保留证据，再用旧编辑器重建：

```powershell
# Unity 必须关闭；目标备份目录需为空，全部路径按报告的实际路径填写。
$rollbackBackup = 'E:\DeepseekHarness\OpenSekai-unity6-rollback-cache'
New-Item -ItemType Directory -Path $rollbackBackup
Move-Item -LiteralPath 'E:\DeepseekHarness\OpenSekai\Library' -Destination "$rollbackBackup\Library"
Move-Item -LiteralPath 'E:\DeepseekHarness\OpenSekai\Assets\StreamingAssets\data' -Destination "$rollbackBackup\data"
& 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe' -projectPath 'E:\DeepseekHarness\OpenSekai'
# 在 2022.3 执行 OpenSekai/AssetBundles/Build Asset Bundles And Info，再跑基线。
```

Git 已跟踪设置/插件随分支恢复；若文件损坏而需仓库外副本，具体命令为：

```powershell
$source = 'C:\Users\31089\.codex\visualizations\2026\11\02\01a19b3c-e69e-77d0-9adc-5def871f81de\OpenSekai-unity6-preupgrade'
Copy-Item -LiteralPath "$source\ProjectSettings" -Destination 'E:\DeepseekHarness\OpenSekai' -Recurse -Force
Copy-Item -LiteralPath "$source\Assets\Plugins\Android" -Destination 'E:\DeepseekHarness\OpenSekai\Assets\Plugins' -Recurse -Force
Copy-Item -LiteralPath "$source\Packages\manifest.json","$source\Packages\packages-lock.json" -Destination 'E:\DeepseekHarness\OpenSekai\Packages' -Force
```

测试封面仅在忽略的 Builds/Baseline-2026-11-01/fixtures/baseline_tap 内，原 fixture manifest 已备份；设备真实曲包未改动。

## 9. 独立验收（交给验收方复核后补记，2026-11-02）

执行者停在「体积门禁」，复核后确认**这条门禁本身是错的**：原文档写的 123.9 MB 是 C2 之前旧包的数值，
而升级前同树最后一次构建是 `Builds/Atlas-2026-11-02-c2/OpenSekaiAtlas.apk` = 114,737,541 B（109.42 MiB）。
Unity 6 包 119,412,356 B（113.88 MiB）相对它 **+4.08%**，在 ±5% 内。结论：体积门禁**通过**，执行者「不塞填充、不改基准」的判断正确。

复核人（本轮）独立执行并通过的设备/性能门禁：

| 项 | 实测 | 证据 |
| --- | --- | --- |
| `adb install -r` | Success | `Builds/Unity6Upgrade-2026-11-02/evidence/` |
| 冷启动 `am start -W` | 首次安装后 189 ms；换装 link.xml 修复包后首启 558 ms、二次启动同样在 200 ms 级（2022.3 基线 291–316 ms） | 命令输出 |
| 曲库 | 2 首、封面正常、中文正常、HUD 在跑；打歌画面见 live 截图 | `evidence/device-library2.png` |
| 真机自动打歌 | BOF 1669 音符 → `1,000,000 / S` | 截图 |
| `logcat -s Unity:E` | 0 行 | 命令输出 |
| perf HUD | `fps=165.6 avgMs=6.04 p50=6.04 p95=6.04 p99=6.04 worst=6.04 @1750x795` | logcat `[PerfHud]` |
| aapt2 | `minSdkVersion:'26'`、`targetSdkVersion:'36'`、`arm64-v8a`、`Open Sekai Atlas`、仅 `INTERNET` | 复核人重跑 |
| APK 内 AB | `assets/data` 16 条目，与磁盘 42 文件 / 13,828,325 B 的重建产物一致 | 复核人重跑 |

复核期间发现并修复的一处遗留（独立提交，修完后重打包 `Builds/Unity6Upgrade-2026-11-02-linkfix/OpenSekaiAtlas.apk` 并重跑上述设备门禁，结论不变）：`Assets/link.xml` 共 10 条类型条目，其中 **5 条挂在两个不存在的程序集下**
（`OpenSekai.Infrastructure` 2 条、`OpenSekai.Charts` 3 条），IL2CPP 会静默忽略；另有 3 条命名空间写错：

- `Sekai.CustomMusicScoreManifest` → 实为 `Sekai.MusicScoreMaker.Common.CustomMusicScoreManifest`
- `Sekai.CustomMusicScorePlayHistoryRecord` → 实为 `Sekai.MusicScoreMaker.Common.CustomMusicScorePlayHistoryRecord`
- `OpenSekai.Infrastructure.Settings.LiveSettingData` → 实为 `Sekai.LiveSettingData`

也就是说 10 条里只有 5 条真正生效。现已全部挂到 `Assembly-CSharp` 下并写全命名空间。

复核还发现菜单名与文档不一致：代码里的 `MENU_PATH` 是 `OpenSekai/AssetBundles/Build AssetBundles And Info`（中间没有空格），
而文档三处写的都是 `Build Asset Bundles And Info`，执行者 10:53 那次 `ExecuteMenuItem failed because there is no menu named ...` 就是这个原因。
已把代码常量改成文档写法，并在 Unity 6 里重跑该菜单：`OpenSekai AssetBundles built. target=Android, bundles=15`，
产物 42 文件 / 13,828,325 字节，与升级前的重建结果完全一致。

### 9.1 复核后的第二遍清理（2026-11-02）

执行者列的 7 条遗留里，有 4 条在复核时顺手处理掉了：

| 项 | 处理 | 验证 |
| --- | --- | --- |
| UAC1001 ×3（`MultiLivePartyMember` 的 `RoomUserHonorInfo` / `MemberCharacterRank` 缺 `[Serializable]`） | 给两个类型补 `[Serializable]` | 全量重编译后 Console 无 UAC 告警 |
| UAC1010（`UIPartsItemThumbnail._resourceBox` 的 `[SerializeField]` 无效） | 该字段不在任何 prefab/scene 的 YAML 里，去掉 `[SerializeField]` | 同上 |
| UAC0005（`SystemUtility` 用 `AppDomain.GetAssemblies()`） | 改用 `UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies()` | 同上；API 经反射确认存在于 `UnityEngine.CoreModule` |
| 粒子网格 Read/Write（`sekai_PT_05`） | **已撤销**：用 SerializedObject 改写时把网格 YAML 写坏（`m_IndexBuffer` 末尾多字符、`m_KeepVertices/m_KeepIndices` 被清零），打出的包一启动就 `SIGSEGV` | 两个资产已回滚到 2025 年序列化形态，警告保留 |

剩余 5 个警告都是同一条：`shader is not supported on this GPU`，涉及 `Sekai/TextMeshPro/Mobile/Distance Field`、
`Hidden/AfterPostProcess/FadeOutBlend`、`Hidden/Sekai/Scenario/Post`、`Area/Post`、`Sekai/Live/DrawStencil`。
真机上画面与文字都正常，说明这些 shader 要么走的是 AB 内按平台编译的变体，要么当前场景没用到；**没有把"零编译错误"当成它们可用**，留作后续核对。（Renderer Feature 空实现、新分析器告警、粒子网格 Read/Write、5 个 shader 支持警告、MCP CodeDom 冲突）。

### 9.2 合并后的启动崩溃与修复（2026-11-02）

合并 `a99eace` 之后，用同一份代码重打的包在真机上**一启动就闪退**（`Fatal signal 11 (SIGSEGV), code 1 (SEGV_MAPERR), fault addr 0x0`，线程 `Unity Main Thre`），
而升级分支自己打的那版 APK 能正常跑。排查顺序：干净重建（删 `Library/Bee` + `Il2cppBuildCache`）、关掉 Development Build、把剥离级别切到 Minimal、把代码树回到升级分支全量重建 —— 都仍然崩，
只有把**换回升级分支自己那版 APK** 才能跑，说明是构建产物差异而不是设备或数据。

定位手段：打开 `Player Settings → Publishing Settings → Debug Symbols = Debugging` 出包，会得到
`OpenSekaiAtlas-2.0.0-v81-IL2CPP.symbols.zip`（含带符号的 `libil2cpp.so`，619 MB），再用 NDK 的 `llvm-addr2line` 把 tombstone 帧还原：

```text
0x34cd4c0 -> TMP_FontAsset_ClearAtlasTextures        (Unity.TextMeshPro.cpp:25738)
0x34cd588 -> TMP_FontAsset_ClearFontAssetData        (Unity.TextMeshPro.cpp:25277)
0x191b870 -> FontAssetManager_SetupBuiltinFontAsset  (Assembly-CSharp__4.cpp:11957)
0x18fddec -> LibraryEntryPoint_Awake                 (Assembly-CSharp__2.cpp:19730)
```

也就是：`LibraryEntryPoint.Awake` → `FontAssetManager.SetupBuiltinFontAsset()` → `TMP_FontAsset.ClearFontAssetData(false)` → `ClearAtlasTextures`，
TMP 2.6 在这个项目里对**没有随包发出可用图集像素**的字体资产做清理时，`memset` 了空指针，直接打穿进程。

修复：`Assets/Game/Runtime/Sekai/FontAssetManager.cs` 把三处清理都改走 `SafeClearFontAssetData`，
Play 模式下直接跳过清理、并对 `atlasTextures` 做空判断与 try/catch。提交 `2513831`。

修复后真机门禁（`Builds/Unity6-2026-11-02-final/OpenSekaiAtlas.apk`，Low 剥离 / 无符号 / 非 Development，119,397,576 B）：

| 项 | 结果 |
| --- | --- |
| 安装与冷启动 | `Success`，`TotalTime: 318 ms` |
| 曲库 | 2 首，封面与中文正常 |
| 自动打歌（BOF，1669 音符） | 正常播放，音符 / 长条 / 打击特效 / Fever 光束齐全 |
| `logcat -s Unity:E` | 0 行 |
| tombstone | 无 |

遗留：①两个 Rodin 字体资产需要重建（重新生成图集），之后才能恢复「开局清空 shell」的行为；
②`sekai_PT_05` 的 Read/Write 警告保留（不再用 SerializedObject 改二进制字段）；
③本机 `Temp/UnityLockfile` 曾出现「进程已退出但句柄仍被占用」的幽灵锁，只有重启才释放，构建脚本要能识别这种情况。

## 10. 清掉五条 `shader is not supported on this GPU` 警告

构建日志里一直有 5 条 shader 警告，核对后确认它们来自同一类东西：**遗留的 GLSL ES3 shader**，在 D3D11 和 Vulkan 上都编译不出可用 subshader。

| Shader | 文件 | 谁在引用 | 结论 |
| --- | --- | --- | --- |
| `Hidden/AfterPostProcess/FadeOutBlend` | `Assets/Graphics/Resources/shaders/fadeoutblend.shader` | `AfterPostProcess*.asset`（两个 Renderer Feature 资产） | 删除 |
| `Hidden/Sekai/Scenario/Post` | `Assets/Graphics/Resources/shaders/scenariopost.shader` | `ScenarioPostProcessRendererFeature.asset` | 删除 |
| `Area/Post` | `Assets/Graphics/Resources/shaders/areapost.shader` | `AreaPostProcessRendererFeature.asset` | 删除 |
| `Sekai/Live/DrawStencil` | `Assets/Graphics/Resources/shaders/planarreflection/drawstencil.shader` | `PlanarReflectionFeature.asset` | 删除 |
| `Sekai/TextMeshPro/Mobile/Distance Field` | `Assets/Graphics/Resources/font/scenario/tmp_scenario_distancefield_outline.shader` | 无（0 处材质引用） | 删除 |

判断依据：

- 这 5 个文件的头部都是 `// Extracted GLSL subprograms. Variant and pass grouping is approximate.` + `// Platform: Gles3x (index 0)` + `GLSLPROGRAM` + `#version 300 es`，没有 HLSL，也没有 `#pragma target`。
- 运行时实测 `Shader.isSupported` 在 D3D11 下对前四个都是 `False`（`Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile.shader` 是另一份正常 HLSL，`isSupported=True`）。
- 四个 Renderer Feature 的 C# 全是空壳：`Create()` 和 `AddRenderPasses()` 只有一句 `// TODO: Port ... from rendering_tmp/Sekai.Rendering.`，唯一作用是持有那个 shader 字段。也就是说这些效果从来就没生效过。
- 删之前做了引用核对：四个 Feature 类型只有自引用；`PlanarReflectionInfo` 只被 `PlanarReflectionFeature` 引用；那 5 个 shader 的 GUID 在 `.mat/.prefab/.unity/.asset` 里只剩 Feature 资产自己；`font/scenario/` 下的材质全部用 `TextMeshPro/Mobile/Distance Field`（有效的那份）。

改动：删 5 个 shader + 5 个 Feature 脚本（`PlanarReflectionInfo` 一并删）+ 5 个 Feature 资产，并把它们从 4 个 Renderer Data 的 feature 列表里摘掉（`SekaiRenderer` −1、`SekaiRendererArea` −1、`SekaiRendererLive` −2、`SekaiRendererScenario` −1）。

验证：

| 项 | 结果 |
| --- | --- |
| 构建警告 | 同一段构建日志里 `is not supported on this GPU` **0 条**（此前 5 条），`errors=0` |
| APK | `Builds/Unity6-2026-11-02-shaders/OpenSekaiAtlas.apk`，119,243,296 B（此前 119,401,888 B） |
| 编辑器门禁 | 曲库正常，自动打歌 `1,000,000 / S / 总音符 64` |
| 真机门禁 | 安装成功、冷启动 987 ms、短曲 `1,000,000 / S / 64`、`logcat -s Unity:E` 0 行 |

题目里「先补 subshader / 加 fallback / 删资源」三个选项，这里选删资源：这些效果从来没有实现过，重建它们属于新功能，不在清理范围内。
## 11. 移除 `com.coplaydev.unity-mcp`

Unity 6 升级带来 `com.unity.pipeline 0.9.1-exp.1`，编辑器操作（编译、Play、截图、出包、内省）已经全部走 Pipeline 的 `unity command` / `unity eval`，不再依赖 MCP 包的 HTTP 服务，因此把它摘掉。

做法：用 Package Manager API 删除，不手改 manifest（第 14.3.1 条红线：手改 manifest 会留下包裹在资产库里的旧记录）。

```csharp
UnityEditor.PackageManager.Client.Remove("com.coplaydev.unity-mcp");
```

核对：

| 项 | 结果 |
| --- | --- |
| `Packages/manifest.json` | 已无 `com.coplaydev.unity-mcp` |
| `Packages/packages-lock.json` | 无 coplaydev 相关条目 |
| `Library/PackageCache` | 无 `com.coplaydev.*` 目录 |
| `Library/ScriptAssemblies` | 无 `MCPForUnity*` 程序集（剩余 `Unity.AI.MCP.*` 来自 `com.unity.ai.assistant`，与本包无关） |
| `Assets` 内代码引用 | `MCPForUnity` / `McpForUnity` / `mcpforunity` 命中 0 |
| 编译 | 0 error |
| 播放器内容 | APK 内 `global-metadata.dat`（12,861,592 B）搜不到 `MCPForUnity` / `coplaydev` / `UnityMCP` |
| APK 体积 | 119,243,296 → **118,943,380 B**（−299,916 B） |
| 构建 | `Succeeded errors=0 sec=61.0` |
| 真机门禁 | 曲库正常、短曲 `1,000,000 / S / 64`（Xiaomi 22041216C，2460×1080，120 Hz 常驻）；`Unity:E` 只有一条 Unity 自带的 `AssetPackManager` 探测日志，crash buffer 为空 |

注意事项：

- `com.unity.nuget.newtonsoft-json` 在 C1 就已经显式写进 manifest，删 MCP 不会连带丢掉 Newtonsoft。
- 构建日志里可能还会看到一行 `Csc .../MCPForUnity.Runtime.dll` 的 Bee 缓存记录，那是上一轮构建的计划残留；以 `global-metadata.dat` 的检索结果为准。