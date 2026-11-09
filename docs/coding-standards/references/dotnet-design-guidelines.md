# .NET 框架设计准则（Microsoft Framework Design Guidelines）整理

> 目的：把微软 Framework Design Guidelines 中与日常写 C# 代码最相关的命名、类型设计、成员设计条款抽成一份可对照执行的中文清单。
> 适用读者：本项目写 C# / Unity C# 的开发者，尤其是设计公共 API、组件接口与事件的人。
> 前置条件：无（本文为外部资料整理）
> 最后更新：2026-11-02

## 这是什么

Framework Design Guidelines（FDG，框架设计准则）是微软官方关于"如何设计可复用的 .NET 类库与组件"的准则集，核心目标是让 API 在不同编程语言、不同团队之间保持一致、易用。它把这些准则组织成四种前缀：

| 前缀 | 中文含义 | 强度 |
| --- | --- | --- |
| `Do` (✔️) | 务必这样做 | 强制建议 |
| `Consider` (✔️ 建议) | 考虑这样做 | 弱建议 |
| `Avoid` (❌ 避免) | 避免这样做 | 弱禁止 |
| `Do not` (❌) | 不要这样做 | 强制禁止 |

微软同时声明："这些准则旨在帮助类库设计器了解不同解决方案之间的权衡。在某些情况下，良好的库设计要求违反这些设计准则。这种情况应该很少见，重要的是你在做出决定时有一个明确且令人信服的原因。"

**重要背景（决定了本文的效力边界）**：这套准则源自 Krzysztof Cwalina 与 Brad Abrams 的《Framework Design Guidelines: Conventions, Idioms, and Patterns for Reusable .NET Libraries》第 2 版（2008 年出版），原书此后已出第 3 版并全面修订，微软自己在总览页标注 **"这些指南中的一些信息可能已过时"**（some of the information in these guidelines may be out of date）。因此本文只抽取**与 Unity 日常写代码相关、且未随语言演进失效**的条款，过时项（如旧的 CLR 约束）不纳入。

准则的适用范围也很关键：原文说"采用这些命名约定作为常规代码开发准则会导致在整个代码中更一致地命名。但是，**只需将它们应用于公开的 API**（公共或受保护的类型和成员，以及显式实现的接口）"。也就是说它本质是**公开 API 的设计准则**，不是内部实现的风格规范。

## 来源与可访问性

抓取日期统一为 **2026-11-02**，全部经 `web_fetch`（沙箱内 curl / Invoke-WebRequest 无法直连外网）。中文页优先，均为 `learn.microsoft.com/zh-cn/`。

| URL | 结果 | 内容 |
| --- | --- | --- |
| `https://learn.microsoft.com/zh-cn/dotnet/standard/design-guidelines/` | ✅ 成功（HTTP 200） | 总览：准则定位、Do/Consider/Avoid/Do not 四种前缀、章节导航、第 2 版来源与"可能过时"声明 |
| `.../design-guidelines/naming-guidelines` | ✅ 成功（HTTP 200） | 命名准则**章节页**：仅目录 + 适用范围说明（只应用于公开 API） |
| `.../design-guidelines/capitalization-conventions` | ✅ 成功（HTTP 200） | **大小写约定**：PascalCasing / camelCasing 定义、标识符大小写对照表、复合词表、区分大小写 |
| `.../design-guidelines/general-naming-conventions` | ✅ 成功（HTTP 200） | **常规命名约定**：用词选择、缩写与首字母缩略词、避免语言特定名称、命名 API 新版本 |
| `.../design-guidelines/names-of-classes-structs-and-interfaces` | ✅ 成功（HTTP 200） | **类/结构/接口名称**：名词短语、I 前缀、泛型类型参数、常见类型后缀表、枚举命名 |
| `.../design-guidelines/names-of-type-members` | ✅ 成功（HTTP 200） | **类型成员名称**：方法、属性、事件、字段 |
| `.../design-guidelines/naming-parameters` | ✅ 成功（HTTP 200） | **命名参数**：camelCase、描述性、运算符重载参数 |
| `.../design-guidelines/type` | ✅ 成功（HTTP 200） | **类型设计准则**章节页：类/接口/结构/枚举/静态类定位 + 子页导航 |
| `.../design-guidelines/choosing-between-class-and-struct` | ✅ 成功（HTTP 200） | **类与结构的选择**：堆/栈、装箱、复制语义、结构的四条门槛 |
| `.../design-guidelines/member` | ✅ 成功（HTTP 200） | **成员设计准则**章节页：成员范围 + 子页导航 |
| `.../design-guidelines/property` | ✅ 成功（HTTP 200） | **属性设计**：只读属性、禁止只写属性、setter 保留旧值、getter 不抛异常、索引器、属性变更通知 |
| `.../design-guidelines/field` | ✅ 成功（HTTP 200） | **字段设计**：禁止 public/protected 实例字段、const、public static readonly |
| `.../design-guidelines/event` | ✅ 成功（HTTP 200） | **事件设计**：raise 而非 fire/trigger、EventHandler<T>、On 前缀虚方法、sender/e、EventArgs 子类 |

