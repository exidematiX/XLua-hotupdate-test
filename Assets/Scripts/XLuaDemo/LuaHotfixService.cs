using System;
using System.IO;
using System.Text;
using UnityEngine;

#if XLUA_PRESENT
using XLua;
#endif

namespace XLuaDemo
{
    public sealed class LuaHotfixService : IDisposable
    {
#if XLUA_PRESENT
        private LuaEnv luaEnv;
#endif

        public bool IsReady { get; private set; }

        public void Install(string localLuaRoot, Action<string> log)
        {
#if XLUA_PRESENT
            Dispose();
            luaEnv = new LuaEnv();
            luaEnv.AddLoader((ref string fileName) => LoadLua(fileName, localLuaRoot));

            try
            {
                luaEnv.DoString("require('HotfixBattle')");
                IsReady = true;
                log("Lua hotfix installed: CoreBattleService.CalculateDamage is now owned by Lua.");
            }
            catch (Exception ex)
            {
                IsReady = false;
                log("Lua hotfix failed: " + ex.Message);
            }
#else
            IsReady = false;
            log("XLua is not installed or XLUA_PRESENT is not defined. Lua script was downloaded, but C# hotfix injection is skipped.");
#endif
        }

        public void Tick()
        {
#if XLUA_PRESENT
            if (luaEnv != null)
            {
                luaEnv.Tick();
            }
#endif
        }

        public void Dispose()
        {
#if XLUA_PRESENT
            if (luaEnv != null)
            {
                luaEnv.Dispose();
                luaEnv = null;
            }
#endif
            IsReady = false;
        }

#if XLUA_PRESENT
        private static byte[] LoadLua(string fileName, string localLuaRoot)
        {
            string relativePath = fileName.Replace('.', '/') + ".lua";
            string localPath = Path.Combine(localLuaRoot, relativePath);
            if (File.Exists(localPath))
            {
                return File.ReadAllBytes(localPath);
            }

            string streamingPath = Path.Combine(Application.streamingAssetsPath, "Lua", relativePath);
            if (File.Exists(streamingPath))
            {
                return File.ReadAllBytes(streamingPath);
            }

            return Encoding.UTF8.GetBytes(string.Empty);
        }
#endif
    }
}
