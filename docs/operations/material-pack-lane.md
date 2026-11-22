# 轨道皮肤资源包

在 Unity 菜单 **OpenSekai → Dev → Material Packs → Enable lane ZIP...** 选择 ZIP，再从曲库开局。选择 **Restore built-in lane** 可在下一局恢复内置轨道。

## 三个轨道图层

| 槽位 | 图层 | 游戏内透明度 |
| --- | --- | --- |
| `lane.base` | 轨道底板 | 轨道透明度 |
| `lane.dividers` | 分道线整图 | 判定线透明度 |
| `lane.judgeLine` | 判定线 | 判定线透明度 |

在 ZIP 根目录的 `pack.json` 中声明 `category: "lane"`。每个槽都可独立设置。下面的完整清单使用三个独立的 PNG，将判定线调整到上游预制体的位置。只想更换一个图层时，可以只写那个槽。

```json
{
  "schemaVersion": 1,
  "packId": "example.lane",
  "version": "1",
  "groups": [{
    "id": "track",
    "category": "lane",
    "slots": {
      "lane.base": { "file": "base.png", "scale": [1, 1], "position": [0, 0] },
      "lane.dividers": { "file": "dividers.png", "scale": [1, 1], "position": [0, 0] },
      "lane.judgeLine": { "file": "judge.png", "size": [1583, 84], "position": [0, -228.96] }
    }
  }]
}
```

| 字段 | 含义 |
| --- | --- |
| `file` | 必填，ZIP 内的 PNG 路径。每边最多 2048 像素。 |
| `size` | 显示宽高，单位为参考像素，108 参考像素等于轨道父节点的 1 个局部单位。省略时使用内置精灵的参考宽高。每项大于 0，最多 8192。 |
| `scale` | 在上述宽高上分别乘以横向、纵向倍数，默认 `[1, 1]`。每项大于 0，最多 64。 |
| `position` | 轴心在轨道父节点内的绝对位置，单位为参考像素，向右、向上为正。省略时沿用该图层的预制体位置，每项范围为 −8192 至 8192。 |
| `rect` | 可选的 PNG 切片，按左上原点填写整数 `[x, y, 宽, 高]`，默认使用整图。 |
| `pivot` | 切片内的归一化轴心，按左下原点填写，范围为 0 至 1。省略时沿用内置精灵的轴心。 |

布局只作用于三个视觉子节点，音符运动和触摸判定仍使用谱面坐标。选包、逐槽覆盖及缺图回退遵循资源包的常规规则。导入其他类别的包后，可以继续使用已选的轨道。

## 上游样包

本地样包位于 `LocalPacks/upstream-lane/32c91d9/upstream-lane.zip`。同目录的 `pack.json` 可直接查看。生成脚本从上游 `LaneView.cs` 确认三个渲染器，经 `FrontUIView.prefab` 的序列化引用和 GUID 找到原始精灵与 PNG，并按精灵的像素密度、轴心和子节点位置换算布局。上游使用带小数边界的切片，样包取覆盖它们的整数像素范围，并同步换算宽高和轴心。源码、原图和 ZIP 均保存在 `LocalPacks`。[上游固定版本](https://github.com/cubeww/OpenSekai/tree/32c91d90c7e18135fef1a214144cce3fb756b3e5)
