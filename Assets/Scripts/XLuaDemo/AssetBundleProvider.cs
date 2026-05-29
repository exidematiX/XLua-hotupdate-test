using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using XLuaDemo.ResourceUpdate;

namespace XLuaDemo
{
    public sealed class AssetBundleProvider
    {
        private readonly string localRoot;
        private readonly Action<string> log;
        private readonly Dictionary<string, AssetBundle> loadedBundles = new Dictionary<string, AssetBundle>();
        private readonly Dictionary<string, string[]> bundleDependencies = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> loadingBundles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool manifestLoaded;

        public AssetBundleProvider(string localRoot, Action<string> log)
        {
            this.localRoot = localRoot;
            this.log = log;
        }

        public GameObject LoadPrefab(string bundlePath, string prefabName)
        {
            if (string.IsNullOrEmpty(bundlePath) || string.IsNullOrEmpty(prefabName))
            {
                return null;
            }

            AssetBundle bundle = LoadBundle(bundlePath);
            if (bundle == null)
            {
                return null;
            }

            return bundle.LoadAsset<GameObject>(prefabName);
        }

        public Texture2D LoadTexture(string bundlePath, string textureName)
        {
            if (string.IsNullOrEmpty(bundlePath) || string.IsNullOrEmpty(textureName))
            {
                return null;
            }

            AssetBundle bundle = LoadBundle(bundlePath);
            if (bundle == null)
            {
                return null;
            }

            return bundle.LoadAsset<Texture2D>(textureName);
        }

        public void UnloadUnusedBundles()
        {
            foreach (AssetBundle bundle in loadedBundles.Values)
            {
                if (bundle != null)
                {
                    bundle.Unload(false);
                }
            }

            loadedBundles.Clear();
        }

        private AssetBundle LoadBundle(string relativePath)
        {
            LoadManifestIfNeeded();
            relativePath = NormalizeBundlePath(relativePath);
            AssetBundle cached;
            if (loadedBundles.TryGetValue(relativePath, out cached))
            {
                return cached;
            }

            if (!loadingBundles.Add(relativePath))
            {
                log("Circular AssetBundle dependency skipped: " + relativePath);
                return null;
            }

            string[] dependencies;
            if (bundleDependencies.TryGetValue(relativePath, out dependencies))
            {
                for (int i = 0; i < dependencies.Length; i++)
                {
                    LoadBundle(dependencies[i]);
                }
            }

            string path = Path.Combine(localRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                log("AssetBundle missing, using generated UI fallback: " + relativePath);
                loadingBundles.Remove(relativePath);
                return null;
            }

            AssetBundle bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null)
            {
                log("AssetBundle load failed: " + path);
                loadingBundles.Remove(relativePath);
                return null;
            }

            loadedBundles.Add(relativePath, bundle);
            loadingBundles.Remove(relativePath);
            return bundle;
        }

        private void LoadManifestIfNeeded()
        {
            if (manifestLoaded)
            {
                return;
            }

            manifestLoaded = true;
            string manifestPath = Path.Combine(localRoot, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                return;
            }

            ResourceManifest manifest = JsonUtility.FromJson<ResourceManifest>(File.ReadAllText(manifestPath));
            if (manifest == null || manifest.bundles == null)
            {
                return;
            }

            for (int i = 0; i < manifest.bundles.Length; i++)
            {
                AssetBundleRecord record = manifest.bundles[i];
                if (record == null || string.IsNullOrEmpty(record.name))
                {
                    continue;
                }

                bundleDependencies[NormalizeBundlePath(record.name)] = record.dependencies ?? Array.Empty<string>();
            }
        }

        private static string NormalizeBundlePath(string relativePath)
        {
            return (relativePath ?? string.Empty).Replace("\\", "/").TrimStart('/');
        }
    }
}
