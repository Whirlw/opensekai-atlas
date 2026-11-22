# 背景皮肤资源包

在 Unity 的 **OpenSekai → Dev → Material Packs → Enable background ZIP...** 选包，从曲库开局后生效。用 **Restore built-in background** 恢复游戏内置背景。

## 自定义图层

在 ZIP 根目录的 `pack.json` 中创建 `category` 为 `background` 的组。组的 `layers` 数组由后往前绘制，第一项在最底层。可只用普通图、只用封面，或在装饰图之间插入多份当前歌曲的封面。下例将同一张歌曲封面放到两个不同的位置，最后叠上前景图。

```json
{
  "schemaVersion": 1,
  "packId": "example.background",
  "version": "1",
  "groups": [{
    "id": "gallery",
    "category": "background",
    "preferVideo": true,
    "layers": [
      { "type": "image", "file": "art/back.png", "fit": "cover" },
      { "type": "jacket", "size": [420, 420], "position": [-320, 40] },
      { "type": "jacket", "size": [270, 270], "position": [340, -100], "rotation": [0, 0, -12] },
      { "type": "image", "file": "art/frame.png", "size": [1080, 680], "position": [0, 20] }
    ]
  }]
}
```

| 字段 | 用法 |
| --- | --- |
| `type` | `image` 从 `file` 读取包内 PNG。`jacket` 使用当前歌曲的封面，非方形封面先居中裁为正方形。多份封面共享纹理。 |
| `position`、`size` | 在高度为 1080 的参考画布上填写轴心位置和宽高。原点在画面中心，向右、向上为正。位置默认 `[0,0]`，省略大小时以当前视口为适配框。 |
| `fit`、`scale` | `cover` 等比铺满，`contain` 等比完整显示。无 `size` 时默认铺满，有 `size` 且省略 `fit` 时使用指定宽高。之后再乘横纵缩放，默认 `[1,1]`。 |
| `rect`、`pivot` | 普通层可按 PNG 左上原点填写整数切片 `[x,y,宽,高]`。两种层均可设置切片内的归一化轴心，按左下原点填写，默认居中。 |
| `flipX`、`flipY` | 布尔值，分别沿水平或垂直方向镜像，可用于封面倒影。默认均为 `false`。 |
| `color` | RGBA 乘色，各通道 0 至 1，默认白色且完全不透明。第四项可单独控制这一层的透明度。 |
| `rotation`、`depth` | 三轴旋转以度为单位，按 Unity 的 Z、X、Y 顺序应用。深度与位置同单位，正数远离相机。默认均为零。可用来布置倾斜的封面或倒影。 |
| `mask` | 可选的独立 PNG 遮罩对象。填入 `file`，可用 `rect`、`pivot`、`size`、`scale`、`position`、`rotation` 和 `depth` 指定它在同一画布上的布局。`cutoff` 是显示遮罩所需的 alpha 阈值，默认 0.5。需要多个可见区域时，`mask` 可填 1 至 16 个遮罩对象组成的数组，取它们的并集，仅裁剪所属图层。 |

每组可放 1 至 256 层。位置与深度范围为 −8192 至 8192，宽高大于 0 且不超过 8192，缩放大于 0 且不超过 64。单张 PNG 每边最多 2048 像素，选中组的不同 PNG 按解码后的 RGBA 共用 32 MiB 预算。重复引用同一纹理只计一次。

## MV、亮度与回退

`preferVideo` 默认是 `true`。歌曲启用 2DMV 且视频可播放时使用原有视频表面，缺视频或播放失败时显示资源包图层。填 `false` 可在 MV 曲目中也使用这套图层。合成结果沿用游戏的背景亮度和开场淡入；屏幕尺寸改变时重新合成。

开局缺少封面时跳过封面层。某个已安装的 PNG 丢失或损坏时跳过使用它的图层，遮罩失效时连同所属层一起跳过。没有可绘制图层时恢复内置封面场景。选择以整个构图为单位，同类别选择中优先级最高的组生效。

## 上游样包

本地样包为 `LocalPacks/upstream-background/32c91d9/upstream-background.zip`。同目录保存可读的 `pack.json`、生成脚本、上游文件和渲染对照。生成脚本从 `Background2DView.cs` 确定默认背景及封面来源，按预制体的渲染顺序、变换、颜色、遮罩和精灵元数据生成 10 个普通层与 6 个动态封面层。包内含底图、窗框、反射、地面渐变和遮罩共 14 张原始 PNG。每次开局从当前歌曲取封面。[上游固定版本](https://github.com/cubeww/OpenSekai/tree/32c91d90c7e18135fef1a214144cce3fb756b3e5)
