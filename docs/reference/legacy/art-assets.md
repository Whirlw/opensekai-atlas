# 美术资产清单

> 目的：记录重构前统计的美术资产清单与替换皮肤步骤，仅作历史参考。
> 适用读者：需要了解资源规模与目录的人。
> 前置条件：无。
> 最后更新：2026-11-02（仅目录整理）

> ⚠️ 历史文档：部分资产（编辑器画面 prefab、SoftMask/UI Particle 相关）已移出工程或摘掉依赖。


> 只做「有什么、在哪、给谁用」的盘点，不含改动流程。工程结构见 [`项目总览（已过期）.md`](old-project-overview.md)，代码细节见 [`实现细节（已过期）.md`](old-implementation-notes.md)。
>
> 统计口径：`Assets/` 下的图片、材质、shader、动画、prefab、图集、字体（不含 `.meta`、不含音频/视频）。所有数字来自当前工作区实测。

---

## 0. 总量

| 类型 | 数量 | 体积 |
|---|---:|---:|
| PNG / JPG | 241 | 45.9 MB |
| Prefab | 158 | 39.3 MB |
| Sprite 定义资产（无像素） | 391 | 1.7 MB |
| SpriteAtlas 资产 | 3 | 0.1 MB |
| Material | 100 | 0.2 MB |
| Shader | 83 | 1.3 MB |
| AnimationClip / AnimatorController | 21 / 19 | 1.0 MB |
| 字体 OTF + TTF | 2 + 1 | 32.3 MB |
| **合计** | **629 个文件** | **≈120 MB** |

对照：整个仓库 549 MB，其中 185 MB 是根目录 `ffmpeg/`（录屏转码用，不是美术），其余主要是 C# 源码、`.meta` 和音频。

**一句话概括**：这是一套**纯 2D 的 UGUI/Sprite 美术**——音游那套（图集 + 音符皮肤 + 打击/结算特效 + 判定线轨道贴图）齐全，UI 图集齐全，但**没有任何 3D 模型、角色、MV 或 Live2D/Spine**。

---

## 1. 图集体系（UI 美术的主体）

UI 图不是散图挂在 Image 上，而是「**图集 + 名字**」寻址：

```csharp
// Sekai/AtlasImage.cs:41-51
sprite = useSharedSprite ? UIUtility.GetSharedSprite(atlas, spriteName) : atlas.GetSprite(spriteName);
```

三层结构：

| 层 | 位置 | 数量 | 说明 |
|---|---|---:|---|
| Sprite 定义 | `Assets/Sprite/*.asset` | **391** | `Sprite` 类型，只有 `m_Rect` + `m_PixelsToUnits` + 指向图集页的引用，**本身没有像素** |
| 图集页（像素） | `Assets/Texture2D/sactx-*.png` | **10** | ASTC 压缩（4x4 / 6x6），最大 2048×2048 |
| SpriteAtlas | `Assets/SpriteAtlases/Rebuilt/` | **3** | `CommonAtlas`、`LiveFrontUIAtlas`、`MusicScoreMakerAtlas`；按 guid 显式列表打包（不是文件夹 glob） |

391 个 Sprite 分别属于哪张图集页（按引用统计）：

| 图集页 | Sprite 数 | 用在哪 |
|---|---:|---|
| `MusicScoreMakerAtlas` 2048×1024 | 149 | 谱面编辑器整个界面 |
| `LiveAtlas` 2048×2048 | 118 | 打歌 HUD、判定、技能条 |
| `CommonAtlas` 页 0 + 页 1 | 10 + 60 | 通用 UI |
| `GachaAtlas` 页 0 + 页 1 | 9 + 7 | 抽卡界面（残留界面，本工程不可达） |
| `JudgeAtlas` 512×512 | 9 | 判定字（Perfect / Great / Good / Bad / Miss 等） |
| `FrameAtlas` 1024×1024 | 5 | 卡框 |
| `HeaderTitleAtlas` 1024×1024 | 1 | 顶栏标题 |
| 独立贴图（不在图集内） | 约 22 | 轨道线、判定线、2D MV 遮罩等，见 §2 |

