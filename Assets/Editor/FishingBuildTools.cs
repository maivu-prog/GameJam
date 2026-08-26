using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RustyFishing.Editor
{
    public static class FishingBuildTools
    {
        [MenuItem("Rusty Fishing/Reset Progression (delete save)")]
        public static void ResetProgression()
        {
            // If a game is running, reset it live; otherwise just delete the save file on disk so the
            // next Play session starts fresh.
            var controller=Object.FindFirstObjectByType<FishingGameController>();
            if(Application.isPlaying&&controller!=null)controller.ResetProgression();
            else PlayerSave.DeleteFile();
            Debug.Log("Rusty Fishing progression reset (save cleared).");
        }

        [MenuItem("Rusty Fishing/Build Android APK")]
        public static void BuildAndroid()
        {
            // Requires the Android Build Support module (install it via Unity Hub ▸ Add Modules).
            Directory.CreateDirectory("Builds/Android");
            var options=new BuildPlayerOptions
            {
                scenes=new[]{"Assets/Scenes/SampleScene.unity"},
                locationPathName="Builds/Android/RustyFishing.apk",
                target=BuildTarget.Android,
                options=BuildOptions.Development // drop this for a clean release APK
            };
            var report=BuildPipeline.BuildPlayer(options);
            if(report.summary.result!=BuildResult.Succeeded)throw new System.Exception($"Android build failed: {report.summary.result}");
            Debug.Log($"Rusty Fishing APK built: {report.summary.totalSize} bytes → {options.locationPathName}");
        }

        /// <summary>
        /// Build the web player. Runs from the menu, or headless:
        ///   Unity.exe -batchmode -quit -projectPath . -executeMethod RustyFishing.Editor.FishingBuildTools.BuildWeb
        ///
        /// Decompression Fallback is forced on. Without it the build expects the SERVER to announce
        /// Content-Encoding for the .br files, which itch.io, GitHub Pages and most static hosts will not
        /// do -- and the page just fails to load with no useful message.
        /// </summary>
        [MenuItem("Rusty Fishing/Build Web (WebGL)")]
        public static void BuildWeb()
        {
            const string outDir = "Builds/Web";
            Directory.CreateDirectory(outDir);

            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/SampleScene.unity" },
                locationPathName = outDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,   // release: a development web build is far larger and slower
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.Exception($"Web build failed: {report.summary.result}");
            Debug.Log($"Rusty Fishing web build complete: {report.summary.totalSize / 1048576} MB -> {outDir}");
        }

        [MenuItem("Rusty Fishing/Build Windows Development")]
        public static void BuildWindows()
        {
            Directory.CreateDirectory("Builds/Windows");
            var options=new BuildPlayerOptions
            {
                scenes=new[]{"Assets/Scenes/SampleScene.unity"},
                locationPathName="Builds/Windows/RustyFishing.exe",
                target=BuildTarget.StandaloneWindows64,
                options=BuildOptions.Development
            };
            var report=BuildPipeline.BuildPlayer(options);
            if(report.summary.result!=BuildResult.Succeeded)throw new System.Exception($"Build failed: {report.summary.result}");
            Debug.Log($"Rusty Fishing build complete: {report.summary.totalSize} bytes");
        }
    }
}
