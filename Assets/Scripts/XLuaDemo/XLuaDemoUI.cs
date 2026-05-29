using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XLuaDemo
{
    public sealed class XLuaDemoUI
    {
        private readonly Font font;
        private readonly Text statusText;
        private readonly Text progressText;
        private readonly Text logText;
        private readonly RectTransform activityRoot;
        private readonly StringBuilder logs = new StringBuilder();

        public XLuaDemoUI(Transform owner)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem();

            GameObject canvasGo = new GameObject("XLua Demo Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(owner, false);

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform root = canvasGo.GetComponent<RectTransform>();
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            Image backdrop = CreateImage("Backdrop", root, ParseColor("#17202A"));
            Stretch(backdrop.rectTransform, 0, 0, 0, 0);

            RectTransform header = CreatePanel("Header", root, ParseColor("#243447"));
            Anchor(header, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -88), new Vector2(0, 0));

            statusText = CreateText("Status", header, "Starting...", 26, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            Anchor(statusText.rectTransform, new Vector2(0, 0), new Vector2(0.72f, 1), new Vector2(28, 0), new Vector2(-12, 0));

            progressText = CreateText("Progress", header, "0%", 22, FontStyle.Normal, TextAnchor.MiddleRight, ParseColor("#9FDBFF"));
            Anchor(progressText.rectTransform, new Vector2(0.72f, 0), new Vector2(1, 1), new Vector2(0, 0), new Vector2(-28, 0));

            activityRoot = CreatePanel("ActivityRoot", root, ParseColor("#F8F4EA"));
            Anchor(activityRoot, new Vector2(0, 0.28f), new Vector2(0.64f, 0.88f), new Vector2(28, 24), new Vector2(-14, -18));

            RectTransform logPanel = CreatePanel("LogPanel", root, ParseColor("#101820"));
            Anchor(logPanel, new Vector2(0.64f, 0), new Vector2(1, 0.88f), new Vector2(14, 24), new Vector2(-28, -18));

            Text logTitle = CreateText("LogTitle", logPanel, "Update / Hotfix Log", 20, FontStyle.Bold, TextAnchor.MiddleLeft, ParseColor("#D9F0FF"));
            Anchor(logTitle.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(18, -48), new Vector2(-18, 0));

            logText = CreateText("Logs", logPanel, "", 16, FontStyle.Normal, TextAnchor.UpperLeft, ParseColor("#E9EEF2"));
            logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            logText.verticalOverflow = VerticalWrapMode.Truncate;
            Anchor(logText.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(18, 18), new Vector2(-18, -54));
        }

        public void SetStatus(string text)
        {
            statusText.text = text;
        }

        public void SetDownloadProgress(float progress)
        {
            progressText.text = string.Format("Download {0:P0}", Mathf.Clamp01(progress));
        }

        public void Log(string message)
        {
            Debug.Log("[XLuaDemo] " + message);
            logs.AppendLine(DateTime.Now.ToString("HH:mm:ss") + "  " + message);
            logText.text = TrimLog(logs.ToString(), 2200);
        }

        public IEnumerator RenderActivity(ActivityConfig config, AssetBundleProvider provider)
        {
            ClearActivityRoot();

            GameObject prefab = provider.LoadPrefab(config.prefabBundle, config.prefabName);
            if (prefab != null)
            {
                GameObject instance = UnityEngine.Object.Instantiate(prefab, activityRoot);
                RectTransform rect = instance.GetComponent<RectTransform>();
                if (rect != null)
                {
                    Stretch(rect, 0, 0, 0, 0);
                }

                Log("Activity prefab loaded from AssetBundle: " + config.prefabBundle + "/" + config.prefabName);
                yield break;
            }

            RenderConfigDrivenFallback(config, provider);
            yield return null;
        }

        public void RenderFallbackActivity()
        {
            ActivityConfig config = new ActivityConfig
            {
                title = "Spring Festival Login",
                subtitle = "Lua patched battle logic + config-driven UI",
                backgroundColor = "#FFF3D6",
                accentColor = "#B42318",
                heroName = "New Hero Preview",
                heroDescription = "Server JSON can switch this panel without shipping a new client.",
                ctaText = "Claim",
                rewards = new[]
                {
                    new RewardConfig { name = "Gold", amount = 1888 },
                    new RewardConfig { name = "Gem", amount = 30 },
                    new RewardConfig { name = "Ticket", amount = 5 }
                }
            };

            RenderConfigDrivenFallback(config, null);
        }

        private void RenderConfigDrivenFallback(ActivityConfig config, AssetBundleProvider provider)
        {
            ClearActivityRoot();
            Color background = ParseColor(string.IsNullOrEmpty(config.backgroundColor) ? "#FFF7E6" : config.backgroundColor);
            Color accent = ParseColor(string.IsNullOrEmpty(config.accentColor) ? "#B42318" : config.accentColor);
            activityRoot.GetComponent<Image>().color = background;

            Image banner = CreateImage("Banner", activityRoot, accent);
            Anchor(banner.rectTransform, new Vector2(0, 0.68f), new Vector2(1, 1), Vector2.zero, Vector2.zero);

            Texture2D backgroundTexture = provider == null ? null : provider.LoadTexture(config.backgroundBundle, config.backgroundAsset);
            if (backgroundTexture != null)
            {
                banner.sprite = Sprite.Create(backgroundTexture, new Rect(0, 0, backgroundTexture.width, backgroundTexture.height), new Vector2(0.5f, 0.5f));
                banner.type = Image.Type.Sliced;
                banner.color = Color.white;
            }

            Text title = CreateText("Title", activityRoot, config.title, 34, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            Anchor(title.rectTransform, new Vector2(0, 0.78f), new Vector2(1, 1), new Vector2(32, 0), new Vector2(-28, -12));

            Text subtitle = CreateText("Subtitle", activityRoot, config.subtitle, 20, FontStyle.Normal, TextAnchor.UpperLeft, ParseColor("#FFF8E7"));
            subtitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            Anchor(subtitle.rectTransform, new Vector2(0, 0.68f), new Vector2(1, 0.82f), new Vector2(32, 0), new Vector2(-28, 0));

            Text hero = CreateText("Hero", activityRoot, config.heroName, 28, FontStyle.Bold, TextAnchor.MiddleLeft, ParseColor("#1D2939"));
            Anchor(hero.rectTransform, new Vector2(0, 0.48f), new Vector2(1, 0.66f), new Vector2(32, 0), new Vector2(-32, 0));

            Text description = CreateText("Description", activityRoot, config.heroDescription, 18, FontStyle.Normal, TextAnchor.UpperLeft, ParseColor("#344054"));
            description.horizontalOverflow = HorizontalWrapMode.Wrap;
            Anchor(description.rectTransform, new Vector2(0, 0.32f), new Vector2(1, 0.52f), new Vector2(32, 0), new Vector2(-32, 0));

            RectTransform rewards = CreatePanel("Rewards", activityRoot, new Color(1f, 1f, 1f, 0.55f));
            Anchor(rewards, new Vector2(0, 0.13f), new Vector2(1, 0.32f), new Vector2(28, 0), new Vector2(-28, 0));

            RewardConfig[] rewardItems = config.rewards ?? Array.Empty<RewardConfig>();
            for (int i = 0; i < rewardItems.Length; i++)
            {
                Text reward = CreateText("Reward" + i, rewards, rewardItems[i].name + " x" + rewardItems[i].amount, 17, FontStyle.Bold, TextAnchor.MiddleCenter, accent);
                float min = i / (float)rewardItems.Length;
                float max = (i + 1) / (float)rewardItems.Length;
                Anchor(reward.rectTransform, new Vector2(min, 0), new Vector2(max, 1), new Vector2(4, 0), new Vector2(-4, 0));
            }

            Button cta = CreateButton("CTA", activityRoot, string.IsNullOrEmpty(config.ctaText) ? "Go" : config.ctaText, accent);
            Anchor(cta.GetComponent<RectTransform>(), new Vector2(0.35f, 0.03f), new Vector2(0.65f, 0.12f), Vector2.zero, Vector2.zero);
            cta.onClick.AddListener(() => Log("Activity CTA clicked: " + config.ctaText));

            Log("Activity rendered from server JSON config.");
        }

        private void ClearActivityRoot()
        {
            for (int i = activityRoot.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(activityRoot.GetChild(i).gameObject);
            }
        }

        private RectTransform CreatePanel(string name, Transform parent, Color color)
        {
            Image image = CreateImage(name, parent, color);
            return image.rectTransform;
        }

        private Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private Text CreateText(string name, Transform parent, string text, int size, FontStyle style, TextAnchor anchor, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text uiText = go.GetComponent<Text>();
            uiText.font = font;
            uiText.text = text ?? string.Empty;
            uiText.fontSize = size;
            uiText.fontStyle = style;
            uiText.alignment = anchor;
            uiText.color = color;
            return uiText;
        }

        private Button CreateButton(string name, Transform parent, string label, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;

            Button button = go.GetComponent<Button>();
            Text text = CreateText("Label", go.transform, label, 20, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            Stretch(text.rectTransform, 0, 0, 0, 0);
            return button;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static Color ParseColor(string html)
        {
            Color color;
            return ColorUtility.TryParseHtmlString(html, out color) ? color : Color.white;
        }

        private static string TrimLog(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            {
                return text;
            }

            return text.Substring(text.Length - maxLength);
        }
    }
}
