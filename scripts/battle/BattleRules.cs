using Godot;
using System.Collections.Generic;

public static class BattleRules
{
    private static readonly Dictionary<string, HashSet<string>> Counters = new()
    {
        ["先锋"] = new HashSet<string> { "刺客", "祭司" },
        ["刺客"] = new HashSet<string> { "斥候", "祭司" },
        ["斥候"] = new HashSet<string> { "先锋", "祭司" },
    };

    public static string GetRelation(string attackerType, string defenderType)
    {
        if (Counters.TryGetValue(attackerType, out var counteredTypes) && counteredTypes.Contains(defenderType))
            return "克制";
        if (Counters.TryGetValue(defenderType, out var attackerCounters) && attackerCounters.Contains(attackerType))
            return "被克制";
        return "中性";
    }

    public static int CalculateRetaliation(UnitState attacker, UnitState defender)
    {
        float v = defender.Attack * defender.RetaliationRatio;
        string relation = GetRelation(attacker.Type, defender.Type);
        
        if (relation == "克制")
            v *= 0.5f;
        else if (relation == "被克制")
        {
            v *= 1.5f;
            if (attacker.Star >= 3) v *= 0.75f;
        }
        
        return Mathf.RoundToInt(v);
    }

    public static int CalculateAttackValue(UnitState attacker, UnitState defender)
    {
        int v = attacker.Attack;
        
        if (attacker.Id == "hero_role_2")
        {
            v += 5;
            if (attacker.SkillTurns > 0) v += 10;
            if (attacker.Star >= 2 && defender.Hp * 2 < defender.MaxHp) v += 5;
        }
        
        return v;
    }
}
