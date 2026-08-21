using Godot;
using System;
using System.Linq;

public partial class BattleRightSidebar : Control
{
	public enum RightPanelMode { CommanderOverview, CardDetail, HeroDetail, EnemyDetail }

	public RightPanelMode Mode { get; private set; } = RightPanelMode.CommanderOverview;
	public bool ShowingDetail => Mode != RightPanelMode.CommanderOverview;

	private Control _commanderOverview = null!;
	private Control _detailView = null!;
	private Label _detailTitle = null!;
	private RichTextLabel _detailText = null!;

	public override void _Ready()
	{
		_commanderOverview = GetNode<Control>("%CommanderOverview");
		_detailView = GetNode<Control>("%DetailView");
		_detailTitle = GetNode<Label>("%DetailTitle");
		_detailText = GetNode<RichTextLabel>("%DetailText");
		GetNode<Button>("%CloseDetailButton").Pressed += ShowCommanderOverview;
		ShowCommanderOverview();
	}

	public void ShowCommanderOverview()
	{
		Mode = RightPanelMode.CommanderOverview;
		_commanderOverview.Visible = true;
		_detailView.Visible = false;
	}

	public void ShowCard(CardInstance card)
	{
		var definition = card.Definition;
		var type = definition.card_kind == CardDefinition.CardKind.Passive ? "被动锦囊" : "主动锦囊";
		var tags = definition.tags.Length == 0 ? "无" : string.Join("、", definition.tags);
		ShowDetail(RightPanelMode.CardDetail, definition.display_name,
			$"[b]费用[/b]　{card.CurrentCost()} AP\n[b]类型[/b]　{type}\n[b]目标[/b]　{TargetName(definition.target_kind)}\n\n[b]完整效果[/b]\n{definition.rules_text}\n\n[b]关键词[/b]　{tags}\n\n[color=8fa3b8]{definition.description}[/color]");
	}

	public void ShowUnit(UnitState unit, bool enemy)
	{
		if (enemy) ShowEnemyDetail(unit);
		else ShowHeroDetail(unit);
	}

	private void ShowEnemyDetail(UnitState unit)
	{
		ShowDetail(RightPanelMode.EnemyDetail, unit.Name,
			$"[b]职业 / 类型[/b]　{unit.Type}\n[b]HP[/b]　{unit.Hp} / {unit.MaxHp}\n[b]攻击[/b]　{unit.Attack}\n[b]公开状态[/b]　{BuildStatus(unit)}\n\n[b]公开意图[/b]　暂无\n\n[b]已发动 / 已知技能[/b]　暂无公开信息");
	}

	private void ShowHeroDetail(UnitState unit)
	{
		if (unit.Definition is not HeroDefinition definition)
		{
			ShowDetail(RightPanelMode.HeroDetail, unit.Name,
				$"[b]类型[/b]　{unit.Type}\n[b]HP[/b]　{unit.Hp} / {unit.MaxHp}\n[b]攻击[/b]　{unit.Attack}\n[b]状态[/b]　{BuildStatus(unit)}");
			return;
		}
		ShowDetail(RightPanelMode.HeroDetail, unit.Name,
			$"[b]职业[/b]　{unit.Type}\n[b]HP[/b]　{unit.Hp} / {unit.MaxHp}\n[b]攻击[/b]　{unit.Attack}\n[b]星级[/b]　★{unit.Star}\n[b]EXP[/b]　{unit.Exp} / {unit.ExpToStar}\n[b]状态[/b]　{BuildStatus(unit)}\n\n[b]技能[/b]\n{definition.skill_1_text}\n{definition.skill_2_text}\n\n[b]被动[/b]\n{definition.passive_text}\n\n[b]队长能力[/b]\n{definition.leader_bonus_text}");
	}

	private void ShowDetail(RightPanelMode mode, string title, string bbcode)
	{
		Mode = mode;
		_detailTitle.Text = title;
		_detailText.Text = bbcode;
		_commanderOverview.Visible = false;
		_detailView.Visible = true;
	}

	private static string TargetName(CardDefinition.TargetKind target) => target switch
	{
		CardDefinition.TargetKind.SelfHero => "自身英雄",
		CardDefinition.TargetKind.AllyHero => "我方英雄",
		CardDefinition.TargetKind.Enemy => "敌方英雄",
		CardDefinition.TargetKind.AnyUnit => "任意单位",
		CardDefinition.TargetKind.AllyEnemyPair => "我方与敌方英雄",
		CardDefinition.TargetKind.SetSlot => "英雄槽",
		_ => "无需目标"
	};

	private static string BuildStatus(UnitState unit)
	{
		string[] values =
		[
			unit.Cooldown > 0 ? $"冷却 {unit.Cooldown}" : "",
			unit.SkillTurns > 0 ? $"技能 {unit.SkillTurns}回合" : "",
			unit.TauntTurns > 0 ? $"嘲讽 {unit.TauntTurns}回合" : "",
			unit.ShieldTurns > 0 ? $"护盾 {Mathf.RoundToInt(unit.ShieldRatio * 100)}%" : "",
			unit.DebuffTurns > 0 ? $"减益 {unit.DebuffTurns}回合" : "",
			unit.CeasefireTurns > 0 ? $"停战 {unit.CeasefireTurns}回合" : ""
		];
		var active = values.Where(value => value.Length > 0).ToArray();
		return active.Length == 0 ? "正常" : string.Join("、", active);
	}
}
