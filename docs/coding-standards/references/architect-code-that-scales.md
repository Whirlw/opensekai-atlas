# 项目变大时怎么组织代码（How to architect code as your project scales）

> 目的：把 Unity 官方 how-to 文章的架构主张逐节整理成可执行的清单：模块边界、单一职责、数据与表现分离、依赖方向、场景加载与自动化。
> 适用读者：项目已经过了"一个场景几个脚本"阶段、开始感到 MonoBehaviours 膨胀的人。
> 前置条件：无（本文为外部资料整理）
> 最后更新：2026-11-02

## 这是什么

Unity 官方 **how-to** 系列里的一篇长文，用同一个 Pong 小游戏（两个挡板、四面墙、计分 UI）做贯穿案例，演示代码结构怎么从"一个人的小游戏"演进到"15 人多年项目"还能撑住。文章的落点是**技术债**：结构不对时，规模一上来，改动成本会非线性上升。

作者在文中自述这是为其 **Unite Berlin 演讲**做的案例（"a very basic Pong-style game my team made for my Unite Berlin talk"，并给出视频链接，即本目录另一份整理的 [From Pong to 15-person project](from-pong-to-15-person-project.md)）。该视频页的简介署名为 **Mikael Kalms（Fall Damage）**，与文中"my team"一致，故可判断文章作者即该演讲者——**这条对应关系是本整理根据两处来源推出来的，文章页面本身未显示署名**。

## 来源与可访问性

抓取日期统一为 **2026-11-02**。

| URL | 结果 | 说明 |
| --- | --- | --- |
| `https://unity.com/how-to/how-architect-code-your-project-scales` | ❌ 未获取正文 | 直接抓只返回站点导航外壳（页面主体由 JS 渲染），`web_fetch` 报 "Content truncated" 后仅剩导航 |
| `https://web.archive.org/web/20251206122640id_/https://unity.com/how-to/how-architect-code-your-project-scales` | ❌ 未获取正文 | Wayback 快照同样是外壳，仍只有导航与语言切换 |
| `https://r.jina.ai/https://unity.com/how-to/how-architect-code-your-project-scales` | ✅ 成功（HTTP 200，全文） | 经渲染代理拿到完整正文（标题 + 8 个小节 + 全部代码示例说明），本文据此整理 |

**结论**：正文已拿到，但**不是**从 unity.com 直连拿到的，而是经 `r.jina.ai` 渲染代理。文中引用的外链（`architect-game-code-scriptable-objects`、`UnityEvents` 手册页、Obstacle Tower 博客、Unity Integrations 公告）**均未抓取**，本文不转述其内容。

## 核心内容

### 1. 起点：小项目成立的三条原则

Pong 阶段的组织方式是：

- 一个"东西" = 一个 **Prefab**；
- 一个"东西"的自定义逻辑 = 一个 **MonoBehaviour**；
- 一个应用 = 一个**场景**，里面装满互相引用的 Prefab。

这三条对极小项目有效，但文章紧接着说：项目要长大，就得换结构。

### 2. 参数该放哪：Instance / Prefab / ScriptableObject

文章的切入点是**Inspector 看不出意图**：同一个 `Paddle` 组件上有 `Input Axis`、`Movement Speed Scale`、`Position Scale` 三个参数，但"我该在实例上改还是在 Prefab 上改"没有任何线索。作者给出的判据（原文三条）：

| 需求 | 做法 |
| --- | --- |
| 只要一个 | 做 Prefab，然后实例化 |
| 要多个、且每个有少量差异 | 做 Prefab、实例化，再**覆盖部分设置** |
| 多个实例必须共享同一份设置 | 做 **ScriptableObject**，数据从那里取 |

按这个判据，`InputAxisName` 每个玩家不同 → 留在实例上；`MovementSpeedScaleFactor` 与 `PositionScale` 两人共用 → 搬进 `PaddleData`（ScriptableObject），组件里只留一个 `PaddleData` 引用 + 实例自己的差异项。文章的评价是：新结构让"不同设置背后的意图"一眼可见。

### 3. 单一职责：拆大 MonoBehaviour

**Single Responsibility Principle**：每个类只管一件事。判据是能对两个问题给出**简短回答**——"这个类做什么？""它不做什么？"。这条原则与代码库规模无关，多大都适用。

文章用 `Ball` 举例，指出三处混用：

1. `velocity` 一个变量被两种用途共用：给策划设**初始速度向量**，同时给自制物理模拟记**当前速度**——球一动，"初始速度"这个信息就永久丢了；
2. 自制物理模拟不只是 `FixedUpdate()` 里的位移，还包括撞墙反应；
3. `OnTriggerEnter()` 深处有一句 `Destroy()`——**触发器逻辑在删自己的 GameObject**。文章说大代码库里很少允许实体自删，惯例是"谁拥有谁负责删"（owners delete things that they own）。

