# Android 构建

> 目的：从「工具链装没装全」到「APK 体积是多少、权限对不对」，给出可复现的出包流程。
> 适用读者：打包与测试人员、开发者。
> 前置条件：读过 [环境与运行](../getting-started/build-and-run.md) 与 [AssetBundle 与清单](../architecture/asset-bundles.md)。
> 最后更新：2026-11-02

## 概述

出 Android 包只需要三件事：装全 Android Build Support（含 NDK）、跑一次 AssetBundle 打包、执行构建。
产物在 `Builds/<目录>/`（已被 `.gitignore` 忽略）。2022.3 基线为 **123.9 MB**、仅 `INTERNET` 权限；Unity 6 本次结果见 [升级执行记录](../changelog/2026-11-unity6-upgrade.md)，不可将旧包指标当作新包结果。

## 核心内容

### 工具链

| 组件 | 要求 | 本机实际路径 |
| --- | --- | --- |
| Unity | 6000.6.7f1（升级分支） | `C:\Program Files\Unity\Hub\Editor\6000.6.7f1` |
| Android Build Support | 含 `AndroidPlayer`、`SDK\platforms`、`SDK\build-tools`、`NDK`、`OpenJDK` | `...\Editor\Data\PlaybackEngines\AndroidPlayer\` |
| SDK platforms | 34 / 35 / 36 | `...\AndroidPlayer\SDK\platforms\` |
| build-tools | 36.0.0 | `...\AndroidPlayer\SDK\build-tools\36.0.0\` |
| adb | 随 SDK 提供 | `...\AndroidPlayer\SDK\platform-tools\adb.exe` |
| aapt2（核对 APK 用） | 随 build-tools | `...\build-tools\36.0.0\aapt2.exe` |

只装 `tools` 是不够的：Hub 的 `android-sdk-ndk-tools` 有时只解压 `sdk-tools-windows-*.zip`，导致
`platforms` / `build-tools` / `NDK` 全缺，构建会直接失败。

### Player Settings（当前默认）

| 项 | 值 | 设定位置 |
| --- | --- | --- |
| Package Name | `io.github.opensekai` | Player Settings |
| Min SDK | 26 | `AndroidMinSdkVersion`；升级前实际配置已为 26 |
| Target SDK | 跟随最高（当前 36） | `AndroidTargetSdkVersion: 0` |
| Scripting Backend | IL2CPP | `scriptingBackend.Android: 1` |
| Target Architectures | **仅 ARM64** | `AndroidTargetArchitectures: 2` |
| Managed Stripping | **Low** | `managedStrippingLevel.Android: 1` |
| 宏 | `DOTWEEN` | `scriptingDefineSymbols` |
| Active Input Handling | Input System Package (New) | `activeInputHandler: 1`；Android 不支持 Both。UI 使用 `InputSystemUIInputModule`，残留旧输入消费者通过 `NativeInput` 读取新系统 |

`ManagedStrippingLevel` 的枚举值是反直觉的：**Disabled=0、Low=1、Minimal=4**，改 YAML 时别写错。

### 出包步骤

```text
1. 菜单 OpenSekai/AssetBundles/Build AssetBundles And Info
   日志应出现：bundles=15, output=Assets/StreamingAssets/data
   目录应有 42 个文件 / 13.1 MB
2. （改过清单或权限时）删除 Library/Bee/Android/Prj/IL2CPP/Gradle
3. Build Settings → Android → Build（或菜单/CI 调 BuildPipeline）
4. 核对产物：aapt2 dump permissions / badging
```

### 真机门禁（约 1 分半）

用仓库里固定的测试曲包代替动辄两分半的正式曲：`Tools/fixtures/baseline_tap`（manifest 标题 `Test`，
221 个音符，9 秒前奏 + 约 40 秒谱面，一局约 55 秒）。

这份谱面是按类别手写的，覆盖运行时全部音符类别（Normal / Long / Connection / Flick / Friction /
FrictionHide / FrictionLong / FrictionHideLong / FrictionFlick / Hidden / Guide 等），所以一局就能看出
某类音符有没有退化。`Tools/generate-test-chart.ps1` 是同一份 fixture 的程序化版本（7 类 × Default/Critical），
需要纯自动生成时用它覆盖 `score.json`。

```powershell
# 1. 推包（Windows 自带 PowerShell 用 -File；装了 pwsh 可直接 pwsh -File）
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/push-device-test-chart.ps1

# 2. 冷启动并确认曲库里有 Test
adb shell am force-stop io.github.opensekai
adb shell am start -W -n io.github.opensekai/com.opensekai.CustomUnityPlayerActivity

# 3. 点 Test 那一行的「自动」，等 60 秒后截图
adb shell screencap -p /sdcard/gate.png
adb pull /sdcard/gate.png gate.png
```

判定：结算面板出现 `Baseline Tap 64 Notes · 自动 · Lv.1 · 完成`、`1,000,000`、`S`、`总音符 221`，
并且 `adb logcat -d -s Unity:E` 里没有本次运行的新错误。

已知良性 E 级日志：每次冷启动会有一条 `java.lang.ClassNotFoundException: Didn't find class "com.google.android.play.core.assetpacks.AssetPackManager"`。
这是 Unity 自带的 Play Asset Delivery 探测代码在找不到 Play Core 库时打的日志；我们不用 AAB 资源分包，APK 里也没有该库，
所以它是预期行为，不影响启动与打歌（`logcat -b crash` 为空）。判据是「除这一条之外没有其它 E 级日志」。曲库排序按标题，标题 `Test` 会落在 `Final Lap` 之后，点它那一行即可。