补充说明（影响引用时的措辞）：

- 中文页是**机器翻译且质量不稳定**，个别标题被译错：`property` 页标题被译成"**地产设计**"（应为"属性设计"）、`type` 页标题被译成"**字体设计指南**"（应为"类型设计准则"）。引用时请以英文原词（Property Design / Type Design Guidelines）为准。
- `naming-guidelines`、`type`、`member` 三页只是**章节目录页**，不含具体条款；具体条款在其子页。子页中 `names-of-namespaces`、`names-of-assemblies-and-dlls`、`naming-resources`、`abstract-class`、`static-class`、`interface`、`struct`、`enum`、`nested-types`、`member-overloading`、`constructor`、`extension-methods`、`operator-overloads`、`parameter-design` 本次**未逐一抓取**（与 Unity 日常写代码关系较远），本文不覆盖其条款。

**结论：一手正文已拿到**，且是微软官方中文页正文（非二手转述）。未覆盖的子页已在上面列明，不做臆测。

## 核心条款

### 1. 大小写：两种且只有两种

原文定义（capitalization-conventions）：

> 若要区分标识符中的单词，请大写标识符中每个单词的第一个字母。**请勿使用下划线来区分字词，也不要在标识符的任意位置使用下划线。**

| 约定 | 规则 | 示例 |
| --- | --- | --- |
| **PascalCasing** | 每个单词（含超过两个字母的首字母缩写）首字母大写 | `PropertyDescriptor`、`HtmlTag` |
| **PascalCasing 特例** | **两个字母**的首字母缩写两个字母都大写 | `IOStream` |
| **camelCasing** | 仅用于**参数名**；除第一个单词外每个单词首字母大写 | `propertyDescriptor`、`htmlTag` |
| **camelCasing 特例** | 开头的双字母缩略词全小写 | `ioStream` |

✔️ 对包含多个单词的所有**公共成员、类型、命名空间名称**使用 PascalCasing。
✔️ 参数名使用 camelCasing。

### 2. 各类标识符的大小写对照表（原文表格，逐行保留）

| 标识符 | 大小写 | 原文示例 |
| --- | --- | --- |
| Namespace | Pascal | `namespace System.Security { ... }` |
| 类型 (Type) | Pascal | `public class StreamReader { ... }` |
| 接口 (Interface) | Pascal | `public interface IEnumerable { ... }` |
| 方法 (Method) | Pascal | `public virtual string ToString();` |
| 属性 (Property) | Pascal | `public int Length { get; }` |
| 事件 (Event) | Pascal | `public event EventHandler Exited;` |
| 字段 (Field) | Pascal | `public static readonly TimeSpan InfiniteTimeout;` / `public const Min = 0;` |
| 枚举值 (Enum value) | Pascal | `public enum FileMode { Append, ... }` |
| 参数 (Parameter) | camel | `public static int ToInt32(string value);` |

### 3. 复合词与常见术语（原文表格，这是最容易踩的坑）

