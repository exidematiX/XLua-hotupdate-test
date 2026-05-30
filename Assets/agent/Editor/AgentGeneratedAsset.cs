using System;
using UnityEngine;

namespace Agent.EditorTools
{
    [Serializable]
    public sealed class AgentGeneratedResponse
    {
        public string summary;
        public AgentGeneratedAsset[] files;
    }

    [Serializable]
    public sealed class AgentGeneratedAsset
    {
        public string path;
        public string kind;
        public string content;
        public string notes;

        public bool IsEmpty => string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(content);
    }

    internal static class AgentGeneratedAssetExtensions
    {
        public static string DisplayName(this AgentGeneratedAsset asset)
        {
            if (asset == null || string.IsNullOrEmpty(asset.path))
                return "Untitled";

            return string.IsNullOrEmpty(asset.kind) ? asset.path : asset.path + " (" + asset.kind + ")";
        }
    }
}
