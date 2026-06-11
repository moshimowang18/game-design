using System;
using System.Collections.Generic;
using UnityEngine;

public static class GameplayResourceStore
{
    private const string ResourceRoot = "GameplayData";
    private static readonly Dictionary<string, UnityEngine.Object> AssetCacheByPath = new();

    /// <summary>
    /// 处理当前属性相关逻辑。
    /// </summary>
    private static readonly Dictionary<Type, object> CacheByType = new();

    /// <summary>
    /// 处理当前属性相关逻辑。
    /// </summary>
    public static IReadOnlyList<T> LoadAll<T>(string folderName) where T : ScriptableObject
    {
        if (CacheByType.TryGetValue(typeof(T), out var cached))
        {
            return (IReadOnlyList<T>)cached;
        }

        var resourcePath = string.IsNullOrWhiteSpace(folderName)
            ? ResourceRoot
            : $"{ResourceRoot}/{folderName}";

        var loaded = Resources.LoadAll<T>(resourcePath);

        Array.Sort(loaded, (left, right) => string.CompareOrdinal(left != null ? left.name : string.Empty, right != null ? right.name : string.Empty));
        CacheByType[typeof(T)] = loaded;
        return loaded;
    }

    /// <summary>
    /// 处理当前属性相关逻辑。
    /// </summary>
    public static T Find<T>(string folderName, Predicate<T> predicate) where T : ScriptableObject
    {
        if (predicate == null)
        {
            return null;
        }

        var all = LoadAll<T>(folderName);
        for (var i = 0; i < all.Count; i++)
        {
            var item = all[i];
            if (item != null && predicate(item))
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>
    /// 清理已缓存的玩法资源引用。
    /// </summary>
    public static void ClearCache()
    {
        CacheByType.Clear();
        AssetCacheByPath.Clear();
    }

    /// <summary>
    /// 按完整资源路径读取运行时资源，统一使用 Resources。
    /// </summary>
    /// <param name="assetPath">Unity 工程内资源完整路径。</param>
    /// <typeparam name="T">资源类型。</typeparam>
    /// <returns>读取到的资源；失败时返回 null。</returns>
    public static T LoadAsset<T>(string assetPath) where T : UnityEngine.Object
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return null;
        }

        var cacheKey = $"{typeof(T).FullName}:{assetPath}";
        if (AssetCacheByPath.TryGetValue(cacheKey, out var cachedAsset))
        {
            return cachedAsset as T;
        }

        var resourceAsset = LoadFromResources<T>(assetPath);
        if (resourceAsset != null)
        {
            AssetCacheByPath[cacheKey] = resourceAsset;
            return resourceAsset;
        }

        return null;
    }

    /// <summary>
    /// 从 Resources 目录尝试读取资源。
    /// </summary>
    /// <param name="assetPath">完整资源路径。</param>
    /// <typeparam name="T">资源类型。</typeparam>
    /// <returns>读取成功时返回资源，否则返回 null。</returns>
    private static T LoadFromResources<T>(string assetPath) where T : UnityEngine.Object
    {
        var resourcePath = ToResourcesPath(assetPath);
        return string.IsNullOrEmpty(resourcePath) ? null : Resources.Load<T>(resourcePath);
    }

    /// <summary>
    /// 把工程资源路径转换成 Resources.Load 可识别的相对路径。
    /// </summary>
    /// <param name="assetPath">完整资源路径。</param>
    /// <returns>Resources 相对路径；不在 Resources 下时返回 null。</returns>
    private static string ToResourcesPath(string assetPath)
    {
        const string marker = "/Resources/";
        var normalized = assetPath.Replace('\\', '/');
        var markerIndex = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return null;
        }

        var resourcePath = normalized[(markerIndex + marker.Length)..];
        var extensionIndex = resourcePath.LastIndexOf('.');
        return extensionIndex > 0 ? resourcePath[..extensionIndex] : resourcePath;
    }

}