原文规则："出于大写目的，大多数复合词被视为**单一字词**"，即**不要**在封闭式复合词（closed form compound）里把每个单词都大写。判定依据是"使用当前字典确定复合词是否以封闭形式书写"。

❌ 不要在所谓的封闭式复合词中大写每个单词。

| 正确（Pascal） | 正确（camel） | 错误 |
| --- | --- | --- |
| `BitFlag` | `bitFlag` | `Bitflag` |
| `Callback` | `callback` | `CallBack` |
| `Canceled` | `canceled` | `Cancelled` |
| `DoNot` | `doNot` | `Don't` |
| `Email` | `email` | `EMail` |
| `Endpoint` | `endpoint` | `EndPoint` |
| `FileName` | `fileName` | `Filename` |
| `Gridline` | `gridline` | `GridLine` |
| `Hashtable` | `hashtable` | `HashTable` |
| `Id` | `id` | `ID` |
| `Indexes` | `indexes` | `Indices` |
| `LogOff` | `logOff` | `LogOut` |
| `LogOn` | `logOn` | `LogIn` |
| `Metadata` | `metadata` | `MetaData`, `metaData` |
| `Multipanel` | `multipanel` | `MultiPanel` |
| `Multiview` | `multiview` | `MultiView` |
| `Namespace` | `namespace` | `NameSpace` |
| `Ok` | `ok` | `OK` |
| `Pi` | `pi` | `PI` |
| `Placeholder` | `placeholder` | `PlaceHolder` |
| `SignIn` | `signIn` | `SignOn` |
| `SignOut` | `signOut` | `SignOff` |
| `UserName` | `userName` | `Username` |
| `WhiteSpace` | `whiteSpace` | `Whitespace` |
| `Writable` | `writable` | `Writeable` |

直接可用的结论：**`Id` 不要写 `ID`**、**`Endpoint` 不要写 `EndPoint`**、**`Callback` 不要写 `CallBack`**、**`Canceled` 只有一个 l**（美式拼写）。

### 4. 用词选择（general-naming-conventions）

| 强度 | 条款 | 原文示例 |
| --- | --- | --- |
| ✔️ | 选择**易于阅读**的标识符名 | `HorizontalAlignment` 优于 `AlignmentHorizontal` |
| ✔️ | **可读性比简洁性更重要** | `CanScrollHorizontally` 优于 `ScrollableX`（对 X 轴的模糊引用） |
| ❌ | 不要使用下划线、连字符或任何其它非字母字符 | — |
| ❌ | 不要使用**匈牙利表示法** | 即 `strName`、`iCount` 这类类型前缀 |
| ❌ | 避免与广泛使用的编程语言关键字冲突的标识符 | 虽然 C# 可用 `@` 转义，但"使用转义序列的方法比没有转义序列的方法要困难得多" |
| ❌ | 不要用**缩写或缩写词**作为标识符名的一部分 | `GetWindow` 而不是 `GetWin` |
| ❌ | 不要用**未被广泛接受**的首字母缩略词，即使"必需" | `IOStream` 是接受的；自造缩略词不接受 |
| ✔️ | 类型名用**语义有趣**的名称，而不是语言特定的关键字 | `GetLength` 优于 `GetInt` |
| ✔️ | 极少数情况下标识符在类型之外无语义含义时，用**CLR 类型名**而非语言特定名 | 转换到 `Int64` 的方法应叫 `ToInt64`，不是 `ToLong`（`long` 是 C# 别名） |
| ✔️ | 标识符无语义含义且参数类型不重要时，用通用名（`value`、`item`）而非重复类型名 | — |

关于"命名现有 API 的新版本"（本文判定与 Unity 日常关系不大，仅记录核心几条）：优先加**后缀**而不是前缀（便于 IntelliSense 按字母排序时新旧 API 相邻）；考虑用全新但有意义的名字；数字后缀作为最后手段；❌ 不要用 `Ex` 后缀区分新旧版本；64 位版本才用 `64` 后缀，且仅当已有 32 位 API 存在。

### 5. 区分大小写（camcelization 的硬约束）

