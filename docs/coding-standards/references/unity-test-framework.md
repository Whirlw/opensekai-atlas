# Unity Test Framework（UTF）手册整理

> 目的：用够用的篇幅讲清 EditMode / PlayMode 测试的区别、测试程序集（asmdef + Test Assemblies）的引用要求、`[Test]` / `[UnityTest]` 的写法，以及命令行跑测试的入口参数。
> 适用读者：要给项目接第一批单元测试、或要把测试塞进构建流水线的人。
> 前置条件：无（本文为外部资料整理）
> 最后更新：2026-11-02

## 这是什么

Unity Test Framework（UTF）就是原来 **Unity Test Runner** 的包化形态（原文原话："UTF is not a new concept or toolset; it is an adjusted and more descriptive naming for the toolset otherwise known as Unity Test Runner"）。它是 **NUnit** 的 Unity 集成，当前基于 **NUnit 3.5**，支持在 **Edit Mode**、**Play Mode** 以及 Standalone / Android / iOS 等目标平台上跑测试。

- 包名：`com.unity.test-framework`，包管理器里显示名 `Test Framework`；
- 随编辑器分发，Unity 2019.2 及以后默认已在项目里；
- 定位：Unity 官方与用户**共用同一套**测试写法的标准框架。

## 来源与可访问性

抓取日期统一为 **2026-11-02**，经 `web_fetch` 抓取。包文档站可直接访问，无需登录。

| URL | 结果 | 说明 |
| --- | --- | --- |
| `https://docs.unity3d.com/Packages/com.unity.test-framework@1.4/manual/index.html` | ✅ 成功（HTTP 200） | 概览：定位、NUnit 3.5、安装、1.3.x 已知限制；页面标注版本 **1.4.6** |
| `https://docs.unity3d.com/Packages/com.unity.test-framework@1.4/manual/manual.html` | ✅ 成功（HTTP 200） | 手册目录（Introduction / Getting started / Extending / Reference 四块），用于确认还有哪些子页 |
| `.../manual/edit-mode-vs-play-mode-tests.html` | ✅ 成功（HTTP 200） | EditMode / PlayMode 的判定条件与 asmdef JSON 示例 |
| `.../manual/workflow-create-test-assembly.html` | ✅ 成功（HTTP 200） | 测试程序集创建流程与 Inspector 里的引用项 |
| `.../manual/reference-command-line.html` | ✅ 成功（HTTP 200） | 命令行参数全表 |

**结论**：正文已拿到，本文只摘录与"写测试 / 接流水线"直接相关的内容，不抄手册目录。**未抓取**：`getting-started.html`、`workflow-create-test.html`、`workflow-run-test.html`、`reference-attribute-unitytest.html` 等子页，下文凡涉及这几页的细节都以已抓页面为准，未展开的部分明确不写。

## 核心内容

### 1. EditMode 与 PlayMode 的差别

| 维度 | Edit Mode（又叫 Editor tests） | Play Mode |
| --- | --- | --- |
| 运行环境 | **只在编辑器里**，且能访问 Editor 代码 + 游戏代码 | 编辑器里跑，或打成 **standalone Player** 跑 |
| 代码运行时机 | 在 `EditorApplication.update` 回调循环里跑 | 按协程跑（标了 `UnityTest` 的测试） |
| 典型用途 | 测 Editor 扩展、纯逻辑、资源导入校验 | 测真实游戏代码：MonoBehaviour 生命周期、物理、跨帧行为 |
| 能否进/出 Play Mode | **可以**，测试里能主动进入/退出 Play Mode，从而"先改状态再进播放" | 本身就在播放态 |
| asmdef 条件 | 引用 `nunit.framework.dll`，且 `"includePlatforms": ["Editor"]` | 引用 `nunit.framework.dll`；`"optionalUnityReferences": ["TestAssemblies"]`，`"includePlatforms": []` |
| 旧式（无 asmdef）替代 | 把测试放进项目的 `Editor` 文件夹即可 | 无此捷径，必须有测试程序集 |

原文给的两段 asmdef 片段（照录）：

```json
// Edit Mode
"includePlatforms": [
    "Editor"
]
```

```json
// Play Mode
"references": [
    "NewAssembly"
],
"optionalUnityReferences": [
    "TestAssemblies"
],
"includePlatforms": [],
```