结论：这些类里混着**游戏逻辑、输入处理、物理模拟、表现**四类职责，应该拆开。

### 4. 职责类型决定承载容器

拆出来的东西放哪种类型里，文章给了规则：

| 内容 | 可以放在 |
| --- | --- |
| 通用游戏逻辑、输入处理、物理模拟、表现 | MonoBehaviour、ScriptableObject、**纯 C# 类**都行 |
| 需要在 Inspector 里暴露参数 | MonoBehaviour 或 ScriptableObject |
| 引擎事件回调、GameObject 生命周期管理 | **必须**留在 MonoBehaviour 里 |

作者的倾向很明确："for many games, it's worthwhile to get as much code as possible out of MonoBehaviours"——尽量把代码挪出 MonoBehaviour。

### 5. 纯 C# 类：好处与代价

| 好处 | 代价 |
| --- | --- |
| 语言设施更适合切分小颗粒、可组合的代码（相比 UnityEngine.Object 体系） | 编辑器不认识这些对象，**没法在 Inspector 里原生显示** |
| 可以与 Unity 之外的 .NET 代码库共享 | 需要自己接序列化/调试入口 |

例子：把物理推进抽成 `BallSimulation`，它只做"积分 + 撞到东西时的反应"。但文章立刻反问：**"撞到了什么"该由球模拟来决策吗？**那更像游戏逻辑。于是形成：`Ball`（MonoBehaviour）持有一部分逻辑来决定怎么驱动模拟，模拟的结果再回填给 MonoBehaviour。重构后一个明显收益是 `Destroy()` 不再埋在很多层之下。

还有一条细节：`FixedUpdate()` 里的位置更新改成"传入当前位置 → 返回新位置"，因为"球模拟并不真正拥有球的位置"，它只是**按传入位置跑一次 tick 并返回结果**。

### 6. 接口与委托：只交出对方需要的那一面

不想把整个 `Ball` 交给别的类时，用接口收窄：`Ball` 实现一个简单接口，`LocalPositionAdapter` 让它能把"位置这一个切面"交出去——"We don't hand the entire Ball object, only the LocalPositionAdapter aspect of it."

需要通知销毁时，不要让 `BallLogic` 直接操作对象：`Ball` 给 `BallLogic` 一个**委托**（而不是让 logic 返回一个"该销毁了"的 flag）。文章的评价：样板代码不少，但每个类的职责都窄而清晰；按这套原则，"一个人的项目也能保持结构良好"。

### 7. 层级与依赖方向

引入 `BallLogic`、`BallSimulation` 这类具体类后，可以拉出一个近似层级：

- **MonoBehaviour 必须知道其他一切**（它就是把那些逻辑包起来的那层）；
- **模拟部分不需要知道逻辑怎么运作**，它只负责跑模拟；逻辑往里喂信号，模拟据此反应；
- **输入应当独立自包含**：输入事件在那里产生，再喂给逻辑，之后发生什么由模拟决定。

### 8. 逻辑与表现分离

输入和模拟这样拆没问题，但**表现**（生成特效、刷新计分）最容易出问题。文章的要求：

- 表现需要知道别的系统在发生什么，但**不需要拥有对它们的完全访问权**；
- 目标是把代码跑成**两种模式**：只跑逻辑 / 逻辑 + 表现；
- 有时必须把两者连起来以保证刷新时机，那也只给"显示所需的最小信息，别多给"。

这样能在两部分之间形成**天然边界**，降低整体复杂度。

### 9. 只放数据的类、只放函数的类、静态方法

- 允许有**只含数据**的类，不必把操作这些数据的方法也塞进同一个类；
- 也鼓励"**不拥有数据、只操作传入对象**"的类（helper）；
- **静态方法**的好处：只要假定它不碰全局变量，光看调用时传了什么参数，就能界定它的影响范围，**不需要读实现**。文章说这接近函数式编程的核心构件（送进去一个值，返回结果或改 out 参数），并建议试一试——"you may find that you get less bugs compared to when you do classic object-oriented programming"。

### 10. 解耦手段：缓冲、队列、消息总线

`Ball` 逻辑与 `Score` 表现之间怎么通信？文章给了递进的三种：

1. **缓冲对象**：逻辑往里写、表现从里读；
2. **队列**：逻辑入队、表现出队；
3. **消息总线（message bus）**：核心是**收发双方互不知道对方，但都知道总线**。计分表现只订阅"分数变化"这类事件，游戏逻辑只管往总线发事件。文章建议从 **UnityEvents** 起步，或自己写一套，并按用途拆多条总线。

### 11. 场景加载

文章的态度非常直接（原文祈使句）：

- **停止使用 `LoadSceneMode.Single`，改用 `LoadSceneMode.Additive`**；
- 要卸载就**显式卸载**——迟早你会需要在切场景时保住几个对象；
- **停止使用 `DontDestroyOnLoad`**：它让你失去对对象生命周期的控制；既然用了 Additive，就不需要它——把长生命周期对象放进一个**专门的常驻场景**；
- 配合上面几条，保留一个**最小的 boot 场景**。

