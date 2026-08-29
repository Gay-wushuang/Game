using Godot;
using System;
using System.Linq;

public static class ExpansionCardRegressionTest
{
    public static void Run(Godot.Collections.Array<CardDefinition> cards)
    {
        Check(cards.Count == 60, "扩展后目录必须为60张");
        Check(cards.Count(c => c.design_code.StartsWith("TA-0", StringComparison.Ordinal)) == 30, "主动编号必须完整");
        Check(cards.Count(c => c.design_code.StartsWith("TP-0", StringComparison.Ordinal)) == 30, "被动编号必须完整");
        CheckActive(cards);
        CheckPassive(cards);
        GD.Print("[PASS] ExpansionCardRegressionTest：30张新增卡牌效果真实回归通过");
    }

    private static void CheckPassive(Godot.Collections.Array<CardDefinition> cards)
    {
        {
            var x = Setup(cards, "CHAIN_REACTION"); var effect = new CardInstance(cards.First(c => c.handler_key == "OVERLOAD_TURRET")); x.Battle.SetPassive("player", 1, effect); Resolve(x); Check(effect.RuntimeFlags.Contains("chain_enhanced"), "TP-016连锁反应错误");
        }
        {
            var x = Setup(cards, "DELAYED_FUSE"); var card = new CardInstance(x.Definition); Resolve(x, card); Check(x.Target.Hp == 100 && card.RuntimeInts["latent"] == 1, "TP-017延时引信首次计数错误"); Resolve(x, card); Check(x.Target.Hp == 85, "TP-017延时引信归零伤害错误");
        }
        {
            var x = Setup(cards, "MIRROR_ILLUSION"); var subject = new CardInstance(cards.First(c => c.handler_key == "OVERLOAD_TURRET"), "ai"); x.Battle.CurrentPassiveEvent = new() { EventKey = "PASSIVE_SET", SubjectCard = subject, SubjectOwnerId = "ai" }; var mirror = new CardInstance(x.Definition); Resolve(x, mirror); Check(mirror.RuntimeStrings["mirror_instance"] == subject.InstanceId, "TP-018镜花水月没有记录安全实例");
        }
        {
            var x = Setup(cards, "BLOOD_SACRIFICE"); var sacrifice = new CardInstance(cards.First(c => c.handler_key == "OVERLOAD_TURRET")); x.Battle.SetPassive("player", 1, sacrifice); x.Target = x.Source; x.Source.Hp = 0; Resolve(x); Check(x.Source.Hp == 50 && x.Player.DiscardPile.Contains(sacrifice), "TP-019血祭错误");
        }
        {
            var x = Setup(cards, "DYING_WISH"); x.Source.Hp = 50; Resolve(x); Check(x.Source.Hp == 65, "TP-020死不瞑目错误");
        }
        {
            var x = Setup(cards, "OVERLOAD_TURRET"); Resolve(x); Check(x.Target.Hp == 75 && x.Battle.PlayerNextTurnBonus == -2, "TP-021过载炮台错误");
        }
        {
            var x = Setup(cards, "CORROSIVE_FOG"); var card = new CardInstance(x.Definition); Resolve(x, card); Resolve(x, card); Resolve(x, card); Check(x.Target.Hp == 85 && card.RuntimeInts["corrosion_count"] == 3, "TP-022侵蚀毒雾错误");
        }
        {
            var x = Setup(cards, "SOUL_CONTRACT"); x.Battle.CurrentPassiveEvent = new() { EventKey = "BEFORE_ATTACK", SourceUnit = x.Target, AttackTarget = x.Source, SubjectOwnerId = "ai" }; Resolve(x); Check(x.Target.Hp == 90, "TP-023灵魂契约生命代价错误");
        }
        {
            var x = Setup(cards, "MUTUAL_DESTRUCTION"); x.Battle.CurrentPassiveEvent = new() { EventKey = "BEFORE_DAMAGE", SourceUnit = x.Target, AttackTarget = x.Source, PendingDamage = 100, SubjectOwnerId = "player" }; var exec = Resolve(x); Check(exec.Cancelled && x.Target.Hp == 0, "TP-024同归于尽错误");
        }
        {
            var x = Setup(cards, "SUBSTITUTION"); var exec = Resolve(x); Check(exec.Cancelled && x.Battle.RuntimeFlags.Contains("substitution_used:player"), "TP-025替身术错误");
        }
        {
            var x = Setup(cards, "REWIND_TIME"); var recover = new CardInstance(cards[0]); x.Player.DiscardPile.Add(recover); Resolve(x); Check(x.Player.Hand.Contains(recover), "TP-026时光倒流错误");
        }
        {
            var x = Setup(cards, "OPPORTUNISTIC_THEFT"); var top = new CardInstance(cards[0], "ai"); x.Enemy.DrawPile.Add(top); Resolve(x); Check(x.Player.Hand.Single().IsTemporaryCopy && x.Enemy.PeekPreparedTop() == top, "TP-027顺手牵羊错误");
        }
        {
            var x = Setup(cards, "FINAL_DEFENSE"); x.Source.Hp = 10; Resolve(x); Check(x.Source.Hp == 100 && x.Source.Attack == 30 && x.Source.RuntimeInts["final_defense_turns"] == 2 && x.Source.RuntimeFlags.Contains("retaliation_immune"), "TP-028最终防线错误");
        }
        {
            var x = Setup(cards, "ENERGY_SIPHON"); x.Battle.EnemyActionPoints = 5; Resolve(x); Check(x.Battle.EnemyActionPoints == 4 && x.Battle.PlayerNextTurnBonus == 1, "TP-029能量虹吸错误");
        }
        {
            var x = Setup(cards, "DRAGNET"); var held = new CardInstance(cards[0], "ai"); x.Enemy.Hand.Add(held); var exec = Resolve(x); Check(exec.Cancelled && x.Enemy.DiscardPile.Contains(held), "TP-030天罗地网错误");
        }
    }