> 两处小异常，改图集时值得留意：
> - `sactx-0-2048x1024-...MusicScoreMakerOutGameAtlas` 有图集页，但**没有任何 Sprite 引用它**（遗留页）。
> - `Assets/Sprite` 是扁平目录，391 个文件同名冲突只能靠改名，没有按功能分文件夹。

---

## 2. 打歌（live）专用美术

### 2.1 音符皮肤（2 套）

| 皮肤 | 目录 | 内容 |
|---|---|---|
| custom01 | `Assets/Sekai/assetbundle/resources/startapp/live/note/custom01/` | 14 prefab + 3 材质 + **33 张 png** + 39 个 Sprite 切片 |
| custom02 | `.../live/note/custom02/` | 同上（换配色/造型） |

每套的组成是固定的：

- **14 个 prefab**：`NormalNote`、`NormalCrtNote`、`LongNote`、`LongCrtNote`、`FlickNote`、`FlickCrtNote`、`ConnectionNote`、`ConnectionCrtNote`、`FrictionNote`、`FrictionCrtNote`、`FrictionFlickNote`、`FrictionFlickCrtNote`、`FrictionLongNote`、`FrictionLongCrtNote`（对应 live 侧 14 种音符形态）
- **3 个材质**：`NoteDefault.mat`、`NoteArrow.mat`、`NoteSlide.mat`
- **贴图**：`notes.png`（+ `notes_0..6` 切片）、`longNoteLine.png`、`simultaneousLine.png`（多押提示线）、`traceLine_eff.png`（引导线），以及 `noteskin/` 下 **29 张**箭头/长条端部贴图（`notes_flick_arrow_01..06` 及 `_diagonal`、`_crtcl` 变体，`notes_long_among`、`notes_friction_among_*`）

编辑器预览另有 `live/note_preview/custom02`（3 png + 3 切片）；`live/note_preview/custom01` 目录**不存在**，而且 `LiveConfig.NotePreviewBundleName` 全仓只有赋值、没有使用点——编辑器预览实际读的是 `Assets/GameObject/NotePreviewPrefab.prefab` 那套自带美术，与皮肤无关。

两套皮肤的 bundle 名由 `LiveConfig.GetNoteBundleName(index)` 生成（`live/note/custom{index+1:00}`），每个皮肤目录里有 **89 个资源带 assetBundleName 标签**。

### 2.2 判定线 / 轨道 / 通用贴图（`Assets/Texture2D/`）

| 文件 | 尺寸 | 用途 |
|---|---|---|
| `judgeLine.png` | 1608×114 | 判定线 |
| `lane_base.png` | 2048×1176 | 轨道底板 |
| `lane_line.png` / `lane_mask.png` / `lane_skill.png` | 1024~2048 | 轨道分隔线 / 遮罩 / 技能条底 |
| `simultaneousLine.png` | — | 多押连接线 |
| `mask_line.png` | — | 长条遮罩 |
| `bg_base_wh.png` / `bg_base_ingame_wh.png` | — | live 白底 |
| `bg_2dmode_window_nml/mirror.png` | — | 2D MV 窗口框 |
| `bg_hiLight_mask.png` | — | 高光遮罩 |
| `grd_append.png` / `label_grd_h40_dbl.png` | — | 渐变条 / 标签底板 |
| `tex_lifeZero.png` | — | 血量归零提示 |
| `skillEffect_text.png` | — | 技能特效文字 |
| `16_9_white.png` | — | 纯白占位 |

### 2.3 打击特效（2 套）

`.../assetbundle/resources/tutorial/effect_asset/live/tap_effect/0|1/`