Play Mode 还额外要求：测试脚本必须和 `.asmdef` 在**同一个文件夹**里，并且测试程序集要**引用被测代码所在的程序集**。

### 2. 测试程序集（Test Assembly）与 `Test Assemblies` 标记

- **判定标准很朴素**：所谓 Test Assembly，就是**任何引用了 NUnit 的程序集**（原文："UTF looks for tests in a Test Assembly, which is any assembly that references Nunit."）。
- **创建方式**（两条等价路径）：
  1. `Window > General > Test Runner` → 点 **Create a new Test Assembly Folder in the active path**；
  2. `Assets > Create > Testing > Test Assembly Folder`。
  两者都会生成一个 `Tests` 文件夹 + 配套 `.asmdef`，并自动加上 **nunit.framework.dll**、**UnityEngine.TestRunner**、**UnityEditor.TestRunner** 三个引用——这就是"这是个测试程序集"的信号。
- **`UnityEditor.TestRunner` 只在 Edit Mode 可用**（原文 Note）。所以想让同一套测试在 Player 上跑，不能依赖它。
- **Platforms 勾选决定能不能上真机**：默认只勾 **Editor**；勾 **Any Platform** 或某个具体平台后，该程序集里的 Play Mode 测试才能跑到 standalone Player 上。
- **重命名坑**：改 `.asmdef` 的**文件名**不会改文件里的 `Name` 属性，要么用 Inspector 改，要么直接编辑 JSON；每个程序集名必须唯一。

关于任务里点名的 `UNITY_INCLUDE_TESTS`：页面本身没有出现这个宏，但它是上面 `TestAssemblies` 标记在代码侧的可见形式——测试程序集只在"编辑器 / 含测试的构建"里编译，Unity 用 `UNITY_INCLUDE_TESTS` 把这一状态暴露给 C#，因此常见写法是把测试专用代码包在 `#if UNITY_INCLUDE_TESTS` 里，让 Player 构建把它裁掉。**这段属 Unity 通用行为与社区实践，不是该手册页的原文**，接入前建议在自己的构建里验证一次。

### 3. `[Test]` 与 `[UnityTest]` 基本写法

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;   // [UnityTest]、LogAssert 等

public class ScoreTests
{
    // 同步测试：最常见的形态
    [Test]
    public void CumulativeScore_AllPerfect_EqualsMax()
    {
        var score = ScoreCalculator.Calculate(hitCount: 100, rulesVersion: 2);
        Assert.AreEqual(1_000_000, score);
    }

