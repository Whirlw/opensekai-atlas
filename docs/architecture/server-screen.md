# 服务器页面

> 目的：说明首页「服务器」入口之后的整套页面：服务器列表、添加服务器、网页服务器（内置 WebView）和 Sonolus 服务器的原生浏览、搜索、详情、下载与游玩。
> 适用读者：改服务器页面、Sonolus 接入或 WebView 的开发者。
> 前置条件：读过 [首页、开屏与品牌标](home-and-brand.md) 和 [NextRUSH+ 原生加载](native-next-rush.md)。
> 最后更新：2026-11-18

## 概述

服务器页面和首页、曲库一样在 `Library.unity` 里由代码构建，有自己的相机（`depth 102`）和 Canvas，三者同一时刻只开一个。
它第一次被打开时才构建（冷启动不花这份时间）。

- **进入**：首页的服务器卡像曲库卡一样放大盖住整页、颜色过渡到底色，然后服务器列表从同色幕布里升起。
- **服务器列表**：游戏不内置任何谱面服务器。列表一开始是空的，中间提示并给出「添加服务器」按钮；之后的每个服务器都是玩家自己添加的。
- **退出**：列表页左上角返回（或 Android 返回键）时幕布落下，服务器卡缩回首页原位。
- **页面栈**：列表 → Sonolus 首页 / 搜索结果 → 谱面详情，左右滑入滑出；返回键先关掉页面上打开的卡片、下拉框，再回上一页。
- **游玩**：详情页点「游玩」，未下载时先下载入库，然后服务器页面落幕、曲库直接上屏并开局（复用曲库的加载卡与开局流程）。打完回到曲库和结算卡。

## 核心内容

### 文件

| 位置 | 职责 |
| --- | --- |
| `Infrastructure/Sonolus/SonolusServerAddress.cs` | 地址与分享链接解析（裸域名、HTTPS、路径前缀、`/levels/{name}`、`sonolus://`、`open.sonolus.com` 包装），资源 URL 相对服务器前缀解析；拒绝 HTTP |
| `Infrastructure/Sonolus/SonolusModels.cs` | 宽松解析：服务器信息、`levels/info` 的栏目与搜索表单、列表分页（页码 / cursor）、详情、评论、排行榜记录 |
| `Infrastructure/Sonolus/SonolusClient.cs` | `UnityWebRequest` 匿名 GET，`localization=zhs`、超时、取消、大小上限，401/403/404/429/5xx 转成中文提示 |
| `Infrastructure/Sonolus/SonolusLevelInstaller.cs` | 下载 data / bgm / cover、校验 SHA-1、按文件头识别格式、解析一次谱面、写 manifest 与 `sonolus-source.json`，经 `SongPackageManager.ImportFolder` 入库；维护「已下载 / 可更新」索引 |
| `Infrastructure/Sonolus/SonolusText.cs` | `#NEWEST`、`#KEYWORDS` 等 Sonolus 文本常量的中文 |
| `App/Server/ServerRegistry.cs` | 保存的服务器（`persistentDataPath/servers.json`），只有玩家自己添加的；游戏不内置任何服务器 |
| `App/UI/Server/ServerScreen.cs` | 相机、Canvas、背景光晕、页面栈与滑动、模态层与下拉层、提示条、幕布；`ServerPage` 基类、`SonolusSession` |
| `App/UI/Server/ServerListPage.cs` | 服务器卡片网格（沿用首页卡片框）、⋯ 菜单（打开 / 移除） |
| `App/UI/Server/ServerAddPanel.cs` | 添加服务器卡：类型（Sonolus / 网页）、地址、名称；Sonolus 先请求 `/sonolus/info` 验证并取默认名称 |
| `App/UI/Server/SonolusBrowsePage.cs` | 首页栏目（横向封面行）、全部谱面与搜索结果（虚拟网格，滚到底自动加载下一页）、快速搜索、筛选 |
| `App/UI/Server/SonolusSearchPanel.cs` | 按服务器 `searches` 声明生成的高级搜索表单 |
| `App/UI/Server/SonolusLevelPage.cs` | 谱面详情：封面、等级 / 难度 / 引擎、署名、标签；游玩、下载 / 更新 / 取消、试听；简介 / 社区 / 排行榜三个标签页 |
| `App/UI/Server/SonolusLevelTile.cs` | 封面格子（等级角标、已下载标记），栏目行与网格共用 |
| `App/UI/Server/RemoteCovers.cs` | 远程封面磁盘缓存（按 SHA-1 命名），解码与取色复用曲库的 `JacketThumbnails` |
| `App/UI/Server/SonolusPreviewPlayer.cs` | 试听：下载 preview（没有则用 bgm），按音乐音量循环播放并淡入淡出 |
| `App/UI/UiToast.cs` | 首页与服务器页共用的底部提示条 |

路径均相对 `Assets/Game/Runtime/OpenSekai/`。复用的曲库部件：`LibraryWidgets`（新增 `TextField`）、`LibrarySettingRows`（新增文本、数值、多选行，供高级搜索）、`LibraryModal`、`LibraryDropdown`、`VirtualListView`、`JacketThumbnails`、首页的 `HomeCardFrame`。

### Sonolus 接口用法

