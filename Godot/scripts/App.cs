using Godot;
using System;
using System.Collections.Generic;

public partial class App : Control
{
    enum Page { Loading, MainMenu, ModeSelect, LevelSelect, Map, Prepare, Battle, DeckSelect, DeckBuilder, Shop, Lab, Settings }
    readonly Dictionary<Page,string> art = new() {
        [Page.Loading]="loading_ui.png", [Page.MainMenu]="main_menu_ui.png", [Page.ModeSelect]="mode_select.png",
        [Page.LevelSelect]="level_select.svg", [Page.Map]="map_ui.png", [Page.Prepare]="prepare_ui.png",
        [Page.Battle]="battle_ui_ecg_wireframe_v3.svg", [Page.DeckSelect]="deck_select.png", [Page.DeckBuilder]="deck_ui.png",
        [Page.Shop]="shop_ui.png", [Page.Lab]="lab_ui.png", [Page.Settings]="settings_ui.png"
    };
    Page current, returnPage; TextureRect background = null!; Control actions = null!; Label status = null!;
    bool shopDone, labDone; int deckCount = 15; Timer loadingTimer = null!; Page loadingTarget;

    public override void _Ready() {
        background = new TextureRect { ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize, StretchMode=TextureRect.StretchModeEnum.Scale };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(background);
        actions = new Control(); actions.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(actions);
        status = new Label { Position=new Vector2(16,16), Size=new Vector2(560,36), MouseFilter=MouseFilterEnum.Ignore };
        status.AddThemeColorOverride("font_color", Colors.White); status.AddThemeFontSizeOverride("font_size", 18); AddChild(status);
        loadingTimer = new Timer { OneShot=true, WaitTime=.45 }; AddChild(loadingTimer); loadingTimer.Timeout += ()=>ShowPage(loadingTarget);
        StartLoading(Page.MainMenu);
    }
    void StartLoading(Page target) { loadingTarget=target; ShowPage(Page.Loading); loadingTimer.Start(); }
    void ShowPage(Page page) {
        current=page; foreach(Node n in actions.GetChildren()) n.QueueFree();
        background.Texture=GD.Load<Texture2D>("res://assets/ui/"+art[page]); status.Text="";
        switch(page) {
            case Page.MainMenu: MainMenu(); break; case Page.ModeSelect: ModeSelect(); break; case Page.LevelSelect: LevelSelect(); break;
            case Page.Map: Map(); break; case Page.Prepare: Prepare(); break; case Page.Battle: Battle(); break;
            case Page.DeckSelect: DeckSelect(); break; case Page.DeckBuilder: DeckBuilder(); break; case Page.Shop: NodePage(Page.Map, true); break;
            case Page.Lab: NodePage(Page.Map, false); break; case Page.Settings: ButtonAt("返回",20,20,110,48,()=>ShowPage(returnPage)); break;
        }
    }
    Button ButtonAt(string text,float x,float y,float w,float h,Action click) {
        var b=new Button { Text=text, Position=new Vector2(x,y), Size=new Vector2(w,h), Modulate=new Color(1,1,1,.92f) };
        b.Pressed += click; actions.AddChild(b); return b;
    }
    void Back(Page p)=>ButtonAt("返回",20,20,100,48,()=>ShowPage(p));
    void Settings()=>ButtonAt("设置",1180,650,90,54,()=>{returnPage=current; ShowPage(Page.Settings);});
    void MainMenu(){ ButtonAt("进入游戏",535,470,210,64,()=>ShowPage(Page.ModeSelect)); Settings(); }
    void ModeSelect(){ Back(Page.MainMenu); ButtonAt("剧情模式",95,180,330,390,()=>ShowPage(Page.LevelSelect)); ButtonAt("其他模式",475,180,330,390,()=>ShowPage(Page.Prepare)); Settings(); }
    void LevelSelect(){ Back(Page.ModeSelect); ButtonAt("选择章节",80,135,260,300,()=>status.Text="已选择剧情章节");
        for(int i=0;i<4;i++){ int slot=i; var b=ButtonAt(i==0?"读取存档（双击图片）":"保存空档",80+i*290,585,240,48,()=>{ if(slot==0) StartLoading(Page.Map); else status.Text=$"已保存至空档 {slot+1}"; }); }
        Settings(); }
    void Map(){ Back(Page.LevelSelect); ButtonAt("战斗节点",530,250,180,70,()=>ShowPage(Page.Prepare)); var s=ButtonAt(shopDone?"商店（已完成）":"商店节点",260,390,180,65,()=>{ if(!shopDone){status.Text="已消耗1次地图行动"; ShowPage(Page.Shop);} }); s.Disabled=shopDone;
        var l=ButtonAt(labDone?"研发（已完成）":"研发节点",840,390,180,65,()=>{if(!labDone){status.Text="已消耗1次地图行动"; ShowPage(Page.Lab);}}); l.Disabled=labDone; Settings(); }
    void NodePage(Page back,bool shop){ ButtonAt("返回并完成节点",20,20,210,50,()=>{if(shop)shopDone=true;else labDone=true;ShowPage(back);}); }
    void Prepare(){ Back(Page.Map); for(int i=0;i<5;i++){int n=i; ButtonAt($"英雄 {i+1}\n选择/装备",110+i*205,200,175,120,()=>status.Text=$"英雄 {n+1} 已选择；再次操作可指定队长/装备");}
        ButtonAt("指定队长",120,355,180,50,()=>status.Text="已指定当前英雄为队长"); ButtonAt("选择卡组",820,450,190,58,()=>ShowPage(Page.DeckSelect)); ButtonAt("开始战斗",1020,635,230,60,()=>StartLoading(Page.Battle)); Settings(); }
    void DeckSelect(){ Back(Page.Prepare); ButtonAt("编辑 / 新建卡组",90,150,300,390,()=>ShowPage(Page.DeckBuilder)); ButtonAt("确认选择",1030,640,210,55,()=>ShowPage(Page.Prepare)); }
    void DeckBuilder(){ Back(Page.DeckSelect); ButtonAt("减少卡牌",100,590,180,50,()=>{deckCount=Math.Max(0,deckCount-1);status.Text=$"当前 {deckCount}/15";}); ButtonAt("增加卡牌",300,590,180,50,()=>{deckCount++;status.Text=$"当前 {deckCount}/15";}); ButtonAt("保存卡组",1030,640,210,55,()=>{if(deckCount==15)ShowPage(Page.DeckSelect);else status.Text=$"必须恰好15张，当前 {deckCount}/15";}); status.Text=$"当前 {deckCount}/15"; }
    void Battle(){ ButtonAt("结束战斗",1060,620,190,65,()=>StartLoading(Page.Map)); Settings(); }
}
