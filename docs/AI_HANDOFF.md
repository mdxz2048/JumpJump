# AI 交接说明

> 本文为下一个接手的 AI 准备。请按顺序阅读，然后再动任何代码。

---

## 第一步：先读这些文件

1. `README.md` — 项目概览、目录说明、快速运行
2. `docs/ARCHITECTURE.md` — 分层架构、所有模块职责、关键 API
3. `docs/BUILD_AND_DEPLOY.md` — 构建、验证、部署流程
4. `Assets/Scripts/SweetJumpJump/DataModels.cs` — 数据类型（Unity 端）
5. `Assets/Scripts/SweetJumpJump/ChineseCheckersCore.cs` — 棋盘规则（Unity 端）
6. `Assets/Scripts/SweetJumpJump/AppRuntime.cs` — UI + 在线 + 存档（Unity 端）
7. `Assets/Scripts/SweetJumpJump/OnlineNetworking.cs` — WebSocket 客户端
8. `Assets/Scripts/SweetJumpJump/NativeMusicPicker.cs` — iOS 本机音乐选择器
9. `Shared/SweetJumpJump.Core/DataModels.cs` — 共享数据类型（服务端版本）
10. `Shared/SweetJumpJump.Core/ChineseCheckersCore.cs` — 规则核心（服务端版本）
11. `Server/Program.cs` — ASP.NET Core 服务端（单文件顶层语句）
12. `Web/app.js` — 网页端逻辑
13. `Assets/Scripts/Editor/StageThreeBuildTools.cs` — iOS 构建工具

---

## 当前项目状态

- **本地对战**：完整可用，三阶段验证全部通过，iPad 真机已验证。
- **在线联网**：Unity 客户端已迁移到 WebSocket 协议（`OnlineClient` in `OnlineNetworking.cs`）；服务端（`Server/Program.cs`）使用 ASP.NET Core + WebSocket，规则由服务端裁判；网页端（`Web/`）与 Unity 端共用同一协议。
- **协议兼容性**：当前 Unity `OnlineClient` 已使用 `ClientWebSocket` 连接 `/ws`，协议与服务端一致（`AUTH/CREATE/JOIN/START/SELECT/MOVE/FINISH/PASS` 等）。但在线对局的完整端到端测试尚未完成，存在细节待完善。

---

## 代码修改原则

| 改什么 | 改哪里 |
|--------|--------|
| 规则逻辑 | 优先改 `Shared/SweetJumpJump.Core/ChineseCheckersCore.cs`（服务端权威），同步改 Unity 端 `ChineseCheckersCore.cs` |
| UI / 视觉 / 存档 | `AppRuntime.cs` |
| 在线网络（Unity 端） | `OnlineNetworking.cs` |
| iOS 构建流程 | `StageThreeBuildTools.cs` |
| 服务端账号/房间/裁判 | `Server/Program.cs` |
| 网页端 | `Web/app.js`、`Web/index.html`、`Web/styles.css` |

**改规则后必须**：

1. 同步更新 Unity 端和共享库两处实现。
2. 跑 Stage One/Two/Three Verifier（见下）。

**不要提交**：`Library/`、`Logs/`、`UserSettings/`、`Builds/`、`Temp/`、`Server/bin/`、`Server/obj/`、`Shared/*/bin/`、`Shared/*/obj/`。

**Unity 资源新增**：`Assets/` 下新增文件必须同时保留 `.meta` 文件。

---

## 功能地图（快速定位代码）

### 规则层

| 功能 | 位置 |
|------|------|
| 棋盘格生成 | `BoardLayout.GenerateAllCells()` |
| 营地生成 | `BoardLayout.GenerateCamps()` |
| 房间校验 | `BoardLayout.ValidateRoom()` |
| 选棋/合法落点 | `GameSession.TrySelectPiece()` |
| 移动/连跳 | `GameSession.TryMoveSelectedPiece()` |
| 悔棋 | `GameSession.TryUndo()` |
| 完成/放弃回合 | `GameSession.TryFinishTurn()` / `TryPassTurn()` |
| 在线直接驱动走子 | `GameSession.TryMovePieceById()` |
| 跳跃规则 | `GetJumpTargets()` / `GetSpaceJumpTargets()` |
| AI 选择 | `GameSession.GetBestAiMove()` / `ScoreMove()` / `GetAllMovesForSlot()` |
| 胜利判定 | `GameSession.HasPlayerWon()` |

