# 架构说明

## 总览

项目由四个主要部分组成：

| 部分 | 路径 | 职责 |
|------|------|------|
| Unity 客户端 | `Assets/Scripts/SweetJumpJump/` | 规则、UI、存档、在线网络（iOS/编辑器） |
| 编辑器工具 | `Assets/Scripts/Editor/` | 阶段验证、iOS 导出 |
| 共享规则库 | `Shared/SweetJumpJump.Core/` | 服务端与客户端共享的数据模型 + 规则核心（.NET） |
| 服务端 | `Server/` | ASP.NET Core WebSocket 服务，账号/房间/裁判 |
| 网页端 | `Web/` | 静态 HTML/CSS/JS，通过 WebSocket 参与对局 |

棋盘、棋子、雪山背景、音效、背景音乐全部由 C# 在运行时程序化生成，无外部美术资源依赖。

---

## 数据模型

### Unity 端：`Assets/Scripts/SweetJumpJump/DataModels.cs`

- `SlotId`：六个座位 `Top`、`TopRight`、`BottomRight`、`Bottom`、`BottomLeft`、`TopLeft`。
- `PlayerKind`：`None`、`Human`、`AiBeginner`、`AiNormal`、`AiAdvanced`。
- `RuleVariant`：`OnePieceJump`（一子跳）、`SpaceJump`（空跳）。
- `MoveMode`：`None`、`AdjacentDone`、`JumpChain`，描述当前回合走子阶段。
- `HexCoord`：轴向六边形坐标，含 `Q`、`R`、派生 `S`，支持加减乘和距离计算。
- `TurnStep`：单步历史记录，用于悔棋。
- `SlotConfig`：座位+玩家类型对。
- `RoomConfig`：房间配置，含规则、音频、主题、催促、座位列表。
- `GameOptions`：全局默认选项，含自定义音乐路径、在线账号信息。
- `AppSaveData`：本地存档根对象，`SaveVersion = 4`，含选项和房间列表。
- **在线协议模型**：`OnlineMessage`、`OnlineGameSnapshot`、`OnlineSeatSummary`、`OnlineRoomSummary` 等，用于 WebSocket 通信。

### 共享端：`Shared/SweetJumpJump.Core/DataModels.cs`

与 Unity 端同名类型的服务端版本（使用 `System.Text.Json`，`record struct` 语法），被 `Server/` 直接引用。包含 `SlotId`、`PlayerKind`、`RuleVariant`、`MoveMode`、`HexCoord`、`TurnStep`、`SlotConfig`、`RoomConfig`、`PieceState`、`PlayerState` 等。

---

## 棋盘规则核心

### Unity 端：`Assets/Scripts/SweetJumpJump/ChineseCheckersCore.cs`

**`BoardLayout`（静态工具类）**

- `GenerateAllCells()`：生成 121 格标准六角星棋盘。
- `GenerateCamps()`：生成六个阵营各 10 格营地。
- `Directions`：六方向向量数组。
- `GetSlotLabel()`、`GetPlayerKindLabel()`、`GetRuleLabel()`：UI 显示标签。
- `GetPieceColor()`：六阵营棋子颜色。
- `CreateDefaultRoom()`、`CreateNewRoom()`、`CreateDefaultOptions()`：默认数据工厂。
- `ValidateRoom()`：房间合法性校验（≥2 玩家、≥1 真人、成对座位）。
- `GetSlotMap()`、`NormalizeRoomSlots()`、`CloneRoom()`：房间配置辅助。

**`GameSession`（对局状态机）**

- 构造时根据 `RoomConfig` 生成玩家顺序（`Bottom` 优先）和 60 枚棋子。
- `TrySelectPiece()`：选棋，计算合法落点。
- `TryMoveSelectedPiece()`：移动，维护 `moveMode`、`visitedJumpCells`、`currentTurnSteps`。
- `TryUndo()`：撤回本回合最后一步，恢复正确的 `moveMode`。
- `TryFinishTurn()`、`TryPassTurn()`：结束/放弃回合，推进到下一玩家。
- `TryMovePieceById()`：供在线模式服务端广播后直接驱动棋子移动。
- `GetBestAiMove()`：按难度选择最优 AI 走法。
  - `AiBeginner`：随机选择。
  - `AiNormal`：贪心分数最高。
  - `AiAdvanced`：贪心 + 随机扰动。
- `HasPlayerWon()`：检查指定座位是否全部棋子到达对面营地。

### 共享端：`Shared/SweetJumpJump.Core/ChineseCheckersCore.cs`

