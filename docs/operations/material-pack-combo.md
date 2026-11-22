# 打歌资源包 · COMBO

在 Unity 的 **OpenSekai → Dev → Material Packs → Enable COMBO ZIP...** 中选择 ZIP，再从曲库开局。**Restore built-in COMBO** 会将下一局的连击显示恢复为内置样式。两条菜单命令只修改 COMBO 选择，音效选择独立保存。要查看 AP 和光圈，请开启打歌设置中的 AP 特效。

| 样式 | 标签槽 | 数字槽（用 0–9 替换 N） |
| --- | --- | --- |
| 普通 | `combo.label` | `combo.digit.N` |
| All Perfect | `combo.ap.label` | `combo.ap.digit.N` |
| AP 光圈 | `combo.glow.label` | `combo.glow.digit.N` |

ZIP 根目录放一份 `pack.json`。本例把图集左上方的一块切片用于 AP 连击的数字 1，并向上移动 18 个参考像素。未指定的数字继续使用原字形。

```json
{
  "schemaVersion": 1,
  "packId": "example.combo",
  "version": "1",
  "groups": [{
    "id": "digits",
    "category": "combo",
    "layout": {
      "position": [671.436, 84.996],
      "scale": [1, 1],
      "digitsPosition": [0, -19.44],
      "burstPosition": [0, -19.44],
      "glowPosition": [0, -19.44]
    },
    "slots": {
      "combo.ap.digit.1": {
        "file": "digits.png",
        "rect": [0, 0, 80, 144],
        "size": [80, 144],
        "offset": [0, 18]
      }
    }
  }]
}
```

PNG 支持 RGB、RGBA、灰度和索引色，每通道最多 8 位，单边最多 2048 像素。`rect` 从图像左上角量取，顺序为 x、y、宽、高，省略时使用全图。切片先适配该槽内置精灵的参考宽高。可选 `pivot` 从切片左下角归一化量取，默认继承内置轴心。`scale` 为宽高倍率，各分量大于 0、最多 64。`offset` 的正方向为右和上，按 1080 高度的参考像素填写，各分量范围为 ±8192。普通和 AP 数字还可用 `advance` 声明单个数字占用的排版宽度，取值大于 0、最多 8192 个参考像素。默认步进为 102.6；一串数字按总宽度居中，光圈跟随主字形的位置。当前 COMBO 预载预算为 32 MiB，按选中 PNG 的宽 × 高 × 4 估算，相同内容只计一次。

### 调整位置、尺寸和光圈

所有位置和显示尺寸都使用高 1080 的参考像素，向右和向上为正。同类组按优先级选择第一份显式 `layout`。布局和逐槽图片独立选择，布局组允许 `"slots": {}`。

| 字段 | 含义 |
| --- | --- |
| 组内 `layout.position`、`layout.scale` | 整个 COMBO 内容根相对于 HUD 锚点的位置，以及绕该根的横、纵缩放。默认沿用预制体。 |
| `layout.digitsPosition`、`burstPosition`、`glowPosition` | 数字行、扩散副本、光圈行各自相对内容根的位置。三个位置设为相同值可保持重合。 |
| 槽内 `size`、`scale` | `size` 为切片的显示宽高，直接指定后与 PNG 分辨率无关；`scale` 再按轴心缩放这个参考框。 |
| 标签槽 `position`、各槽 `offset` | 标签可直接指定相对内容根的轴心位置。数字的基准位置由行位置与步进决定。`offset` 在上述位置上追加偏移。 |
| 光圈槽 `opacity` | 0–1，乘以原有 AP 闪烁曲线。设为 0 可隐藏该槽光圈；设为 1 沿用完整闪烁。主字形单独配置。 |

`MaterialPackStore.ResolveCombo` 可预览各槽来源。调用 `SaveComboSelection(selection, expectedRevision)` 保存选择。和音效一样，每槽可指定一个组或显式选回内置。已安装图片在开局时损坏或缺失，引用该文件的槽会回退并进入当局诊断；退出时释放当局精灵和纹理。
