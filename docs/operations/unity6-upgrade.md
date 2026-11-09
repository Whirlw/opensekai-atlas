# Unity 6 升级清单

- 目的：把 2022.3.62f3 → 6000.6.7f1 的升级拆成可勾选的步骤，并列出本项目会踩的坑。
- 适用读者：要执行升级的人（本机需装 Unity 6 编辑器）。
- 前置条件：能跑通当前的 Android 构建；读过 `docs/Unity-2022.3.62f3-to-6000.6.7f1-Android-升级指南.md`。
- 最后更新：2026-11-02

## 概述

升级不是在编辑器里点一下「升级」就结束。这个工程的耦合面有三处容易被忽略：

1. **渲染管线**：全量 URP，且项目里曾有一整套 URP 14 的 shader 副本（已在 C2 清掉），剩下 3 个自建 shader 里有一个是 TMP SDF 变体；
2. **字体**：TMP 在 Unity 6 并入 uGUI 2.0，`Assets/TextMesh Pro` 与 TMP shader 的归属会变；
3. **Android 出包链路**：`Assets/Plugins/Android` 有自定义 Activity 与手工清单，构建脚本按路径找 `StreamingAssets/data`。

## 快速开始（升级当天的最短路径）

```text
1. git switch -c upgrade/unity6          # 不要直接在主线上升级
2. 备份 ProjectSettings/ 与 Assets/Plugins/Android/
3. 用 Unity 6 打开工程，允许迁移（先不要修改任何脚本）
4. 编译 → 修编译错误（见「常见问题」）
5. 跑 AB 重建：OpenSekai/AssetBundles/Build AssetBundles And Info
6. Play：曲库 → 自动打歌，结算应为 1,000,000 / S
7. 打 Android 包并装机复验
```

## 详细说明

### 本次执行进度（2026-11-02）

证据与续接见 [升级执行记录](../changelog/2026-11-unity6-upgrade.md)。下列勾选仅表示本次实际完成的步骤。

- [x] 创建 `upgrade/unity6`；记录基线 SHA、编辑器版本与 manifest SHA256；完成仓库外备份与逐文件哈希校验。
- [x] 2022.3.62f3 Play 基线：曲库中文正常、自动打歌结算 `1,000,000 / S`，留存 MCP 原始返回和截图。
- [x] 安装并以 6000.6.7f1 打开工程；退出 Safe Mode，未接受脚本自动更新，未保存场景。
- [x] API 与其他编译错误逐类修复并提交。
- [x] TMP shader、Overdraw、Renderer Data 与 Feature 升级检查（保留 5 个 Renderer Data、9 个独立 Feature，均可加载，无丢失 Feature）。
- [x] Unity 6 重建 Android AB（`target=Android, bundles=15`，42 文件，13,828,325 字节）。
- [x] Unity 6 编辑器门禁；改为仅新输入系统后再次结算 `1,000,000 / S`，本轮 Console error=0。
- [x] Android 完整门禁：APK 零错误构建，aapt2 的 minSdk 26 / arm64-v8a / Open Sekai Atlas / 仅 INTERNET 全部符合；装机冷启动 189 ms；真机自动打歌 `1,000,000 / S`（1669 音符），`logcat -s Unity:E` 0 行。**体积门禁按修正后的基准通过**（见下条）。
- [x] `OPENSEKAI_PERF_HUD` 真机性能回归：`fps=165.6 / p50 6.04 ms / p95 6.04 ms / worst 6.04 ms @1750x795`，与 2022.3 基线一致。

**体积基准修正（2026-11-02）**：本文原先写的「APK 体积 123.9 MB ±5%」是 C2 之前旧包的数值，已过时。升级前最后一次同树构建是
`Builds/Atlas-2026-11-02-c2/OpenSekaiAtlas.apk`＝114,737,541 B（109.42 MiB），Unity 6 包 119,412,356 B（113.88 MiB），**+4.08%**，在 ±5% 内。
验收基准一律取「升级前最后一次构建」，不再写死绝对体积。