服务端版本的 `BoardLayout` 和 `GameSession`，逻辑与 Unity 端一致，不依赖 `UnityEngine`。服务端用此库执行裁判，保证规则唯一权威来源。

---

## 运行时和 UI（Unity）

文件：`Assets/Scripts/SweetJumpJump/AppRuntime.cs`

**入口**

- `AppController.Bootstrap()`：`RuntimeInitializeOnLoadMethod(AfterSceneLoad)` 自动创建单例 `SweetJumpJumpApp` GameObject，`DontDestroyOnLoad`。
- `Awake()`：初始化字体、Camera、EventSystem、音频、存档、UI、主题，启动 Splash 协程。

**面板体系**

所有界面由代码生成，主场景保持空白。面板列表：

| 面板字段 | 说明 |
|----------|------|
| `splashPanel` | Splash 3 秒后跳主菜单 |
| `menuPanel` | 主菜单（本地/在线/选项） |
| `optionsPanel` | 游戏选项（规则、主题、音效、音乐、催促、自定义音乐） |
| `roomsPanel` | 房间列表 |
| `roomEditPanel` | 房间编辑/新建 |
| `onlinePanel` | 在线大厅（登录、房间列表、房间内等待） |
| `gamePanel` | 对局界面（棋盘 + 底部操作栏） |

**棋盘视觉**

- `GenerateMountainBackdropSprite()`：雪山渐变背景。
- `GenerateHexCellSprite()`：六边形格子（单线边框）。
- `GenerateHexGlowSprite()`：可落点高亮。
- `GenerateRingSprite()`：格子内圈/选中圈。
- `GeneratePieceSprite()`、`GeneratePieceHighlightSprite()`：棋子纹理 + 选中高光。
- 棋盘由 `EnsureBoardCreated()` 创建 121 个 `BoardCellView`；`RefreshBoard()` 每帧同步显示状态。

**关键颜色常量**（直接在 `AppRuntime.cs` 顶部定义，方便微调）

| 常量 | 用途 |
|------|------|
| `BoardCellColor` | 普通格子填充色（冰蓝） |
| `BoardTargetColor` | 可走目标格子高亮色 |
| `BoardSlotRingColor` | 格子内圈默认颜色 |
| `BoardTargetRingColor` | 可走目标内圈颜色 |
| `BoardTargetDotColor` | 可走目标中心提示点 |
| `BoardSelectionColor` | 选中棋子局部亮斑 |

**音频**

- 音效：`CreateToneClip()`（单音）、`CreateArpeggioClip()`（琶音），程序化生成。
- 背景音乐：`CreateMusicLoop()` 程序化生成；支持自定义音乐文件（`UnityWebRequest` 加载）和 iOS 本机音乐选择器（`NativeMusicPicker`）。

**存档**

- `SaveManager`：`JsonUtility` 写入 `Application.persistentDataPath/sweet_jump_jump_save.json`。
- 存档含全局选项（`GameOptions`）和房间列表（`List<RoomConfig>`），`SaveVersion = 4`。

**在线模式**

- `onlineMode` 标志区分本地/在线对局。
- `OnlineClient`（`OnlineNetworking.cs`）：后台线程维护 WebSocket 连接，线程安全入队收到的消息。
- `Update()` 中 `TryDequeue` 消费消息，处理 `WELCOME`、`AUTH_OK`、`STATE`、`ROOM`、`LOBBY`、`ROOM_LIST`、`ERROR`、`DISCONNECTED` 等类型。
- `STATE` 消息驱动 `GameSession` 状态同步（由服务端裁判，客户端只渲染）。
- 在线模式下悔棋按钮不可用（`undoButton.interactable = false`）。
- 棋盘各阵营角落显示玩家昵称，仅在 `onlineMode` 下展示。

---

## 在线网络层

### Unity 端：`Assets/Scripts/SweetJumpJump/OnlineNetworking.cs`

- `OnlineClient`：封装 `System.Net.WebSockets.ClientWebSocket`，后台线程持续接收。
- 发送：`Send(OnlineMessage)` 手动序列化为 JSON（不依赖 `JsonUtility` 数组）。
- 接收：`ParseMessage()` 先用 `RawEnvelope` 解析平铺字段，再用 `ParseNested<T>()` / `ParseNestedArray<T>()` 提取嵌套对象。
- `TryDequeue()` 线程安全地供主线程消费消息。
- **注意**：Unity 端协议已迁移到 WebSocket，`SendReliable()` 和 `UpdateRetries()` 保留为空壳（兼容旧调用点）。