❌ **不要假设所有编程语言都区分大小写——它们不是。名称不能仅靠大小写区分。**

原文理由：CLR 上运行的语言不要求支持区分大小写；"外部可访问的任何 API 都不能单独依赖于大小写来区分同一上下文中的两个名称"。落地含义：不要同时存在 `health` 与 `Health` 两个对外成员。

### 6. 类型命名（names-of-classes-structs-and-interfaces）

| 强度 | 条款 |
| --- | --- |
| ✔️ | 类和结构用 **PascalCasing + 名词或名词短语**（区分于用谓词短语命名的方法） |
| ✔️ | 接口命名为**形容词短语**，偶尔可用名词或名词短语 |
| ✔️ | 很少使用名词/名词短语命名接口——那可能表示该类型应为**抽象类**而不是接口 |
| ❌ | 不要给类名加前缀（例如 `C`） |
| ✔️ | 考虑让派生类名以基类名结尾（`ArgumentOutOfRangeException` 是 `Exception`，`SerializableAttribute` 是 `Attribute`），但需合理判断（`Button` 是 `Control`，名字里没有 `Control`） |
| ✔️ | 接口名用字母 **`I`** 作前缀（`IComponent`、`ICustomAttributeProvider`、`IPersistable`）；同样避免缩写 |
| ✔️ | 定义"类 + 接口"配对时，类名应只比接口名少一个 `I` 前缀 |

**泛型类型参数**：

| 强度 | 条款 |
| --- | --- |
| ✔️ | 不要用描述性名称命名泛型类型参数，除非单字母名完全自解释且描述性名不增值 |
| ✔️ | 只有一个类型参数时用 `T`（`IComparer<T>`、`Predicate<T>`、`Nullable<T>`） |
| ✔️ | 描述性类型参数名要加 `T` 前缀（`ISessionChannel<TSession>`） |
| ✔️ | 考虑在参数名中体现对类型参数的约束（限制为 `ISession` 的参数叫 `TSession`） |

**常见基类型 → 后缀约定（原文表格）**：

| 基类型 | 派生/实现类型准则 |
| --- | --- |
| `System.Attribute` | 加后缀 `Attribute` |
| `System.Delegate` | 事件用的委托加 `EventHandler`；非事件处理器的委托加 `Callback`；❌ 不要加 `Delegate` |
| `System.EventArgs` | 加后缀 `EventArgs` |
| `System.Enum` | ❌ 不要从 `System.Enum` 派生（用 `enum` 关键字）；❌ 不要加 `Enum` 或 `Flag` 后缀 |
| `System.Exception` | 加后缀 `Exception` |
| `IDictionary` / `IDictionary<TKey,TValue>` | 加后缀 `Dictionary`（此条优先于更通用的集合准则） |
| `IEnumerable` / `ICollection` / `IList` 及其泛型版 | 加后缀 `Collection` |
| `System.IO.Stream` | 加后缀 `Stream` |
| `CodeAccessPermission` / `IPermission` | 加后缀 `Permission` |

**枚举命名（写游戏状态机时高频）**：

| 强度 | 条款 |
| --- | --- |
| ✔️ | 枚举类型名遵循标准类型命名规则（PascalCasing 等） |
| ✔️ | 非位字段枚举用**单数**类型名 |
| ✔️ | 位字段（标志）枚举用**复数**类型名 |
| ❌ | 不要在枚举类型名里用 `Enum` 后缀 |
| ❌ | 不要在枚举类型名里用 `Flag` 或 `Flags` 后缀 |
| ❌ | 不要在枚举**值**上加前缀（原文反例：ADO 枚举用 `ad`、富文本枚举用 `rtf`） |

### 7. 成员命名（names-of-type-members）

**方法**：

| 强度 | 条款 |
| --- | --- |
| ✔️ | 用**谓词或谓词短语**命名方法（因为方法"采取措施"）；这也把方法名与属性名（名词/形容词）区分开 |
| 原文示例 | `CompareTo(...)`、`Split(...)`、`Trim()` |

**属性**：

