# SweetJumpJump 在线模式

## 总览

当前在线模式由三部分组成：

- Unity iPad 客户端：`Assets/Scripts/SweetJumpJump/`
- ASP.NET Core WebSocket 服务端：`Server/Program.cs`
- 网页端：`Web/`

服务端是唯一规则裁判，Unity 和网页端都只发送用户意图，再接收服务端广播的 `STATE` 快照。

## 启动本地网页 + 规则服务器

```bash
dotnet run --project Server/SweetJumpJump.Server.csproj -- 53333
```

打开：

```text
http://127.0.0.1:53333/
```

## 登录与账号

- 默认账号：`tian / tian`、`mdxz / mdxz`
- 服务端会把账号数据持久化到 `data/accounts.json`
- 网页端登录成功后会保存 `sessionToken` 到 `localStorage`
- 刷新或重连后，网页端会优先使用 `AUTH_TOKEN` 自动恢复登录
- 同一账号重复登录时，会提示是否踢掉已有会话
- iPad 端在线模式同样需要先登录，登录成功后才进入在线大厅

## 房间与对局流程

1. 登录
2. 建房或加入 4 位房间号
3. 选座位
4. 房主开始本局
5. 服务端广播 `STATE`，各端同步进入棋局

补充规则：

- 创建房间有 10 秒冷却
- 已在未开始房间中时不能重复建房
- 只有房主可以开始本局、重开本局、解散房间
- 断线后服务端会尝试按账号恢复房间和座位

## 网页端当前体验

- 棋盘视角固定为“自己的棋在下方”
- 底部栏显示当前身份对应的棋子颜色
- 音效事件与 iPad 端对齐
- 自动完成提醒改为底部 10 秒倒计时，最后 3 秒高亮
- 大厅支持发现并点击加入可用房间

## 管理员入口

首页连续点击圆形标记 7 次，会显示管理员入口。

- 管理员密钥：`xiaozhi2048-admin`
- 可查看在线成员、房间状态
- 可新增、禁用、启用、删除棋手账号

## 生产部署

当前生产部署结构：

- 服务目录：`/opt/sweetjumpjump`
- 服务名：`sweetjumpjump`
- 静态网页目录：`/opt/sweetjumpjump/Web`
- 对外访问：`https://jump.mddxz.top/`

只更新网页端时，可直接同步 `Web/` 到远端对应目录；静态文件通常不需要重启服务即可生效。
