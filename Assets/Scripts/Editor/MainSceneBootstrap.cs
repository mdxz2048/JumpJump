#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SweetJumpJump.Editor
{
    public static class MainSceneBootstrap
    {
        [MenuItem("Tools/SweetJumpJump/Create Or Update MainScene")]
        public static void CreateOrUpdateMainScene()
        {
            const string scenesFolder = "Assets/Scenes";
            const string scenePath = "Assets/Scenes/MainScene.unity";

            if (!AssetDatabase.IsValidFolder(scenesFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraObject = new GameObject("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(1f, 0.96f, 0.97f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;

            Directory.CreateDirectory(Path.GetDirectoryName(scenePath) ?? scenesFolder);
            EditorSceneManager.SaveScene(scene, scenePath);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(scenePath, true)
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("MainScene 已创建并加入 Build Settings。");
        }
    }
}
#endif