升级前 manifest 实测 **39 项（11 个外部包 + 28 个内置模块）**，升级后 **46 项**（见 [升级执行记录](../changelog/2026-11-unity6-upgrade.md) 的逐包差异）。

### 一、升级前的项目现状（2026-11-02 实测）

| 项 | 当前值 | 升级后应为 |
| --- | --- | --- |
| 编辑器 | 2022.3.62f3 | 6000.6.7f1 |
| URP | `com.unity.render-pipelines.universal` 14.0.12 | 17.x（随编辑器） |
| TMP | `com.unity.textmeshpro` 3.0.7（独立包） | 并入 `com.unity.ugui` 2.0，独立包移除 |
| uGUI | 1.0.0 | 2.0.0 |
| Input System | 1.19.0 | 保持 1.x（升级前先确认兼容表） |
| Newtonsoft | `com.unity.nuget.newtonsoft-json` 3.2.1（显式声明） | 不变 |
| Android minSdk | 26 | 26（Unity 6 的下限就是 26） |
| 目标架构 | 仅 ARM64 | 仅 ARM64（Unity 6 不再支持 x86-64） |
| 脚本后端 | IL2CPP + Low 剥离 | 不变 |
| 包总数 | 39（11 真实包 + 28 内置模块） | 升级后 46（12 真实包 + 34 内置模块，含 Unity 6 新增的 accessibility / adaptiveperformance / physicscore2d / tetgen / timelinefoundation / vectorgraphics） |

### 二、升级前必须先做的准备

- [x] **依赖显式化**：Newtonsoft 已从「MCP 包的传递依赖」改成 manifest 里显式声明（`3.2.1`）。
- [x] **`link.xml`**：已存在，保住 `Newtonsoft.Json`、TMP、以及存档 DTO（`AssetBundleInfo`、`LiveSettingData`、`ChartDocument` 等）；Unity 6 换 IL2CPP 版本后要再跑一次真机验证。
- [x] **URP shader 副本**：项目内的 `Hidden/Universal Render Pipeline_*` 与 4 个 `Hidden/Universal/*` 同名 shader 已删除；剩下 3 个自建 shader 见下一节。
- [x] **TMP shader 归属**：删除旧 Mobile 与 `tmp_sdf.shader` 副本；12 + 24 个材质引用改为已安装 uGUI 2.6.0 的 TMP Essentials shader。后者修复进歌信息页白色方块。
- [x] **Renderer Data**：原 6 个 Renderer Data + 10 个独立 Feature 逐项检查；删除 Overdraw Renderer 与对应 Feature 后，保留 5 + 9 个，丢失 Feature=0。管线旧调试槽保留空位以保持相机索引。
- [ ] **Android 插件层**：`Assets/Plugins/Android/AndroidManifest.xml` 用 `@style/UnityThemeSelector`，`CustomUnityPlayerActivity` 继承 `UnityPlayerActivity`；Unity 6 对启动 Activity 与主题的默认值有变动，升级后在真机确认冷启动不闪退。

### 三、代码层的 API 迁移点（本项目实测数量）

| 用法 | 命中 | Unity 6 处理 |
| --- | --- | --- |
| `FindObjectOfType<T>()` | 9 | 换成 `FindFirstObjectByType<T>()`（或 `FindAnyObjectByType`） |
| `FindObjectsOfType<T>()` | 7 | 换成 `FindObjectsByType<T>(FindObjectsSortMode.None)` |
| `UNITY_64` / `DEVELOPMENT_BUILD` | 0 | 无需处理（6.6 起会对这两个符号发弃用诊断） |

其余注意：

- `Resources.Load` 路径（`screen/prefabs/*`、`screen/data/*`、`Fonts/NotoSansSC-UI`、`live/*`）在 Unity 6 不变，但**资源名冲突的检测更严**，升级后如果出现空 prefab，先看 Console 的重复资源警告。
- `AssetBundle` 清单是我们自己的 JSON（`AssetBundleInfo.bytes`），与引擎版本无关；但 **URP shader 的变体集合会变**，所以升级后必须重打 AB，不能复用旧的 `StreamingAssets/data`。
- `EditorBuildSettings` 里三个场景路径已是新结构：`Assets/Game/Scenes/{Library,TransitionBlank,Core}.unity`，升级脚本若按旧路径找会失败。

