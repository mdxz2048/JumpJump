# 甜姐的跳跳棋 / Sweet Jump Jump

Unity iPad 竖屏跳跳棋游戏，包含本地对战、ASP.NET Core WebSocket 在线对战，以及与服务端共用协议的网页端。

## 当前状态

- 本地模式完整可用，Stage One / Two / Three 验证工具可运行。
- 在线模式已打通 `Unity iPad <-> Server <-> Web` 同一套协议。
- 网页端已支持自动登录恢复、断线重连恢复房间、固定“自己的棋在下方”视角、底部身份棋子显示、与 iPad 一致的音效事件、10 秒自动完成提醒。
- iPad 端在线模式已改为先登录再进入在线大厅。
- 生产环境已部署到 Linux 服务器，systemd 服务名为 `sweetjumpjump`，静态网页目录为 `/opt/sweetjumpjump/Web`。

## 修订记录

### 2026-05-16

- 更新文档以反映当前在线模式实现、Web 自动登录、账号持久化、生产部署路径和网页端新交互。

### 2026-05-10

- 更新所有文档以反映当前代码：在线联网模式、WebSocket 协议、共享 Core 库、NativeMusicPicker 等。

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
- 自定义背景音乐：iOS 本机音乐选择器 `NativeMusicPicker`、从文件导入。

### 在线联网对战

- Unity 客户端通过 WebSocket 连接服务端 `/ws`，服务端负责账号认证、房间管理和规则裁判。
- 网页端 `Web/` 与 Unity 客户端共享同一协议和同一服务端状态快照 `STATE`。
- 网页端支持 `sessionToken + localStorage` 自动登录恢复，并在同 token 恢复时自动挤下旧页面。
- 服务端支持按账号恢复原房间与座位，并在断线后继续广播房间状态。
- 网页端棋盘始终保持“自己的棋在下方”；底部栏会显示当前身份对应的棋子颜色。
- 网页端已补齐选棋、移动、完成、提醒、胜利等音效事件。
- 自动完成提醒已改为底部 10 秒倒计时，最后 3 秒高亮提示，不再弹窗。
- 服务端 AI 按步执行移动，并在步与步之间保留短暂停顿，便于观察。
- 管理员入口：首页连续点击圆形标记 7 次，密钥为 `xiaozhi2048-admin`。

## 工程信息

- Unity 版本：`2022.3.62f3`
- 主场景：`Assets/Scenes/MainScene.unity`
- Bundle ID：`com.lvzhipeng.sweetjumpjump`
- iOS 目标：`iPhoneAndiPad`、Portrait、IL2CPP
- 服务端：ASP.NET Core Minimal API + WebSocket
- 共享规则库：`Shared/SweetJumpJump.Core`

## 目录说明

| 目录 | 内容 |
|------|------|
| `Assets/Scenes/` | Unity 场景 |
| `Assets/Scripts/SweetJumpJump/` | 运行时代码：规则、UI、存档、在线网络 |
| `Assets/Scripts/Editor/` | 编辑器工具：阶段验证、iOS 导出 |
| `Shared/SweetJumpJump.Core/` | 服务端/客户端共享规则库 |
| `Server/` | ASP.NET Core WebSocket 服务端 |
| `Web/` | 静态网页端 |
| `docs/` | 架构、构建部署、AI 交接文档 |

## 快速运行

### Unity 本地运行

1. 用 Unity `2022.3.62f3` 打开仓库根目录。
2. 打开 `Assets/Scenes/MainScene.unity`。
3. 点击 Play。

### 本地在线服务

```bash
dotnet run --project Server/SweetJumpJump.Server.csproj -- 53333
```

然后访问：

```text
http://127.0.0.1:53333/
```

### 生产环境

- 服务目录：`/opt/sweetjumpjump`
- systemd 服务：`sweetjumpjump`
- 静态网页目录：`/opt/sweetjumpjump/Web`
- 对外域名：`https://jump.mddxz.top/`

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

更多细节见 `docs/ARCHITECTURE.md`、`docs/BUILD_AND_DEPLOY.md`、`docs/AI_HANDOFF.md`、`README_ONLINE.md`。
