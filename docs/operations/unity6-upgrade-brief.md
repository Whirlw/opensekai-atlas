# 交给执行者的提示词：Open Sekai Atlas 升级到 Unity 6

> **状态：已执行完成（历史文档，不再更新）。** 结论见 `docs/changelog/2026-11-unity6-upgrade.md`。

> 用途：把下面「提示词正文」整段复制给执行升级的 AI。它按这份提示词从零完成升级，最后按「交付格式」把结果交回。
> 最后更新：2026-11-02

## 提示词正文

````text
你要把一个 Unity 工程从 2022.3.62f3 升级到 6000.6.7f1，并在真机上验证。这是一次真实的升级，
不是演练：每一步都要有证据，不能靠「应该没问题」。

## 0. 工程与权限

- 工程根目录：E:\DeepseekHarness\OpenSekai（Windows）。
- 当前分支：codex/player-lite-refactor。**先新建分支** `upgrade/unity6`，不要在主线上升级。
- 允许 git commit，但**禁止** amend/rebase/push 到远端，禁止改远端地址。
- 禁止大规模重构、改代码风格、重命名文件；只做升级必需的改动。
- 遇到与本任务无关的既有问题（例如某些历史遗留的编译警告），记录到「未解决」里，不要顺手改。

## 1. 先读这些文档（顺序别换）

1. `docs/README.md` —— 文档约定与本机工具表。
2. `docs/operations/unity6-upgrade.md` —— 本次升级的清单，**这是你的主参照**。
3. `docs/Unity-2022.3.62f3-to-6000.6.7f1-Android-升级指南.md` —— 引擎侧变更的原始依据。
4. `docs/operations/android-build.md` —— 出包与三个已知坑。
5. `docs/operations/android-performance.md` —— 升级后要复现的性能基线。
6. `docs/operations/troubleshooting.md` —— 卡住时先查这里。

读完后，用 5 行以内复述「升级后的验收标准」，再开始动手。

## 2. 工程结构（不要移动）

- `Assets/Game/`：`Runtime/{Sekai,OpenSekai,Platform,CustomMusicScoreManager}`、`Editor/`、`Resources/`、`Scenes/`、`AssetBundles/`、`BuiltinAssets/`。
- `Assets/Graphics/`：`Shaders/`、`Materials/`、`Textures/`、`Sprites/`、`Rendering/`（URP 资产）。
- `Assets/Vendor/`：ZString、StandaloneFileBrowser、UniSafeAreaAdjuster。
- `Assets/Plugins/Android/`：`AndroidManifest.xml` + `CustomUnityPlayerActivity.java` + `ShareExportHelper.java`。
- 构建场景：`Assets/Game/Scenes/Library.unity` → `TransitionBlank.unity` → `Core.unity`。

## 3. 执行顺序（每步一次 commit）

1. **基线**
   - `git switch -c upgrade/unity6`
   - 记录 `git rev-parse HEAD`、`Unity` 版本、`Packages/manifest.json` 的 sha256。
   - 复制 `ProjectSettings/`、`Assets/Plugins/Android/` 到仓库外做备份，路径写进最终报告。
   - 跑一次 Play 门禁，确认升级前基线是好的（见第 4 节）。
2. **打开工程**：用 Unity 6000.6.7f1 打开，允许资源迁移；中途不要保存场景、不要接受脚本自动更新（选 No）。
3. **修编译**：按 `docs/operations/unity6-upgrade.md` 第三节的 API 表替换（`FindObjectOfType` 9 处、`FindObjectsOfType` 7 处）。其余报错逐个处理，每类错误单独 commit。
4. **图形面**：
   - 删 `Assets/Graphics/Shaders/TextMeshPro_Mobile_Distance Field.shader`，材质改指 uGUI 2.0 包内的 TMP shader。
   - 删 Overdraw 相关（`OverdrawRenderer.asset`、`MysekaiOverdrawRendererFeature`）。
   - 逐个打开 `Assets/Graphics/Rendering/` 下的 6 个 Renderer Data + 10 个 Feature，确认没有丢脚本/粉色。
5. **重打 AB**：菜单 `OpenSekai/AssetBundles/Build Asset Bundles And Info`，期望日志 `OpenSekai AssetBundles built. target=Android, bundles=15`，产物 `Assets/StreamingAssets/data` 约 42 个文件。
6. **编辑器门禁**（见第 4 节）。
7. **Android 包**：出包后按第 4 节的命令核验，再装机。
8. **性能回归**：用 `OPENSEKAI_PERF_HUD` 宏打一首，对比 `docs/operations/android-performance.md` 的表（打歌稳态 165 fps、`worst` 6.04 ms）。
9. **收尾**：更新 `docs/changelog/`（新增一篇升级记录）、`docs/operations/unity6-upgrade.md` 的勾选框、`docs/INDEX.md`。

