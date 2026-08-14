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
