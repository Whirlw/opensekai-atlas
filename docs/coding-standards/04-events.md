# 事件规范

> 目的：统一事件的声明、触发、订阅三段写法，避免空引用和"不知道谁在触发"。
> 适用读者：要写事件、订阅事件的人。
> 前置条件：读过 [03-naming.md](03-naming.md)。
> 最后更新：2026-11-02

## 概述

事件在本规范里被拆成固定的三段：**声明 → 触发 → 订阅**，每段都有固定的命名和写法。照做即可，不要临场发挥。

## 3.1 声明：优先泛型委托

```csharp
// 正确：用 Action / Func
public event Action OpeningDoor;
public event Action DoorOpened;
public event Action<int> PointsScored;
public event Action<CustomEventArgs> ThingHappened;

// 避免：自造委托类型
public delegate void DoorOpenedHandler();
public event DoorOpenedHandler DoorOpened;
```

规则：**优先 `event Action<T>` 或 `event Func<T>` 泛型委托，减少自定义委托的使用。**

理由：自定义委托类型多一个名字要维护，还要多写一个 `delegate` 声明；`Action`/`Func` 是 BCL 标准类型，任何 C# 开发者都认识。

## 3.2 触发：`Raise` + 事件名，必须空值检查

```csharp
public void RaiseDoorOpened()
{
    DoorOpened?.Invoke();
}

public void RaisePointsScored(int points)
{
    PointsScored?.Invoke(points);
}
```

两条硬规则：

1. **方法名 = `Raise` + 事件名**（`DoorOpened` → `RaiseDoorOpened`）。看到 `Raise` 就知道这个方法会广播事件。
2. **触发前必须 `?.Invoke()`**。事件字段在没有任何订阅者时是 `null`，直接 `DoorOpened()` 会抛 `NullReferenceException`。

`?.Invoke()` 还有一层好处：它先把委托快照下来再调用，触发过程中有订阅者退订也不会影响本次广播。

## 3.3 订阅：`订阅者类名_事件触发方法名`

```csharp
// 在订阅者类里
public void OdinToolkitsCodeStyleExample_OnDoorOpened()
{
    Debug.Log("订阅了 OnDoorOpened 事件的方法被调用");
}
```

规则：**订阅外部类的事件时，处理方法的命名为 `订阅者类名_事件触发方法名`。**

- `OdinToolkitsCodeStyleExample` 是订阅者类名；
- `OnDoorOpened` 对应被订阅的触发方法 `RaiseDoorOpened`（去掉 `Raise`，换成 `On`）。

好处：在 Rider / VS 里搜方法名，能立刻反查"是哪个类订阅了哪个事件"，不用翻 `+=` 的调用点。

## 3.4 参数：多个参数用 `struct` 打包

```csharp
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
```

规则：**事件需要传递多个参数时，优先用 `struct` 结构体整合，避免方法参数过多。**

对比：

```csharp
// 不好：三个参数，加一个就要改所有订阅者
public event Action<int, Color, string> ThingHappened;

// 好：一个参数，加字段不破坏订阅方
public event Action<CustomEventArgs> ThingHappened;
```

用 `struct` 而不是 `class` 的理由：事件参数通常是短命的小对象，`struct` 不产生 GC 分配；如果参数里有引用类型字段（如上面的 `Color` 是值类型，`string` 是引用），仍然要注意别在热路径高频触发。

> 如果参数后来变复杂、需要继承或需要可空语义，再改成 `class`，不必教条。

## 与原版示例的差异

Unity-Code-Style-Guide 仓库里的原始示例，**触发方法用的是 `OnDoorOpened`**（`On` + 事件名），处理方法是 `MySubject_DoorOpened`；本站整理稿（来自 Unity 中国社区文章）统一改成 **`Raise` + 事件名**。

两者都对，但本项目选 `Raise`，理由是：`On` 前缀在 Unity 里已经被 `OnEnable`/`OnCollisionEnter` 这类**引擎消息方法**和 UI 回调占用，再用 `On` 当"广播者"会分不清"谁调谁"。改用 `Raise` 后：

- `RaiseDoorOpened()` —— 我是广播方；
- `OnDoorOpened()` —— 我是接收方（引擎/UI 回调）；
- `类名_DoorOpened()` —— 我是订阅者，处理外来事件。

原版写法见 [references/unity-code-style-guide-repo.md](references/unity-code-style-guide-repo.md)。

## 完整对照

| 环节 | 写法 | 示例 |
| --- | --- | --- |
| 声明 | `public event Action<T> 名字;` | `public event Action<CustomEventArgs> ThingHappened;` |
| 触发 | `public void Raise名字(...)` + `?.Invoke()` | `public void RaiseDoorOpened()` |
| 订阅方法 | `订阅者类名_On事件名` | `OdinToolkitsCodeStyleExample_OnDoorOpened()` |
| 参数 | `struct` 打包 | `CustomEventArgs` |

## 相关文档

- [命名规范](03-naming.md)
- [样式样例](02-style-sample.md)
- [代码陷阱](08-code-smells.md)
