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
        Check(arena.content.cards.Any(c => c.display_name == "拿来主义") && arena.content.cards.Any(c => c.display_name == "我觉得不行"), "新锦囊资源未加载");
        var dummy = arena.GetNode<CheckButton>("%DummyMode"); dummy.ButtonPressed = true; arena.ResetTraining(); await Frame();
        arena.GetNode<Button>("%HeroBag").EmitSignal(Button.SignalName.Pressed); await Frame(); var heroButton = arena.GetNode<VBoxContainer>("%HeroList").GetChildren().OfType<Button>().First(); heroButton.EmitSignal(Button.SignalName.Pressed);
        var ally = arena.GetNode<HBoxContainer>("%AllyRow").GetChild<UnitSlot>(0); var enemy = arena.GetNode<HBoxContainer>("%EnemyRow").GetChild<UnitSlot>(0); ally.EmitSignal(Button.SignalName.Pressed); await Delay(.5); Check(ally.Unit?.Alive == true, "英雄部署失败");
        ally.EmitSignal(Button.SignalName.Pressed); enemy.EmitSignal(Button.SignalName.Pressed); await Frame(); var hp = enemy.Unit!.Hp; Check(enemy.Text.Contains("预计"), "第一次点目标未写入攻击预览"); enemy.EmitSignal(Button.SignalName.Pressed); await Delay(1.5); Check(enemy.Unit.Hp < hp, "第二次点目标未结算攻击");
        var deck = (DeckState)GetPrivate(arena, "_deck")!; var aiDeck = (DeckState)GetPrivate(arena, "_aiDeck")!; var stealDef = arena.content.cards.First(c => c.builtin_effect == CardDefinition.BuiltinEffect.StealCard); var counterDef = arena.content.cards.First(c => c.builtin_effect == CardDefinition.BuiltinEffect.CancelEnemyDraw);
        var steal = new CardInstance(stealDef); deck.Hand.Add(steal); aiDeck.Hand.Add(new CardInstance(counterDef, "ai")); arena.RefreshAll(); await Frame(); var tile = arena.GetNode<HandFan>("%Hand").GetChildren().OfType<CardTile>().First(t => t.Card == steal); tile.EmitSignal(Button.SignalName.Pressed); await Frame(); tile.EmitSignal(Button.SignalName.Pressed); await Delay(2.5); Check(deck.DiscardPile.Contains(steal), "拿来主义未进入弃牌堆"); Check(aiDeck.DiscardPile.Any(c => c.Definition == counterDef), "我觉得不行未抵消拿来主义");
        arena.QueueFree(); await Frame();
    }
    private static object? GetPrivate(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target);
    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    private async Task Delay(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
}
