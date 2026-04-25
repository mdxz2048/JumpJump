# AI 交接说明

## 先读这些文件

1. `README.md`
2. `docs/ARCHITECTURE.md`
3. `docs/BUILD_AND_DEPLOY.md`
4. `Assets/Scripts/SweetJumpJump/DataModels.cs`
5. `Assets/Scripts/SweetJumpJump/ChineseCheckersCore.cs`
6. `Assets/Scripts/SweetJumpJump/AppRuntime.cs`
7. `Assets/Scripts/Editor/StageThreeBuildTools.cs`

## 当前项目状态

- Unity 本地单机跳棋原型。
- 第一、二、三阶段已完成。
- 已完成 iPad 真机构建、安装、启动验证。
- 最新 UI 已改为参考图风格的棋盘界面。
- 当前没有联网、账号、广告、局域网对战。

## 代码修改原则

- 规则优先改 `ChineseCheckersCore.cs`。
- UI/主题/音频/存档优先改 `AppRuntime.cs`。
- iOS 构建流程优先改 `StageThreeBuildTools.cs`。
- 改规则后必须跑 Stage One/Two/Three verifier。
- 不要提交 `Library/`、`Logs/`、`UserSettings/`、`Builds/`。
- Unity 资源如果新增到 `Assets/`，必须同时保留 `.meta` 文件。

## 常用验证命令

```bash
'/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity' \
  -batchmode -nographics -quit \
  -projectPath '/path/to/SweetJumpJump' \
  -executeMethod SweetJumpJump.Editor.StageThreeBuildTools.VerifyStageThree \
  -logFile /tmp/sweetjumpjump_verify.log
```

查错误：

```bash
rg -n "StageThreeVerifier|StageTwoVerifier|StageOneVerifier|error CS|Exception|failed|Failed" /tmp/sweetjumpjump_verify.log
```

## 功能地图

- 房间配置：`RoomConfig`、`BoardLayout.ValidateRoom()`、`RefreshRoomEditor()`。
- 棋盘格生成：`BoardLayout.GenerateAllCells()`。
- 营地生成：`BoardLayout.GenerateCamps()`。
- 选棋/移动：`GameSession.TrySelectPiece()`、`GameSession.TryMoveSelectedPiece()`。
- 跳跃规则：`GetJumpTargets()`、`GetSpaceJumpTargets()`。
- AI：`GetBestAiMove()`、`ScoreMove()`、`GetAllMovesForSlot()`。
- 胜利：`HasPlayerWon()`。
- UI 创建：`BuildUi()`、`BuildGamePanel()`、`EnsureBoardCreated()`。
- UI 刷新：`RefreshBoard()`。
- 程序化视觉：`GenerateMountainBackdropSprite()`、`GenerateHexCellSprite()`、`GeneratePieceSprite()`。
- 存档：`SaveManager.Load()`、`SaveManager.Save()`。

## 后续建议

- 补正式 App Icon，解决 Xcode 的 1024x1024 图标警告。
- 如果继续美术迭代，可新增 `Assets/Art/` 并把棋盘背景、棋子、按钮替换为正式资源。
- 如果增加联网对战，建议先把 `GameSession` 的命令/状态序列化能力补出来，再接网络同步。
- 如果增加测试，可把规则层进一步拆成不依赖 UnityEngine UI 的纯逻辑程序集。
