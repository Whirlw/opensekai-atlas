# Unity 官方示例 C# 代码风格指南（Unity-Code-Style-Guide 仓库）整理

> 目的：把 Unity 官方示例风格指南仓库中的具体条款逐条整理成可直接对照执行的中文清单。
> 适用读者：本项目所有写 Unity C# 代码的开发者，以及做 Code Review 的人。
> 前置条件：无（本文为外部资料整理）
> 最后更新：2026-11-02

## 这是什么

`thomasjacobsen-unity/Unity-Code-Style-Guide` 是 Unity 官方 C# 风格指南（C# Code Style Guide，Unity 6 edition，配套电子书《Creating a C# style guide for clean and scalable game code》）的公开示例仓库。作者是 Unity 的 Thomas Jacobsen，内容基于 Unity 内部与外部专家的最佳实践汇总，定位是**示例**（a source of inspiration）而非强制规范——README 明确写着 "The guidelines we provide are recommendations, not hard and fast rules"，并要求团队按自己偏好裁剪后**一致执行**。

仓库本身非常小，只有三个条目：`README.md`（导言，说明这份指南的来历与用法）、`StyleExample.cs`（**真正的规则正文**，用一个带大量 `// |` 注释的示例文件逐条演示规则）、`.idea/`（IDE 配置目录，无规范内容）。因此**本文件的条款全部来自 `StyleExample.cs`**，README 只提供定位与免责说明。

