# AI 交接说明

## 先读这些文件

1. `README.md`
2. `README_ONLINE.md`
3. `docs/ARCHITECTURE.md`
4. `docs/BUILD_AND_DEPLOY.md`
5. `Assets/Scripts/SweetJumpJump/AppRuntime.cs`
6. `Assets/Scripts/SweetJumpJump/OnlineNetworking.cs`
7. `Shared/SweetJumpJump.Core/ChineseCheckersCore.cs`
8. `Shared/SweetJumpJump.Core/DataModels.cs`
9. `Server/Program.cs`
10. `Web/app.js`

## 当前项目状态

- 本地对战：可用
- 在线模式：Unity / Web / Server 已跑通同一套 WebSocket 协议
- 网页端：已支持自动登录恢复、底部身份棋子显示、音效对齐、10 秒自动完成提醒
- iPad 端：在线模式已改为先登录再进大厅
- 生产环境：部署在 Linux，systemd 服务名 `sweetjumpjump`，目录 `/opt/sweetjumpjump`

## 修改原则

| 修改内容 | 优先位置 |
|----------|----------|
| 规则逻辑 | `Shared/SweetJumpJump.Core/ChineseCheckersCore.cs`，再同步 Unity 端 |
| Unity UI / iPad 在线流程 | `Assets/Scripts/SweetJumpJump/AppRuntime.cs` |
| Unity 网络 | `Assets/Scripts/SweetJumpJump/OnlineNetworking.cs` |
| 服务端认证、房间、裁判 | `Server/Program.cs` |
| 网页端 | `Web/app.js`、`Web/index.html`、`Web/styles.css` |
| iOS 构建脚本 | `Assets/Scripts/Editor/StageThreeBuildTools.cs` |

## 关键事实

- 服务端现在会把账号持久化到 `data/accounts.json`
- 网页端依赖 `sessionToken` 自动恢复登录
- 生产静态网页目录就是 `/opt/sweetjumpjump/Web`
- 只更新网页端时，直接同步 `Web/` 即可
- 线上服务通过 `systemctl restart sweetjumpjump` 重启

## 常用验证

Unity 菜单：

- `Tools/SweetJumpJump/Verify Stage One`
- `Tools/SweetJumpJump/Verify Stage Two`
- `Tools/SweetJumpJump/Verify Stage Three`

本地服务端：

```bash
dotnet run --project Server/SweetJumpJump.Server.csproj -- 53333
```

生产环境静态网页发布：

```bash
rsync -av --delete Web/ root@<server>:/opt/sweetjumpjump/Web/
```

## 仍需关注的点

- Unity 导出的 iOS 工程会有少量过时 API 警告，但不阻塞安装
- App Store 1024 图标仍未补齐
- 在线端到端流程虽然已打通，但后续每次改协议仍要同时回归 Unity / Web / Server