| 内容 | 数量 / 套 |
|---|---:|
| 特效 prefab（`fx_note_*` 26 个、`fx_lane_*` 5 个、`fx_ove_01` 1 个） | 32 |
| 网格 prefab（`mesh/square4_mirror`） | 1 |
| 贴图 png | 5（含 `tex_note_common_all.png`、`tex_tap_texturesheet`） |
| 材质 | 3 |
| 网格 asset | 1（粒子用程序化 mesh） |

另有 `Assets/Resources/effects/tap/common/`：7 张 png + 6 个材质 + 1 个粒子 mesh（`sekai_PT_05.asset`），是通用打击/光环特效。

### 2.4 结算特效

`.../effect_asset/live/result/default/`

| 内容 | 数量 |
|---|---:|
| 贴图 png | **24** |
| 材质 | 14（含 `mesh/materials` 下 5 个） |
| prefab | 8 |
| AnimationClip / Controller | 13 / 13 |
| 其他 asset（sprite/面板数据） | 83 |

动画覆盖：`ALLPERFECT_01`、`ALLPERFECT_v2_01`、`FULLCOMBO_01`、`FULLCOMBO_v2_02`、`CLEAR_v2_01`、`FINISH_v2_01`、`FAILED_v2_01`、`FEVER_01`、`CUTIN_01/02`、`skill_CutIn_01`、`result_clear 1/2`。

### 2.5 技能特效

`.../effect_asset/live/skill/default/`

10 张 png + 5 个材质 + 2 个 prefab + 3 组动画（`skill_JudgeLine`、`skill_LifeGauge`、`skill_ScoreUp`）。

### 2.6 倒数 / 转场

| 位置 | 内容 |
|---|---|
| `Assets/Resources/effects/live_countdown/` | 2 png（`tex_live_countdown`、`gauge_bar_circle_h384_wh`）+ 1 材质 + 1 控制器/动画 |
| `Assets/Resources/effects/transition/` | 3 png + 3 材质 + 1 个 prefab（`fx_live_transition_v2_03_b`） |

### 2.7 2D MV 背景

`.../startapp/live/2dmode/background/default.png`（1.49 MB）+ `Assets/Texture2D/` 下 10 张 jacket 贴图/遮罩（`tex_2dmode_jacket_nml_*`、`tex_2dmode_jacket_mirror_*`、`mask_2dmode_*`、`mask_2dmode_floor_grd.png`）。

---

## 3. UI 与编辑器美术

| 位置 | 内容 | 数量 |
|---|---|---:|
| `Assets/GameObject/` | 编辑器可视化 prefab：`NotePreviewPrefab`、`LongNoteLinePreviewPrefab`、`BarLinePrefab`、`BeatLinePrefab`、`QuantizeLinePrefab`、`ToolIcons`、`InvalidArea`、`musicEventBalloonPreviewPrefab`、`ClipboardCacheListItemPrefab` | 10 |
| `Assets/Resources/screen/` | 232 个 `ScreenLayerData` 配置 + **8 个真实 prefab** + 6 个注册表资产 | 246 |
| `Assets/Resources/screen/prefabs/` | `ScreenLayerTouchEffect`（4.17 MB，单文件最大）、`ScreenLayerMusicScoreMaker`（1.50 MB）、`ScreenLayerLiveLoading`、`ScreenLayerLoading`、`ScreenLayerHeader`、`ScreenLayerInsertNoti`、`ScreenLayerNetworkIndicator`、`ScreenLayerAssetRemoving` | 8 |
| `Assets/Resources/dialog/` + `subwindow/` | 对话框 prefab | 14 |
| `Assets/Resources/live/` | `LiveOutUI`、`FrontUIView`、`FrontMusicVideoView`、`FrontRankUIView`、`FrontCheerfulCarnivalUIView`、`FrontMultiUIView`、`Background2DView`、`Background3DView`、`SkillTextView` | 10 |
| `Assets/Resources/ui/` | uiparts（renewal） | 5 |
| `Assets/Icons/` | 应用/通知图标 | 2 |
| `Assets/Material/` + `Assets/Resources/materials/` + `Assets/Resources/ui/materials/` | UI 材质：顶层 `Assets/Material` 8 个 + `Resources/materials`（含 `ui`、`ingame`、`musicdifficulty` 难度色）+ `ui/materials` 共 6 个 | 14 |

