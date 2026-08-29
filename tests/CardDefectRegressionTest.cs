using Godot;
using System;
using System.Linq;

/// <summary>V2 卡牌缺陷回归；所有断言均执行正式 CardApi 或正式数据入口。</summary>
public static class CardDefectRegressionTest
{
	public static void Run(Godot.Collections.Array<CardDefinition> cards)
	{
		Check(cards.Count == CardCatalog.V2ExpectedCount, "V2 卡牌数量错误");
		Check(cards.All(card => CardApi.SupportsHandler(card.handler_key)), "存在未注册的 V2 handler");
		CheckComponentModel(cards);
		CheckExpansionInfrastructure(cards);
		CheckLuaEntrypoints(cards);
		CheckNextTurnMinimum();
		CheckCoreActiveEffects(cards);
		CheckPileSelection(cards);
		CheckTemporaryCopies(cards);
		CheckPassiveEffects(cards);
		GD.Print("[PASS] CardDefectRegressionTest：V2 handler、Lua入口、AP下限及核心效果回归通过");
	}

	private static void CheckExpansionInfrastructure(Godot.Collections.Array<CardDefinition> cards)
	{
		var deck = new DeckState(); deck.Setup([], "player");
		var top = new CardInstance(cards[0]); deck.DiscardPile.Add(top); deck.PrepareDrawPile(1);
		Check(deck.PeekPreparedTop() == top && deck.DiscardPile.Count == 0, "BEFORE_DRAW 前必须完成弃牌堆回洗并提供确定牌堆顶");

		var target = new UnitState { Definition = new HeroDefinition { id = "damage_pipeline_target" }, Hp = 20, MaxHp = 20, ShieldPoints = 4 };
		var normal = DamageResolution.Apply(new(null, target, null, 10), 10);
		Check(normal.AbsorbedByShield == 4 && normal.FinalHpDamage == 6 && target.Hp == 14, "伤害管线必须区分护盾吸收和最终HP伤害");
		var cost = DamageResolution.Apply(new(null, target, null, 99, "LIFE_COST", true, true), 99);
		Check(cost.FinalHpDamage == 13 && target.Hp == 1, "生命消耗必须绕过护盾且不能使英雄低于1HP");
	}

	private static void CheckComponentModel(Godot.Collections.Array<CardDefinition> cards)
	{
		Check(cards.All(card => card.components.IsExplicit), "V2 卡牌必须使用显式组件数据");
		Check(cards.All(card => card.components.Effect.HandlerKey == card.handler_key), "效果组件没有正确投影到 Lua handler");
		Check(cards.All(card => card.components.Target.Key == card.target_key), "目标组件没有正确投影到现有目标接口");
		Check(cards.All(card => card.components.Cost.BaseCost == card.action_cost), "费用组件没有正确投影到现有费用接口");
		Check(cards.All(card => card.components.Triggers.Keys.SequenceEqual(card.trigger_keys)), "触发组件没有正确投影到被动事件接口");

		var exileDefinition = new CardDefinition {
			components = new CardComponentSet { Lifecycle = new CardLifecycleComponent { OnResolve = CardLifecycleComponent.Exile } }
		};
		var deck = new DeckState();
		deck.Setup([], "player");
		var exileCard = new CardInstance(exileDefinition, "player");
		deck.Hand.Add(exileCard);
		deck.FinishPlayedCard(exileCard);
		Check(deck.ExilePile.Contains(exileCard) && !deck.DiscardPile.Contains(exileCard), "生命周期组件未控制结算后区域");
	}

	private static void CheckLuaEntrypoints(Godot.Collections.Array<CardDefinition> cards)
	{
		foreach (var card in cards)
		{
			using var file = FileAccess.Open(card.lua_script, FileAccess.ModeFlags.Read);
			Check(file != null, $"Lua 文件不存在：{card.lua_script}");
			var expected = $"resolve_card_effect(\"{card.handler_key}\")";
			Check(file!.GetAsText().Contains(expected, StringComparison.Ordinal), $"{card.display_name} Lua 未调用 V2 handler {card.handler_key}");
		}
	}

	private static void CheckNextTurnMinimum()
	{
		Check(TrainingArena.CalculateNextTurnActionPoints(false, null, -99) == 0, "玩家/AI 下回合 AP 未限制最低0");
		Check(TrainingArena.CalculateNextTurnActionPoints(false, 7, -2) == 5, "动态 AP override 计算错误");
		Check(TrainingArena.CalculateNextTurnActionPoints(true, 7, 8) == 0, "强制归零优先级错误");
	}

	private static void CheckCoreActiveEffects(Godot.Collections.Array<CardDefinition> cards)
	{
		var (battle, player, enemy, ally, foe) = CreateBattle();
		ally.Hp = 50;
		Resolve(cards, "HEAL_PERCENT", battle, player, enemy, ally, ally);
		Check(ally.Hp == 65, "妙手回春必须回复 MaxHp 的15%");
		Resolve(cards, "APPLY_SHIELD", battle, player, enemy, ally, ally);
		Check(ally.ShieldPoints == 20, "重甲必须获得 MaxHp 的20%护盾");
		Resolve(cards, "DEAL_DAMAGE", battle, player, enemy, null, foe);
		Check(foe.Hp == 75, "暴力手段必须造成25点伤害");
		Resolve(cards, "APPLY_GRUDGE", battle, player, enemy, null, null);
		Check(foe.GrudgeStacks == 3 && foe.Attack == 17 && foe.GrudgeAttackPenaltyPerStack == 1, "怨恨必须施加3层并立即降低攻击");

		player.Hand.Clear();
		var guide = Card(cards, "DISCARD_DRAW_AP");
		player.Hand.Add(guide);
		player.Hand.Add(new CardInstance(cards[0], "player"));
		player.Hand.Add(new CardInstance(cards[1], "player"));
		player.DrawPile.Add(new CardInstance(cards[2], "player"));
		player.DrawPile.Add(new CardInstance(cards[3], "player"));
		battle.PlayerActionPoints = 1;
		new CardApi(Context(battle, guide, player, enemy)).ResolveCardEffect("DISCARD_DRAW_AP");
		Check(player.DiscardPile.Count == 2 && battle.PlayerActionPoints == 3, "迷蒙指引不足3张时必须全部弃置并获得2AP");
	}

