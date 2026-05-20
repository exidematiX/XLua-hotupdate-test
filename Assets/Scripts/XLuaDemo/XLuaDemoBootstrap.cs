using System;
using System.Collections;
using System.IO;
using UnityEngine;
using XLuaDemo.ResourceUpdate;

namespace XLuaDemo
{
    public sealed class XLuaDemoBootstrap : MonoBehaviour
    {
        [Tooltip("Serv-U/HTTP root that contains manifest.json. Leave empty to use StreamingAssets/RemoteServer.")]
        [SerializeField] private string remoteBaseUrlOverride = "";

        private const string DemoObjectName = "XLua Demo Bootstrap";
        private XLuaDemoUI ui;
        private LuaHotfixService luaHotfixService;
        private HotUpdateManager hotUpdateManager;
        private CoreBattleService battleService;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindObjectOfType<XLuaDemoBootstrap>() != null)
            {
                return;
            }

            GameObject go = new GameObject(DemoObjectName);
            DontDestroyOnLoad(go);
            go.AddComponent<XLuaDemoBootstrap>();
        }

        private void Awake()
        {
            ui = new XLuaDemoUI(transform);
            battleService = new CoreBattleService();
            luaHotfixService = new LuaHotfixService();
            hotUpdateManager = new HotUpdateManager();
        }

        private IEnumerator Start()
        {
            ui.SetStatus("XLua hot update demo starting...");
            ui.Log("Remote root: " + ResolveRemoteBaseUrl());
            ui.Log("Local hot update root: " + GetLocalRoot());

            ReportDamage("Before Lua hotfix");

            yield return hotUpdateManager.UpdateFromRemote(
                ResolveRemoteBaseUrl(),
                GetLocalRoot(),
                ui.Log,
                ui.SetDownloadProgress);

            luaHotfixService.Install(Path.Combine(GetLocalRoot(), "lua"), ui.Log);
            ReportDamage("After Lua hotfix");

            yield return RenderActivity();
            ui.SetStatus("Demo ready");
        }

        private void Update()
        {
            if (luaHotfixService != null)
            {
                luaHotfixService.Tick();
            }
        }

        private void OnDestroy()
        {
            if (luaHotfixService != null)
            {
                luaHotfixService.Dispose();
            }
        }

        private IEnumerator RenderActivity()
        {
            string configPath = Path.Combine(GetLocalRoot(), "activity", "festival_activity.json");
            if (!File.Exists(configPath))
            {
                ui.Log("Activity config not found locally, using built-in fallback UI.");
                ui.RenderFallbackActivity();
                yield break;
            }

            string json = File.ReadAllText(configPath);
            ActivityConfig config = JsonUtility.FromJson<ActivityConfig>(json);
            if (config == null)
            {
                ui.Log("Activity config parse failed: " + configPath);
                ui.RenderFallbackActivity();
                yield break;
            }

            AssetBundleProvider provider = new AssetBundleProvider(GetLocalRoot(), ui.Log);
            yield return ui.RenderActivity(config, provider);
            provider.UnloadUnusedBundles();
        }

        private void ReportDamage(string title)
        {
            int damage = battleService.CalculateDamage(attack: 120, defense: 36, skillPower: 150);
            float criticalRate = battleService.CalculateCriticalRate(luck: 30);
            ui.Log(string.Format("{0}: damage={1}, critical={2:P0}", title, damage, criticalRate));
        }

        private string ResolveRemoteBaseUrl()
        {
            if (!string.IsNullOrWhiteSpace(remoteBaseUrlOverride))
            {
                return remoteBaseUrlOverride.TrimEnd('/', '\\');
            }

            string saved = PlayerPrefs.GetString("XLuaDemo.RemoteBaseUrl", "");
            if (!string.IsNullOrWhiteSpace(saved))
            {
                return saved.TrimEnd('/', '\\');
            }

            string localRemoteRoot = Path.Combine(Application.streamingAssetsPath, "RemoteServer");
            return new Uri(localRemoteRoot).AbsoluteUri.TrimEnd('/');
        }

        private static string GetLocalRoot()
        {
            return Path.Combine(Application.persistentDataPath, "XLuaHotUpdateDemo");
        }
    }
}
