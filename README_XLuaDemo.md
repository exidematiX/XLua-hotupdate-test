# XLua Hot Update Demo

This project now contains a small end-to-end hot update sample:

- C# core battle logic in `Assets/Scripts/XLuaDemo/CoreBattleService.cs`.
- Lua hotfix script in `Assets/StreamingAssets/Lua/HotfixBattle.lua`.
- Incremental resource update with manifest comparison, MD5/size validation, and retry logic in `Assets/Scripts/XLuaDemo/ResourceUpdate`.
- Data-driven activity UI from downloaded JSON plus optional AssetBundle prefab/texture loading.
- A Unity editor builder at `XLua Demo/Build Remote Resource Server` that generates sample AssetBundles and a fresh manifest under `Assets/StreamingAssets/RemoteServer`.

## Quick Run

1. Open `Assets/Scenes/SampleScene.unity`.
2. Enter Play Mode.
3. The demo bootstrap creates a canvas automatically, downloads `manifest.json` from `StreamingAssets/RemoteServer`, copies changed files into `Application.persistentDataPath/XLuaHotUpdateDemo`, then renders the activity page from JSON.

The project is safe to open before xLua is installed. Without xLua, the UI still demonstrates manifest diffing, retry flow, JSON-driven UI, and AssetBundle fallback behavior. After installing xLua, add `XLUA_PRESENT` to Player Settings > Scripting Define Symbols so `LuaHotfixService` compiles the real `LuaEnv` bridge.

## xLua Setup

1. Import Tencent xLua into the project.
2. Add `XLUA_PRESENT` to scripting define symbols.
3. Run xLua code generation/hotfix injection according to the xLua package menu.
4. Enter Play Mode again. The log should show different damage and critical-rate values before and after `HotfixBattle.lua` is installed.

The Lua patch uses:

```lua
xlua.hotfix(CS.XLuaDemo.CoreBattleService, 'CalculateDamage', function(self, attack, defense, skillPower)
    -- patched formula
end)
```

## Serv-U Simulation

The default demo uses a local `file://` URL pointed at `Assets/StreamingAssets/RemoteServer`, so it works immediately.

To simulate a remote resource server with Serv-U:

1. In Serv-U, create a domain/user whose root directory is this folder:
   `Assets/StreamingAssets/RemoteServer`
2. Serve it over HTTP if available, for example:
   `http://127.0.0.1:8080/`
3. Set the URL at runtime before Play Mode with:

```csharp
PlayerPrefs.SetString("XLuaDemo.RemoteBaseUrl", "http://127.0.0.1:8080");
```

You can also set it from Unity menu:

`XLua Demo/Set Serv-U HTTP URL`

To return to the built-in local file server, use:

`XLua Demo/Use Local StreamingAssets Server`

Another option is placing `XLuaDemoBootstrap` on a scene object and filling `remoteBaseUrlOverride` in the Inspector.

## Build Remote AssetBundles

Use Unity menu:

`XLua Demo/Build Remote Resource Server`

That command creates:

- `Assets/XLuaDemoGenerated/ABSource/FestivalActivityPanel.prefab`
- `Assets/XLuaDemoGenerated/ABSource/festival_banner.png`
- AssetBundles under `Assets/StreamingAssets/RemoteServer/bundles`
- A refreshed `manifest.json` with MD5 and file size entries

After building, enter Play Mode. The updater downloads only changed files into the persistent hot update cache, then the activity UI loads the prefab and texture from AssetBundle instead of using the fallback generated UI.
