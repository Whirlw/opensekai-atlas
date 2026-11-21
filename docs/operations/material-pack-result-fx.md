# 结算特效资源包

在 Unity 菜单 **OpenSekai → Dev → Material Packs → Enable resultFx ZIP...** 中选择 ZIP，然后从曲库开局。选择 **Restore built-in resultFx** 后，下一局使用内置演出。结算音效由独立的 `audio` 组控制。

## 调整模板

在 ZIP 根目录的 `pack.json` 声明 `category: "resultFx"`。下面将 FULL COMBO 缩小到 80%，向上移 54 个参考像素，并更换粒子纹理。仅调整位置或比例时，ZIP 可以只含清单。

```json
{
  "schemaVersion": 1,
  "packId": "example.results",
  "version": "1",
  "groups": [{
    "id": "concert",
    "category": "resultFx",
    "slots": {
      "resultFx.fullCombo.layout": { "position": [0, 54], "scale": [0.8, 0.8] },
      "resultFx.fullCombo.particles": { "file": "sparkles.png" }
    }
  }]
}
```

每种结果有独立的 `.layout`。位置以结算视图中心为原点，向右、向上为正，每 108 个参考像素等于一个局部单位。`position` 的两个分量各为 −8192 到 8192，`scale` 是相对于模板的横纵倍率，大于 0 且最多 64。模板随游戏的动画时钟播放，逐字入场、粒子发射、淡入以及结算时长沿用原逻辑。

下表列出模板部件。槽名为 `resultFx.<结果>.<部件>`，例如 `resultFx.clear.L1`。同名的上下描边共用 `line`，字母后的数字区分同一横幅内的字形。

| 结果 | 图片和光效 | 字形 |
| --- | --- | --- |
| `failure` 失败 | `line` | `text` 整幅文字 |
| `lifeZero` 生命耗尽 | `line`、`flare`、`particles` | `L1 I V E1 F I2 N I3 S H` |
| `clear` 通关 | `line`、`flare`、`particles` | `L1 I V E1 C1 L2 E2 A R bang` |
| `fullCombo` 全连 | `flare`、`circle`、`particles`、`mesh` | `F U L1 L2 C O1 M B O2 bang` |
| `allPerfect` 全完美 | `line`、`flare`、`glow`、`particles`、`mesh` | `A L1 L2 P E1 R F E2 C T bang1 bang2` |

普通图片槽填写 `file`，可以指定 `rect`（从图左上角量起的整数 `[x,y,w,h]`）、`pivot`（从切片左下角量起，范围 0–1）、`size`（参考像素宽高）和 `scale`。省略尺寸和轴心时，使用模板精灵的显示尺寸和轴心。字形继续跟随各自的动画节点。粒子和网格槽使用整张 PNG，只需 `file`，模板保留 UV 动画和混合方式。

共享槽 `resultFx.background` 替换结算时铺在演出后面的遮罩，支持普通图片字段及 `position`。遮罩保留预制体的父级缩放，尺寸和位置写在父节点的局部坐标中。自定义 PNG 保留自身颜色，游戏将遮罩透明度过渡至 50%。

## 替换整段演出

在 `slots` 中直接使用结果名，例如下面的 `resultFx.allPerfect`。它从中心向上移 32 个参考像素，用 384×96 的两格图集显示两帧。

```json
{
  "resultFx.allPerfect": {
    "kind": "sequence",
    "position": [0, 32],
    "scale": [1.25, 1.25],
    "layers": [{
      "file": "perfect.png",
      "anchor": "center",
      "size": [768, 192],
      "offset": [0, 0],
      "frames": [
        { "rect": [0, 0, 192, 96], "durationMs": 120 },
        { "rect": [192, 0, 192, 96], "durationMs": 900 }
      ]
    }]
  }
}
```

每段可有 1–8 层，按数组顺序从后往前绘制，各层可以设置 `size`、`scale`、`offset` 和 `pivot`。省略 `size` 时，每帧以切片的像素宽高作为参考尺寸。每层 1–256 帧，每帧时长为 1–60000 毫秒。各层从同一时刻起播一次，播完即隐藏。游戏原有的退场时刻可以提前结束演出。

完整序列优先于同结果的模板及其布局。已安装序列的任一层丢失或损坏时，游戏回退到该结果的模板，仍应用可用的模板部件和布局。禁用结算演出、重开、卸载或重复触发会清理正在播放的实例。单张 PNG 最大 2048×2048，同类选中纹理按内容去重后共享 32 MiB 解码预算。

## 上游样包和验证

本地目录 `LocalPacks/upstream-result-fx/32c91d9/` 包含 `upstream-result-fx.zip`、可读清单、生成脚本、源文件哈希和渲染对照。生成器从 `LiveResultView.cs` 提取五种结果与预制体的映射，再读取精灵切片、像素密度、轴心、变换和粒子材质引用。共享遮罩按上游内置白色精灵的黑色着色生成。样包有 65 个槽位、18 张 PNG。[上游固定版本](https://github.com/cubeww/OpenSekai/tree/32c91d90c7e18135fef1a214144cce3fb756b3e5)

`comparison.png` 并排展示五种结算。两侧在同一 Unity 相机下采样相同的动画时刻，粒子使用固定随机种子。左侧使用与上游一致的预制体、原始精灵网格及上游纹理，右侧从 ZIP 清单绑定同一套演出。目录中另存 0.4 秒和 1.1 秒的原尺寸画面及像素差异统计。

原始模板里有些透明部件处于同一深度和排序值，绘制先后会影响重叠的字边。另用固定的同层顺序复核了 10 组画面，960×540 下每个像素的最大颜色通道差为 3/255。
