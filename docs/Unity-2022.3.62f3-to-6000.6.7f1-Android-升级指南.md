# Android 项目从 Unity 2022.3.62f3 升级至 Unity 6000.6.7f1 详细指南

> 核对日期：2026-11-01。目标版本为 **Unity 6.6（6000.6.7f1）**。
> 本文是通用迁移实施指南，尚未检查你的项目源码、插件清单或构建配置。涉及插件版本、图形效果、商店审核和性能的结论，须以项目验证结果为准。

## 1. 升级策略与关键结论

建议先保留可发布的 2022.3 分支，在独立副本上升级；完成代码、资源和 Android 构建迁移后，再进行性能优化。不要把渲染管线切换、网络框架重写等工作合并到第一次升级中。

可选择以下路线：

| 路线 | 适用情况 | 实施方式 |
| --- | --- | --- |
| 直接升级 | 依赖较少，有完整测试和版本控制 | 在副本上直接打开 6000.6.7f1，按阶段修复 |
| 分段升级 | 原生插件、URP 自定义渲染、复杂资源系统较多 | 2022.3.62f3 → 选定的 6000.0 或 6000.3 补丁版 → 6000.6.7f1，每段保留检查点 |

中间版本是排查问题的工程手段，并非要求逐个安装所有 Unity 版本。即使直接升级，也应阅读跨越版本的升级说明，尤其是 6.0、6.5 和 6.6。

### 1.1 这次升级必须提前处理的变化

| 事项 | 对项目的影响 | 处理要求 |
| --- | --- | --- |
| Android 最低支持版本为 Android 8.0 / API 26 | 部分旧设备不再属于支持范围 | 评估用户设备分布，将 Minimum API Level 至少设为 26 |
| GLES 最低为 3.1 | 仅支持 GLES 3.0 且无 Vulkan 的设备无法运行 | 检查设备覆盖范围和最终 Manifest |
| Android x86-64 支持已移除 | 原有 x86-64 构建或模拟器方案需要调整 | 以支持的 ARM 设备验证，不沿用旧架构矩阵 |
| Android 仅支持 Adaptive 图标 | Legacy、Round 图标不会按旧方式打包 | 补齐 Adaptive 图标并真机查看 |
| Android 构建工具链变化 | 旧 Gradle 模板和 SDK 注入脚本可能失效 | 用目标编辑器模板重建，再迁移必要定制 |
| Cinemachine 成为核心包 | 依赖 Cinemachine 2 的项目可能自动迁移至 3 | 单独审查相机代码与镜头资源 |

