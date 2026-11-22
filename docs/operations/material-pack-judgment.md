# 打歌资源包 · 判定文字

在 Unity 菜单 **OpenSekai → Dev → Material Packs → Enable judgment ZIP...** 选择 ZIP，下一局使用该包。**Restore built-in judgment** 恢复内置判定文字。判定、COMBO 和音效各自保存选择。

| 显示内容 | 槽位 |
| --- | --- |
| 主判定 | `judgment.miss`、`judgment.bad`、`judgment.good`、`judgment.great`、`judgment.perfect`、`judgment.auto` |
| 快慢与划键提示 | `judgment.fast`、`judgment.late`、`judgment.flickMiss` |

ZIP 根目录放 `pack.json`。下例仅替换 PERFECT，将它显示为 310 × 81 个参考像素，并在默认主判定位置上移 18 个参考像素。

```json
{
  "schemaVersion": 1,
  "packId": "example.judgment",
  "version": "1",
  "groups": [{
    "id": "labels",
    "category": "judgment",
    "slots": {
      "judgment.perfect": {
        "file": "perfect.png",
        "size": [310, 81],
        "position": [0, 18]
      }
    }
  }]
}
```

| 每槽字段 | 换算方式 |
| --- | --- |
| `file`、`rect` | PNG 路径；可选切片从图集左上角量取，顺序为 x、y、宽、高。省略切片时使用整张图。 |
| `size`、`scale` | `size` 是动画完全展开时的显示宽高，单位为高 1080 的参考像素，范围为 (0, 8192]。默认沿用该槽内置字图的尺寸。`scale` 是叠加到宽高的倍率，每轴范围为 (0, 64]。 |
| `pivot` | 字图轴心，从切片左下角归一化量取。默认继承内置字图的轴心。 |
| `position`、`offset` | `position` 指定轴心相对判定锚点的坐标，省略时继承该槽的预制体位置。`offset` 在此基础上平移。均按高 1080 的参考像素填写，向右和向上为正，每轴范围 ±8192。 |

每槽独立回退。同帧多个音符沿用最差判定的主文字及提示，自动播放开启伪装 PERFECT 时使用 `judgment.perfect`。快慢及划键提示沿用打歌设置。包的尺寸与位置在每次弹出时生效，重试沿用当局选择。
