using UnityEngine;

#if XLUA_PRESENT
using XLua;
#endif

namespace XLuaDemo
{
#if XLUA_PRESENT
    [Hotfix]
#endif
    public class CoreBattleService
    {
        public virtual int CalculateDamage(int attack, int defense, int skillPower)
        {
            int rawDamage = attack * skillPower / 100 - defense;
            return Mathf.Max(1, rawDamage);
        }

        public virtual float CalculateCriticalRate(int luck)
        {
            return Mathf.Clamp01(0.05f + luck * 0.002f);
        }
    }
}
