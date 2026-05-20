using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using XLuaDemo;
using XLuaDemo.ResourceUpdate;

public static class XLuaDemoAssetBundleBuilder
{
    private const string GeneratedRoot = "Assets/XLuaDemoGenerated";
    private const string SourceRoot = GeneratedRoot + "/ABSource";
    private const string RemoteRoot = "Assets/StreamingAssets/RemoteServer";
    private const string RemoteBaseUrlKey = "XLuaDemo.RemoteBaseUrl";

    [MenuItem("XLua Demo/Build Remote Resource Server")]
    public static void BuildRemoteResourceServer()
    {
        Directory.CreateDirectory(SourceRoot);
        Directory.CreateDirectory(RemoteRoot);
        Directory.CreateDirectory(Path.Combine(RemoteRoot, "activity"));
        Directory.CreateDirectory(Path.Combine(RemoteRoot, "lua"));
        Directory.CreateDirectory(Path.Combine(RemoteRoot, "bundles"));

        Texture2D banner = CreateBannerTexture();
        string bannerPath = SourceRoot + "/festival_banner.png";
        File.WriteAllBytes(bannerPath, banner.EncodeToPNG());
        Object.DestroyImmediate(banner);

        AssetDatabase.ImportAsset(bannerPath);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(bannerPath);
        importer.textureType = TextureImporterType.Default;
        importer.assetBundleName = "bundles/festivaltextures";
        importer.SaveAndReimport();

        string prefabPath = SourceRoot + "/FestivalActivityPanel.prefab";
        CreateActivityPrefab(prefabPath);
        AssetImporter.GetAtPath(prefabPath).assetBundleName = "bundles/festivalui";

        BuildPipeline.BuildAssetBundles(
            Path.Combine(Application.dataPath, "StreamingAssets/RemoteServer"),
            BuildAssetBundleOptions.ChunkBasedCompression,
            EditorUserBuildSettings.activeBuildTarget);

        WriteActivityConfig();
        CopyLuaHotfix();
        WriteManifest();

        AssetDatabase.Refresh();
        Debug.Log("XLua demo remote resource server generated at: " + Path.GetFullPath(RemoteRoot));
    }

    [MenuItem("XLua Demo/Use Local StreamingAssets Server")]
    public static void UseLocalStreamingAssetsServer()
    {
        PlayerPrefs.DeleteKey(RemoteBaseUrlKey);
        PlayerPrefs.Save();
        Debug.Log("XLua demo remote URL reset. The demo will use Assets/StreamingAssets/RemoteServer through file://.");
    }

    [MenuItem("XLua Demo/Set Serv-U HTTP URL")]
    public static void SetServUHttpUrl()
    {
        ServUUrlWindow.Open(PlayerPrefs.GetString(RemoteBaseUrlKey, "http://127.0.0.1:8080"));
    }