### 12. 干净、可控的关闭

- 让应用在退出前能**释放几乎所有资源**；理想情况下不留仍被赋值的全局变量、不留 `DontDestroyOnLoad` 的对象；
- 有明确的关闭顺序，才容易发现错误和资源泄漏；
- 额外好处：**退出 Play Mode 时 Unity 不做完整域重载**，有干净关闭，编辑器（以及 EditMode 脚本）才不容易在跑过游戏后出现怪异行为。

### 13. 降低场景文件合并的痛苦

- 用版本控制（Git / Perforce / Plastic）；
- **资源全部按文本存**；
- 把对象从场景文件里搬出来做成 **Prefab**；
- 把大场景**拆成多个小场景**（文章提醒：可能需要额外工具支持）。

### 14. 流程自动化

团队到 10 人以上就必须做：

- **代码测试**：逻辑与模拟搬进普通类之后，用单元测试框架写测试"非常直接"；不是处处都值得，但能让后来者更容易接手；
- **内容测试**：给内容创作者一个**标准化的快速校验**入口（校验 Prefab、校验自定义编辑器里填的数据），最好就是编辑器里点一个按钮；
- **把 Test Runner 接进构建系统**，定期自动重跑；问题出现时用通知（Slack / 邮件）推给相关人；
- **自动化试玩（automated playthroughs）**：让 AI 代替人玩游戏并记录错误。文章给的实做法是：同一台机器上跑约 10 个客户端、画质调到最低，让它们自己跑、自己崩，然后看日志——"每一次崩溃都是替我们省下的一次人工试玩"，这样真人试玩时才能专注在"好不好玩"和"视觉问题"上。

## 在本项目怎么用

本项目现状（实测）：主代码在 `Assets/Scripts/Assembly-CSharp`，约 370 个脚本统一 `namespace Sekai.*`；页面切换由 `ScreenManager` 注册表管理（见 `../../architecture/screen-system.md`），AssetBundle 由 JSON 清单驱动（`../../architecture/asset-bundles.md`）。

1. **拿第 2 节的判据清理"该在实例上还是在 Prefab 上"的模糊参数**。设定项、判定宽度这类全局调参已经在 `LiveSettingData` 里（走 ScriptableObject/数据资产），但 MonoBehaviour 上仍可能混着"每个实例不同"和"全项目共用"两类字段；按"多实例共享 → ScriptableObject / 单实例差异 → 实例覆盖"重新分一遍，Inspector 的可读性会立刻改善。
2. **把 Live 运行时的纯逻辑抽成普通 C# 类**。计分（ADR-0004 的累积摊分）、时间轴推进、输入判定这些不依赖引擎回调的部分，照 `BallSimulation` 的模式做成"传入状态 → 返回新状态"的类；`MonoBehaviour` 只留生命周期与引擎回调（第 4 节那张表的硬约束）。这一步同时是接测试的前提。
3. **明确依赖方向**：`ScreenManager` / 页面层可以知道一切，`Live` 的模拟与计分**不应**反过来依赖 UI 或页面。评审时用第 7 节那句话当尺子——"模拟不需要知道逻辑怎么运作"。
4. **计分 → UI 的通信走总线/事件，不要直接引用**。当前如果是结算页直接读计分对象，改成事件（`UnityEvent` 或自建轻量总线），对应第 10 节；这也让"只跑逻辑"的测试模式成为可能（第 8 节）。
5. **场景策略对齐第 11 节**：如果有常驻对象（音频、存档、设置）挂在 `DontDestroyOnLoad` 上，改成"常驻场景 + Additive 加载 + 显式卸载"；启动链保留一个最小 boot 场景。这条与 `../../architecture/live-runtime.md` 里"开局六步"的落地方式直接相关，改动前先看那篇。

## 与本站整理稿的关系

- 本文第 3、4、5 节是 `07-design-practices.md` 里"MonoBehaviour 只做薄壳"主张的上游出处；
- 第 11、12 节的场景与关闭策略，属于架构决策而非代码风格，落地记录应进 `../../architecture/`，不要塞进规范正文；
- 第 14 节的自动化，与 `unity-test-framework.md` 是同一套东西的"为什么"和"怎么做"。

## 相关文档

- [外部资料整理目录说明](README.md)
- [代码规范总入口](../README.md)
- [设计实践](../07-design-practices.md)
- [Unity Test Framework 手册](unity-test-framework.md)
- [模块划分与依赖方向](../../architecture/modules.md)
- [文档索引](../../INDEX.md)
- 同批整理：[Unity 脚本模板](unity-script-templates.md)、[CSS 命名规范（BEM）](css-naming-conventions.md)、[From Pong to 15-person project](from-pong-to-15-person-project.md)
