local hotfix_type = CS.XLuaDemo.CoreBattleService

xlua.hotfix(hotfix_type, 'CalculateDamage', function(self, attack, defense, skillPower)
    local baseDamage = attack * skillPower / 100
    local armorBreak = math.floor(defense * 0.55)
    local eventBonus = 24
    local finalDamage = math.floor(baseDamage - armorBreak + eventBonus)

    if finalDamage < 1 then
        finalDamage = 1
    end

    CS.UnityEngine.Debug.Log(string.format('[LuaHotfix] CalculateDamage attack=%d defense=%d skill=%d final=%d', attack, defense, skillPower, finalDamage))
    return finalDamage
end)

xlua.hotfix(hotfix_type, 'CalculateCriticalRate', function(self, luck)
    local rate = 0.12 + luck * 0.004
    if rate > 0.85 then
        rate = 0.85
    end
    return rate
end)

return true
