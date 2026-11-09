# Unite Berlin 2018：From Pong to 15 person project（视频）

> 目的：登记这条 Unity 官方演讲视频的可核实元信息（标题、讲者、频道、发布日期、章节结构），并明确说明字幕/讲稿未获取，避免二手转述被当成原文引用。
> 适用读者：想追 [可扩展代码架构](architect-code-that-scales.md) 那份文章出处的人，以及做资料归档的人。
> 前置条件：无（本文为外部资料整理）
> 最后更新：2026-11-02

## 这是什么

Unity 官方频道在 **Unite Berlin 2018** 发布的一场技术演讲，主题是"一个 Pong 级别的小项目，代码结构怎么撑到 15 人、多年的规模"。它是 20 分钟的架构主张 + 后半段的工程流程建议，与本目录另一份整理的 Unity 官方文章 [How to architect code as your project scales](architect-code-that-scales.md) **同源**——那篇文章的正文里明确写着案例是"为我的 Unite Berlin 演讲做的"，并直接链回本视频。

**⚠️ 本文不含视频观点。** 字幕文本未获取（见下一节），任何"视频讲了什么"的逐条断言都会是编造，因此本文只登记可核实的元信息与章节骨架。

## 来源与可访问性

抓取日期统一为 **2026-11-02**。

| URL | 结果 | 说明 |
| --- | --- | --- |
| `https://www.youtube.com/watch?v=1le4vScG3gk` | ⚠️ 部分成功 | 直连返回 200，但页面主体要求登录（"Sign in to confirm you're not a bot"）；`Transcript` 面板存在但内容为空（`NaN / NaN`），**字幕未获取** |
| `https://www.youtube.com/oembed?url=...&format=json` | ✅ 成功（HTTP 200） | 拿到权威的 `title`、`author_name`、`author_url`（用于核实标题与频道） |
| `https://r.jina.ai/https://www.youtube.com/watch?v=1le4vScG3gk` | ⚠️ 部分成功 | 经渲染代理拿到了**播放页全部静态元信息**：发布日期、观看数、简介首段、讲者署名、**全部自动生成章节及时间点**；但字幕正文仍为空 |

**结论**：**未获取字幕/讲稿全文**。可核实的是元信息与章节标题。想看内容主张，请以同源的书面文章为准（见"核心内容"末段）。

## 核心内容

### 可核实的元信息

| 项 | 值 | 来源 |
| --- | --- | --- |
| 标题 | `Unite Berlin 2018 - From Pong to 15 person project` | oEmbed + 播放页 |
| 频道 | **Unity**（`https://www.youtube.com/@unity`，订阅数 1.27M） | oEmbed `author_name` |
| 发布日期 | **2018-07-26** | 播放页（"Jul 26, 2018"） |
| 观看数（抓取时） | 8,779 | 播放页 |
| 讲者 | **Mikael Kalms**（Fall Damage） | 播放页简介："Speakers: Mikael Kalms - Fall Damage…"（简介在播放页被截断，仅此一位可见） |
| 时长 | **未核实**。自动章节最后一条为 `42:16`，故总时长应略长于 42 分钟 | 由章节时间点推断，非官方字段 |
| 字幕 / 讲稿 | **未获取** | 见上一节 |
| 简介首段（原文照录） | "How do you manage the code behind a Unity application as it grows in size? Fall Damage shares code design techniques that can allow a simple Pong game to scale seamlessly to a multi-year, 15-person project." | 播放页 |

### 章节骨架（YouTube 自动生成，可核实）

这些标题是平台自动生成的**分段标签**，只说明视频在哪里换话题，**不等于**该段的论述内容：

| 时间 | 章节 |
| --- | --- |
| 0:00 | Intro |
| 3:55 | Demo: Pong game |
| 5:17 | Principles at work |
| 7:20 | Different purposes |
| 7:35 | Strategy: Instance - Prefab - ScriptableObject |
| 9:55 | Single Responsibility Principle |
| 10:50 | What does this do, anyway? |
| 13:10 | Different types of responsibilities in our classes |
| 15:03 | Moving logic MonoBehaviours - regular C# classes |
| 19:12 | Reorganized version |
| 21:45 | Can we create an almost-hierarchy of our classes? |
| 23:59 | Separate Logic and Presentation |
| 25:06 | Create data-only classes |
| 25:42 | Create helper classes |
| 26:24 | Create static methods |
| 27:57 | Decouple your objects |
| 29:35 | Messaging |
| 31:20 | Stop using LoadScene Mode Single |
| 31:58 | Stop using DontDestroyOnLoad |
| 33:02 | Have a minimal boot scene |
| 33:45 | Support a clean, controlled shutdown |
| 35:30 | Our game's scene layout |
| 38:52 | Test your code |
| 39:35 | Test your content |
| 41:01 | Automate building / testing |
| 42:16 | Create automated playthroughs |

### 视频与文章的对应关系（可核实）

Unity 官方文章 [How to architect code as your project scales](architect-code-that-scales.md) 的正文，在"停止使用 `DontDestroyOnLoad`"那一节引用了本视频并带时间戳 `t=1919s`（即 **31:59**），恰好落在上表 `31:58 Stop using DontDestroyOnLoad` 这一条上。这说明：

- 文章与该演讲**是同一套内容**（文章是该演讲的书面化版本）；
- 想要这段演讲的**可引用内容**，应引用文章正文，而不是"视频里说过"。

因此本文的定位只是**出处登记**，内容主张一律见那份文章。

## 在本项目怎么用

1. **别把"视频说"当引用依据**。本仓库的规范条目若要引这条演讲，一律引 `architect-code-that-scales.md` 的对应小节（它有可核对的原文），视频只作为"原始出处"附在后面。
2. **按章节表跳看**。上表 26 条时间点是可核实的导航：只想看场景加载策略就跳 `31:20`–`33:45`，只想看测试与自动化就跳 `38:52`–`42:16`，不必从头播。
3. **想核对"文章是否忠实于演讲"时**，用章节表做对照坐标（例如文章讲"接口 + 委托"的部分应落在 `21:45`–`23:59` 之间）；核对结论写进本文件的 `## 核心内容`，不要新建旁支文件。
4. **归档时保留这条链接但标注可用性**。YouTube 页面随时可能要求登录或下架，链接旁边必须带"字幕未获取、内容以文章为准"的说明，避免后来人以为这里有逐字稿。

## 与本站整理稿的关系

- 本文是 [可扩展代码架构](architect-code-that-scales.md) 的**上游出处**，两者内容同源；那份文件里所有条款都能在文章正文中找到原文。
- 该演讲后半段"测试与自动化"的主张，落地细节见 [Unity Test Framework 手册整理](unity-test-framework.md)。

## 相关文档

- [外部资料整理目录说明](README.md)
- [代码规范总入口](../README.md)
- [可扩展代码架构（同源文章）](architect-code-that-scales.md)
- [Unity Test Framework 手册](unity-test-framework.md)
- [文档索引](../../INDEX.md)
- 同批整理：[Unity 脚本模板](unity-script-templates.md)、[CSS 命名规范（BEM）](css-naming-conventions.md)
