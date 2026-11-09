# CSS 命名规范（BEM）整理

> 目的：把 BEM 的 Block / Element / Modifier 三层命名规则、连字符约定、以及"类名被 JS 复用"这类事故的规避办法整理成可对照的清单，供 UI Toolkit 的 USS 类名参考。
> 适用读者：写 UXML/USS 的人，以及要给样式类定命名规则、做 review 的人。
> 前置条件：无（本文为外部资料整理）
> 最后更新：2026-11-02

## 这是什么

freeCodeCamp 上的一篇入门向文章，作者 **Emmanuel Ohans**，2018-01-16 发布。核心主张只有三件事：

1. 类名用**连字符分隔**的小写串，不要驼峰；
2. 复杂界面用 **BEM**（Block-Element-Modifier）：`block`、`block__element`、`block--modifier`；
3. 不要让**样式类名兼职当 JavaScript 选择器**，否则改个类名就悄悄把脚本打崩。

文章开篇给的理由很直白：CSS 难维护，"poorly written CSS will quickly turn into a nightmare"，而命名是成本最低的止血手段。作者也承认自己的取舍：小项目只上连字符，界面复杂了才上 BEM。

## 来源与可访问性

抓取日期统一为 **2026-11-02**，经 `web_fetch`（沙箱内 curl / Invoke-WebRequest 无法直连外网）。

| URL | 结果 | 说明 |
| --- | --- | --- |
| `https://www.freecodecamp.org/news/css-naming-conventions-that-will-save-you-hours-of-debugging-35cea737d849/` | ✅ 成功（HTTP 200，全文） | 正文完整：连字符约定、BEM 三要素、`js-` 钩子、`rel` 与 `data-*` 的争议、写注释的建议；末段是作者自己的课程/电子书推广 |

**结论**：正文已拿到。文中引用的 `getbem.com/naming/` 与 Bootstrap 源码链接**未抓取**，本文不转述它们的内容。

## 核心内容

### 1. 连字符分隔（Hyphen Delimited Strings）

驼峰是 JS 的习惯，不是 CSS 的。文章给的对照：

```css
/* ✗ 不要这样 */
.redBox { border: 1px solid red; }

/* ✓ 这样 */
.red-box { border: 1px solid red; }
```

理由有两条：可读性更好；与 CSS **属性名**的风格一致（属性是 `font-weight`，不是 `fontWeight`）。

### 2. BEM 三要素

命名规范要解决三个问题（文章原列的 3 条）：

1. 光看名字就知道这个选择器**做什么**；
2. 光看名字就知道它**能用在哪**；
3. 光看名字就知道类名之间的**关系**。

BEM 把界面拆成小块可复用组件：

| 层 | 含义 | 写法 | 文章示例 |
| --- | --- | --- | --- |
| **B**lock | 一个独立的界面块（导航、页头、页脚……） | `.block` | `.stick-man` |
| **E**lement | 块内部的子部件，不能脱离块单独存在 | `.block__element`（**两个下划线**） | `.stick-man__head`、`.stick-man__arms`、`.stick-man__feet` |
| **M**odifier | 块或元素的一种变体/状态 | `.block--modifier` / `.block__element--modifier`（**两个连字符**） | `.stick-man--blue`、`.stick-man--red`、`.stick-man__head--small`、`.stick-man__head--big` |

关键点：modifier 可以挂在**块**上（红的整个人），也可以挂在**元素**上（只换头的大小），两种都合法，写法都是 `--`；元素上的 modifier 直接接在元素名后，**不要**省略元素部分。

### 3. 常见错误写法对照

| ✗ 错误 | ✓ 正确 | 依据 |
| --- | --- | --- |
| `.redBox` | `.red-box` | 连字符约定；驼峰不适合 CSS |
| `fontWeight: 10em` | `font-weight: 10em` | CSS 属性名本身是连字符风格，写驼峰是无效声明 |
| `.stick-man__blue`（把变体写成元素） | `.stick-man--blue` | `__` 是元素，`--` 才是 modifier |
| `.stick-man__head-small`（元素名里塞连字符表达变体） | `.stick-man__head--small` | 变体必须用 `--` 显式标出，否则无法与"另一个元素"区分 |
| `.navHeader`、`.nav_header` 混用 | 全项目统一 `.nav-header` | 混用分隔符会让人分不清层级 |
| 用 `.site-navigation` 同时当 JS 选择器 | 额外加 `.js-site-navigation` | 见下节 |