| 强度 | 条款 |
| --- | --- |
| ✔️ | 用**名词、名词性短语或形容词**命名属性（属性引用数据） |
| ✔️ | 属性名**总是** Pascal 大小写 |
| ❌ | 不要有与 `Get` 方法名匹配的属性。反例：`public string TextWriter { get {...} set {...} }` + `public string GetTextWriter(int value)` —— 这种模式通常表示该"属性"实际应该是方法 |
| ✔️ | 集合属性用**描述集合中各项的复数短语**，而不是"单数短语 + `List` / `Collection`" |
| ✔️ | 布尔属性用**肯定**短语（`CanSeek` 而非 `CantSeek`）；可按需加 `Is` / `Can` / `Has` 前缀，但**仅当有助于提升可读性** |
| ✔️ | 考虑让属性名与它的类型同名（`public Color Color { get; set; }`） |

**事件**：

| 强度 | 条款 |
| --- | --- |
| ✔️ | 用**谓词或谓词短语**命名事件（事件总指某个动作） |
| 原文示例 | `Clicked`、`Painting`、`DroppedDown` |
| ✔️ | 用**现在时和过去时**表达"之前/之后"：关闭窗口**前**触发的叫 `Closing`，**之后**触发的叫 `Closed` |
| ❌ | **不要**用 `Before` / `After` 前缀或后缀表示事件前后 |
| ✔️ | 事件处理程序（用作事件类型的委托）加 `EventHandler` 后缀：`public delegate void ClickedEventHandler(object sender, ClickedEventArgs e);` |
| ✔️ | 事件处理程序用两个参数，命名为 `sender` 和 `e`；`sender` 通常是 `object` 类型（即使可以有更具体的类型） |
| ✔️ | 事件参数类用 `EventArgs` 后缀 |

**字段**：

| 强度 | 条款 |
| --- | --- |
| 适用范围 | 这些字段准则**只适用于静态公共字段和受保护字段**；成员设计准则不允许 public/protected **实例**字段（见下节字段设计） |
| ✔️ | 字段名用 **Pascal 大小写** |
| ✔️ | 用名词、名词性短语或形容词命名字段 |
| ❌ | **不要对字段名使用前缀**。原文明确点名："不要使用 `g_` 或 `s_` 来指示静态字段" |

**参数**：

| 强度 | 条款 |
| --- | --- |
| ✔️ | 参数名用**驼峰式大小写**（camelCase） |
| ✔️ | 用描述性参数名 |
| ✔️ | 考虑按**参数的含义**而不是参数的类型命名 |
| ✔️ | 二元运算符重载参数无语义时用 `left` 和 `right` |
| ✔️ | 一元/二元运算符重载参数无语义时用 `value` |
| ✔️ | 若增加重要价值，考虑给运算符重载参数起有意义的名字 |
| ❌ | 不要对运算符重载参数名使用缩写或数值索引 |

### 8. 类型设计：类与结构的选择（choosing-between-class-and-struct）

原文先比较引用类型与值类型的五个差异（写游戏时直接相关）：

1. **分配位置**：引用类型在堆上分配并 GC；值类型在栈上分配（栈展开时释放）或内联在包含类型中。→ 值类型的分配/释放开销通常更低。
2. **数组**：引用类型数组只存引用（元素在堆上）；值类型数组内联存实际实例。→ 值类型数组分配/释放便宜得多，且**引用局部性更好**（缓存友好，对游戏数据导向设计很关键）。
3. **装箱**：值类型转成引用类型或其实现的接口时要**装箱**，转回时**拆箱**。装箱在堆上分配、会被 GC。→ 过度装箱"会对堆、垃圾回收器，并最终对应用程序的性能产生负面影响"。
4. **复制成本**：引用类型赋值复制引用；值类型赋值复制**整个值**。→ 大型引用类型的赋值比大型值类型便宜。
5. **传递语义**：引用类型按引用传递，改一处影响所有引用；值类型按值传递，复制后互不影响。→"值类型应不可变"（因为副本是隐式创建的，可变值类型会让人困惑）。

所以原文结论："一般说来，框架中的大多数类型应该是类。"

