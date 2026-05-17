#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace SweetJumpJump.Editor
{
    public static class StageThreeBuildTools
    {
        private const string MainScenePath = "Assets/Scenes/MainScene.unity";
        private const string IOSExportPath = "Builds/iOS";
        private const string MacBuildPath = "Builds/Mac/SweetJumpJump.app";

        [MenuItem("Tools/SweetJumpJump/Configure iPad Build")]
        public static void ConfigureIPadBuild()
        {
            EnsureTextMeshProResources();

            PlayerSettings.productName = "甜姐的跳跳棋";
            PlayerSettings.companyName = "lvzhipeng";
            PlayerSettings.applicationIdentifier = "com.lvzhipeng.sweetjumpjump";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = true;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.targetOSVersionString = "13.0";
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.iOS.appleDeveloperTeamID = "J3M89K2N56";

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainScenePath, true)
            };

            AssetDatabase.SaveAssets();
            Debug.Log("iPad build settings configured for portrait iPad play.");
        }

        [MenuItem("Tools/SweetJumpJump/Import TextMeshPro Essentials")]
        public static void EnsureTextMeshProResources()
        {
            if (File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
            {
                return;
            }

            string packagePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Library/PackageCache/com.unity.textmeshpro@3.0.6/Package Resources/TMP Essential Resources.unitypackage");
            if (!File.Exists(packagePath))
            {
                throw new FileNotFoundException("TextMeshPro essential resources package not found.", packagePath);
            }

            AssetDatabase.ImportPackage(packagePath, false);
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
            Debug.Log("TextMeshPro essential resources imported.");
        }

        [MenuItem("Tools/SweetJumpJump/Verify Stage Three")]
        public static void VerifyStageThree()
        {
            StageOneVerifier.Run();
            StageTwoVerifier.Run();
            ConfigureIPadBuild();

            Assert(File.Exists(MainScenePath), "MainScene must exist before iOS export.");
            Assert(PlayerSettings.defaultInterfaceOrientation == UIOrientation.Portrait, "iPad build must default to portrait.");
            Assert(PlayerSettings.iOS.targetDevice == iOSTargetDevice.iPhoneAndiPad, "iOS target device should support iPhone and iPad.");
            Assert(PlayerSettings.GetScriptingBackend(BuildTargetGroup.iOS) == ScriptingImplementation.IL2CPP, "iOS build should use IL2CPP.");

            Debug.Log("StageThreeVerifier passed: stage one/two regressions, portrait iPad settings, and iOS export readiness.");
        }

        [MenuItem("Tools/SweetJumpJump/Export Xcode Project")]
        public static void ExportXcodeProject()
        {
            ConfigureIPadBuild();
            Directory.CreateDirectory(IOSExportPath);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { MainScenePath },
                locationPathName = IOSExportPath,
                target = BuildTarget.iOS,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException("Xcode export failed: " + report.summary.result);
            }

            Debug.Log("Xcode project exported to " + Path.GetFullPath(IOSExportPath));
        }

        [MenuItem("Tools/SweetJumpJump/Build Mac App")]
        public static void BuildMacApp()
        {
            PlayerSettings.productName = "甜姐的跳跳棋";
            PlayerSettings.companyName = "lvzhipeng";
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainScenePath, true)
            };

            Directory.CreateDirectory(Path.GetDirectoryName(MacBuildPath));
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { MainScenePath },
                locationPathName = MacBuildPath,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException("Mac build failed: " + report.summary.result);
            }

            Debug.Log("Mac app built at " + Path.GetFullPath(MacBuildPath));
        }

        [PostProcessBuild]
        public static void ConfigureIosInfoPlist(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS)
            {
                return;
            }

            string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            PlistDocument plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            PlistElementDict root = plist.root;
            root.SetString("NSLocalNetworkUsageDescription", "用于连接同一局域网内的甜姐跳跳棋服务器。");
            PlistElementDict ats = root.CreateDict("NSAppTransportSecurity");
            ats.SetBoolean("NSAllowsArbitraryLoads", true);
            plist.WriteToFile(plistPath);
            Debug.Log("Configured iOS Info.plist for local SweetJumpJump server access.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
#endif
