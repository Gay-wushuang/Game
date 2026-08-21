using Godot;

public partial class LoadingRing : Control
{
    public float Progress { get; private set; } = 0f;

    public override void _Draw()
    {
        var center = Size / 2f;
        var radius = Size.X * 0.5f;
        DrawArc(center, radius, -Mathf.Pi / 2f, -Mathf.Pi / 2f + Mathf.Tau * Progress, 64, new Color(1, 0.27451f, 0.33333f, 0.9f), 18f, true);
    }

    public void SetProgress(float value)
    {
        Progress = Mathf.Clamp(value, 0f, 1f);
        QueueRedraw();
    }
}