    // 需要跨帧 / 等待时的写法：返回 IEnumerator，用 yield 推进
    [UnityTest]
    public IEnumerator Paddle_MovesAfterOneFrame()
    {
        var go = new GameObject("paddle");
        go.AddComponent<Paddle>();
        yield return null;                       // 跳过一帧
        Assert.IsNotNull(go.GetComponent<Paddle>());
    }
}
```

选型建议（原文 Recommendations 一节）：**除非需要 yield 特殊指令、需要跳帧或等待时间，否则优先用 NUnit 的 `[Test]`，而不是 `[UnityTest]`**。

### 4. 已知限制（1.3.x 列表，页面照录）

- `UnityTest` 不支持 WSA 平台；
- `UnityTest` 不支持参数化测试（`ValueSource` 除外）；
- `UnityTest` 不支持 NUnit 的 `Repeat` 特性；
- 嵌套的 test fixture 无法从编辑器 UI 运行；
- Play Mode 测试里用 NUnit 的 `Retry` 会抛 `InvalidCastException`。

### 5. 命令行入口

最小示例（原文，Windows）：

```bash
Unity.exe -runTests -batchmode -projectPath PATH_TO_YOUR_PROJECT -testResults C:\temp\results.xml -testPlatform PS4
```

必须配 `-batchmode`，否则会等人工输入。常用参数：

| 参数 | 作用 | 备注 |
| --- | --- | --- |
| `-runTests` | 跑测试的总开关 | 必填 |
| `-batchmode` | 无界面批处理 | 必填 |
| `-projectPath` | 工程路径 | 必填 |
| `-testPlatform` | `EditMode` / `PlayMode` / 任意 `BuildTarget` 枚举值 | **不填默认 EditMode**；给具体平台 = 在对应 Player 上跑 Play Mode |
| `-testResults` | 结果文件路径 | NUnit XML 格式；**默认写到工程根目录** |
| `-testFilter` | 测试全名列表（分号分隔）或正则 | 支持 `!` 取反；参数化变体可写 `"ClassName\.MethodName\(Param1,Param2\)"` |
| `-testCategory` | 按 `[Category]` 过滤（分号分隔或正则） | 支持 `!` 取反；与 `testFilter` 同时给时取**交集** |
| `-assemblyNames` | 只跑指定测试程序集（分号分隔） | — |
| `-testSettingsFile` | 指向 `TestSettings.json` | 进阶配置 |
| `-orderedTestListFile` | 按 `.txt` 里列出的顺序执行 | 每行一个全名，可带参数 |
| `-randomOrderSeed` | 非 0 整数，随机化执行顺序 | 同 seed 可复现 |
| `-retry` / `-repeat` | 失败重试次数 / 成功重复次数 | 取先满足者 |
| `-runSynchronously` | 所有测试压在一次编辑器 update 里 | **仅 EditMode**，且会过滤掉 `[UnityTest]`、含 `[UnitySetUp]` / `[UnityTearDown]` 的多帧测试 |
| `-playerHeartbeatTimeout` | 等 Player 心跳的秒数 | 默认 10 分钟 |
| `-forgetProjectPath` | 不把该工程写进 Hub 历史 | 流水线里建议加 |

**退出码别当判据**：原文明确说 "There is currently no common definition for exit codes reported by individual Unity components under test"，只能靠结果 XML 和错误信息/堆栈判断成败。这一点直接影响 CI 脚本怎么写——必须解析 XML，而不是看 `%ERRORLEVEL%`。

## 在本项目怎么用

现状（实测）：`Assets` 下只有 3 个 asmdef（`CriMw.CriWare.Editor`、`CriMw.CriWare.Runtime`、`ZString`），**没有任何 Tests 文件夹或测试程序集**；主代码在 `Assets/Scripts/Assembly-CSharp`（预定义程序集 `Assembly-CSharp`），约 370 个脚本在 `namespace Sekai.*`。

1. **先测纯逻辑，别一上来测 MonoBehaviour**。计分（ADR-0004 的累积摊分公式）、谱面解析（`.oskp` → `ChartDocument`）、设置项换算这类无引擎依赖的代码最适合第一批 `[Test]`。
2. **注意 asmdef 与预定义程序集的引用方向**。asmdef 化的测试程序集**不能**引用 `Assembly-CSharp` 这类预定义程序集，而本项目主代码恰好都在 `Assembly-CSharp` 里。零改造的可行路径是：把 EditMode 测试放进 `Assets/Editor/Tests/`（走旧式条件，`Assembly-CSharp-Editor` 天然能看见 `Assembly-CSharp` 的代码）；想走 asmdef 正规路线，就得先给被测代码加 asmdef，把依赖方向掰正。
3. **只跑得快的那批进流水线**。用 `[Category("Fast")]` 标纯逻辑测试，CI 里 `-testPlatform EditMode -testCategory "Fast"`；涉及资源和场景的 PlayMode 测试留到 nightly，避免每次提交都起 Player。
4. **CI 判据用 XML 不用退出码**。脚本里解析 `-testResults` 产出的 NUnit XML，统计 `failed`/`errors` 计数；同时把 XML 当构建产物存档。
5. **别为了跑测试勾 Any Platform**。当前没有真机测试需求时，测试程序集保持只勾 Editor，少一份构建风险。

## 与本站整理稿的关系

- `07-design-practices.md` 里"逻辑与表现分离"的主张，正好是"能被 `[Test]` 覆盖"的前提：把逻辑从 MonoBehaviour 里挪出来，测试才不需要起场景。
- `architect-code-that-scales.md`（同批整理）第四节"流程自动化"给了同一套建议的上游出处。

## 相关文档

- [外部资料整理目录说明](README.md)
- [代码规范总入口](../README.md)
- [设计实践](../07-design-practices.md)
- [文档索引](../../INDEX.md)
- 同批整理：[Unity 脚本模板](unity-script-templates.md)、[CSS 命名规范（BEM）](css-naming-conventions.md)、[可扩展代码架构](architect-code-that-scales.md)、[From Pong to 15-person project](from-pong-to-15-person-project.md)