该仓库自述其规则基本是 [Microsoft Framework Design Guidelines](https://docs.microsoft.com/en-us/dotnet/standard/design-guidelines/) 的子集，并建议团队风格指南在与微软指南冲突时**优先于**微软指南。

## 来源与可访问性

抓取日期统一为 **2026-11-02**，全部经 `web_fetch`（沙箱内 curl / Invoke-WebRequest 无法直连外网）。

| URL | 结果 | 说明 |
| --- | --- | --- |
| `https://github.com/thomasjacobsen-unity/Unity-Code-Style-Guide` | 未获取正文 | 仓库首页为 GitHub 动态渲染页面，正文以 raw 形式获取 |
| `https://raw.githubusercontent.com/thomasjacobsen-unity/Unity-Code-Style-Guide/master/README.md` | ✅ 成功（HTTP 200，全文） | 导言：指南定位、来源、电子书链接、与其他指南的关系 |
| `https://raw.githubusercontent.com/thomasjacobsen-unity/Unity-Code-Style-Guide/master/StyleExample.cs` | ✅ 成功（HTTP 200，全文） | **规则正文**，21 KB，含 NAMING / CASING AND PREFIXES / FORMATTING / SPACING / COMMENTS / CLASS ORGANIZATION / USING / NAMESPACES / ENUMS / INTERFACES / CLASSES / FIELDS / PROPERTIES / EVENTS / METHODS 等分节注释 |
| `https://raw.githubusercontent.com/thomasjacobsen-unity/Unity-Code-Style-Guide/main/README.md` | ❌ 失败（HTTP 404） | 仓库只有 `master` 分支，无 `main` |
| `https://api.github.com/repos/thomasjacobsen-unity/Unity-Code-Style-Guide/contents/` | ✅ 成功（HTTP 200，JSON） | 根目录仅 `.idea/`、`README.md`、`StyleExample.cs` 三项 |
| `https://raw.githubusercontent.com/wiki/thomasjacobsen-unity/Unity-Code-Style-Guide/Home.md` | ❌ 失败（HTTP 404） | 仓库**没有 wiki** |
| `StyleExample.cs` 之外的 `docs/`、`.editorconfig`、`CSharpStyleGuide.md` | 未获取（不存在） | 由上面的 contents API 目录清单确认：仓库根目录没有 `docs/` 目录、没有 `.editorconfig`、没有 `CSharpStyleGuide.md` 之类正文文件 |
| `https://unity.com/resources/c-sharp-style-guide-unity-6` | ❌ 失败（正文未获取） | 返回的是营销页外壳，正文由 JS 渲染，抓取内容只有站点导航；该资源实际形态是电子书/PDF |
| `https://unity.com/how-to/naming-and-code-style-tips-c-scripting-unity` | ❌ 失败（正文未获取） | 同上，JS 外壳，无可读正文 |

**结论：一手正文已拿到**——`StyleExample.cs` 就是本指南的规则正文，本文全部条款均据其原文整理，未做补充推断。唯一"未获取"的是 unity.com 上的电子书与两篇 how-to 文章正文（JS 渲染 / PDF），本文**不引用**其内容。

## 核心条款

### 0. 指导性原则（KEY PRINCIPLES）

| 条款（原文用词） | 具体含义 |
| --- | --- |
| **Favor readability over brevity** | 可读性优先于简短。"Clarity is more important than any time saved from omitting a few vowels." 不要为了少打几个字母牺牲清晰度 |
| **Consistency** | 最重要的一条："The most important thing is to be consistent." 团队先达成一致，然后坚持 |
| **Pragmatic** | 务实：100+ 人团队和 2 人团队的需求不同，规则可裁剪 |
| **Living document** | 风格指南是活文档，随项目与团队演进更新 |
| **Learn the rules before you break them** | 新手先守规则；有经验后再有依据地偏离 |
| **Favor what can be pronounced naturally** | 选能自然读出来的名字，例如 `HorizontalAlignment` 优于 `AlignmentHorizontal` |
| **Names reveal intent** | "Pick meaningful names from the beginning to minimize refactoring later." |

### 1. 大小写与命名总则（CASING AND PREFIXES / NAMING）

| 条款 | 原文关键词 |
| --- | --- |
| 默认一律 **Pascal case**，除非另有说明 | `Use Pascal case ... unless noted otherwise` |
| **camel case** 用于局部变量、私有变量、参数 | `for local/private variables and parameters` |
| **禁止** snake case、kebab case、Hungarian notation | `Avoid snake case, kebab case, Hungarian notation` |
| 一个文件里若有 **MonoBehaviour**，源文件名必须与类名一致 | `the source file name must match` |
| 可用 `m_`（成员）/`k_`（常量）/`s_`（静态）前缀；也可用 `_` 前缀；也可完全不用前缀——**选一种并保持一致** | 原文给出三种流派并明说 "Pick what works best for you and the team" |
| 删除冗余初始化：`int` 不写 `= 0`，引用类型不写 `= null` | `Drop redundant initializers` |
| 访问修饰符**要么全写、要么全省**，保持一致 | `Specify (or omit) access level modifiers consistently` |
| 避免冗余名字：类叫 `Player` 就别写 `PlayerScore` / `PlayerTarget`，直接 `Score` / `Target` | `Avoid redundant names` |
| 变量名用**名词**，但 `bool` 要用**动词前缀** | `isDead`, `isWalking`, `hasDamageMultiplier` |
| 不要缩写（数学或业界通用缩写除外） | `Don't abbreviate (unless it's math or commonly accepted)` |
| 类型名要在一组命名空间/问题域里无歧义，避免通用词或加前后缀 | `use 'PhysicsSolver', not 'Solver'` |

> 注意：原文在"是否写 `private`"上**自相矛盾**——`CASING AND PREFIXES` 段写 "We recommended explicitly specifying private"，而 `FIELDS` 段写 "We recommend to leave out things that are implicit and thus redundant (such as private)"。落地时必须由团队裁决一条，本文只记录该冲突。

### 2. 字段（FIELDS）

| 条款 | 说明 |
| --- | --- |
| 名字里**不要**出现特殊字符（反斜杠、符号、Unicode 字符），会干扰命令行工具 | `Avoid special characters ... they can interfere with command line tools` |
| 用名词；`bool` 用动词前缀（`m_isPlayerDead`） | 布尔量"提一个能用 true/false 回答的问题" |
| **public 字段用 Pascal case，私有变量用 camel case** | 原文明确区分 |
| 想在不 public 的前提下让私有字段出现在 Inspector 里，用 `[SerializeField]` | `[SerializeField] bool m_isPlayerDead;` |
| 静态字段用 `s_` 前缀，常量用 `k_` 前缀 | `static int s_sharedCount;` / `const int k_maxCount = 100;` |
| 用数值范围限制 Inspector 输入时用 `[Range(min, max)]`，会生成滑条 | `[Range(0f, 1f)] [SerializeField] float m_rangedStat;` |
| 序列化字段的说明**用 `[Tooltip]` 而不是注释**，"do double duty" | `[Tooltip("...")] [SerializeField] float m_anotherStat;` |
| 用 `[Serializable]` 的结构体/类把一组字段在 Inspector 里成组显示 | `[Serializable] PlayerStats m_stats;` |

### 3. 属性（PROPERTIES）

| 条款 | 说明 |
| --- | --- |
| 属性**优于** public 字段（`Preferable to a public field`） | 但注意序列化限制，见"与 Unity 场景的差异" |
| Pascal case，无特殊字符 | — |
| 只读属性用 expression-bodied（`=>`），其余用 `{ get; set; }` | 原文建议："use expression-bodied for read-only properties but { get; set; } for everything else" |
| 没有 backing field 的 public 属性用 **auto-implemented property** | `public string DescriptionName { get; set; } = "Fireball";` |
| 暴露私有数据的首选是属性，其次才是方法 | `properties are generally recommended` |
| get/set 里含复杂逻辑或计算时，**改用方法** | `use methods instead of properties` |

### 4. 事件（EVENTS）

| 条款 | 说明 |
| --- | --- |
| 事件名用**动词短语** | `Name with a verb phrase` |
| **现在分词 = 之前，过去分词 = 之后** | `Present participle means "before" and past participle means "after."` |
| 大多数事件用 `System.Action`（支持 0–16 个参数） | `public event Action OpeningDoor;` |
| 只有必要时才定义自定义 `EventArgs`（`System.EventArgs` 或自定义 struct） | `public event Action<CustomEventArgs> ThingHappened;` |
| 或者统一用 `System.EventHandler`——**二选一，保持一致** | `choose one and apply consistently` |
| 为事件、事件处理方法（订阅方）、事件触发方法（发布方）各定一套命名方案 | 原文示例：event/action = `OpeningDoor`，触发方法 = `OnDoorOpened`，处理方法 = `MySubject_DoorOpened` |
| 触发方法（在 subject 上）用 `On` 前缀 | `Prefix the event raising method (in the subject) with "On"` |

### 5. 方法（METHODS）

| 条款 | 说明 |
| --- | --- |
| 用 **method** 而不是 function 这个词（C# 里方法必须在类内） | `method is the right term in Unity development` |
| 方法名以动词/动词短语开头，必要时补上下文 | `GetDirection`, `FindTarget` |
| 参数用 camel case，并按局部变量的格式书写 | `Use camel case for parameters` |
| 返回 `bool` 的方法要"提问"：动词前缀 | `IsGameOver`, `HasStartedTurn` |
| 避免过长的方法，拆小 | `Avoid long methods` |
| 参数**超过三个**就用类或结构体打包 | `more than three parameters, consider using a class or struct to group them` |
| 避免过度重载 | `Avoid excessive overloading` |
| 避免副作用：方法只做名字宣称的事 | `Avoid side effects` |
| **不要用 flag 参数让一个方法做多种模式**，拆成两个名字不同的方法 | `GetAngleInDegrees` / `GetAngleInRadians` |

### 6. 枚举（ENUMS / FLAG ENUM）

| 条款 | 说明 |
| --- | --- |
| 枚举名与枚举值都用 **Pascal case** | `public enum Direction { North, South, East, West, }` |
| 普通枚举名用**单数名词**（它代表一组可能值中的一个） | `Use a singular noun` |
| 枚举**不加前缀或后缀** | `They should have no prefix or suffix` |
| public 枚举可放在类外使其全局可用 | `You can place public enums outside of a class` |
| 位标志用 `[Flags]`，枚举名用**复数名词**（如 `AttackModes`） | `Use a plural noun to indicate the possibility of multiple selections` |
| 位标志**按二进制列对齐**书写，或用 `1 << bitnum` 风格 | 原文示例用等宽注释对齐 `000001` / `000010` |
| 标志枚举同样不加前缀/后缀 | — |

### 7. 接口（INTERFACES）

| 条款 | 说明 |
| --- | --- |
| 接口名以大写 **`I`** 开头 | `Prefix interface names with a capital I` |
| 其后用**形容词短语**描述能力 | `IDamageable`, `IPersistable` |
| 用途：无关的类需要共享同一套能力、但各自实现不同时使用接口 | `define a common contract` |
| 支持泛型接口 | `public interface IDamageable<T> { void Damage(T damageTaken); }` |

### 8. 类与结构（CLASSES or STRUCTS）

| 条款 | 说明 |
| --- | --- |
| Pascal case | — |
| 用**名词或名词短语**，以此与方法（动词短语）区分 | `Name them with nouns or noun phrases` |
| 不加前缀 | `Avoid prefixes` |
| **一个文件一个 MonoBehaviour**，文件名与类名一致 | `One Monobehaviour per file` |
| 辅助类/非 MonoBehaviour 类可尽量放在同一文件里 | `Define as many other helper/non-Monobehaviour classes in your file as needed` |
| `[Serializable]` 的 struct 常用于在 Inspector 里成组字段 | `public struct PlayerStats { public int MovementSpeed; ... }` |

### 9. using 与命名空间（USING LINES / NAMESPACES）

| 条款 | 说明 |
| --- | --- |
| `using` 行放在文件顶部 | `Keep using lines at the top of your file` |
| 删除未使用的 using，但保留"最小必需集" | `Strip unused 'usings' except the 'minimally-required set'` |
| 用命名空间避免与其它命名空间或全局命名空间冲突 | — |
| 命名空间 Pascal case，**不含**特殊符号或下划线 | `Use Pascal case, without special symbols or underscores` |
| 用点号创建子命名空间 | `MyApplication.GameFlow`, `MyApplication.AI` |
| 建议命名空间反映项目文件夹结构，便于逻辑分组 | `reflect the folder structure of the project` |

### 10. 格式与括号（FORMATTING / BRACES）

| 条款 | 说明 |
| --- | --- |
| **K&R**（左括号同行）或 **Allman**（左括号新行）**二选一**并统一 | `Choose K&R ... or Allman ... style braces` |
| 保持行短；可在风格指南里定最大行宽（**建议小于 120 字符**） | `some prefer less than 120 characters` |
| 长行拆成多条语句，而不是让它溢出 | `Break a long line into smaller statements` |
| 流程控制条件前**一个空格**：`while (x == y)` | `Use a single space before flow control conditions` |
| 方括号内**不加空格**：`x = dataArray[index]` | `Avoid spaces inside brackets` |
| 比较运算符两侧各一个空格 | — |
| **不要省略大括号**，即使单行语句也保留；或干脆避免单行语句（可调试性更好） | `Where possible, don't omit braces, even for single-line statements` |
| 嵌套多行语句必须保留大括号 | `Keep braces in nested multi-line statements` |

### 11. 空格（SPACING）

| 条款 | 说明 |
| --- | --- |
| 函数实参逗号后**一个空格**：`CollectItem(myObject, 0, 1);` | — |
| 圆括号与实参之间**不加空格** | — |
| 函数名与圆括号之间**不加空格**：`DropPowerUp(myPrefab, 0, 1);` | 原文重复强调两次 |
| 用**垂直留白**（空行）做视觉分隔 | `Use vertical spacing (extra blank line)` |
| 多数情况**一行一个变量声明**，牺牲紧凑换可读性 | `Use one variable declaration per line in most cases` |

### 12. 注释（COMMENTS）

| 条款 | 说明 |
| --- | --- |
| 只在代码不自解释、好命名不足以表达意图时才注释 | `Comment when needed` |
| 需要注释解释一大团绕的逻辑时，先考虑**重构** | `consider restructuring your code` |
| 先考虑改名再考虑注释 | `Consider renaming before commenting` |
| 注释回答 **"why"**，而不是 "what" / "how" | `Documenting the 'why' is far more important` |
| 就近用 `//` 行注释贴着逻辑写 | `Use the // comment to keep the explanation next to the logic` |
| **序列化字段用 `[Tooltip]` 代替注释** | `Use a Tooltip instead of a comment for serialized fields` |
| **不要用 `#region`**：视为 code smell，说明类太大该拆 | `Rather than using Regions think of them as a code smell` |
| 法律/许可信息用**外部链接**，别把全文塞进代码 | `Use a link to an external reference for legal information or licensing` |
| public 方法前用 **XML `<summary>`** 标签（供 IntelliSense/文档输出） | `Use a summary XML tag in front of public methods` |
| **不要写署名注释**（`// Created by`、`// Modified by`），用版本控制记录 | `Avoid attributions ... Use version control` |

### 13. 类的成员组织顺序（CLASS ORGANIZATION）

原文规定的顺序（自上而下）：

1. **Fields**
2. **Properties**
3. **Events**
4. **MonoBehaviour 方法**（`Awake`, `Start`, `OnEnable`, `OnDisable`, `OnDestroy`, etc.）
5. **public 方法**
6. **private 方法**
7. **其它类（other Classes）**

并强调 `#region` 一般不鼓励使用，因为它会隐藏复杂度、降低可读性。

### 14. `var`、`switch`（VAR / SWITCH STATEMENTS）

| 条款 | 说明 |
| --- | --- |
| 类型从上下文一看就清楚时**可以**用 `var` | `you can use var when the type is clear from the context` |
| `var` 让重构更简单（类型变化时需改动的地方更少） | `Refactoring is simpler with var` |
| `foreach` 里用 `var` 可保证迭代变量类型与枚举器一致，避免显式写错类型导致的运行期错误 | `var ensures that the iteration variable matches the type provided by the enumerator` |
| 较长的 `if-else` 链应换 `switch`，可读性更好 | `replace longer if-else chains with a switch statement` |
| `switch` 的格式（`case` 缩进、`break` 位置）自选一种并固定 | 原文示例把 `case` 与 `break` 一起缩进 |

## 与 Unity 场景的差异

本节记录"这份 Unity 指南与纯 .NET / Framework Design Guidelines 的冲突点"，以及落地到 Unity 项目时必须改写的地方。**A 文件本身已是 Unity 向的指南**，所以这里的"差异"主要是它与微软 .NET 准则的不一致，以及 Unity 序列化机制带来的硬约束。

| 议题 | .NET / FDG 的做法 | Unity 里必须怎么改 |
| --- | --- | --- |
| **public 实例字段** | FDG 明令"Do not provide public or protected instance fields"，一律用属性封装 | Unity 的 Inspector 序列化**只认字段，不认属性**。要让策划/美术在 Inspector 里调参，必须 `public` 字段或 `[SerializeField] private` 字段。本仓库的取舍是：**能用 `[SerializeField]` 就用它**（保持封装），必须公开时才 public，且 public 字段用 PascalCase |
| **字段前缀** | FDG："Do not use prefixes on field names"，明确点名不要 `g_` / `s_` | 本指南**允许并推荐** `m_` / `s_` / `k_`，也允许 `_`。这是两侧最直接的对立；采用前缀就必须在团队内统一，且不要混用三种流派 |
| **访问修饰符** | FDG 无强制写法 | 本指南要求"全写或全省"，但自身在 `private` 该不该写上前后矛盾，需团队裁决 |
| **文件级命名空间**（`namespace X;`） | 现代 .NET 文档推荐 file-scoped namespace | Unity 长期停留在 **C# 9**，文件级命名空间（C# 10 特性）通常不可用，仍需块状 `namespace X { ... }` |
| **`readonly` / `const` 字段** | 鼓励 `readonly`、`const` 表达不可变 | Unity **不序列化** `readonly` 与 `const` 字段，Inspector 里看不到也改不了。想"只读但可调参"要用 `[SerializeField] private` + 只读属性暴露 |
| **`Init`/`required` 属性初始化** | 现代 C# 推荐 `required` 成员、对象初始化器 | Unity 的序列化流程与 `required` 不兼容（Inspector 赋值不经过构造函数），MonoBehaviour 上不要用 |
| **事件模型** | FDG：用 `EventHandler<TEventArgs>`，`sender` + `e` 两参数，受保护虚方法 `OnXxx` 触发 | 本指南更偏好 `System.Action` / `Action<T>`（参数 0–16），因为 Unity 的 `MonoBehaviour` 生命周期与序列化对 `EventHandler` 并不友好；`sender` 惯例在 Unity 里经常省略。两者**只能选一套** |
| **事件参数类** | FDG：必须继承 `EventArgs`，命名 `XxxEventArgs` | 本指南允许用自定义 `struct`（`CustomEventArgs`）以避免堆分配，这在每帧触发的游戏事件里很实际；但需注意 struct 装箱与可空性 |
| **属性 vs 方法** | FDG：getter 不应抛异常、不应有前置条件 | Unity 一致；但 Unity 里 `GetComponent` 一类开销大的操作**绝不**放进 getter，因为属性看起来"便宜" |
| **每文件一个类型** | .NET 通常一个文件一个 public 类型 | 本指南明确**允许多个**辅助类同文件，但**MonoBehaviour 必须一文件一个且与文件名同名**（这是 Unity 的硬要求：否则 Inspector 里挂不上脚本） |
| **`#region`** | .NET 常见用法 | 本指南明确视为 code smell，Unity 项目里应改用拆分 partial class / 拆类 |
| **注释里的 `[Tooltip]`** | .NET 没有这个概念 | Unity 特有：能进 Inspector 的说明应写 `[Tooltip]`，不要再写 `//` 注释重复一遍 |
| **行宽 120 字符** | 微软 samples 甚至要求 65 字符（为移动端阅读） | Unity 里 120 是常见上限；65 对 C# 业务代码太窄，不要照搬微软那一条 |

## 相关文档

- [`../README.md`](../README.md) —— 本项目代码规范总览与阅读顺序
- [`../03-naming.md`](../03-naming.md) —— 本项目命名约定（字段/属性/事件/序列化的内部裁决）
- [`../../INDEX.md`](../../INDEX.md) —— 文档总索引
- 同目录参考：[`dotnet-design-guidelines.md`](dotnet-design-guidelines.md)、[`csharp-coding-conventions.md`](csharp-coding-conventions.md)
