# 常见 C# 代码约定（Microsoft .NET 文档团队 coding-conventions）整理

> 目的：把微软 .NET 文档团队自用的 C# 代码约定逐条整理成中文清单，作为本项目日常写 C# 的对照表。
> 适用读者：本项目所有写 C# / Unity C# 的开发者，尤其是需要评审别人代码的人。
> 前置条件：无（本文为外部资料整理）
> 最后更新：2026-11-02

## 这是什么

这是 Microsoft Learn 上 `Common C# code conventions` 一文（路径 `dotnet/csharp/fundamentals/coding-style/coding-conventions`）的整理。它是 **.NET 文档团队写示例代码和文档时自用的约定**，明确说明：

> "These guidelines are used by Microsoft to develop samples and documentation. They were adopted from the [.NET Runtime, C# Coding Style] and [C# compiler (roslyn)] guidelines... They're meant to be an example of common C# conventions, and **not an authoritative list** (see Framework Design Guidelines for detailed guidelines)."

也就是说它的效力低于 Framework Design Guidelines（FDG），定位是"常见 C# 约定的示例"。原文给出四条选择理由（goals）：

1. **Correctness（正确性）**——示例会被复制粘贴进别人的项目，所以必须经得起反复修改仍然正确。
2. **Teaching（教学性）**——目的是教整个 .NET 与 C#，因此**不禁止任何语言特性或 API**，只示范"什么时候用某个特性是好选择"。
3. **Consistency（一致性）**——所有示例服从同一套风格。
4. **Adoption（采用新特性）**——积极把示例更新到新语言特性，让读者熟悉它们。

原文还解释了它为什么与 runtime / roslyn 的约定不同：后两者对热路径有严格性能指标，而 `.NET docs` 没有；教学性目标导致它不禁止任何构造，采用新特性的节奏也比多数生产项目激进。**这解释了本文为什么会出现 65 字符行宽、`required` 属性、集合表达式这类对游戏项目并不适用的条款**（见"与 Unity 场景的差异"）。

工具方面原文推荐用 `code analysis` + `.editorconfig` 强制执行团队约定，并可直接复制 `dotnet/docs` 的 `.editorconfig` 作为起点。

## 来源与可访问性

抓取日期 **2026-11-02**，经 `web_fetch`（沙箱内 curl / Invoke-WebRequest 无法直连外网）。

| URL | 结果 | 内容 |
| --- | --- | --- |
| `https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions` | ✅ 成功（HTTP 200，**全文**） | 页面标题 `Common C# code conventions`，含 Tools/analyzers、Language guidelines（字符串、构造与初始化、数组与集合、委托、try-catch 与 using、`&&`/`\|\|`、`new`、事件处理、静态成员、LINQ、隐式类型局部变量、文件级命名空间、using 位置）、Style guidelines（注释风格、布局约定）、Security 全部小节 |
| 同文中文版 `.../zh-cn/dotnet/csharp/fundamentals/coding-style/coding-conventions` | 未获取 | 本次只抓了 `en-us` 原文；中文版存在但其译法未核实，本文术语一律保留英文原词 |
| `https://github.com/dotnet/docs/blob/main/.editorconfig` | 未获取（未抓取） | 原文推荐用它作为风格起点；本文不引用其内容，需用时请自行抓取 |

页面元信息：`Last updated on 2025-01-18`；页面顶部有 `Edit` 链接指向 `github.com/dotnet/docs/blob/main/docs/csharp/fundamentals/coding-style/coding-conventions.md`，说明该文是开源可追溯的。

**结论：一手正文已拿到（英文全文）**，条款均据原文整理。中文版与 `.editorconfig` 未抓取，已在表中标注"未获取"，本文不做相关陈述。

## 核心条款

### 1. 通则（Language guidelines 开头的总清单）