**客户端发送的指令类型**：`AUTH`、`CREATE`、`JOIN`、`LIST`、`START`、`SELECT`、`MOVE`、`FINISH`、`PASS`、`UPDATE_NICKNAME`、`ADMIN_AUTH`、`ADMIN_SNAPSHOT`、`ADD_PLAYER`。

**服务端推送的消息类型**：`WELCOME`、`AUTH_OK`、`ADMIN_OK`、`STATE`、`ROOM`、`LOBBY`、`ROOM_LIST`、`ERROR`、`DISCONNECTED`、`PROFILE`、`ADMIN_NOTICE`、`ADMIN_SNAPSHOT`。

### iOS 本机音乐：`Assets/Scripts/SweetJumpJump/NativeMusicPicker.cs`

- 仅在 `UNITY_IOS && !UNITY_EDITOR` 下调用原生 `SJJ_OpenMusicPicker(gameObjectName)`。
- `IsSupported`：仅 `RuntimePlatform.IPhonePlayer` 返回 `true`。

---

## 服务端

文件：`Server/Program.cs`（顶层语句，ASP.NET Core Minimal API）

**架构**

- 单文件顶层语句，所有逻辑内联（`HandleClientAsync`、`HandleCommandAsync` 等）。
- 依赖 `Shared/SweetJumpJump.Core` 执行规则裁判。
- 静态文件服务 `Web/` 目录（网页端）。
- `/ws`：WebSocket 端点，每个连接对应一个 `ClientPeer`。
- `/health`：健康检查。

**核心数据结构**

| 类型 | 说明 |
|------|------|
| `ClientPeer` | 单个 WebSocket 连接，含账号、房间、主机标记 |
| `GameRoom` | 在线房间，含 `GameSession`、座位分配、AI 槽位 |
| `PlayerAccount` | 账号/密码/昵称，存内存（重启恢复默认） |

**账号系统**

- 内置账号：`tian/tian`、`mdxz/mdxz`。
- 管理员通过 `ADMIN_AUTH` + 密钥 `xiaozhi2048-admin` 进入，可用 `ADD_PLAYER` 新增账号。
- 同一账号不允许多处同时登录。

**房间管理**

- 房间 Key 为 4 位随机数字，不重复。
- 创建房间有 10 秒冷却；已在未开始房间中不能再创建。
- 只有房主（`isHost`）可以点击 `START`。
- 成员离开后房间自动清理。

**规则裁判**

- `START` 后 `GameRoom` 持有 `GameSession`（来自共享库）。
- `SELECT`/`MOVE`/`FINISH`/`PASS` 指令由服务端调用 `GameSession` 执行，广播 `STATE` 快照给所有房间成员。
- AI 槽位由服务端自动驱动（房主可设置哪些座位为 AI）。

---

## 网页端

文件：`Web/index.html`、`Web/styles.css`、`Web/app.js`

- 纯静态，由服务端 `UseStaticFiles` 托管。
- 与服务端通过同一 WebSocket 协议通信。
- 棋盘使用 Canvas 2D 绘制，视觉参数与 Unity 端对齐（相同颜色常量、相同格子尺寸比例）。
- 棋盘旋转为"自己的棋在下方"，各角落显示玩家昵称。
- 大厅显示可加入的发现房间列表。

---

## 编辑器工具

目录：`Assets/Scripts/Editor/`

| 文件 | 菜单 | 职责 |
|------|------|------|
| `MainSceneBootstrap.cs` | `Tools/SweetJumpJump/Create Or Update MainScene` | 创建/更新主场景并加入 Build Settings |
| `StageOneVerifier.cs` | `Tools/SweetJumpJump/Verify Stage One` | 验证棋盘 121 格、营地 10 格、默认房间、真人先手、AI 可行动 |
| `StageTwoVerifier.cs` | `Tools/SweetJumpJump/Verify Stage Two` | 验证房间校验、悔棋、空跳、三档 AI |
| `StageThreeBuildTools.cs` | `Tools/SweetJumpJump/Verify Stage Three` / `Export Xcode Project` | 运行一/二阶段回归、配置 iPad 参数、导出 Xcode 工程到 `Builds/iOS` |

## 当前已知取舍

- 当前为本地单机，未实现联网/局域网/账号/广告。
- UI 为代码生成，利于版本控制，但不适合非程序同学在 Unity Inspector 里拖拽编辑。
- 程序化美术资源便于提交和复现，但如果后续有正式美术，应迁移到 `Assets/Art/` 并保留 `.meta` 文件。
