using System;

namespace XLuaDemo.ResourceUpdate
{
    [Serializable]
    public sealed class ResourceManifest
    {
        public string version;
        public string buildTarget;
        public string generatedAtUtc;
        public AssetBundleRecord[] bundles;
        public ResourceFile[] files;
    }

    [Serializable]
    public sealed class ResourceFile
    {
        public string path;
        public string md5;
        public long size;
        public string url;
    }

    [Serializable]
    public sealed class AssetBundleRecord
    {
        public string name;
        public string md5;
        public string hash;
        public long size;
        public string[] assets;
        public string[] dependencies;
    }
}