### 4. 样式类名不要兼职 JS 钩子

文章讲了个"新人第一天"的故事：John 把 `class="siteNavigation"` 改成 `class="site-navigation"`，很规范，但 JS 里 `document.querySelector('.siteNavigation')` 从此返回 `null`。作者的结论是：**给需要被脚本抓的元素单独一个 `js-` 前缀的类**。

```html
<div class="site-navigation js-site-navigation"></div>
```

```javascript
const nav = document.querySelector('.js-site-navigation');
```

文章还列了另外两种见过但不推荐的做法，态度要分清：

| 做法 | 文章态度 |
| --- | --- |
| `js-` 前缀类 | 推荐 |
| `rel="js-site-navigation"` 属性 + `[rel='...']` 选择器 | 作者明确"怀疑"（"I have my doubts about this technique"），但承认会遇到 |
| 用 `data-*` 当 JS 钩子 | 作者认为"不对"，理由是 data 属性按定义是**存数据**的；文末"Edit #1"补充说评论区有人指出某些场合可以接受，**看你自己的判断** |

### 5. 附带建议：多写 CSS 注释

文章额外提了一条：CSS 里对**不显然的技巧**写注释是有回报的（不必给 `color: red` 写注释），并拿 Bootstrap 的 SCSS 源码当正面例子。这条与命名无关，但同属"降低维护成本"。

## 在本项目怎么用

本项目的 UI 现状：主 UI 走 TextMesh Pro / 传统 uGUI 路线，`uPalette`、`SoftMask`、`UIParticle` 等增强插件已按 ADR-0003 移除。本文的**直接用途是给 UI Toolkit（UXML + USS）定类名规则**，在引入 UI Toolkit 或写编辑器 UI 时生效。

1. **USS 里照抄 BEM 的三层写法**，类名形如 `chart-list`、`chart-list__item`、`chart-list__item--selected`。`__` 与 `--` 在 USS 标识符里都合法，不需要为 Unity 改写规则。
2. **块名带业务前缀**，避免与 Unity 内置控件类撞车：Unity 自带的类名统一以 `unity-` 开头（`unity-button`、`unity-label`、`unity-toggle`），自定义块用 `osk-` 或模块名开头（如 `osk-chart-list`），一眼能分清"这是谁的样式"。
3. **别用层级选择器表达结构**。`.page .card .title` 这种写法把 UXML 结构焊死在 USS 里，改结构就全崩；用 BEM 的元素类（`.card__title`）替代，这也是文章"看名字就知道关系"的落地方式。
4. **状态用 modifier，不要用内联 style**。选中态、禁用态一律 `.block__element--disabled` 这类类名切换（`AddToClassList` / `RemoveFromClassList`），而不是在 C# 里改 `style.width`；这样样式可被 USS 统一覆盖，也便于 review。
5. **`js-` 那套在 Unity 里对应"不要在 C# 里按类名找元素"**。UXML 里用 `name` 属性 + `root.Q<VisualElement>("...")` 定位，`name` 当脚本钩子、`class` 当样式钩子，两者分开——这正是文章那条建议在 UI Toolkit 里的等价做法。

## 与本站整理稿的关系

- `03-naming.md` 管 C# 侧的命名；本文补的是**样式层**的命名，两者不冲突但都要求"全项目一致"。
- 若要给 USS 定规则，建议把"块前缀 `osk-`"和"`--` 表状态"这两条直接写进 `03-naming.md` 的扩展段，而不是只活在这份外部资料里。

## 相关文档

- [外部资料整理目录说明](README.md)
- [代码规范总入口](../README.md)
- [命名约定](../03-naming.md)
- [文档索引](../../INDEX.md)
- 同批整理：[Unity 脚本模板](unity-script-templates.md)、[Unity Test Framework 手册](unity-test-framework.md)、[可扩展代码架构](architect-code-that-scales.md)、[From Pong to 15-person project](from-pong-to-15-person-project.md)
