# SweetJumpJump 在线模式

## 启动服务器

服务器监听本机 `53333` 端口，适合配合 frpc 把 `frp-put.com:53333` 映射到这台 Mac。

```bash
dotnet run --project Server/SweetJumpJump.Server.csproj -- 53333
```

发布后的可执行文件在：

```bash
Server/bin/Release/net7.0/osx-arm64/publish/SweetJumpJumpServer
```

## 进入游戏

1. 打开 Mac 版游戏。
2. 点「在线玩」。
3. 房主点「创建房间」，把 4 位房间密钥发给其他人。
4. 其他玩家输入密钥并点「加入房间」。
5. 每位玩家点「准备」。
6. 房主点「开始本局」。

棋盘开始后，所有玩家的走子、完成回合、放弃移动都会通过 TCP 同步到各自终端的棋盘。
