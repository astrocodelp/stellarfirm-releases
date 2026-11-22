using System;
using System.IO;
using Emberfall.Gameplay;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Emberfall.Editor
{
    /// <summary>
    /// CI entry point (game-ci/unity-builder buildMethod). Creates the graybox
    /// scene from code — no scene authoring needed — registers it in the build
    /// settings and builds a player that boots straight into the game.
    /// Also exposed as a menu item so the same scene can be made in the Editor.
    /// </summary>
    public static class BuildScript
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Emberfall/Create Playable Scene")]
        public static void CreatePlayableScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Emberfall");
            root.AddComponent<GrayboxBootstrap>();
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("Emberfall: playable scene saved to " + ScenePath + ". Press Play.");
        }

        // Invoked by .github/workflows/build-windows.yml via -executeMethod.
        public static void BuildWindows()
        {
            Build(BuildTarget.StandaloneWindows64, ".exe");
        }

        private static void Build(BuildTarget target, string extension)
        {
            if (!File.Exists(ScenePath)) CreatePlayableScene();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            string location = GetCommandLineArg("-customBuildPath");
            if (string.IsNullOrEmpty(location))
                location = "build/" + target + "/Emberfall";
            if (!location.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                location += extension;
            Directory.CreateDirectory(Path.GetDirectoryName(location));

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = location,
                target = target,
                options = BuildOptions.None
            };

            Debug.Log("Emberfall: building " + target + " to " + location);
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("Emberfall: build failed (" + report.summary.result + "), " +
                               report.summary.totalErrors + " errors.");
                EditorApplication.Exit(1);
                return;
            }
            Debug.Log("Emberfall: build succeeded, " + report.summary.totalSize + " bytes.");
        }

        private static string GetCommandLineArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