推包脚本只写应用私有目录 `Android/data/io.github.opensekai/files/CustomMusicScores/`，不会覆盖用户已有的曲包。

### 测试设备

| 设备 | 分辨率 | 刷新率 | 用途 |
| --- | --- | --- | --- |
| Xiaomi 22041216C | 2460×1080 | 120 Hz 常驻 | 后续默认测试机（用户 2026-11-02 指定） |
| OPPO PLQ110 | 2800×1272 | 自适应，仅触摸到 165 Hz | 旧机，仅做对比 |

新机（22041216C）上有两处与旧机不同，门禁前先确认：

1. `adb shell input tap` 会报 `java.lang.SecurityException: Injecting input events requires ... INJECT_EVENTS permission`。
   需要在该机开发者选项里打开 **USB 调试（安全设置）**（MIUI/HyperOS 的额外开关），否则只能人工点屏幕。
2. `adb push` 写 `Android/data/io.github.opensekai/files/` 会返回 `failed to read copy response: EOF` 并留下空目录。
   改用 `Tools/push-device-test-chart.ps1` 里「先 mkdir、逐文件 push」的顺序，或把曲包打成 .oskp 放进 `/sdcard/Download` 再用应用内「导入 ZIP」。

### 三个必踩的坑

1. **AB 没进包**：`Assets/StreamingAssets/data` 为空时照打不误，APK 里就没有 `assets/data/**`。
   Unity 把 `StreamingAssets` **平铺**到 APK 的 `assets/` 根，所以核对的是 `assets/data/*` 是否有 16 个条目。
2. **权限剥离没生效**：Android 的清单合并发生在多个模块。`RecordingBuildRules` 现在扫描整个 Gradle 工程里
   所有 `AndroidManifest.xml`；同时 `Assets/Plugins/Android/AndroidManifest.xml` 用
   `tools:node="remove"` 声明要删的权限（`READ_EXTERNAL_STORAGE` / `WRITE_EXTERNAL_STORAGE`）。
   自检菜单 `OpenSekai/Dev/Verify Recording Permission Rule` 期望输出 `plainDeclarations=0 removeMarkers=2`。
3. **Unity 复用旧 APK**：只改清单不触发重建，13–35 秒「构建成功」但 APK 的 mtime 与大小都不变。
   必须删 `Library/Bee/Android/Prj/IL2CPP/Gradle`。

### 体积

| 配置 | APK | `libil2cpp.so`（arm64 / armv7） |
| --- | --- | --- |
| Minimal 剥离 + 双架构 | 156.2 MB | 74.9 / 63.5 MB |
| Low 剥离 + 双架构 | 149.7 MB | 60.4 / 51.5 MB |
| **Low 剥离 + 仅 ARM64（当前默认）** | **123.9–125.2 MB** | 60.4 MB / — |

构成（ARM64 + Low）：`assets/bin` 69.2 MB、`lib/arm64-v8a` 25.2 MB、`assets/data` 13.1 MB、`.acb` 音频约 13 MB。
`libil2cpp.so` 未压缩 60.4 MB，进包压缩后仅 16.4 MB —— 想继续压体积要动资源和纹理/音频编码，而不是代码。

代价：32 位设备装不上。要兼容老机器把 `AndroidTargetArchitectures` 改回 3，体积回到约 149.7 MB。

## 示例

核对一个已构建的 APK：

```powershell
$aapt2 = "C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\build-tools\36.0.0\aapt2.exe"
& $aapt2 dump permissions Builds/Unity6Upgrade-2026-11-02/OpenSekaiAtlas-unity6.apk
& $aapt2 dump badging   Builds/Unity6Upgrade-2026-11-02/OpenSekaiAtlas-unity6.apk | Select-String "package:|native-code|sdkVersion"
```

安装并启动：

```powershell
$adb = "C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"
& $adb install -r Builds/Unity6Upgrade-2026-11-02/OpenSekaiAtlas-unity6.apk
& $adb shell am start -W -n io.github.opensekai/com.opensekai.CustomUnityPlayerActivity
```

## 常见问题与故障排查

- **`Native extension for Android target not found`**：Android Build Support 没装或没装全。
- **构建 13 秒就「成功」但包没变**：见坑 3。
- **装上去闪退**：`adb logcat -s Unity:E` 看异常；Low 剥离下若用到反射，需回归对应功能。
- **权限还在**：核对 `Assets/Plugins/Android/AndroidManifest.xml` 的 `tools:node="remove"` 是否还在，
  以及是否删过 Gradle 缓存目录。

## 相关文档

- [真机性能](android-performance.md)
- [AssetBundle 与清单](../architecture/asset-bundles.md)
- [故障排查](troubleshooting.md)
