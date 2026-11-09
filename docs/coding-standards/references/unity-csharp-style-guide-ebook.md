# Unity 官方电子书《使用 C# 风格指南编写简洁且可扩展的游戏代码》（Unity 6 版）

> 目的：把 Unity 官方这本 64 页 C# 风格指南电子书的内容落成中文可检索文本，供本站在定自己的编码规范时对照引用。
> 适用读者：要在本工程里写或评审 C# 代码的人；要给团队定编码规范的人；做代码评审 checklist 的人。
> 前置条件：无（本文为外部资料整理）
> 最后更新：2026-11-02

## 这是什么

Unity 官方电子书 *Use a C# style guide for clean and scalable game code*（Unity 6 edition）的整理稿。版权页标注 © 2025 Unity Technologies，正文 64 页。书中注明这是该电子书的**第二版**，为 Unity 6 更新（E1，p.5："This is the second edition of this e-book, now updated for Unity 6."）；第一版的英文书名据配套仓库 README 为 *Create a C# style guide: Write cleaner code that scales*（E2）。

它不讲 Unity API，讲的是**团队层面的 C# 书写约定**：命名、格式化、类的组织、方法的粒度、注释、UI Toolkit 的 USS 命名，外加两个附录（脚本模板、测试与调试）。全书口径明确：这些是**建议（recommendations）而非硬规则**，且是 Microsoft Framework Design Guidelines 的子集；当本书与团队自己的规范冲突时，**以团队规范为准**（E1，p.9–10）。

一句话定位：它不是"Unity 代码必须这样写"的标准，而是一份可以直接抄、再按团队口味改的**样板规范 + 理由**。

## 来源与可访问性

抓取日期：**2026-11-02**。全部通过 `web_fetch` 完成（沙箱内 `curl` / `Invoke-WebRequest` 直连外网失败，未使用）。

### 尝试顺序与结果

| 顺序 | URL | 结果 | 说明 |
| --- | --- | --- | --- |
| 1 | `https://unity.com/cn/resources/c-sharp-style-guide-unity-6` | HTTP 200，**只有站点导航** | 页面 `<title>` 是「C# 代码风格指南（Unity 6 版）」，说明中文版存在；正文与下载表单未渲染，属需留资/JS 渲染类页面 |
| 2 | `https://unity.com/resources/c-sharp-style-guide-unity-6` | HTTP 200，**只有站点导航** | `<title>` 为「C# Code Style Guide (Unity 6 edition)」；同样取不到正文 |
| 3 | `https://unity.com/resources/c-sharp-style-guide-unity-6.pdf` | **HTTP 404** | 按常见形式猜的 PDF 直链不存在 |
| 4 | `https://resources.unity.com/games/create-code-style-guide-e-book` | **失败** | 跨域 301 跳回 `https://unity.com`，旧入口已无落地页 |
| 5 | `https://raw.githubusercontent.com/thomasjacobsen-unity/Unity-Code-Style-Guide/master/README.md` | HTTP 200，**正文可读** | 配套示例规范仓库的说明页，作者即本书贡献者 Thomas Krogh-Jacobsen；可用于核实成书背景 |
| 6 | `https://github.com/pieiie/unity_book`（仓库页） | HTTP 200，只有 GitHub 导航 | 仓库页面本身抓不到文件列表；但其 raw 文件可直读 |
| 7 | 见下方 E1（raw Markdown 全文） | HTTP 200，**全文可读** | 官方 PDF 的逐页文本转录，保留 `Page 1`–`Page 65` 页边界，本次整理的**主要依据** |
| 8 | `https://developer.unity.cn/projects/692daa3dedbc2a001d18599a` | HTTP 200，**只有标题** | 仅取到「Unity 电子书｜C# 代码编写规范 - 技术专栏 - Unity官方开发者社区」，正文由 JS 渲染，未取得 |
| 9 | `https://unity.com/how-to/naming-and-code-style-tips-c-scripting-unity` | HTTP 200，**只有站点导航** | 电子书里点名的两篇同主题文章之一，正文同样抓不到 |

### 出处编号