> 注意 `Background3DView.prefab` 在，但对应的 `Background3DView.cs` 是空壳——**prefab 在、功能不在**。

---

## 4. 背景与加载图

| 位置 | 内容 | 体积 |
|---|---|---:|
| `Assets/Resources/loading/` | **6 张**乐队/组合主题长加载背景（`longLoading_bg_nightcode`、`leoneed`、`moremorejump`、`vividbadsquad`、`wonderlands`、`vertualsinger`，各 1.8~2.0 MB）+ 2 张换图遮罩（`ImageChangeMask`、`ImageChangeMask_F`） | 12.7 MB |
| `Assets/Resources/commonbg/bg_common_color.png` | 通用渐变底，**单张最大 6.67 MB** | 6.67 MB |
| `Assets/Resources/Common/bg/bg_sekai.png` | 通用底图 | 0.57 MB |
| `Assets/Resources/background/` | 背景材质 1 个 | — |

---

## 5. 字体与文本美术

`Assets/Resources/font/`：

| 内容 | 数量 | 说明 |
|---|---:|---|
| `FOT-RodinNTLGPro-DB.otf` / `-EB.otf` | 2 | 15.8 MB + 16.2 MB，日文字形，标题与正文两种字重 |
| TMP SDF 字体资产（`.asset`） | 7 | `SDF_Base`、`SDF_Dynamic`、`-Builtin` 等 |
| SDF 图集页（png） | 7 | 字形烘焙图 |
| 材质预设（`.mat`） | **41** | 描边（Bold/White/Gray/Dbl/Alpha30-60）、阴影、Gacha、Music_Player、Convert_Slot 等变体 |
| shader | 1 | 字体专用 |

另有 TMP 自带的 `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans.ttf`（0.33 MB）与 `TextMesh Pro/Sprites` 1 张。

---

## 6. Shader 与材质

83 个 `.shader` 里，**36 个是本工程的游戏 shader**（`Assets/Shader/`），其余是 TMP（13）、URP 内置、CRIMW 插件自带的波形渲染等。

游戏 shader 按用途分：

| 用途 | shader |
|---|---|
| 音符 | `note-default`、`note-arrow`、`note-slide`、`note-hidearea`、`noteline`、`notepairline` |
| 特效/粒子 | `particle add`（多个变体）、`particle add + alpha blend`、`particle alpha blend`、`particle-ui`、`mobile-particle-add`、`mobile-particle-alpha`、`sekaieffect` |
| UI | `sprites-additive`、`overlaymask`、`drawstencil`、`fillcolor`、`textureswap`、`crossfade`、`fadeoutblend` |
| 场景/后处理 | `music3dview`、`areapost`、`scenariopost` |

100 个材质按用途分布：

| 用途 | 数量 |
|---|---:|
| 字体材质预设（`Resources/font` 下，含 scenario / musicdifficulty / musicscoremaker 变体） | 41 |
| 结算特效（`result/default` 含 mesh/materials） | 14 |
| 顶层 `Assets/Material` | 8 |
| 打击/转场/技能/倒数/音符皮肤/UI/背景/加载 | 35 |
| TMP 自带 | 2 |

---

## 7. 动画

21 个 `.anim` + 19 个 `.controller`，全部是 UI/特效动画（没有角色骨骼动画）：

