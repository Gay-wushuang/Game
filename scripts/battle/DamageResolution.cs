using System;

public sealed record DamageRequest(
    UnitState? Source,
    UnitState Target,
    CardInstance? Card,
    int Amount,
    string Tag = "NORMAL",
    bool BypassShield = false,
    bool IsLifeCost = false,
    bool SuppressRecursiveResponses = false);

public sealed record DamageResult(int Requested, int AfterModifiers, int AbsorbedByShield, int FinalHpDamage, bool Cancelled);

/// <summary>唯一的伤害数值提交入口；事件响应由调用者在提交前后编排。</summary>
public static class DamageResolution
{
    public static DamageResult Apply(DamageRequest request, int modifiedAmount, bool cancelled = false)
    {
        var requested = Math.Max(0, request.Amount);
        if (cancelled || requested == 0) return new(requested, 0, 0, 0, cancelled);
        var incoming = Math.Max(0, modifiedAmount);
        var absorbed = request.BypassShield ? 0 : Math.Min(request.Target.ShieldPoints, incoming);
        if (!request.BypassShield) request.Target.ShieldPoints -= absorbed;
        incoming -= absorbed;
        var hpLimit = request.IsLifeCost ? Math.Max(0, request.Target.Hp - 1) : request.Target.Hp;
        var applied = Math.Min(hpLimit, incoming);
        request.Target.Hp -= applied;
        return new(requested, modifiedAmount, absorbed, applied, false);
    }
}
