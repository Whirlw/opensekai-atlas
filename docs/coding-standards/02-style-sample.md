# 样式样例（完整单体示例）

> 目的：给出一份把所有规范揉在一起的完整 C# 文件，新写脚本时照着它的骨架抄。
> 适用读者：要新建 `.cs` 文件的人。
> 前置条件：读过 [01-principles.md](01-principles.md)。
> 最后更新：2026-11-02

## 概述

下面这份示例由 Unity 官方风格指南整理而来（原作者 Yuumix，命名空间 `Yuumix.OdinToolkits` 是示例工具包的，不影响规范本身）。它同时演示了：命名、字段与属性的写法、特性排列、事件三段式、区域划分、嵌套类型、表达式体。

**不要逐字照搬类型名**，抄的是结构和写法。

## 完整示例

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

// 示例使用，避免警告提示
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
// ReSharper disable ConvertToAutoPropertyWhenPossible

#pragma warning disable CS0067 // 示例使用，避免警告提示

namespace Yuumix.OdinToolkits.Core.Editor
{
    public enum Direction
    {
        North,
        South,
        East,
        West
    }

    [Flags]
    public enum AttackModes
    {
        None = 0,
        Melee = 1,
        Ranged = 2,
        Special = 4,
        MeleeAndSpecial = Melee | Special
    }

    public interface IDamageable
    {
        string DamageTypeName { get; }
        float DamageValue { get; }
        bool ApplyDamage(string description, float damage, int numberOfHits);
    }

    public interface IDamageable<in T>
    {
        void Damage(T damageTaken);
    }

    public class OdinToolkitsCodeStyleExample : MonoBehaviour
    {
        const int MAX_COUNT = 100;
        static int _sharedCount;

        #region Serialized Fields

        [SerializeField]
        bool isPlayerDead;

        [SerializeField]
        PlayerStats stats;

        [Tooltip("单个特性独占一行，提升可读性")]
        [SerializeField]
        float anotherStat;

        #endregion

        // 私有字段:下划线前缀+驼峰命名法
        int _elapsedTimeInDays;
        int _maxHealth;

        public int MaxHealthReadOnly => _maxHealth;

        // 读写属性:完整get/set实现
        public int MaxHealth
        {
            get => _maxHealth;
            set => _maxHealth = value;
        }

        // 私有set属性:控制外部修改权限
        public int Health { get; private set; }

        // 自动属性:带默认值初始化
        public string DescriptionName { get; set; } = "Fireball";

        // 单语句方法:简化为表达式体
        public void SetMaxHealth(int newMaxValue) => _maxHealth = newMaxValue;

        // 事件声明:优先使用Action/Func泛型委托
        public event Action OpeningDoor;
        public event Action DoorOpened;
        public event Action<int> PointsScored;
        public event Action<CustomEventArgs> ThingHappened;

        // 事件触发方法:Raise+事件名前缀，空值检查后触发
        public void RaiseDoorOpened()
        {
            DoorOpened?.Invoke();
        }

        public void RaisePointsScored(int points)
        {
            PointsScored?.Invoke(points);
        }

        // 事件订阅方法:订阅者类名_事件触发方法名
        public void OdinToolkitsCodeStyleExample_OnDoorOpened()
        {
            Debug.Log("订阅了 OnDoorOpened 事件的方法被调用");
        }

        // 多参数方法:参数含义清晰，避免模糊命名
        public void SetInitialPosition(float x, float y, float z)
        {
            transform.position = new Vector3(x, y, z);
        }

        // 布尔返回值方法:表达式体简化，命名体现判断语义
        public bool IsNewPosition(Vector3 newPosition) => transform.position == newPosition;

        // 代码格式示例:结构清晰，嵌套层级不超过3层
        void FormatExamples(int someExpression)
        {
            var powerUps = new List<PlayerStats>();
            var dict = new Dictionary<string, List<GameObject>>();

            switch (someExpression)
            {
                case 0:
                    // 业务逻辑注释
                    break;
                case 1:
                    // 业务逻辑注释
                    break;
                case 2:
                    // 业务逻辑注释
                    break;
            }

            for (var i = 0; i < 100; i++)
            {
                DoSomething(i);
            }

            for (var i = 0; i < 10; i++)
            {
                for (var j = 0; j < 10; j++)
                {
                    DoSomething(j);
                }
            }
        }

