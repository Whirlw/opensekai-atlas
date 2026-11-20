# 从服务器游玩和下载谱面

从首页进入「服务器」，点击「添加服务器」，选择「Sonolus」或「网页」。列表初始为空，由玩家添加站点。

## Sonolus 服务器

1. 输入服务器地址或粘贴 Sonolus 谱面分享链接。打开服务器后，可从栏目选谱、按关键词搜索，或使用服务器提供的高级筛选。提交过的高级筛选会按服务器记住，重启后可继续使用。
2. 打开谱面详情，可试听、浏览简介、评论或排行榜。支持的谱面可直接游玩，也可先下载到曲库供离线使用。
3. 从详情页直接开局，结算后可返回同一详情页。已下载的谱面使用本地文件。未下载的谱面暂存在当前进程的内存中，重启后需重新获取。
4. 游玩后返回详情并点「下载」。相同资源仍在缓存时，游戏直接保存到曲库。安装成功后会释放该谱面的临时缓存。

可游玩的引擎和功能范围见 [NextRUSH+ 兼容清单](../architecture/next-rush-compatibility.md)。服务器浏览、缓存和安装机制见 [服务器页面](../architecture/server-screen.md)。

## Android 网页服务器

点网页服务器后，在内置浏览器中打开它的地址；顶栏的返回按钮先退网页历史，到第一页时回到游戏，系统返回键 / 手势相同，右侧有刷新和关闭。用户自行搜索、登录、完成网站人机验证并点击下载，系统 WebView 的 DownloadListener 将 HTTPS 下载交给 Android DownloadManager。返回游戏后自动解压、验证并刷新曲库，无需再次选择文件。

## 集成方式

项目为 Unity 6000.6.7f1，沿用 `Assets/Plugins/Android/com/opensekai/` 的 Java 源码插件方式，由 Unity 的 Android 构建自动编译，无需额外 AAR、Android Studio 工程或场景接线。不要再导入包含相同类的 AAR。Manifest 增加了 INTERNET 权限和不可外部启动的 ChartBrowserActivity；不需要存储权限、AndroidX、OkHttp 或第三方 WebView SDK。使用项目随 Unity 安装的 JDK 和 compileSdk 36 或更高版本。

主要文件：

- `HomeView` / `HomeScreen`：服务器卡、进出动画、进度和导入结果提示、曲库刷新。
- `ServerScreen` / `ServerListPage`：服务器列表与添加；网页服务器经 `ServerChartDownloads.Open(url, title)` 打开。
- `ChartBrowserActivity.java`：WebView、顶栏（返回 / 站名与状态 / 刷新 / 关闭）、加载进度条、返回键与手势、生命周期及销毁。
- `MiniWebView.java`：DownloadManager、Cookie / User-Agent / Referer、持久任务记录和查询。
- `ServerChartDownloads.cs`：启动及返回前台后的轮询、串行导入、状态提示和重试。
- `SafeZipExtractor.cs`：后台安全解压；`SongPackageManager.ImportDownloadedZipAsync` 校验并原子发布。

## 文件与恢复

下载保存到 `Application.persistentDataPath/Downloads/<随机UUID>_<过滤后的文件名>`。Android 的 persistentDataPath 对应应用的 external files 目录，启动下载不使用公共 Downloads。下载任务 ID 与真实路径保存在 SharedPreferences；关闭网页、切后台或进程被回收不会主动取消系统任务。下次游戏运行时重新查询，成功任务自动导入。

WebView 是独立的原生 Activity。网页显示期间 Unity 可能暂停；下载仍由系统执行，解压和游戏内进度提示会在返回游戏后继续。系统限制、强行停止应用、无网络及卸载应用可能影响下载；不承诺强行停止后仍执行后台工作。

包先解压到曲库同卷的 `.chart-import-<UUID>` 临时目录。校验 manifest 和谱面后移动到 `CustomMusicScores/server-<下载ID>`，再更新现有缓存。相同下载 ID 的恢复不会重复导入；用户主动重新下载是新任务，会保留为另一份谱面，不覆盖原有曲包。成功后清除任务记录并删除原始压缩包。导入失败保留原包，重新打开「服务器」会重试；下载失败会清除失效任务，需在网站重新下载。

进程恰好在解压中被终止可能留下 `.chart-import-*` 临时目录；它不在曲库内，不会被加载。可在确认没有导入进行时清理这些目录。若发布成功后进程终止，固定的安装路径充当收据，下次查询直接恢复缓存。

## 安全与限制

网页只能在所打开站点的 host 及其子域名内导航（OpenSekai 另加其下载页面实际使用的 `tzi998.oss-cn-shanghai.aliyuncs.com` 存储域名，见 `MiniWebView.extraHostsFor`），站外链接被拦截。下载必须由本站页面发起且为 HTTPS，文件可以来自 CDN；Cookie 只发给本站 host。下载内容按曲包严格校验后才导入。Cloudflare 验证使用网页自身的脚本和 iframe，无 JS 到 Unity 的桥接接口。SSL 错误使用系统默认拒绝策略；禁用 file/content 访问及混合内容。

