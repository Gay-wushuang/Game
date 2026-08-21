using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

public partial class BattleUiSmoke : Node
{
    public override async void _Ready()
    {
        try { await Run(); GD.Print("CSHARP_BATTLE_UI_SMOKE_OK"); GetTree().Quit(0); }
        catch (Exception exception) { GD.PushError("BATTLE_UI_SMOKE_FAILED: " + exception); GetTree().Quit(1); }
    }

    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    private async Task Delay(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private async Task Run()
    {
        var viewport = new SubViewport { Size = new Vector2I(1920, 1080), RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        AddChild(viewport);
        var arena = GD.Load<PackedScene>("res://scenes/training_arena.tscn").Instantiate<TrainingArena>();
        viewport.AddChild(arena); await Frame(); await Frame();

        var root = arena.GetNode<Control>("Margin/Root");
        var top = arena.GetNode<Control>("Margin/Root/TopBar");
        var left = arena.GetNode<Control>("Margin/Root/MainRow/LeftSidebar");
        var center = arena.GetNode<Control>("Margin/Root/MainRow/CenterColumn");
        var right = arena.GetNode<Control>("Margin/Root/MainRow/RightSidebar");
        var battlefield = arena.GetNode<Control>("Margin/Root/MainRow/CenterColumn/Battlefield");
        var handArea = arena.GetNode<Control>("Margin/Root/MainRow/CenterColumn/HandArea");
        var faction = arena.GetNode<Control>("Margin/Root/MainRow/LeftSidebar/LeftMargin/LeftContent/FactionPanel");
        var resourceDock = arena.GetNode<GridContainer>("Margin/Root/MainRow/LeftSidebar/LeftMargin/LeftContent/Piles");
        Check(Mathf.IsEqualApprox(root.Size.X, 1888), "1920 Master 安全区宽度错误");
        Check(Mathf.IsEqualApprox(top.Size.Y, 64), "TopBar 不是64px");
        Check(Mathf.IsEqualApprox(left.Size.X, 240), "LeftSidebar 不是240px");
        Check(Mathf.IsEqualApprox(center.Size.X, 1328), "CenterColumn 不是1328px");
        Check(Mathf.IsEqualApprox(right.Size.X, 304), "RightSidebar 不是304px");
        Check(Mathf.IsEqualApprox(battlefield.Size.Y, 700), $"Battlefield 不是700px，实际 {battlefield.Size.Y}");
        Check(Mathf.IsEqualApprox(handArea.Size.Y, 268), $"HandArea 不是268px，实际 {handArea.Size.Y}");
        Check(faction.Size.Y is >= 120 and <= 132, $"FactionPanel 未压缩到120~132px，实际 {faction.Size.Y}");
        Check(resourceDock.Columns == 2 && resourceDock.GetChildCount() == 4, "左侧资源区不是2×2 Dock");
        Check(resourceDock.Size.X <= 170 && resourceDock.GetChildren().OfType<Button>().All(button => button.CustomMinimumSize == new Vector2(78, 56) && !button.SizeFlagsVertical.HasFlag(Control.SizeFlags.Expand)), "资源Dock未限制在170px内，或按钮未使用78×56紧凑规格");
        var factionText = faction.GetNode<Label>("FactionText").Text;
        Check(factionText.IndexOf("敌方", StringComparison.Ordinal) < factionText.IndexOf("我方", StringComparison.Ordinal), "左栏阵营顺序不是敌上我下");
        Check(left is MarginContainer, "LeftSidebar 仍是实心Panel，而不是透明HUD Rail");
        Check(resourceDock.Position.Y > left.Size.Y * .65f, "资源Dock没有固定到左栏下部");
        Check(arena.FindChild("CancelButton", true, false) == null, "CancelButton 仍作为常驻UI存在");
        Check(arena.FindChild("SkillButton", true, false) == null, "SkillButton 仍作为常驻UI存在");
        Check(arena.FindChildren("ActionPoints", "Label", true, false).Count == 1, "AP仍存在多个常驻Label");
        var enemyCommander = arena.GetNode<Label>("Margin/Root/MainRow/RightSidebar/RightMargin/RightContent/ContentHost/CommanderOverview/CommanderContent/EnemyCommander");
        var playerCommander = arena.GetNode<Label>("Margin/Root/MainRow/RightSidebar/RightMargin/RightContent/ContentHost/CommanderOverview/CommanderContent/PlayerCommander");
        Check(enemyCommander.GlobalPosition.Y < playerCommander.GlobalPosition.Y, "右栏不是敌上我下");

        var allies = arena.GetNode<HBoxContainer>("%AllyRow").GetChildren().OfType<UnitSlot>().ToArray();
        var enemies = arena.GetNode<HBoxContainer>("%EnemyRow").GetChildren().OfType<UnitSlot>().ToArray();
        Check(allies.Length == 5 && enemies.Length == 5, "BattleSlot 未保持5v5复用");
        for (var index = 0; index < 5; index++) Check(Mathf.IsEqualApprox(allies[index].GlobalPosition.X, enemies[index].GlobalPosition.X), $"第{index + 1}列敌我槽未对齐");

        var rightPanel = arena.GetNode<BattleRightSidebar>("%ContentHost");
        var detailText = arena.GetNode<RichTextLabel>("%DetailText");
        var rightClick = new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true };
        var firstCard = arena.GetNode<HandFan>("%Hand").GetChildren().OfType<CardTile>().First();
        firstCard.EmitSignal(Control.SignalName.GuiInput, rightClick); await Frame();
        Check(rightPanel.Mode == BattleRightSidebar.RightPanelMode.CardDetail, "卡牌详情未进入固定右栏");
        viewport.PushInput(new InputEventKey { Keycode = Key.Escape, Pressed = true }, true); await Frame();
        Check(rightPanel.Mode == BattleRightSidebar.RightPanelMode.CommanderOverview, "取消选择未恢复指挥官总览");

        enemies[0].SetUnit(new HeroCardInstance(arena.content.heroes[1], "ai").Deploy());
        enemies[0].GetNode<Button>("%InteractionArea").EmitSignal(Control.SignalName.GuiInput, rightClick); await Frame();
        Check(rightPanel.Mode == BattleRightSidebar.RightPanelMode.EnemyDetail, "敌人右键详情未进入固定右栏");
        var enemyDefinition = arena.content.heroes[1];
        Check(!detailText.Text.Contains(enemyDefinition.skill_1_text) && !detailText.Text.Contains(enemyDefinition.passive_text) && !detailText.Text.Contains(enemyDefinition.leader_bonus_text), "EnemyDetail 泄露了敌方完整技能、被动或队长能力");
        allies[0].SetUnit(new HeroCardInstance(arena.content.heroes[0]).Deploy());
        allies[0].GetNode<Button>("%InteractionArea").EmitSignal(Control.SignalName.GuiInput, rightClick); await Frame();
        Check(rightPanel.Mode == BattleRightSidebar.RightPanelMode.HeroDetail, "我方英雄右键详情未进入固定右栏");
        Check(detailText.Text.Contains(arena.content.heroes[0].skill_1_text), "HeroDetail 没有显示我方已知技能");
        allies[0].Activate(); await Frame();
        var contextSkill = allies[0].GetNode<Button>("%ContextSkillButton");
        Check(contextSkill.Visible, "选择我方英雄后技能上下文动作没有出现");
        battlefield.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { Position = battlefield.GlobalPosition + battlefield.Size / 2f, ButtonIndex = MouseButton.Right, Pressed = true }); await Frame();
        Check(!contextSkill.Visible, "右键空白没有取消英雄选择或隐藏技能上下文动作");
        Check(!allies[0].DisplayText.Contains("等待部署") && !enemies[1].DisplayText.Contains("等待敌方部署"), "空战位仍显示重复等待说明");

        var turnControl = arena.GetNode<TurnControl>("%TurnControl");
        Check(turnControl.Current == BattleState.DefaultActionPoints && turnControl.Maximum == BattleState.DefaultActionPoints && !turnControl.Suggested, "TurnControl没有显示默认5点AP或错误进入提示态");
        SetPrivate(arena, "_ap", 0); arena.RefreshAll(); await Frame();
        Check(turnControl.Current == 0 && turnControl.Suggested, "AP归零后TurnControl没有进入提示态");
        SetPrivate(arena, "_ap", BattleState.DefaultActionPoints); arena.RefreshAll(); await Frame();

        var allyCard = new CardInstance(arena.content.cards.First(card => card.target_kind == CardDefinition.TargetKind.AllyHero));
        var dragTile = GD.Load<PackedScene>("res://scenes/components/card_tile.tscn").Instantiate<CardTile>(); AddChild(dragTile); dragTile.Setup(allyCard); await Frame();
        var dropped = false; allies[0].CardDropped += (_, card) => dropped = card == allyCard;
        dragTile.ForceDrag(dragTile.GetInstanceId(), new Button { CustomMinimumSize = new Vector2(144, 192) });
        var dragData = dragTile.GetViewport().GuiGetDragData();
        Check(allies[0]._CanDropData(Vector2.Zero, dragData), "合法锦囊拖拽目标未被识别");
        allies[0]._DropData(Vector2.Zero, dragData); await Frame();
        Check(dropped, "Drag/Drop 回调没有把卡牌交给合法战位");
        enemies[0].SetInteractionEnabled(false);
        var modeBeforeDisabledClick = rightPanel.Mode;
        enemies[0].GetNode<Button>("%InteractionArea").EmitSignal(Control.SignalName.GuiInput, rightClick); await Frame();
        Check(rightPanel.Mode == modeBeforeDisabledClick, "禁用战斗交互后敌方战位仍响应右键");
        Check(!enemies[0]._CanDropData(Vector2.Zero, dragData), "禁用战斗交互后敌方战位仍接收拖放");
        dragTile.GetViewport().GuiCancelDrag();
        dragTile.QueueFree();

        var fan = new HandFan { Size = new Vector2(1280, 268) }; AddChild(fan); await Frame();
        foreach (var count in new[] { 1, 2, 4, 5, 6, 8 })
        {
            foreach (var child in fan.GetChildren()) child.QueueFree();
            await Frame();
            for (var index = 0; index < count; index++) fan.AddChild(new Button());
            await Frame(); fan.ArrangeCards(); await Frame();
            var cards = fan.GetChildren().OfType<Control>().ToArray();
            Check(cards.Length == count, $"{count}张手牌布局数量错误");
            Check(cards.All(card => Mathf.IsEqualApprox(card.Size.X / card.Size.Y, .75f)), $"{count}张手牌未保持3:4");
            Check(cards.All(card => card.Position.X >= 0 && card.Position.X + card.Size.X <= fan.Size.X + .5f), $"{count}张手牌越出HandArea");
            if (count > 1) Check(cards.First().Rotation < cards.Last().Rotation, $"{count}张手牌没有形成扇形旋转");
        }
        var hoverCard = fan.GetChild<Control>(3); hoverCard.EmitSignal(Control.SignalName.MouseEntered); await Delay(.2);
        Check(Mathf.IsEqualApprox(hoverCard.Rotation, 0, .01f) && hoverCard.Scale.X > 1, "Hover未回正并放大卡牌");
        hoverCard.EmitSignal(Control.SignalName.MouseExited); await Delay(.2);
        Check(Mathf.IsEqualApprox(hoverCard.Scale.X, 1, .01f), "Hover结束后卡牌未平滑回位");

        fan.QueueFree(); viewport.QueueFree(); await Frame();
    }
    private static void SetPrivate(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
}