### 四、升级当天的执行顺序

1. **分支与备份**：`git switch -c upgrade/unity6`；复制 `ProjectSettings/`、`Assets/Plugins/Android/` 到工作区外。
2. **打开工程**：让 Unity 6 走完资源迁移，中途不要保存场景。
3. **修编译错误**：优先按上一节的 API 表替换，再处理 TMP 命名空间变化（`TMPro` 仍在，但部分 API 签名变了）。
4. **处理图形**：删 TMP 项目内 shader 副本、改指包内；删 Overdraw Feature；逐个打开 Renderer Data 确认不是紫色/丢失。
5. **重打 AB**：菜单 `OpenSekai/AssetBundles/Build AssetBundles And Info`，确认日志 `bundles=15`、产物 42 文件左右。
6. **编辑器门禁**：Play → 曲库 → 自动打歌，结算 `1,000,000 / S`，live 画面没有粉色材质。
7. **Android 包**：构建后用 `aapt2 dump badging/permissions` 确认 `sdkVersion:'26'`、`native-code:'arm64-v8a'`、权限只有 `INTERNET`。
8. **真机**：装机、冷启动、打一首、看 `logcat -s Unity:E` 是否为空。
9. **性能回归**：用 `OPENSEKAI_PERF_HUD` 构型打一首，对比升级前基线（打歌稳态 165 fps、`worst` 6.04 ms）。

### 五、验收与回滚

验收（全部满足才算升级成功）：

- 编辑器与真机都能自动打歌到 `1,000,000 / S`；
- 曲库封面、中文、设置面板、更多/排序都正常；
- APK 体积相对**升级前最后一次构建**的变化在 ±5% 内（当前基准：`Builds/Atlas-2026-11-02-c2/OpenSekaiAtlas.apk` 109.42 MiB；Unity 6 包 113.88 MiB，+4.08%）；
- 帧率不低于升级前基线（`worst` 不超过 8 ms）。

回滚：升级分支不合并即可；`StreamingAssets/data` 是构建产物（已被 `.gitignore` 忽略），回滚后记得切回 2022.3 重新出包，不要复用 Unity 6 打的 AB。

## 示例

升级后在编辑器里用一条命令确认 AB 与场景都还活着：

```bash
# 菜单：OpenSekai/AssetBundles/Build AssetBundles And Info
# 期望日志：
#   OpenSekai AssetBundles built. target=Android, bundles=15
```

## 常见问题

**Q：升级后所有 UI 文字变方框？**
A：TMP 3.0 的字体资产（`FOT-RodinNTLGPro-*.otf` 生成的 SDF）在 uGUI 2.0 下要重新生成或确认 `FontAsset` 版本；本项目另有内置中文字体 `Resources/Fonts/NotoSansSC-UI.ttf`，先确认 `[LibraryScreen] CJK font:` 日志指向哪个。

**Q：场景打开后一片粉？**
A：shader 没跟着升级。先看是不是漏删了项目内的 URP 副本 shader，再看材质引用的 shader 名在 URP 17 里是否还存在。

**Q：AB 加载失败，音符不出来？**
A：90% 是复用了旧 `StreamingAssets/data`。删掉整个目录重打，并确认 `AssetBundleInfo.bytes` 是 JSON 头（`{"version":"2.0.0",...`）。

**Q：Android 一启动就退回桌面？**
A：先看 `logcat -s Unity:E`；常见是 `UnityThemeSelector` 在 Unity 6 被改名，或者自定义 Activity 的 `onActivityResult` 签名与新模板不一致。

## 相关文档

- [Android 构建](android-build.md)
- [Android 性能基线](android-performance.md)
- [构建宏与剥离](../reference/build-defines.md)
- [决策 0003：移除 UI 第三方包](../decisions/0003-drop-ui-packages.md)
- [重构日志 §15–§19](../changelog/2026-11-refactor-log.md)