### UI 层（Unity）

| 功能 | 位置 |
|------|------|
| UI 根构建 | `BuildUi()` |
| 棋盘面板 | `BuildGamePanel()` |
| 棋盘格创建 | `EnsureBoardCreated()` |
| 棋盘刷新 | `RefreshBoard()` |
| 主题切换 | `ApplyTheme()` |
| 在线面板 | `BuildOnlinePanel()` / `RefreshOnlinePanel()` |
| 棋盘名字显示 | `boardNameButtons` / `boardNameRevealUntil` |

### 在线层

| 功能 | 位置 |
|------|------|
| WebSocket 连接 | `OnlineClient.Connect()` |
| 发消息 | `OnlineClient.Send()` |
| 收消息（主线程消费） | `AppController.Update()` → `onlineClient.TryDequeue()` |
| 服务端指令处理 | `Server/Program.cs HandleCommandAsync()` |
| 服务端 STATE 广播 | `GameRoom.BroadcastStateAsync()` |

### 存档层

| 功能 | 位置 |
|------|------|
| 读存档 | `SaveManager.Load()` |
| 写存档 | `SaveManager.Save()` |
| 存档路径 | `Application.persistentDataPath/sweet_jump_jump_save.json` |
| 版本号 | `AppSaveData.SaveVersion = 4` |

---

## 常用验证命令

Unity 菜单（编辑器中运行）：

- `Tools/SweetJumpJump/Verify Stage One`
- `Tools/SweetJumpJump/Verify Stage Two`
- `Tools/SweetJumpJump/Verify Stage Three`

命令行批处理（CI / 无头验证）：

```bash
'/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity' \
  -batchmode -nographics -quit \
  -projectPath '/path/to/SweetJumpJump' \
  -executeMethod SweetJumpJump.Editor.StageThreeBuildTools.VerifyStageThree \
  -logFile /tmp/sweetjumpjump_verify.log
```

查错误：

```bash
grep -n "StageThreeVerifier\|StageTwoVerifier\|StageOneVerifier\|error CS\|Exception\|failed\|Failed" \
  /tmp/sweetjumpjump_verify.log
```

---

## 已知待完善事项

| 问题 | 说明 |
|------|------|
| 在线对局端到端测试 | Unity ↔ Server ↔ Web 完整流程需要更多测试 |
| App Store 图标 | 1024×1024 图标缺失，Xcode 有警告但不影响真机安装 |
| 账号持久化 | 服务端账号存内存，重启恢复默认；如需持久化，改 `Server/Program.cs` 写文件/数据库 |
| 在线悔棋 | 联网模式下悔棋不可用（设计决策），如需支持需服务端协议扩展 |
| iOS 本机音乐回调 | `NativeMusicPicker` 需要对应 iOS 原生插件（`Plugins/iOS/`），请确认插件文件存在 |

---

## 后续建议

1. **美术资源**：如需正式美术，新增 `Assets/Art/` 并替换 `GeneratePieceSprite`、`GenerateMountainBackdropSprite` 等程序化生成函数。
2. **在线测试**：启动 Server，用两个浏览器窗口和一个 Unity Play 模式同时加入同一房间，验证 `STATE` 广播正确同步。
3. **规则扩展**：优先改 `Shared/SweetJumpJump.Core/ChineseCheckersCore.cs` 并补 Verifier 断言，然后同步 Unity 端。
4. **独立测试**：规则层不依赖 `UnityEngine`（共享库），可直接用 xUnit 编写单元测试。