        void DoSomething(int x) { }

        #region Nested type

        // 事件参数结构体:参数较多时用结构体整合
        public struct CustomEventArgs
        {
            public int ObjectID { get; }
            public Color Color { get; }

            public CustomEventArgs(int objectId, Color color)
            {
                ObjectID = objectId;
                Color = color;
            }
        }

        #endregion
    }

    [Serializable]
    public struct PlayerStats
    {
        public int movementSpeed;
        public int hitPoints;
        public bool hasHealthPotion;
    }
}
```

## 逐段导读

| 示例里的位置 | 演示的规则 | 细则 |
| --- | --- | --- |
| `const int MAX_COUNT = 100;` | 常量全大写 + 下划线 | [03](03-naming.md) |
| `static int _sharedCount;` | 静态私有字段同样 `_` 前缀 | [03](03-naming.md) |
| `#region Serialized Fields` | 序列化字段单独成区域，放最前 | [06](06-regions-editorconfig.md) |
| `[SerializeField] bool isPlayerDead;` | 私有字段 + `[SerializeField]`，字段名 camelCase | [05](05-attributes-serialization.md) |
| `[Tooltip]` 与 `[SerializeField]` 分两行 | 一行一个特性 | [05](05-attributes-serialization.md) |
| `int _elapsedTimeInDays;` | 私有字段 `_` + camelCase | [03](03-naming.md) |
| `public int MaxHealthReadOnly => _maxHealth;` | 只读属性用表达式体 | [03](03-naming.md) |
| `public int Health { get; private set; }` | 外部只读、内部可写时用私有 setter | [03](03-naming.md) |
| `public void SetMaxHealth(...) => ...` | 单语句方法用表达式体 | 本文 |
| `event Action<int>` | 用泛型委托，不自造委托类型 | [04](04-events.md) |
| `RaiseDoorOpened()` + `?.Invoke()` | 触发方法 `Raise` 前缀 + 空值检查 | [04](04-events.md) |
| `OdinToolkitsCodeStyleExample_OnDoorOpened()` | 订阅方法 `类名_事件名` | [04](04-events.md) |
| `SetInitialPosition(float x, float y, float z)` | 参数名要能看出含义，不用 `a`/`b` | [03](03-naming.md) |
| `IsNewPosition(...) => ...` | 布尔方法以 `Is`/`Has`/`Can` 开头 | [03](03-naming.md) |
| `switch` / 双层 `for` | 嵌套层级 ≤ 3 | [08](08-code-smells.md) |
| `#region Nested type` | 嵌套类型单独成区域 | [06](06-regions-editorconfig.md) |
| `struct CustomEventArgs` | 多参数事件用 struct 打包 | [04](04-events.md) |
| `[Serializable] struct PlayerStats` | 纯数据载体用 public 字段 | 见下方注 |

## 一处需要说清楚的例外

示例末尾的 `PlayerStats` 用的是 **public 字段**：

```csharp
[Serializable]
public struct PlayerStats
{
    public int movementSpeed;
    public int hitPoints;
    public bool hasHealthPotion;
}
```

这看起来和 [05 篇](05-attributes-serialization.md) 的"禁止 public 字段序列化"矛盾，其实管的是两件事（整理者注，非原文）：

- Unity 序列化**结构体**要求字段可见，`public` 字段 + `[Serializable]` 是标准写法；
- 05 篇禁止的是**在 `MonoBehaviour` / 普通类上**用 `public` 字段把状态直接暴露给外部改。

判断口径：**纯数据载体（无行为、只被序列化）可以用 public 字段；有行为的类一律 `private` + 属性。**

## 一处按意图更正

原文有一处笔误，按"示例要能直接抄"的目标做了最小更正，其余一字未改：

```csharp
// 原文：注释写"私有 set 属性"，代码却是私有 get + 公开 set（外部能写不能读，正好反了）
public int Health { private get; set; }

// 本文：改为私有 set，与注释一致
public int Health { get; private set; }
```

## 相关文档

- [命名规范](03-naming.md)
- [事件规范](04-events.md)
- [特性与序列化](05-attributes-serialization.md)
- [区域与 .editorconfig](06-regions-editorconfig.md)