| 强度 | 条款（原文措辞） |
| --- | --- |
| 建议 | **Utilize modern language features and C# versions whenever possible.** 尽可能使用现代语言特性与新版本 C# |
| 建议 | **Avoid outdated language constructs.** 避免过时的语言构造 |
| 须知 | 只捕获能正确处理异常的异常；**避免捕获通用异常**——示例代码不应在没有异常过滤器的情况下捕获 `System.Exception` |
| 建议 | 使用**具体异常类型**以提供有意义的错误消息 |
| 建议 | 用 **LINQ 查询与方法**做集合操作，提升可读性 |
| 建议 | I/O 密集操作用 **async / await** 异步编程 |
| 须知 | 注意死锁，在合适处使用 `Task.ConfigureAwait` |
| 建议 | 数据类型用**语言关键字**而不是运行时类型：`string` 而非 `System.String`，`int` 而非 `System.Int32`；此建议也包含 `nint` 与 `nuint` |
| 建议 | 用 `int` 而**不是无符号类型**；`int` 在 C# 中通用、与其它库互操作更容易。例外：文档本身就专门讲无符号数据类型 |
| 建议 | **只有当读者能从表达式推断出类型时才用 `var`**。原文给出的理由很具体：读者在 docs 平台上看示例，**没有悬停提示**能显示变量类型 |
| 建议 | 以**清晰和简单**为目标写代码 |
| 建议 | 避免过于复杂和绕的代码逻辑 |

### 2. `var`（隐式类型局部变量）——本文最细的一块

这是原文着墨最多、也最容易在团队里吵起来的一条，逐条列出：

| 强度 | 条款 | 原文示例 |
| --- | --- | --- |
| ✔️ | **变量类型从赋值右侧显而易见时**，用隐式类型 | `var message = "This is clearly a string.";` / `var currentTemperature = 27;` |
| ❌ | **类型从右侧不明显时不要用 `var`**；**不要因为方法名看起来像就假定类型清楚** | `int numberOfIterations = Convert.ToInt32(Console.ReadLine());` / `int currentMaximum = ExampleClass.ResultSoFar();` |
| 判定标准 | 类型被视为"清楚"的三种情况：**`new` 运算符、显式强制转换、赋值为字面量** | — |
| ❌ | **不要用变量名来"指定"变量类型**——它可能根本不对。应该用类型指定类型、用变量名表达语义 | 反例：`var inputInt = Console.ReadLine();` 应写成 `string` 类型 + 名字如 `iterations` |
| ❌ | 不要用 `var` 代替 `dynamic`；需要运行期类型推断就用 `dynamic` | — |
| ✔️ | **`for` 循环的循环变量**用隐式类型 | `for (var i = 0; i < 10000; i++)` |
| ❌ | **`foreach` 循环的循环变量不要用隐式类型**。理由："在多数情况下，集合中元素的类型并不显而易见。不应只依赖集合名去推断元素类型" | `foreach (char ch in laugh)` |
| ✔️ | **LINQ 查询的结果序列**用隐式类型（很多查询产生匿名类型，必须用 `var`；其它查询产生嵌套泛型，`var` 更可读）。这条**优先于**上面的通则 | — |
| ✔️ | LINQ **查询变量和范围变量**的声明用隐式类型（此条明确"overrides the general rules for implicitly typed local variables"） | `var seattleCustomers = from customer in Customers ...` |
| 注意 | 小心别意外改变可迭代集合的元素类型：例如在 `foreach` 里从 `System.Linq.IQueryable` 变成 `System.Collections.IEnumerable` 会改变查询的执行方式 | — |
| 例外 | 示例要讲解表达式的 **natural type** 时必须用 `var`，让编译器选自然类型；即便不那么直观也要用，并在正文里解释行为 | — |

### 3. 字符串（String data）