来源：[Android 支持范围](https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/android/introducing/requirements-and-compatibility)、[Unity 6.5 升级说明](https://docs.unity.com/en-us/engine/6000.6/manual/upgrade-guides/upgrade-guide-unity65)、[Unity 6.6 升级说明](https://docs.unity.com/en-us/engine/6000.6/manual/upgrade-guides/upgrade-guide-unity66)。

**业务决策点：**如果产品必须继续支持 Android 7 或仅 GLES 3.0 的设备，应先重新评估目标版本。单纯修改 Manifest 或降低 Player Settings 不等于恢复引擎支持。

## 2. 升级前准备：建立可回退的基线

### 2.1 保存完整工程与发布资料

1. 在 2022.3.62f3 中确认工程能够编译、运行并生成 Android 包。
2. 提交全部有效改动，建立升级前标签，再创建升级分支。
3. 保留旧编辑器与 Android 模块，不覆盖原安装。
4. 备份以下内容：
   - `Assets/` 及全部 `.meta` 文件。
   - `Packages/manifest.json`、`Packages/packages-lock.json`、本地与嵌入包。
   - `ProjectSettings/`，包括 `ProjectVersion.txt`。
   - 工程外引用的本地包、原生 Android 工程、构建脚本和 CI 配置。
   - Keystore、Alias、证书指纹与安全保存的密码；密码不要提交到仓库。
   - Addressables 配置、远程 Catalog、已发布资源与内容构建状态文件。
   - 混淆映射、原生符号、线上版本号和依赖清单。

Git 示例，执行前先确认工作区没有待处理改动：

```bash
git status --short
git tag pre-unity-6000.6.7f1
git switch -c upgrade/unity-6000.6.7f1
```

不要将 `Library/`、`Temp/`、`Obj/` 等缓存当作唯一备份。也不要为了减少升级差异而删除 `.meta`：这可能破坏 GUID 引用。

### 2.2 记录当前构建配置

建议保存截图和一份配置表，至少包含：

| 类别 | 需要记录的配置 |
| --- | --- |
| 应用身份 | Package Name、Version、Version Code、签名方式 |
| Android | Minimum/Target API、IL2CPP/Mono、ARM 架构、Activity 入口 |
| 图形 | Built-in/URP、Color Space、Graphics API、质量等级、纹理压缩 |
| 代码 | API Compatibility Level、宏、Managed Stripping Level、`link.xml` |
| 构建 | 场景顺序、资源构建流程、APK/AAB、导出 Gradle 工程方式 |
| Android 定制 | Manifest、Gradle 模板、ProGuard/R8、Java/Kotlin、原生库 |
| 在线功能 | 登录、广告、支付、推送、崩溃上报、远程资源版本 |

### 2.3 制作旧版测试基线

在相同设备和场景下记录冷启动时间、内存、CPU/GPU 帧耗时、包体和资源下载量。保存核心页面、战斗场景、特效、文字和光照截图。

准备一台安装旧发布版的测试设备，保留真实存档；后续验证应包含“覆盖安装后继续使用”，不能只测卸载后的首次启动。

**本阶段完成标准：**旧工程、旧安装包、签名资料和关键资源均可找回，且旧版可独立构建。

## 3. 盘点依赖与升级风险

为每项依赖填表，不建议在编辑器升级时同时无差别升级全部包：

| 依赖 | 当前版本 | 目标兼容版本 | 依赖来源 | Android 原生代码 | 验证结果 |
| --- | --- | --- | --- | --- | --- |
| 渲染管线与 Shader 插件 | 待填写 | 待核对 | UPM/源码/商店 | 是/否 | 待验证 |
| Addressables / AssetBundle 工具 | 待填写 | 待核对 | 待填写 | 是/否 | 待验证 |
| 输入、相机、动画、UI | 待填写 | 待核对 | 待填写 | 是/否 | 待验证 |
| 广告、统计、支付、登录、推送 | 待填写 | 待核对 | 待填写 | 是/否 | 待验证 |
| 热更新、反射、代码生成、AOT | 待填写 | 待核对 | 待填写 | 是/否 | 待验证 |
| AAR/JAR/SO、自定义 Activity | 待填写 | 待核对 | 待填写 | 是/否 | 待验证 |

特别检查：

- 是否同时存在 UPM 和手动导入的同一插件。
- 是否混用多套 Android 依赖解析器，或残留旧版 AAR/JAR。
- 插件是否支持目标 Unity、目标 AGP/Gradle、IL2CPP 和所需 ARM ABI。
- 原生 `.so` 是否具备 16 KB 内存页兼容性。
- 无源码插件如果没有兼容版本，是否可以替换或暂时移除。
- 自定义 Editor 扩展是否依赖内部 API、反射访问或硬编码菜单路径。

插件供应商明确支持的版本比“最新版”更适合作为首轮迁移目标。每一组依赖升级后记录版本、变更原因及验证结果。

## 4. 安装 6000.6.7f1 与 Android 工具链

### 4.1 通过 Unity Hub 安装

从 [6000.6.7f1 官方发布页](https://unity.com/releases/editor/whats-new/6000.6.7f1)进入 Hub 安装指定版本，并勾选：

- Android Build Support。
- Android SDK & NDK Tools。
- OpenJDK。

不要误装成 6000.0、6000.3 或其他 6000.6 补丁版；CI 与开发机也应统一完整版本号。

### 4.2 核对工具版本

官方当前依赖表对 6000.6.7f1 列出的工具如下：

| 工具 | 目标配置 |
| --- | --- |
| OpenJDK | 17 |
| NDK | r27c，27.2.12479018 |
| SDK Build Tools | 36.0.0 |
| SDK Command-line Tools | 16.0 |
| SDK Platform Tools | 37.0.1 |

2022.3 的 JDK 11、NDK r23b 不能作为默认配置直接沿用。SDK Build Tools 版本也不等于应用的 Target API Level。来源：[官方依赖版本表](https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/android/getting-started/sdksetup/supported-dependency-versions)。

在 Windows 的 `Edit > Preferences > External Tools` 中检查 Android 路径，优先使用随目标编辑器安装的工具。CI 同步检查 `JAVA_HOME`、SDK/NDK 路径和 Gradle 配置，避免仍指向旧安装目录。

Unity 6.5 升级说明列出了 Gradle 9.1.0、AGP 9.0.0 的工具链更新。对目标工程，应以 **6000.6.7f1 实际生成的模板、Gradle Wrapper 和构建日志**为最终核验依据，不要从旧模板强制锁回旧工具链。来源：[Android 构建工具更新](https://docs.unity.com/en-us/engine/6000.6/manual/upgrade-guides/upgrade-guide-unity65)。

**本阶段完成标准：**目标编辑器安装正确，Android 模块完整，开发机与 CI 使用一致的工具来源。

## 5. 首次打开与 C# 编译修复

### 5.1 首次打开

1. 关闭旧版编辑器，从 Hub 选择升级副本并指定 6000.6.7f1。
2. 阅读升级提示，允许在副本上进行必要的 API 更新。
3. 如提示进入 Safe Mode，先在 Safe Mode 中修复编译错误。
4. 等待包解析、脚本编译与资源导入完成，保存完整 Editor 日志。
5. 按版本控制差异检查自动修改，确认没有意外删除包、脚本或资源。

先解决最早出现的有效错误，再重新编译。后面的数百条错误可能只是同一个包或程序集未编译的连锁结果。

### 5.2 建议修复顺序

1. 包解析、缺失程序集和 `.asmdef` 引用。
2. 第三方插件与代码生成器。
3. 项目自身的 Editor 代码。
4. 运行时代码、平台宏和 Android 桥接。
5. 废弃 API 与警告；开启 warnings-as-errors 的项目需提前处理。

API Updater 不能保证处理反射字符串、预编译 DLL、原生代码、序列化迁移以及所有行为变化。对自动修复的每一组改动单独审查并提交。

### 5.3 行为兼容性检查

- 替换查找对象 API 时，保留原来的排序、非激活对象和查找范围语义。
- 避免因命名空间或程序集变化导致序列化类型、反射类型名和存档类型名失效。
- 检查 `UNITY_2022_*` 等条件编译分支是否会让新版走入错误逻辑。
- 保留 `.meta` 和资源引用，打开核心 Prefab、场景并检查 Missing Script。
- 复核 `link.xml`、`[Preserve]`、反射、泛型 AOT、热更新桥接代码；编辑器运行正常不能证明 IL2CPP 发布包正常。

6.6 对 `UNITY_64`、`DEVELOPMENT_BUILD` 增加弃用诊断，并引入 Managed Code Variant。迁移时按“检查/断言”与“日志/性能采样”的实际用途审查宏，不要简单全局替换。来源：[6.6 编程变更](https://docs.unity.com/en-us/engine/6000.6/manual/upgrade-guides/upgrade-guide-unity66)。

**本阶段完成标准：**无编译错误，主要场景可打开，关键对象引用完整，进入 Play Mode 后主流程可运行。

## 6. 渲染、相机与资源迁移

### 6.1 保持原有渲染方案完成首轮升级

Built-in 项目先验证原管线；URP 项目使用与目标编辑器兼容的包组合。不要直接复制其他工程的 URP Asset、Renderer Asset 或整套 Graphics Settings。

对照基线检查：

- Graphics 与各 Quality 等级实际使用的渲染资产。
- 粉色材质、Shader 编译、Shader Graph、自定义关键字和变体剔除。
- 深度/不透明纹理、相机堆叠、透明物体、阴影、后处理、RenderTexture。
- 自定义 Renderer Feature/Pass 与目标 Render Graph API 的兼容性。
- 光照烘焙、Light Probe、Reflection Probe 和天空盒效果。

若旧渲染扩展依赖兼容模式，先确认目标版本确实支持，再使用过渡方案；不要假定早期 Unity 6 教程中的开关在 6.6 仍存在。Unity 将 Built-in、URP、HDRP 的升级说明分开，需根据项目管线查阅。[Unity 6 渲染升级入口](https://docs.unity.com/en-us/engine/6000.6/manual/upgrade-guides/upgrade-guide-unity6)。

### 6.2 GLES 与 Vulkan 分开验证

先按旧版主要图形路径建立可运行版本，再分别测试 Vulkan 和 GLES 3.1。对同一设备保持画质、分辨率与测试场景一致，记录 GPU 帧耗时、内存和异常日志。

“Use OpenGL ES 3.0 shaders”用于 Shader 过渡兼容，不会恢复 GLES 3.0 设备支持。更改 Graphics API 后重新生成包，并检查最终 Manifest，不能只看编辑器设置。[Android 图形要求](https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/android/introducing/requirements-and-compatibility)。

### 6.3 Cinemachine、UI 与输入

Cinemachine 2 项目应将迁移视为独立工作项：备份镜头资源，按迁移工具和手册处理后，检查 Timeline、镜头切换、优先级、碰撞、跟随与相机脚本。不要只替换命名空间。[Cinemachine 2 → 3 迁移手册](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachineUpgradeFrom2.html)。

同时测试字体、中文与回退字形、Mask、Canvas 缩放、刘海与安全区、多点触控、软键盘及返回键。输入系统保持既有选择，确有需要再迁移，并检查是否出现双重输入事件。

## 7. Android 原生层、Manifest 与 Gradle

### 7.1 重建自定义 Gradle 模板

重点盘点 `Assets/Plugins/Android/` 中的以下文件及插件生成脚本：

```text
mainTemplate.gradle
launcherTemplate.gradle
baseProjectTemplate.gradle
settingsTemplate.gradle
gradleTemplate.properties
AndroidManifest.xml
proguard-user.txt
```

不同项目实际文件可能不同。推荐操作：

1. 将旧模板另存到工程外的备份目录，记录定制用途。
2. 在升级分支中临时停用自定义模板，生成目标编辑器默认工程。
3. 重新生成目标版本模板。
4. 逐项迁回 Maven 仓库、依赖、签名接入、ABI、混淆及构建定制。
5. 每迁入一组定制即构建，确认最终导出工程和依赖树。

检查 namespace、旧 Gradle API、插件任务名、AndroidX、重复类、仓库配置和依赖版本约束。不要把删除冲突依赖或降低 AGP 当成通用解决方法；先找到引入依赖的 SDK 与兼容组合。

### 7.2 自定义 Activity 与 Unity as a Library

若工程包含 Java/Kotlin 启动类、渠道壳或宿主 Android 工程，先确认使用 Activity 还是 GameActivity 入口。为减少变量，首轮尽量保留原入口类型。

Unity 6 的 Android 桥接存在结构变化：部分旧 `UnityPlayer` 使用场景需适配 `UnityPlayerForActivityOrService`；需要容器时使用 `getFrameLayout()`，不能继续假定 `UnityPlayer` 自身就是 `FrameLayout`。具体按入口和编译错误迁移，避免对全部类型名做盲目替换。[Unity 6 Android Java 迁移说明](https://docs.unity.com/en-us/engine/6000.6/manual/upgrade-guides/upgrade-guide-unity6)。

逐项验证生命周期、焦点、权限、返回键、Deep Link、通知点击、登录支付回调，以及宿主与 Unity 页面的反复进入退出。

### 7.3 检查最终合并 Manifest

源 Manifest 正确不代表合并结果正确。导出构建后检查：

- Package Name、启动 Activity、`exported`、Intent Filter。
- Minimum/Target SDK、权限、`uses-feature` 与 GLES 要求。
- FileProvider authority、Deep Link scheme、通知与后台服务。
- SDK 自动注入的权限、重复组件、`tools:replace` 的实际效果。
- Adaptive 图标资源与系统启动画面。

6.5 的 Android 全屏/insets 配置有调整。使用旧自定义主题或 `unity.launch-fullscreen` 的项目，应按新文档复核系统栏、手势区及横竖屏布局。[6.5 Android 适配说明](https://docs.unity.com/en-us/engine/6000.6/manual/upgrade-guides/upgrade-guide-unity65)。

### 7.4 16 KB 内存页兼容

不要因为升级了编辑器，就认定包内所有原生库均兼容。第三方 AAR 内也可能包含旧 `.so`。

建议按以下顺序检查：

1. 枚举 APK/AAB 中所有原生库，记录来源、ABI 与版本。
2. 根据 Android 官方工具检查 ELF 段对齐和 APK ZIP 对齐。
3. 对不兼容库索取供应商兼容版本，或从源码重新编译。
4. 在 16 KB 环境验证启动及实际调用原生 SDK 的路径。
5. 同时回归普通 4 KB 设备。

可使用 Android SDK 工具辅助检查；下例中的工具须已加入 PATH，文件名请替换：

```powershell
# 查看连接设备实际内存页大小；16 KB 环境应为 16384
adb shell getconf PAGE_SIZE

# 检查 APK 的 ZIP 对齐；此检查不能代替 ELF 和运行验证
zipalign -c -P 16 -v 4 .\app-release.apk
```

AAB 还应检查实际生成/交付的 APK，不能将上述 APK 命令直接用于 AAB。完整流程参见 [Android 官方 16 KB 指南](https://developer.android.com/guide/practices/page-sizes)。商店政策可能更新，提交前以对应商店控制台和官方政策为准。

## 8. Addressables、AssetBundle 与存档

### 8.1 将资源版本与客户端版本绑定

跨引擎升级时，不要未经测试就让新客户端复用旧 Bundle，也不要让旧客户端自动加载新引擎构建的资源。

建议：

1. 用目标编辑器重建 Android 资源与 Catalog。
2. 新旧客户端使用独立远程路径，例如 `android/unity2022/`、`android/unity6000/`。
3. 明确客户端可加载的资源版本，保留旧发布资源。
4. 检查依赖重复、Shader 资源、压缩格式、缓存与下载大小。
5. 测试首次下载、命中旧缓存、断网重试、下载中断和 CDN 缓存。
6. 核对内容更新流程对旧构建状态文件的使用条件；引擎和包大版本迁移优先建立新完整构建基线。

资源服务端回滚与客户端回滚需要联动，避免旧客户端被导向不兼容资源。

### 8.2 验证存档与覆盖安装

- 使用相同 Package Name 和合法延续的签名更新关系。
- 在旧版创建角色、关卡、设置和下载缓存后，直接覆盖安装新版。
- 测试 PlayerPrefs、JSON、数据库、自定义二进制与加密存档。
- 检查重命名类型、程序集、字段与序列化格式的变化。
- 存档迁移使用明确版本号；先备份，再转换，避免失败后覆盖唯一旧数据。
- 约定新版产生的数据是否能被旧客户端读取。

若只测卸载重装，会掩盖最关键的存档、数据库和缓存兼容问题。

## 9. 分阶段构建与 CI

### 9.1 构建顺序

| 阶段 | 构建内容 | 通过标准 |
| --- | --- | --- |
| A | 最小启动场景、本地资源 | 能安装、启动并输出可用日志 |
| B | 完整场景、Development 包 | 主流程、SDK 与资源下载正常 |
| C | IL2CPP ARM64、正式剥离/混淆配置 | 反射、AOT、原生桥接与发布路径正常 |
| D | 正式签名 AAB/渠道包 | 内测交付、覆盖安装和支付登录回调正常 |

AAB 不能像 APK 一样直接用 `adb install` 安装，需通过商店内测或 bundletool 生成适配 APK 集。渠道分包与重签名之后也要复测。

使用 `File > Build Profiles` 检查 Android 配置、场景列表与活动 Profile。迁移旧构建脚本时，验证它最终使用的场景、平台和设置，不仅确认脚本返回成功。

### 9.2 CI 必须同步的内容

- 固定编辑器完整版本及 Android 模块，验证许可证可用。
- 使用与开发机一致的 Packages 锁定文件及本地包来源。
- 按编辑器版本、平台和依赖区分缓存；首次验收进行一次干净构建。
- 检查构建前后处理脚本、Android 工程修改脚本与模板注入。
- 明确发布包的 Managed Code Variant、剥离、混淆与符号策略。
- 构建失败以 BuildReport、进程退出码和产物检查综合判断。
- 归档日志、配置快照、原生符号、混淆映射、包哈希与资源版本。
- 签名凭据通过 CI 安全变量提供，避免写入日志。

建议在独立干净检出中验证一次“从源码到最终包”的完整过程，防止本机缓存掩盖缺失文件。

## 10. 真机验证矩阵与验收标准

### 10.1 设备选择

| 维度 | 覆盖建议 |
| --- | --- |
| Android 系统 | 最低支持系统、主流系统、最新目标系统 |
| GPU | 产品实际覆盖的 Adreno、Mali 等 GPU |
| 图形 API | Vulkan、GLES 3.1 分别测试 |
| 性能 | 低内存/低端、中端、高端设备 |
| 内存页 | 4 KB 与 16 KB 环境 |
| 安装路径 | 全新安装、旧版覆盖升级、商店内测安装 |
| 网络 | 弱网、断网、切换网络、恢复下载 |
| 窗口状态 | 前后台、旋转、分屏及设备支持的窗口变化 |

仅 GLES 3.0 且无 Vulkan 的设备应验证为不再支持的设备类别，不应列为运行验收通过项。

### 10.2 测试清单

- [ ] 启动、首场景、重复切场景、退出与重启。
- [ ] 登录、注销、账号切换、Deep Link、推送唤起。
- [ ] 支付成功/取消/恢复、广告展示与奖励发放。
- [ ] 权限允许、拒绝、再次请求和从设置恢复。
- [ ] 前后台、锁屏、音频中断、焦点变化。
- [ ] 文字、UI、安全区、软键盘、输入与返回键。
- [ ] Shader、透明、阴影、后处理、粒子与视频。
- [ ] 碰撞、触发、动画、寻路及核心玩法数值。
- [ ] 远程资源、缓存、断点重试和版本切换。
- [ ] 存档迁移、覆盖安装、低存储空间和失败恢复。
- [ ] 发布包反射/AOT、混淆后 JNI 调用。
- [ ] 崩溃上报、符号解析与 ANR 监控可用。

在测试前约定性能阈值，例如核心场景帧耗时、峰值内存和冷启动时长的最大允许回退。与旧版比较时统一设备温度、画质、分辨率、测试时长和构建类型；Development 包只用于诊断，最终性能以发布配置复核。

### 10.3 6000.6.7f1 的定向回归

官方发布页列有与本项目可能相关的已知问题，包括重复 `LoadSceneAsync` 切场景后停止响应，以及设备旋转时的 Android RenderPass 错误日志。应把高频切场景和旋转纳入定向测试；是否受影响需按具体 Issue 的复现条件判断。若命中发布阻断问题，记录最小复现并评估修复补丁，不能把升级完成等同于版本适用。[6000.6.7f1 已知问题](https://unity.com/releases/editor/whats-new/6000.6.7f1)。

## 11. 常见问题排查表

| 现象 | 优先调查 | 建议操作 |
| --- | --- | --- |
| 导入后大量 C# 报错 | 包解析、首个编译错误、程序集 | 先恢复依赖，再修业务代码 |
| Gradle 构建失败 | 日志中首个失败任务及根因 | 对照新模板，检查工具版本和 SDK 注入 |
| Duplicate class | 同一 Android 库被多次引入 | 查依赖树，统一来源和兼容版本 |
| Manifest merger failed | 重复组件、authority、SDK 要求 | 查看合并报告，逐项处理冲突 |
| Java 找不到 Unity 方法 | 旧 UnityPlayer API、入口不一致 | 按目标 Activity/桥接结构适配 |
| 启动黑屏或闪退 | Logcat、原生库、图形初始化 | 对比 API、ABI、插件与首场景 |
| 发布包出错但开发包正常 | 剥离、混淆、AOT、诊断宏 | 用最小必要保留规则定位 |
| `DllNotFoundException` | ABI、SO 打包和依赖库 | 检查包内容及动态加载依赖 |
| 粉色材质或特效消失 | Shader 错误、管线、变体剔除 | 对照材质与构建日志定位 |
| 存档/下载资源失效 | 类型变更、旧缓存、Catalog | 使用旧版数据复现并执行显式迁移 |
| 16 KB 环境启动失败 | ELF、ZIP 对齐和第三方 SO | 找到不兼容库后升级或重编译 |
| 包体/内存明显增大 | ABI、重复资源、纹理、符号 | 对比构建报告和资源依赖 |

排查时每次只调整一组变量。保留失败包、日志和配置，避免不断清空缓存后失去复现条件。

## 12. 发布与回退

### 12.1 发布前门槛

- [ ] 工程记录的编辑器版本为 6000.6.7f1，干净环境可构建。
- [ ] 所有核心依赖版本及兼容证据已记录。
- [ ] Minimum/Target API、架构与设备覆盖符合产品要求。
- [ ] Package Name、签名、Version Code 正确。
- [ ] 最终 AAB/渠道产物完成内测与覆盖升级验证。
- [ ] 新旧资源隔离，存档迁移具备失败恢复方案。
- [ ] 发布性能达标，阻断缺陷清零。
- [ ] 符号、映射、日志、配置和产物均已归档。
- [ ] 商店当前审核要求与 SDK 合规要求已核对。

先内部测试，再小范围发布，观察崩溃、ANR、启动成功率、资源失败率以及支付/广告指标。为每个指标指定负责人和停止放量的阈值。

### 12.2 回退方案

工程回退：使用升级前标签恢复到独立目录，并以原编辑器打开。不要把已升级资源的工程直接交回旧编辑器。

线上回退：停止放量，必要时用旧代码基线生成 **更高 Version Code** 的修复包；通常不能把较低版本号安装包作为普通商店更新发布。同步恢复服务端开关、资源路由和兼容配置。

数据回退：如果新版已写入不兼容存档或数据库，旧客户端未必能读取。发布前明确向后兼容、双格式或恢复策略，不能只准备一个旧 APK。

## 13. 官方资料索引

- [6000.6.7f1 发布说明与下载](https://unity.com/releases/editor/whats-new/6000.6.7f1)
- [Unity 6.0 升级指南](https://docs.unity.com/en-us/engine/6000.6/manual/upgrade-guides/upgrade-guide-unity6)
- [Unity 6.5 升级指南](https://docs.unity.com/en-us/engine/6000.6/manual/upgrade-guides/upgrade-guide-unity65)
- [Unity 6.6 升级指南](https://docs.unity.com/en-us/engine/6000.6/manual/upgrade-guides/upgrade-guide-unity66)
- [Android 环境配置](https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/android/getting-started/sdksetup)
- [Android 工具依赖版本](https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/android/getting-started/sdksetup/supported-dependency-versions)
- [Android 支持范围](https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/android/introducing/requirements-and-compatibility)
- [Cinemachine 迁移](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachineUpgradeFrom2.html)
- [Android 16 KB 内存页兼容指南](https://developer.android.com/guide/practices/page-sizes)

> 执行时，建议为每个阶段建立独立提交，并在迁移记录中写清“改了什么、解决什么问题、在哪台设备和哪种构建配置下验证通过”。这份记录将直接支持后续排错、复审和版本维护。
