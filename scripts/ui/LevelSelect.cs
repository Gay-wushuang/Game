using Godot;

public partial class LevelSelect : Control
{
    private int _deleteSlot;
    private int _loadSlot;
    private ConfirmationDialog _deleteConfirm = null!;
    private ConfirmationDialog _loadConfirm = null!;

    public override void _Ready()
    {
        GetNode<Button>("%BackButton").Pressed += Back;
        for (var i = 1; i <= 5; i++) { var index = i; GetNode<Button>($"%Level{i}").Pressed += () => { if (index == 1) SceneRouter.Instance.GoTo(SceneRouter.Scenes.Map); else SystemNotice.Instance.Show("敬请期待"); }; }
        _deleteConfirm = GetNode<ConfirmationDialog>("%DeleteConfirm"); _loadConfirm = GetNode<ConfirmationDialog>("%LoadConfirm"); _loadConfirm.AddButton("自动保存后读取", false, "autosave");
        _deleteConfirm.Confirmed += ConfirmDelete; _loadConfirm.Confirmed += () => StartLoad(_loadSlot); _loadConfirm.CustomAction += action => { if (action == "autosave") AutoSaveThenLoad(); };
        for (var i = 1; i <= GameSaveManager.SlotCount; i++) { var slot = i; GetNode<Button>($"Save{i}Save").Pressed += () => RequestLoad(slot); GetNode<Button>($"Save{i}Delete").Pressed += () => RequestDelete(slot); }
        GetNode<Button>("%AbandonButton").Pressed += Back; RefreshSlots();
    }
    private void RefreshSlots()
    {
        var manager = GameSaveManager.Instance;
        for (var i = 1; i <= GameSaveManager.SlotCount; i++) { GetNode<Button>($"Save{i}").Text = $"存档 {i}\n{manager.Describe(i)}"; var load = GetNode<Button>($"Save{i}Save"); load.Text = "读取"; load.Disabled = !manager.HasSave(i) || manager.SavingBecauseFull; var delete = GetNode<Button>($"Save{i}Delete"); delete.Text = manager.SavingBecauseFull ? "删除并保存" : "删除"; delete.Disabled = !manager.HasSave(i); }
        GetNode<Button>("%AbandonButton").Visible = manager.SavingBecauseFull; GetNode<Label>("%SaveHint").Text = manager.SavingBecauseFull ? "存档已满：删除一个旧存档后保存当前对局，或放弃本局" : "读取或删除已有对局存档";
    }
    private void RequestLoad(int slot)
    {
        if (!GameSaveManager.Instance.HasSave(slot)) return; _loadSlot = slot;
        if (GameSaveManager.Instance.PendingSave == null) { StartLoad(slot); return; }
        _loadConfirm.DialogText = "读取存档会离开当前对局。\n“放弃并读取”不会保存当前进度；“自动保存后读取”会先保存当前对局。"; _loadConfirm.PopupCentered();
    }
    private void AutoSaveThenLoad()
    {
        _loadConfirm.Hide(); var manager = GameSaveManager.Instance; if (manager.PendingSave == null) { StartLoad(_loadSlot); return; } var empty = manager.FirstEmptySlot();
        if (empty > 0) { manager.Write(empty, manager.PendingSave); manager.PendingSave = null; StartLoad(_loadSlot); return; }
        manager.PendingLoadAfterSave = _loadSlot; manager.SavingBecauseFull = true; RefreshSlots(); SystemNotice.Instance.Show("存档已满，请删除一个旧存档以保存当前对局");
    }
    private void RequestDelete(int slot) { if (!GameSaveManager.Instance.HasSave(slot)) return; _deleteSlot = slot; _deleteConfirm.DialogText = $"确定删除存档 {slot} 吗？\n删除后将永远失去该对局信息。"; _deleteConfirm.PopupCentered(); }
    private void ConfirmDelete()
    {
        var manager = GameSaveManager.Instance; manager.Delete(_deleteSlot);
        if (manager.SavingBecauseFull && manager.PendingSave != null) { manager.Write(_deleteSlot, manager.PendingSave); manager.PendingSave = null; manager.SavingBecauseFull = false; if (manager.PendingLoadAfterSave > 0) { var target = manager.PendingLoadAfterSave; manager.PendingLoadAfterSave = 0; StartLoad(target); } else SceneRouter.Instance.GoTo(SceneRouter.Scenes.MainMenu); return; }
        RefreshSlots(); SystemNotice.Instance.Show($"存档 {_deleteSlot} 已删除");
    }
    private void StartLoad(int slot) { var manager = GameSaveManager.Instance; manager.PendingSave = null; manager.SavingBecauseFull = false; manager.PendingLoadSlot = slot; SceneRouter.Instance.LoadAndEnter(SceneRouter.Scenes.Battle); }
    private void Back() { var manager = GameSaveManager.Instance; if (manager.SavingBecauseFull) { manager.PendingSave = null; manager.SavingBecauseFull = false; manager.PendingLoadAfterSave = 0; SceneRouter.Instance.GoTo(SceneRouter.Scenes.MainMenu); } else { manager.ResumePendingSave = manager.PendingSave != null; SceneRouter.Instance.Back(); } }
}
