using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using System.IO;
using System;

[InitializeOnLoad]
public class CompilationWatchdog
{
    private static DateTime compilationStartTime;
    private static bool isCompiling = false;
    private static readonly int COMPILATION_TIMEOUT_MINUTES = 5;
    private static readonly string PREFS_KEY = "CompilationWatchdog_LastCheck";

    static CompilationWatchdog()
    {
        CompilationPipeline.compilationStarted += OnCompilationStarted;
        CompilationPipeline.compilationFinished += OnCompilationFinished;
        EditorApplication.update += CheckCompilationTimeout;

        // Check if we're recovering from a hang
        CheckForPreviousHang();
    }

    private static void OnCompilationStarted(object obj)
    {
        isCompiling = true;
        compilationStartTime = DateTime.Now;
        EditorPrefs.SetString(PREFS_KEY, compilationStartTime.ToString());
        Debug.Log($"[CompilationWatchdog] Compilation started at {compilationStartTime:HH:mm:ss}");
    }

    private static void OnCompilationFinished(object obj)
    {
        isCompiling = false;
        TimeSpan duration = DateTime.Now - compilationStartTime;
        Debug.Log($"[CompilationWatchdog] Compilation finished. Duration: {duration.TotalSeconds:F1}s");
        EditorPrefs.DeleteKey(PREFS_KEY);
    }

    private static void CheckCompilationTimeout()
    {
        if (!isCompiling) return;

        TimeSpan compilationDuration = DateTime.Now - compilationStartTime;

        if (compilationDuration.TotalMinutes > COMPILATION_TIMEOUT_MINUTES)
        {
            Debug.LogWarning($"[CompilationWatchdog] Compilation has been running for {compilationDuration.TotalMinutes:F1} minutes!");
            Debug.LogWarning("[CompilationWatchdog] This may indicate a compilation hang. Consider using the menu: Tools > Fix Compilation Hang");
        }
    }

    private static void CheckForPreviousHang()
    {
        if (EditorPrefs.HasKey(PREFS_KEY))
        {
            string startTimeStr = EditorPrefs.GetString(PREFS_KEY);
            if (DateTime.TryParse(startTimeStr, out DateTime lastStartTime))
            {
                TimeSpan timeSinceStart = DateTime.Now - lastStartTime;

                if (timeSinceStart.TotalMinutes > COMPILATION_TIMEOUT_MINUTES)
                {
                    Debug.LogWarning($"[CompilationWatchdog] Detected previous compilation hang (started {timeSinceStart.TotalMinutes:F0} minutes ago)");

                    bool autoFix = EditorUtility.DisplayDialog(
                        "Compilation Hang Detected",
                        $"Unity appears to have crashed during compilation.\n\nLast compilation started: {timeSinceStart.TotalMinutes:F0} minutes ago\n\nWould you like to clean the build cache automatically?",
                        "Clean Cache Now",
                        "Ignore"
                    );

                    if (autoFix)
                    {
                        CleanBuildCache();
                    }
                }
            }

            EditorPrefs.DeleteKey(PREFS_KEY);
        }
    }

    [MenuItem("Tools/Fix Compilation Hang")]
    private static void FixCompilationHangMenu()
    {
        bool confirm = EditorUtility.DisplayDialog(
            "Fix Compilation Hang",
            "This will clean the following folders:\n\n" +
            "• Library/Bee\n" +
            "• Library/ScriptAssemblies\n" +
            "• Library/ArtifactDB\n" +
            "• Library/StateCache\n\n" +
            "Unity will need to recompile all scripts.\n\nContinue?",
            "Clean Cache",
            "Cancel"
        );

        if (confirm)
        {
            CleanBuildCache();
        }
    }

    [MenuItem("Tools/Fix Asset Database Hang")]
    private static void FixAssetDatabaseHangMenu()
    {
        bool confirm = EditorUtility.DisplayDialog(
            "Fix Asset Database Hang",
            "This will clean the following:\n\n" +
            "• Library/SourceAssetDB\n" +
            "• Library/metadata\n" +
            "• Library/ShaderCache\n" +
            "• All .lock files\n\n" +
            "Unity will need to reimport all assets.\n\nContinue?",
            "Clean Now",
            "Cancel"
        );

        if (confirm)
        {
            CleanAssetDatabase();
        }
    }

