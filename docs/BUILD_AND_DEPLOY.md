# 构建与部署

## 环境要求

| 工具 | 版本 / 说明 |
|------|------------|
| macOS | 开发机 |
| Unity | `2022.3.62f3` |
| Xcode | 与 iOS 目标匹配的版本 |
| .NET SDK | 7+ |
| iPad | 真机 |
| Apple 证书 | Apple Development 证书 + 可用 Team（自动签名） |

Unity 工程设置由 `StageThreeBuildTools.ConfigureIPadBuild()` 自动写入：

- Product Name：`甜姐的跳跳棋`
- Bundle ID：`com.lvzhipeng.sweetjumpjump`
- iOS Team ID：`J3M89K2N56`
- iOS Target：`iPhoneAndiPad`
- Target OS：`13.0+`
- Orientation：Portrait
- Scripting Backend：IL2CPP

---

## 一、Unity 内验证

在 Unity 编辑器菜单执行：

- `Tools/SweetJumpJump/Verify Stage One`
- `Tools/SweetJumpJump/Verify Stage Two`
- `Tools/SweetJumpJump/Verify Stage Three`

命令行批处理：

```bash
'/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity' \
  -batchmode -nographics -quit \
  -projectPath '/path/to/SweetJumpJump' \
  -executeMethod SweetJumpJump.Editor.StageThreeBuildTools.VerifyStageThree \
  -logFile /tmp/sweetjumpjump_stage3.log
```

成功日志应包含 `StageOneVerifier passed`、`StageTwoVerifier passed`、`StageThreeVerifier passed`。

---

## 二、导出 Xcode 工程

Unity 菜单：`Tools/SweetJumpJump/Export Xcode Project`

命令行：

```bash
'/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity' \
  -batchmode -nographics -quit \
  -projectPath '/path/to/SweetJumpJump' \
  -executeMethod SweetJumpJump.Editor.StageThreeBuildTools.ExportXcodeProject \
  -logFile /tmp/sweetjumpjump_export.log
```

导出目录：`Builds/iOS/`

---

## 三、Xcode 真机构建

```bash
'/Applications/Xcode.app/Contents/Developer/usr/bin/xcodebuild' \
  -project '/path/to/SweetJumpJump/Builds/iOS/Unity-iPhone.xcodeproj' \
  -scheme Unity-iPhone \
  -destination 'id=<IPAD_DEVICE_ID>' \
  -configuration Release \
  build
```

成功日志应包含：`** BUILD SUCCEEDED **`

常见警告：

- `A 1024x1024 app store icon is required for iOS apps`：不影响真机安装，但上架前要补齐
- Unity 生成的少量 iOS 过时 API 警告：当前不阻塞构建

---

## 四、安装到 iPad

查设备：

```bash
DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
/Applications/Xcode.app/Contents/Developer/usr/bin/xcodebuild \
  -showdestinations \
  -scheme Unity-iPhone \
  -project '/path/to/SweetJumpJump/Builds/iOS/Unity-iPhone.xcodeproj'
```

如果需要命令行安装，可使用：

```bash
DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
/usr/bin/xcrun devicectl device install app \
  --device <IPAD_DEVICE_ID> \
  '/path/to/DerivedData/Build/Products/Release-iphoneos/ProductName.app'
```

---

## 五、本地服务端运行

```bash
dotnet run --project Server/SweetJumpJump.Server.csproj -- 53333
```

访问：

```text
http://127.0.0.1:53333/
```

说明：

- 服务端会自动托管 `Web/` 静态网页
- `/ws` 为 WebSocket 入口
- `/health` 为健康检查接口
- 账号数据持久化到 `data/accounts.json`

## 六、生产部署

### 生产机当前结构

- systemd 服务：`sweetjumpjump`
- 工作目录：`/opt/sweetjumpjump`
- 可执行文件：`/opt/sweetjumpjump/SweetJumpJumpServer`
- 静态网页目录：`/opt/sweetjumpjump/Web`
- systemd 环境：`ASPNETCORE_URLS=http://127.0.0.1:53333`

### 只发布网页端

```bash
rsync -av --delete Web/ root@<server>:/opt/sweetjumpjump/Web/
```

静态文件直接从磁盘提供，通常不需要重启服务；如需保险，可执行：

```bash
ssh root@<server> 'systemctl restart sweetjumpjump'
```

### 发布服务端 + 网页端

本地发布：

```bash
dotnet publish Server/SweetJumpJump.Server.csproj -c Release -o /tmp/sweetjumpjump-publish
cp -R Web /tmp/sweetjumpjump-publish/Web
```

上传并替换：

```bash
rsync -av --delete /tmp/sweetjumpjump-publish/ root@<server>:/opt/sweetjumpjump/
ssh root@<server> 'systemctl restart sweetjumpjump && systemctl status sweetjumpjump --no-pager'
```

### 线上检查

```bash
curl -I https://jump.mddxz.top/
curl https://jump.mddxz.top/health
```

## 七、默认账号与管理员

| 账号 | 密码 |
|------|------|
| `tian` | `tian` |
| `mdxz` | `mdxz` |

管理员密钥：`xiaozhi2048-admin`（首页连续点圆形标记 7 次进入管理入口）。

---

## 签名排查

遇到 `No Account for Team` 或 `No profiles for com.lvzhipeng.sweetjumpjump`：

1. 打开 Xcode，打开 `Builds/iOS/Unity-iPhone.xcodeproj`。
2. 选 `Unity-iPhone` target → `Signing & Capabilities`。
3. 登录 Apple ID，勾选 `Automatically manage signing`，选择可用 Team。
4. 确认 Bundle ID 为 `com.lvzhipeng.sweetjumpjump`。

如果 Team ID 变化，同步更新 `Assets/Scripts/Editor/StageThreeBuildTools.cs` 中的 `appleDeveloperTeamID`。