| 强度 | 条款 |
| --- | --- |
| ✔️ | 如果类型的实例**小、通常生存期短、或通常嵌入在其它对象中**，考虑定义结构而不是类 |
| ❌ | **避免定义结构，除非类型同时具备以下所有特征**：<br>1. 逻辑上表示**单个值**，类似基元类型（`int`、`double`）<br>2. 实例大小**低于 16 字节**<br>3. 它是**不可变的**<br>4. 它**不必频繁装箱** |
| ✔️ | 其它所有情况都应把类型定义为**类** |

另有一条跨章节的总纲（`type` 页）：✔️ 确保每种类型都是**一组定义完好的相关成员**，而不是不相关功能的随机集合。

### 9. 成员设计：字段（field）

原文的论证值得记住：封装意味着"应设计一种类型，以便可以更改该类型的字段（改名或改类型）而不会中断类型成员以外的代码。**这个解释立即意味着所有字段都必须是私有的。**"常量和静态只读字段被排除在严格限制之外，因为"此类字段几乎按定义不需要更改"。

| 强度 | 条款 |
| --- | --- |
| ❌ | **不要提供公共或受保护的实例字段**；应提供用于访问该字段的**属性** |
| ✔️ | 对**永远不会更改的值**使用常量字段（`const`）。编译器会把常量值直接嵌入调用代码，所以常量值永远无法更改——改了就是破坏兼容性 |
| ✔️ | 对**预定义的对象实例**使用 `public static readonly` 字段 |
| ❌ | **不要把可变类型的实例赋给 `readonly` 字段**。`readonly` 只阻止"字段里存的实例被替换"，不阻止"通过调用成员修改实例内部数据"（数组、大多数集合、流都是可变类型；`Int32`、`Uri`、`String` 是不可变类型） |

### 10. 成员设计：属性（property）

原文定位："尽管属性在技术上与方法非常相似，但它们在使用方案中大相径庭。它们应被视为**智能字段**（smart fields）。它们兼具字段的调用语法和方法的灵活性。"

| 强度 | 条款 |
| --- | --- |
| ✔️ | 如果调用方不应更改属性的值，就创建**只有 get 没有 set** 的属性。注意：属性类型是可变引用类型时，即使只有 get，属性值仍可能被改 |
| ❌ | **不要提供只写属性**（set-only），也不要提供"setter 比 getter 可访问性更宽"的属性（例如 public setter + protected getter） |
| ✔️ | 无法提供 getter 时，用**方法**实现该功能，方法名以 `Set` 开头（`SetCachePath`，而不是只写的 `CachePath` 属性） |
| ✔️ | 为所有属性提供**合理的默认值**，且默认值不得导致安全漏洞或非常低效的代码 |
| ✔️ | 允许**按任意顺序**设置属性，即使这会导致对象临时处于无效状态；由无效状态引起的异常应推迟到对象**实际使用**相关属性时 |
| ✔️ | setter 抛异常时，**保留以前的值** |
| ❌ | **避免在属性读取器（getter）中引发异常**。访问器应是简单操作、不应有前置条件；getter 可能抛异常就应改设计为方法（此规则不适用于索引器） |

索引器（与 Unity 日常关系中等，游戏里做格子/背包数据结构时有用）：✔️ 考虑用索引器提供对内部数组数据的访问；✔️ 考虑为表示项集合的类型提供索引器；❌ 避免多参数索引属性；❌ 避免索引器参数类型不是 `Int32`/`Int64`/`String`/`Object`/枚举；✔️ 索引器名为 `Item`（除非有更好的名字，如 `String.Chars[]`）；❌ 不要同时提供索引器和语义等价的方法；❌ 不要在一个类型中提供多个重载索引器系列；❌ 不要使用非默认索引属性。

属性变更通知：✔️ 在修改**高层 API（通常是设计器组件）**的属性值时，考虑触发变更通知事件；✔️ 考虑在属性值**因外部因素**变化时触发变更通知事件（原文例：在 `TextBox` 里打字会改变 `Text` 属性）。

### 11. 成员设计：事件（event）

