using Godot;

public partial class CardTile : Button
{
    public event System.Action<CardInstance>? CardChosen;
    public event System.Action<CardDefinition>? DetailRequested;
    public CardInstance Card { get; private set; } = null!;
    private bool _faceDown; private string _baseText = "";
    public void Setup(CardInstance value, bool showBack = false, bool small = false)
    {
        Card = value; _faceDown = showBack; SizeFlagsHorizontal = SizeFlags.ExpandFill; SizeFlagsVertical = SizeFlags.ExpandFill;
        ClipText = true; TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        if (showBack) { Text = small ? "◆" : "◆\n卡背"; TooltipText = "敌方手牌"; }
        else { _baseText = $"主动锦囊　AP {Card.CurrentCost()}\n\n{Card.Definition.display_name}\n\n{ShortRules(Card.Definition.rules_text)}"; Text = _baseText; TooltipText = string.IsNullOrWhiteSpace(Card.Definition.rules_text) ? Card.Definition.description : Card.Definition.rules_text; }
    }
    public override void _Ready() { Pressed += () => CardChosen?.Invoke(Card); GuiInput += OnGuiInput; }
    public void SetActionPreview(string target, string result) { if (!_faceDown) Text = $"{_baseText}\n\n→ {target}\n预计：{result}"; }
    public void ClearActionPreview() { if (!_faceDown) Text = _baseText; }
    private static string ShortRules(string value) => value.Length <= 18 ? value : value[..17] + "…";
    private void OnGuiInput(InputEvent e) { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } && !_faceDown) DetailRequested?.Invoke(Card.Definition); }
}