DownloadManager 自己处理 HTTP 重定向，插件无法逐跳审查其目标，因此白名单约束初始 URL 和网页导航；可信服务器须保证下载重定向目的地可信。仅支持 DownloadListener 能提供的 HTTP GET 下载，`blob:`、`data:`、需要 POST 请求体的文件响应不支持，不注入通用 JS 下载桥。网站的验证 POST 若最终跳到 HTTPS GET 附件地址可正常使用。网站验证码由用户完成，不能自动绕过。

ZIP / OSKP 按内容读取，不依赖扩展名；要求包含现有曲包格式的 `manifest.json` 和有效谱面。单个压缩包最多 4096 项，单文件 512 MiB，解压总大小 2 GiB。拒绝绝对路径、`..`、盘符、符号链接和重复文件，按实际输出字节数再次检查限制。解压前检查可用空间，写入中磁盘耗尽作为失败处理。Manifest 的文件引用必须与 manifest 位于同一目录，以匹配现有曲包读取器；压缩包本身可以包含子目录。JSON 和原生 NextRUSH 谱面在后台校验，SUS 转换需要加载 Unity 配置，保留在主线程。不会执行压缩包中的程序或脚本。多个任务可同时下载，导入严格串行；网络暂停、未知总大小和下载失败有独立状态提示。下载轮询发现文件超过 2 GiB 时取消；Unity 暂停时不会轮询，因此它不是系统后台下载的硬性限额。

Android 10–16 使用相同的系统下载与应用专属目录 API；Android 13+ 注册返回手势回调，旧版本处理返回键。系统 WebView 必须已安装且可用。Android 10–16 真机兼容性仍须按下列清单实测，不能以 Java 编译替代。

## 验证清单

1. Android 构建进入首页，点服务器，添加一个网页服务器（例如 `https://opensekai.com/`）并打开，确认网页打开，登录/人机验证可用。
2. 下载一个真实曲包，返回游戏，确认自动出现进度、成功提示，曲库能找到并播放。
3. 同时下载多个谱面；未知 Content-Length 不显示错误百分比。
4. 关闭/重复打开网页、网页返回、返回手势、旋转屏幕和切后台，确认界面与下载正常。
5. 下载期间断网再恢复；模拟磁盘不足、404、无效 ZIP，确认失败提示和重试。
6. 下载中杀进程后重启，确认任务恢复；发布后未确认前重启，确认没有重复导入。
7. 检查 `persistentDataPath/Downloads`、曲库安装目录及临时目录清理。
8. 运行 EditMode 的 `ServerChartImportTests` 与现有 `SongPackageManagerTests`，覆盖路径越界、UTF-8、符号链接、文件数限制、恢复去重及失败保留原曲库。
9. 在 Android 10、13、16 至少各一台设备上验证系统 WebView、DownloadManager、返回与后台行为。

体积方面未引入 Chromium、下载 SDK 或 ZIP 库，只有 Java / C# 代码及现有 .NET ZIP 依赖。增量预计为几十至数百 KB，取决于 IL2CPP 裁剪、压缩和已有 ZIP 依赖；这是估算，尚未实测。使用相同构建设置比较集成前后的 APK Analyzer / AAB 大小。当前项目未开启自定义 ProGuard；若未来启用 Java 缩减，需保留 `com.opensekai.MiniWebView` 的 JNI 调用方法（例如 `-keep class com.opensekai.MiniWebView { public *; }`）。

## 历史验证（2026-11-17）

Android IL2CPP 兼容性修复：不再调用 `DriveInfo`（其 `IsReady` 会触发不支持的 `GetDriveFormat` 内部调用）。Android 使用系统 `android.os.StatFs.getAvailableBytes()` 查询实际解压目录的剩余空间；异步导入在 Unity 主线程先获取数值，再传给后台解压器，避免后台线程执行 JNI 调用。空间预检保留 32 MiB 余量，写入时的磁盘不足仍会作为导入失败处理。先前导入失败保留的压缩包无需重新下载：使用新包覆盖安装、不要卸载，然后重新打开服务器并返回游戏即可重试。

- Unity 6000.6.7f1 编辑器编译通过；C# 的 Android Player 分支独立编译通过。
- 两个 Java 插件源文件使用 Unity 自带 JDK、android-36 android.jar 和 Unity classes.jar 编译通过。旧版返回键 API 的弃用提示属于兼容路径。
- EditMode 曲包相关测试共 68 项全部通过，其中服务器导入测试 14 项，包含空间不足预检和传入可用空间快照的边界验证。
- 已检查真实谱面详情与安全下载页面，确认存在用户人机验证及阿里云存储地址。尚未在 Android 真机上完成验证后的实际下载、自动导入和播放；未生成或测量 APK/AAB。

参考：[Android DownloadManager.Request](https://developer.android.com/reference/android/app/DownloadManager.Request)。
