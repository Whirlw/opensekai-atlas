# 自定义 Unity 脚本模板（Script Templates）

> 目的：讲清 Unity "新建脚本"生成的内容从哪里来、模板文件怎么命名、`#SCRIPTNAME#` 占位符怎么用，以及模板不生效时按什么顺序排查。
> 适用读者：要给团队统一 MonoBehaviour 骨架（命名空间、region 分段、空方法占位）的人。
> 前置条件：无（本文为外部资料整理）
> 最后更新：2026-11-02

## 这是什么

Unity 编辑器里的 **Create > C# Script**（以及 Shader、Playable 等菜单项）不是硬编码生成代码的，而是从一组**纯文本模板文件**渲染出来的：文件名决定它出现在哪个菜单、新建出来的默认文件名是什么，文件内容就是新脚本的初始文本，其中的 `#SCRIPTNAME#` 在创建时被替换成实际文件名。

模板有两级作用域：

| 作用域 | 位置 | 影响范围 | 备注 |
| --- | --- | --- | --- |
| 编辑器级（全局） | `<Unity 安装目录>/Editor/Data/Resources/ScriptTemplates` | 该机器该版本的所有项目 | 文章主讲的方案；升级 Unity 会被覆盖 |
| 项目级 | `Assets/ScriptTemplates` | 只影响本项目，随仓库分发 | 文章末尾一句 Note 提到："you can allocate your scripting templates on a single project by creating a ScriptTemplates folder on the Assets and following the previous steps" |

对本项目而言，**项目级才是可选项**：全局模板改的是本机安装目录，团队里每个人的机器各改各的，无法 review、无法进版本库。

## 来源与可访问性

抓取日期统一为 **2026-11-02**，全部经 `web_fetch`（沙箱内 curl / Invoke-WebRequest 无法直连外网）。

| URL | 结果 | 说明 |
| --- | --- | --- |
| `https://support.unity.com/hc/en-us/articles/210223733-How-to-customize-Unity-script-templates` | ❌ 未获取正文 | 跨域重定向死循环：先跳 `unity3d.zendesk.com`，再跳回 `support.unity.com`，`web_fetch` 拒绝自动跟随；加 `?mobile_site=false` 同样失败 |
| `https://web.archive.org/web/20250120093009id_/https://support.unity.com/hc/en-us/articles/210223733-How-to-customize-Unity-script-templates` | ✅ 成功（HTTP 200，全文） | 2025-01-20 的 Wayback 快照，正文完整（作者 Iain Gilfeather，2024-02-20 更新） |
| `https://support.unity.com/hc/en-us/articles/4403342550548-How-to-customize-Unity-script-templates-on-Unity-Project` | ❌ 失败 | 原文给出的"项目内模板"延伸阅读链接，现行站点与 Wayback 均 404（该文已下线） |
| `https://raw.githubusercontent.com/hubertgdev/unity-script-templates/master/README.md` | ✅ 成功（HTTP 200，全文） | 社区模板仓库，用于**交叉验证**文件命名各字段含义（二手来源，已在下文标注） |

**结论**：主文章正文已拿到（Wayback 快照）。命名规则中"序号 / 菜单名 / 子菜单 / 默认文件名 / 扩展名"的逐字段解释，主文章只给了实例未给表格，该表格来自上面的社区 README，属**二手来源**；正文中已逐处标注哪些是文章原文、哪些是补充。

## 核心内容

### 1. 模板文件在哪

文章给的路径（原文照录）：

- Windows：`C:\Program Files\Unity\Editor\Data\Resources\ScriptTemplates`
- Mac：`/Applications/Unity/Editor/Data/Resources/ScriptTemplates`
- Mac（5.2.1f1 起）：`/Applications/Unity/Unity.app/Contents/Resources/ScriptTemplates`