    private static void CreateActivityPrefab(string prefabPath)
    {
        Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        GameObject root = new GameObject("FestivalActivityPanel", typeof(RectTransform), typeof(Image));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(820, 420);
        root.GetComponent<Image>().color = ParseColor("#FFF2CC");

        Image stripe = CreateImage("Stripe", root.transform, ParseColor("#B42318"));
        Anchor(stripe.rectTransform, new Vector2(0, 0.66f), new Vector2(1, 1), Vector2.zero, Vector2.zero);

        Text title = CreateText("Title", root.transform, "Lantern Festival", font, 38, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        Anchor(title.rectTransform, new Vector2(0, 0.76f), new Vector2(1, 1), new Vector2(34, 0), new Vector2(-34, 0));

        Text subtitle = CreateText("Subtitle", root.transform, "Prefab + texture loaded from AssetBundle, selected by server JSON.", font, 20, FontStyle.Normal, TextAnchor.UpperLeft, ParseColor("#FFF8E7"));
        Anchor(subtitle.rectTransform, new Vector2(0, 0.66f), new Vector2(1, 0.8f), new Vector2(34, 0), new Vector2(-34, 0));

        Text hero = CreateText("Hero", root.transform, "New Hero: Yun Sheng", font, 30, FontStyle.Bold, TextAnchor.MiddleLeft, ParseColor("#1D2939"));
        Anchor(hero.rectTransform, new Vector2(0, 0.42f), new Vector2(1, 0.62f), new Vector2(34, 0), new Vector2(-34, 0));

        Text body = CreateText("Body", root.transform, "The server can swap this prefab for holiday campaigns, hero previews, and regional activities.", font, 19, FontStyle.Normal, TextAnchor.UpperLeft, ParseColor("#344054"));
        body.horizontalOverflow = HorizontalWrapMode.Wrap;
        Anchor(body.rectTransform, new Vector2(0, 0.18f), new Vector2(1, 0.44f), new Vector2(34, 0), new Vector2(-34, 0));

        Button button = CreateButton("ClaimButton", root.transform, "Claim Gift", font, ParseColor("#B42318"));
        Anchor(button.GetComponent<RectTransform>(), new Vector2(0.36f, 0.04f), new Vector2(0.64f, 0.15f), Vector2.zero, Vector2.zero);

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
    }

    private static void WriteActivityConfig()
    {
        ActivityConfig config = new ActivityConfig
        {
            activityId = "festival_001",
            title = "Lantern Festival Login",
            subtitle = "Server JSON controls prefab, texture, copy, rewards, and colors.",
            heroName = "New Hero Preview: Yun Sheng",
            heroDescription = "A tiny data-driven page: change this JSON on the remote server and restart Play Mode to see a different activity.",
            ctaText = "Claim Gift",
            backgroundColor = "#FFF2CC",
            accentColor = "#B42318",
            prefabBundle = "bundles/festivalui",
            prefabName = "FestivalActivityPanel",
            backgroundBundle = "bundles/festivaltextures",
            backgroundAsset = "festival_banner",
            rewards = new[]
            {
                new RewardConfig { name = "Gold", amount = 1888 },
                new RewardConfig { name = "Gem", amount = 30 },
                new RewardConfig { name = "Hero Shard", amount = 10 }
            }
        };

        File.WriteAllText(Path.Combine(RemoteRoot, "activity/festival_activity.json"), JsonUtility.ToJson(config, true), Encoding.UTF8);
    }

    private static void CopyLuaHotfix()
    {
        string source = "Assets/StreamingAssets/Lua/HotfixBattle.lua";
        string target = Path.Combine(RemoteRoot, "lua/HotfixBattle.lua");
        if (File.Exists(source))
        {
            File.Copy(source, target, true);
        }
    }

    private static void WriteManifest()
    {
        List<ResourceFile> files = new List<ResourceFile>();
        AddManifestFile(files, "activity/festival_activity.json");
        AddManifestFile(files, "lua/HotfixBattle.lua");
        AddManifestFile(files, "bundles/festivalui");
        AddManifestFile(files, "bundles/festivalui.manifest");
        AddManifestFile(files, "bundles/festivaltextures");
        AddManifestFile(files, "bundles/festivaltextures.manifest");

        ResourceManifest manifest = new ResourceManifest
        {
            version = System.DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
            files = files.ToArray()
        };

        File.WriteAllText(Path.Combine(RemoteRoot, "manifest.json"), JsonUtility.ToJson(manifest, true), Encoding.UTF8);
    }

    private static void AddManifestFile(List<ResourceFile> files, string relativePath)
    {
        string fullPath = Path.Combine(RemoteRoot, relativePath);
        if (!File.Exists(fullPath))
        {
            return;
        }

        FileInfo info = new FileInfo(fullPath);
        files.Add(new ResourceFile
        {
            path = relativePath,
            md5 = CalculateMd5(fullPath),
            size = info.Length,
            url = relativePath
        });
    }

    private static Texture2D CreateBannerTexture()
    {
        Texture2D texture = new Texture2D(512, 256, TextureFormat.RGBA32, false);
        Color left = ParseColor("#B42318");
        Color right = ParseColor("#F79009");
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                float t = x / (float)(texture.width - 1);
                texture.SetPixel(x, y, Color.Lerp(left, right, t));
            }
        }

        texture.Apply();
        return texture;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text CreateText(string name, Transform parent, string value, Font font, int size, FontStyle style, TextAnchor anchor, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = anchor;
        text.color = color;
        return text;
    }

    private static Button CreateButton(string name, Transform parent, string label, Font font, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;

        Text text = CreateText("Label", go.transform, label, font, 20, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        Anchor(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return go.GetComponent<Button>();
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

    private static string CalculateMd5(string path)
    {
        using (MD5 md5 = MD5.Create())
        using (FileStream stream = File.OpenRead(path))
        {
            byte[] hash = md5.ComputeHash(stream);
            StringBuilder builder = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
            {
                builder.Append(hash[i].ToString("x2"));
            }

            return builder.ToString();
        }
    }

    private sealed class ServUUrlWindow : EditorWindow
    {
        private string url;

        public static void Open(string currentUrl)
        {
            ServUUrlWindow window = GetWindow<ServUUrlWindow>(true, "Serv-U URL");
            window.url = currentUrl;
            window.minSize = new Vector2(430, 94);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Remote Resource Root", EditorStyles.boldLabel);
            url = EditorGUILayout.TextField(url);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save"))
            {
                PlayerPrefs.SetString(RemoteBaseUrlKey, url.TrimEnd('/', '\\'));
                PlayerPrefs.Save();
                Debug.Log("XLua demo remote URL set to: " + PlayerPrefs.GetString(RemoteBaseUrlKey));
                Close();
            }

            if (GUILayout.Button("Use Local"))
            {
                UseLocalStreamingAssetsServer();
                Close();
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
