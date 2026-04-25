# 架构说明

## 总览

项目采用轻量 Unity 运行时代码生成 UI 的方式实现。当前没有依赖外部美术资源，棋盘、棋子、雪山背景、音效、背景音乐都由 C# 在运行时生成，便于 GitHub 保存和 AI 后续接手。

核心分层：

- 数据模型层：`DataModels.cs`
- 棋盘规则/对局状态层：`ChineseCheckersCore.cs`
- Unity 运行时/UI/音频/存档层：`AppRuntime.cs`
- 编辑器工具/验证/导出层：`Assets/Scripts/Editor/*.cs`

## 数据模型

文件：`Assets/Scripts/SweetJumpJump/DataModels.cs`

主要类型：

- `SlotId`：六个座位，分别是 `Top`、`TopRight`、`BottomRight`、`Bottom`、`BottomLeft`、`TopLeft`。
- `PlayerKind`：`None`、`Human`、`AiBeginner`、`AiNormal`、`AiAdvanced`。
- `RuleVariant`：`OnePieceJump` 和 `SpaceJump`。
- `HexCoord`：轴向六边形坐标，包含 `Q`、`R`、派生 `S`，以及距离计算。
- `RoomConfig`：房间配置，包含规则、音频、主题、催促、座位。
- `GameOptions`：全局默认选项。
- `AppSaveData`：本地存档根对象，当前 `SaveVersion = 3`。

## 棋盘规则核心

文件：`Assets/Scripts/SweetJumpJump/ChineseCheckersCore.cs`

主要职责：

- `BoardLayout`
  - 生成 121 格标准星形棋盘。
  - 生成六个 10 格营地。
  - 定义六方向移动向量。
  - 提供座位标签、玩家标签、房间默认值、房间校验、房间克隆。
  - 提供棋子颜色。

- `GameSession`
  - 管理当前房间的一局对战。
  - 根据房间座位生成玩家顺序与棋子。
  - 处理选棋、移动、连续跳、悔棋、完成移动、放弃移动。
  - 根据目标营地判断胜利。
  - 生成 AI 候选移动，并按 AI 难度打分选择。

## 运行时和 UI

文件：`Assets/Scripts/SweetJumpJump/AppRuntime.cs`

入口：

- `AppController.Bootstrap()` 使用 `RuntimeInitializeOnLoadMethod` 自动创建全局控制器。
- `Awake()` 初始化 Camera、EventSystem、音频、存档、UI、主题。

UI 特点：

- 使用 `Canvas` + `UGUI` 全部代码生成。
- 主场景只需要一个空的 `MainScene`，实际界面由 `AppController` 创建。
- 棋盘由 `EnsureBoardCreated()` 根据 `BoardLayout.AllCells` 创建 121 个可点击格子。
- `RefreshBoard()` 同步棋子、可落点、选中状态、底部操作栏状态。

棋盘视觉：

- 雪山背景：`GenerateMountainBackdropSprite()`
- 六边形棋格：`GenerateHexCellSprite()`
- 可落点高亮：`GenerateHexGlowSprite()`
- 空位内圈/选中圈：`GenerateRingSprite()`
- 棋子纹理：`GeneratePieceSprite()`

音频：

- 音效由 `CreateToneClip()` 和 `CreateArpeggioClip()` 程序化生成。
- 背景音乐由 `CreateMusicLoop()` 程序化生成。

存档：

- `SaveManager` 使用 `JsonUtility` 写入 `Application.persistentDataPath/sweet_jump_jump_save.json`。
- 存档包含全局选项和房间列表。

## 编辑器工具

目录：`Assets/Scripts/Editor/`

- `MainSceneBootstrap.cs`
  - 菜单：`Tools/SweetJumpJump/Create Or Update MainScene`
  - 创建/更新 `Assets/Scenes/MainScene.unity` 并加入 Build Settings。

- `StageOneVerifier.cs`
  - 验证棋盘、营地、默认房间、真人先手、AI 可行动。

- `StageTwoVerifier.cs`
  - 验证房间配置、悔棋、空跳、AI 难度。

- `StageThreeBuildTools.cs`
  - 配置 iPad 构建参数。
  - 执行阶段一/二回归。
  - 导出 Xcode 工程到 `Builds/iOS`。

## 当前已知取舍

- 当前为本地单机，未实现联网/局域网/账号/广告。
- UI 为代码生成，利于版本控制，但不适合非程序同学在 Unity Inspector 里拖拽编辑。
- 程序化美术资源便于提交和复现，但如果后续有正式美术，应迁移到 `Assets/Art/` 并保留 `.meta` 文件。