    private static void CheckActive(Godot.Collections.Array<CardDefinition> cards)
    {
        {
            var x = Setup(cards, "DESPERATE_GAMBIT"); x.Source.Hp = 100; Resolve(x); Check(x.Source.Hp == 80 && x.Target.Hp == 60, "TA-016破釜沉舟错误");
        }
        {
            var x = Setup(cards, "CHAIN_SCHEME"); Resolve(x); Check(x.Battle.RuntimeInts["chain:player"] == 1, "TA-017连环计错误");
        }
        {
            var x = Setup(cards, "GATHERING_FORCE"); Resolve(x); Check(x.Source.Attack == 24 && x.Source.RuntimeInts["charge_stacks"] == 2, "TA-018蓄势待发错误");
        }
        {
            var x = Setup(cards, "GRAFT_BUFF"); x.Target.RuntimeInts["flame_marks"] = 3; Resolve(x); Check(x.Source.RuntimeInts["flame_marks"] == 3 && !x.Target.RuntimeInts.ContainsKey("flame_marks"), "TA-019移花接木错误");
        }
        {
            var x = Setup(cards, "BURN_BOOKS"); var fodder = new CardInstance(cards[0]); x.Player.DiscardPile.Add(fodder); x.Selection.SelectedCardIds.Add(fodder.InstanceId); x.Selection.EnemySlots.Add(0); Resolve(x); Check(x.Player.ExilePile.Contains(fodder) && x.Target.Hp == 95, "TA-020焚书坑儒错误");
        }
        {
            var x = Setup(cards, "CICADA_ESCAPE"); Resolve(x); Check(x.Source.RuntimeInts["cicada_rounds"] == 2 && x.Source.RuntimeInts["cicada_heal_pct"] == 15, "TA-021金蝉脱壳错误");
        }
        {
            var x = Setup(cards, "FLAME_MARK"); Resolve(x); Check(x.Target.RuntimeInts["flame_marks"] == 3, "TA-022烈焰印记错误");
        }
        {
            var x = Setup(cards, "MEND_BROKEN_MIRROR"); x.Source.LinkTurns = x.Target.LinkTurns = 1; Resolve(x); Check(x.Source.LinkTurns == 0 && x.Target.LinkTurns == 0 && x.Source.Hp == 92 && x.Target.Hp == 92, "TA-023破镜重圆错误");
        }
        {
            var x = Setup(cards, "HEAVENLY_REINFORCEMENT"); var deployed = false; x.Deploy = _ => deployed = true; Resolve(x); Check(deployed, "TA-024天降神兵未调用后备英雄部署入口");
        }
        {
            var x = Setup(cards, "BACKLASH_CURSE"); Resolve(x); Check(x.Source.Hp == 92 && x.Target.Hp == 85, "TA-025反噬之咒错误");
        }
        {
            var x = Setup(cards, "STEAL_DAYLIGHT"); var original = new CardInstance(cards[0], "ai"); x.Enemy.DiscardPile.Add(original); x.Selection.SelectedCardIds.Add(original.InstanceId); Resolve(x); Check(x.Player.Hand.Single().IsTemporaryCopy && x.Player.Hand.Single().CurrentCost() == 0, "TA-026偷天换日错误");
        }
        {
            var x = Setup(cards, "RECUPERATE"); x.Source.Hp = 50; Resolve(x); Check(x.Source.Hp == 60 && x.Source.RuntimeFlags.Contains("rest_no_attack"), "TA-027休养生息错误");
        }
        {
            var x = Setup(cards, "COVERT_PASSAGE"); Resolve(x); Check(x.Source.Attack == 26 && x.Source.RuntimeFlags.Contains("stealth"), "TA-028暗度陈仓错误");
        }
        {
            var x = Setup(cards, "BREAKER"); x.Battle.EnemyUnits.Add(new UnitState { Definition = new HeroDefinition { id = "extra_enemy" }, Name = "额外敌人", Hp = 100, MaxHp = 100, Attack = 10 }); Resolve(x); Check(x.Source.Attack == 40 && x.Battle.RuntimeFlags.Contains("breaker_used:player"), "TA-029破局者错误");
        }
        {
            var x = Setup(cards, "UNDERMINE"); var held = new CardInstance(cards[0], "ai"); x.Enemy.Hand.Add(held); x.Selection.SelectedCardIds.Add(held.InstanceId); Resolve(x); Check(x.Enemy.DiscardPile.Contains(held), "TA-030釜底抽薪错误");
        }
    }

