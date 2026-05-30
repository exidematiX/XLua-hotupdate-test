using System;
using System.IO;
using UnityEditor;

namespace Agent.EditorTools
{
    internal static class AgentAssetApplier
    {
        static readonly string[] AllowedExtensions =
        {
            ".cs", ".shader", ".hlsl", ".cginc", ".compute", ".txt", ".json", ".asmdef", ".uxml", ".uss"
        };

        public static void Apply(AgentGeneratedAsset asset)
        {
            if (asset == null || asset.IsEmpty)
                throw new InvalidOperationException("Generated asset is empty.");

            string projectRelativePath = NormalizeProjectPath(asset.path);
            ValidatePath(projectRelativePath);

            string fullPath = Path.GetFullPath(projectRelativePath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(fullPath, asset.content);
            AssetDatabase.ImportAsset(projectRelativePath);
            AssetDatabase.Refresh();
        }

        public static string NormalizeProjectPath(string path)
        {
            string normalized = (path ?? string.Empty).Replace('\\', '/').Trim();
            while (normalized.StartsWith("./", StringComparison.Ordinal))
                normalized = normalized.Substring(2);

            return normalized;
        }

        public static bool IsValidPath(string path, out string reason)
        {
            try
            {
                ValidatePath(NormalizeProjectPath(path));
                reason = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                reason = ex.Message;
                return false;
            }
        }

        static void ValidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException("Path is empty.");

            if (!path.StartsWith("Assets/", StringComparison.Ordinal))
                throw new InvalidOperationException("Path must start with Assets/.");

            if (path.Contains(".."))
                throw new InvalidOperationException("Path cannot contain '..'.");

            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (Array.IndexOf(AllowedExtensions, extension) < 0)
                throw new InvalidOperationException("Extension is not allowed: " + extension);

            string projectRoot = Path.GetFullPath(".");
            string fullPath = Path.GetFullPath(path);
            if (!fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Path escapes the Unity project.");
        }
    }
}
