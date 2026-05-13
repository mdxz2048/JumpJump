# 甜姐的跳跳棋 / Sweet Jump Jump

Unity iPad 竖屏跳跳棋游戏，含本地对战 + WebSocket 在线联网对战。已完成第一至三阶段，并在 iPad Air 11-inch 真机上验证。

## 修订记录

### 2026-05-10

- 更新所有文档以反映当前代码：在线联网模式、WebSocket 协议、共享 Core 库、NativeMusicPicker 等。

### 2026-04-26

- 调整棋盘视觉为单线六边形边框，格子内增加小圆单线。
- 增加选中棋子局部高光效果，强化可走目标位置提示。
- 修改完成规则：单个玩家完成后只在底部文字提示，游戏继续，不再弹出胜利弹窗。

## 当前功能

### 本地对战

- Splash、主菜单、游戏选项、房间列表、房间创建/编辑/删除。
- 标准 121 格六角星跳棋棋盘，6 个营地各 10 枚棋子。
- 默认房间：下方真人玩家 vs 上方普通 AI。
- 支持 2 人、4 人、6 人成对座位配置。
- 支持相邻移动、一子跳、空跳、多跳、完成移动、放弃移动、本回合悔棋。
- 支持初学者、普通、高级三档 AI。
- 支持催促提示、音效、背景音乐、玩家完成文字提示。
- 雪山背景、浅蓝六边形棋格、纹理棋子、底部半透明操作栏。
- 自定义背景音乐：iOS 本机音乐选择器（`NativeMusicPicker`）、从文件导入。
- iPad 竖屏 iOS 导出、Xcode 真机构建、iPad 安装与启动已验证。

### 在线联网对战

- Unity 客户端通过 WebSocket（`OnlineClient`）连接服务端 `/ws`。
- 服务端（ASP.NET Core）负责账号认证、房间管理、规则裁判，向所有客户端广播 `STATE` 快照。
- 网页端（`Web/`）同样通过 WebSocket 参与对局，与 Unity 客户端共享同一服务端。
- 在线对局中棋盘旋转为"自己的棋在下方"，各阵营角落显示玩家昵称。
- 管理员入口：首页连续点击圆形标记 7 次，密钥为 `xiaozhi2048-admin`。
- 内置账号：`tian/tian`、`mdxz/mdxz`（服务重启恢复默认）。

## 工程信息

- Unity 版本：`2022.3.62f3`
- 主场景：`Assets/Scenes/MainScene.unity`
- Bundle ID：`com.lvzhipeng.sweetjumpjump`
- iOS 目标：iPad only（`iPhoneAndiPad`）、Portrait、IL2CPP
- 主要命名空间：Unity 端 `SweetJumpJump`，服务端共享库 `SweetJumpJump.Core`

## 目录说明

| 目录 | 内容 |
|------|------|
| `Assets/Scenes/` | Unity 场景 |
| `Assets/Scripts/SweetJumpJump/` | 运行时代码：规则、UI、存档、在线网络 |
| `Assets/Scripts/Editor/` | 编辑器工具：阶段验证、iOS 导出 |
| `Shared/SweetJumpJump.Core/` | 服务端/客户端共享规则库（.NET 标准库） |
| `Server/` | ASP.NET Core WebSocket 服务端 |
| `Web/` | 静态网页端（HTML/CSS/JS） |
| `Packages/` | Unity 包依赖声明 |
| `ProjectSettings/` | Unity 工程设置 |
| `docs/` | 架构、构建、AI 交接文档 |

## 不提交到 GitHub 的内容

- `Library/`、`Logs/`、`UserSettings/`、`Builds/`、`Temp/`
- Xcode `DerivedData/`、`.ipa`、`.xcarchive` 等构建产物
- `Server/bin/`、`Server/obj/`、`Shared/*/bin/`、`Shared/*/obj/`

## 快速运行（本地对战）

1. 用 Unity `2022.3.62f3` 打开仓库根目录。
2. 打开 `Assets/Scenes/MainScene.unity`，点击 Play。

场景丢失时在 Unity 菜单执行 `Tools/SweetJumpJump/Create Or Update MainScene`。

## 快速运行（在线服务端）

```bash
dotnet run --project Server/SweetJumpJump.Server.csproj -- 53333
```

访问 `http://127.0.0.1:53333/` 进入网页端。

## 常用验证

Unity 菜单：`Tools/SweetJumpJump/Verify Stage One / Two / Three`

命令行：

```bash
'/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity' \
  -batchmode -nographics -quit \
  -projectPath '/path/to/SweetJumpJump' \
  -executeMethod SweetJumpJump.Editor.StageThreeBuildTools.VerifyStageThree \
  -logFile /tmp/sweetjumpjump_verify.log
```

更多细节见 `docs/ARCHITECTURE.md`、`docs/BUILD_AND_DEPLOY.md`、`docs/AI_HANDOFF.md`。