| 页面 | 请求 |
| --- | --- |
| 添加服务器 | `GET /sonolus/info`：标题、是否提供谱面 |
| 首页栏目 | `GET /sonolus/levels/info`：`sections`（只显示 `itemType = level` 的非空栏目）、`searches` |
| 全部谱面 | `GET /sonolus/levels/list?page=n` |
| 快速搜索 | `GET /sonolus/levels/list?type=quick&keywords=…` |
| 高级搜索 | `GET /sonolus/levels/list?type=<search.type>&<每个字段>=…`，字段与默认值全部来自服务器 |
| 详情 / 简介 | `GET /sonolus/levels/{name}`：`item`、`description`、`hasCommunity`、`leaderboards` |
| 社区 | `GET /sonolus/levels/{name}/community/comments/list?page=n` |
| 排行榜 | `GET /sonolus/levels/{name}/leaderboards/{lb}/records/list?page=n`，空时回退到 `/leaderboards/{lb}` 的 `topRecords` |
| 下载 / 试听 / 封面 | 跟随 SRL 的 `url`，以 `/` 开头的相对服务器地址（含前缀）拼接 |

`pageCount < 0` 的服务器按 cursor 分页。高级搜索支持 `text`、`textArea`、`slider`、`toggle`、`select`、`multi`；其它类型（如 `serverItem`）不显示也不发送。

UntitledCharts 实测（2026-11-18）：`levels/info` 有 4 个栏目（Random Staff Pick、#NEWEST、#RANDOM、#POPULAR）和一个 14 字段的 `advanced` 表单；列表 1557 页；详情含 7 个排行榜。

### 下载与入库

只接受 `engine.name == NextRUSH_P` 的谱面，其它引擎在详情页标明原因并禁用下载。
上限：谱面数据 32 MiB、音乐 256 MiB、封面 16 MiB；有 hash 的资源逐个校验 SHA-1，失败不入库。
暂存目录在 `temporaryCachePath/SonolusStaging/<随机>`，谱面能解析后才交给 `ImportFolder`，结束即删除。

曲包内容与命令行工具 `Tools/download-sonolus-chart.ps1` 一致：

```text
manifest.json          chartFormat = next-rush，title/composer/userName/playLevel 来自谱面，难度取自标签（Easy … Append），没有则 master
level-data.json.gz     服务器原始压缩字节
audio.mp3|ogg|wav      按文件头识别
jacket.png|jpg         可选；封面失败不影响入库
sonolus-source.json    服务器地址、name、version、引擎、各资源 hash、导入器版本、完整详情
```

「已下载」索引在 `persistentDataPath/sonolus-downloads.json`，键是 `服务器 host+前缀 | level name`，值是曲包目录和 data hash。
目录被删掉（曲库里删除了曲包）后自动失效。服务器上的 data hash 变了显示「更新」：新曲包入库成功后才删除旧曲包（旧曲包的游玩记录随之不再显示）。

下载可随时取消；能否播放仍以 [NextRUSH+ 原生加载](native-next-rush.md) 的兼容范围为准，不兼容的谱面在开局时报原因。

### 网页服务器（WebView）

网页服务器在 Android 的独立 Activity（`ChartBrowserActivity`）里打开，顶栏与游戏同色：**返回**（先退网页历史，到第一页时回到游戏）、站名和状态、刷新、关闭。
系统返回键 / 返回手势同返回按钮。可导航的范围是该站的 host 及其子域名（OpenSekai 另加其曲包存储域名）；站外链接被拦截并在顶栏提示。
下载必须由本站页面发起、且为 HTTPS，文件本身可来自 CDN；Cookie 只发给本站 host。下载与导入流程见 [Android 社区谱面下载](../guides/server-chart-downloads.md)。
编辑器里没有内置 WebView，会用系统浏览器打开网址。

## 示例

截一套服务器页面（需要联网，UntitledCharts 的内容以当时为准）：

```powershell
powershell -File Tools/capture-screens.ps1 -Server -Width 1920 -Height 1080
```

依次输出：服务器卡放大 0.35 / 0.70、点进后的列表、添加服务器卡，再直接打开 UntitledCharts（只用于截图，不写进服务器列表）的首页、高级搜索、全部谱面、第一个栏目第一首的详情（简介 / 社区 / 排行榜）、返回后的首页。

> **版权提示**：Sonolus 画面里的封面、曲名和评论来自线上服务器的第三方内容。这些截图只用于本地检查，不要提交到仓库、不要放进文档或发布说明，用完即删。

## 验证

- `Assets/Tests/EditMode/Sonolus/SonolusProtocolTests.cs`（不联网）：地址与分享链接解析、前缀拼接与资源 URL、栏目 / 搜索表单 / 详情 / 评论 / 排行榜解析、分页、文本常量、引擎判断、manifest 映射、媒体文件头、服务器列表的保存与恢复。
- `SonolusLiveServerTests`（可选，联网）：设置 `OPENSEKAI_SONOLUS_LIVE=untitledcharts.com` 后读取栏目与两页列表，把第一首 NextRUSH+ 谱面装进临时曲库，检查谱面字节与 SHA-1 一致、可解析、索引为「已下载」。
- `Tools/capture-screens.ps1 -Server`：见上文示例。

尚未在 Android 真机上验收 WebView 顶栏、试听解码和下载后开局。

## 常见问题与故障排查

- **添加时提示「服务器返回的不是 Sonolus 数据」**：地址指向了网站首页以外的东西，或服务器不在根路径；带上路径前缀，或直接粘贴一个谱面分享链接。
- **详情页「下载」是灰的**：谱面不是 NextRUSH+ 引擎，或详情还没加载完。
- **游玩后回到的是曲库**：结算卡属于曲库；服务器页面不在打歌后恢复。
- **封面一直是占位**：封面不是 PNG / JPEG，或下载失败（见日志 `[Sonolus] Cover download failed`）。

## 相关文档

- [Sonolus 谱面下载接入调研](sonolus-download.md)
- [NextRUSH+ 原生加载](native-next-rush.md)
- [Android 社区谱面下载](../guides/server-chart-downloads.md)
- [首页、开屏与品牌标](home-and-brand.md)
- [截图工具](../operations/screenshot-tools.md)