    private static Fixture Setup(Godot.Collections.Array<CardDefinition> cards, string handler)
    {
        var player = new DeckState(); var enemy = new DeckState(); player.Setup([], "player"); enemy.Setup([], "ai"); var battle = new BattleState(player, enemy, 99);
        var source = new UnitState { Definition = new HeroDefinition { id = "exp_source" }, Name = "使用者", Hp = 100, MaxHp = 100, Attack = 20, Star = 2 };
        var target = new UnitState { Definition = new HeroDefinition { id = "exp_target" }, Name = "目标", Hp = 100, MaxHp = 100, Attack = 20, Star = 2 };
        battle.PlayerUnits.Add(source); battle.EnemyUnits.Add(target); battle.SetSlotUnit("player", 0, source); battle.SetSlotUnit("ai", 0, target);
        return new(cards.First(c => c.handler_key == handler), battle, player, enemy, source, target);
    }
    private static CardExecutionContext Resolve(Fixture x, CardInstance? card = null)
    {
        var context = new CardExecutionContext { State = x.Battle, Card = card ?? new CardInstance(x.Definition), OwnerDeck = x.Player, OpponentDeck = x.Enemy, Source = x.Source, Target = x.Target, Selection = x.Selection, DeployReserveHero = x.Deploy, Log = _ => { } };
        new CardApi(context).ResolveCardEffect(x.Definition.handler_key); return context;
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private sealed class Fixture(CardDefinition definition, BattleState battle, DeckState player, DeckState enemy, UnitState source, UnitState target)
    {
        public CardDefinition Definition { get; } = definition; public BattleState Battle { get; } = battle; public DeckState Player { get; } = player; public DeckState Enemy { get; } = enemy; public UnitState Source { get; } = source; public UnitState Target { get; set; } = target;
        public CardSelectionTransaction Selection { get; } = new() { OwnerId = "player", IsComplete = true }; public Func<int, bool>? Deploy { get; set; }
    }
}
