# 打歌 HUD 皮肤资源包

在 Unity 的 **OpenSekai → Dev → Material Packs → Enable hud ZIP...** 中选择 ZIP，从曲库开局即可看到新 HUD。选择 **Restore built-in hud** 后，下一局恢复内置 HUD。未声明的槽位沿用下一优先级的组或内置素材。

## 清单示例

把 `pack.json` 放在 ZIP 根目录。下面将生命面板缩小到原来的 85%，自定义暂停按钮，并将每一位分数数字 2 向右移两个参考像素。

```json
{
  "schemaVersion": 1,
  "packId": "example.hud",
  "version": "1",
  "groups": [{
    "id": "overlay",
    "category": "hud",
    "slots": {
      "hud.life.layout": { "scale": [0.85, 0.85] },
      "hud.life.pause": { "file": "pause.png", "size": [80, 80], "position": [12, 0] },
      "hud.score.digit.2": { "file": "two.png", "size": [30, 46], "position": [2, 0] }
    }
  }]
}
```

## 可用槽位

下表均省略开头的 `hud.`。花括号内任选一项，例如 `hud.score.marker.A.label`。

| 用途 | 槽位 |
| --- | --- |
| 整体布局 | `score.layout`、`life.layout`、`countdown.layout` |
| 分数面板、进度槽、填充、评级底板、技能高亮 | `score.{base,gaugeBase,gauge,rankPlate,skill}` |
| 当前评级和对应的 RANK 标签 | `score.{rank,rankLabel}.{S,A,B,C,D}` |
| 四个分数线标记的底座、刻线和字母 | `score.marker.{C,B,A,S}.{base,line,label}` |
| 分数、加分、生命数字和描边 | `{score,add,life}.{digit,outline}.{0…9}` |
| 整行数字或描边的布局 | `{score,add,life}.{digit,outline}.layout` |
| 加分符号及描边 | `add.{digit,outline}.{plus,minus}`，游戏按数值选择符号，上游加分动画使用正号 |
| 生命面板、普通／受伤底槽、对应填充、心形和技能高亮 | `life.{base,normal,damage,normalGauge,damageGauge,icon,skill}` |
| 暂停、禁用暂停和跳过 | `life.{pause,pauseDisabled,skip}` |
| 自动打歌标识 | `auto.label` |
| 连续自动打歌遮罩、按钮和标识 | `continuous.{shade,pause,pauseDisabled,label}` |
| 三拍倒计时的文字和粒子圆环 | `countdown.{1,2,3}.{text,ring}` |

## 大小、位置和切片

| 字段 | 含义 |
| --- | --- |
| `file` | ZIP 内 PNG 的相对路径，每边最多 2048 像素。多个槽可引用同一图集，HUD 选中纹理按内容去重，解码预算为 32 MiB。 |
| `rect` | 从 PNG 左上角量起的整数框 `[x,y,w,h]`。省略时使用整图。 |
| `pivot` | 在切片内从左下角量起的归一化轴心，例如 `[0,0.5]` 适合从左端增长的进度条。省略时沿用对应内置精灵的轴心。 |
| `size` | 参考像素宽高，每项大于 0 且最多 8192。进度条填满时达到此宽度。省略时沿用内置显示尺寸。 |
| `scale` | 宽、高倍率，每项大于 0 且最多 64，默认 `[1,1]`。 |
| `position` | 轴心在原父节点中的局部位置，范围 ±8192 参考像素。每 108 参考像素等于一个局部单位，正方向向右、向上。省略时沿用原位置。数字和正负号在各自字格内应用此偏移，因此多位数字仍按原间距排列。 |
| `border` | 九宫格切片的左、下、右、上边距，单位为纹理像素，各对边之和应能放入切片。 |
| `drawMode` | `stretch` 拉伸中心区域，`tile` 平铺中心区域。省略时沿用内置平铺行为，其余元素按显示框拉伸。 |

`*.layout` 槽只填 `position` 和／或 `scale`，可以单独移动或缩放整组内置素材。面板继续跟随原有屏幕锚点，子图的位置和尺寸接受父级变换。分数标记默认跟随进度条重新定位，给标记底座指定 `position` 可固定在自定义位置。暂停和跳过的点击区域会按显示后的按钮重新计算。

倒计时圆环使用整张粒子纹理，支持 `file`、`position` 和 `scale`。每拍文字另有独立的普通图片槽。倒计时动画、生命值填充、换评级、加分动画和 AUTO 显隐由游戏控制。

## 本地上游样包与对照

`LocalPacks/upstream-hud/32c91d9/` 中保存 `upstream-hud.zip`、可独立阅读的 `pack.json`、生成脚本、源文件清单和 `comparison.png`。生成器读取 `FrontUIView.prefab` 的序列化引用、精灵切片、九宫格和变换，从分数计算与颜色脚本读取评级阈值和标签颜色。样包的 119 个槽共用 9 张 PNG，包括原始 HUD 图集、倒计时贴图和按上游颜色生成的评级用图。[上游固定版本](https://github.com/cubeww/OpenSekai/tree/32c91d90c7e18135fef1a214144cce3fb756b3e5)

PlayMode 使用真实 ZIP 验证导入、选择、预制体绑定、数字与评级切换、低血量、重试及恢复。渲染对照取一个固定画面，将分数、评级、生命、暂停、AUTO 和倒计时数字与按上游预制体及图集重建的参考图并排展示。