原文先区分两组事件：状态改变**之前**触发的称 **pre-events**（预事件，如 `Form.Closing`），改变**之后**触发的称 **post-events**（后事件，如 `Form.Closed`）。

| 强度 | 条款 |
| --- | --- |
| ✔️ | 用 **"raise"（引发）** 这个词，**不要**用 "fire"（激发）或 "trigger"（触发） |
| ✔️ | 使用 `System.EventHandler<TEventArgs>`，而不是手工新建委托作为事件处理程序 |
| ✔️ | 考虑用 `EventArgs` 的**子类**作事件参数——除非绝对确定事件永远不需要传数据。理由：直接使用 `EventArgs` 传 API，以后就再也无法在不破坏兼容性的情况下添加数据字段；先用子类即使完全为空也还能加 |
| ✔️ | 用**受保护的虚方法**引发每个事件（仅适用于未密封类上的非静态事件；不适用于结构、密封类、静态事件）。该方法的作用是给派生类提供替代处理方式；按约定方法名以 **`On`** 开头后接事件名；派生类可选择不调用基实现，所以该方法里不要放"基类正常工作所必需"的处理 |
| ✔️ | 给引发事件的受保护方法传**一个参数**，命名 `e`，类型为事件参数类 |
| ❌ | 引发**非静态**事件时，**不要**把 `null` 作为 sender 传入 |
| ✔️ | 引发**静态**事件时，把 `null` 作为 sender 传入 |
| ❌ | 引发事件时**不要**把 `null` 作为事件数据参数传递；不想传数据就传 `EventArgs.Empty`（"开发人员期望此参数不为 null"） |
| ✔️ | 考虑引发**最终用户可取消**的事件（仅 pre-events），用 `System.ComponentModel.CancelEventArgs` 或其子类作参数 |

自定义事件处理程序委托（`EventHandler<T>` 不可用时）：✔️ 返回类型用 `void`（一个事件可对多个对象调用多个处理方法，若允许返回值就会有多个返回值）；✔️ 第一个参数类型 `object`、名 `sender`；✔️ 第二个参数类型 `EventArgs` 或其子类、名 `e`；❌ 不要有两个以上参数。

## 与 Unity 场景的差异

FDG 面向的是**类库公开 API**，而 Unity 项目是**应用代码 + Inspector 驱动的数据**，下面这些条款在 Unity 里不适用或必须改写。