文中也用 `%EDITOR_PATH%\Data\Resources\ScriptTemplates` 概括。**补充（非原文）**：用 Unity Hub 装的多版本，路径形如 `C:\Program Files\Unity\Hub\Editor\<版本号>\Editor\Data\Resources\ScriptTemplates`，改之前先确认改的是当前实际启动的那个版本。

### 2. 文件命名规则

文章列出的实际文件（原文照录）：

```
81-C# Script-NewBehaviourScript.cs.txt
82-Javascript-NewBehaviourScript.js.txt
83-Shader__Standard Surface Shader-NewSurfaceShader.shader.txt
84-Shader__Unlit Shader-NewUnlitShader.shader.txt
```

从上例可以稳定读出的结构（前四个字段的解释参考社区 README，属二手来源）：

```
<序号>-<菜单名>[__<子菜单>...]-<新建文件的默认名>.<扩展名>.txt
 81      C# Script                          NewBehaviourScript      .cs
 83      Shader   __ Standard Surface Shader NewSurfaceShader        .shader
```

| 字段 | 含义 | 示例 |
| --- | --- | --- |
| 序号 | 菜单里的排序位（决定 `Assets > Create` 下的先后） | `81`、`83` |
| 菜单名 | 一级菜单项名字；`__` 表示再下一级子菜单，可叠加多层 | `Shader__Standard Surface Shader` → `Create > Shader > Standard Surface Shader` |
| 默认文件名 | 新建出来时预填的文件名（用户可改） | `NewBehaviourScript` |
| 扩展名 | 生成文件的类型 | `.cs`、`.shader` |
| `.txt` 后缀 | **必须有**，否则编辑器不把它当模板 | — |

分隔符要分清：**`-` 只出现两次语义位置**（序号后、默认名前），**`__`（双下划线）用于菜单层级**。把菜单名里塞单个连字符会切错字段。

### 3. `#SCRIPTNAME#` 占位符

文章原话的要点：

> "You can change anything you want within the script, but remember to leave *#SCRIPTNAME#* where it is. Without it, your template class name will not change, and a new script file will be generated with an incorrect class name."

即：`#SCRIPTNAME#` 在创建时被替换成文件名（不含扩展名），模板里通常写成 `public class #SCRIPTNAME# : MonoBehaviour`。删掉占位符的后果不是"报错"，而是**类名与文件名不一致**——Unity 认为脚本里应当有一个与文件同名的类，于是新脚本立刻处于"类名对不上"的状态，`AddComponent` 也找不到类型。

**重命名时会发生什么**：模板里保留 `#SCRIPTNAME#` 的脚本，类名天然等于文件名，之后在 Project 窗口改文件名，Unity 会同步把类名改成新名字；反过来，如果把类名在模板里**写死**（例如 `public class Foo`），新建出来叫 `Bar.cs` 的文件里类名仍是 `Foo`，之后重命名文件类名也不会跟着变，只能手工维护。这条"重命名同步"是编辑器通用行为，非文章原文。

### 4. 最小可用模板（基于原文示例改写）

文章给的默认 C# 模板内容（去掉了原文粘贴产生的排版噪声）：

```csharp
using UnityEngine;
using System.Collections;

public class #SCRIPTNAME# : MonoBehaviour
{
    // Use this for initialization
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
    }
}
```

文章还示范了一版"改过的模板"，思路是加注释头 + `#region` 分段：

```csharp
/* Modified template by Unity Support. */
using UnityEngine;

public class #SCRIPTNAME# : MonoBehaviour
{
    #region Public Fields
    #endregion

    #region Unity Methods
    void Start()
    {
    }

    void Update()
    {
    }
    #endregion

    #region Private Methods
    #endregion
}
```

### 5. 生效与排查

文章明确给出的操作与坑：