| 强度 | 条款 | 示例 |
| --- | --- | --- |
| ✔️ | 拼接**短字符串**用**字符串内插**（string interpolation） | `string displayName = $"{nameList[n].LastName}, {nameList[n].FirstName}";` |
| ✔️ | 在**循环里**追加字符串（尤其大量文本）用 `System.Text.StringBuilder` | `manyPhrases.Append(phrase);` |
| ✔️ | 优先用 **raw string literals**，而不是转义序列或逐字字符串（verbatim strings） | `var message = """ ... """;` |
| ✔️ | 用**基于表达式的字符串内插**，而不是**位置式**内插 | `Console.WriteLine($"{student.Last} Score: {student.score}");` |

### 4. 构造函数与初始化（Constructors and initialization）

| 强度 | 条款 |
| --- | --- |
| ✔️ | **record 类型**的主构造函数参数用 **Pascal case**：`public record Person(string FirstName, string LastName);` |
| ✔️ | **class 与 struct 类型**的主构造函数参数用 **camel case** |
| ✔️ | 用 **`required` 属性**而不是构造函数来强制属性值必须初始化 |

### 5. 数组与集合（Arrays and collections）

| 强度 | 条款 |
| --- | --- |
| ✔️ | 用**集合表达式**初始化所有集合类型：`string[] vowels = [ "a", "e", "i", "o", "u" ];` |

### 6. 委托（Delegates）

| 强度 | 条款 |
| --- | --- |
| ✔️ | 用 **`Func<>` 和 `Action<>`**，而不是自己定义委托类型；在类里定义委托方法 |
| ✔️ | 用 `Func<>` / `Action<>` 定义的签名来调用方法 |
| ✔️ | 创建委托类型实例时用**简洁语法**（`Del exampleDel2 = DelMethod;`），而不是完整语法（`new Del(DelMethod)`） |

### 7. 异常处理与 `using`（try-catch / using）

| 强度 | 条款 |
| --- | --- |
| ✔️ | 大多数异常处理用 **`try-catch`**；catch 具体异常类型（示例 catch `System.ArithmeticException`），必要时 `throw;` 重新抛出 |
| ✔️ | 若 `try-finally` 的 `finally` 块**只有一句 `Dispose` 调用**，改用 **`using` 语句** |
| ✔️ | 用**新的 `using` 语法**（`using Font normalStyle = new Font("Arial", 10.0f);`）——不需要大括号 |

### 8. `&&` 与 `||`（短路）

| 强度 | 条款 |
| --- | --- |
| ✔️ | 做比较时用 **`&&`** 而不是 `&`，用 **`\|\|`** 而不是 `\|` |
| 理由 | 短路求值。原文示例：`divisor` 为 0 时，`&` 会求值第二个子句从而运行期报错；`&&` 在第一个表达式为 false 时短路，不求值第二个 |

### 9. `new` 运算符

| 强度 | 条款 |
| --- | --- |
| ✔️ | 变量类型与对象类型**一致**时，用简洁形式：`var firstExample = new ExampleClass();` 或 `ExampleClass instance2 = new();` |
| 限制 | 变量是**接口类型**或运行时类型的**基类**时，这种形式**无效** |
| ✔️ | 用**对象初始化器**简化对象创建：`new ExampleClass { Name = "Desktop", ID = 37414, Location = "Redmond", Age = 2.3 };` |

### 10. 事件处理（Event handling）

| 强度 | 条款 |
| --- | --- |
| ✔️ | **不需要后续移除**的事件处理器，用 **lambda 表达式**定义：`this.Click += (s, e) => { ... };` |
| 对比 | 传统写法需单独定义 `void Form1_Click(object? sender, EventArgs e)` 方法并用 `new EventHandler(Form1_Click)` 绑定 |

### 11. 静态成员（Static members）

| 强度 | 条款 |
| --- | --- |
| ✔️ | 用**类名**调用静态成员：`ClassName.StaticMember`。这样静态访问一目了然，可读性更好 |
| ❌ | **不要用派生类名去限定基类里定义的静态成员**。虽然能编译，但可读性有误导性，而且将来派生类加了同名静态成员时代码可能坏掉 |

### 12. LINQ 查询（LINQ queries）