| 议题 | FDG 的条款 | Unity 里怎么办 / 为什么不适用 |
| --- | --- | --- |
| **public 实例字段** | ❌ 不要提供公共或受保护的实例字段，一律用属性封装 | **这条在 Unity 里必须打破**：Inspector 序列化只认**字段**，属性（property）不被序列化。要用 `[SerializeField] private` 保住封装（首选），确需公开时才 public。属性方案会导致参数无法在 Inspector 里调、无法被 Prefab 覆盖 |
| **只读属性 / 只写属性** | ✔️ 只 get 不给 set；❌ 禁止 set-only 属性 | Unity 里"只读但要在 Inspector 调参"的常见解法是 `[SerializeField] private` 字段 + 只读属性暴露，这恰好符合 FDG；但 `{ get; private set; }` 的自动属性**不能**序列化，不要用它做可调参数 |
| **`readonly` / `const` 字段** | ✔️ 常量用 `const`；✔️ 预定义实例用 `public static readonly` | Unity **不序列化** `const` 与 `readonly` 字段，它们在 Inspector 里不可见、不能被美术/策划改。`const` 只适合真正的编译期常量（如 Mathf 常量、层名缓存之外的场景），调参值一律用普通 `[SerializeField]` 字段 |
| **可变类型赋给 `readonly`** | ❌ 不要把可变类型实例赋给 `readonly` 字段 | Unity 里同样成立且更危险：序列化的 `List<T>` 字段就是可变类型，`readonly` 挡不住别人 `Clear()` 它，还可能被 UnityEngine.Object 的伪 null 语义坑到 |
| **getter 不抛异常 / 不做重活** | ❌ 避免在 getter 中抛异常；访问器应是简单操作 | 在 Unity 里要**加强**成一条硬规则：getter 里**绝不** `GetComponent`、绝不查场景、绝不算寻路。Unity 的组件查找开销大且可能在对象已销毁时返回伪 null，getter 必须是纯内存访问 |
| **结构体四条门槛（<16 字节、不可变、单值、免装箱）** | ❌ 除非同时满足四条，否则用类 | **游戏开发需要放宽**：`Vector3`（12 B）、`Color`（16 B）这类热路径类型天然该是 struct，但它们常被频繁装箱（如放进非泛型集合、被 `object` 参数接收）。实践折中：**保留 struct 换取缓存友好与零 GC**，同时用泛型容器、`in`/`ref` 参数、`IEquatable<T>` 避免装箱与默认 `Equals` 的反射开销。注意 `UnityEngine.Object` 一律是类 |
| **`EventArgs` 子类作事件参数** | ✔️ 用 `EventArgs` 子类，禁止 `null` 事件数据 | Unity 项目普遍直接用 `System.Action` / `Action<T>`（无 sender、无 EventArgs）以避免每次触发都 `new` 一个 EventArgs 造成 GC。这是对 FDG 的**有意识偏离**，代价是没有"以后加字段不破坏兼容"的保护——用 struct 载荷或专用 event struct 补偿 |
| **`On` 前缀的受保护虚方法引发事件** | ✔️ 用 `protected virtual void OnXxx(XxxEventArgs e)` 引发 | Unity 的 `MonoBehaviour` 不鼓励继承扩展（组件靠组合），且 `On` 前缀在 Unity 里**已被占用**为消息方法语义（`OnEnable`、`OnDisable`、`OnTriggerEnter`、`OnDestroy`），`OnXxx` 极易与 Unity 回调混淆。建议改用 `RaiseXxx()` 之类的名字，避免 Unity 消息方法与自定义虚方法撞名 |
| **`sender` / `e` 双参数** | ✔️ `object sender, EventArgs e` | Unity 事件几乎都是 `Action`，没有 sender。走 `EventHandler` 路线时也要注意：`sender` 是 `object`，会丢掉 `UnityEngine.Object` 的伪 null 判断，订阅方还得强转 |
| **禁止字段前缀（`g_`/`s_`）** | ❌ 不要前缀，明确点名 `g_`、`s_` | Unity 社区惯例相反：`m_` / `s_` / `k_` / `_` 都常见（Unity 官方示例风格指南也推荐 `m_`）。若采用前缀，必须全项目统一，并接受与 FDG 的偏离 |
| **一个文件一个类型** | .NET 惯例一文件一 public 类型（FDG 未强制，但生态如此） | Unity 的硬要求只有一条：**MonoBehaviour 的类名必须与文件名一致**（否则脚本挂不上）。非 MonoBehaviour 类型可以同文件 |
| **`Comparer`/`IComparable` 等 BCL 接口约定** | FDG 对 BCL 接口有详细准则 | Unity 里真正高频的是 `IEquatable<T>`、`IComparable<T>` 与 `IEqualityComparer<T>` 用于避免 struct 装箱；FDG 的相关条款（`equality-operators`、`usage-guidelines`）本次未抓取，不要凭记忆引用 |
| **准则的适用面** | 只应用于**公开的 API**（public/protected 类型与成员、显式接口实现） | 这条在 Unity 里就是"只对外暴露的组件 API 与事件遵守 FDG，内部实现与 Inspector 字段遵循 Unity 惯例"。不要把 FDG 当成内部变量命名的规范来用 |

## 相关文档

- [`../README.md`](../README.md) —— 本项目代码规范总览与阅读顺序
- [`../03-naming.md`](../03-naming.md) —— 本项目命名约定（把上面"Pascal vs camel vs 前缀"的分歧落成内部裁决）
- [`../../INDEX.md`](../../INDEX.md) —— 文档总索引
- 同目录参考：[`unity-code-style-guide-repo.md`](unity-code-style-guide-repo.md)、[`csharp-coding-conventions.md`](csharp-coding-conventions.md)
