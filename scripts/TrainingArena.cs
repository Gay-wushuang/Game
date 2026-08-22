using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class TrainingArena : Control
{
	[Export] public TrainingContent content { get; set; } = null!;
	private PackedScene _slotScene = null!, _cardScene = null!;
	private Label _status = null!; private HandFan _hand = null!; private BattleRightSidebar _rightSidebar = null!; private TurnControl _turnControl = null!; private PassiveGate _passiveGate = null!;
	private readonly DeckState _deck = new(), _aiDeck = new();
	private BattleState _battle = null!; private CardResolver _cardResolver = null!; private readonly PassiveTriggerResolver _passiveResolver = new();
	private readonly List<UnitSlot> _allies = [], _enemies = [];
	private readonly List<HeroCardInstance> _heroBag = [], _aiHeroBag = [];
	private readonly List<string> _logs = [];
	private HeroCardInstance? _pendingHero; private CardInstance? _pendingCard; private UnitSlot? _pendingStarSlot;
	private int _pendingCardTarget = -1, _allyIndex = -1, _enemyIndex = -1, _ap = BattleState.DefaultActionPoints, _turn = 1, _pendingStarCost;
	private string _leaderId = "", _freeCardId = ""; private int _leaderTurns; private bool _cancelNextEnemyEffect, _playerDeployedThisTurn, _aiDeployedThisTurn;
	private readonly List<(string Kind, object Target, UnitSlot? Slot)> _testTargets = [];

	public override void _Ready()
	{
		content.cards = CardCatalog.Load();
		_battle = new(_deck, _aiDeck, 20260817); _cardResolver = new();
		ValidateCardScripts();
		_slotScene = GD.Load<PackedScene>("res://scenes/components/unit_slot.tscn"); _cardScene = GD.Load<PackedScene>("res://scenes/components/card_tile.tscn");
		_status = GetNode<Label>("%Status"); _hand = GetNode<HandFan>("%Hand"); _rightSidebar = GetNode<BattleRightSidebar>("%ContentHost"); _turnControl = GetNode<TurnControl>("%TurnControl"); _passiveGate = GetNode<PassiveGate>("%PassiveGate"); _passiveGate.DetailRequested += ShowCardDetail;
		_battle.Events.Subscribe(BattleEvent.BattleEnded, HandleBattleEnd);
		ConnectControls(); CreateSlots(); ResetTraining();
		if (DisplayServer.GetName() != "headless") { AudioManager.Instance?.PlayBattleMusic(); AudioManager.Instance?.PlaySfx(GameSfx.Horn); }
	}
	public override void _ExitTree() => _cardResolver?.Dispose();
	public override void _UnhandledInput(InputEvent input)
	{
		if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
		{
			if (_pendingHero != null || _pendingCard != null || _allyIndex >= 0 || _enemyIndex >= 0) CancelSelection();
			GetViewport().SetInputAsHandled();
			return;
		}
		if (input is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
		if (_rightSidebar.ShowingDetail) { _rightSidebar.ShowCommanderOverview(); GetViewport().SetInputAsHandled(); return; }
		if (_pendingHero != null || _pendingCard != null || _allyIndex >= 0 || _enemyIndex >= 0) { CancelSelection(); GetViewport().SetInputAsHandled(); }
	}
	private void ValidateCardScripts()
	{
		if (content.cards.Count != CardCatalog.V2ExpectedCount) throw new InvalidOperationException($"V2卡牌目录数量错误：{content.cards.Count}，预期 {CardCatalog.V2ExpectedCount}");
		foreach (var card in content.cards) { if (card.logic_mode != "LUA" || string.IsNullOrWhiteSpace(card.lua_script)) throw new InvalidOperationException($"{card.display_name}没有独立Lua入口"); if (!_cardResolver.ValidateLua(card.lua_script, out var error)) throw new InvalidOperationException($"{card.display_name} Lua校验失败：{error}"); }
	}
	private T N<T>(string name) where T : Node => GetNode<T>('%' + name);
	private void ConnectControls()
	{
		N<Control>("Battlefield").GuiInput += HandleBattlefieldBlankInput;
		N<Button>("HeroBag").Pressed += OpenHeroBag; N<Button>("DrawPile").Pressed += () => ShowPile("抽牌堆", _deck.DrawPile);
		N<Button>("DiscardPile").Pressed += () => ShowPile("弃牌堆", _deck.DiscardPile); N<Button>("CatalogButton").Pressed += ShowCatalog;
		N<Button>("EndTurnButton").Pressed += async () => { AudioManager.Instance?.PlaySfx(GameSfx.NextRound); await EndTurn(); };
		N<Button>("LogButton").Pressed += () => N<AcceptDialog>("LogDialog").PopupCenteredRatio(.72f); N<Button>("SettingsButton").Pressed += () => N<AcceptDialog>("SettingsDialog").PopupCenteredRatio(.56f);
		N<Button>("ResetButton").Pressed += () => { N<AcceptDialog>("SettingsDialog").Hide(); ResetTraining(); };
		N<CheckButton>("DummyMode").Toggled += _ => ResetTraining(); N<CheckButton>("TestMode").Toggled += TestModeToggled;
		N<Button>("OpenTestEditor").Pressed += OpenTestEditor; N<OptionButton>("Category").ItemSelected += i => LoadTestCategory((int)i); N<OptionButton>("Target").ItemSelected += i => LoadTestTarget((int)i);
		N<Button>("ApplyTestValues").Pressed += ApplyTestValues; N<AcceptDialog>("StarChoiceDialog").Confirmed += () => ApplyStarChoice(true); N<AcceptDialog>("StarChoiceDialog").Canceled += () => ApplyStarChoice(false);
		N<Button>("ReloadLuaButton").Pressed += ReloadCardScripts;
	}
	private void HandleBattlefieldBlankInput(InputEvent input)
	{
		if (input is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }) return;
		if (_pendingHero != null || _pendingCard != null || _allyIndex >= 0 || _enemyIndex >= 0) CancelSelection();
		AcceptEvent();
	}
	private void CreateSlots()
	{
		for (var i = 0; i < 5; i++) { var enemy = _slotScene.Instantiate<UnitSlot>(); enemy.Side = "enemy"; enemy.SlotIndex = i; enemy.SlotChosen += EnemyChosen; enemy.DetailRequested += ShowUnitDetail; enemy.CardDropped += OnCardDropped; N<HBoxContainer>("EnemyRow").AddChild(enemy); _enemies.Add(enemy); var ally = _slotScene.Instantiate<UnitSlot>(); ally.Side = "ally"; ally.SlotIndex = i; ally.SlotChosen += AllyChosen; ally.DetailRequested += ShowUnitDetail; ally.CardDropped += OnCardDropped; ally.SkillRequested += ContextSkillRequested; N<HBoxContainer>("AllyRow").AddChild(ally); _allies.Add(ally); }
	}
	public void ResetTraining()
	{
		_heroBag.Clear(); _aiHeroBag.Clear(); foreach (var h in content.heroes) { _heroBag.Add(new(h)); _aiHeroBag.Add(new(h, "ai")); }
		_battle.ResetRandom(); _battle.ClearSlotUnits(); _deck.Setup(content.cards, "player"); _deck.Draw(4); _aiDeck.Setup(content.cards, "ai");
		AudioManager.Instance?.PlaySfx(GameSfx.Shuffle);
		_ap = BattleState.DefaultActionPoints; _turn = 1; _battle.Turn = 1; _battle.PlayerActionPoints = BattleState.DefaultActionPoints; _battle.EnemyActionPoints = BattleState.DefaultActionPoints; _battle.PlayerNextTurnBonus = 0; _battle.EnemyNextTurnBonus = 0; _battle.PlayerNextTurnActionPointsOverride = null; _battle.EnemyNextTurnActionPointsOverride = null; _battle.Passives.Clear(); _battle.ResetOutcome();
		_battle.SetReserveHeroCount("player", _heroBag.Count); _battle.SetReserveHeroCount("ai", _aiHeroBag.Count);
		_logs.Clear(); _leaderId = _freeCardId = ""; _leaderTurns = 0; _cancelNextEnemyEffect = false; _playerDeployedThisTurn = _aiDeployedThisTurn = false; N<Label>("Title").Text = "训练场 · 第 1 回合";
		EnableAllBattleControls();
		foreach (var s in _allies) s.SetUnit(null);
		for (var i = 0; i < _enemies.Count; i++) s_set(_enemies[i], N<CheckButton>("DummyMode").ButtonPressed && i < content.monsters.Count ? FromMonster(content.monsters[i]) : null);
		SynchronizeBattleState();
		CancelSelection(false); AddLog($"[color=75d7ff]系统[/color] {(N<CheckButton>("DummyMode").ButtonPressed ? "三职业稻草人就位" : "AI持有4张英雄牌")}，双方开局各抽取4张锦囊。"); _status.Text = "打开英雄卡包，选择英雄并部署到我方空位"; RefreshAll();
	}
	private static void s_set(UnitSlot s, UnitState? u) => s.SetUnit(u);
	private void SynchronizeBattleState()
	{
		_battle.SynchronizeUnits(
			_allies.Where(slot => slot.Unit != null).Select(slot => slot.Unit!),
			_enemies.Where(slot => slot.Unit != null).Select(slot => slot.Unit!));
		// 同步槽位索引单位，供 TryPlacePassive 验证
		for (int i = 0; i < _allies.Count; i++)
			_battle.SetSlotUnit("player", i, _allies[i].Unit);
		for (int i = 0; i < _enemies.Count; i++)
			_battle.SetSlotUnit("ai", i, _enemies[i].Unit);
	}
	private CardExecutionContext CardContext(CardInstance card, UnitState? source = null, UnitState? target = null, bool ai = false)
	{
		SynchronizeBattleState(); if (!ai) _battle.PlayerActionPoints = _ap;
		return new() { State = _battle, Card = card, OwnerDeck = ai ? _aiDeck : _deck, OpponentDeck = ai ? _deck : _aiDeck, Source = source, Target = target, Log = AddLog };
	}
	private static UnitState FromMonster(MonsterDefinition d) => new() { Definition = d, Name = d.display_name, Type = d.TypeName(), Hp = d.max_hp, MaxHp = d.max_hp, Attack = d.attack, Star = 1, RetaliationRatio = d.retaliation_ratio };
	private void OpenHeroBag()
	{
		var list = N<VBoxContainer>("HeroList"); Clear(list); if (_heroBag.Count == 0) list.AddChild(new Label { Text = "所有英雄均已部署。" });
		foreach (var hc in _heroBag) { var h = hc.Definition; var b = new Button { Text = $"{HeroIdentity(h)} · {h.TypeName()}　HP {h.max_hp}　攻击 {h.attack}\n{h.description}", SizeFlagsVertical = Control.SizeFlags.ExpandFill }; b.Pressed += () => SelectHero(hc); list.AddChild(b); }
		N<AcceptDialog>("HeroBagDialog").PopupCenteredRatio(.62f);
	}
	public void SelectHero(HeroCardInstance hero) { if (_battle.IsFinished) return; if (_playerDeployedThisTurn) { _status.Text = "本回合已经部署过英雄，每回合最多上场1名"; return; } _pendingHero = hero; _pendingCard = null; N<AcceptDialog>("HeroBagDialog").Hide(); _status.Text = $"免费部署 {hero.Definition.display_name}：点击一个我方空位"; }
	private async void AllyChosen(UnitSlot slot)
	{
		if (_battle.IsFinished) return;
		if (_pendingHero != null) { if (_playerDeployedThisTurn) { _status.Text = "本回合已经部署过英雄"; return; } if (slot.Unit?.Alive == true) { _status.Text = "该站位已有存活英雄"; return; } var hero = _pendingHero; slot.SetUnit(hero.Deploy()); _battle.SetSlotUnit("player", slot.SlotIndex, slot.Unit); _heroBag.Remove(hero); _battle.DecrementReserveHero("player"); _playerDeployedThisTurn = true; if (_leaderId == "") { _leaderId = hero.Definition.id.ToString(); _leaderTurns = hero.Definition.CustomValue("leader_duration", 0).AsInt32(); ApplyLeaderBonus(); } else if (_leaderId == "hero_role_1" && (_leaderTurns > 0 || LeaderIsStarTwo())) { slot.Unit!.MaxHp += 50; slot.Unit.Hp += 50; } AddLog($"[color=75d7ff]免费部署[/color] {hero.Definition.display_name} 进入我方 {slot.SlotIndex + 1} 号位（本回合部署次数已用）。"); _pendingHero = null; _allyIndex = -1; RefreshAll(); AudioManager.Instance?.PlaySfx(GameSfx.Advance); await slot.PlayDeployAnimation(); return; }
		if (slot.Unit?.Alive != true) return;
		if (_pendingCard == null && _allyIndex == slot.SlotIndex && _enemyIndex < 0) { CancelSelection(); return; }
		_allyIndex = slot.SlotIndex; _enemyIndex = -1;
		if (_pendingCard != null) { if (_pendingCardTarget == slot.SlotIndex) await UseCard(slot); else PreviewCard(slot); } else _status.Text = $"已选择 {slot.Unit.Name}；请选择敌方目标"; RefreshSelection();
	}
	private async void EnemyChosen(UnitSlot slot) { if (_battle.IsFinished) return; if (slot.Unit?.Alive != true) return; if (_pendingCard?.Definition.target_kind is CardDefinition.TargetKind.Enemy or CardDefinition.TargetKind.AllyEnemyPair) { if (!slot.Unit.CardTargetable) { _status.Text = "该召唤物不能成为锦囊目标"; return; } if (_pendingCard.Definition.target_kind == CardDefinition.TargetKind.AllyEnemyPair && (_allyIndex < 0 || _allies[_allyIndex].Unit?.Alive != true)) { _status.Text = "请先选择我方英雄"; return; } if (_enemyIndex == slot.SlotIndex) await UseEnemyCard(slot); else PreviewEnemyCard(slot); return; } if (_allyIndex < 0 || _allies[_allyIndex].Unit == null) { _status.Text = "请先选择我方英雄"; return; } if (_enemyIndex == slot.SlotIndex) { await ConfirmAttack(); return; } _enemyIndex = slot.SlotIndex; UpdatePreview(); RefreshSelection(); }
	private void UpdatePreview() { var a = _allies[_allyIndex].Unit!; var d = _enemies[_enemyIndex].Unit!; var raw = BattleRules.CalculateRetaliation(a, d); var counter = raw; if (a.Id == "hero_role_2" && a.SkillTurns > 0) counter = Mathf.RoundToInt(counter * (a.Star >= 5 ? 1.2f : 1.4f)); var final = PreviewDamage(a, counter); var damage = BattleRules.CalculateAttackValue(a, d); _allies[_allyIndex].SetActionPreview($"HP {a.Hp} → {Math.Max(0, a.Hp - final)}（反伤）"); _enemies[_enemyIndex].SetActionPreview($"HP {d.Hp} → {Math.Max(0, d.Hp - damage)}（受击）"); _status.Text = $"{BattleRules.GetRelation(a.Type, d.Type)}：伤害 {damage}，反伤 {raw}→{final}；再次点击目标结算"; }
	public async Task ConfirmAttack() { if (_battle.IsFinished) return; if (_allyIndex < 0 || _enemyIndex < 0 || _ap <= 0) return; var a = _allies[_allyIndex].Unit!; if (!a.CanAttack || a.HasAttackedThisTurn) { _status.Text = $"{a.Name} 本回合已经攻击过或不能攻击"; return; } var d = _enemies[_enemyIndex].Unit!; var counter = d.CanRetaliate ? BattleRules.CalculateRetaliation(a, d) : 0; var damage = BattleRules.CalculateAttackValue(a, d); await AnimateAttack(_allies[_allyIndex], _enemies[_enemyIndex]); if (await TriggerPassive(_enemies, _aiDeck, "BEFORE_DAMAGE")) damage = 0; d.Hp = Math.Max(0, d.Hp - damage); if (a.Id == "hero_role_2" && a.SkillTurns > 0) counter = Mathf.RoundToInt(counter * (a.Star >= 5 ? 1.2f : 1.4f)); if (await TriggerPassive(_allies, _deck, "BEFORE_DAMAGE", new PassiveEventContext { EventKey = "BEFORE_DAMAGE", AttackTarget = a, AttackTargetSlot = _allyIndex })) counter = 0; ApplyDamageToAlly(a, counter); GainExp(a, 2); if (a.ExtraAttacksRemaining > 0) a.ExtraAttacksRemaining--; else a.HasAttackedThisTurn = true; _ap--;
		// 攻击消耗 AP 后检查 ACTION_POINTS_ZERO
		if (_ap == 0) await CheckActionPointsZero("player");
		MarkDefeated(); AddLog($"[color=ffcc66]攻击[/color] {a.Name} → {d.Name}：伤害 {damage}，反伤 {counter}，{BattleRules.GetRelation(a.Type, d.Type)}，获得 2 EXP。"); CancelSelection(false); if (_battle.IsFinished) return; _status.Text = "攻击结算完成"; RefreshAll(); }
	private async void ChooseCard(CardInstance card)
	{
		if (_battle.IsFinished) return;
		if (!card.CanPlay) { _status.Text = $"「{card.Definition.display_name}」冷却剩余 {card.CooldownRemaining} 回合"; return; }
		foreach (var c in _hand.GetChildren().OfType<CardTile>()) c.ClearActionPreview(); if (card.Definition.card_kind == CardDefinition.CardKind.Passive) { if (EffectiveCost(card) > _ap) { _status.Text = "行动点不足"; return; } PlacePassive(card); return; }
		if (card.Definition.target_kind is CardDefinition.TargetKind.None or CardDefinition.TargetKind.AllEnemies or CardDefinition.TargetKind.SelectCards) { if (_pendingCard == card && _pendingCardTarget == -2) await UseNoTargetCard(card); else { _pendingCard = card; _pendingCardTarget = -2; var i = _deck.Hand.IndexOf(card); _hand.SetSelected(i); if (i >= 0 && _hand.GetChild(i) is CardTile tile) tile.SetActionPreview("结算预览", NoTargetPreview(card)); _status.Text = $"{card.Definition.display_name}效果已写入卡面；再次点击该牌确认"; } return; }
		var hasFreeSelfTarget = card.Definition.target_kind == CardDefinition.TargetKind.AllyHero && _allies.Any(slot => slot.Unit is { Alive: true, Id: "hero_role_3", Star: >= 5, FreeSelfCards: > 0 });
		if (EffectiveCost(card) > _ap && !hasFreeSelfTarget) { _status.Text = "行动点不足"; return; }
		_pendingCard = card; _pendingCardTarget = -1; _pendingHero = null; _enemyIndex = -1; _hand.SetSelected(_deck.Hand.IndexOf(card)); _status.Text = card.Definition.target_kind == CardDefinition.TargetKind.Enemy ? $"锦囊「{card.Definition.display_name}」：请选择敌方英雄预览效果" : $"锦囊「{card.Definition.display_name}」：请选择我方英雄预览效果";
	}
	private async Task UseCard(UnitSlot slot)
	{
		if (_battle.IsFinished) return;
		var card = _pendingCard!; var d = card.Definition; var u = slot.Unit!; var cost = EffectiveCost(card); if (d.id.ToString() == _freeCardId) _freeCardId = ""; if (u.Id == "hero_role_3" && u.Star >= 5 && u.FreeSelfCards > 0) { cost = 0; u.FreeSelfCards--; }
		await AnimateCard(card); if (d.logic_mode == "LUA") { var resolved = _cardResolver.Resolve(CardContext(card, u, u), out var luaError); if (luaError == "CANCELLED") { _cancelNextEnemyEffect = true; _status.Text = "拒绝生效：敌方下一张锦囊将被抵消"; return; } else if (!resolved) { _status.Text = $"Lua卡牌结算失败：{luaError}"; return; } AddLog($"[color=77ee99]Lua锦囊[/color] {d.display_name} → {u.Name}。"); } else if (_cardResolver.CanResolveBuiltin(d.handler_key)) { if (!_cardResolver.Resolve(CardContext(card, u, u), out var error)) { _status.Text = error; return; } AddLog($"[color=77ee99]内置锦囊[/color] {d.display_name} → {u.Name}。"); } else switch (d.handler_key) { case "FREE_UNANSWERED_ATTACK": var target = _enemies.Where(s => s.Unit?.Alive == true).OrderBy(_ => _battle.Random.Next()).FirstOrDefault(); if (target?.Unit != null) { target.Unit.Hp = Math.Max(0, target.Unit.Hp - BattleRules.CalculateAttackValue(u, target.Unit)); target.Refresh(); } MarkDefeated(); break; case "STAR_UP": if (u.Star >= 6) { _status.Text = "该英雄已达到6星"; return; } _pendingStarSlot = slot; _pendingStarCost = cost; N<AcceptDialog>("StarChoiceDialog").PopupCenteredRatio(.42f); return; default: ApplyGenericAlly(d, u); break; }
		// 发布 CARD_TARGETED 事件，让 AI 方被动锦囊有机会响应
		var targetedCtx = new PassiveEventContext { EventKey = "CARD_TARGETED", SubjectCard = card, SubjectOwnerId = "player" };
		await TriggerPassive(_enemies, _aiDeck, "CARD_TARGETED", targetedCtx);
		_ap -= cost; _deck.FinishPlayedCard(card); await NotifyPlayerCardResolved(card); _pendingCard = null; _pendingCardTarget = -1;
		// 打牌后检查 HAND_EMPTY
		if (_deck.Hand.Count == 0)
			await TriggerPassive(_allies, _deck, "HAND_EMPTY", new PassiveEventContext { EventKey = "HAND_EMPTY", SubjectOwnerId = "player" });
		// 打牌消耗 AP 后检查 ACTION_POINTS_ZERO
		if (_ap == 0) await CheckActionPointsZero("player");
		if (_battle.IsFinished) return; _status.Text = "锦囊已结算并进入弃牌堆"; RefreshAll();
	}
	private void PreviewCard(UnitSlot slot) { var d = _pendingCard!.Definition; var u = slot.Unit!; var effect = d.handler_key switch { "HEAL_CLEANSE" => $"HP {u.Hp} → {Math.Min(u.MaxHp, u.Hp + Param(d, "heal", 20))}", "STAR_UP" => $"星级 {u.Star} → {Math.Min(6, u.Star + 1)}", "APPLY_SHIELD" => $"护盾 0% → {(u.Star >= Param(d, "star_required", 4) ? 50 : 20)}%", "FREE_UNANSWERED_ATTACK" => $"免费攻击，攻击 {u.Attack}，不受反伤", _ => "按卡面规则结算" }; _pendingCardTarget = slot.SlotIndex; _allyIndex = slot.SlotIndex; var i = _deck.Hand.IndexOf(_pendingCard); if (i >= 0 && _hand.GetChild(i) is CardTile tile) tile.SetActionPreview(u.Name, effect); slot.SetActionPreview(effect); _status.Text = d.target_kind == CardDefinition.TargetKind.AllyEnemyPair ? "已选择我方英雄；请选择敌方英雄预览" : "效果已预览，再次点击同一英雄确认使用"; RefreshSelection(); }
	private void PreviewEnemyCard(UnitSlot slot)
	{
		var card = _pendingCard!; var unit = slot.Unit!; _enemyIndex = slot.SlotIndex;
		var effect = card.Definition.handler_key switch { "DAMAGE_STAR_ALL" => $"HP {unit.Hp} → {Math.Max(0, unit.Hp - Param(card.Definition, "damage", 15))}", "LINK_RESONANCE" when _allyIndex >= 0 => $"HP {unit.Hp} → {Math.Max(0, unit.Hp - _allies[_allyIndex].Unit!.Attack)}", "APPLY_DAMAGE_HEAL_AMPLIFY" => "本回合受到的伤害与回复提高10%", _ => "按卡面规则结算" };
		var i = _deck.Hand.IndexOf(card); if (i >= 0 && _hand.GetChild(i) is CardTile tile) tile.SetActionPreview(unit.Name, effect); slot.SetActionPreview(effect); _status.Text = "效果已预览，再次点击同一敌方英雄确认使用"; RefreshSelection();
	}
	private async Task UseEnemyCard(UnitSlot slot)
	{
		if (_battle.IsFinished) return;
		var card = _pendingCard!; var definition = card.Definition; var unit = slot.Unit!; var cost = EffectiveCost(card); await AnimateCard(card);
		if (definition.logic_mode == "LUA") { var resolved = _cardResolver.Resolve(CardContext(card, null, unit), out var luaError); if (luaError == "CANCELLED") { _cancelNextEnemyEffect = false; _status.Text = "敌方锦囊被抵消"; return; } else if (!resolved) { _status.Text = luaError; return; } }
		else if (_cardResolver.CanResolveBuiltin(definition.handler_key)) { if (!_cardResolver.Resolve(CardContext(card, null, unit), out var error)) { _status.Text = error; return; } }
		else switch (definition.handler_key)
			{
				case "DAMAGE_STAR_ALL": unit.Hp = Math.Max(0, unit.Hp - Param(definition, "damage", 15)); break;
				case "LINK_RESONANCE" when _allyIndex >= 0: unit.Hp = Math.Max(0, unit.Hp - _allies[_allyIndex].Unit!.Attack); break;
				case "APPLY_DAMAGE_HEAL_AMPLIFY": unit.DamageTakenMultiplier = 1.1f; break;
				default: unit.Hp = Math.Max(0, unit.Hp - Param(definition, "damage", 0)); break;
			}
		_ap -= cost; _deck.FinishPlayedCard(card); await NotifyPlayerCardResolved(card); AddLog($"[color=dd99ff]锦囊[/color] 「{definition.display_name}」对 {unit.Name} 生效。");
		// 发布 CARD_TARGETED 事件，让玩家方被动锦囊有机会响应
		var enemyTargetedCtx = new PassiveEventContext { EventKey = "CARD_TARGETED", SubjectCard = card, SubjectOwnerId = "ai" };
		await TriggerPassive(_allies, _deck, "CARD_TARGETED", enemyTargetedCtx);
		CancelSelection(false); MarkDefeated(); RefreshAll();
	}
	private async Task UseNoTargetCard(CardInstance card)
	{
		if (_battle.IsFinished) return;
		if (EffectiveCost(card) > _ap) { _status.Text = "行动点不足"; return; }
		if (card.Definition.handler_key == "STEAL_TEMPORARY" && card.Definition.logic_mode == "BUILTIN") { await UseStealCard(card, false); return; }
		await AnimateCard(card); var definition = card.Definition;
		if (definition.logic_mode == "LUA")
		{
			var resolved = _cardResolver.Resolve(CardContext(card), out var luaError);
			if (luaError == "CANCELLED")
			{
				// 拒绝生效：取消敌方下一张锦囊。卡牌本身正常打出并弃置。
				_cancelNextEnemyEffect = true;
				_ap -= EffectiveCost(card);
				_deck.FinishPlayedCard(card);
				await NotifyPlayerCardResolved(card);
				AddLog($"[color=77ee99]Lua锦囊[/color] 「{definition.display_name}」触发拒绝生效：敌方下一张锦囊将被抵消。");
				_status.Text = "拒绝生效：敌方下一张锦囊将被抵消";
				CancelSelection(false); RefreshAll(); return;
			}
			else if (!resolved)
			{
				_status.Text = $"Lua卡牌结算失败：{luaError}";
				AddLog($"[color=ff6666]Lua错误[/color] {luaError}");
				CancelSelection(false); RefreshAll(); return;
			}
			if (definition.handler_key == "PREPAY_AND_DISCARD") _ap = _battle.PlayerActionPoints; else _ap -= EffectiveCost(card); _deck.FinishPlayedCard(card); await NotifyPlayerCardResolved(card); AddLog($"[color=dd99ff]Lua锦囊[/color] 「{definition.display_name}」已在沙盒中结算。");
			if (definition.handler_key == "CANCEL_NEXT_ACTIVE") _cancelNextEnemyEffect = true;
			if (definition.cost_mode == "VARIABLE_AP" || definition.handler_key == "DISCARD_DRAW_AP") _ap = _battle.PlayerActionPoints;
			// 打牌后检查 HAND_EMPTY
			if (_deck.Hand.Count == 0) await TriggerPassive(_allies, _deck, "HAND_EMPTY", new PassiveEventContext { EventKey = "HAND_EMPTY", SubjectOwnerId = "player" });
			// 打牌消耗 AP 后检查 ACTION_POINTS_ZERO
			if (_ap == 0) await CheckActionPointsZero("player");
			CancelSelection(false); MarkDefeated(); RefreshAll(); return;
		}
		if (_cardResolver.CanResolveBuiltin(definition.handler_key))
		{
			if (!_cardResolver.Resolve(CardContext(card), out var builtinError)) { _status.Text = builtinError; return; }
			_ap -= EffectiveCost(card); _deck.FinishPlayedCard(card); await NotifyPlayerCardResolved(card); AddLog($"[color=dd99ff]内置锦囊[/color] 「{definition.display_name}」已结算。");
			// 打牌后检查 HAND_EMPTY
			if (_deck.Hand.Count == 0) await TriggerPassive(_allies, _deck, "HAND_EMPTY", new PassiveEventContext { EventKey = "HAND_EMPTY", SubjectOwnerId = "player" });
			// 打牌消耗 AP 后检查 ACTION_POINTS_ZERO
			if (_ap == 0) await CheckActionPointsZero("player");
			CancelSelection(false); RefreshAll(); return;
		}
		switch (definition.handler_key)
		{
			case "CANCEL_PENDING_EFFECT": _cancelNextEnemyEffect = true; break;
			case "ZERO_HAND_COSTS": foreach (var held in _deck.Hand.Where(c => c != card)) held.RuntimeCostModifier = -held.Definition.action_cost; break;
			case "PREPAY_AND_DISCARD": foreach (var held in _deck.Hand.Where(c => c != card).ToList()) _deck.Discard(held); break;
			case "APPLY_GRUDGE": foreach (var enemy in _enemies.Where(s => s.Unit?.Alive == true)) enemy.Unit!.GrudgeStacks += Param(definition, "stacks", 5); break;
			case "APPLY_CEASEFIRE": foreach (var enemy in _enemies.Where(s => s.Unit?.Alive == true)) enemy.Unit!.CeasefireTurns = Param(definition, "silence_turns", 2); break;
			case "FORCE_MUTUAL_ATTACK": ForceEnemyMutualAttack(); break;
			case "RANDOM_CROSS_ATTACK": RandomCrossAttack(); break;
		}
		_ap -= EffectiveCost(card); _deck.FinishPlayedCard(card); await NotifyPlayerCardResolved(card); AddLog($"[color=dd99ff]锦囊[/color] 「{definition.display_name}」已结算。");
		// 打牌后检查 HAND_EMPTY
		if (_deck.Hand.Count == 0) await TriggerPassive(_allies, _deck, "HAND_EMPTY", new PassiveEventContext { EventKey = "HAND_EMPTY", SubjectOwnerId = "player" });
		// 打牌消耗 AP 后检查 ACTION_POINTS_ZERO
		if (_ap == 0) await CheckActionPointsZero("player");
		CancelSelection(false); if (_battle.IsFinished) return; RefreshAll();
	}
	private string NoTargetPreview(CardInstance card) => card.Definition.handler_key switch { "STEAL_TEMPORARY" => $"敌方手牌 {_aiDeck.Hand.Count} → {Math.Max(0, _aiDeck.Hand.Count - 1)}；我方 {_deck.Hand.Count} → {_deck.Hand.Count + 1}", "ZERO_HAND_COSTS" => $"其余 {_deck.Hand.Count - 1} 张手牌费用变为0", "APPLY_GRUDGE" => "所有敌方英雄获得5层怨恨", "APPLY_CEASEFIRE" => "所有敌方英雄2回合无法发动技能", _ => "按卡面规则结算" };
	private Task<bool> NotifyPlayerCardResolved(CardInstance card) => TriggerPassive(_enemies, _aiDeck, "AFTER_CARD_RESOLVE", new PassiveEventContext { EventKey = "AFTER_CARD_RESOLVE", SubjectCard = card, SubjectOwnerId = "player" });
	private void ForceEnemyMutualAttack() { var living = _enemies.Where(s => s.Unit?.Alive == true).OrderBy(_ => _battle.Random.Next()).Take(2).ToList(); if (living.Count < 2) return; var first = living[0].Unit!; var second = living[1].Unit!; var firstAttack = first.Attack; var secondAttack = second.Attack; first.Hp = Math.Max(0, first.Hp - secondAttack); second.Hp = Math.Max(0, second.Hp - firstAttack); MarkDefeated(); }
	private void RandomCrossAttack() { var allies = _allies.Where(s => s.Unit?.Alive == true).ToList(); var enemies = _enemies.Where(s => s.Unit?.Alive == true).ToList(); if (allies.Count == 0 || enemies.Count == 0) return; var ally = allies[_battle.Random.Next(allies.Count)].Unit!; var enemy = enemies[_battle.Random.Next(enemies.Count)].Unit!; enemy.Hp = Math.Max(0, enemy.Hp - ally.Attack); MarkDefeated(); }
	private static void ApplyGenericAlly(CardDefinition definition, UnitState unit) { if (definition.effect_params.TryGetValue("attack", out Variant value)) unit.Attack += value.AsInt32(); }
	private static int Param(CardDefinition definition, string key, int fallback) => definition.effect_params.TryGetValue(key, out Variant value) && value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
	private void PlacePassive(CardInstance card)
	{
		if (_battle.IsFinished) return;
		var gateIndex = _battle.NextPassiveGateIndex("player");
		if (gateIndex < 0) { _status.Text = $"战门最多放置 {BattleState.PassiveGateCapacity} 张伏牌"; return; }
		if (!_battle.TryPlacePassive("player", gateIndex, card, out var error)) { _status.Text = error; return; }
		if (!_deck.SetPassive(card)) { _battle.RemovePassive(card); _status.Text = "被动锦囊盖放失败"; return; }
		if (card.Definition.trigger_keys.Contains("ON_PLACED")) { _battle.CurrentPassiveEvent = new PassiveEventContext { EventKey = "ON_PLACED", SubjectCard = card, SubjectOwnerId = "player", SubjectSlotIndex = gateIndex }; var exec = CardContext(card); _cardResolver.Resolve(exec, out _); }
		_ap -= EffectiveCost(card);
		if (card.Definition.id.ToString() == _freeCardId) _freeCardId = "";
		AddLog($"[color=99bbff]战门[/color] 玩家背面设置了被动锦囊（第 {gateIndex + 1} 张）。");
		_pendingCard = null; _pendingCardTarget = -1; _hand.SetSelected(-1); 
		_status.Text = "被动锦囊已背面设置到战门";
		RefreshAll();
	}
	private void GainExp(UnitState u, int n) { u.Exp += n; AddLog($"[color=aaddff]经验[/color] {u.Name} 当前 EXP {u.Exp}；升星仅由升星牌触发。"); }
	private async Task CheckActionPointsZero(string ownerId)
	{
		var (slots, deck) = ownerId == "player" ? (_enemies, _aiDeck) : (_allies, _deck);
		await TriggerPassive(slots, deck, "ACTION_POINTS_ZERO", new PassiveEventContext { EventKey = "ACTION_POINTS_ZERO", SubjectOwnerId = ownerId });
	}
	public void DrawOne() { if (_battle.IsFinished) return; if (_deck.Hand.Count >= DeckState.HandLimit) { _status.Text = $"手牌已满（上限 {DeckState.HandLimit} 张）"; return; } var result = _deck.Draw(); if (result.Count > 0) AudioManager.Instance?.PlaySfx(GameSfx.DrawCard); _status.Text = result.Count == 0 ? "没有可抽的牌" : $"抽到「{result[0].Definition.display_name}」"; RefreshAll(); }
	private async Task EndTurn() { if (_battle.IsFinished) return; await TriggerPassive(_allies, _deck, "ALLY_TURN_ENDED"); TickGrudge(_allies); var discardProtected = _battle.PreventsDiscard("player"); var discarded = _deck.DiscardRemainingHand(discardProtected); AddLog(discardProtected ? "[color=75d7ff]神器2[/color] 未使用手牌被保留。" : $"[color=75d7ff]回合整理[/color] 未使用的 {discarded} 张手牌进入弃牌堆。"); await EnemyPhase(); if (_battle.IsFinished) return; _battle.AdvanceTurn(); _turn = _battle.Turn; _deck.TickCooldowns(); _aiDeck.TickCooldowns(); _playerDeployedThisTurn = false; foreach (var slot in _allies.Where(slot => slot.Unit != null)) { slot.Unit!.HasAttackedThisTurn = false; slot.Unit.ExtraAttacksRemaining = 0; } _ap = _battle.PlayerZeroNextTurnActionPoints ? 0 : (_battle.PlayerNextTurnActionPointsOverride ?? BattleState.DefaultActionPoints) + _battle.PlayerNextTurnBonus; _battle.PlayerZeroNextTurnActionPoints = false; _battle.PlayerNextTurnActionPointsOverride = null; _battle.PlayerNextTurnBonus = 0; _battle.PlayerActionPoints = _ap; await TriggerPassive(_allies, _deck, "ALLY_TURN_STARTED"); await TriggerPassive(_allies, _deck, "NEXT_ALLY_TURN_STARTED"); await TriggerPassive(_allies, _deck, "ALLY_BATTLE_PHASE_STARTED"); TickStatuses(); foreach (var s in _allies.Where(s => s.Unit is { Alive: true, Id: "hero_role_3" })) { _ap++; if (s.Unit!.Star >= 5) s.Unit.FreeSelfCards = 2; } _battle.PlayerActionPoints = _ap;
		// 检查 AP 是否为 0，触发 ACTION_POINTS_ZERO 事件
		if (_ap == 0) await CheckActionPointsZero("player"); AssignFreeCard(); {
			// 发布 BEFORE_DRAW 事件，让对方被动锦囊有机会阻止
			var beforeDrawCtx = new PassiveEventContext { EventKey = "BEFORE_DRAW", SubjectOwnerId = "player" };
			_battle.CurrentPassiveEvent = beforeDrawCtx;
			_battle.InvalidatedPassives.Clear();
			var blockingDraw = _passiveResolver.Collect(_battle, "ai", "BEFORE_DRAW", beforeDrawCtx);
			if (blockingDraw.Count > 0)
			{
				// 有 CANCEL_DRAW 被动触发，阻止抽牌
				AddLog($"[color=ff99aa]被动锦囊[/color] 抽牌被阻止。");
				foreach (var placed in blockingDraw)
				{
					var card = placed.Card;
					card.FaceUp = true;
					_aiDeck.DiscardPlaced(card);
					AddLog($"[color=ff99aa]被动锦囊[/color] 「{card.Definition.display_name}」翻面并结算。");
				}
				ApplyInvalidatedPassives(); ApplyPendingSummons();
			}
			else
			{
				var drawResult = _deck.Draw(4); AddLog($"[color=75d7ff]回合抽牌[/color] 抽到 {drawResult.Count} 张。");
				// 发布 AFTER_DRAW 和 HAND_EMPTY 事件
				await TriggerPassive(_enemies, _aiDeck, "AFTER_DRAW", new PassiveEventContext { EventKey = "AFTER_DRAW", SubjectOwnerId = "player" });
				if (_deck.Hand.Count == 0)
					await TriggerPassive(_enemies, _aiDeck, "HAND_EMPTY", new PassiveEventContext { EventKey = "HAND_EMPTY", SubjectOwnerId = "player" });
			}
		} N<Label>("Title").Text = $"训练场 · 第 {_turn} 回合"; AddLog($"[color=75d7ff]系统[/color] 第 {_turn} 回合开始。"); CancelSelection(false); _status.Text = "新回合：行动点恢复，自动抽牌"; RefreshAll(); }
	private void CancelSelection(bool update = true) { _pendingHero = null; _pendingCard = null; _pendingCardTarget = _allyIndex = _enemyIndex = -1; _hand.SetSelected(-1); _rightSidebar.ShowCommanderOverview(); foreach (var s in _allies.Concat(_enemies)) s.ClearActionPreview(); if (update) _status.Text = "已取消选择"; RefreshSelection(); }
	private async Task EnemyPhase()
	{
		if (_battle.IsFinished) return;
		var aiDrawBlocked = await TriggerPassive(_allies, _deck, "BEFORE_DRAW", new PassiveEventContext { EventKey = "BEFORE_DRAW", SubjectOwnerId = "ai" });
		var aiOpeningDraw = aiDrawBlocked ? [] : _aiDeck.Draw(4);
		if (!aiDrawBlocked) await TriggerPassive(_allies, _deck, "AFTER_DRAW", new PassiveEventContext { EventKey = "AFTER_DRAW", SubjectOwnerId = "ai" });
		AddLog($"[color=ff8888]AI回合[/color] 抽取 {aiOpeningDraw.Count} 张新手牌。");
		if (N<CheckButton>("DummyMode").ButtonPressed) { TickGrudge(_enemies); var protectedHand = _battle.PreventsDiscard("ai"); var unused = _aiDeck.DiscardRemainingHand(protectedHand); AddLog(protectedHand ? "[color=999999]稻草人模式[/color] 敌方跳过行动，神器2保留其手牌。" : $"[color=999999]稻草人模式[/color] 敌方跳过全部行动，{unused} 张未使用手牌进入弃牌堆。"); return; }
		if (await TriggerPassive(_allies, _deck, "ENEMY_BATTLE_PHASE_STARTED")) { AddLog("[color=99bbff]被动锦囊[/color] 敌方战斗阶段被跳过。"); return; }
		_aiDeployedThisTurn = false; foreach (var slot in _enemies.Where(slot => slot.Unit != null)) slot.Unit!.HasAttackedThisTurn = false; var aiAp = _battle.EnemyZeroNextTurnActionPoints ? 0 : (_battle.EnemyNextTurnActionPointsOverride ?? BattleState.DefaultActionPoints) + _battle.EnemyNextTurnBonus; _battle.EnemyZeroNextTurnActionPoints = false; _battle.EnemyNextTurnActionPointsOverride = null; _battle.EnemyNextTurnBonus = 0; _battle.EnemyActionPoints = aiAp; if (_turn <= 4 && _aiHeroBag.Count > 0) await AiDeploy(); var attacks = _turn <= 4 ? 1 : 2;
		// 检查 AI AP 是否为 0，触发 ACTION_POINTS_ZERO 事件
		if (aiAp == 0) await CheckActionPointsZero("ai");
		for (var i = 0; i < attacks && aiAp > 0 && !_battle.IsFinished; i++) if (await AiAttack()) aiAp--; if (aiAp > 0 && !_battle.IsFinished && await AiUseCard()) aiAp--; if (_battle.IsFinished) return;
		TickGrudge(_enemies); var aiProtected = _battle.PreventsDiscard("ai"); var aiUnused = _aiDeck.DiscardRemainingHand(aiProtected);
		AddLog(aiProtected ? $"[color=ff8888]AI回合[/color] 敌方行动结束，剩余行动点 {aiAp}；神器2保留其手牌。" : $"[color=ff8888]AI回合[/color] 敌方行动结束，剩余行动点 {aiAp}；{aiUnused} 张未使用手牌进入弃牌堆。");
	}
	private async Task<int> AiDeploy() { if (_battle.IsFinished || _aiDeployedThisTurn) return 0; var empty = _enemies.Where(s => s.Unit?.Alive != true).ToList(); if (empty.Count == 0 || _aiHeroBag.Count == 0) return 0; var hero = _aiHeroBag[_battle.Random.Next(_aiHeroBag.Count)]; var slot = empty[_battle.Random.Next(empty.Count)]; slot.SetUnit(hero.Deploy()); _battle.SetSlotUnit("ai", slot.SlotIndex, slot.Unit); _aiHeroBag.Remove(hero); _battle.DecrementReserveHero("ai"); _aiDeployedThisTurn = true; await slot.PlayDeployAnimation(); AddLog($"[color=ff8888]AI免费部署[/color] {hero.Definition.display_name} 进入敌方 {slot.SlotIndex + 1} 号位。"); return 0; }
	private async Task<bool> AiAttack()
	{
		if (_battle.IsFinished) return false;
		var pairs = (from e in _enemies where e.Unit is { Alive: true, HasAttackedThisTurn: false } from a in _allies where a.Unit?.Alive == true select (E: e, A: a)).ToList(); if (pairs.Count == 0) return false; var advantage = pairs.Where(p => BattleRules.GetRelation(p.E.Unit!.Type, p.A.Unit!.Type) == "克制").ToList(); var pool = advantage.Count > 0 ? advantage : pairs; var chosen = pool[_battle.Random.Next(pool.Count)]; var attacker = chosen.E.Unit!; var target = chosen.A.Unit!; var damage = BattleRules.CalculateAttackValue(attacker, target); var counter = BattleRules.CalculateRetaliation(attacker, target);
		var attackCtx = new PassiveEventContext { EventKey = "BEFORE_ATTACK", AttackTarget = target, AttackTargetSlot = chosen.A.SlotIndex, AliveAllySlots = _allies.Where(s => s.Unit?.Alive == true).Select(s => s.SlotIndex).ToArray() };
		await TriggerPassive(_allies, _deck, "BEFORE_ATTACK", attackCtx);
		if (attackCtx.RedirectSlot >= 0 && attackCtx.RedirectSlot < _allies.Count && _allies[attackCtx.RedirectSlot].Unit?.Alive == true) { chosen = (chosen.E, _allies[attackCtx.RedirectSlot]); target = chosen.A.Unit!; }
		await AnimateAttack(chosen.E, chosen.A); if (await TriggerPassive(_allies, _deck, "BEFORE_DAMAGE")) damage = 0; ApplyDamageToAlly(target, damage); attacker.Hp = Math.Max(0, attacker.Hp - counter); attacker.HasAttackedThisTurn = true; MarkDefeated(); AddLog($"[color=ff8888]AI攻击[/color] {attacker.Name} → {target.Name}：{BattleRules.GetRelation(attacker.Type, target.Type)}，伤害{damage}，受到反伤{counter}。"); chosen.E.Refresh(); chosen.A.Refresh(); return true;
	}
	private async Task<bool> AiUseCard()
	{
		if (_battle.IsFinished) return false;
		var playable = _aiDeck.Hand.ToList(); if (playable.Count == 0) return false; var card = playable[_battle.Random.Next(playable.Count)];
		if (card.Definition.card_kind == CardDefinition.CardKind.Passive) { var gateIndex = _battle.NextPassiveGateIndex("ai"); if (gateIndex < 0) return false;
		if (!_battle.TryPlacePassive("ai", gateIndex, card)) return false;
		if (!_aiDeck.SetPassive(card)) { _battle.RemovePassive(card); return false; }
		if (card.Definition.trigger_keys.Contains("ON_PLACED")) { _battle.CurrentPassiveEvent = new PassiveEventContext { EventKey = "ON_PLACED", SubjectCard = card, SubjectOwnerId = "ai", SubjectSlotIndex = gateIndex }; _cardResolver.Resolve(CardContext(card, ai: true), out _); }
		AddLog($"[color=ff99aa]AI战门[/color] 敌方背面设置了1张被动锦囊。"); await TriggerPassive(_allies, _deck, "PASSIVE_SET", new PassiveEventContext { EventKey = "PASSIVE_SET", SubjectCard = card, SubjectSlotIndex = gateIndex, SubjectOwnerId = "ai" }); RefreshEnemyHand(); return true; }
		if (_cancelNextEnemyEffect) { _cancelNextEnemyEffect = false; await Announce($"拒绝生效：敌方「{card.Definition.display_name}」被抵消"); _aiDeck.FinishPlayedCard(card); return true; }
		if (card.Definition.logic_mode == "LUA") { var own = _enemies.Where(s => s.Unit?.Alive == true).ToList(); var opposing = _allies.Where(s => s.Unit?.Alive == true).ToList(); UnitState? source = null, target = null; if (card.Definition.target_kind == CardDefinition.TargetKind.AllyHero && own.Count > 0) source = target = own[_battle.Random.Next(own.Count)].Unit; else if (card.Definition.target_kind is CardDefinition.TargetKind.Enemy or CardDefinition.TargetKind.AllyEnemyPair && opposing.Count > 0) { target = opposing[_battle.Random.Next(opposing.Count)].Unit; if (own.Count > 0) source = own[_battle.Random.Next(own.Count)].Unit; } await AnimateCard(card); if (!_cardResolver.Resolve(CardContext(card, source, target, true), out var luaError)) { AddLog($"[color=ff6666]AI Lua错误[/color] {luaError}"); return false; } _aiDeck.FinishPlayedCard(card); await TriggerPassive(_allies, _deck, "AFTER_CARD_RESOLVE", new PassiveEventContext { EventKey = "AFTER_CARD_RESOLVE", SubjectCard = card, SubjectOwnerId = "ai" }); AddLog($"[color=dd99ff]AI Lua锦囊[/color] 「{card.Definition.display_name}」已结算。"); return true; }
		if (card.Definition.builtin_effect == CardDefinition.BuiltinEffect.StealCard) { await UseStealCard(card, true); return true; }
		var slot = AiCardTarget(card.Definition); if (slot?.Unit == null) { await AnimateCard(card); _aiDeck.FinishPlayedCard(card); AddLog($"[color=dd99ff]AI锦囊[/color] 「{card.Definition.display_name}」已结算。"); return true; }
		await AnimateCard(card); var u = slot.Unit; switch (card.Definition.builtin_effect) { case CardDefinition.BuiltinEffect.Heal: u.Hp = Math.Min(u.MaxHp, u.Hp + card.Definition.effect_amount); break; case CardDefinition.BuiltinEffect.AddAttack: u.Attack += card.Definition.effect_amount; break; case CardDefinition.BuiltinEffect.AddExp: u.Exp += card.Definition.effect_amount; break; case CardDefinition.BuiltinEffect.StarUp: AiStarUp(u); break; default: ApplyGenericAlly(card.Definition, u); break; }
		_aiDeck.FinishPlayedCard(card); await TriggerPassive(_allies, _deck, "AFTER_CARD_RESOLVE", new PassiveEventContext { EventKey = "AFTER_CARD_RESOLVE", SubjectCard = card, SubjectOwnerId = "ai" }); slot.Refresh(); AddLog($"[color=dd99ff]AI锦囊[/color] 「{card.Definition.display_name}」对 {u.Name} 生效。"); return true;
	}
	private async Task UseStealCard(CardInstance card, bool byAi)
	{
		await Announce($"{(byAi ? "AI" : "玩家")}打出「拿来主义」"); var source = byAi ? _deck : _aiDeck; var destination = byAi ? _aiDeck : _deck;
		var counterCard = FindCounterCard(byAi); if (counterCard != null) { await TriggerCounter(counterCard, byAi); await Announce("「我觉得不行」发动：拿来主义被抵消"); (byAi ? _aiDeck : _deck).Discard(card); if (!byAi) _ap -= card.CurrentCost(); _pendingCard = null; _pendingCardTarget = -1; CallDeferred(MethodName.RefreshAll); return; }
		await AnimateCard(card); if (source.Hand.Count > 0) { var stolen = source.Hand[_battle.Random.Next(source.Hand.Count)]; source.Hand.Remove(stolen); destination.Hand.Add(stolen); stolen.OwnerId = byAi ? "ai" : "player"; stolen.FaceUp = !byAi; stolen.Zone = CardInstance.ZoneKind.Hand; stolen.ReturnToOriginalOwnerDiscardAtTurnEnd = true; AddLog($"[color=dd99ff]{(byAi ? "AI锦囊" : "锦囊")}[/color] 「拿来主义」获得了对方一张手牌。"); }
		(byAi ? _aiDeck : _deck).Discard(card); if (!byAi) _ap -= card.CurrentCost(); _pendingCard = null; _pendingCardTarget = -1; _hand.SetSelected(-1); CallDeferred(MethodName.RefreshAll);
	}
	private CardInstance? FindCounterCard(bool attackerAi) => _battle.Passives.FirstOrDefault(placed => placed.OwnerId == (attackerAi ? "player" : "ai") && placed.Card.Definition.builtin_effect == CardDefinition.BuiltinEffect.CancelEnemyDraw)?.Card;
	private async Task<bool> TriggerPassive(IEnumerable<UnitSlot> slots, DeckState ownerDeck, string eventKey, PassiveEventContext? context = null)
	{
		var ownerId = ownerDeck.OwnerId; _battle.CurrentPassiveEvent = context ?? new PassiveEventContext { EventKey = eventKey }; _battle.InvalidatedPassives.Clear();
		var triggered = _passiveResolver.Collect(_battle, ownerId, eventKey, _battle.CurrentPassiveEvent); var cancelled = false;
		foreach (var placed in triggered) { var card = placed.Card; card.FaceUp = true; await AnimateCard(card); var exec = CardContext(card, ai: ownerId == "ai"); if (card.Definition.logic_mode == "LUA" && !_cardResolver.Resolve(exec, out var luaError)) AddLog($"[color=ff6666]被动Lua错误[/color] {luaError}"); if (!_battle.Passives.Any(item => item.Card == card)) { if (card.Definition.effect_params.TryGetValue("post_zone", out Variant postZone) && postZone.AsString() == "EXILE") ownerDeck.Exile(card); else { ApplyLeaveCooldown(card); ownerDeck.DiscardPlaced(card); } } await Announce($"被动锦囊「{card.Definition.display_name}」发动"); AddLog($"[color=ff99aa]被动锦囊[/color] 「{card.Definition.display_name}」从战门翻面并结算。"); cancelled |= PassiveTriggerResolver.CancelsEvent(card.Definition, exec.Cancelled); }
		if (eventKey == "HAND_EMPTY" && ownerDeck.Hand.Count == 0)
		{
			var emergency = ownerDeck.DrawPile.Concat(ownerDeck.Hand).Concat(ownerDeck.DiscardPile).FirstOrDefault(card => card.Definition.handler_key == "EMERGENCY_DRAW" && !card.EmergencyUsed && card.CooldownRemaining <= 0);
			if (emergency != null)
			{
				ownerDeck.DrawPile.Remove(emergency); ownerDeck.Hand.Remove(emergency); ownerDeck.DiscardPile.Remove(emergency);
				emergency.EmergencyUsed = true; emergency.FaceUp = true; emergency.Zone = CardInstance.ZoneKind.Discard;
				var exec = CardContext(emergency, ai: ownerId == "ai");
				if (_cardResolver.Resolve(exec, out var emergencyError)) { emergency.CooldownRemaining = Param(emergency.Definition, "emergency_cooldown", 3); ownerDeck.DiscardPile.Add(emergency); await Announce($"急救「{emergency.Definition.display_name}」自动发动"); AddLog($"[color=ff99aa]急救[/color] 手牌为0，自动抽取5张牌。"); }
				else AddLog($"[color=ff6666]急救Lua错误[/color] {emergencyError}");
			}
		}
		ApplyInvalidatedPassives(); ApplyPendingSummons(); return cancelled;
	}
	private async Task CheckEnemyEmptySlots()
	{
		foreach (var slot in _enemies.Where(s => s.Unit?.Alive != true))
			await TriggerPassive(_allies, _deck, "ENEMY_SLOT_EMPTY", new PassiveEventContext { EventKey = "ENEMY_SLOT_EMPTY", SubjectSlotIndex = slot.SlotIndex, SubjectOwnerId = "ai" });
		ApplyPendingSummons();
	}
	private void ApplyPendingSummons()
	{
		foreach (var (ownerId, slotIndex, unit) in _battle.PendingSummons.ToList())
		{
			var row = ownerId == "player" ? _allies : _enemies;
			if (slotIndex >= 0 && slotIndex < row.Count && row[slotIndex].Unit?.Alive != true) { row[slotIndex].SetUnit(unit); AddLog($"[color=ff99aa]召唤[/color] {unit.Name} 出现在{(ownerId == "player" ? "我方" : "敌方")} {slotIndex + 1} 号位。"); }
		}
		_battle.PendingSummons.Clear(); SynchronizeBattleState();
	}
	private void ApplyInvalidatedPassives()
	{
		foreach (var (ownerId, slotIndex, card) in _battle.InvalidatedPassives) { ApplyLeaveCooldown(card); (ownerId == "player" ? _deck : _aiDeck).DiscardPlaced(card); AddLog($"[color=ff99aa]反制[/color] 敌方战门中的被动锦囊「{card.Definition.display_name}」被揭穿并失效。"); }
		_battle.InvalidatedPassives.Clear();
	}
	private static void ApplyLeaveCooldown(CardInstance card) { if (card.Definition.effect_params.TryGetValue("cooldown_on_leave", out Variant value) && value.VariantType == Variant.Type.Int) card.CooldownRemaining = value.AsInt32(); }
	private async Task TriggerCounter(CardInstance card, bool attackerAi) { _battle.RemovePassive(card); card.FaceUp = true; await AnimateCard(card); (attackerAi ? _deck : _aiDeck).DiscardPlaced(card); AddLog($"[color=ff99aa]被动锦囊[/color] {(attackerAi ? "玩家" : "AI")}的「我觉得不行」从战门翻开并阻止了拿来主义。"); }
	private async Task AnimateAttack(UnitSlot a, UnitSlot d) { _status.Text = $"{a.Unit!.Name} 锁定 {d.Unit!.Name}，准备结算……"; AudioManager.Instance?.PlayAttackFor(a.Unit.Type); await a.PlayTargetFlash(new(1.55f, 1.25f, .55f)); await d.PlayTargetFlash(new(1.55f, .7f, .7f)); }
	private async Task Announce(string text) { var label = N<Label>("Announcement"); label.Text = text; label.Visible = true; label.Modulate = new(1.35f, 1.25f, .65f, 0); var t = CreateTween(); t.TweenProperty(label, "modulate:a", 1, .12); t.TweenInterval(.5); t.TweenProperty(label, "modulate:a", 0, .18); await ToSignal(t, Tween.SignalName.Finished); label.Visible = false; }
	private async Task AnimateCard(CardInstance card) { AudioManager.Instance?.PlaySfx(GameSfx.PlayCard); var tile = _cardScene.Instantiate<CardTile>(); AddChild(tile); tile.Setup(card); tile.MouseFilter = MouseFilterEnum.Ignore; var v = GetViewportRect().Size; tile.Size = new(v.X * .12f, v.Y * .28f); tile.Position = new((v.X - tile.Size.X) / 2, v.Y * .32f); tile.Modulate = new(1.5f, 1.5f, 1.5f, 0); tile.Scale = new(.8f, .8f); tile.PivotOffset = tile.Size / 2; var t = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out); t.TweenProperty(tile, "modulate", Colors.White, .18); t.TweenProperty(tile, "scale", Vector2.One, .25); await ToSignal(t, Tween.SignalName.Finished); await ToSignal(GetTree().CreateTimer(.22), SceneTreeTimer.SignalName.Timeout); tile.QueueFree(); }
	private UnitSlot? AiCardTarget(CardDefinition card) { var living = _enemies.Where(s => s.Unit?.Alive == true).ToList(); if (living.Count == 0) return null; if (card.builtin_effect == CardDefinition.BuiltinEffect.Heal) living.Sort((a, b) => ((float)a.Unit!.Hp / a.Unit.MaxHp).CompareTo((float)b.Unit!.Hp / b.Unit.MaxHp)); else if (card.builtin_effect == CardDefinition.BuiltinEffect.AddAttack) living.Sort((a, b) => ((float)b.Unit!.Hp / b.Unit.MaxHp).CompareTo((float)a.Unit!.Hp / a.Unit.MaxHp)); else living.Sort((a, b) => b.Unit!.Exp != a.Unit!.Exp ? b.Unit.Exp.CompareTo(a.Unit.Exp) : b.Unit.Hp.CompareTo(a.Unit.Hp)); return living[0]; }
	private static void AiStarUp(UnitState u) { if (u.Star >= 6 || u.Definition is not HeroDefinition d) return; u.Star++; var i = u.Star - 1; if (u.Star is 1 or 4) u.Attack += d.star_attack_choices[i]; else if (u.Star == 6) { u.Attack += d.star_attack_choices[i]; u.MaxHp += d.star_hp_choices[i]; u.Hp += d.star_hp_choices[i]; u.Type = "无职业"; } }
	private void ApplyDamageToAlly(UnitState u, int amount) { var target = _allies.Select(s => s.Unit).FirstOrDefault(x => x is { Alive: true, TauntTurns: > 0 }) ?? u; if (target.Id == "hero_role_1") { var ratio = (float)target.Hp / target.MaxHp; if (target.Star >= 5 && ratio <= .3) amount = Mathf.RoundToInt(amount * .25f); else if (ratio <= .5) amount = Mathf.RoundToInt(amount * .5f); } target.Hp = Math.Max(0, target.Hp - amount); }
	private int PreviewDamage(UnitState u, int amount) { var target = _allies.Select(s => s.Unit).FirstOrDefault(x => x is { Alive: true, TauntTurns: > 0 }) ?? u; if (target.Id == "hero_role_1") { var ratio = (float)target.Hp / target.MaxHp; if (target.Star >= 5 && ratio <= .3) return Mathf.RoundToInt(amount * .25f); if (ratio <= .5) return Mathf.RoundToInt(amount * .5f); } return amount; }
	private async void MarkDefeated()
	{
		var newlyDead = new List<(UnitState Unit, string Side)>();
		foreach (var s in _allies)
			if (s.Unit is { Alive: false, DeathHandled: false }) { s.Unit.DeathHandled = true; newlyDead.Add((s.Unit!, "player")); }
		foreach (var s in _enemies)
			if (s.Unit is { Alive: false, DeathHandled: false }) { s.Unit.DeathHandled = true; newlyDead.Add((s.Unit!, "ai")); }
		if (newlyDead.Any(item => item.Unit.TriggersHeroDeath)) AudioManager.Instance?.PlaySfx(GameSfx.HeroDies);
		
		foreach (var s in _allies.Concat(_enemies))
			if (s.Unit is { Alive: false }) s.Refresh();
		_battle.FinalizeDeaths(
			_allies.Where(slot => slot.Unit != null).Select(slot => slot.Unit!),
			_enemies.Where(slot => slot.Unit != null).Select(slot => slot.Unit!));
		
		// 发布 HERO_DIED 事件，让对方被动锦囊有机会响应
		foreach (var (deadUnit, side) in newlyDead.Where(item => item.Unit.TriggersHeroDeath))
		{
			var ctx = new PassiveEventContext { EventKey = "HERO_DIED", SubjectCard = null, SubjectOwnerId = side };
			if (side == "player")
				await TriggerPassive(_enemies, _aiDeck, "HERO_DIED", ctx);
			else
				await TriggerPassive(_allies, _deck, "HERO_DIED", ctx);
		}
		
		SynchronizeBattleState();
		CallDeferred(MethodName.DeferredCheckEnemyEmptySlots);
	}

	private void HandleBattleEnd(BattleEventData data)
	{
		if (!_battle.IsFinished) return;
		
		string resultText = data.Amount switch
		{
			1 => "胜利！所有敌方英雄已被击败。",
			-1 => "失败...我方英雄全部阵亡。",
			_ => "平局！双方英雄同归于尽。"
		};
		if (data.Amount > 0) AudioManager.Instance?.PlayVictory();
		else if (data.Amount < 0) AudioManager.Instance?.PlayDefeat();
		else AudioManager.Instance?.StopMusic();
		
		AddLog($"[color=ffcc66]战斗结束[/color] {resultText}");
		_status.Text = resultText;
		
		DisableAllBattleControls();
	}

	private void DisableAllBattleControls()
	{
		N<Button>("HeroBag").Disabled = true;
		_turnControl.SetDisabled(true);
		N<Button>("CatalogButton").Disabled = true;
		foreach (var tile in _hand.GetChildren().OfType<CardTile>())
			tile.MouseFilter = MouseFilterEnum.Ignore;
		foreach (var slot in _allies.Concat(_enemies))
			slot.SetInteractionEnabled(false);
	}

	private void EnableAllBattleControls()
	{
		N<Button>("HeroBag").Disabled = false;
		_turnControl.SetDisabled(false);
		N<Button>("CatalogButton").Disabled = false;
		foreach (var tile in _hand.GetChildren().OfType<CardTile>())
			tile.MouseFilter = MouseFilterEnum.Stop;
		foreach (var slot in _allies.Concat(_enemies))
			slot.SetInteractionEnabled(true);
	}
	private async void DeferredCheckEnemyEmptySlots() => await CheckEnemyEmptySlots();
	private void ApplyLeaderBonus() { if (_leaderId == "hero_role_1") foreach (var s in _allies.Where(s => s.Unit != null)) { s.Unit!.MaxHp += 50; s.Unit.Hp += 50; s.Refresh(); } else if (_leaderId == "hero_role_3") AssignFreeCard(); AddLog("[color=ffee88]队长[/color] 第一名部署英雄成为队长，队长加成开始生效。"); }
	private bool LeaderIsStarTwo() => _allies.Any(s => s.Unit is { Star: >= 2 } u && u.Id == _leaderId);
	private void AssignFreeCard() { _freeCardId = ""; if (((_leaderId == "hero_role_3" && _leaderTurns > 0) || _allies.Any(s => s.Unit is { Id: "hero_role_3", Star: >= 2 })) && _deck.Hand.Count > 0) _freeCardId = _deck.Hand[_battle.Random.Next(_deck.Hand.Count)].Definition.id.ToString(); }
	private int EffectiveCost(CardInstance c) => c.Definition.id.ToString() == _freeCardId ? 0 : c.CurrentCost(_ap, BattleState.DefaultActionPoints);
	private void OpenSkillDialog()
	{
		if (_battle.IsFinished) return;
		if (_allyIndex < 0 || _allies[_allyIndex].Unit == null) { _status.Text = "请先选择我方英雄"; return; }
		var u = _allies[_allyIndex].Unit!; if (u.Cooldown > 0) { _status.Text = $"技能冷却剩余 {u.Cooldown} 回合"; return; }
		var list = N<VBoxContainer>("SkillList"); Clear(list); var d = (HeroDefinition)u.Definition; var texts = new List<string> { d.skill_1_text }; if (!string.IsNullOrEmpty(d.skill_2_text)) texts.Add(d.skill_2_text); for (var i = 0; i < texts.Count; i++) { var index = i; var b = new Button { Text = $"技能 {i + 1}\n{texts[i]}", SizeFlagsVertical = SizeFlags.ExpandFill }; b.Pressed += () => ExecuteSkill(index); list.AddChild(b); }
		N<AcceptDialog>("SkillDialog").PopupCenteredRatio(.58f);
	}
	private void ContextSkillRequested(UnitSlot slot)
	{
		if (_battle.IsFinished || slot.Unit?.Alive != true || slot.Side != "ally") return;
		_allyIndex = slot.SlotIndex;
		_enemyIndex = -1;
		RefreshSelection();
		OpenSkillDialog();
	}
	public void ExecuteSkill(int skill)
	{
		if (_battle.IsFinished) return;
		var u = _allies[_allyIndex].Unit!; var id = u.Id; if (id == "hero_role_4" && (_enemyIndex < 0 || _enemies[_enemyIndex].Unit == null)) { N<AcceptDialog>("SkillDialog").Hide(); _status.Text = "祭司技能需要先选择敌方目标"; return; }
		if (id == "hero_role_1") u.TauntTurns = u.Star >= 5 ? 3 : 2; else if (id == "hero_role_2") u.SkillTurns = 2; else if (id == "hero_role_3") { var count = Math.Min(2, _aiDeck.Hand.Count); for (var i = 0; i < count; i++) _aiDeck.Discard(_aiDeck.Hand[0]); _ap = 0; } else if (id == "hero_role_4") { var target = _enemies[_enemyIndex].Unit!; if (skill == 0) { var down = Math.Min(2, target.Attack); target.Attack -= down; target.AttackRestore += down; target.DebuffTurns = 3; } else { u.LinkedEnemy = _enemyIndex; u.LinkTurns = 2; } u.Hp = Math.Min(u.MaxHp, u.Hp + Mathf.RoundToInt(u.Hp * (u.Star >= 5 ? .1f : .05f))); }
		var d = (HeroDefinition)u.Definition; u.Cooldown = Math.Max(0, d.skill_cooldown - (u.Star >= 2 && id == "hero_role_4" ? 1 : 0) - (_leaderId == "hero_role_4" && _leaderTurns > 0 ? 1 : 0)); AddLog($"[color=99ddff]技能[/color] {u.Name} 使用技能 {skill + 1}。"); N<AcceptDialog>("SkillDialog").Hide(); RefreshAll();
	}
	private void ApplyStarChoice(bool attackRoute) { if (_pendingStarSlot?.Unit == null || _pendingCard == null) return; var u = _pendingStarSlot.Unit; var d = (HeroDefinition)u.Definition; u.Star++; var i = u.Star - 1; if (u.Star is 1 or 4) { if (attackRoute) u.Attack += d.star_attack_choices[i]; else { u.MaxHp += d.star_hp_choices[i]; u.Hp += d.star_hp_choices[i]; } } else if (u.Star == 6) { u.Attack += d.star_attack_choices[i]; u.MaxHp += d.star_hp_choices[i]; u.Hp += d.star_hp_choices[i]; u.Type = "无职业"; } if (u.Star == 2 && u.Id == "hero_role_1" && _leaderId == u.Id) _leaderTurns = 999; _deck.Discard(_pendingCard); _ap -= _pendingStarCost; _pendingCard = null; _pendingCardTarget = -1; _pendingStarCost = 0; AudioManager.Instance?.PlaySfx(GameSfx.LevelUp); AddLog($"[color=ffd75a]升星[/color] {u.Name} 达到★{u.Star}，{(attackRoute ? "攻击路线" : "生命路线")}。"); _pendingStarSlot = null; RefreshAll(); }
	private static void TickGrudge(IEnumerable<UnitSlot> slots) { foreach (var unit in slots.Where(slot => slot.Unit != null).Select(slot => slot.Unit!)) { if (unit.GrudgeStacks <= 0) continue; unit.GrudgeStacks--; unit.Attack += unit.GrudgeAttackPenaltyPerStack; if (unit.GrudgeStacks == 0) unit.GrudgeAttackPenaltyPerStack = 0; } }
	private void TickStatuses() { if (_leaderTurns > 0) _leaderTurns--; foreach (var s in _allies.Where(s => s.Unit != null)) { var u = s.Unit!; u.Cooldown = Math.Max(0, u.Cooldown - 1); u.SkillTurns = Math.Max(0, u.SkillTurns - 1); u.TauntTurns = Math.Max(0, u.TauntTurns - 1); u.CeasefireTurns = Math.Max(0, u.CeasefireTurns - 1); if (u.LinkTurns > 0) { if (u.LinkedEnemy >= 0 && _enemies[u.LinkedEnemy].Unit != null) { u.Attack = Math.Max(0, u.Attack - 1); _enemies[u.LinkedEnemy].Unit!.Attack = Math.Max(0, _enemies[u.LinkedEnemy].Unit!.Attack - 1); } u.LinkTurns--; } } foreach (var s in _enemies.Where(s => s.Unit != null)) { var u = s.Unit!; u.CeasefireTurns = Math.Max(0, u.CeasefireTurns - 1); if (u.DebuffTurns > 0 && --u.DebuffTurns == 0) { u.Attack += u.AttackRestore; u.AttackRestore = 0; } } }
	public void RefreshAll()
	{
		_turnControl.SetActionPoints(_ap, BattleState.DefaultActionPoints, _ap <= 0 || !HasObviousLegalAction()); N<Button>("HeroBag").Text = $"♛　{_heroBag.Count}"; N<Button>("HeroBag").TooltipText = $"♛ 英雄牌库 · 剩余{_heroBag.Count}"; N<Button>("DrawPile").Text = $"▣　{_deck.DrawPile.Count}"; N<Button>("DrawPile").TooltipText = $"▣ 抽牌堆 · {_deck.DrawPile.Count}"; N<Button>("DiscardPile").Text = $"▨　{_deck.DiscardPile.Count}"; N<Button>("DiscardPile").TooltipText = $"▨ 弃牌堆 · {_deck.DiscardPile.Count}"; N<Button>("CatalogButton").Text = $"◇　{content.cards.Count}"; N<Button>("CatalogButton").TooltipText = $"◇ 锦囊总览 · {content.cards.Count}";
		_passiveGate.SetCards(_battle.Passives);
		Clear(_hand); foreach (var card in _deck.Hand) { var tile = _cardScene.Instantiate<CardTile>(); _hand.AddChild(tile); tile.Setup(card); tile.CardChosen += ChooseCard; tile.DetailRequested += ShowCardDetail; _hand.RegisterCard(tile); }
		_hand.CallDeferred(HandFan.MethodName.ArrangeCards, false); RefreshEnemyHand(); RefreshSelection(); if (_battle.IsFinished) DisableAllBattleControls();
	}
	private bool HasObviousLegalAction()
	{
		if (_battle.IsFinished) return false;
		if (!_playerDeployedThisTurn && _heroBag.Count > 0 && _allies.Any(slot => slot.Unit?.Alive != true)) return true;
		if (_ap <= 0) return false;
		if (_allies.Any(slot => slot.Unit is { Alive: true, HasAttackedThisTurn: false }) && _enemies.Any(slot => slot.Unit?.Alive == true)) return true;
		if (_deck.Hand.Any(card => EffectiveCost(card) <= _ap)) return true;
		return _allies.Any(slot => slot.Unit is { Alive: true, Cooldown: <= 0 });
	}
	private void RefreshEnemyHand() { var row = N<HBoxContainer>("EnemyHand"); Clear(row); foreach (var card in _aiDeck.Hand) { var tile = _cardScene.Instantiate<CardTile>(); row.AddChild(tile); tile.Setup(card, true, true); tile.CustomMinimumSize = new(20, 32); tile.MouseFilter = MouseFilterEnum.Ignore; } }
	private static void Clear(Node node) { foreach (var child in node.GetChildren()) { node.RemoveChild(child); child.QueueFree(); } }
	private void RefreshSelection() { for (var i = 0; i < _allies.Count; i++) _allies[i].SetSelected(i == _allyIndex); for (var i = 0; i < _enemies.Count; i++) _enemies[i].SetSelected(i == _enemyIndex); }
	private void ShowPile(string title, IEnumerable<CardInstance> cards) { PopulatePile(title, cards.ToList()); N<AcceptDialog>("PileDialog").PopupCenteredRatio(.65f); }
	private void ShowCatalog() { PopulatePile("锦囊牌总卡包", content.cards.Select(c => new CardInstance(c, "catalog")).ToList()); N<AcceptDialog>("PileDialog").PopupCenteredRatio(.72f); }
	private void PopulatePile(string title, List<CardInstance> cards) { var dialog = N<AcceptDialog>("PileDialog"); dialog.Title = $"{title}（{cards.Count}）"; var grid = N<GridContainer>("PileCards"); Clear(grid); foreach (var c in cards) { var tile = _cardScene.Instantiate<CardTile>(); grid.AddChild(tile); tile.Setup(c); tile.CustomMinimumSize = CardTile.NativeSize; tile.DetailRequested += ShowCardDetail; } }
	private void ShowCardDetail(CardInstance card) { var pile = N<AcceptDialog>("PileDialog"); if (pile.Visible) pile.Hide(); _rightSidebar.ShowCard(card); }
	private void ShowUnitDetail(UnitSlot slot) { if (slot.Unit != null) _rightSidebar.ShowUnit(slot.Unit, slot.Side == "enemy"); }
	private async void OnCardDropped(UnitSlot slot, CardInstance card)
	{
		if (!_deck.Hand.Contains(card)) return;
		var target = card.Definition.target_kind;
		var validSide = slot.Side == "enemy"
			? target is CardDefinition.TargetKind.Enemy or CardDefinition.TargetKind.AnyUnit or CardDefinition.TargetKind.AllyEnemyPair
			: target is not CardDefinition.TargetKind.Enemy and not CardDefinition.TargetKind.None;
		if (!validSide) { _status.Text = "该位置不是此锦囊的合法目标"; return; }
		ChooseCard(card);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		slot.Activate();
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		if (_pendingCard == card && _pendingCardTarget == slot.SlotIndex) slot.Activate();
	}
	private void AddLog(string e) { _logs.Insert(0, $"[b]第 {_turn} 回合[/b]　{e}"); if (_logs.Count > 80) _logs.RemoveAt(_logs.Count - 1); N<RichTextLabel>("LogText").Text = string.Join("\n\n", _logs); }
	public void ReloadCardScripts()
	{
		// 传递所有卡牌定义给 Reload，使其在替换前验证所有脚本。
		_cardResolver.ReloadLua(content.cards);
		var errors = new List<string>(); foreach (var card in content.cards) if (!_cardResolver.ValidateLua(card.lua_script, out var error)) errors.Add($"{card.display_name}：{error}");
		if (errors.Count == 0) { _status.Text = "30张卡牌Lua脚本已重新加载并通过校验"; AddLog("[color=77ee99]Lua热重载[/color] 30张卡牌脚本校验通过。"); }
		else { _status.Text = $"Lua热重载失败：{errors.Count}张脚本错误"; AddLog($"[color=ff6666]Lua热重载失败[/color]\n{string.Join("\n", errors)}"); }
	}
	private static string HeroIdentity(HeroDefinition h) => h.display_name.Trim() == h.character_number.ToString() ? h.character_number.ToString() : $"{h.character_number} · {h.display_name}";
	private void TestModeToggled(bool enabled) { N<Button>("OpenTestEditor").Disabled = !enabled; _status.Text = enabled ? "测试模式已开启：可修改本次运行数值" : "正常模式"; }
	private void OpenTestEditor() { if (!N<CheckButton>("TestMode").ButtonPressed) return; var c = N<OptionButton>("Category"); c.Clear(); foreach (var s in new[] { "我方场上英雄", "敌方场上英雄", "英雄资源", "锦囊资源", "怪物资源" }) c.AddItem(s); LoadTestCategory(0); N<AcceptDialog>("TestEditorDialog").PopupCenteredRatio(.58f); }
	private void LoadTestCategory(int category) { _testTargets.Clear(); var target = N<OptionButton>("Target"); target.Clear(); if (category is 0 or 1) { var slots = category == 0 ? _allies : _enemies; foreach (var s in slots.Where(s => s.Unit != null)) AddTest($"{s.SlotIndex + 1}号位：{s.Unit!.Name}", "unit", s.Unit, s); } else if (category == 2) foreach (var h in content.heroes) AddTest(HeroEditorIdentity(h), "hero", h); else if (category == 3) foreach (var c in content.cards) AddTest(c.display_name, "card", c); else foreach (var m in content.monsters) AddTest(m.display_name, "monster", m); if (_testTargets.Count > 0) LoadTestTarget(0); }
	private void AddTest(string label, string kind, object target, UnitSlot? slot = null) { N<OptionButton>("Target").AddItem(label); _testTargets.Add((kind, target, slot)); }
	private static string HeroEditorIdentity(HeroDefinition hero)
	{
		var identity = HeroIdentity(hero);
		return identity.EndsWith(hero.TypeName(), StringComparison.Ordinal) ? identity : $"{identity} · {hero.TypeName()}";
	}
	private void LoadTestTarget(int i) { if (i < 0 || i >= _testTargets.Count) return; var e = _testTargets[i]; var unit = e.Target as UnitState; var hero = e.Target as HeroDefinition; var monster = e.Target as MonsterDefinition; var card = e.Target as CardDefinition; SetTest("Hp", unit != null); SetTest("MaxHp", unit != null || hero != null || monster != null); SetTest("Attack", unit != null || hero != null || monster != null); SetTest("Exp", unit != null); SetTest("Cost", card != null); SetTest("Effect", card != null); N<SpinBox>("TestHp").Value = unit?.Hp ?? 0; N<SpinBox>("TestMaxHp").Value = unit?.MaxHp ?? hero?.max_hp ?? monster?.max_hp ?? 0; N<SpinBox>("TestAttack").Value = unit?.Attack ?? hero?.attack ?? monster?.attack ?? 0; N<SpinBox>("TestExp").Value = unit?.Exp ?? 0; N<SpinBox>("TestCost").Value = card?.action_cost ?? 0; N<SpinBox>("TestEffect").Value = card?.effect_amount ?? 0; }
	private void SetTest(string name, bool visible) { N<Control>(name + "Label").Visible = visible; N<Control>("Test" + name).Visible = visible; }
	private void ApplyTestValues() { if (_testTargets.Count == 0) return; var e = _testTargets[N<OptionButton>("Target").Selected]; if (e.Target is UnitState u) { u.MaxHp = Math.Max(1, (int)N<SpinBox>("TestMaxHp").Value); u.Hp = Math.Clamp((int)N<SpinBox>("TestHp").Value, 0, u.MaxHp); u.Attack = Math.Max(0, (int)N<SpinBox>("TestAttack").Value); u.Exp = Math.Max(0, (int)N<SpinBox>("TestExp").Value); e.Slot?.Refresh(); } else if (e.Target is HeroDefinition h) { h.max_hp = Math.Max(1, (int)N<SpinBox>("TestMaxHp").Value); h.attack = Math.Max(0, (int)N<SpinBox>("TestAttack").Value); } else if (e.Target is MonsterDefinition m) { m.max_hp = Math.Max(1, (int)N<SpinBox>("TestMaxHp").Value); m.attack = Math.Max(0, (int)N<SpinBox>("TestAttack").Value); } else if (e.Target is CardDefinition c) { c.action_cost = Math.Max(0, (int)N<SpinBox>("TestCost").Value); c.effect_amount = (int)N<SpinBox>("TestEffect").Value; } RefreshAll(); }
}
