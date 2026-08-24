using Godot;

[Tool]
public partial class CardVisual : Control
{
    public static readonly Vector2 NativeSize = new(192, 244);

    private Texture2D? _illustrationTexture;
    private Texture2D? _frameTexture;
    private Texture2D? _starTexture;
    private string _previewCardName = "卡牌名称";
    private int _previewActionPointCost = 2;
    private int _previewStars;

    [ExportCategory("视觉资源")]
    [Export]
    public Texture2D? illustration_texture
    {
        get => _illustrationTexture;
        set { _illustrationTexture = value; Refresh(); }
    }

    [Export]
    public Texture2D? frame_texture
    {
        get => _frameTexture;
        set { _frameTexture = value; Refresh(); }
    }

    [Export]
    public Texture2D? star_texture
    {
        get => _starTexture;
        set { _starTexture = value; Refresh(); }
    }

    [ExportCategory("编辑器预览")]
    [Export]
    public string preview_card_name
    {
        get => _previewCardName;
        set { _previewCardName = value; Refresh(); }
    }

    [Export(PropertyHint.Range, "0,20,1")]
    public int preview_action_point_cost
    {
        get => _previewActionPointCost;
        set { _previewActionPointCost = value; Refresh(); }
    }

    [Export(PropertyHint.Range, "0,6,1")]
    public int preview_stars
    {
        get => _previewStars;
        set { _previewStars = value; Refresh(); }
    }

    public override void _Ready()
    {
        CustomMinimumSize = NativeSize;
        TextureFilter = TextureFilterEnum.Nearest;
        MouseFilter = MouseFilterEnum.Ignore;
        Refresh();
    }

    public void Bind(CardInstance card)
    {
        SetCardName(card.Definition.display_name);
        SetActionPointCost(card.CurrentCost());
        SetStars(card.Definition.rarity);
    }

    public void SetIllustration(Texture2D? texture) { _illustrationTexture = texture; Refresh(); }
    public void SetFrame(Texture2D? texture) { _frameTexture = texture; Refresh(); }
    public void SetCardName(string name) { _previewCardName = name; Refresh(); }
    public void SetActionPointCost(int value) { _previewActionPointCost = value; Refresh(); }
    public void SetStars(int stars) { _previewStars = Mathf.Clamp(stars, 0, 6); Refresh(); }

    private void Refresh()
    {
        if (!IsInsideTree()) return;

        GetNode<TextureRect>("Illustration").Texture = _illustrationTexture;
        GetNode<TextureRect>("Frame").Texture = _frameTexture;
        GetNode<Label>("APValue").Text = _previewActionPointCost.ToString();
        GetNode<Label>("CardName").Text = _previewCardName;

        var stars = GetNode<Control>("Stars");
        foreach (var child in stars.GetChildren()) child.Free();
        if (_starTexture == null || _previewStars <= 0) return;

        Texture2D displayStar = _starTexture;
        if (_starTexture.GetSize() == NativeSize)
        {
            displayStar = new AtlasTexture
            {
                Atlas = _starTexture,
                Region = new Rect2(93, 231, 5, 5)
            };
        }

        const float starSize = 5f;
        const float gap = 2f;
        var totalWidth = _previewStars * starSize + (_previewStars - 1) * gap;
        for (var index = 0; index < _previewStars; index++)
        {
            var star = new TextureRect
            {
                Texture = displayStar,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Keep,
                TextureFilter = TextureFilterEnum.Nearest,
                MouseFilter = MouseFilterEnum.Ignore,
                Position = new Vector2((NativeSize.X - totalWidth) / 2f + index * (starSize + gap), 231),
                Size = new Vector2(starSize, starSize)
            };
            stars.AddChild(star);
        }
    }
}
