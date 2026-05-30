using System.Text;
using UnityEditor;
using UnityEngine;

namespace Agent.EditorTools
{
    internal static class AgentGenerationPrompt
    {
        public static string Build(string userRequest, string defaultFolder)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("You are a senior Unity editor tooling assistant.");
            builder.AppendLine("Generate Unity Editor C# scripts or ShaderLab/HLSL snippets for the user's request.");
            builder.AppendLine("Return JSON only. Do not wrap it in markdown.");
            builder.AppendLine("Schema:");
            builder.AppendLine("{\"summary\":\"short explanation\",\"files\":[{\"path\":\"Assets/agent/Generated/File.cs\",\"kind\":\"EditorScript|Shader|Script|Text\",\"content\":\"full file content\",\"notes\":\"optional\"}]}");
            builder.AppendLine("Rules:");
            builder.AppendLine("- All paths must be project-relative and start with Assets/.");
            builder.AppendLine("- Prefer Assets/agent/Generated/ when the user does not specify a path.");
            builder.AppendLine("- EditorWindow/MenuItem/AssetPostprocessor code must be under an Editor folder.");
            builder.AppendLine("- Generated C# must compile in Unity 2022.3 and avoid external packages.");
            builder.AppendLine("- Generated ShaderLab must be a complete .shader file unless the user explicitly asks for only a snippet.");
            builder.AppendLine("- Do not use destructive file APIs, shell commands, networking, credential collection, or editor automation that deletes project assets.");
            builder.AppendLine("- Include using directives and namespaces where useful.");
            builder.AppendLine();
            builder.AppendLine("Project context:");
            builder.AppendLine("- Unity version: " + Application.unityVersion);
            builder.AppendLine("- Default output folder: " + defaultFolder);
            builder.AppendLine("- Selected asset path: " + GetSelectedAssetPath());
            builder.AppendLine();
            builder.AppendLine("User request:");
            builder.AppendLine(userRequest);
            return builder.ToString();
        }

        static string GetSelectedAssetPath()
        {
            Object active = Selection.activeObject;
            if (active == null)
                return "(none)";

            string path = AssetDatabase.GetAssetPath(active);
            return string.IsNullOrEmpty(path) ? "(scene object)" : path;
        }
    }
}
