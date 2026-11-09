# 发布（GitHub 分发）

> 目的：把「从当前源码打一个能发到 GitHub Releases 的包」这件事固定下来，包括版本号、签名、校验和与说明模板。
> 适用读者：要发版或做发布验证的人。
> 前置条件：读过 [Android 构建](android-build.md)，本机已装 Unity 6000.6.7f1 与 Android Build Support。
> 最后更新：2026-11-03

## 概述

本项目是纯开源项目：**只从 GitHub 发布源码与 APK，不上架任何应用商店**。因此没有 AAB、Play Core、商店审核与商店签名流程，需要保证的是另外三件事：

1. 同一个包名下的签名保持稳定，用户才能「下载新 APK → 直接覆盖安装」，不用卸载（卸载会清掉曲库与历史记录）。
2. 每个发布包都能被独立重建，并且有校验和可以对照。
3. 版本号与 tag 一一对应，出问题时能回到确定的提交。

## 快速开始

```powershell
# 1. 从干净的 main 出发
git switch main
git pull
git status --porcelain          # 必须是空的

# 2. 版本号：改 ProjectSettings 里的 bundleVersion 与 AndroidBundleVersionCode
#    然后提交，打 tag（tag 名与 bundleVersion 一致）
#    预发布用 0.1.0-dev 这样的后缀；AndroidBundleVersionCode 必须单调递增，
#    否则覆盖安装会被系统拒绝（INSTALL_FAILED_VERSION_DOWNGRADE）
# 3. 构建（编辑器打开本工程时走 Pipeline 命令；没人开编辑器时用 unity build）
unity command build.run --target Android `
  --outputPath Builds/Release-2026-11-03/OpenSekaiAtlas-release.apk --confirm true

# 4. 校验签名与权限
apksigner verify --print-certs Builds/Release-2026-11-03/OpenSekaiAtlas-release.apk
aapt2 dump badging Builds/Release-2026-11-03/OpenSekaiAtlas-release.apk |
  Select-String "package:|sdkVersion|targetSdkVersion|native-code|uses-permission"

# 5. 生成校验和并写进 release notes
Get-FileHash Builds/Release-2026-11-03/OpenSekaiAtlas-release.apk -Algorithm SHA256
```

## 详细说明

### 签名

仓库里带一个**公开的** release keystore：

| 项 | 值 |
| --- | --- |
| 文件 | `Tools/signing/opensekai-atlas-release.keystore`（PKCS12） |
| 别名 | `opensekai` |
| 库口令 / 别名口令 | `opensekai` |
| 证书 | `CN=Open Sekai Atlas, OU=Community, O=OpenSekai, C=CN` |
| 证书 SHA-256 | `B4:62:79:74:5F:6A:9B:1F:D0:75:7B:9D:3D:94:0B:8A:B6:0E:B1:51:6B:1F:F7:AC:09:EB:21:97:B0:E4:EC:89` |
| 有效期 | 10000 天 |

口令写在这里是刻意的：签名的作用是「同一个包名的更新能被系统接受」，不是防伪。任何人都能用它签出同包名的包，所以**不要用签名判断某个 APK 是不是官方构建**——判断依据是 GitHub Releases 页面本身、tag 和 SHA-256 校验和。

`Assets/Game/Editor/ReleaseSigningBuildStep.cs` 是一个 `IPreprocessBuildWithReport`，在每次 Android 构建前把 keystore 写进 `PlayerSettings`，所以编辑器里点 Build 和命令行构建签出来的证书一致。需要手工确认当前配置时用菜单 `OpenSekai/Dev/Apply Release Signing`。

> Unity 6 里 `PlayerSettings.Android.keystorePass` / `keyaliasPass` 不写进 `ProjectSettings.asset`（明文口令不落 ProjectSettings），所以 `ProjectSettings.asset` 里看不到口令是正常的；打包时由上面那个预处理步骤注入。

从**旧的 debug 签名包**迁移过来时，签名不同，系统会拒绝覆盖安装，会报 `INSTALL_FAILED_UPDATE_INCOMPATIBLE`。这是**一次性**的：让用户先备份曲库、卸载旧包、再装新包；之后所有版本都用同一把 keystore，就能正常覆盖升级。

### 版本号与 tag

- `bundleVersion`（`projectVersion`）用语义化版本 `MAJOR.MINOR.PATCH`；`AndroidBundleVersionCode` 是单调递增的整数，每次发布 +1，**不能重复**（Google 的要求不适用，但 Android 覆盖安装仍然要求它不小于已安装版本）。
- tag 用 `v` + `bundleVersion`，例如 `v2.1.0`。tag 指向的提交必须就是构建用的提交。
- 建议在 release notes 里写出构建用的提交短哈希，便于对照。

### 发布前检查

1. 编辑器门禁：Play → 曲库 → 自动打歌 → `1,000,000 / S`。
2. 真机门禁：见 [Android 构建](android-build.md) 的「真机门禁」小节（`Test` 谱 221 音符，`Unity:E` 除 AssetPackManager 外无日志）。
3. `apksigner verify` 的证书 SHA-256 与上表一致。
4. `aapt2 dump badging` 里 `sdkVersion:'26'`、`targetSdkVersion:'36'`、`native-code:'arm64-v8a'`、权限只有 `INTERNET`。
5. 包体没有异常膨胀（Unity 6 + Low 剥离 + 仅 ARM64 约 113–119 MB）。

### release notes 模板

```markdown
## Open Sekai Atlas v2.1.0

- 提交：<短哈希>
- 引擎：Unity 6000.6.7f1 / URP 17.6.0
- 包体：<bytes> 字节（<MiB> MiB）
- SHA-256：<hash>

### 变更
- …

### 安装
1. 下载 `OpenSekaiAtlas-v2.1.0.apk`
2. 允许「安装未知来源应用」
3. 覆盖安装；如果是从旧 debug 签名包升级，需要先备份曲库再卸载旧版

### 已知问题
- …
```

## 示例

本地验证一个已经下发的包是不是这次构建出来的：

```powershell
$apk = "Builds/Release-2026-11-03/OpenSekaiAtlas-release.apk"
(Get-FileHash $apk -Algorithm SHA256).Hash.ToLower()
# 与 release notes 里的 SHA-256 逐字符对照
```

## 常见问题

**覆盖安装失败 `INSTALL_FAILED_UPDATE_INCOMPATIBLE`**：本次包与已安装包的签名不同。检查是否误用了 debug keystore（`ProjectSettings.asset` 里 `androidUseCustomKeystore` 应为 1，或用菜单重新应用），或用户装的是更早的 debug 版。

**签名后装到设备，曲库报「Access to the path ... is denied」**：release 包不可调试，如果曲库目录是先由 `adb push` 创建的，它属于 shell 而不是应用。用 `Tools/push-device-test-chart.ps1`（已内置 `chmod -R 777`）重推，或者先让应用启动一次再推。这是测试环境的坑，不影响真实用户（他们的目录始终由应用自己创建）。

**构建出来的证书对、但是 `sdkVersion` 还是 25**：Unity 复用了旧的 Gradle 工程，删掉 `Library/Bee/Android/Prj/IL2CPP/Gradle` 重新打包。

## 相关文档

- [Android 构建](android-build.md)
- [编辑器菜单](editor-menus.md)
- [Unity 6 升级清单](unity6-upgrade.md)
