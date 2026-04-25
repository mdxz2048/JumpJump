# 构建与部署

## 环境

- macOS
- Unity `2022.3.62f3`
- Xcode
- iPad 真机
- Apple Development 证书和可用的自动签名 Team

当前项目设置：

- Product Name：`甜姐的跳跳棋`
- Bundle ID：`com.lvzhipeng.sweetjumpjump`
- iOS Team ID：`J3M89K2N56`
- iOS Target：iPad only
- Orientation：Portrait
- Scripting Backend：IL2CPP

## Unity 内验证

Unity 菜单：

- `Tools/SweetJumpJump/Verify Stage One`
- `Tools/SweetJumpJump/Verify Stage Two`
- `Tools/SweetJumpJump/Verify Stage Three`

命令行：

```bash
'/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity' \
  -batchmode -nographics -quit \
  -projectPath '/path/to/SweetJumpJump' \
  -executeMethod SweetJumpJump.Editor.StageThreeBuildTools.VerifyStageThree \
  -logFile /tmp/sweetjumpjump_stage3.log
```

成功日志应包含：

```text
StageOneVerifier passed
StageTwoVerifier passed
StageThreeVerifier passed
```

## 导出 Xcode 工程

Unity 菜单：

- `Tools/SweetJumpJump/Export Xcode Project`

命令行：

```bash
'/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity' \
  -batchmode -nographics -quit \
  -projectPath '/path/to/SweetJumpJump' \
  -executeMethod SweetJumpJump.Editor.StageThreeBuildTools.ExportXcodeProject \
  -logFile /tmp/sweetjumpjump_export.log
```

导出目录：

```text
Builds/iOS/
```

注意：`Builds/` 是构建产物，默认不提交到 GitHub。其他机器需要重新导出。

## Xcode 真机构建

示例命令：

```bash
'/Applications/Xcode.app/Contents/Developer/usr/bin/xcodebuild' \
  -project '/path/to/SweetJumpJump/Builds/iOS/Unity-iPhone.xcodeproj' \
  -scheme Unity-iPhone \
  -destination 'id=<IPAD_DEVICE_ID>' \
  -configuration Debug \
  -derivedDataPath /tmp/sweetjumpjump-ios-dd \
  -allowProvisioningUpdates \
  build
```

成功日志应包含：

```text
** BUILD SUCCEEDED **
```

常见非阻塞警告：

- `A 1024x1024 app store icon is required for iOS apps`

这不会阻止真机安装，但正式上架前需要补齐 App Store 图标。

## 安装到 iPad

先查设备：

```bash
DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
/usr/bin/xcrun devicectl list devices
```

安装：

```bash
DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
/usr/bin/xcrun devicectl device install app \
  --device <IPAD_DEVICE_ID> \
  /tmp/sweetjumpjump-ios-dd/Build/Products/Debug-iphoneos/ProductName.app
```

启动：

```bash
DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
/usr/bin/xcrun devicectl device process launch \
  --device <IPAD_DEVICE_ID> \
  com.lvzhipeng.sweetjumpjump
```

成功日志应包含：

```text
App installed
Launched application with com.lvzhipeng.sweetjumpjump bundle identifier.
```

## 签名排查

如果遇到：

```text
No Account for Team
No profiles for com.lvzhipeng.sweetjumpjump were found
```

处理方式：

- 打开 Xcode。
- 打开 `Builds/iOS/Unity-iPhone.xcodeproj`。
- 选择 `Unity-iPhone` target。
- 进入 `Signing & Capabilities`。
- 登录 Apple ID。
- 勾选 `Automatically manage signing`。
- Team 选择可用团队。
- 确认 Bundle Identifier 是 `com.lvzhipeng.sweetjumpjump`。

如果 Team ID 变化，同步更新：

```text
Assets/Scripts/Editor/StageThreeBuildTools.cs
```