| 强度 | 条款 |
| --- | --- |
| ✔️ | 查询变量用**有意义的名字**（示例用 `seattleCustomers` 表示位于 Seattle 的客户） |
| ✔️ | 用**别名**保证匿名类型的属性名正确采用 **Pascal 大小写**：`select new { Customer = customer, Distributor = distributor };` |
| ✔️ | 结果中属性名会歧义时**重命名属性**：不要留下两个都叫 `Name` 的，明确写成 `CustomerName`、`DistributorName` |
| ✔️ | 查询**子句对齐到 `from` 子句下面** |
| ✔️ | **`where` 子句放在其它查询子句之前**，确保后续子句作用在已过滤缩减的数据集上 |
| ✔️ | 访问**内部集合**用多个 `from` 子句，而**不是 `join`**（示例：学生对象各含成绩集合，展开后筛出 >90 分的成绩） |

### 13. 命名空间（File scoped namespace declarations）

| 强度 | 条款 |
| --- | --- |
| ✔️ | 大多数代码文件只声明一个命名空间，因此示例应使用**文件级命名空间声明**：`namespace MySampleCode;` |

### 14. `using` 指令的位置（Place using directives outside the namespace）

| 强度 | 条款 |
| --- | --- |
| ✔️ | 把 `using` 指令放在**命名空间声明之外** |
| 理由 | `using` 在命名空间外时，导入的是**完全限定名**，更清晰；放在命名空间内则可能是相对名，也可能是完全限定名，**名字解析依赖上下文** |
| 原文实例 | `CoolStuff.AwesomeFeature` 内的 `using Azure;` 会依次匹配 `CoolStuff.AwesomeFeature.Azure` → `CoolStuff.Azure` → `Azure`。一旦有人新增同名命名空间，代码就会突然报 `error CS0246: The type or namespace name 'WaitUntil' could not be found...` 与 `error CS0103: The name 'WaitUntil' does not exist in the current context` |
| 补救 | 可用 `using global::Azure;` 消除歧义，但**把 using 放到命名空间外**更简单 |

### 15. 代码风格（Style guidelines）

| 强度 | 条款 |
| --- | --- |
| ✔️ | 缩进用**四个空格**，**不要用制表符（tab）** |
| ✔️ | 一致地对齐代码，提升可读性 |
| ✔️ | **把行限制在 65 个字符**——为在 docs 上（尤其手机屏）阅读而设 |
| ✔️ | 把长语句**拆成多行**，提升清晰度与体验 |
| ✔️ | 大括号用 **"Allman" 风格**：开括号和闭括号**各自独占一行**，与当前缩进级别对齐 |
| ✔️ | 需要换行时，**在二元运算符之前**换行（line breaks should occur before binary operators） |

### 16. 注释风格（Comment style）

| 强度 | 条款 |
| --- | --- |
| ✔️ | 简短说明用**单行注释 `//`** |
| ❌ | **避免用多行注释 `/* */`** 写长说明（示例代码里的注释不做本地化，即不翻译；长段解释应放在配套文章里，便于本地化） |
| ✔️ | 描述方法、类、字段和**所有 public 成员**时用 **XML 注释** |
| ✔️ | 注释**单独占一行**，不要放在代码行末 |
| ✔️ | 注释正文**以大写字母开头** |
| ✔️ | 注释正文**以句号结尾** |
| ✔️ | `//` 与注释正文之间**留一个空格**：`// The following declaration creates a query.` |

### 17. 布局约定（Layout conventions）

| 强度 | 条款 |
| --- | --- |
| ✔️ | 使用代码编辑器**默认设置**（智能缩进、四字符缩进、tab 存为空格） |
| ✔️ | **一行一条语句** |
| ✔️ | **一行一个声明** |
| ✔️ | 续行若未自动缩进，**缩进一个制表位（四个空格）** |
| ✔️ | **方法定义与属性定义之间至少留一个空行** |
| ✔️ | 用**圆括号**让表达式中的子句边界清晰：`if ((startX > endX) && (startX > previousX))` |
| 例外 | 示例本身在讲运算符或表达式优先级时，不必加这层括号 |

