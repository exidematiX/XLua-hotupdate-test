using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Agent.EditorTools
{
    public sealed class AgentSidebarWindow : EditorWindow
    {
        const string SettingsKey = "Agent.Sidebar.Settings";
        const string RequestKey = "Agent.Sidebar.Request";
        const string DefaultOutputFolder = "Assets/agent/Generated";

        readonly AgentLlmClient client = new AgentLlmClient();

        AgentLlmSettings settings = new AgentLlmSettings();
        AgentGeneratedResponse response;
        CancellationTokenSource cancellation;

        string requestText;
        string rawResponse;
        string status;
        string error;
        Vector2 leftScroll;
        Vector2 rightScroll;
        int selectedFileIndex;
        bool showAdvanced;
        bool isGenerating;

        [MenuItem("Window/Agent/LLM Script Sidebar")]
        public static void Open()
        {
            AgentSidebarWindow window = GetWindow<AgentSidebarWindow>("Agent");
            window.minSize = new Vector2(760, 480);
            window.Show();
        }

        void OnEnable()
        {
            LoadState();
        }

        void OnDisable()
        {
            SaveState();
            cancellation?.Cancel();
            cancellation?.Dispose();
            cancellation = null;
        }

        void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            DrawRequestPane();
            DrawPreviewPane();
            EditorGUILayout.EndHorizontal();
        }

        void DrawRequestPane()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(Mathf.Clamp(position.width * 0.38f, 280f, 420f)));
            leftScroll = EditorGUILayout.BeginScrollView(leftScroll);

            GUILayout.Label("Natural Language", EditorStyles.boldLabel);
            requestText = EditorGUILayout.TextArea(requestText, GUILayout.MinHeight(150));

            EditorGUILayout.Space(8);
            DrawSettings();

            EditorGUILayout.Space(8);
            using (new EditorGUI.DisabledScope(isGenerating || string.IsNullOrWhiteSpace(requestText)))
            {
                if (GUILayout.Button("Generate", GUILayout.Height(32)))
                    _ = GenerateAsync();
            }

            using (new EditorGUI.DisabledScope(!isGenerating))
            {
                if (GUILayout.Button("Cancel"))
                    CancelGeneration();
            }

            EditorGUILayout.Space(8);
            if (!string.IsNullOrEmpty(status))
                EditorGUILayout.HelpBox(status, MessageType.Info);

            if (!string.IsNullOrEmpty(error))
                EditorGUILayout.HelpBox(error, MessageType.Error);

            EditorGUILayout.Space(8);
            GUILayout.Label("Examples", EditorStyles.boldLabel);
            DrawExampleButton("创建一个 EditorWindow，扫描场景里缺失脚本的对象并列出来");
            DrawExampleButton("生成一个透明渐变的 Unlit Shader，支持颜色和强度参数");
            DrawExampleButton("写一个菜单项，把选中的 Texture 批量设置为 Sprite");

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawSettings()
        {
            GUILayout.Label("LLM Settings", EditorStyles.boldLabel);
            settings.baseUrl = EditorGUILayout.TextField("Base URL", settings.baseUrl);
            settings.model = EditorGUILayout.TextField("Model", settings.model);
            settings.apiKey = EditorGUILayout.PasswordField("API Key", settings.apiKey);

            showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Advanced");
            if (showAdvanced)
            {
                settings.temperature = EditorGUILayout.Slider("Temperature", settings.temperature, 0f, 2f);
                settings.maxTokens = EditorGUILayout.IntSlider("Max Tokens", settings.maxTokens, 512, 16000);
                EditorGUILayout.LabelField("Default Folder", DefaultOutputFolder);
            }
        }

        void DrawExampleButton(string text)
        {
            if (GUILayout.Button(text, EditorStyles.miniButton))
                requestText = text;
        }

        void DrawPreviewPane()
        {
            EditorGUILayout.BeginVertical();
            rightScroll = EditorGUILayout.BeginScrollView(rightScroll);

            GUILayout.Label("Generated Result", EditorStyles.boldLabel);
            if (response == null || response.files == null || response.files.Length == 0)
            {
                EditorGUILayout.HelpBox("Generate a result to preview files here.", MessageType.None);
                DrawRawResponse();
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            if (!string.IsNullOrWhiteSpace(response.summary))
                EditorGUILayout.HelpBox(response.summary, MessageType.Info);

            string[] names = new string[response.files.Length];
            for (int i = 0; i < response.files.Length; i++)
                names[i] = response.files[i].DisplayName();

            selectedFileIndex = Mathf.Clamp(selectedFileIndex, 0, response.files.Length - 1);
            selectedFileIndex = GUILayout.Toolbar(selectedFileIndex, names);

            AgentGeneratedAsset selected = response.files[selectedFileIndex];
            EditorGUILayout.Space(6);
            DrawSelectedFile(selected);
            DrawRawResponse();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawSelectedFile(AgentGeneratedAsset selected)
        {
            if (selected == null)
                return;

            selected.path = EditorGUILayout.TextField("Path", selected.path);
            selected.kind = EditorGUILayout.TextField("Kind", selected.kind);

            if (!string.IsNullOrWhiteSpace(selected.notes))
                EditorGUILayout.HelpBox(selected.notes, MessageType.None);

            string reason;
            bool valid = AgentAssetApplier.IsValidPath(selected.path, out reason);
            if (!valid)
                EditorGUILayout.HelpBox(reason, MessageType.Warning);

            GUILayout.Label("Preview", EditorStyles.boldLabel);
            selected.content = EditorGUILayout.TextArea(selected.content, GUILayout.MinHeight(280));

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!valid || selected.IsEmpty))
            {
                if (GUILayout.Button("Apply Selected", GUILayout.Height(28)))
                    ApplySelected(selected);
            }

            using (new EditorGUI.DisabledScope(response == null || response.files == null || response.files.Length == 0))
            {
                if (GUILayout.Button("Apply All", GUILayout.Height(28)))
                    ApplyAll();
            }

            EditorGUILayout.EndHorizontal();
        }

        void DrawRawResponse()
        {
            if (string.IsNullOrWhiteSpace(rawResponse))
                return;

            EditorGUILayout.Space(10);
            GUILayout.Label("Raw Response", EditorStyles.boldLabel);
            rawResponse = EditorGUILayout.TextArea(rawResponse, GUILayout.MinHeight(100));
        }

        async Task GenerateAsync()
        {
            SaveState();
            CancelGeneration();
            cancellation = new CancellationTokenSource();
            isGenerating = true;
            status = "Generating...";
            error = string.Empty;
            rawResponse = string.Empty;
            response = null;
            Repaint();

            try
            {
                string prompt = AgentGenerationPrompt.Build(requestText, DefaultOutputFolder);
                rawResponse = await client.GenerateAsync(settings, prompt, cancellation.Token);
                response = AgentResponseParser.Parse(rawResponse);
                selectedFileIndex = 0;
                status = "Generated " + response.files.Length + " file(s). Review before applying.";
            }
            catch (OperationCanceledException)
            {
                status = "Generation canceled.";
            }
            catch (Exception ex)
            {
                error = ex.Message;
                status = string.Empty;
            }
            finally
            {
                isGenerating = false;
                Repaint();
            }
        }

        void ApplySelected(AgentGeneratedAsset asset)
        {
            try
            {
                AgentAssetApplier.Apply(asset);
                status = "Applied: " + AgentAssetApplier.NormalizeProjectPath(asset.path);
                error = string.Empty;
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
        }

        void ApplyAll()
        {
            if (response == null || response.files == null)
                return;

            int applied = 0;
            try
            {
                foreach (AgentGeneratedAsset file in response.files)
                {
                    if (file == null || file.IsEmpty)
                        continue;

                    AgentAssetApplier.Apply(file);
                    applied++;
                }

                status = "Applied " + applied + " file(s).";
                error = string.Empty;
            }
            catch (Exception ex)
            {
                error = "Applied " + applied + " file(s), then failed: " + ex.Message;
            }
        }

        void CancelGeneration()
        {
            if (cancellation == null)
                return;

            cancellation.Cancel();
            cancellation.Dispose();
            cancellation = null;
        }

        void LoadState()
        {
            string settingsJson = EditorPrefs.GetString(SettingsKey, string.Empty);
            if (!string.IsNullOrEmpty(settingsJson))
            {
                try
                {
                    AgentLlmSettings loaded = JsonUtility.FromJson<AgentLlmSettings>(settingsJson);
                    if (loaded != null)
                        settings = loaded;
                }
                catch
                {
                    settings = new AgentLlmSettings();
                }
            }

            requestText = EditorPrefs.GetString(RequestKey, string.Empty);
        }

        void SaveState()
        {
            EditorPrefs.SetString(SettingsKey, JsonUtility.ToJson(settings));
            EditorPrefs.SetString(RequestKey, requestText ?? string.Empty);
        }
    }
}
