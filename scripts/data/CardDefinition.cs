using Godot;

[GlobalClass]
public partial class CardDefinition : ContentDefinition
{
    public enum CardKind { Active, Passive }
    public enum TargetKind { SelfHero, AllyHero, Enemy, AnyUnit, None }
    public enum BuiltinEffect { Heal, AddAttack, AddExp, Damage, Custom, StarUp, StealCard, CancelEnemyDraw }
    [ExportCategory("卡牌规则")]
    [Export] public CardKind card_kind { get; set; }
    [Export] public TargetKind target_kind { get; set; } = TargetKind.AllyHero;
    [Export(PropertyHint.Range, "0,20,1")] public int action_cost { get; set; } = 1;
    [Export] public BuiltinEffect builtin_effect { get; set; } = BuiltinEffect.Custom;
    [Export] public int effect_amount { get; set; } = 1;
    [Export] public Script? effect_script { get; set; }
    [Export(PropertyHint.MultilineText)] public string rules_text { get; set; } = "";
}