| 现象 / 步骤 | 处理 |
| --- | --- |
| 改完模板没反应 | **重启 Unity 编辑器**（原文："please relaunch the Unity Editor to apply these changes"），模板只在启动时读入 |
| 模板"不被识别"，新脚本还是老内容 | 原文建议保留原文件备份后重来（"You will need original files if your template is not recognized correctly. If your template isn't recognized, you will have to start again."） |
| 升级 Unity 后自定义内容消失 | 原文："When you upgrade your Unity version, the template files will be overwritten"，需要重新拷入；因此**改好的模板要在 Unity 目录之外另存一份** |
| 只想影响一个项目 | 用 `Assets/ScriptTemplates`，不要改安装目录 |

**补充排查顺序（非原文，按代价从低到高）**：

1. 文件名是否以 `.txt` 结尾、字段分隔是否用对了 `-` 与 `__`；
2. 是否放在 `Assets/ScriptTemplates`（项目级）而不是 `Assets/ScriptTemplates/xxx/` 子目录里——项目级模板只认这一层；
3. 文件编码是否为 UTF-8（无 BOM），BOM 会跑到生成文件开头；
4. 重启编辑器（全局模板必须，项目级模板改完加文件通常也会触发重扫，但不保证）；
5. 序号是否与内置模板撞号（撞号可能被覆盖或顺序异常），自定义序号建议从 `01` 起、避开 `81`–`84` 这类内置号段。

## 在本项目怎么用

本项目现状（实测）：`Assets/ScriptTemplates` **不存在**；`Assets/Scripts/Assembly-CSharp` 下约 370 个脚本统一在 `namespace Sekai.*` 里，编辑器脚本在 `Assets/Editor`。

1. **建项目级模板，别改安装目录**。新建 `Assets/ScriptTemplates/`，放一个 `01-OpenSekai__MonoBehaviour-MonoBehaviour.cs.txt`（菜单落在 `Create > OpenSekai > MonoBehaviour`），随仓库提交，改动可 review、可回滚。
2. **模板里预置命名空间与 region 骨架**，和 `06-regions-editorconfig.md` 的分段约定对齐，例如：

   ```csharp
   // <auto-generated by template>
   using UnityEngine;

   namespace Sekai.ChangeMe
   {
       public class #SCRIPTNAME# : MonoBehaviour
       {
           #region Public Fields
           #endregion

           #region Unity Methods
           #endregion

           #region Private Methods
           #endregion
       }
   }
   ```

   命名空间这一行**无法**用 `#SCRIPTNAME#` 自动填（占位符只有文件名一个值），所以模板只能给 `Sekai.ChangeMe` 这类占位命名空间，新建后改一次——比从零打骨架省事，也不会假装能自动完成。
3. **类名永远交给 `#SCRIPTNAME#`**，模板里不要出现写死的类名，否则重命名脚本时类名不会跟随，直接破坏"类名 = 文件名"这条 Unity 硬约束。
4. **别指望模板管住格式**。模板只决定初始文本，后续格式靠 `.editorconfig` 与 IDE 格式化（见 `06-regions-editorconfig.md`）；模板里塞太多空 region 反而制造噪声。
5. **新增模板后同步登记**：改动落在 `Assets/ScriptTemplates/`，属于团队共用资产，按 `../../INDEX.md` 的规矩在本目录留一行可查记录。

## 与本站整理稿的关系

- 模板预置的 `#region` 分段与空方法顺序，是 `06-regions-editorconfig.md` 的**落地载体**：规范说"怎么分段"，模板负责"新建时就分好"。
- 模板里的文件名/类名一致性要求，与 `03-naming.md` 的命名条款同源（Unity 硬约束优先于风格偏好）。

## 相关文档

- [外部资料整理目录说明](README.md)
- [代码规范总入口](../README.md)
- [Region 与 .editorconfig 约定](../06-regions-editorconfig.md)
- [命名约定](../03-naming.md)
- [文档索引](../../INDEX.md)
- 同批整理：[Unity Test Framework 手册](unity-test-framework.md)、[CSS 命名规范（BEM）](css-naming-conventions.md)、[可扩展代码架构](architect-code-that-scales.md)、[From Pong to 15-person project](from-pong-to-15-person-project.md)