## 4. 验收标准（不过就停下报告，不要绕过）

编辑器门禁：
- Play → 曲库页出现（中文不是方框，日志有 `[LibraryScreen] CJK font:`）。
- 菜单 `OpenSekai/Library/Play Local Baseline Automatically` → 结算面板显示 **1,000,000 / S**。
- live 画面没有粉色材质、没有空 prefab。

Android 门禁（`adb` 与 `aapt2` 路径自行按本机 Unity 版本替换）：
- `aapt2 dump badging`：`sdkVersion:'26'`、`native-code:'arm64-v8a'`、`application-label:'Open Sekai Atlas'`。
- `aapt2 dump permissions`：只有 `android.permission.INTERNET`。
- `adb install -r` 成功，`am start -W` 的 `TotalTime` < 1500 ms。
- 真机点「自动」打一首完整曲，score 应该是 `1,000,000`，`adb logcat -s Unity:E` 为空。
- APK 体积 123.9 MB ±5%。

任何一条不过：先修，修不动就**带着证据停下**，不要为了通过而放宽条件。

## 5. 已知坑（都会遇到，别重复踩）

- **Unity 复用 Gradle 缓存**：改了 minSdk 之类设置后出包，包还是旧值。删 `Library/Bee/Android/Prj/IL2CPP/Gradle` 再打。
- **`Assets/StreamingAssets/data` 是构建产物**（被 `.gitignore` 忽略）：升级后必须重打 AB，不能复用旧目录。
- **Auto Refresh 关闭**：新建 `.cs` 不会自动编译，需要触发一次资源导入；判断「编译是否真的发生」看 `Editor.log` 里的 `[ScriptCompilation] Requested script compilation`，`CompileScripts: 1.x ms` 是空转。
- **`Editor.log` 里的 `error CS` 可能是上一轮的**：判断编译是否干净最快的办法是「能不能进 Play」，或直接看结算面板。
- **MCP 会话在 domain reload 后会断**：Play 期间如果 MCP 工具全挂，退出 Play 立刻恢复；不要据此判断工程坏了。
- **`.meta` 必须以换行结尾**，否则 Unity 报 `Parser Failure`。
- **改 manifest 要在 Unity 关闭时做**，否则会要求重建 `Library/ArtifactDB`（重建约 6 分钟）。

## 6. 交付格式（这是最重要的部分，交回来我要能直接接着干）

结束时输出一份 Markdown 报告，必须包含：

1. **结论**：成功 / 部分成功 / 失败，一句话说清楚卡在哪（如果有）。
2. **分支与提交**：`git log --oneline <基线sha>..HEAD`、`git diff --stat <基线sha>..HEAD` 的摘要。
3. **版本与依赖**：升级前后 `ProjectVersion.txt`、`manifest.json` 的 diff（逐条列出增删的包）。
4. **逐项处理表**：每行 = 问题 / 处理方式 / 证据（文件路径、命令输出、截图路径）。

   | 项 | 处理 | 证据 |
   | --- | --- | --- |

5. **门禁记录**：
   - 结算截图路径（1,000,000 那张）；
   - APK 绝对路径 + `aapt2` 原始输出片段；
   - 冷启动 `TotalTime`；
   - `logcat -s Unity:E` 结果（贴前 5 行或「空」）；
   - perf HUD 的 `fps / worst / p95` 三个数。
6. **未解决 / TODO**：每条写「现象 + 复现步骤 + 日志片段 + 你怀疑的原因」，**不要写「大概」「可能没问题」**；确实不确定就写 `TODO: 待确认`。
7. **文档改动**：改了哪些 md，`docs/INDEX.md` 是否同步。
8. **回滚方式**：具体到命令（分支名、需要还原的目录）。

报告里禁止出现：没有证据的「已验证」、把警告当成功、省略命令输出。
````

## 交付回来的东西

执行者应当交回：上面第 6 节那份报告，加上分支 `upgrade/unity6`（在本地即可，不必 push）。

你把它贴给我，我会据此：

1. 对照 `docs/operations/unity6-upgrade.md` 的验收清单复核它是否真跑通；
2. 补完它留下的 `TODO`；
3. 决定是否合并到主线，以及是否继续做剩下的 asmdef 拆分。