- **E1** — [官方 PDF 全文转录（pieiie/unity_book，raw Markdown）](https://raw.githubusercontent.com/pieiie/unity_book/5df653e2f301a0672e2dd0012a9371ada4859da0/Use_a_C__style_guide_for_clean_and_scalable_game_code_Unity_6_edition_e-book.md)
- **E2** — [配套示例规范仓库 README](https://raw.githubusercontent.com/thomasjacobsen-unity/Unity-Code-Style-Guide/master/README.md)
- **E3** — [中文资源页](https://unity.com/cn/resources/c-sharp-style-guide-unity-6)
- **E4** — [英文资源页](https://unity.com/resources/c-sharp-style-guide-unity-6)
- **E5** — [developer.unity.cn 中文整理稿](https://developer.unity.cn/projects/692daa3dedbc2a001d18599a)

### 关于这份全文的可靠性

1. **拿到全文了**，不是靠目录推测。全书 64 页正文 + 封底，各章建议与示例代码都在 E1 里。
2. E1 是**第三方对官方 PDF 的文本转录**，不是 Unity 自己发布的网页，存在 OCR 讹误：例如 `public SetMaxHealth(int newMaxValue)` 缺返回类型、`_currentHealth` 与 `m_currentHealth` 混用、`compontent` 之类拼写错误。本文引用的代码做了最小修正，**条款含义未改**。
3. 页码引用（如 p.14）指 E1 中保留的 PDF 页边界，可直接与官方 PDF 对页。
4. 若要逐字引用或对外发布，请自行到 E3/E4 留资下载官方 PDF 复核。

## 内容结构

### 全书章节骨架

| 章节 | 起始页 | 讲了什么 |
| --- | --- | --- |
| Introduction | p.4 | 为什么要干净代码；干净代码的四条落地要求；声明本书是第二版、随 Unity 6 更新 |
| Contributors | p.5 | 作者 Wilmer Lin（独立开发者/教育者）；贡献者 Thomas Krogh-Jacobsen、Peter Andreasen、Scott Bilas、Mihai Popescu |
| Developing as a team | p.6 | 与具体风格无关的团队准则：KISS、YAGNI、不要绕过问题、增量改进、够好即提交、计划但要应变、保持一致、人人有责 |
| Follow a style guide | p.9 | 规范的定位：MSFT 指南的子集、建议而非规则、团队规范优先、单人开发也该用；点名 Microsoft C# Coding Conventions 与 Google C# Style Guide 为行业基线 |
| Naming conventions | p.11 | 标识符、四种命名法（camel/Pascal/snake/kebab）与匈牙利命名法、字段与变量、枚举、类与接口、方法、事件与事件处理器、命名空间 |
| Formatting | p.23 | 属性、序列化字段、大括号与缩进风格、EditorConfig、水平间距、垂直间距、`#region`、在 Visual Studio 里落地格式化 |
| Classes | p.38 | 类要小、"报纸隐喻"（倒金字塔）、类成员分区顺序、单一职责原则、Pong 的 Paddle 重构示例 |
| Methods | p.43 | 方法要小且单一职责、少参数、避免过度重载、避免副作用、别用 flag 参数分模式、method vs function、扩展方法、DRY 原则 |
| Comments | p.48 | 注释解释 why 不解释 what；十条左右的 do / don't（含 `[Tooltip]`、`/// <summary>`、删掉注释掉的代码、清理 TODO） |
| UI Toolkit naming conventions | p.51 | 把 BEM 用于 UXML/USS：`block-name__element-name--modifier-name`，以及六条命名细则 |
| Common pitfalls | p.54 | 七种 code smell：命名晦涩、无谓复杂、僵化、脆弱、不可移植、重复代码、注释过多 |
| Conclusion | p.56 | 这些是习惯不是规则；抄这份表再改；配套推荐另一本电子书 *Level up your code with design patterns and SOLID* |
| References | p.57 | MSFT 指南 + 四本经典书（Clean Code、The Pragmatic Programmer、Refactoring、TDD）与 ISBN |
| Appendix: Script templates | p.58 | Unity 脚本模板文件的位置、命名规则、`#SCRIPTNAME#` / `#NOTRIM#` 占位符、按项目定制 |
| Appendix: Testing and debugging | p.62 | TDD 六步（Kent Beck）、Unity Test Framework（基于 NUnit）的工作流 |

### 一句话读法

- 只想抄规则：看 `Naming conventions` + `Formatting` 两章（p.11–37），约占全书一半篇幅。
- 想理解"为什么"：看 `Developing as a team`（p.6–8）和每章开头的引语。
- 想改代码结构而不只是改格式：看 `Classes` + `Methods`（p.38–47）。
- 想连工具链一起统一：看 `EditorConfig`（p.31）、`Code formatting in Visual Studio`（p.34）、`Appendix: Script templates`（p.58）。

## 可核实的关键主张

以下每条都来自实际抓到的文本。`E1，p.N` 为出处与页号；`E2` 为配套仓库 README。E1 是唯一的条款来源，故未逐条重复长 URL。

### 0. 全书定位（E1，p.9–10；E2）

- 本书聚焦 Unity 开发中最常见的约定，**基本上是 Microsoft Framework Design Guidelines 的子集**，后者规则远多于本书。
- **这些是建议，不是硬规则**；按团队偏好裁剪，"一致性比单条规则的具体取值更重要"。
- 与本书或 MSFT 指南冲突时，**团队自己的规范优先**。
- 单人开发也该用规范：前期像约束，后期省下重学成本；目标是"不管多少人参与，读起来像一个作者写的"。
- Unity 官方**不**在其入门教程/文档/示例项目里强制统一风格，理由是严格规范会挡住初学者；本书走的是"务实优先于严格"的路线，接近 Unity 工程与文档团队自己的用法。
- E2 独立佐证：配套示例规范由 Unity 内部与外部专家实践汇总，Wilmer（Unity Game Academy）贡献显著；作者自述"风格指南高度主观，放在我个人 GitHub 上，法律意义上仅作灵感示例"。

### 1. 命名（E1，p.11–22）

| 主题 | 主张 |
| --- | --- |
| 标识符 | 避免反斜杠、符号、Unicode 字符，**即使 C# 允许**——可能干扰某些 Unity 命令行工具，也影响跨平台兼容（p.11） |
| camelCase | 局部变量、方法参数（p.12） |
| PascalCase | 类、公共字段、方法名（p.12） |
| snake_case / kebab-case | 书中只作术语介绍；kebab 明确推荐给 UI Toolkit 的 USS（p.12、p.51） |
| 匈牙利命名法 | 老派做法，**Unity 开发中不常见**（p.13） |
| 变量用名词 | 变量表示"事物/状态"；`bool` 例外（p.13） |
| bool 加动词前缀 | 让名字成为问句：`isDead`、`isWalking`、`hasDamageMultiplier`（p.13） |
| 不要缩写 | 单字母只留给循环与数学表达式；`HorizontalAlignment` 优于 `AlignmentHorizontal`（p.13） |
| 可见性前缀 | 私有成员 `m_`、常量 `k_`、静态 `s_`；`movementSpeed` → `m_movementSpeed`。也可改用 `this` 消歧而不加前缀；公共字段与属性不加前缀（p.14） |
| 访问修饰符 | 建议**省略隐式的 `private`**（与 MSFT"显式写出"相反），前提是团队认为不影响可读性；关键是**做法一致**（p.15） |
| 常量 | `k_` 前缀 + PascalCase，如 `public const int k_MaxItems = 100;`（p.15） |
| 声明密度 | 一行一个变量声明（p.16） |
| 冗余名 | `Player` 类里不要 `PlayerScore`/`PlayerTarget`，直接 `Score`/`Target`（p.16） |
| 冗余初始化 | 去掉 `= 0`、`= null`（p.16） |
| 幽默命名 | 不要；`infiniteMonkeys`、`dudeWheresMyChar` 既不耐读，也违背"命名暴露上下文"（p.16） |
| `var` | 类型从上下文显而易见时可用；`PowerUpManager.GetPowerUps()` 这种返回值不明的场景应避免（p.16） |
| 枚举 | PascalCase、**单数名词**、无前后缀；`[Flags]` 位枚举是例外，用复数（p.17） |
| 类 | PascalCase 名词/名词短语（p.18） |
| MonoBehaviour 文件 | **文件名必须与 MonoBehaviour 类名一致**，一个文件只放一个 MonoBehaviour（p.18、p.39） |
| 接口 | 大写的 `I` + 描述能力的形容词：`IKillable`、`IDamageable<T>`（p.18） |
| 方法 | 动词/动词短语开头：`GetDirection`、`FindTarget`；参数用 camelCase；返回 bool 的用问句：`IsGameOver`、`HasStartedTurn`（p.19） |
| 事件名 | 动词短语，用现在分词/过去分词区分"发生前/发生后"：`OpeningDoor` vs `DoorOpened`（p.19） |
| 事件委托 | 多数情况用 `System.Action` / `Action<T>`（0–16 个入参、返回 void），可省掉自定义 delegate（p.20） |
| 触发方法 | 主题方以 `On` 前缀：`OnDoorOpened()`（p.20） |
| 订阅方处理方法 | 可选 `主题名_事件名`，如 `GameEvents_DoorOpened`（p.20） |
| EventArgs | 仅在需要传自定义数据时新建；示例用只读 struct（p.21） |
| 命名空间 | PascalCase、无下划线与特殊符号；`using` 放文件顶部；用点分层 `MyApplication.GameFlow` / `MyApplication.AI` / `MyApplication.UI`；也可镜像文件夹结构（p.21） |

原文给出的"避免 → 改用"对照（E1，p.14）：

| 避免 | 改用 | 原因 |
| --- | --- | --- |
| `int d` | `int elapsedTimeInDays` | 单字母缩写只留给计数器/表达式；测量单位要写清 |
| `int hp`、`string tName`、`int mvmtSpeed` | `int healthPoints`、`string teamName`、`int movementSpeed` | 名字要暴露意图、可搜索、可朗读 |
| `int getMovementSpeed` | `int movementSpeed` | 名词留给变量，动词留给方法（bool 例外） |
| `bool dead` | `bool isDead` / `bool isPlayerDead` | bool 应是一个能被 true/false 回答的问题 |

### 2. 格式化（E1，p.23–37）

**属性（p.24–25）**

- 单行只读属性用表达式体：`public int MaxHealth => m_maxHealth;`
- 其余用 `{ get; set; }` 语法（自动实现属性）；不需要写权限就把 setter 设为 `private`。
- 简单的 get/set 用属性，涉及复杂逻辑或计算的方法更合适。

```csharp
public class PlayerHealth
{
    private int m_maxHealth;

    // 只读，返回 backing field
    public int MaxHealth => m_maxHealth;

    // 显式 getter / setter
    public int Health
    {
        get => m_maxHealth;
        set => m_maxHealth = value;
    }
}
```

**序列化字段（p.26）**

- 不能序列化 `static`、`const`、`readonly` 字段；必须是 `public` 或标了 `[SerializeField]`。
- 优先 `[SerializeField] private`，而不是把字段设成 `public`——封装更好。
- 数值字段用 `[Range(min, max)]`，Inspector 里会变成滑杆。
- 用 `[Serializable]` 的 class / struct 分组数据，Inspector 里会变成可折叠区块。

```csharp
[Serializable]
public struct PlayerStats
{
    public int MovementSpeed;
    public int HitPoints;
    public bool HasHealthPotion;
}

[SerializeField]
private PlayerStats m_stats;
```

**大括号与缩进（p.27–31）**

- 两种主流风格：**Allman/BSD**（左大括号另起一行）与 **K&R**（左大括号留在上一行行尾）。本书示例用 **Allman**（来自 MSFT 指南）。
- 统一缩进，通常 2 或 4 个空格；别为 tab/space 开战，VS 可把 tab 转空格。
- **尽量不要省略大括号**，单行语句也一样；嵌套多行语句**不要**省大括号。
- `switch` 建议缩进 `case`，并且**保留 `default`**——即使所有取值都覆盖了，也留着应对意外值。

**EditorConfig（p.31、p.37）**

- VS、Rider 原生支持，不需要插件；随代码进版本控制，**优先级高于 VS 全局文本编辑器设置**；没有 `.editorconfig` 时才回落到个人设置。

```ini
root = true

[*]
end_of_line = lf
insert_final_newline = true

[*.cs]
indent_style = space
indent_size = 4
charset = utf-8
trim_trailing_whitespace = true
```

**水平间距（p.32–33）**

| 规则 | 正确 | 避免 |
| --- | --- | --- |
| 加空格降低密度 | `for (int i = 0; i < 100; i++) { DoSomething(i); }` | `for(inti=0;i<100;i++){DoSomething(i);}` |
| 参数逗号后一个空格 | `CollectItem(myObject, 0, 1);` | `CollectItem(myObject,0,1);` |
| 括号内不加空格 | `DropPowerUp(myPrefab, 0, 1);` | `DropPowerUp( myPrefab, 0, 1 );` |
| 函数名与括号之间不加空格 | `DoSomething()` | `DoSomething ()` |
| 方括号内不加空格 | `x = dataArray[index];` | `x = dataArray[ index ];` |
| 流程控制/比较运算符两侧加空格 | `while (x == y)`、`if (x == y)` | `while(x==y)`、`if (x==y)` |
| 控制行长 | 标准行宽 **80–120 字符**，超长就拆语句 | — |
| 不做列对齐 | `public float Speed = 12f;` | 用空格把类型/名字对齐成列（位运算、字段多的 struct 例外） |

**垂直间距与 `#region`（p.34）**

- 相关方法放相邻位置，读逻辑不用来回跳。
- 可在"变量声明与方法之间""类与接口之间""if-else 块之间"留两个空行，但要克制，并写进规范。
- `#region`：如果按本书的"类要小"来做，region 就是多余的；**很多开发者视其为 code smell / 反模式**，团队自己定立场。

**在 IDE 里落地（p.34–37）**

| 操作 | Visual Studio (Windows) | Visual Studio for Mac |
| --- | --- | --- |
| 配置规则 | Tools > Options > Text Editor > C# > Code Style Formatting | Preferences > Source Code > Code Formatting > C# source code |
| 格式化整个文档 | Edit > Advanced > Format Document（`Ctrl+K, Ctrl+D`） | Edit > Format Document（`Ctrl+I`） |
| 只整理空白/对齐 | Run Code Cleanup（`Ctrl+K, Ctrl+E`） | — |
| 团队共享设置 | Tools > Import and Export Settings 导出/导入 | — |

书中提醒：与其导入导出 VS 设置，不如用 `.editorconfig`，跨 IDE 更好分享且能进版本控制。

### 3. 类（E1，p.38–42）

- 引用 Clean Code：类的第一条规则是**小**，第二条是**比那还小**。
- **报纸隐喻 / 倒金字塔**：先标题与导语，再细节。代码自上而下分层——高层方法（如 `ThrowBall`）在前，被它调用的支撑方法（`SetInitialVelocity`、`CalculateTrajectory`）在后。
- **类成员分区顺序**：
  1. Fields
  2. Properties
  3. Events / Delegates
  4. MonoBehaviour 方法（`Awake`、`Start`、`OnEnable`、`OnDisable`、`OnDestroy` 等）
  5. Public Methods
  6. Private Methods
- **单一职责**示例（Pong 的 Paddle）：把"存速度数据 / 读键盘 / 移动 / 撞球播声音"塞进一个类，等于把几件简单事缠在一起，**反而违背 KISS**。拆成 `PaddleData`（或 ScriptableObject）、`PaddleInput`、`PaddleMovement`、`PaddleAudio`、`PaddleLogic`，`Paddle` 只负责把它们串起来。
- 明确提醒：**干净代码不等于更短**。拆分后总行数可能变多，但每个类更好读，调试和加功能时改动位置更清楚。

### 4. 方法（E1，p.43–47）

- 方法也要小、单一职责：**只做一个动作，或只回答一个问题，不要两者都做**。
- 好名字说明行为：`GetDistanceToTarget`；项目里有多种单位时 `GetDistanceToTargetInMeters` 更好。
- 少传参；**避免过度重载**——若确实重载，保证各签名的参数个数不同，避免歧义。
- **避免副作用**：方法只做名字宣称的事，尽量按值传参；用 `out`/`ref` 回传结果时，要确保那正是该方法唯一的目的。
- **不要用 flag 参数分两种模式**，改成两个方法：不要 `GetAngle(unitFlag)`，要 `GetAngleInDegrees()` 和 `GetAngleInRadians()`。
- 术语：C# 里叫 **method** 不叫 function（方法必须定义在类/对象上下文里）。
- **扩展方法**：静态类 + 第一个参数前加 `this`；按被扩展类型归类到 `TransformExtensions`、`Vector3Extensions` 等静态类里，不用为此新增 MonoBehaviour。

```csharp
public static class TransformExtensions
{
    public static void ResetTransformation(this Transform transform)
    {
        transform.position = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }
}
```

- **DRY**（Don't Repeat Yourself，出自 The Pragmatic Programmer）vs **WET**（"we enjoy typing" / "write everything twice"）：`PlayExplosionA` 与 `PlayExplosionB` 是复制粘贴，应提取成 `PlayFXWithSound(ParticleSystem particle, AudioClip clip, Vector3 hitPosition)`，以后加更多粒子/音效直接复用。
- 关键限定：**可以复制代码，不要复制逻辑**。DRY 针对的是重复的*逻辑*。（p.47）

### 5. 注释（E1，p.48–50）

- 注释解释 **why 而不是 what**；好命名 + KISS 拆分之后，大部分代码不需要注释。
- 不要用注释掩盖坏代码——先把逻辑理顺，注释自然消失。
- 位置：尽量单独一行，不写在代码行尾；多数情况用 `//`，靠近被解释的代码，而不是文件头堆一大段。
- 序列化字段要说明就用 `[Tooltip("...")]`，一举两得，别另写注释。
- 公共方法可用 XML `/// <summary>`，VS 会给 IntelliSense。
- 格式：`//` 与正文之间一个空格；每条注释风格统一（首字母大写、句末句号，团队自定）。
- 法律/版权声明可以放注释，但**只放链接**，别把整篇法务文本塞进代码。
- 禁止：星号横幅等装饰性区块、注释掉的死代码（交给版本控制）、过期的 TODO（可加名字和日期；五年前的 TODO 按 YAGNI 直接删）、开发日记式的流水账、`// added by devA` 这类署名。

### 6. UI Toolkit 命名（E1，p.51–53）

- 推荐 **BEM**（Block Element Modifier）：`block-name__element-name--modifier-name`，例：`navbar-menu__shop-button--small`。
- 各部分之间用**双下划线 `__`** 或**双连字符 `--`** 连接；每段只含拉丁字母、数字、连字符（kebab）。
- block＝一个独立有意义的 UI 组件；通用按钮可省 block，直接 `button--small`。
- element＝块的子部件，语义上依附于块；构造函数里若创建子元素，也要给子元素设类，如 `my-block__first-child`。
- modifier＝变体或状态（按下、选中、小号）。
- 更多例子：`menu__home-button`、`menu__shop-button`、`navbar-menu__shop-button--small`。
- 六条细则：名字短而明确；**不要**在选择器里用类型名（`Button`、`Label`）或 `#id`；用语义不用外观（`button--quit` 而不是 `button--red`）；把同一套命名延伸到 sprites/textures 等美术资源；跨项目复用时加前缀/命名空间防冲突；在构造函数里用 `AddToClassList()` 挂 USS 类。

### 7. 常见陷阱 / code smell（E1，p.54–55）

| 症状 | 含义与处置 |
| --- | --- |
| Enigmatic naming（命名晦涩） | 类/方法/变量名要直白 |
| Needless complexity（无谓复杂） | 过度预判需求 → God object、超长方法、巨型类；拆成职责单一的小块 |
| Inflexibility（僵化） | 改一处被迫改多处 → 多半破坏了单一职责 |
| Fragility（脆弱） | 小改动就全盘崩，通常说明耦合有问题 |
| Immobility（不可移植） | 逻辑想复用却带一堆依赖 → 解耦 |
| Duplicate code（重复代码） | 一眼看出是复制粘贴 → 提取核心逻辑（见 DRY） |
| Excessive commentary（注释过多） | 逐行解说通常意味着命名/拆分没做好 |

### 8. 附录：脚本模板（E1，p.58–61）

- 模板目录：
  - Windows：`C:\Program Files\Unity\Hub\Editor\[UnityVersion]\Editor\Data\Resources\ScriptTemplates`
  - macOS：`/Applications/Unity/Hub/Editor/[UnityVersion]/Unity.app/Contents/Resources/ScriptTemplates`
- 默认模板文件：`1-Scripting__MonoBehaviour Script-NewMonoBehaviourScript.cs.txt`、`2-Scripting__ScriptableObject Script-NewScriptableObjectScript.cs.txt`。
- 占位符：`#SCRIPTNAME#` 为脚本名；`#NOTRIM#` 保证大括号之间留出空行。
- 文件名模式：`PriorityNumber–MenuPath–DefaultName.FileExtension.txt`
  - `PriorityNumber`：在 Create 菜单里的排序，数字越小越靠前
  - `MenuPath`：`__` 表示子菜单层级，如 `CustomScript__Misc__ScriptableObject` → Create > CustomScript > Misc
  - `DefaultName`：未指定名字时的默认名
  - `FileExtension`：资产扩展名，末尾还要再加 `.txt`
- 只影响本项目的做法：把整个 `ScriptTemplates` 目录拷到 `Assets/ScriptTemplates`；改程序安装目录会**影响该 Unity 版本的所有项目**，需谨慎。

### 9. 附录：测试与调试（E1，p.62–64）

- TDD 六步（引自 Kent Beck, *Test-Driven Development by Example*）：
  1. 加**一个**单元测试，描述要加的新功能
  2. 跑测试，**它应该失败**（顺便验证测试本身有效）
  3. 写**刚好能通过**的最简代码（此时可以不干净，允许硬编码）
  4. 确认**全部**测试通过；不通过只改新代码
  5. 重构：按规范清理、调整顺序、去重、重命名、拆分过长的方法/类；每次重构后重跑测试
  6. 重复；频繁提交，出问题就回滚到上一个 commit
- Unity Test Framework（UTF，原 Unity Test Runner）：基于 **NUnit**，可在 Editor 的 Edit Mode / Play Mode 及目标平台（Standalone、Android、iOS）跑测试；通过 Package Manager 安装。
- 工作流：建 Test Assembly（Test Runner UI 会生成目录）→ Assets > Create > Testing > C# Test Script 写用例 → 用 Test Runner 跑全部或单个（Rider 里可直接跑）→ 需要运行时测试就另建 Play Mode assembly，并在 standalone build 中配置（结果显示回 Editor）。

### 10. 参考书单（E1，p.57）

| 书 | 版本/年 | ISBN |
| --- | --- | --- |
| Clean Code: A Handbook of Agile Software Craftsmanship（Robert C. Martin） | 2008, Prentice Hall | 978-0132350884 |
| The Pragmatic Programmer（David Thomas & Andrew Hunt） | 20th Anniversary, 2019, Addison Wesley | 978-0135957059 |
| Refactoring: Improving the Design of Existing Code（Martin Fowler） | 2nd, 2018, Addison-Wesley | 978-0-134-75759-9 |
| Test-Driven Development（Kent Beck） | 1st, 2002, Addison-Wesley | 978-0321146533 |

## 与本站整理稿的关系

本站另有一篇中文整理稿：《Unity 电子书｜C# 代码编写规范》，地址 `https://developer.unity.cn/projects/692daa3dedbc2a001d18599a`（Unity 官方开发者社区「技术专栏」）。两者的关系判断如下。

**同一本书，两个入口。** 这本书的中文名在不同地方不同：

| 场合 | 名称 |
| --- | --- |
| 任务/俗称（直译书名） | 使用 C# 风格指南编写简洁且可扩展的游戏代码 |
| Unity 中文资源页标题（E3，已核实） | C# 代码风格指南（Unity 6 版） |
| Unity 英文资源页标题（E4，已核实） | C# Code Style Guide (Unity 6 edition) |
| developer.unity.cn 文章标题（E5，已核实） | Unity 电子书｜C# 代码编写规范 |

**必须说清的一点：E5 的正文我没有拿到。** 该页 web_fetch 只返回标题，正文由 JS 渲染。因此：

- 能确认的是**标题层面的对应**——"Unity 电子书 + C# 代码编写规范"指向的就是这本电子书，属于社区侧的中文整理/翻译稿。
- **不能确认**的是它与官方 PDF 的逐条一致性、是否完整、是否有增删或本土化改写、是否带示例代码。本文不对 E5 的具体条款作任何断言。
- 因此本文件与 E5 不是互相替代关系：**条款以官方 PDF（本文件 E1 转录）为准**，E5 只能当中文对照读物；两者若有出入，以官方原文为准。

**给本站的用法建议**

1. 定规范时的基线用三件套：本文件（条款摘要）+ E1（原文全文，可查页）+ E2 配套示例仓库（可直接抄的示例规范文件）。
2. 本书明确说"团队规范优先于本书"，所以本站规范应当**显式覆盖**本文条目，而不是默默沿用——尤其是本书自己也承认有分歧的几处：`private` 要不要显式写、`#region` 用不用、是否加 `m_`/`k_`/`s_` 前缀。
3. 本书的表格与示例适合直接转成本站代码评审 checklist；`Common pitfalls`（p.54）那七条可以原样当 smell 清单用。

## 相关文档

- [编码规范目录](../README.md)
- [文档索引](../../INDEX.md)
