using System;

namespace XLuaDemo.ResourceUpdate
{
    [Serializable]
    public sealed class ResourceManifest
    {
        public string version;
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
}
