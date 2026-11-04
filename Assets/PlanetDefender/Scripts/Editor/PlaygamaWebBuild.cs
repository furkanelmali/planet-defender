using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PlanetDefender.EditorTools
{
    // Playgama WebGL release: Bridge template, no splash, gzip with decompression fallback, no IndexedDB cache
    // (Bridge Storage is the only save channel), ZIP with index.html at its root.
    public static class PlaygamaWebBuild
    {
        public const string Version = "1.1";
        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Playgama"));
        public static string Output => Path.Combine(Root, "WebGL");
        public static string Archive => Path.Combine(Root, $"PlanetDefender_Playgama_v{Version}.zip");

        [MenuItem("Planet Defender/Playgama/Configure WebGL")]
        public static void Configure()
        {
            var web = NamedBuildTarget.WebGL;
            PlayerSettings.WebGL.template = "PROJECT:Bridge";
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = false;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.showDiagnostics = false;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.WebGL.initialMemorySize = 64;
            PlayerSettings.WebGL.maximumMemorySize = 1024;
            PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
            PlayerSettings.WebGL.powerPreference = WebGLPowerPreference.HighPerformance;
            PlayerSettings.SetManagedStrippingLevel(web, ManagedStrippingLevel.Medium);
            PlayerSettings.SetIl2CppCodeGeneration(web, Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.SetIl2CppCompilerConfiguration(web, Il2CppCompilerConfiguration.Master);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Planet Defender/Playgama/Build WebGL + ZIP")]
        public static void Build() => BuildTo(Output, BuildOptions.None, true);

        // Batch: -executeMethod PlanetDefender.EditorTools.PlaygamaWebBuild.BatchBuild
        public static void BatchBuild()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        // Faster local check build (no IL2CPP master optimisation, no ZIP).
        public static void BatchBuildDev()
        {
            try
            {
                PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL, Il2CppCompilerConfiguration.Release);
                BuildTo(Path.Combine(Root, "WebGL-dev"), BuildOptions.None, false);
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static void BuildTo(string output, BuildOptions options, bool package)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                throw new InvalidOperationException("WebGL Build Support is not installed for this editor.");
            if (output == Output) Configure();
            if (Directory.Exists(output)) Directory.Delete(output, true);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { PlanetDefenderBuilder.ScenePath }, locationPathName = output,
                target = BuildTarget.WebGL, targetGroup = BuildTargetGroup.WebGL, options = options
            });
            var summary = report.summary;
            Debug.Log($"PD_WEBGL_BUILD {summary.result} size={summary.totalSize} errors={summary.totalErrors} time={summary.totalTime}");
            if (summary.result != BuildResult.Succeeded) throw new InvalidOperationException("WebGL build failed: " + summary.result);
            if (package) Package();
        }

        [MenuItem("Planet Defender/Playgama/Package ZIP")]
        public static void Package()
        {
            foreach (var required in new[] { "index.html", "playgama-bridge-config.json", "playgama-bridge.js", "playgama-bridge-unity.js" })
                if (!File.Exists(Path.Combine(Output, required))) throw new FileNotFoundException("Build is missing " + required);
            var bad = Directory.GetFiles(Output, "*", SearchOption.AllDirectories).Select(f => f.Substring(Output.Length + 1)).Where(f => f.Any(c => c > 127)).ToArray();
            if (bad.Length > 0) throw new InvalidOperationException("Non-Latin file names: " + string.Join(", ", bad));
            if (File.Exists(Archive)) File.Delete(Archive);
            ZipFile.CreateFromDirectory(Output, Archive, System.IO.Compression.CompressionLevel.Optimal, false);
            Debug.Log($"PD_ZIP_OK {Archive} bytes={new FileInfo(Archive).Length}");
        }
    }
}