    [MenuItem("Tools/Deep Clean (Compilation + Asset Database)")]
    private static void DeepCleanMenu()
    {
        bool confirm = EditorUtility.DisplayDialog(
            "Deep Clean",
            "This will perform a complete clean:\n\n" +
            "• All compilation cache\n" +
            "• All asset database files\n" +
            "• All lock files\n\n" +
            "Unity will reimport everything.\n\nContinue?",
            "Deep Clean",
            "Cancel"
        );

        if (confirm)
        {
            CleanBuildCache();
            CleanAssetDatabase();
        }
    }

    private static void CleanBuildCache()
    {
        Debug.Log("[CompilationWatchdog] Cleaning build cache...");

        string[] foldersToClean = {
            "Library/Bee",
            "Library/ScriptAssemblies",
            "Library/ArtifactDB",
            "Library/ArtifactDB-lock",
            "Library/StateCache"
        };

        int cleanedCount = CleanPaths(foldersToClean);
        Debug.Log($"[CompilationWatchdog] Cleaned {cleanedCount} items. Requesting script reload...");

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "Build Cache Cleaned",
            $"Successfully cleaned {cleanedCount} items.\n\nUnity will now recompile scripts.",
            "OK"
        );
    }

    private static void CleanAssetDatabase()
    {
        Debug.Log("[CompilationWatchdog] Cleaning Asset Database...");

        string[] foldersToClean = {
            "Library/SourceAssetDB",
            "Library/SourceAssetDB-lock",
            "Library/metadata",
            "Library/ShaderCache",
            "Library/ShaderCache.db"
        };

        int cleanedCount = CleanPaths(foldersToClean);

        // Clean all .lock files in Library
        string libraryPath = Path.Combine(Application.dataPath, "..", "Library");
        if (Directory.Exists(libraryPath))
        {
            string[] lockFiles = Directory.GetFiles(libraryPath, "*.lock", SearchOption.AllDirectories);
            foreach (string lockFile in lockFiles)
            {
                try
                {
                    File.Delete(lockFile);
                    Debug.Log($"[CompilationWatchdog] Deleted lock file: {Path.GetFileName(lockFile)}");
                    cleanedCount++;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[CompilationWatchdog] Could not delete {lockFile}: {e.Message}");
                }
            }
        }

        Debug.Log($"[CompilationWatchdog] Cleaned {cleanedCount} items. Requesting asset reimport...");

        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        EditorUtility.DisplayDialog(
            "Asset Database Cleaned",
            $"Successfully cleaned {cleanedCount} items.\n\nUnity will now reimport all assets.",
            "OK"
        );
    }

    private static int CleanPaths(string[] paths)
    {
        int cleanedCount = 0;
        foreach (string path in paths)
        {
            string fullPath = Path.Combine(Application.dataPath, "..", path);

            try
            {
                if (Directory.Exists(fullPath))
                {
                    Directory.Delete(fullPath, true);
                    Debug.Log($"[CompilationWatchdog] Deleted: {path}");
                    cleanedCount++;
                }
                else if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    Debug.Log($"[CompilationWatchdog] Deleted: {path}");
                    cleanedCount++;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CompilationWatchdog] Could not delete {path}: {e.Message}");
            }
        }
        return cleanedCount;
    }

    [MenuItem("Tools/Compilation Info")]
    private static void ShowCompilationInfo()
    {
        if (isCompiling)
        {
            TimeSpan duration = DateTime.Now - compilationStartTime;
            EditorUtility.DisplayDialog(
                "Compilation Status",
                $"Compilation is currently running.\n\n" +
                $"Started: {compilationStartTime:HH:mm:ss}\n" +
                $"Duration: {duration.TotalSeconds:F1}s",
                "OK"
            );
        }
        else
        {
            EditorUtility.DisplayDialog(
                "Compilation Status",
                "No compilation is currently running.",
                "OK"
            );
        }
    }
}