	private static void CheckPileSelection(Godot.Collections.Array<CardDefinition> cards)
	{
		var (battle, player, enemy, _, _) = CreateBattle();
		var scout = Card(cards, "SELECT_FROM_PILES");
		var first = new CardInstance(cards[0], "player");
		var duplicate = new CardInstance(cards[0], "player");
		var second = new CardInstance(cards[1], "player");
		player.DrawPile.Add(first);
		player.DiscardPile.Add(duplicate);
		player.DiscardPile.Add(second);
		new CardApi(Context(battle, scout, player, enemy)).ResolveCardEffect("SELECT_FROM_PILES");
		Check(player.Hand.Count == 2 && player.Hand.Select(card => card.Definition.id.ToString()).Distinct().Count() == 2, "侦查必须同时检索抽牌堆/弃牌堆中的不同卡");
	}

	private static void CheckTemporaryCopies(Godot.Collections.Array<CardDefinition> cards)
	{
		var (battle, player, enemy, _, _) = CreateBattle();
		var original = new CardInstance(cards[0], "ai");
		enemy.Hand.Add(original);
		Resolve(cards, "STEAL_TEMPORARY", battle, player, enemy, null, null);
		Check(enemy.DiscardPile.Contains(original), "拿来主义必须把敌方原牌移入敌方弃牌堆");
		Check(player.Hand.Count == 1 && player.Hand[0].IsTemporaryCopy && player.Hand[0].CurrentCost() == 0 && player.Hand[0].ExileAtTurnEnd, "拿来主义复制牌必须0费且回合结束放逐");

		var copier = Card(cards, "COPY_AND_EXPIRE");
		battle.CurrentPassiveEvent = new PassiveEventContext { EventKey = "AFTER_CARD_RESOLVE", SubjectCard = original, SubjectOwnerId = "ai" };
		new CardApi(Context(battle, copier, player, enemy)).ResolveCardEffect("COPY_AND_EXPIRE");
		Check(player.Hand.Last().IsTemporaryCopy && player.Hand.Last().ExileAtTurnEnd, "以偏概全必须生成临时放逐复制");
	}

	private static void CheckPassiveEffects(Godot.Collections.Array<CardDefinition> cards)
	{
		var (battle, player, enemy, ally, _) = CreateBattle();
		var objection = Card(cards, "CANCEL_DAMAGE");
		var cancelContext = Context(battle, objection, player, enemy, ally, ally);
		new CardApi(cancelContext).ResolveCardEffect("CANCEL_DAMAGE");
		Check(cancelContext.Cancelled, "异议必须取消伤害事件");

		var revive = Card(cards, "REVIVE_WITH_PENALTY");
		ally.Hp = 0;
		ally.Star = 3;
		new CardApi(Context(battle, revive, player, enemy, ally, ally)).ResolveCardEffect("REVIVE_WITH_PENALTY");
		Check(ally.Hp == 40 && ally.Star == 2 && !ally.DeathHandled, "杀出冥界复活数值错误");

		battle.CurrentPassiveEvent = new PassiveEventContext { EventKey = "BEFORE_ATTACK", AttackTargetSlot = 0, AliveAllySlots = [0, 1] };
		Resolve(cards, "REDIRECT_ATTACK", battle, player, enemy, null, null);
		Check(battle.CurrentPassiveEvent.RedirectSlot == 1, "背刺必须改为其他存活友方目标");
	}

	private static void Resolve(Godot.Collections.Array<CardDefinition> cards, string handler, BattleState battle, DeckState owner, DeckState opponent, UnitState? source, UnitState? target)
	{
		var card = Card(cards, handler);
		new CardApi(Context(battle, card, owner, opponent, source, target)).ResolveCardEffect(handler);
	}

	private static CardInstance Card(Godot.Collections.Array<CardDefinition> cards, string handler) => new(cards.First(card => card.handler_key == handler), "player");
	private static CardExecutionContext Context(BattleState battle, CardInstance card, DeckState owner, DeckState opponent, UnitState? source = null, UnitState? target = null) => new() { State = battle, Card = card, OwnerDeck = owner, OpponentDeck = opponent, Source = source, Target = target, Log = _ => { } };

	private static (BattleState Battle, DeckState Player, DeckState Enemy, UnitState Ally, UnitState Foe) CreateBattle()
	{
		var player = new DeckState();
		var enemy = new DeckState();
		player.Setup([], "player");
		enemy.Setup([], "ai");
		var battle = new BattleState(player, enemy, 4242);
		var ally = new UnitState { Definition = new HeroDefinition { id = "test_ally" }, Name = "友方", Type = "先锋", Hp = 100, MaxHp = 100, Attack = 20, Star = 3 };
		var foe = new UnitState { Definition = new HeroDefinition { id = "test_enemy" }, Name = "敌方", Type = "刺客", Hp = 100, MaxHp = 100, Attack = 20, Star = 3 };
		battle.PlayerUnits.Add(ally);
		battle.EnemyUnits.Add(foe);
		battle.SetSlotUnit("player", 0, ally);
		battle.SetSlotUnit("ai", 0, foe);
		return (battle, player, enemy, ally, foe);
	}

	private static void Check(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}