### 18. 关于"命名"，本文实际给出的内容

⚠️ 需要注意：**这篇页面没有独立的命名章节**（命名规则在 FDG 与 runtime 的 coding-style 文档里）。它散落的命名条款只有这些，全部列出以免误以为本文覆盖了命名总则：

| 场景 | 约定 |
| --- | --- |
| record 的主构造函数参数 | **Pascal case**（`Person(string FirstName, string LastName)`） |
| class / struct 的主构造函数参数 | **camel case** |
| 匿名类型属性（LINQ 投影） | 用别名保证 **Pascal 大小写** |
| 查询变量、范围变量 | 用**有意义的名字**（`seattleCustomers`） |
| 变量名 | 表达**语义信息**，不要用来编码类型（`inputInt` 是反例） |
| 静态成员访问 | 必须用**声明它的类名**限定 |

字段命名的具体规则、`public`/`private` 字段的大小写差异、常量与事件命名——**本文未覆盖**，请见 [`dotnet-design-guidelines.md`](dotnet-design-guidelines.md)。

## 与 Unity 场景的差异

本文的定位是"文档示例的约定"，多条直接搬进 Unity 项目会出问题。下面按"能不能用 / 怎么改"逐条对照。

### 语言特性可用性（受 Unity 的 C# 版本限制）

Unity 目前（含 Unity 6）编译器级别停在 **C# 9**，因此本文推荐的若干"现代特性"在本项目里**不可用**：

| 本文条款 | 所需 C# 版本 | Unity 里能否用 |
| --- | --- | --- |
| `namespace MySampleCode;` 文件级命名空间 | C# 10 | ❌ **不可用**，必须写块状 `namespace X { ... }` |
| 集合表达式 `[ "a", "e", "i" ]` | C# 12 | ❌ 不可用，用 `new[] { ... }` 或 `new List<T> { ... }` |
| raw string literals `"""..."""` | C# 11 | ❌ 不可用，仍需转义序列或逐字字符串 `@"..."` |
| `required` 属性 | C# 11 | ❌ 不可用；且与 Unity 序列化（Inspector 赋值不走构造函数）根本冲突 |
| class / struct 主构造函数 | C# 12 | ❌ 不可用（record 的 positional 形式 C# 9 可用，但 Unity 不序列化 record，慎用） |
| `using Font f = new(...);`（无大括号 using 声明） | C# 8 | ✅ 可用 |
| `ExampleClass instance2 = new();`（目标类型 `new`） | C# 9 | ✅ 可用 |
| `Func<>` / `Action<>`、lambda 事件处理器、LINQ 查询语法 | C# 3 | ✅ 可用（但见下面的性能提示） |

### 与 Unity 工程实践冲突的条款

