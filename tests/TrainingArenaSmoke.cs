using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

public partial class TrainingArenaSmoke : Node
{
    public override async void _Ready()
    {
        try { await Run(); GD.Print("CSHARP_TRAINING_ARENA_SMOKE_OK"); GetTree().Quit(0); }
        catch (Exception e) { GD.PushError("CSHARP_SMOKE_FAILED: " + e); GetTree().Quit(1); }
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private async Task Run()
    {
        var arena = GD.Load<PackedScene>("res://scenes/training_arena.tscn").Instantiate<TrainingArena>(); AddChild(arena); await Frame(); await Frame();
        Check(arena.content.heroes.Count == 4, "必须加载4张英雄资源"); Check(arena.content.heroes[1].character_number == 2, "刺客编号必须为2");
        arena.GetNode<CheckButton>("%TestMode").ButtonPressed = true; arena.GetNode<Button>("%OpenTestEditor").EmitSignal(Button.SignalName.Pressed); await Frame(); arena.GetNode<OptionButton>("%Category").Select(2); arena.GetNode<OptionButton>("%Category").EmitSignal(OptionButton.SignalName.ItemSelected, 2); await Frame(); var heroTargets = arena.GetNode<OptionButton>("%Target"); Check(heroTargets.GetItemText(0) == "1 · 铁卫 · 先锋", "先锋必须显示编号、名称和职业"); Check(heroTargets.GetItemText(1) == "2 · 训练用木桩 · 刺客", "刺客必须同时显示编号、名称和职业"); Check(heroTargets.GetItemText(2) == "3 · 风羽 · 斥候", "斥候必须显示编号、名称和职业"); Check(heroTargets.GetItemText(3) == "4 · 律祷 · 祭司", "祭司必须显示编号、名称和职业"); arena.GetNode<AcceptDialog>("%TestEditorDialog").Hide();
        Check(arena.content.cards.Count == 30 && arena.content.cards.Select(c => c.id.ToString()).Distinct().Count() == 30, "规范化后的30张锦囊未完整加载");
        Check(arena.content.cards.Count(c => c.card_kind == CardDefinition.CardKind.Active) == 15 && arena.content.cards.Count(c => c.card_kind == CardDefinition.CardKind.Passive) == 15, "主动/被动锦囊分类错误");
        var dummy = arena.GetNode<CheckButton>("%DummyMode"); dummy.ButtonPressed = true; arena.ResetTraining(); await Frame();
        arena.GetNode<Button>("%HeroBag").EmitSignal(Button.SignalName.Pressed); await Frame(); var heroButton = arena.GetNode<VBoxContainer>("%HeroList").GetChildren().OfType<Button>().First(); heroButton.EmitSignal(Button.SignalName.Pressed);
        var ally = arena.GetNode<HBoxContainer>("%AllyRow").GetChild<UnitSlot>(0); var enemy = arena.GetNode<HBoxContainer>("%EnemyRow").GetChild<UnitSlot>(0); ally.EmitSignal(Button.SignalName.Pressed); await Delay(.5); Check(ally.Unit?.Alive == true, "英雄部署失败");
        ally.EmitSignal(Button.SignalName.Pressed); enemy.EmitSignal(Button.SignalName.Pressed); await Frame(); var hp = enemy.Unit!.Hp; Check(enemy.Text.Contains("预计"), "第一次点目标未写入攻击预览"); enemy.EmitSignal(Button.SignalName.Pressed); await Delay(1.5); Check(enemy.Unit.Hp < hp, "第二次点目标未结算攻击");
        var deck = (DeckState)GetPrivate(arena, "_deck")!; var stealDef = arena.content.cards.First(c => c.handler_key == "STEAL_TEMPORARY"); var counterDef = arena.content.cards.First(c => c.handler_key == "CANCEL_DRAW");
        var playerCounter = new CardInstance(counterDef); deck.Hand.Add(playerCounter); arena.RefreshAll(); await Frame(); var passiveTile = arena.GetNode<HandFan>("%Hand").GetChildren().OfType<CardTile>().First(t => t.Card == playerCounter); passiveTile.EmitSignal(Button.SignalName.Pressed); await Frame(); ally.EmitSignal(Button.SignalName.Pressed); await Frame(); Check(ally.PassiveCard == playerCounter && !deck.Hand.Contains(playerCounter), "被动锦囊必须从手牌背面盖放到英雄槽");
        SetPrivate(arena, "_ap", 3); var steal = new CardInstance(stealDef); deck.Hand.Add(steal); arena.RefreshAll(); await Frame(); var tile = arena.GetNode<HandFan>("%Hand").GetChildren().OfType<CardTile>().First(t => t.Card == steal); tile.EmitSignal(Button.SignalName.Pressed); await Frame(); tile.EmitSignal(Button.SignalName.Pressed); await Delay(2.0); Check(deck.DiscardPile.Contains(steal), "拿来主义未进入弃牌堆");
        arena.QueueFree(); await Frame();
    }
    private static object? GetPrivate(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target);
    private static void SetPrivate(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    private async Task Delay(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
}
