using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Agent.EditorTools
{
    internal static class AgentResponseParser
    {
        public static AgentGeneratedResponse Parse(string raw)
        {
            string json = StripCodeFence(raw);
            AgentGeneratedResponse response = JsonUtility.FromJson<AgentGeneratedResponse>(json);
            if (response == null)
                throw new InvalidOperationException("Unable to parse generated JSON.");

            if (response.files == null)
                response.files = Array.Empty<AgentGeneratedAsset>();

            return response;
        }

        public static string StripCodeFence(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            string trimmed = raw.Trim();
            Match match = Regex.Match(trimmed, "^```(?:json)?\\s*(.*?)\\s*```$", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : trimmed;
        }
    }
}
