using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Agent.EditorTools
{
    internal sealed class AgentLlmClient
    {
        static readonly HttpClient Http = new HttpClient();

        public async Task<string> GenerateAsync(AgentLlmSettings settings, string prompt, CancellationToken cancellationToken)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            string apiKey = settings.ResolveApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("Missing API key. Set OPENAI_API_KEY or paste a key in the Agent window.");

            string baseUrl = string.IsNullOrWhiteSpace(settings.baseUrl) ? AgentLlmSettings.DefaultBaseUrl : settings.baseUrl.TrimEnd('/');
            string url = baseUrl + "/chat/completions";
            string body = BuildRequestBody(settings, prompt);

            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await Http.SendAsync(request, cancellationToken))
                {
                    string responseText = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException("LLM request failed: " + (int)response.StatusCode + " " + response.ReasonPhrase + "\n" + responseText);

                    return ExtractContent(responseText);
                }
            }
        }

        static string BuildRequestBody(AgentLlmSettings settings, string prompt)
        {
            ChatCompletionRequest request = new ChatCompletionRequest
            {
                model = string.IsNullOrWhiteSpace(settings.model) ? AgentLlmSettings.DefaultModel : settings.model,
                temperature = Mathf.Clamp(settings.temperature, 0f, 2f),
                max_tokens = Mathf.Max(512, settings.maxTokens),
                messages = new[]
                {
                    new ChatMessage
                    {
                        role = "system",
                        content = "You write concise, complete Unity assets and return strict JSON only."
                    },
                    new ChatMessage
                    {
                        role = "user",
                        content = prompt
                    }
                }
            };

            return JsonUtility.ToJson(request);
        }

        static string ExtractContent(string json)
        {
            ChatCompletionResponse response = JsonUtility.FromJson<ChatCompletionResponse>(json);
            if (response == null || response.choices == null || response.choices.Length == 0 || response.choices[0].message == null)
                throw new InvalidOperationException("LLM response did not contain choices[0].message.content.");

            return response.choices[0].message.content;
        }

        [Serializable]
        sealed class ChatCompletionRequest
        {
            public string model;
            public float temperature;
            public int max_tokens;
            public ChatMessage[] messages;
        }

        [Serializable]
        sealed class ChatMessage
        {
            public string role;
            public string content;
        }

        [Serializable]
#pragma warning disable 0649
        sealed class ChatCompletionResponse
        {
            public Choice[] choices;
        }

        [Serializable]
        sealed class Choice
        {
            public ChatMessage message;
        }
#pragma warning restore 0649
    }

    [Serializable]
    internal sealed class AgentLlmSettings
    {
        public const string DefaultBaseUrl = "https://api.openai.com/v1";
        public const string DefaultModel = "gpt-4.1-mini";

        public string baseUrl = DefaultBaseUrl;
        public string model = DefaultModel;
        public string apiKey;
        public float temperature = 0.2f;
        public int maxTokens = 4096;

        public string ResolveApiKey()
        {
            if (!string.IsNullOrWhiteSpace(apiKey))
                return apiKey.Trim();

            string env = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            return string.IsNullOrWhiteSpace(env) ? string.Empty : env.Trim();
        }
    }
}
