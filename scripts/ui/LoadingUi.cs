using Godot;

public partial class LoadingUi : Control
{
    private LoadingRing _ring = null!;
    private string _pending = "";
    private bool _cached = false, _loading = false, _finishing = false;
    private PackedScene? _loaded = null;

    public override void _Ready()
    {
        _ring = GetNode<LoadingRing>("%Ring");
        _pending = SceneRouter.Instance.PendingLoad;
        if (string.IsNullOrEmpty(_pending)) { _loading = false; return; }
        if (ResourceLoader.HasCached(_pending))
        {
            _cached = true;
            _loaded = ResourceLoader.Load(_pending) as PackedScene;
        }
        else
        {
            ResourceLoader.LoadThreadedRequest(_pending);
        }
        _loading = true;
    }

    public override void _Process(double delta)
    {
        if (!_loading) return;
        if (_cached)
        {
            _ring.SetProgress(1f);
            if (_loaded != null) Finish(_loaded);
            return;
        }
        var progress = new Godot.Collections.Array();
        var status = ResourceLoader.LoadThreadedGetStatus(_pending, progress);
        var percent = progress.Count > 0 ? progress[0].AsSingle() : 0f;
        _ring.SetProgress(percent);
        if (status == ResourceLoader.ThreadLoadStatus.Loaded)
        {
            _loaded = ResourceLoader.LoadThreadedGet(_pending) as PackedScene;
            _cached = true;
        }
    }

    private void Finish(PackedScene scene)
    {
        if (_finishing) return;
        _finishing = true;
        if (scene == null) { SystemNotice.Instance.Show("场景加载失败"); return; }
        var tween = CreateTween();
        tween.TweenInterval(0.4);
        tween.TweenCallback(Callable.From(() => SceneRouter.Instance.DoTransition(scene)));
    }
}