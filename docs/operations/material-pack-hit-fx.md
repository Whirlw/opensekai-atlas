# 打歌资源包 · 判定特效

从 Unity 菜单 **OpenSekai → Dev → Material Packs → Enable hitFx ZIP...** 选择 ZIP，再从曲库开局。**Restore built-in hitFx** 会恢复下一局的内置特效。判定文字、COMBO 和音效各自保存选择。同一套 `hitFx` 槽位适用于两种内置粒子样式及 NextRUSH 共用的打歌表现。

### 使用粒子模板

ZIP 根目录放 `pack.json`。下面的包只替换普通 Perfect 敲击的粒子图集，让粒子变宽并上移四分之一轨。该音符的轨道反馈和光环仍分别从各自的槽取图。

```json
{
  "schemaVersion": 1,
  "packId": "example.hit-effects",
  "version": "1",
  "groups": [{
    "id": "notes",
    "category": "hitFx",
    "templateSet": 2,
    "slots": {
      "hitFx.normal.perfect.particles": {
        "file": "textures/particles.png",
        "scale": [1.5, 0.8],
        "position": [0, 0.25]
      }
    }
  }]
}
```

组字段 `templateSet` 选择粒子模板。`1` 为上游第一套，`2` 为第二套。两套上游图集相同，预制体的粒子透明度不同。带此字段的包在下一次开局时直接使用指定模板。混搭多个组时，每个特效采用其主槽所属组的模板，拖尾槽只提供贴图。未选主槽、主槽素材损坏或旧包省略此字段时，使用设置中的打击特效样式。

| 模板字段 | 含义 |
| --- | --- |
| `file` | 完整 PNG 图集。粒子继续使用原模板的 UV、粒子寿命、发射、混合方式和 NextRUSH 配色。公共粒子图、Critical 拖尾、受伤闪屏使用不同的贴图布局。 |
| `scale` | 宽、高相对原演出的倍率，默认为 `[1,1]`，每轴大于 0 且不超过 64。绕本次触发点缩放最终绘制的图形，包含各发射器和拖尾。 |
| `position` | 相对本次触发锚点的位移，默认为 `[0,0]`，向右、向上为正。轨道特效以一条轨宽为单位，受伤闪屏以高 1080 的参考像素为单位。每轴范围为 ±8192。划键方向、动态长按中心和屏幕宽高比由游戏提供。 |

普通和 Critical 长条的持续光环以当前条身的键宽为基准。`scale[0]` 为 1 时光带与键面等宽，为 0.8 时收窄到当前键宽的 80%。变宽、变窄的长条也按当帧宽度计算。

### 槽位

下表各键前均加 `hitFx.`。大括号内的每一项展开为一个槽，例如 `normal.good.aura`。同一次判定可同时触发轨道反馈、光环和粒子。包可单独覆盖其中任何一个。

| 用途 | 槽位后缀 |
| --- | --- |
| 空击、命中轨道 | `lane.tap`、`lane.normal`、`lane.critical`、`lane.normal.flick`、`lane.critical.flick` |
| 普通和 Critical 敲击 | `normal.{perfect,great,good}.{aura,particles}`、`critical.{aura,particles}` |
| 划键 | `flick.{aura,particles,flash}`、`critical.flick.{aura,particles,flash}` |
| 摩擦、长按判定和中继点 | `trace.aura`、`critical.trace.aura`、`long.{aura,particles}`、`critical.long.{aura,particles}`、`tick.aura`、`critical.tick.aura` |
| 长按持续 | `long.hold.{aura,particles}`、`critical.long.hold.{aura,particles}` |
| 受伤闪屏 | `screen.damage` |
| 独立拖尾贴图 | `critical.aura.trail`、`critical.flick.aura.trail`、`critical.long.aura.trail`。拖尾槽只指定 `file`，布局跟随所属光环。 |

以上共 32 个模板及 3 个拖尾槽。Miss、Bad 和 NextRUSH 隐藏／伤害音符是否触发某种特效，沿用打歌逻辑。

### 用分层序列帧替换一个模板

在任一主槽内声明 `kind: "sequence"`。下面是 `hitFx.long.hold.aura` 的槽位值。三帧从一张横向图集中读取，长按期间重复第二帧，松手时转入第三帧。

```json
{
  "kind": "sequence",
  "position": [0, 0.1],
  "scale": [1, 1],
  "layers": [{
    "file": "textures/hold.png",
    "anchor": "hit",
    "pivot": [0.5, 0.5],
    "size": [1, 0.8],
    "offset": [0, 0],
    "frames": [
      { "rect": [0, 0, 64, 64], "durationMs": 70 },
      { "rect": [64, 0, 64, 64], "durationMs": 100 },
      { "rect": [128, 0, 64, 64], "durationMs": 120 }
    ],
    "loopFrameRange": [1, 2]
  }]
}
```

- 每槽支持 1–8 层，每层 1–256 帧。层按数组顺序从后往前叠加，各层可用不同 PNG。切片从图片左上角量取，顺序为 x、y、宽、高；每帧时长为 1–60000 毫秒。
- 层的 `size` 为触发模板本地坐标中的显示宽高，`scale` 为叠加倍率，`offset` 为相对中心的位移。对轨道特效，以轨宽为单位；宽度跟随音符的模板（如划键闪光、长按光环）还会按当前音符宽度伸展。闪屏层使用相对于当前全屏矩形的比例，`[1,1]` 填满矩形。省略尺寸时宽为 1，高按帧宽高比换算。`pivot` 从帧左下角归一化量取，默认居中。
- `anchor` 可省略，轨道用 `hit`，闪屏用 `center`。整段的 `scale` 同时作用于各层尺寸和相对位移。整段的 `position` 使用上表的锚点单位。
- 只有长按持续槽接受 `loopFrameRange`。下标从 0 起，区间左闭右开。省略循环时，该层每次按下播放一遍。自动播放也会在长条到达判定线时触发持续粒子和光环，沿条身移动，到条尾进入收尾段。暂停冻结特效，重试和结束时清理；池对象再次触发时从首帧播放。
- 序列完整时接管该模板及其拖尾。选中的某个图层文件在安装后损坏时，开局诊断会标出文件并回退到粒子模板，仍可使用单独选中的拖尾纹理。选中图像按内容去重，判定特效类别的解码纹理预算为 32 MiB。
