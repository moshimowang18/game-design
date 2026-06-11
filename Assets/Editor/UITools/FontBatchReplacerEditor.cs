using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class FontBatchReplacerEditor : EditorWindow
{
    // TMP 字体
    private TMP_FontAsset oldTMPFont;
    private TMP_FontAsset newTMPFont;
    private Material newTMPMaterial; // 用户指定材质

    // UI.Text 字体
    private Font oldUIFont;
    private Font newUIFont;

    // 搜索目录
    private string searchDirectories = "Assets/Prefabs";

    [MenuItem("Tools/Anii/字体批量替换")]
    public static void ShowWindow()
    {
        GetWindow<FontBatchReplacerEditor>("字体批量替换");
    }

    private void OnGUI()
    {
        GUILayout.Label("TMP 字体替换", EditorStyles.boldLabel);
        oldTMPFont = (TMP_FontAsset)EditorGUILayout.ObjectField("原 TMP 字体", oldTMPFont, typeof(TMP_FontAsset), false);
        newTMPFont = (TMP_FontAsset)EditorGUILayout.ObjectField("新 TMP 字体", newTMPFont, typeof(TMP_FontAsset), false);
        newTMPMaterial = (Material)EditorGUILayout.ObjectField("替换材质", newTMPMaterial, typeof(Material), false);

        GUILayout.Space(10);
        GUILayout.Label("UI.Text 字体替换", EditorStyles.boldLabel);
        oldUIFont = (Font)EditorGUILayout.ObjectField("原 UI 字体", oldUIFont, typeof(Font), false);
        newUIFont = (Font)EditorGUILayout.ObjectField("新 UI 字体", newUIFont, typeof(Font), false);

        GUILayout.Space(10);
        GUILayout.Label("Prefab 搜索目录（可用 ; 分隔多个目录）", EditorStyles.label);
        searchDirectories = EditorGUILayout.TextField(searchDirectories);

        GUILayout.Space(20);
        GUILayout.Label("操作按钮", EditorStyles.boldLabel);

        if (GUILayout.Button("替换选中 Prefab 字体"))
        {
            ReplaceSelectedPrefabFont();
        }

        if (GUILayout.Button("替换指定目录下所有 Prefab 字体"))
        {
            ReplacePrefabsInDirectories(searchDirectories.Split(';'));
        }
    }

    // ---------------- 单个 Prefab ----------------
    private void ReplaceSelectedPrefabFont()
    {
        if (Selection.activeObject == null)
        {
            Debug.LogWarning("请先选中一个 Prefab！");
            return;
        }

        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (!path.EndsWith(".prefab"))
        {
            Debug.LogWarning("请选择一个 Prefab！");
            return;
        }

        GameObject prefabInstance = PrefabUtility.LoadPrefabContents(path);

        if (HasMissingScript(prefabInstance))
        {
            Debug.LogWarning($"Prefab {path} 存在丢失脚本，已跳过！");
            PrefabUtility.UnloadPrefabContents(prefabInstance);
            return;
        }

        int tmpCount = ReplaceFontInGameObject(prefabInstance);

        PrefabUtility.SaveAsPrefabAsset(prefabInstance, path);
        PrefabUtility.UnloadPrefabContents(prefabInstance);
        AssetDatabase.Refresh();

        Debug.Log($"替换完成：TMP/Text 组件共替换 {tmpCount} 个");
    }

    // ---------------- 批量替换目录 ----------------
    private void ReplacePrefabsInDirectories(string[] directories)
    {
        List<string> allPrefabGUIDs = new List<string>();
        foreach (var dir in directories)
        {
            if (string.IsNullOrEmpty(dir)) continue;
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { dir.Trim() });
            allPrefabGUIDs.AddRange(guids);
        }

        int totalCount = 0;
        int skippedCount = 0;

        foreach (var guid in allPrefabGUIDs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefabInstance = PrefabUtility.LoadPrefabContents(path);

            if (HasMissingScript(prefabInstance))
            {
                Debug.LogWarning($"Prefab {path} 存在丢失脚本，已跳过！");
                PrefabUtility.UnloadPrefabContents(prefabInstance);
                skippedCount++;
                continue;
            }

            int count = ReplaceFontInGameObject(prefabInstance);
            if (count > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabInstance, path);
            }

            PrefabUtility.UnloadPrefabContents(prefabInstance);
            totalCount += count;
        }

        AssetDatabase.Refresh();
        Debug.Log($"目录替换完成：共处理 {allPrefabGUIDs.Count} 个 Prefab，替换组件 {totalCount} 个，跳过 {skippedCount} 个");
    }

    // ---------------- 替换字体核心 ----------------
    private int ReplaceFontInGameObject(GameObject go)
    {
        int count = 0;

        // TMP_Text
        TMP_Text[] tmpTexts = go.GetComponentsInChildren<TMP_Text>(true);
        foreach (var t in tmpTexts)
        {
            if (oldTMPFont != null && newTMPFont != null && t.font == oldTMPFont)
            {
                t.font = newTMPFont;
                if (newTMPMaterial != null)
                    t.fontSharedMaterial = newTMPMaterial;
                count++;
            }
        }

        // TMP_InputField
        TMP_InputField[] inputFields = go.GetComponentsInChildren<TMP_InputField>(true);
        foreach (var f in inputFields)
        {
            if (f.textComponent != null && oldTMPFont != null && newTMPFont != null && f.textComponent.font == oldTMPFont)
            {
                f.textComponent.font = newTMPFont;
                if (newTMPMaterial != null)
                    f.textComponent.fontSharedMaterial = newTMPMaterial;
                count++;
            }
        }

        // UI.Text
        Text[] uiTexts = go.GetComponentsInChildren<Text>(true);
        foreach (var t in uiTexts)
        {
            if (oldUIFont != null && newUIFont != null && t.font == oldUIFont)
            {
                t.font = newUIFont;
                count++;
            }
        }

        return count;
    }

    // ---------------- 检查丢失脚本 ----------------
    private bool HasMissingScript(GameObject go)
    {
        MonoBehaviour[] components = go.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var c in components)
        {
            if (c == null)
                return true;
        }
        return false;
    }
}
