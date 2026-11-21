# 音符皮肤资源包

音符皮肤使用 ZIP，根目录放 UTF-8 的 `pack.json`，图片使用 PNG。在 Unity 菜单选择 **OpenSekai → Dev → Material Packs → Enable notes ZIP...**，选中 ZIP 后开始下一局即可试用。用同一菜单下的 **Restore built-in notes** 恢复当前设置所选的内置皮肤。可以只提供一个槽位，其余音符从已选组或当前内置样式取图。

## 调整大小和位置

下面的清单把普通点按音头加宽 10%、压低高度到 90%，向右移动四分之一条轨道，并把同时线厚度减半。将两个 PNG 放在对应路径即可使用。

```json
{
  "schemaVersion": 1,
  "packId": "example.note-skin",
  "version": "1",
  "groups": [{
    "id": "keys",
    "category": "notes",
    "slots": {
      "notes.notes_2": {
        "file": "images/tap.png",
        "scale": [1.1, 0.9],
        "position": [0.25, 0]
      },
      "notes.line.simultaneous": {
        "file": "images/simultaneous.png",
        "scale": [1, 0.5]
      }
    }
  }]
}
```

| 字段 | 单位与行为 |
| --- | --- |
| `file` | 必填。ZIP 内的 PNG 路径，区分大小写。同图可用于多个切片。 |
| `scale` | 相对内置绘制尺寸的横、纵倍率，默认 `[1,1]`，两项均大于 0 且最大为 64。音头继承谱面的动态键宽和九宫格边界。 |
| `position` | 相对原位置的横、纵偏移，默认 `[0,0]`，单位为判定线处的一条轨道宽度，正值向右、向上。远处音符的位移随透视缩小。 |
| `rect` | 精灵在 PNG 内的 `[左,上,宽,高]`，单位为像素，可含小数。省略时使用整图。 |
| `pivot` | 精灵切片中的归一化轴心，左下为 `[0,0]`，右上为 `[1,1]`。省略时继承对应内置精灵的轴心。 |

音头的根节点带动箭头、摩擦图标等子节点。要单独移动箭头或图标，在它自己的槽位填写 `position`。子节点沿原动画的局部坐标移动，斜划会随方向旋转和镜像。两套内置皮肤均可使用同一组配置。布局只影响绘制，触摸区、音符轨道和判定时刻仍由谱面决定。

## 槽位

| 槽位 | 对应部件 |
| --- | --- |
| `notes.notes_0` 至 `notes.notes_3` | 依次为 critical 宽音头、普通长条头、普通点按、普通划键。其他使用同一切片的音符同步换图。 |
| `notes.notes_4` 至 `notes.notes_6` | 依次为普通摩擦／摩擦长条、critical 摩擦类、普通摩擦划键的细音头。 |
| `notes.notes_flick_arrow_01` 至 `06` | 不同键宽的普通直划箭头。斜划加后缀 `_diagonal`。两套上游预制体的四轨普通斜划均引用 `03_diagonal`。 |
| `notes.notes_flick_arrow_crtcl_01` 至 `06` | critical 直划箭头，六种宽度均有对应的 `_diagonal` 斜划槽位。 |
| `notes.notes_long_among`、`notes.notes_long_among_crtcl` | 普通、critical 长条连接点。 |
| `notes.notes_friction_among_long`、`notes.notes_friction_among_flick`、`notes.notes_friction_among_crtcl` | 摩擦、摩擦划键和 critical 摩擦的中心图标，按预制体实际引用共享。 |
| `notes.notes_damage` | 本项目的 NextRUSH 伤害音符。两套上游样包均保留内置伤害音符。 |
| `notes.line.long`、`notes.line.guide`、`notes.line.simultaneous` | 长条条身、引导线、同时线。三者使用整张材质贴图，布局字段为 `scale` 和 `position`。 |

长条与引导线的横向倍率围绕每个截面的中心调整，纵向倍率以判定线为基准。全屏段以视口中心缩放。同时线的横向倍率调整两端间距，纵向倍率调整线厚。条身沿用原材质的分区采样，制作时可以从样包中的 `longNoteLine.png`、`traceLine_eff.png` 和 `simultaneousLine.png` 开始。NextRUSH 谱面指定的纯色 Guide 使用谱面颜色和透明度，同样接受引导线槽位的几何布局。

## 上游配置与验证

本地样包对应 `cubeww/OpenSekai` 的 `32c91d90c7e18135fef1a214144cce3fb756b3e5`。在项目根目录运行以下命令可从已保存的源码和素材重新生成两个 ZIP，以及各自可单独查看的清单。

```powershell
python LocalPacks/upstream-notes/32c91d9/build_sample.py
```

生成器从 `NotesViewManager` 找到普通和 critical 共 14 个音符预制体，从序列化的精灵引用和纹理 GUID 找到对应 PNG。它读取每个精灵的 `m_Rect` 和 `m_Pivot`，将切片纵坐标换算为从上边缘起算，并从两种线视图的调用处取材质贴图名。生成的 `scale: [1,1]` 和 `position: [0,0]` 使用预制体原有布局。

`LocalPacks/upstream-notes/32c91d9/` 中的 `custom01.zip`、`custom02.zip` 各含 38 个槽位和 32 张 PNG。`MaterialPackNoteTests` 在 PlayMode 下将两包分别应用到两套内置样式，验证切片、轴心、池化复用和线网格。可设置 `OPENSEKAI_NOTES_TEST_DIR` 指定这两个 ZIP 的本地目录。普通检出使用测试临时生成的 PNG 验证全部 39 个槽位和非默认布局。上游素材与样包仅保存在忽略 Git 的 `LocalPacks`。

另有 `custom01-layout.zip` 供直接对比。它使用样式 1 的原图，仅把普通点按音头改为 `scale: [1.35, 0.7]` 和 `position: [0.6, 0.35]`。同目录的 `custom01-tap.png` 与 `custom01-layout-tap.png` 是同一摄像机渲染的三轨点按截图。