| 本文条款 | Unity 里的处理 |
| --- | --- |
| **行宽限制 65 字符** | ❌ 不采用。原文自述该限制是"为在 docs 上尤其手机屏阅读"，属文档渲染约束。Unity 项目用 **100–120** 更合理；Unity 官方示例风格指南给的是"some prefer less than 120 characters" |
| **一行一个声明 / 一行一条语句** | ✅ 采用。`[SerializeField] private float a, b, c;` 这种合并声明在 Inspector 里虽然能用，但不利于 review 与 diff |
| **Allman 大括号** | ✅ 采用（与 Visual Studio 默认 C# 格式一致）。但 Unity 官方示例风格指南允许 K&R 或 Allman，**关键是全项目统一** |
| **`var` 仅在右侧可推断时用；`foreach` 不用 `var`** | ⚠️ 部分采用。`foreach` 那条在 Unity 里可以放宽：`foreach (var kv in dict)` 在 `Dictionary` / `List<T>` 上类型由容器泛型参数一目了然，比写全 `KeyValuePair<string, List<GameObject>>` 可读性更好。但**遍历返回 `object` 的 API（如 `Transform`、`SerializedProperty`）时必须显式写类型**，否则拿不到强类型成员 |
| **`int` 而非无符号类型** | ⚠️ 大体采用；但 Unity 的 API 大量使用 `uint`（如 `Mesh.SetIndices` 的 `uint` 重载、`Hash128`），跟随 API 签名即可，不要强行转换 |
| **`async` / `await` 做 I/O** | ⚠️ 需谨慎。Unity 里 `async void` 与 `Task` 的上下文切换容易与主线程 API（`transform`、`Instantiate`）冲突，且 `Task` 异常不会被 Unity 自动打日志。项目里若已有 UniTask / 协程方案，**跟随项目方案**，不要按本文引入裸 `Task` |
| **LINQ 做集合操作** | ⚠️ 谨慎。LINQ 在热路径（每帧 `Update`、大量实体遍历）会分配迭代器与闭包造成 GC，并在 IL2CPP 下受 AOT 泛型实例化限制。**冷路径**（编辑器工具、初始化、资源加载）可以放心用；热路径改 `for` 循环 |
| **`dynamic` 作运行期类型推断** | ❌ Unity / IL2CPP **不支持** `dynamic`（需要 `Microsoft.CSharp` 运行时代码生成）。Unity 里不要出现 `dynamic` |
| **`System.Text.StringBuilder` 在循环里拼串** | ✅ 采用，而且比 .NET 更必要——Unity 的 GC 压力敏感 |
| **XML 注释用于所有 public 成员** | ⚠️ 按需。Unity 项目的 `public` 成员多数是给 Inspector 用、不对外发布 API，为它们写全套 XML 注释收益低。**真正对外/给其它模块用的 API 才写**；Inspector 调参字段用 `[Tooltip]` 更实用 |
| **`required` 属性强制初始化** | ❌ 不可用。Unity 组件由引擎实例化，字段值来自序列化数据，构造期强制初始化既做不到也不需要；替代方案是 `[SerializeField]` + `Awake` 里校验 + `[Tooltip]` 说明 |
| **`throw;` 与具体异常类型** | ⚠️ 但注意 Unity 里**不要用异常做流程控制**：每帧路径上的异常在 IL2CPP 有成本，且 Unity 会把它打进 Console 干扰排查。用返回值/`TryGet` 模式 |
| **静态成员用类名限定** | ✅ 采用。Unity 里特别注意 `UnityEngine.Object` 的伪 null 语义，`SomeClass.Instance?.Foo()` 对已销毁对象不会短路 |

### 需要补的（本文没有、Unity 必需的）

| 议题 | 说明 |
| --- | --- |
| **public 实例字段** | 本文完全不涉及；Unity 的 Inspector 序列化要求"字段可序列化"，`[SerializeField] private` 是保住封装的唯一手段。属性不会被序列化 |
| **序列化字段的命名与分组** | Unity 特有：`[Header]`、`[Tooltip]`、`[Range]`、`[Serializable]` 分组结构体 |
| **MonoBehaviour 文件名 = 类名** | 硬要求，本文未提 |
| **`Update`/`FixedUpdate`/`Awake` 等消息方法名** | 由 Unity 引擎按名字反射调用，**必须**逐字匹配，不能按本文的命名美学改 |
| **可空引用类型** | 本文出现了 `object? sender` 的写法（C# 8 可空注解），Unity 默认不开启 `#nullable`，团队需自行决定是否逐文件开启 |

## 相关文档

- [`../README.md`](../README.md) —— 本项目代码规范总览与阅读顺序
- [`../03-naming.md`](../03-naming.md) —— 本项目命名约定（本文不含命名总则，命名以该文件与 FDG 为准）
- [`../../INDEX.md`](../../INDEX.md) —— 文档总索引
- 同目录参考：[`dotnet-design-guidelines.md`](dotnet-design-guidelines.md)、[`unity-code-style-guide-repo.md`](unity-code-style-guide-repo.md)
