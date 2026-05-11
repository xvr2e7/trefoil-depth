using System.IO;
using UnityEditor;
using UnityEngine;

public static class OpenDataFolderMenu
{
    [MenuItem("Tools/Open Persistent Data Folder")]
    public static void OpenPersistentDataFolder()
    {
        string path = Application.persistentDataPath;
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        Debug.Log($"[Tools] persistentDataPath = {path}");
        EditorUtility.RevealInFinder(path);
    }
}
