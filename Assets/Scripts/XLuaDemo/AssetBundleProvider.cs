using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace XLuaDemo
{
    public sealed class AssetBundleProvider
    {
        private readonly string localRoot;
        private readonly Action<string> log;
        private readonly Dictionary<string, AssetBundle> loadedBundles = new Dictionary<string, AssetBundle>();

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
            AssetBundle cached;
            if (loadedBundles.TryGetValue(relativePath, out cached))
            {
                return cached;
            }

            string path = Path.Combine(localRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                log("AssetBundle missing, using generated UI fallback: " + relativePath);
                return null;
            }

            AssetBundle bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null)
            {
                log("AssetBundle load failed: " + path);
                return null;
            }

            loadedBundles.Add(relativePath, bundle);
            return bundle;
        }
    }
}