| 用途 | 位置 |
|---|---|
| 结算演出 13 组 | `effect_asset/live/result/default/animation/` |
| 技能条/JudgeLine 3 组 | `effect_asset/live/skill/default/animation/` |
| 倒数 1 组 | `Resources/effects/live_countdown/animation/` |
| 加载图标 2 组 | `Resources/animator/` |
| 下载指示 2 组 | `Assets/AnimationClip/` + `Assets/AnimatorController/` |

---

## 8. 混在美术目录里、但不是游戏美术的东西

`Assets/Texture2D/` 里除图集页和轨道贴图外，还有 **URP / 后处理内部资源**，被 `Assets/MonoBehaviour/PostProcessData.asset`、`UniversalRenderPipelineAsset.asset` 引用：

| 文件 | 数量 | 说明 |
|---|---:|---|
| `LDR_LLL1_0..32.png` | 33 | 16×16 颜色分级 LUT |
| `Large01/02`、`Medium01..06`、`Thin01/02` | 10 | SMAA 查找纹理 |
| `AreaTex.png`、`SearchTex.png` | 2 | SMAA 面积/搜索图 |
| `BayerMatrix.png` | 1 | 4×4 抖动矩阵 |

**这些不是可替换的游戏美术，删掉会破坏 URP 后处理配置。**

---

## 9. 明显缺的资产

| 缺什么 | 证据 |
|---|---|
| **3D 模型 / 角色** | 全仓 0 个 `.fbx` / `.obj` / `.blend` / `.gltf`；`Assets/Mesh` 只有 1 个 `Circle_mdl.asset`，特效目录里共 6 个程序化 mesh |
| **Spine / Live2D** | 0 个 `.skel` / `.atlas` / live2d 资源 |
| **MV 视频** | 无视频文件；`Background3DView` 与 MV 玩法是空壳 |
| **骨骼/蒙皮动画** | 21 个 anim 全是 UI 变换动画 |
| **官方曲目美术**（封面、谱面皮肤全量） | 只有 `custom01/02` 两套皮肤和通用 UI 图集，没有按曲目的 jacket 库 |
| **Live2D 立绘、剧情背景、抽卡演出** | 相关界面（Gacha、Story、Area）在本工程不可达，图集只是残留页 |

---

## 10. 体积与许可

- 美术总计约 **120 MB**，占仓库两成；单文件前几名：`bg_common_color.png` 6.67 MB、`ScreenLayerTouchEffect.prefab` 4.17 MB、两张 15~16 MB 的日文字体、图集页 1~1.8 MB。
- **许可**：`README.md:19-23` 明确——代码 MIT，但第三方库、字体、音频、贴图、prefab、shader 以及任何可能来自原作或其他权利方的资源**不在 MIT 范围内**，只在本地研究使用，不要随构建产物分发。
- 音符皮肤与打击/结算特效在 AssetBundle 源目录（`Assets/Sekai/assetbundle/resources/**`），其余 UI 图集与 prefab 在 `Resources` 下按需加载；两条路径的差别见 [`项目总览（已过期）.md`](old-project-overview.md) 第 8 节。

---

## 11. 定位某个资产的方法

```bash
# 某个 bundle 里到底有什么（按 assetBundleName 标签）
grep -rl "assetBundleName: live/note/custom01" Assets --include=*.meta

# 某张图被哪个 prefab/material 用（先取 guid，再搜引用）
grep "^guid:" "Assets/Texture2D/judgeLine.png.meta"
grep -rl "<把 guid 粘这里>" Assets --include=*.prefab --include=*.mat --include=*.asset

# UI 上的图是哪张 sprite（代码按名字取，prefab 里存的是 atlas + spriteName）
grep -rn "spriteName" Assets/Resources --include=*.prefab | head
```

图上还有一层间接：UGUI 上挂的是 `AtlasImage`（`Sekai/AtlasImage.cs`），它的 `atlas` + `spriteName` 两字段才对应到 `Assets/Sprite/*.asset` 与图集页。
