using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class TrainingArena : Control
{
    [Export] public TrainingContent content { get; set; } = null!;
    private static readonly Dictionary<string, string> Counters = new() { ["先锋"] = "刺客", ["刺客"] = "斥候", ["斥候"] = "先锋" };
    private PackedScene _slotScene = null!, _cardScene = null!;
    private Label _status = null!, _apText = null!; private HandFan _hand = null!;
    private readonly DeckState _deck = new(), _aiDeck = new();
    private readonly List<UnitSlot> _allies = [], _enemies = [];
    private readonly List<HeroCardInstance> _heroBag = [], _aiHeroBag = [];
    private readonly List<string> _logs = [];
    private HeroCardInstance? _pendingHero; private CardInstance? _pendingCard; private UnitSlot? _pendingStarSlot;
    private int _pendingCardTarget = -1, _allyIndex = -1, _enemyIndex = -1, _ap = 3, _turn = 1, _pendingStarCost;
    private string _leaderId = "", _freeCardId = ""; private int _leaderTurns; private readonly Random _rng = new();
    private readonly List<(string Kind, object Target, UnitSlot? Slot)> _testTargets = [];

    public override void _Ready()
    {
        _slotScene = GD.Load<PackedScene>("res://scenes/components/unit_slot.tscn"); _cardScene = GD.Load<PackedScene>("res://scenes/components/card_tile.tscn");
        _status = GetNode<Label>("%Status"); _apText = GetNode<Label>("%ActionPoints"); _hand = GetNode<HandFan>("%Hand");
        ConnectControls(); CreateSlots(); ResetTraining();
    }
    private T N<T>(string name) where T : Node => GetNode<T>('%' + name);
    private void ConnectControls()
    {
        N<Button>("HeroBag").Pressed += OpenHeroBag; N<Button>("DrawPile").Pressed += () => ShowPile("抽牌堆", _deck.DrawPile);
        N<Button>("DiscardPile").Pressed += () => ShowPile("弃牌堆", _deck.DiscardPile); N<Button>("CatalogButton").Pressed += ShowCatalog;
        N<Button>("SkillButton").Pressed += OpenSkillDialog; N<Button>("CancelButton").Pressed += () => CancelSelection(); N<Button>("EndTurnButton").Pressed += async () => await EndTurn();
        N<Button>("LogButton").Pressed += () => N<AcceptDialog>("LogDialog").PopupCenteredRatio(.72f); N<Button>("SettingsButton").Pressed += () => N<AcceptDialog>("SettingsDialog").PopupCenteredRatio(.56f);
        N<Button>("ResetButton").Pressed += () => { N<AcceptDialog>("SettingsDialog").Hide(); ResetTraining(); };
        N<CheckButton>("DummyMode").Toggled += _ => ResetTraining(); N<CheckButton>("TestMode").Toggled += TestModeToggled;
        N<Button>("OpenTestEditor").Pressed += OpenTestEditor; N<OptionButton>("Category").ItemSelected += i => LoadTestCategory((int)i); N<OptionButton>("Target").ItemSelected += i => LoadTestTarget((int)i);
        N<Button>("ApplyTestValues").Pressed += ApplyTestValues; N<AcceptDialog>("StarChoiceDialog").Confirmed += () => ApplyStarChoice(true); N<AcceptDialog>("StarChoiceDialog").Canceled += () => ApplyStarChoice(false);
    }
    private void CreateSlots()
    {
        for (var i = 0; i < 5; i++) { var enemy = _slotScene.Instantiate<UnitSlot>(); enemy.Side = "enemy"; enemy.SlotIndex = i; enemy.SlotChosen += EnemyChosen; N<HBoxContainer>("EnemyRow").AddChild(enemy); _enemies.Add(enemy); var ally = _slotScene.Instantiate<UnitSlot>(); ally.Side = "ally"; ally.SlotIndex = i; ally.SlotChosen += AllyChosen; N<HBoxContainer>("AllyRow").AddChild(ally); _allies.Add(ally); }
    }
    public void ResetTraining()
    {
        _heroBag.Clear(); _aiHeroBag.Clear(); foreach (var h in content.heroes) { _heroBag.Add(new(h)); _aiHeroBag.Add(new(h, "ai")); }
        _deck.Setup(content.cards, "player"); _deck.Draw(4); _aiDeck.Setup(content.cards, "ai"); _aiDeck.Draw(4);
        _ap = 3; _turn = 1; _logs.Clear(); _leaderId = _freeCardId = ""; _leaderTurns = 0; N<Label>("Title").Text = "训练场 · 第 1 回合";
        foreach (var s in _allies) s.SetUnit(null); for (var i = 0; i < _enemies.Count; i++) s_set(_enemies[i], N<CheckButton>("DummyMode").ButtonPressed && i < content.monsters.Count ? FromMonster(content.monsters[i]) : null);
        CancelSelection(false); AddLog($"[color=75d7ff]系统[/color] {(N<CheckButton>("DummyMode").ButtonPressed ? "三职业稻草人就位" : "AI持有4张英雄牌")}，双方开局各抽取4张锦囊。"); _status.Text = "打开英雄卡包，选择英雄并部署到我方空位"; RefreshAll();
    }
    private static void s_set(UnitSlot s, UnitState? u) => s.SetUnit(u);
    private static UnitState FromMonster(MonsterDefinition d) => new() { Definition = d, Name = d.display_name, Type = d.TypeName(), Hp = d.max_hp, MaxHp = d.max_hp, Attack = d.attack, Star = 1, RetaliationRatio = d.retaliation_ratio };
    private void OpenHeroBag()
    {
        var list = N<VBoxContainer>("HeroList"); Clear(list); if (_heroBag.Count == 0) list.AddChild(new Label { Text = "所有英雄均已部署。" });
        foreach (var hc in _heroBag) { var h = hc.Definition; var b = new Button { Text = $"{HeroIdentity(h)} · {h.TypeName()}　HP {h.max_hp}　攻击 {h.attack}\n{h.description}", SizeFlagsVertical = Control.SizeFlags.ExpandFill }; b.Pressed += () => SelectHero(hc); list.AddChild(b); }
        N<AcceptDialog>("HeroBagDialog").PopupCenteredRatio(.62f);
    }
    public void SelectHero(HeroCardInstance hero) { if (_ap < 1) { _status.Text = "行动点不足：部署英雄需要1点"; return; } _pendingHero = hero; _pendingCard = null; N<AcceptDialog>("HeroBagDialog").Hide(); _status.Text = $"部署 {hero.Definition.display_name}：点击一个我方空位"; }
    private async void AllyChosen(UnitSlot slot)
    {
        if (_pendingHero != null) { if (slot.Unit?.Alive == true) { _status.Text = "该站位已有存活英雄"; return; } var hero = _pendingHero; slot.SetUnit(hero.Deploy()); _heroBag.Remove(hero); _ap--; if (_leaderId == "") { _leaderId = hero.Definition.id.ToString(); _leaderTurns = hero.Definition.CustomValue("leader_duration", 0).AsInt32(); ApplyLeaderBonus(); } else if (_leaderId == "hero_role_1" && (_leaderTurns > 0 || LeaderIsStarTwo())) { slot.Unit!.MaxHp += 50; slot.Unit.Hp += 50; } AddLog($"[color=75d7ff]部署[/color] {hero.Definition.display_name} 进入我方 {slot.SlotIndex + 1} 号位。"); _pendingHero = null; _allyIndex = slot.SlotIndex; RefreshAll(); await slot.PlayDeployAnimation(); return; }
        if (slot.Unit?.Alive != true) return; _allyIndex = slot.SlotIndex; _enemyIndex = -1; N<Button>("SkillButton").Disabled = slot.Unit.Cooldown > 0;
        if (_pendingCard != null) { if (_pendingCardTarget == slot.SlotIndex) await UseCard(slot); else PreviewCard(slot); } else _status.Text = $"已选择 {slot.Unit.Name}；请选择敌方目标"; RefreshSelection();
    }
    private async void EnemyChosen(UnitSlot slot) { if (_allyIndex < 0 || _allies[_allyIndex].Unit == null) { _status.Text = "请先选择我方英雄"; return; } if (slot.Unit?.Alive != true) return; if (_enemyIndex == slot.SlotIndex) { await ConfirmAttack(); return; } _enemyIndex = slot.SlotIndex; UpdatePreview(); RefreshSelection(); }
    private static string Relation(string a, string d) => Counters.GetValueOrDefault(a) == d ? "克制" : Counters.GetValueOrDefault(d) == a ? "被克制" : "中性";
    private static int Retaliation(UnitState a, UnitState d) { var v = Mathf.RoundToInt(d.Attack * d.RetaliationRatio); if (Relation(a.Type, d.Type) == "克制") v = Mathf.RoundToInt(v * .5f); else if (Relation(a.Type, d.Type) == "被克制") v = Mathf.RoundToInt(v * 1.5f); if (a.Star >= 3 && Relation(a.Type, d.Type) == "被克制") v = Mathf.RoundToInt(v * .75f); return v; }
    private static int AttackValue(UnitState a, UnitState d) { var v = a.Attack; if (a.Id == "hero_role_2") { v += 5; if (a.SkillTurns > 0) v += 10; if (a.Star >= 2 && d.Hp * 2 < d.MaxHp) v += 5; } return v; }
    private void UpdatePreview() { var a = _allies[_allyIndex].Unit!; var d = _enemies[_enemyIndex].Unit!; var raw = Retaliation(a, d); var final = PreviewDamage(a, raw); var damage = AttackValue(a, d); _allies[_allyIndex].SetActionPreview($"HP {a.Hp} → {Math.Max(0, a.Hp - final)}（反伤）"); _enemies[_enemyIndex].SetActionPreview($"HP {d.Hp} → {Math.Max(0, d.Hp - damage)}（受击）"); _status.Text = $"{Relation(a.Type, d.Type)}：伤害 {damage}，反伤 {raw}→{final}；再次点击目标结算"; }
    public async Task ConfirmAttack() { if (_allyIndex < 0 || _enemyIndex < 0 || _ap <= 0) return; var a = _allies[_allyIndex].Unit!; var d = _enemies[_enemyIndex].Unit!; var counter = Retaliation(a, d); var damage = AttackValue(a, d); await AnimateAttack(_allies[_allyIndex], _enemies[_enemyIndex]); d.Hp = Math.Max(0, d.Hp - damage); if (a.Id == "hero_role_2" && a.SkillTurns > 0) counter = Mathf.RoundToInt(counter * (a.Star >= 5 ? 1.2f : 1.4f)); ApplyDamageToAlly(a, counter); GainExp(a, 2); _ap--; MarkDefeated(); AddLog($"[color=ffcc66]攻击[/color] {a.Name} → {d.Name}：伤害 {damage}，反伤 {counter}，{Relation(a.Type, d.Type)}，获得 2 EXP。"); CancelSelection(false); _status.Text = "攻击结算完成"; RefreshAll(); }
    private async void ChooseCard(CardInstance card)
    {
        foreach (var c in _hand.GetChildren().OfType<CardTile>()) c.ClearActionPreview(); if (card.Definition.card_kind == CardDefinition.CardKind.Passive) { _status.Text = $"被动锦囊「{card.Definition.display_name}」会自动触发"; return; }
        if (card.Definition.builtin_effect == CardDefinition.BuiltinEffect.StealCard) { if (_pendingCard == card && _pendingCardTarget == -2) await UseStealCard(card, false); else { _pendingCard = card; _pendingCardTarget = -2; var i = _deck.Hand.IndexOf(card); _hand.SetSelected(i); if (i >= 0 && _hand.GetChild(i) is CardTile tile) tile.SetActionPreview("敌方手牌", $"{_aiDeck.Hand.Count} → {Math.Max(0, _aiDeck.Hand.Count - 1)}；我方 {_deck.Hand.Count} → {_deck.Hand.Count + 1}"); _status.Text = "拿来主义效果已写入卡面；再次点击该牌确认"; } return; }
        if (EffectiveCost(card) > _ap && !_allies.Any(s => s.Unit is { Id: "hero_role_3", Star: >= 5, FreeSelfCards: > 0 })) { _status.Text = "行动点不足"; return; }
        _pendingCard = card; _pendingCardTarget = -1; _pendingHero = null; _enemyIndex = -1; _hand.SetSelected(_deck.Hand.IndexOf(card)); _status.Text = $"锦囊「{card.Definition.display_name}」：请选择我方英雄预览效果";
    }
    private async Task UseCard(UnitSlot slot)
    {
        var card = _pendingCard!; var d = card.Definition; var u = slot.Unit!; var cost = EffectiveCost(card); if (d.id.ToString() == _freeCardId) _freeCardId = ""; if (u.Id == "hero_role_3" && u.Star >= 5 && u.FreeSelfCards > 0) { cost = 0; u.FreeSelfCards--; }
        await AnimateCard(card); switch (d.builtin_effect) { case CardDefinition.BuiltinEffect.Heal: var amount = Math.Min(d.effect_amount, u.MaxHp - u.Hp); u.Hp += amount; AddLog($"[color=77ee99]锦囊[/color] {d.display_name} → {u.Name}：回复 {amount} HP。"); break; case CardDefinition.BuiltinEffect.AddAttack: u.Attack += d.effect_amount; break; case CardDefinition.BuiltinEffect.AddExp: GainExp(u, d.effect_amount); break; case CardDefinition.BuiltinEffect.StarUp: if (u.Star >= 6) { _status.Text = "该英雄已达到6星"; return; } _pendingStarSlot = slot; _pendingStarCost = cost; N<AcceptDialog>("StarChoiceDialog").PopupCenteredRatio(.42f); return; }
        _ap -= cost; _deck.Discard(card); _pendingCard = null; _pendingCardTarget = -1; _status.Text = "锦囊已结算并进入弃牌堆"; RefreshAll();
    }
    private void PreviewCard(UnitSlot slot) { var d = _pendingCard!.Definition; var u = slot.Unit!; var effect = d.builtin_effect switch { CardDefinition.BuiltinEffect.Heal => $"HP {u.Hp} → {Math.Min(u.MaxHp, u.Hp + d.effect_amount)}", CardDefinition.BuiltinEffect.AddAttack => $"攻击 {u.Attack} → {u.Attack + d.effect_amount}", CardDefinition.BuiltinEffect.AddExp => $"EXP {u.Exp} → {u.Exp + d.effect_amount}", CardDefinition.BuiltinEffect.StarUp => $"星级 {u.Star} → {Math.Min(6, u.Star + 1)}", _ => "无数值变化" }; _pendingCardTarget = slot.SlotIndex; _allyIndex = slot.SlotIndex; var i = _deck.Hand.IndexOf(_pendingCard); if (i >= 0 && _hand.GetChild(i) is CardTile tile) tile.SetActionPreview(u.Name, effect); slot.SetActionPreview(effect); _status.Text = "效果已预览，再次点击同一英雄确认使用"; RefreshSelection(); }
    private void GainExp(UnitState u, int n) { u.Exp += n; AddLog($"[color=aaddff]经验[/color] {u.Name} 当前 EXP {u.Exp}；升星仅由升星牌触发。"); }
    public void DrawOne() { if (_deck.Hand.Count >= 5) { _status.Text = "手牌已满"; return; } var result = _deck.Draw(); _status.Text = result.Count == 0 ? "没有可抽的牌" : $"抽到「{result[0].Definition.display_name}」"; RefreshAll(); }
    private async Task EndTurn() { await EnemyPhase(); _turn++; _ap = 3; TickStatuses(); foreach (var s in _allies.Where(s => s.Unit is { Alive: true, Id: "hero_role_3" })) { _ap++; if (s.Unit!.Star >= 5) s.Unit.FreeSelfCards = 2; } AssignFreeCard(); if (_deck.Hand.Count < 5) _deck.Draw(); N<Label>("Title").Text = $"训练场 · 第 {_turn} 回合"; AddLog($"[color=75d7ff]系统[/color] 第 {_turn} 回合开始。"); CancelSelection(false); _status.Text = "新回合：行动点恢复，自动抽牌"; RefreshAll(); }
    private void CancelSelection(bool update = true) { _pendingHero = null; _pendingCard = null; _pendingCardTarget = _allyIndex = _enemyIndex = -1; _hand.SetSelected(-1); foreach (var s in _allies.Concat(_enemies)) s.ClearActionPreview(); if (update) _status.Text = "已取消选择"; RefreshSelection(); }
    private async Task EnemyPhase()
    {
        if (N<CheckButton>("DummyMode").ButtonPressed) { AddLog("[color=999999]稻草人模式[/color] 敌方跳过全部行动。"); return; }
        var aiAp = 3; if (_turn <= 4 && _aiHeroBag.Count > 0) aiAp -= await AiDeploy(); var attacks = _turn <= 4 ? 1 : 2;
        for (var i = 0; i < attacks && aiAp > 0; i++) if (await AiAttack()) aiAp--; if (aiAp > 0 && await AiUseCard()) aiAp--; if (_aiDeck.Hand.Count < 5) _aiDeck.Draw(); AddLog($"[color=ff8888]AI回合[/color] 敌方行动结束，剩余行动点 {aiAp}。");
    }
    private async Task<int> AiDeploy() { var empty = _enemies.Where(s => s.Unit?.Alive != true).ToList(); if (empty.Count == 0 || _aiHeroBag.Count == 0) return 0; var hero = _aiHeroBag[_rng.Next(_aiHeroBag.Count)]; var slot = empty[_rng.Next(empty.Count)]; slot.SetUnit(hero.Deploy()); _aiHeroBag.Remove(hero); await slot.PlayDeployAnimation(); AddLog($"[color=ff8888]AI部署[/color] {hero.Definition.display_name} 进入敌方 {slot.SlotIndex + 1} 号位（消耗1行动点）。"); return 1; }
    private async Task<bool> AiAttack()
    {
        var pairs = (from e in _enemies where e.Unit?.Alive == true from a in _allies where a.Unit?.Alive == true select (E: e, A: a)).ToList(); if (pairs.Count == 0) return false; var advantage = pairs.Where(p => Relation(p.E.Unit!.Type, p.A.Unit!.Type) == "克制").ToList(); var pool = advantage.Count > 0 ? advantage : pairs; var chosen = pool[_rng.Next(pool.Count)]; var attacker = chosen.E.Unit!; var target = chosen.A.Unit!; var damage = AttackValue(attacker, target); var counter = Retaliation(attacker, target); await AnimateAttack(chosen.E, chosen.A); ApplyDamageToAlly(target, damage); attacker.Hp = Math.Max(0, attacker.Hp - counter); MarkDefeated(); AddLog($"[color=ff8888]AI攻击[/color] {attacker.Name} → {target.Name}：{Relation(attacker.Type, target.Type)}，伤害{damage}，受到反伤{counter}。"); chosen.E.Refresh(); chosen.A.Refresh(); return true;
    }
    private async Task<bool> AiUseCard()
    {
        var playable = _aiDeck.Hand.Where(c => c.Definition.card_kind == CardDefinition.CardKind.Active).ToList(); if (playable.Count == 0) return false; var card = playable[_rng.Next(playable.Count)]; if (card.Definition.builtin_effect == CardDefinition.BuiltinEffect.StealCard) { await UseStealCard(card, true); return true; } var slot = AiCardTarget(card.Definition); if (slot?.Unit == null) return false; await AnimateCard(card); var u = slot.Unit; switch (card.Definition.builtin_effect) { case CardDefinition.BuiltinEffect.Heal: u.Hp = Math.Min(u.MaxHp, u.Hp + card.Definition.effect_amount); break; case CardDefinition.BuiltinEffect.AddAttack: u.Attack += card.Definition.effect_amount; break; case CardDefinition.BuiltinEffect.AddExp: u.Exp += card.Definition.effect_amount; break; case CardDefinition.BuiltinEffect.StarUp: AiStarUp(u); break; default: return false; } _aiDeck.Discard(card); slot.Refresh(); AddLog($"[color=dd99ff]AI锦囊[/color] 「{card.Definition.display_name}」对 {u.Name} 生效。"); return true;
    }
    private async Task UseStealCard(CardInstance card, bool byAi)
    {
        await Announce($"{(byAi ? "AI" : "玩家")}打出「拿来主义」"); var source = byAi ? _deck : _aiDeck; var destination = byAi ? _aiDeck : _deck;
        if (TriggerCounter(source, byAi)) { await Announce("「我觉得不行」发动：拿来主义被抵消"); (byAi ? _aiDeck : _deck).Discard(card); if (!byAi) _ap -= card.CurrentCost(); _pendingCard = null; _pendingCardTarget = -1; CallDeferred(MethodName.RefreshAll); return; }
        await AnimateCard(card); if (source.Hand.Count > 0) { var stolen = source.Hand[_rng.Next(source.Hand.Count)]; source.Hand.Remove(stolen); destination.Hand.Add(stolen); stolen.OwnerId = byAi ? "ai" : "player"; stolen.FaceUp = !byAi; stolen.Zone = CardInstance.ZoneKind.Hand; AddLog($"[color=dd99ff]{(byAi ? "AI锦囊" : "锦囊")}[/color] 「拿来主义」获得了对方一张手牌。"); }
        (byAi ? _aiDeck : _deck).Discard(card); if (!byAi) _ap -= card.CurrentCost(); _pendingCard = null; _pendingCardTarget = -1; _hand.SetSelected(-1); CallDeferred(MethodName.RefreshAll);
    }
    private bool TriggerCounter(DeckState defender, bool attackerAi) { var c = defender.Hand.FirstOrDefault(x => x.Definition.builtin_effect == CardDefinition.BuiltinEffect.CancelEnemyDraw); if (c == null) return false; defender.Discard(c); AddLog($"[color=ff99aa]被动锦囊[/color] {(attackerAi ? "玩家" : "AI")}的「我觉得不行」阻止了拿来主义。"); return true; }
    private async Task AnimateAttack(UnitSlot a, UnitSlot d) { _status.Text = $"{a.Unit!.Name} 锁定 {d.Unit!.Name}，准备结算……"; await a.PlayTargetFlash(new(1.55f, 1.25f, .55f)); await d.PlayTargetFlash(new(1.55f, .7f, .7f)); }
    private async Task Announce(string text) { var label = N<Label>("Announcement"); label.Text = text; label.Visible = true; label.Modulate = new(1.35f, 1.25f, .65f, 0); var t = CreateTween(); t.TweenProperty(label, "modulate:a", 1, .12); t.TweenInterval(.5); t.TweenProperty(label, "modulate:a", 0, .18); await ToSignal(t, Tween.SignalName.Finished); label.Visible = false; }
    private async Task AnimateCard(CardInstance card) { var tile = _cardScene.Instantiate<CardTile>(); AddChild(tile); tile.Setup(card); tile.MouseFilter = MouseFilterEnum.Ignore; var v = GetViewportRect().Size; tile.Size = new(v.X * .12f, v.Y * .28f); tile.Position = new((v.X - tile.Size.X) / 2, v.Y * .32f); tile.Modulate = new(1.5f, 1.5f, 1.5f, 0); tile.Scale = new(.8f, .8f); tile.PivotOffset = tile.Size / 2; var t = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out); t.TweenProperty(tile, "modulate", Colors.White, .18); t.TweenProperty(tile, "scale", Vector2.One, .25); await ToSignal(t, Tween.SignalName.Finished); await ToSignal(GetTree().CreateTimer(.22), SceneTreeTimer.SignalName.Timeout); tile.QueueFree(); }
    private UnitSlot? AiCardTarget(CardDefinition card) { var living = _enemies.Where(s => s.Unit?.Alive == true).ToList(); if (living.Count == 0) return null; if (card.builtin_effect == CardDefinition.BuiltinEffect.Heal) living.Sort((a,b) => ((float)a.Unit!.Hp/a.Unit.MaxHp).CompareTo((float)b.Unit!.Hp/b.Unit.MaxHp)); else if (card.builtin_effect == CardDefinition.BuiltinEffect.AddAttack) living.Sort((a,b) => ((float)b.Unit!.Hp/b.Unit.MaxHp).CompareTo((float)a.Unit!.Hp/a.Unit.MaxHp)); else living.Sort((a,b) => b.Unit!.Exp != a.Unit!.Exp ? b.Unit.Exp.CompareTo(a.Unit.Exp) : b.Unit.Hp.CompareTo(a.Unit.Hp)); return living[0]; }
    private static void AiStarUp(UnitState u) { if (u.Star >= 6 || u.Definition is not HeroDefinition d) return; u.Star++; var i = u.Star - 1; if (u.Star is 1 or 4) u.Attack += d.star_attack_choices[i]; else if (u.Star == 6) { u.Attack += d.star_attack_choices[i]; u.MaxHp += d.star_hp_choices[i]; u.Hp += d.star_hp_choices[i]; u.Type = "无职业"; } }
    private void ApplyDamageToAlly(UnitState u, int amount) { var target = _allies.Select(s => s.Unit).FirstOrDefault(x => x is { Alive: true, TauntTurns: > 0 }) ?? u; if (target.Id == "hero_role_1") { var ratio = (float)target.Hp / target.MaxHp; if (target.Star >= 5 && ratio <= .3) amount = Mathf.RoundToInt(amount * .25f); else if (ratio <= .5) amount = Mathf.RoundToInt(amount * .5f); } target.Hp = Math.Max(0, target.Hp - amount); }
    private int PreviewDamage(UnitState u, int amount) { var target = _allies.Select(s => s.Unit).FirstOrDefault(x => x is { Alive: true, TauntTurns: > 0 }) ?? u; if (target.Id == "hero_role_1") { var ratio = (float)target.Hp / target.MaxHp; if (target.Star >= 5 && ratio <= .3) return Mathf.RoundToInt(amount * .25f); if (ratio <= .5) return Mathf.RoundToInt(amount * .5f); } return amount; }
    private void MarkDefeated() { foreach (var s in _allies.Concat(_enemies)) if (s.Unit is { Alive: false }) s.Refresh(); }
    private void ApplyLeaderBonus() { if (_leaderId == "hero_role_1") foreach (var s in _allies.Where(s => s.Unit != null)) { s.Unit!.MaxHp += 50; s.Unit.Hp += 50; s.Refresh(); } else if (_leaderId == "hero_role_3") AssignFreeCard(); AddLog("[color=ffee88]队长[/color] 第一名部署英雄成为队长，队长加成开始生效。"); }
    private bool LeaderIsStarTwo() => _allies.Any(s => s.Unit is { Star: >= 2 } u && u.Id == _leaderId);
    private void AssignFreeCard() { _freeCardId = ""; if (((_leaderId == "hero_role_3" && _leaderTurns > 0) || _allies.Any(s => s.Unit is { Id: "hero_role_3", Star: >= 2 })) && _deck.Hand.Count > 0) _freeCardId = _deck.Hand[_rng.Next(_deck.Hand.Count)].Definition.id.ToString(); }
    private int EffectiveCost(CardInstance c) => c.Definition.id.ToString() == _freeCardId ? 0 : c.CurrentCost();
    private void OpenSkillDialog()
    {
        if (_allyIndex < 0 || _allies[_allyIndex].Unit == null) { _status.Text = "请先选择我方英雄"; return; } var u = _allies[_allyIndex].Unit!; if (u.Cooldown > 0) { _status.Text = $"技能冷却剩余 {u.Cooldown} 回合"; return; }
        var list = N<VBoxContainer>("SkillList"); Clear(list); var d = (HeroDefinition)u.Definition; var texts = new List<string> { d.skill_1_text }; if (!string.IsNullOrEmpty(d.skill_2_text)) texts.Add(d.skill_2_text); for (var i = 0; i < texts.Count; i++) { var index = i; var b = new Button { Text = $"技能 {i + 1}\n{texts[i]}", SizeFlagsVertical = SizeFlags.ExpandFill }; b.Pressed += () => ExecuteSkill(index); list.AddChild(b); } N<AcceptDialog>("SkillDialog").PopupCenteredRatio(.58f);
    }
    public void ExecuteSkill(int skill)
    {
        var u = _allies[_allyIndex].Unit!; var id = u.Id; if (id == "hero_role_4" && (_enemyIndex < 0 || _enemies[_enemyIndex].Unit == null)) { N<AcceptDialog>("SkillDialog").Hide(); _status.Text = "祭司技能需要先选择敌方目标"; return; }
        if (id == "hero_role_1") u.TauntTurns = u.Star >= 5 ? 3 : 2; else if (id == "hero_role_2") u.SkillTurns = 2; else if (id == "hero_role_3") { var count = Math.Min(2, _aiDeck.Hand.Count); for (var i = 0; i < count; i++) _aiDeck.Discard(_aiDeck.Hand[0]); _ap = 0; } else if (id == "hero_role_4") { var target = _enemies[_enemyIndex].Unit!; if (skill == 0) { var down = Math.Min(2, target.Attack); target.Attack -= down; target.AttackRestore = down; target.DebuffTurns = 3; } else { u.LinkedEnemy = _enemyIndex; u.LinkTurns = 2; } u.Hp = Math.Min(u.MaxHp, u.Hp + Mathf.RoundToInt(u.Hp * (u.Star >= 5 ? .1f : .05f))); }
        var d = (HeroDefinition)u.Definition; u.Cooldown = Math.Max(0, d.skill_cooldown - (u.Star >= 2 && id == "hero_role_4" ? 1 : 0) - (_leaderId == "hero_role_4" && _leaderTurns > 0 ? 1 : 0)); AddLog($"[color=99ddff]技能[/color] {u.Name} 使用技能 {skill + 1}。"); N<AcceptDialog>("SkillDialog").Hide(); RefreshAll();
    }
    private void ApplyStarChoice(bool attackRoute) { if (_pendingStarSlot?.Unit == null || _pendingCard == null) return; var u = _pendingStarSlot.Unit; var d = (HeroDefinition)u.Definition; u.Star++; var i = u.Star - 1; if (u.Star is 1 or 4) { if (attackRoute) u.Attack += d.star_attack_choices[i]; else { u.MaxHp += d.star_hp_choices[i]; u.Hp += d.star_hp_choices[i]; } } else if (u.Star == 6) { u.Attack += d.star_attack_choices[i]; u.MaxHp += d.star_hp_choices[i]; u.Hp += d.star_hp_choices[i]; u.Type = "无职业"; } if (u.Star == 2 && u.Id == "hero_role_1" && _leaderId == u.Id) _leaderTurns = 999; _deck.Discard(_pendingCard); _ap -= _pendingStarCost; _pendingCard = null; _pendingCardTarget = -1; _pendingStarCost = 0; AddLog($"[color=ffd75a]升星[/color] {u.Name} 达到★{u.Star}，{(attackRoute ? "攻击路线" : "生命路线")}。"); _pendingStarSlot = null; RefreshAll(); }
    private void TickStatuses() { if (_leaderTurns > 0) _leaderTurns--; foreach (var s in _allies.Where(s => s.Unit != null)) { var u = s.Unit!; u.Cooldown = Math.Max(0, u.Cooldown - 1); u.SkillTurns = Math.Max(0, u.SkillTurns - 1); u.TauntTurns = Math.Max(0, u.TauntTurns - 1); if (u.LinkTurns > 0) { if (u.LinkedEnemy >= 0 && _enemies[u.LinkedEnemy].Unit != null) { u.Attack = Math.Max(0, u.Attack - 1); _enemies[u.LinkedEnemy].Unit!.Attack = Math.Max(0, _enemies[u.LinkedEnemy].Unit!.Attack - 1); } u.LinkTurns--; } } foreach (var s in _enemies.Where(s => s.Unit != null)) { var u = s.Unit!; if (u.DebuffTurns > 0 && --u.DebuffTurns == 0) { u.Attack += u.AttackRestore; u.AttackRestore = 0; } } }
    public void RefreshAll()
    {
        _apText.Text = $"行动点 {_ap}/3"; N<Button>("HeroBag").Text = $"英雄卡包\n{_heroBag.Count}"; N<Button>("DrawPile").Text = $"抽牌堆\n{_deck.DrawPile.Count}"; N<Button>("DiscardPile").Text = $"弃牌堆\n{_deck.DiscardPile.Count}";
        Clear(_hand); foreach (var card in _deck.Hand) { var tile = _cardScene.Instantiate<CardTile>(); _hand.AddChild(tile); tile.Setup(card); tile.CardChosen += ChooseCard; tile.DetailRequested += ShowCardDetail; } _hand.CallDeferred(HandFan.MethodName.ArrangeCards, false); RefreshEnemyHand(); RefreshSelection();
    }
    private void RefreshEnemyHand() { var row = N<HBoxContainer>("EnemyHand"); Clear(row); foreach (var card in _aiDeck.Hand) { var tile = _cardScene.Instantiate<CardTile>(); row.AddChild(tile); tile.Setup(card, true, true); tile.CustomMinimumSize = new(20, 32); tile.MouseFilter = MouseFilterEnum.Ignore; } }
    private static void Clear(Node node) { foreach (var child in node.GetChildren()) { node.RemoveChild(child); child.Free(); } }
    private void RefreshSelection() { for (var i = 0; i < _allies.Count; i++) _allies[i].SetSelected(i == _allyIndex); for (var i = 0; i < _enemies.Count; i++) _enemies[i].SetSelected(i == _enemyIndex); }
    private void ShowPile(string title, IEnumerable<CardInstance> cards) { PopulatePile(title, cards.ToList()); N<AcceptDialog>("PileDialog").PopupCenteredRatio(.65f); }
    private void ShowCatalog() { PopulatePile("锦囊牌总卡包", content.cards.Select(c => new CardInstance(c, "catalog")).ToList()); N<AcceptDialog>("PileDialog").PopupCenteredRatio(.72f); }
    private void PopulatePile(string title, List<CardInstance> cards) { var dialog = N<AcceptDialog>("PileDialog"); dialog.Title = $"{title}（{cards.Count}）"; var grid = N<GridContainer>("PileCards"); Clear(grid); foreach (var c in cards) { var tile = _cardScene.Instantiate<CardTile>(); grid.AddChild(tile); tile.Setup(c); tile.CustomMinimumSize = new(135, 175); tile.DetailRequested += ShowCardDetail; } }
    private void ShowCardDetail(CardDefinition c) { var d = N<AcceptDialog>("CardDetailDialog"); d.Title = c.display_name; N<RichTextLabel>("CardDetailText").Text = $"[center][font_size=22][b]{c.display_name}[/b][/font_size][/center]\n\n[b]行动点：[/b]{c.action_cost}\n[b]标签：[/b]{string.Join("、", c.tags)}\n\n{c.rules_text}\n\n[color=999999]{c.description}[/color]"; d.PopupCenteredRatio(.58f); }
    private void AddLog(string e) { _logs.Insert(0, $"[b]第 {_turn} 回合[/b]　{e}"); if (_logs.Count > 80) _logs.RemoveAt(_logs.Count - 1); N<RichTextLabel>("LogText").Text = string.Join("\n\n", _logs); }
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
