using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AgDns {
 public sealed partial class MainForm:Form {
  sealed class ProfileItem{public string Name,Label,Template,Slot;public string[] Servers;public bool Custom,Experimental;
   public ProfileSpec ToSpec()=>new ProfileSpec{Id=Name,Slot=Slot,Template=Template,Servers=Servers,Custom=Custom,Experimental=Experimental};}
  const int W=380,H=556,HeaderH=10,NavH=58,PageBottom=H-NavH-18;
  const int TabBridge=0,TabBench=1,TabSettings=2,TabLogs=3,TabInfo=4;
  const string TgUser="your_sister_loves_me",PatcherTgUser="avencoresyt",PatcherUrl="https://sourceforge.net/projects/open-antigravity-patcher/",Credit="@narechiyaprincess";
  [DllImport("uxtheme.dll",CharSet=CharSet.Unicode)]static extern int SetWindowTheme(IntPtr hwnd,string app,string ids);
  [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr hwnd,int attr,ref int value,int size);

  readonly bool smoke;bool hideOnStart;
  readonly AppSettings settings;   // a render run and the self-test start from defaults: the owner's own choices (browser, own provider, profile) must not change what they check
  readonly List<ProfileItem> profiles=new List<ProfileItem>();
  readonly Panel[] pages=new Panel[NavBar.Count];
  readonly Label statusLabel=new Label(),subLabel=new Label(),benchStatus=new Label(),autostartNote=new Label();
  // The texts under the logo are kept as functions, so a change of language writes them again in the new language (no engine call, no state change).
  Func<string> titleFn,detailFn,benchFn;
  string statusDetail=>detailFn?.Invoke();   // when set, replaces the profile line under the status (message text + one action from the code table)
  void Title(Func<string> f){titleFn=f;statusLabel.Text=f();}
  void Detail(Func<string> f){detailFn=f;}
  CodeInfo SwitchOfferStatus(string from,string to){Title(()=>CodeTable.Lookup("PROVIDER_SWITCH_OFFERED").Title);Detail(()=>CodeTable.SwitchOfferText(from,to)+"\n"+CodeTable.Lookup("PROVIDER_SWITCH_OFFERED").Action);return CodeTable.Lookup("PROVIDER_SWITCH_OFFERED");}
  CodeInfo StatusInfo(string code){Title(()=>CodeTable.Lookup(code).Title);Detail(()=>{var i=CodeTable.Lookup(code);return i.Text+"\n"+i.Action;});return CodeTable.Lookup(code);}
  void BenchStatus(Func<string> f){benchFn=f;benchStatus.Text=f();}
  string addrSource="";   // Source of the active bridge addresses, shown in «Подробнее».
  readonly LogoBox bigLogo=new LogoBox();
  readonly Font bigFont=Theme.F(11.5f,true),smallFont=Theme.F(10,true);
  readonly PillButton bridge=new PillButton(),diagBtn=new PillButton(),copyBtn=new PillButton(),clearBtn=new PillButton(),saveCustom=new PillButton(),delCustom=new PillButton(),findIp=new PillButton(),benchRun=new PillButton(),benchBest=new PillButton(),customClose=new PillButton();
  // Scope of the bridge on the main page (Antigravity / + Gemini / all of Google) and the language switch in the settings.
  readonly Segmented scopeSel=new Segmented(),langSel=new Segmented();
  readonly LinkRow customRow=new LinkRow(),tgRow=new LinkRow(),patcherRow=new LinkRow(),patcherTgRow=new LinkRow();
  RoundPanel customPanel,settingsCardA,settingsCardB;bool scopeUpdating;readonly PathLabel prevCopy=new PathLabel();readonly PillButton prevDelete=new PillButton();readonly LinkRow browserRow=new LinkRow(),langRow=new LinkRow();
  readonly PillSelect profileSelect=new PillSelect();
  // Ping of the working bridge (shown in the profile field): measured in the background, read-only; never while an operation runs.
  SpeedResult speed;bool speedRunning;string speedKey;
  // Pings of the profiles the list measured (by DoH template): read-only, in the background, at most once per BridgeSpeed.Fresh; shown for a few minutes.
  readonly Dictionary<string,SpeedResult> pings=new Dictionary<string,SpeedResult>(StringComparer.OrdinalIgnoreCase);bool pingsRunning;
  static readonly TimeSpan PingShown=TimeSpan.FromMinutes(5);string[] activeServers=new string[0];string activeTemplate;
  internal Func<string,string[],CancellationToken,Task<SpeedResult>> SpeedSource;   // test seam; null = the live probe (never in smoke)
  // "Подробнее": one in-window panel for provider notes, the last operation's text/action and the manual recovery steps (instead of tooltips).
  RoundPanel detailsPanel;TextBox detailsBox;Label detailsTitle;readonly PillButton detailsClose=new PillButton(),detailsCopy=new PillButton();
  LinkLabel detailsLink;string lastCode="",lastInstruction;bool checksPassed,recoveryNeeded;
  readonly ToggleRow tgAutostart=new ToggleRow(),tgCloseTray=new ToggleRow(),tgQuiet=new ToggleRow(),tgNotify=new ToggleRow();
  HealthOptions healthOptions;Label infoText;
  readonly TextBox logBox=new TextBox();
  readonly NavBar nav=new NavBar();
  readonly BenchTable bench=new BenchTable();
  readonly System.Windows.Forms.Timer benchTimer=new System.Windows.Forms.Timer{Interval=400};
  NotifyIcon tray;ToolStripMenuItem trayToggle,trayStatus,trayFix,trayOpen,trayExit;Label versionLabel;string detailsProfile,detailsExtra;Icon iconOn,iconOff,trayOn,trayOff;
  // 1.5-1 tray health check (read-only, no UAC; "Починить" is always the user's click) and 1.5-2 support report.
  readonly PillButton fixBtn=new PillButton(),reportBtn=new PillButton();
  // another catalog profile that passed the check when the current one has no usable address; offered, never applied by itself.
  SwitchOffer offer;readonly PillButton altBtn=new PillButton();
  HealthScheduler healthSched;HealthMonitor healthMon;HealthReport health,lastCheck;System.Windows.Forms.Timer healthTimer;CancellationTokenSource healthCts;bool healthRunning;string lastNotifiedCode;
  OperationHistory history;
  (int Code,string Output) lastDiagnosis=(-1,null);
  CancellationTokenSource currentCts,benchCts;
  string last="";bool busy,needsRestore,known,isActive,exiting,autostartUpdating,benchRunning,repointed;DateTime lastStatus=DateTime.MinValue,benchStarted,benchCooldownUntil=DateTime.MinValue;
  BenchRun lastBench;bool lastBenchStale;int benchDone;

  internal bool Busy=>busy;
  // Test seams: the journal source and read access to the recovery state of the main page.
  internal Func<string> JournalReader=()=>Recovery.ReadJournal();
  internal void PresentForTest(string engineOutput)=>Present(engineOutput);
  // The user's own operation settles the state: a stale drift (or a disconnect the automation would undo) must not linger.
  void Settle(string engineOutput){offer=null;health=null;lastNotifiedCode=null;lastCheck=null;Present(engineOutput);}
  internal void SettleForTest(string engineOutput)=>Settle(engineOutput);
  internal bool BridgeActive=>isActive;
  internal bool BridgeEnabled=>bridge.Enabled;
  internal string BridgeText=>bridge.Text;
  internal bool RecoveryNeeded=>recoveryNeeded;
  internal string StatusText=>statusLabel.Text;
  internal string LastInstruction=>lastInstruction;
  internal void SetBusyForTest(bool value)=>SetBusy(value);
  internal Control AutostartControl=>tgAutostart;
  internal void SelectTab(int i){nav.Selected=i;}
  internal int TabCount=>NavBar.Count;

  ProfileItem Current=>profiles[Math.Max(0,Math.Min(profiles.Count-1,profileSelect.SelectedIndex))];

  void BuildProfiles(){
   profiles.Clear();
   foreach(var p in ProfileCatalog.BuiltIn.Profiles)profiles.Add(new ProfileItem{Name=p.Id,Label=p.Label+(p.Experimental?L.T("  ·  эксп."):""),Slot=p.Slot,Servers=p.Servers,Template=p.Template,Experimental=p.Experimental});
   if(TryParseCustom(settings.CustomName,settings.CustomServers,settings.CustomTemplate,out var cs,out var ct,out _,out var cn))
    profiles.Add(new ProfileItem{Name=cn,Label="★  "+cn+L.T("  ·  свой"),Servers=cs,Template=ct,Custom=true});
   profileSelect.Items.Clear();foreach(var p in profiles)profileSelect.Items.Add(p.Label);
   RebuildBenchRows();
  }
  // a custom profile needs only the DoH URL. The name defaults to the host; IPs are an optional advanced field (without them the addresses are found at connect time).
  internal static bool TryParseCustom(string name,string servers,string template,out string[] srv,out string tmpl,out string error,out string finalName){
   srv=null;tmpl=null;error=null;finalName=null;
   if(!Uri.TryCreate((template??"").Trim(),UriKind.Absolute,out var uri)||uri.Scheme!=Uri.UriSchemeHttps||IPAddress.TryParse(uri.IdnHost,out _)){error=L.T("DoH-ссылка должна начинаться с https:// и содержать имя хоста, а не IP.");return false;}
   string nm=string.IsNullOrWhiteSpace(name)?uri.IdnHost:name.Trim();
   if(nm.Length>28){error=L.T("Название не должно быть длиннее 28 символов.");return false;}
   var parts=(servers??"").Split(new[]{',',';',' ','\r','\n'},StringSplitOptions.RemoveEmptyEntries);
   if(parts.Length>4){error=L.T("Можно указать не больше 4 IP-адресов.");return false;}
   var list=new List<string>();
   foreach(var p in parts){if(!IPAddress.TryParse(p.Trim('[',']'),out var ip)||!Program.PublicIp(ip.ToString())){error=L.T("Неверный или непубличный IP-адрес: ")+p;return false;}list.Add(ip.ToString());}
   srv=list.ToArray();tmpl=uri.AbsoluteUri;finalName=nm;return true;
  }

  Label MakeLabel(string text,Color color,Font font,Rectangle r,ContentAlignment a=ContentAlignment.TopLeft){return new Label{Text=text,ForeColor=color,Font=font,BackColor=Color.Transparent,AutoSize=false,Bounds=r,TextAlign=a};}
  RoundPanel Card(Panel page,int x,int y,int w,int h){var c=new RoundPanel{Bounds=new Rectangle(x,y,w,h)};page.Controls.Add(c);return c;}
  TextBox Field(RoundPanel card,string label,int y,string value,int x,int fw){
   card.Controls.Add(MakeLabel(label,Theme.Muted,Theme.F(8f),new Rectangle(20+x,y,fw,15)));
   var box=new RoundPanel{Fill=Theme.Field,Radius=10,Bounds=new Rectangle(20+x,y+16,fw,30),Border=Theme.Line};card.Controls.Add(box);
   var tb=new TextBox{BorderStyle=BorderStyle.None,BackColor=Theme.Field,ForeColor=Theme.Text,Font=Theme.F(9.5f),Bounds=new Rectangle(11,6,fw-22,18),Text=value??""};box.Controls.Add(tb);return tb;
  }
  void Btn(Control parent,PillButton b,string text,Rectangle r,Color fill,Action onClick){b.Text=text;b.Bounds=r;b.SetFill(fill);parent.Controls.Add(b);b.Click+=(_,__)=>{try{onClick();}catch(Exception e){MessageBox.Show(this,e.Message,Text);}};}
  void BtnAsync(Control parent,PillButton b,string text,Rectangle r,Color fill,Func<Task> onClick){b.Text=text;b.Bounds=r;b.SetFill(fill);parent.Controls.Add(b);b.Click+=async(_,__)=>{try{await onClick();}catch(Exception e){MessageBox.Show(this,e.Message,Text);}};}

  public MainForm(bool smoke=false,bool startHidden=false) {
   this.smoke=smoke;settings=smoke?new AppSettings{NoSave=true}:AppSettings.Load();UiDiag.Enabled=!smoke;hideOnStart=startHidden&&!smoke&&settings.StartHidden;
   if(!smoke){Program.Google=settings.Google;Program.Gemini=settings.Gemini||settings.Google;L.En=settings.Language=="en";}
   Motion.Enabled=!smoke&&SystemInformation.UIEffectsEnabled;   // smoke renders and self-tests see final states; Windows "Show animations" off = no motion
   history=new OperationHistory(smoke?Path.Combine(Path.GetTempPath(),"agdns-smoke-history-"+Guid.NewGuid().ToString("N")+".json"):null);
   SuspendLayout();
   AutoScaleDimensions=new SizeF(96f,96f);AutoScaleMode=AutoScaleMode.Dpi;
   Text="Antigravity DNS";FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;ClientSize=new Size(W,H);BackColor=Theme.Bg;ForeColor=Theme.Text;Font=Theme.F(10);StartPosition=FormStartPosition.CenterScreen;DoubleBuffered=true;
   iconOn=Branding.IconSet(true);iconOff=Branding.IconSet(false);Icon=iconOff;

   // No page has a header: the bar below says where you are, and the logo is on the main page and in the window icon.
   for(int i=0;i<pages.Length;i++){pages[i]=new Panel{BackColor=Theme.Bg};Controls.Add(pages[i]);}
   nav.Bounds=new Rectangle(14,H-NavH-8,W-28,NavH);Controls.Add(nav);
   BuildProfiles();BuildMain(pages[TabBridge]);BuildBench(pages[TabBench]);BuildSettings(pages[TabSettings]);BuildLogs(pages[TabLogs]);BuildInfo(pages[TabInfo]);BuildDetails();BuildCustomPanel();BuildBrowserPanel();
   if(smoke)BrowserEnv=SmokeBrowsers();
   ApplyTab();

   int idx=profiles.FindIndex(p=>p.Name==settings.Profile);profileSelect.SetSelectedSilently(idx<0?0:idx);UpdateDetailsText();
   profileSelect.SelectedIndexChanged+=(_,__)=>{offer=null;settings.Profile=Current.Name;settings.Save();UpdateDetailsText();};
   nav.SelectedChanged+=(_,__)=>ApplyTab();

   if(!smoke)repointed=AutostartHelper.RepointIfMissing(Environment.ProcessPath);   // an entry whose file is gone is quietly pointed at this copy; no question
   try{autostartUpdating=true;tgAutostart.Checked=AutostartHelper.IsEnabled();}catch{tgAutostart.Checked=false;}finally{autostartUpdating=false;}
   UpdateAutostartNote();
   tgCloseTray.Checked=settings.CloseToTray;tgQuiet.Checked=settings.StartHidden;tgNotify.Checked=settings.Notify;scopeSel.SelectedIndex=(int)ScopeNow;langSel.SelectedIndex=L.En?1:0;
   tgAutostart.CheckedChanged+=(_,__)=>OnAutostartToggled();
   tgCloseTray.CheckedChanged+=(_,__)=>{settings.CloseToTray=tgCloseTray.Checked;settings.Save();};
   tgQuiet.CheckedChanged+=(_,__)=>{settings.StartHidden=tgQuiet.Checked;settings.Save();};
   tgNotify.CheckedChanged+=(_,__)=>{settings.Notify=tgNotify.Checked;settings.Save();};
   scopeSel.SelectedIndexChanged+=(_,__)=>OnScopeSelected();
   langSel.SelectedIndexChanged+=(_,__)=>OnLanguageSelected();
   benchTimer.Tick+=(_,__)=>UpdateBenchStatus();
   bench.RowClicked+=r=>ShowDetails(r.Id,bench.Describe(r));
   LoadBenchHistory();

   ResumeLayout(false);
   if(smoke)known=true;
   UpdateButtons();
   if(!smoke){
    InitTray();Log("Приложение запущено");if(repointed)Log("Файл автозапуска не найден: автозапуск переведён на эту копию.");ShowJournalHint();InitHealth();RestoreWindowPosition();
    var t=new System.Windows.Forms.Timer{Interval=50};t.Tick+=async(_,__)=>{t.Stop();t.Dispose();await RefreshStatus();};t.Start();
    Activated+=async(_,__)=>{if(!busy&&(DateTime.UtcNow-lastStatus).TotalSeconds>20)await RefreshStatus();};
   }else{known=true;Title(()=>L.T("Мост отключён"));Log("Готов к работе");Log("Профиль: "+Current.Name);}
   FormClosing+=OnFormClosing;
  }

  void ApplyTab(){
   int sel=nav.Selected;if(detailsPanel!=null)detailsPanel.Visible=false;if(customPanel!=null)customPanel.Visible=false;if(browserPanel!=null)CloseBrowserPanel(false);
   float s=LayoutScale;   // layout units were scaled by AutoScale after construction
   for(int i=0;i<pages.Length;i++){
    pages[i].Visible=i==sel;
    pages[i].Bounds=i==TabBridge?new Rectangle(0,0,(int)Math.Round(W*s),(int)Math.Round(PageBottom*s)):new Rectangle(0,(int)Math.Round(HeaderH*s),(int)Math.Round(W*s),(int)Math.Round((PageBottom-HeaderH)*s));
   }
  }

  // ---- pages ----
  void BuildMain(Panel pg){
   // Top to bottom: logo, state, one line of context (or the failure text), the one main button, the profile field (with the ping),
   // what goes through the bridge, and the secondary action.
   bigLogo.Bounds=new Rectangle((W-LogoS)/2,LogoY,LogoS,LogoS);pg.Controls.Add(bigLogo);
   statusLabel.Bounds=new Rectangle(0,StatusY,W,30);statusLabel.Font=Theme.F(16,true);statusLabel.ForeColor=Theme.Text;statusLabel.BackColor=Color.Transparent;statusLabel.TextAlign=ContentAlignment.MiddleCenter;Title(()=>L.T("Проверяю мост…"));pg.Controls.Add(statusLabel);
   subLabel.Bounds=new Rectangle(24,StatusY+32,W-48,46);subLabel.Font=Theme.F(9f);subLabel.ForeColor=Theme.Muted;subLabel.BackColor=Color.Transparent;subLabel.TextAlign=ContentAlignment.TopCenter;subLabel.UseMnemonic=false;pg.Controls.Add(subLabel);
   bridge.Font=bigFont;fixBtn.Font=bigFont;altBtn.Font=Theme.F(10.5f,true);   // the offer names a profile: a size smaller so the longest name fits
   bridge.Glyph="\uE7E8";fixBtn.Glyph="\uE90F";altBtn.Glyph="\uE8AB";
   BtnAsync(pg,bridge,L.T("Подключить мост"),new Rectangle(70,MainY,W-140,MainH),Theme.Accent,async()=>{if(needsRestore)await Mutate("restore");else await Mutate("enable");});
   pg.Controls.Add(profileCaption);profileCaption.Text=L.T("Профиль");profileCaption.Bounds=new Rectangle(30,ProfileCapY,300,CapH);profileCaption.ForeColor=Theme.Muted;profileCaption.Font=Theme.F(8f);profileCaption.BackColor=Color.Transparent;profileCaption.AutoSize=false;
   profileSelect.Font=Theme.F(9.5f);profileSelect.Bounds=new Rectangle(24,ProfileY,W-48,FieldH);pg.Controls.Add(profileSelect);
   profileSelect.Detail=PingDetail;profileSelect.DropDownOpening+=(_,__)=>{_=MeasureProfilesAsync(false);};
   pg.Controls.Add(scopeCaption);scopeCaption.Text=L.T("Через мост");scopeCaption.Bounds=new Rectangle(30,ScopeCapY,300,CapH);scopeCaption.ForeColor=Theme.Muted;scopeCaption.Font=Theme.F(8f);scopeCaption.BackColor=Color.Transparent;scopeCaption.AutoSize=false;
   scopeSel.Cumulative=true;scopeSel.Items=new[]{"Antigravity","+ Gemini Desktop",L.T("+ Браузер")};   // product names are not translated
   scopeSel.Tips=new[]{L.T("Только домены Antigravity."),L.T("Antigravity, Gemini Desktop и сайт gemini.google.com."),L.T("Все домены Google: Gemini, NotebookLM, AI Studio, поиск, почта и остальные сервисы.")};
   scopeSel.Bounds=new Rectangle(24,ScopeY,W-48,FieldH);pg.Controls.Add(scopeSel);
   BtnAsync(pg,fixBtn,L.T("Починить"),new Rectangle(70,MainY,W-140,MainH),Theme.Accent,async()=>{if(!busy&&known)await Mutate("enable",true);});fixBtn.Visible=false;
   BtnAsync(pg,altBtn,L.T("Подключить через …"),new Rectangle(70,MainY,W-140,MainH),Theme.Accent,async()=>{await SwitchToOfferedAsync();});altBtn.Visible=false;
   // Only when there is something to read: a failure, an unfinished operation or manual steps. A working bridge needs no "more".
   detailsLink=new LinkLabel{Text=L.T("Подробнее"),AutoSize=false,Bounds=new Rectangle(0,SecondY+SecondH+2,W,18),TextAlign=ContentAlignment.MiddleCenter,LinkColor=Theme.AccentHover,ActiveLinkColor=Theme.Accent,VisitedLinkColor=Theme.AccentHover,LinkBehavior=LinkBehavior.HoverUnderline,BackColor=Color.Transparent,Font=Theme.F(8.5f),Visible=false};
   detailsLink.LinkClicked+=(_,__)=>ShowDetails(null);pg.Controls.Add(detailsLink);
  }
  // One dense block under the logo: the main button, the profile field, the levels; each field has its caption right above it, with equal gaps.
  // The gap G is the same above the profile caption, above the levels' caption and above the secondary button; a caption sits CapGap above its field.
  const int LogoY=22,LogoS=100,StatusY=126,MainY=216,MainH=46,MainMinW=236,G=18,CapH=14,CapGap=3,FieldH=38,ProfileCapY=MainY+MainH+G,ProfileY=ProfileCapY+CapH+CapGap,ScopeCapY=ProfileY+FieldH+G,ScopeY=ScopeCapY+CapH+CapGap,SecondY=ScopeY+FieldH+14,SecondH=34;
  readonly Label profileCaption=new Label(),scopeCaption=new Label();

  // ---- benchmark tab ----
  readonly List<BenchRow> benchRows=new List<BenchRow>();
  void BuildBench(Panel pg){
   int pageH=PageBottom-HeaderH;
   bench.Bounds=new Rectangle(14,2,W-28,0);pg.Controls.Add(bench);
   // height is fixed after profiles are known (RebuildBenchRows)
   Btn(pg,benchRun,L.T("Измерить"),new Rectangle(14,pageH-84,176,42),Theme.Accent,()=>{var _=ToggleBenchmark();});
   Btn(pg,benchBest,L.T("Выбрать лучший"),new Rectangle(198,pageH-84,W-28-184,42),Theme.Soft,SelectBestProfile);
   benchStatus.Bounds=new Rectangle(18,pageH-36,W-36,34);benchStatus.Font=Theme.F(8.5f);benchStatus.ForeColor=Theme.Muted;benchStatus.BackColor=Color.Transparent;BenchStatus(()=>L.T("Замер ничего не меняет в системе: только DoH-запросы и проверка TLS."));pg.Controls.Add(benchStatus);
   RebuildBenchRows();
  }
  void RebuildBenchRows(){
   var old=benchRows.ToDictionary(r=>r.Id,r=>r.Result);
   benchRows.Clear();
   foreach(var p in profiles){
    var spec=ProfileCatalog.BuiltIn.Find(p.Name);
    string note=spec==null||p.Custom?null:string.Join("\n",new[]{spec.Experimental?L.T("Экспериментальный профиль: не рекомендуется, пока замер не покажет «Работает»."):null,spec.KnownIssue,spec.Note}.Where(x=>!string.IsNullOrWhiteSpace(x)));
    benchRows.Add(new BenchRow{Id=p.Name,Name=p.Name,Slot=p.Slot,CatalogNote=string.IsNullOrEmpty(note)?null:note,Result=old.TryGetValue(p.Name,out var r)?r:null});
   }
   bench.Rows.Clear();bench.Rows.AddRange(benchRows);
   int pageH=PageBottom-HeaderH;
   bench.Height=pageH-104;   // the table takes the page; its rows share the height (layout units; AutoScale handles DPI)
   benchRun.Top=benchBest.Top=bench.Bottom+10;benchStatus.Top=benchRun.Bottom+8;
   bench.Invalidate();
  }
  void LoadBenchHistory(){
   try{
    var runs=Benchmark.LoadHistory();var last=runs.LastOrDefault(r=>!r.Synthetic);
    if(last==null)return;
    // A measurement from an earlier session is shown with its date and never offered as a fresh recommendation.
    lastBench=last;lastBenchStale=true;foreach(var r in last.Results)r.Best=false;ApplyBenchResults(last);
    string when=DateTime.TryParse(last.StartedUtc,null,System.Globalization.DateTimeStyles.RoundtripKind,out var dt)?dt.ToLocalTime().ToString("dd.MM.yyyy HH:mm"):"?";
    BenchStatus(()=>L.T("Замер от ")+when+L.T(" (прошлый запуск). Сеть могла измениться: нажмите «Измерить»."));
    benchBest.Enabled=false;
   }catch{}
  }
  void ApplyBenchResults(BenchRun run){
   foreach(var row in benchRows){var r=run.Results.FirstOrDefault(x=>x.Id==row.Id);row.Result=r;row.Running=false;row.Note=r==null?L.T("не измерялся"):null;}
   var ordered=benchRows.OrderBy(r=>r.Result==null?int.MaxValue:(r.Result.Rank==0?int.MaxValue-1:r.Result.Rank)).ToList();
   bench.Rows.Clear();bench.Rows.AddRange(ordered);bench.Invalidate();
  }
  async Task ToggleBenchmark(){
   if(benchRunning){benchCts?.Cancel();BenchStatus(()=>L.T("Отменяю…"));return;}
   if(DateTime.UtcNow<benchCooldownUntil){int wait=Math.Max(1,(int)(benchCooldownUntil-DateTime.UtcNow).TotalSeconds+1);BenchStatus(()=>L.T("Повторный замер через ")+wait+L.T(" с."));return;}
   benchRunning=true;benchCts=new CancellationTokenSource();benchStarted=DateTime.UtcNow;benchDone=0;
   foreach(var row in benchRows){row.Result=null;row.Running=true;row.Note=null;}
   bench.Rows.Clear();bench.Rows.AddRange(benchRows);bench.Progress=0;bench.Invalidate();
   benchTimer.Start();UpdateBenchButtons();UpdateBenchStatus();
   Log("Замер профилей: старт ("+profiles.Count+" профилей)");
   var specs=profiles.Select(p=>p.ToSpec()).ToList();
   var progress=new Progress<BenchProgress>(OnBenchProgress);
   var options=new BenchOptions();BenchRun run=null;
   try{
    using(var live=new LiveTransport())run=await Task.Run(()=>Benchmark.RunAsync(specs,options,live,progress,benchCts.Token));
   }catch(Exception e){Log("Замер не удался: "+e.Message);}
   finally{benchRunning=false;benchTimer.Stop();bench.Progress=-1;benchCooldownUntil=DateTime.UtcNow.AddSeconds(4);}
   if(run!=null){
    lastBench=run;lastBenchStale=false;ApplyBenchResults(run);
    if(run.Results.Any(r=>r.Attempts>0))Benchmark.AppendHistory(run);
    var best=run.Results.FirstOrDefault(r=>r.Best);
    BenchStatus(()=>run.Cancelled?L.T("Замер отменён. Показаны результаты до остановки.")
     :best!=null?L.T("Лучший: ")+best.Id+" · "+best.Score+L.T(" баллов · медиана ")+(best.MedianMs<0?"—":best.MedianMs+L.T(" мс"))+(run.Truncated?L.T(" · не все профили успели (лимит ")+(options.TotalBudgetMs/1000)+L.T(" с)"):"")
     :L.T("Подходящего профиля нет: ни один не прошёл проверку всех доменов на этой сети."));
    Log(benchStatus.Text);
   }
   UpdateBenchButtons();UpdateButtons();
   // brief cooldown so a double click cannot hammer the providers
   var cool=new System.Windows.Forms.Timer{Interval=4000};cool.Tick+=(_,__)=>{cool.Stop();cool.Dispose();UpdateBenchButtons();};cool.Start();
  }
  void OnBenchProgress(BenchProgress p){
   var row=benchRows.FirstOrDefault(r=>r.Id==p.ProfileId);if(row==null)return;
   row.Result=p.Partial;row.Running=!p.ProfileFinished;
   if(p.ProfileFinished){benchDone=p.Done;bench.Progress=profiles.Count==0?0:(float)benchDone/profiles.Count;}
   bench.Invalidate();
  }
  void UpdateBenchStatus(){
   if(!benchRunning)return;
   BenchStatus(()=>L.T("Идёт замер: ")+benchDone+L.T(" из ")+profiles.Count+L.T(" профилей, ")+(int)(DateTime.UtcNow-benchStarted).TotalSeconds+L.T(" с (лимит 30 с). «Отмена» остановит замер."));
  }
  void UpdateBenchButtons(){
   benchRun.Text=benchRunning?L.T("Отмена"):L.T("Измерить");benchRun.SetFill(benchRunning?Theme.Danger:Theme.Accent);
   benchRun.Enabled=benchRunning||DateTime.UtcNow>=benchCooldownUntil;
   bool hasBest=!lastBenchStale&&lastBench!=null&&lastBench.Winner!=null&&profiles.Any(p=>p.Name==lastBench.Winner);
   benchBest.Enabled=!benchRunning&&!busy&&!needsRestore&&hasBest;
  }
  void SelectBestProfile(){
   if(lastBench?.Winner==null)return;
   int i=profiles.FindIndex(p=>p.Name==lastBench.Winner);if(i<0)return;
   profileSelect.SetSelectedSilently(i);settings.Profile=profiles[i].Name;settings.Save();UpdateDetailsText();
   Log("Выбран лучший профиль по замеру: "+profiles[i].Name+" (мост не менялся)");
   nav.Selected=TabBridge;
  }
  // Used by --ui-smoke --states: puts the main page into a given state without touching the system, so each state can be rendered and inspected.
  internal void SmokeState(string mode){
   known=true;detailsPanel.Visible=false;nav.Selected=TabBridge;health=null;offer=null;checking=false;refreshing=false;busy=false;
   switch(mode){
    case "active":needsRestore=true;isActive=true;recoveryNeeded=false;checksPassed=true;Title(()=>L.T("Мост подключён"));Detail(null);addrSource=L.T("Адреса: найдены сейчас, 12.10 15:30");activeTemplate=Current.Template;speed=new SpeedResult{Ms=42,Proxy="203.0.113.10",Utc=DateTime.UtcNow};break;
    case "active-unchecked":needsRestore=true;isActive=true;recoveryNeeded=false;checksPassed=false;Title(()=>L.T("Мост подключён"));Detail(null);addrSource=L.T("Адреса: закреплённые");activeTemplate=Current.Template;speed=null;break;
    case "recovery":{needsRestore=true;isActive=false;recoveryNeeded=true;checksPassed=false;addrSource="";var ri=StatusInfo("RECOVERY_REQUIRED");break;}
    case "conflict":{needsRestore=false;isActive=false;recoveryNeeded=false;checksPassed=false;addrSource="";var ci=StatusInfo("NRPT_OR_HOSTS_CONFLICT");break;}
    case "drift":{needsRestore=true;isActive=false;recoveryNeeded=false;checksPassed=false;addrSource="";health=new HealthReport{Kind=HealthKind.Drift,Code=HealthMonitor.ProviderDown,CanFix=true,Utc=DateTime.UtcNow};var di=StatusInfo(HealthMonitor.ProviderDown);break;}
    case "switch":{needsRestore=false;isActive=false;recoveryNeeded=false;checksPassed=false;addrSource="";var pi=profiles.FirstOrDefault(x=>!x.Experimental&&x.Name!=Current.Name)??Current;offer=new SwitchOffer{ProfileId=pi.Name,FromId=Current.Name,Reason="BOOTSTRAP_NO_ADDRESS"};var si=SwitchOfferStatus(Current.Name,pi.Name);break;}
    case "noprovider":{needsRestore=false;isActive=false;recoveryNeeded=false;checksPassed=false;addrSource="";var ni=StatusInfo("NO_WORKING_PROVIDER");break;}
    case "wmi":{needsRestore=false;isActive=false;recoveryNeeded=false;checksPassed=false;addrSource="";var wi=StatusInfo("WMI_NOT_RESPONDING");break;}
    case "checking":needsRestore=true;isActive=false;recoveryNeeded=false;checksPassed=false;checking=true;refreshing=true;Title(()=>L.T("Мост подключён"));Detail(null);addrSource="";break;
    case "busy":needsRestore=false;isActive=false;recoveryNeeded=false;checksPassed=false;busy=true;Title(()=>L.T("Подключаю мост…"));Detail(null);addrSource="";break;
    default:health=null;needsRestore=false;isActive=false;recoveryNeeded=false;checksPassed=false;addrSource="";Title(()=>L.T("Мост отключён"));Detail(null);break;
   }
   UpdateDetailsText();UpdateButtons();Application.DoEvents();
  }
  // DrawToBitmap of the form skips the overlay panel; paint it on top of the form snapshot so the details panel can be inspected like every other state.
  internal void OverlayDetails(Bitmap target){
   foreach(var panel in new[]{detailsPanel,browserPanel}){
    if(panel==null||!panel.Visible)continue;
    using(var pb=new Bitmap(panel.Width,panel.Height)){
     panel.DrawToBitmap(pb,new Rectangle(Point.Empty,pb.Size));
     var origin=PointToScreen(Point.Empty);
     // a button that became visible after the panel was already on screen comes out blank in the panel's own snapshot: paint every button with its own code
     // (on its own bitmap over the panel colour, because text drawing ignores a graphics transform)
     foreach(Control ch in panel.Controls)if(ch is PillButton btn&&ch.Visible)using(var cb=new Bitmap(ch.Width,ch.Height)){
      using(var gc=Graphics.FromImage(cb)){gc.Clear(panel is RoundPanel rp?rp.Fill:Theme.Card);btn.PaintForSnapshot(gc);}
      using(var gp=Graphics.FromImage(pb))gp.DrawImage(cb,ch.Left,ch.Top);
     }
     using(var g=Graphics.FromImage(target))g.DrawImage(pb,origin.X-Left+panel.Left,origin.Y-Top+panel.Top);
    }
   }
  }
  internal void SmokeDetails(string profile,bool withInstruction){
   lastCode=withInstruction?"RESTORE_UNCONFIRMED":"";
   lastInstruction=withInstruction?Recovery.BuildInstruction("{\"Schema\":1,\"Owner\":\""+new string('a',32)+"\",\"Phase\":\"Restoring\",\"Rules\":[{\"Namespace\":\""+Program.Targets[0]+"\",\"Name\":\"{82c33f7e-1111-4222-8333-444455556666}\"},{\"Namespace\":\""+Program.Targets[1]+"\",\"Name\":\"{a99b6302-aaaa-4bbb-8ccc-ddddeeeeffff}\"}],\"Doh\":[{\"Server\":\""+Program.Servers[0]+"\",\"Created\":true,\"Ownership\":\"Owned\"},{\"Server\":\""+Program.Servers[1]+"\",\"Created\":true,\"Ownership\":\"Owned\"}]}"):null;
   ShowDetails(profile);Application.DoEvents();
  }
  // Used by --ui-smoke --bench: runs the real benchmark through the real UI path and pumps messages until it ends.
  internal void RunBenchmarkForSmoke(Func<string,bool> snapshot,int midRunMs){
   nav.Selected=TabBench;Application.DoEvents();
   var t=ToggleBenchmark();var sw=Stopwatch.StartNew();bool midShot=false;
   while(!t.IsCompleted&&sw.ElapsedMilliseconds<60000){
    Application.DoEvents();Thread.Sleep(30);
    if(!midShot&&sw.ElapsedMilliseconds>=midRunMs){midShot=true;snapshot("running");}
   }
   Application.DoEvents();snapshot("done");
  }

  void BuildSettings(Panel pg){
   int cw=W-28,iw=cw-36;
   // Rows in the Windows 11 manner: an icon, what the switch does in one line, a short explanation under it. The own provider and the browser open their own panels.
   var a=settingsCardA=Card(pg,14,4,cw,222);
   a.Controls.Add(MakeLabel(L.T("ЗАПУСК И ТРЕЙ"),Theme.Muted,Theme.F(7.5f,true),new Rectangle(18,12,iw,14)));
   void T(ToggleRow t,IconKind icon,string text,string sub,int y,bool divider){t.Icon=icon;t.Text=text;t.Subtitle=sub;t.Divider=divider;t.Bounds=new Rectangle(18,y,iw,46);a.Controls.Add(t);}
   T(tgAutostart,IconKind.Power,L.T("Запускать при входе в Windows"),L.T("Программа следит за состоянием моста"),30,true);
   T(tgQuiet,IconKind.Tray,L.T("При автозапуске — сразу в трей"),L.T("Без окна, только значок у часов"),76,true);
   T(tgCloseTray,IconKind.Minimize,L.T("Крестик сворачивает в трей"),L.T("Выйти — через меню значка в трее"),122,true);
   T(tgNotify,IconKind.Bell,L.T("Уведомлять о сбоях моста"),L.T("Сообщение, если мост перестал работать"),168,false);
   prevCopy.Divider=true;prevCopy.Visible=false;a.Controls.Add(prevCopy);
   Btn(a,prevDelete,L.T("Удалить"),new Rectangle(18+iw-72,76+(PrevH-26)/2,72,26),Theme.Soft,OnDeletePreviousCopy);prevDelete.Font=Theme.F(8.5f,true);prevDelete.Visible=false;
   var b=settingsCardB=Card(pg,14,238,cw,176);
   b.Controls.Add(MakeLabel(L.T("ПРОГРАММА"),Theme.Muted,Theme.F(7.5f,true),new Rectangle(18,12,iw,14)));
   langRow.Icon=IconKind.Globe;langRow.Text=L.T("Язык");langRow.Subtitle="Русский · English";langRow.Cursor=Cursors.Default;langRow.Bounds=new Rectangle(18,30,iw-158,46);langRow.ShowChevron=false;b.Controls.Add(langRow);
   b.Controls.Add(new Panel{BackColor=Color.FromArgb(36,36,44),Bounds=new Rectangle(58,75,iw-40,1)});   // the row divider, drawn under the selector as well
   langSel.Items=new[]{"Русский","English"};langSel.Bounds=new Rectangle(18+iw-150,38,150,30);b.Controls.Add(langSel);langSel.BringToFront();   // each language named in itself
   customRow.Icon=IconKind.Server;customRow.Text=L.T("Свой DoH-провайдер");customRow.Divider=true;customRow.Bounds=new Rectangle(18,76,iw,46);b.Controls.Add(customRow);UpdateCustomRow();
   customRow.Click+=(_,__)=>ShowCustomPanel();
   browserRow.Icon=IconKind.Compass;browserRow.Text=L.T("Браузер для Google");browserRow.Bounds=new Rectangle(18,122,iw,46);b.Controls.Add(browserRow);UpdateBrowserRow();
   browserRow.Click+=(_,__)=>ShowBrowserPanel(false);
  }
  void UpdateCustomRow(){customRow.Subtitle=string.IsNullOrWhiteSpace(settings.CustomTemplate)?L.T("Не задан: нажмите, чтобы добавить"):(string.IsNullOrWhiteSpace(settings.CustomName)?settings.CustomTemplate:settings.CustomName+" · "+settings.CustomTemplate);}
  // ---- own provider: a panel over the window, like «Подробнее» ----
  void BuildCustomPanel(){
   int pw=W-28,iw=pw-36;
   customPanel=new RoundPanel{Visible=false,Fill=Theme.Card,Radius=18};customPanel.Bounds=new Rectangle(14,10,pw,PageBottom-20);Controls.Add(customPanel);
   customPanel.Controls.Add(MakeLabel(L.T("Свой DoH-провайдер"),Theme.Text,Theme.F(12.5f,true),new Rectangle(18,14,pw-140,26)));
   Btn(customPanel,customClose,L.T("Закрыть"),new Rectangle(pw-18-96,12,96,28),Theme.Soft,()=>{customPanel.Visible=false;});
   customPanel.Controls.Add(MakeLabel(L.T("Хватит DoH-ссылки: адреса провайдера найдутся при подключении. Название и IP — по желанию."),Theme.Muted,Theme.F(8f),new Rectangle(18,48,iw,32)));
   var t2=Field(customPanel,L.T("DoH-ссылка  https://…/dns-query"),86,settings.CustomTemplate,-2,iw);
   var n=Field(customPanel,L.T("Название (необязательно)"),136,settings.CustomName,-2,iw);
   var s=Field(customPanel,L.T("IP-адреса (необязательно)"),186,settings.CustomServers,-2,iw);
   int bw=(iw-12)/3,by=246;
   BtnAsync(customPanel,findIp,L.T("Найти IP"),new Rectangle(18,by,bw,36),Theme.Soft,async()=>{
    if(!Bootstrap.TryParse(t2.Text,out var boot)){MessageBox.Show(this,L.T("Введите DoH-ссылку с именем хоста, например https://dns.example/dns-query."),Text,MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
    findIp.Enabled=false;findIp.Text=L.T("Ищу…");UseWaitCursor=true;JsonElement res;
    try{res=JsonSerializer.SerializeToElement(await Task.Run(()=>Bootstrap.RunAsync(boot.AbsoluteUri)),Program.JsonOptions);}
    catch(Exception ex){MessageBox.Show(this,L.T("Найти IP не удалось: ")+ex.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
    finally{UseWaitCursor=false;findIp.Text=L.T("Найти IP");findIp.Enabled=!busy&&!needsRestore;}
    var lines=new List<string>();var pinned=res.GetProperty("Pinned").EnumerateArray().Select(x=>x.GetString()).ToList();
    foreach(var c in res.GetProperty("Candidates").EnumerateArray()){
     bool tlsOk=c.GetProperty("TlsOnName").GetBoolean(),doh=c.GetProperty("DohBothDomains").GetBoolean();
     lines.Add("  "+c.GetProperty("Ip").GetString()+"  —  "+L.T("TLS по имени: ")+(tlsOk?L.T("ок"):L.T("нет"))+"; "+L.T("DoH для всех доменов: ")+(doh?L.T("ок"):L.T("нет")));
    }
    if(pinned.Count==0){MessageBox.Show(this,L.T("Хост ")+res.GetProperty("Host").GetString()+":\n"+(lines.Count==0?L.T("  публичных IPv4 нет"):string.Join("\n",lines))+"\n\n"+L.T("Ни один адрес не прошёл проверку TLS по имени и DoH для всех доменов. Закреплять нечего."),Text,MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
    var take=pinned.Take(4).ToList();
    var answer=MessageBox.Show(this,L.T("Хост ")+res.GetProperty("Host").GetString()+L.T(" (сверка с системным резолвером, публичными DoH-резолверами и провайдерами каталога):")+"\n"+string.Join("\n",lines)+"\n\n"+L.T("Закрепить прошедшие проверку адреса?")+"\n  "+string.Join(", ",take)+"\n\n"+L.T("Адреса попадут в поле IP, а профиль сохранит кнопка «Сохранить»."),Text,MessageBoxButtons.YesNo,MessageBoxIcon.Question);
    if(answer==DialogResult.Yes){s.Text=string.Join(", ",take);if(string.IsNullOrWhiteSpace(n.Text))n.Text=res.GetProperty("Host").GetString();Log("IP найдены и подтверждены: "+string.Join(", ",take));}
   });
   Btn(customPanel,saveCustom,L.T("Сохранить"),new Rectangle(24+bw,by,bw,36),Theme.Accent,()=>{
    if(!TryParseCustom(n.Text,s.Text,t2.Text,out var srv,out var tmpl,out var err,out var finalName)){MessageBox.Show(this,err,Text,MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
    n.Text=finalName;
    settings.CustomName=finalName;settings.CustomServers=string.Join(", ",srv);settings.CustomTemplate=tmpl;settings.Profile=settings.CustomName;settings.Save();RefreshProfiles(settings.CustomName);UpdateCustomRow();Log("Свой профиль сохранён: "+settings.CustomName);Flash(saveCustom,L.T("Сохранено"));
   });
   Btn(customPanel,delCustom,L.T("Удалить"),new Rectangle(30+2*bw,by,iw-12-2*bw,36),Theme.Soft,()=>{
    settings.CustomName=settings.CustomServers=settings.CustomTemplate="";settings.Profile=ProfileCatalog.BuiltIn.Default;settings.Save();n.Text=s.Text=t2.Text="";RefreshProfiles(ProfileCatalog.BuiltIn.Default);UpdateCustomRow();Log("Свой профиль удалён");
   });
   customPanel.Controls.Add(MakeLabel(L.T("Свой провайдер не входит в проверенные: программа проверит его так же строго (TLS по имени и DoH для всех доменов), но работу не гарантирует."),Theme.Muted,Theme.F(7.5f),new Rectangle(18,by+48,iw,44)));
  }
  void ShowCustomPanel(){
   float sc=LayoutScale;
   customPanel.Bounds=new Rectangle((int)Math.Round(14*sc),(int)Math.Round(10*sc),(int)Math.Round((W-28)*sc),(int)Math.Round((PageBottom-20)*sc));
   customPanel.Visible=true;customPanel.BringToFront();
  }
  internal bool CustomPanelVisible=>customPanel.Visible;
  internal void ShowCustomPanelForTest()=>ShowCustomPanel();
  void RefreshProfiles(string select){
   BuildProfiles();int i=profiles.FindIndex(p=>p.Name==select);profileSelect.SetSelectedSilently(i<0?0:i);UpdateDetailsText();UpdateButtons();UpdateBenchButtons();
  }

  void BuildLogs(Panel pg){
   int pageH=PageBottom-HeaderH;int cw=W-28;
   var c=Card(pg,14,2,cw,pageH-62);
   logBox.Multiline=true;logBox.ReadOnly=true;logBox.ScrollBars=ScrollBars.Vertical;logBox.WordWrap=true;logBox.BorderStyle=BorderStyle.None;logBox.BackColor=Theme.Card;logBox.ForeColor=Color.FromArgb(198,203,214);logBox.Font=new Font(Theme.MonoFont,8.5f);logBox.Bounds=new Rectangle(14,14,cw-28,pageH-62-28);logBox.HandleCreated+=(_,__)=>{try{SetWindowTheme(logBox.Handle,"DarkMode_Explorer",null);}catch{}};c.Controls.Add(logBox);
   BtnAsync(pg,diagBtn,L.T("Диагностика"),new Rectangle(14,pageH-52,140,40),Theme.Accent,async()=>{await Run("diagnose");});
   Btn(pg,copyBtn,L.T("Копировать"),new Rectangle(162,pageH-52,108,40),Theme.Soft,()=>{if(logBox.Text.Length>0){Clipboard.SetText(logBox.Text);Flash(copyBtn,L.T("Скопировано"));}});
   Btn(pg,clearBtn,L.T("Очистить"),new Rectangle(278,pageH-52,cw-264,40),Theme.Soft,()=>{logBox.Clear();});
  }

  void BuildInfo(Panel pg){
   int cw=W-28,iw=cw-36;
   // About (who, version, how it works), the patcher Russian accounts need, and one way to reach the author.
   var a=Card(pg,14,2,cw,InfoCardH);
   var logo=new LogoBox{Bounds=new Rectangle(18,16,50,50)};a.Controls.Add(logo);
   a.Controls.Add(MakeLabel("Antigravity DNS",Theme.Text,Theme.F(12.5f,true),new Rectangle(80,15,cw-96,24)));
   a.Controls.Add(MakeLabel("by "+Credit,Theme.AccentHover,Theme.F(8.5f,true),new Rectangle(80,39,cw-96,16)));
   versionLabel=MakeLabel(L.T("Версия")+" "+AppInfo.Display+"  ·  Windows x64",Theme.Muted,Theme.F(8f),new Rectangle(80,56,cw-96,15));a.Controls.Add(versionLabel);
   infoText=MakeLabel(InfoTextFor(ScopeNow),Theme.Muted,Theme.F(9f),new Rectangle(14,80,cw-28,InfoTextH));infoText.UseMnemonic=false;a.Controls.Add(infoText);
   a.Controls.Add(MakeLabel(L.T("Остальные сайты и VPN не меняются; при отключении удаляются только правила программы."),Theme.Muted,Theme.F(8f),new Rectangle(14,80+InfoTextH+2,cw-28,32)));
   int by=2+InfoCardH+8;
   var b=Card(pg,14,by,cw,152);
   b.Controls.Add(MakeLabel("ANTIGRAVITY PATCHER",Theme.Muted,Theme.F(7.5f,true),new Rectangle(18,12,iw,14)));
   b.Controls.Add(MakeLabel(L.T("Российским аккаунтам для стабильной работы нужен ещё Antigravity Patcher."),Theme.Muted,Theme.F(8f),new Rectangle(18,28,iw,32)));
   patcherRow.Icon=IconKind.Download;patcherRow.Text=L.T("Скачать Antigravity Patcher");patcherRow.Subtitle="sourceforge.net";patcherRow.Divider=true;patcherRow.Bounds=new Rectangle(18,62,iw,44);b.Controls.Add(patcherRow);
   patcherRow.Click+=(_,__)=>OpenUrl(PatcherUrl);
   patcherTgRow.Kind=LinkRow.Mark.Telegram;patcherTgRow.Text="@"+PatcherTgUser;patcherTgRow.Subtitle=L.T("Автор патчера — если с ним что-то не так");patcherTgRow.Bounds=new Rectangle(18,106,iw,44);b.Controls.Add(patcherTgRow);
   patcherTgRow.Click+=(_,__)=>OpenTelegram(PatcherTgUser);
   var c=Card(pg,14,by+152+8,cw,76);
   c.Controls.Add(MakeLabel(L.T("СВЯЗАТЬСЯ"),Theme.Muted,Theme.F(7.5f,true),new Rectangle(18,12,iw,14)));
   tgRow.Kind=LinkRow.Mark.Telegram;tgRow.Text="@"+TgUser;tgRow.Subtitle=L.T("Автор Antigravity DNS");tgRow.Bounds=new Rectangle(18,28,iw,44);c.Controls.Add(tgRow);
   tgRow.Click+=(_,__)=>OpenTelegram(TgUser);
  }
  internal const int InfoCardH=220,InfoTextH=98;

  // ---- "Подробнее": provider notes, last operation, manual recovery steps (replaces long tooltips) ----
  void BuildDetails(){
   detailsPanel=new RoundPanel{Visible=false,Fill=Theme.Card,Radius=18};
   detailsPanel.Bounds=new Rectangle(14,10,W-28,PageBottom-20);Controls.Add(detailsPanel);
   int pw=W-28;
   detailsTitle=MakeLabel(L.T("Подробнее"),Theme.Text,Theme.F(12.5f,true),new Rectangle(18,12,pw-140,26));detailsPanel.Controls.Add(detailsTitle);
   Btn(detailsPanel,detailsClose,L.T("Закрыть"),new Rectangle(pw-18-96,12,96,28),Theme.Soft,()=>{detailsPanel.Visible=false;});
   detailsBox=new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,WordWrap=true,BorderStyle=BorderStyle.None,BackColor=Theme.Card,ForeColor=Theme.Text,Font=Theme.F(9f),Bounds=new Rectangle(18,48,pw-36,PageBottom-20-48-58),TabStop=false};
   detailsBox.HandleCreated+=(_,__)=>{try{SetWindowTheme(detailsBox.Handle,"DarkMode_Explorer",null);}catch{}};
   detailsPanel.Controls.Add(detailsBox);
   int half=(pw-36-10)/2;
   Btn(detailsPanel,detailsCopy,L.T("Копировать"),new Rectangle(18,PageBottom-20-48,half,36),Theme.Accent,()=>{if(detailsBox.Text.Length>0){Clipboard.SetText(detailsBox.Text);Flash(detailsCopy,L.T("Скопировано"));}});
   BtnAsync(detailsPanel,reportBtn,L.T("Отчёт для поддержки"),new Rectangle(18+half+10,PageBottom-20-48,pw-36-half-10,36),Theme.Soft,async()=>{await SupportReportAsync();});
  }
  internal void ShowDetails(string profileName,string extra=null){
   detailsProfile=profileName;detailsExtra=extra;
   var p=profileName==null?Current:(profiles.FirstOrDefault(x=>x.Name==profileName)??Current);
   var sb=new System.Text.StringBuilder();
   sb.Append(L.T("Профиль: ")+p.Name+(p.Experimental?L.T(" (экспериментальный: не рекомендуется, пока замер не покажет «Работает»)"):p.Custom?L.T(" (свой: не входит в официально поддерживаемые)"):"")+"\r\n");
   sb.Append("DoH: "+p.Template+"\r\n");
   sb.Append(L.T("Закреплённые адреса: ")+(p.Servers.Length==0?L.T("нет, адреса ищутся при подключении"):string.Join(", ",p.Servers))+"\r\n");
   if(isActive&&addrSource.Length>0&&p==Current)sb.Append(addrSource+"\r\n");
   if(isActive&&p==Current)sb.Append(L.T("Сетевые проверки: ")+(checksPassed?L.T("пройдены при подключении"):L.T("не выполнялись (вкладка «Логи» → «Диагностика»)"))+"\r\n");
   if(isActive&&p==Current&&speed!=null&&speed.Ms>=0)sb.Append(L.T("Пинг до сервера-посредника провайдера: ")+speed.Ms+L.T(" мс (")+speed.Proxy+", "+speed.Utc.ToLocalTime().ToString("HH:mm")+")\r\n");
   var spec=p.Custom?null:ProfileCatalog.BuiltIn.Find(p.Name);
   if(spec!=null){
    if(!string.IsNullOrWhiteSpace(spec.KnownIssue))sb.Append(L.T("\r\nИзвестная проблема: ")+spec.KnownIssue+"\r\n");
    if(!string.IsNullOrWhiteSpace(spec.Note))sb.Append(L.T("\r\nЗаметка: ")+spec.Note+"\r\n");
   }
   if(!string.IsNullOrWhiteSpace(extra))sb.Append("\r\n"+extra.Replace("\r\n","\n").Replace("\n","\r\n")+"\r\n");
   if(!string.IsNullOrEmpty(lastCode)){
    var info=CodeTable.Lookup(lastCode);
    sb.Append(L.T("\r\nПоследнее сообщение: ")+info.Text+"\r\n"+(info.Action.Contains(L.T("«Подробнее»"))?"":L.T("Что делать: ")+info.Action+"\r\n")+L.T("Код: ")+lastCode+"\r\n");
   }
   if(!string.IsNullOrWhiteSpace(lastInstruction))sb.Append(L.T("\r\nИнструкция по ручному восстановлению:\r\n")+lastInstruction.Replace("\r\n","\n").Replace("\n","\r\n"));
   detailsBox.Text=sb.ToString();detailsBox.SelectionStart=0;detailsBox.SelectionLength=0;
   float s=LayoutScale;
   detailsPanel.Bounds=new Rectangle((int)Math.Round(14*s),(int)Math.Round(10*s),(int)Math.Round((W-28)*s),(int)Math.Round((PageBottom-20)*s));
   detailsPanel.Visible=true;detailsPanel.BringToFront();
  }

  // The Telegram app when it is installed (tg://), the browser otherwise.
  static void OpenTelegram(string user){
   bool desktop=false;try{using(var k=Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(@"tg\shell\open\command")){desktop=k!=null;}}catch{}
   string url=desktop?"tg://resolve?domain="+user:"https://t.me/"+user;
   try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}
   catch{OpenUrl("https://t.me/"+user);}
  }
  static void OpenUrl(string url){try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}catch{}}

  // ---- what goes through the bridge: Antigravity / + Gemini / all of Google (the hosts and suffixes themselves come from the catalog) ----
  internal enum BridgeScope{Core=0,Gemini=1,Google=2}
  internal static BridgeScope ScopeNow=>Program.Google?BridgeScope.Google:Program.Gemini?BridgeScope.Gemini:BridgeScope.Core;
  internal static BridgeScope ScopeOf(IEnumerable<string> targets)=>Program.HasGoogle(targets)?BridgeScope.Google:Program.HasGemini(targets)?BridgeScope.Gemini:BridgeScope.Core;
  static string ScopeName(BridgeScope v)=>v==BridgeScope.Google?L.T("Antigravity/Gemini/браузер"):v==BridgeScope.Gemini?"Antigravity/Gemini":"Antigravity";
  internal static string InfoTextFor(BridgeScope scope)=>L.T("Google закрывает Antigravity, Gemini и другие сервисы для ряда стран, например России. Только DNS-запросы к нужным доменам Google уходят зашифрованно (DoH) провайдеру; он отвечает адресом своего сервера-посредника. С Google соединение шифруется напрямую, провайдер видит лишь имя сайта.");
  void SetScope(BridgeScope v){
   scopeUpdating=true;try{scopeSel.SelectedIndex=(int)v;}finally{scopeUpdating=false;}
   Program.Google=v==BridgeScope.Google;Program.Gemini=v!=BridgeScope.Core;
   if(settings.Gemini!=Program.Gemini||settings.Google!=Program.Google){settings.Gemini=Program.Gemini;settings.Google=Program.Google;if(!smoke)settings.Save();}
   if(healthOptions!=null)healthOptions.Targets=Program.Hosts;
   UpdateDetailsText();
  }
  void SetGemini(bool value)=>SetScope(value?BridgeScope.Gemini:BridgeScope.Core);
  // A new scope with no bridge: only the next connect changes. With a working bridge the choice is applied at once, as the ordinary connect (compensated switch):
  // the selector shows that it is busy and ignores clicks, a failure puts the selector back and says why in the usual way, and the old bridge stays.
  // «+ Браузер» asks first which browser the person uses (a browser with its own secure DNS skips the bridge); backing out puts the selector back.
  bool scopeApplying;
  async void OnScopeSelected(){
   if(scopeUpdating||scopeApplying)return;
   var want=(BridgeScope)scopeSel.SelectedIndex;var prev=ScopeNow;
   try{
    if(busy||benchRunning){SetScope(ScopeNow);return;}
    if(want==BridgeScope.Google&&ScopeNow!=BridgeScope.Google){
     if(!await AskBrowserAsync()){SetScope(ScopeNow);return;}
     if(busy||benchRunning){SetScope(ScopeNow);return;}
    }
    if(needsRestore&&isActive){
     SetScope(want);Log("Через мост: "+ScopeName(want)+" — подключаю заново.");
     scopeApplying=true;UpdateButtons();
     try{await Mutate("enable");}
     finally{scopeApplying=false;}
     if(!SyncGeminiFromJournal()&&!isActive)SetScope(prev);   // refused before the engine and nothing to read: the selector goes back to what it was
     UpdateButtons();
     return;
    }
    SetScope(want);Log("Через мост: "+ScopeName(want)+" (со следующего подключения).");
   }catch(Exception e){scopeApplying=false;SetScope(prev);UpdateButtons();Log("Выбор «Через мост»: "+e.Message);}
  }
  // The scope of the bridge that exists is the journal's (a plain file, readable without elevation). No journal or a restored one: the selector keeps the person's choice.
  bool SyncGeminiFromJournal(){
   try{
    string j=JournalReader();if(string.IsNullOrWhiteSpace(j))return false;
    using(var doc=JsonDocument.Parse(j)){
     var r=doc.RootElement;if(r.TryGetProperty("Phase",out var ph)&&ph.ValueKind==JsonValueKind.String&&ph.GetString()=="Restored")return false;
     if(!r.TryGetProperty("Targets",out var t)||t.ValueKind!=JsonValueKind.Array)return false;
     var scope=t.EnumerateArray().Select(x=>x.GetString()).Where(x=>x!=null).ToList();
     if(scope.Count==0)return false;
     var sc=ScopeOf(scope);if(sc!=ScopeNow)SetScope(sc);return true;
    }
   }catch{return false;}
  }
  // ---- language: the whole window follows it at once; switching rewrites every text of the window, the tray and the tooltips, nothing restarts ----
  void OnLanguageSelected(){
   string want=langSel.SelectedIndex==1?"en":"ru";
   if(settings.Language==want)return;
   if(busy||benchRunning){langSel.SelectedIndex=L.En?1:0;return;}
   settings.Language=want;settings.Save();
   ApplyLanguage(want=="en");
  }
  // Static texts are exchanged through the dictionary (every control that holds a plain phrase); the texts that are built from state are made again by their own
  // functions (status, line under it, buttons, tray, the rows of settings, the benchmark note, the open panels). The log keeps its Russian technical lines.
  internal void ApplyLanguage(bool en){
   L.En=en;
   SuspendLayout();
   try{
    SwapTexts(this,en);
    BuildProfiles();int pi=profiles.FindIndex(x=>x.Name==settings.Profile);profileSelect.SetSelectedSilently(pi<0?0:pi);
    scopeSel.Items=new[]{"Antigravity","+ Gemini Desktop",L.T("+ Браузер")};
    scopeSel.Tips=new[]{L.T("Только домены Antigravity."),L.T("Antigravity, Gemini Desktop и сайт gemini.google.com."),L.T("Все домены Google: Gemini, NotebookLM, AI Studio, поиск, почта и остальные сервисы.")};
    if(titleFn!=null)statusLabel.Text=titleFn();
    if(benchFn!=null)benchStatus.Text=benchFn();
    versionLabel.Text=L.T("Версия")+" "+AppInfo.Display+"  ·  Windows x64";
    infoText.Text=InfoTextFor(ScopeNow);
    UpdateDetailsText();UpdateButtons();UpdateAutostartNote();UpdateCustomRow();UpdateBrowserRow();
    if(lastBench!=null)ApplyBenchResults(lastBench);
    if(trayOpen!=null){trayOpen.Text=L.T("Открыть окно");trayFix.Text=L.T("Починить мост");trayExit.Text=L.T("Выход");}
    if(detailsPanel!=null&&detailsPanel.Visible)ShowDetails(detailsProfile,detailsExtra);
    if(browserPanel!=null&&browserPanel.Visible)RelabelBrowserPanel();
   }finally{ResumeLayout(true);}
   Invalidate(true);
  }
  // A control's own phrase, when it is one the dictionary knows, is written in the new language; anything built from state is left to the functions above.
  static void SwapTexts(Control root,bool en){
   foreach(Control c in root.Controls){
    switch(c){
     case PillButton b:b.Text=L.Swap(b.Text,en);break;
     case ToggleRow t:t.Text=L.Swap(t.Text,en);t.Subtitle=L.Swap(t.Subtitle,en);break;
     case LinkRow r:r.Text=L.Swap(r.Text,en);r.Subtitle=L.Swap(r.Subtitle,en);break;
     case Segmented s:s.Items=s.Items.Select(x=>L.Swap(x,en)).ToArray();s.Tips=s.Tips.Select(x=>L.Swap(x,en)).ToArray();break;
     case Label l:l.Text=L.Swap(l.Text,en);break;
    }
    if(c.HasChildren)SwapTexts(c,en);
   }
  }
  internal void SyncGeminiFromJournalForTest()=>SyncGeminiFromJournal();
  internal bool GeminiOn=>Program.Gemini&&scopeSel.SelectedIndex>=1;
  internal bool GeminiToggleEnabled=>scopeSel.Enabled;
  internal void SetGeminiForTest(bool value)=>SetGemini(value);
  internal void SetScopeForTest(BridgeScope v)=>SetScope(v);
  internal void ClickScopeForTest(BridgeScope v)=>scopeSel.ClickForTest((int)v);
  internal int ScopeIndex=>scopeSel.SelectedIndex;
  internal string InfoText=>infoText.Text;

  // ---- autostart: only the switch turns it on, and it always names the running exe; no question, no copy ----
  // The switch is on when the entry names this exe. An entry that names another file (a copy elsewhere) shows the switch off with a note; turning it on moves the entry
  // here without asking and remembers the other file, which the row «Прежняя копия» offers to delete (the person confirms; nothing is deleted by itself).
  void UpdateAutostartNote(){
   string target=AutostartHelper.CurrentTarget();
   bool other=target!=null&&!AutostartHelper.SamePath(target,Environment.ProcessPath??"");
   autostartNote.Text=other?L.T("Автозапуск ведёт на другую копию"):"";
   tgAutostart.Subtitle=other?autostartNote.Text:L.T("Программа следит за состоянием моста");tgAutostart.SubtitleColor=other?Theme.Amber:Theme.Muted;
   if(!string.IsNullOrEmpty(settings.PreviousCopy)&&(!File.Exists(settings.PreviousCopy)||AutostartHelper.SamePath(settings.PreviousCopy,Environment.ProcessPath??""))){settings.PreviousCopy="";settings.Save();}   // gone, or it is this very file: nothing to offer
   prevCopy.Text=L.T("Прежняя копия: ")+settings.PreviousCopy;
   bool show=!string.IsNullOrEmpty(settings.PreviousCopy);
   if(prevCopy.Visible!=show){prevCopy.Visible=show;prevDelete.Visible=show;tgAutostart.Divider=!show;}
   LayoutSettings();
  }
  const int PrevH=36;
  // The row under the switch takes room from the cards below it; positions are set in pixels from the layout units.
  void LayoutSettings(){
   if(settingsCardA==null)return;
   float s=LayoutScale;int R(int v)=>(int)Math.Round(v*s);int iw=W-28-36,extra=prevCopy.Visible?PrevH:0;
   settingsCardA.Bounds=new Rectangle(R(14),R(4),R(W-28),R(222+extra));
   tgAutostart.Bounds=new Rectangle(R(18),R(30),R(iw),R(46));
   prevCopy.Bounds=new Rectangle(R(18),R(76),R(iw),R(PrevH));
   prevDelete.Bounds=new Rectangle(R(18+iw-72),R(76+(PrevH-26)/2),R(72),R(26));
   tgQuiet.Bounds=new Rectangle(R(18),R(76+extra),R(iw),R(46));tgCloseTray.Bounds=new Rectangle(R(18),R(122+extra),R(iw),R(46));tgNotify.Bounds=new Rectangle(R(18),R(168+extra),R(iw),R(46));
   settingsCardB.Bounds=new Rectangle(R(14),R(238+extra),R(W-28),R(176));
  }
  internal Func<string,bool> DeleteConfirm;     // test seam: the question before a previous copy is deleted (null = a message box)
  internal Action<string> Notice;               // test seam: where a plain notice goes (null = a message box)
  internal Func<string,bool> RunningProbe;      // test seam: is that file running (null = look at the processes)
  void Tell(string text){if(Notice!=null)Notice(text);else MessageBox.Show(this,text,Text,MessageBoxButtons.OK,MessageBoxIcon.Information);}
  void OnAutostartToggled(){
   if(autostartUpdating)return;
   bool want=tgAutostart.Checked;
   try{
    if(want){
     string exe=Environment.ProcessPath;
     if(!AutostartHelper.EnableForCurrent(exe,out string previous))throw new InvalidOperationException(L.T("Не удалось обновить запись автозапуска в реестре."));
     if(previous!=null){settings.PreviousCopy=previous;settings.Save();Log("Автозапуск переведён на эту копию: "+exe+"; прежняя: "+previous);}
     else Log("Автозапуск включён: "+exe);
    }else{
     if(!AutostartHelper.SetEnabled(false))throw new InvalidOperationException(L.T("Не удалось обновить запись автозапуска в реестре."));
     Log("Автозапуск выключен");
    }
    UpdateAutostartNote();
   }catch(Exception ex){MessageBox.Show(this,L.T("Автозапуск не изменён: ")+ex.Message,Text);autostartUpdating=true;tgAutostart.Checked=!want;autostartUpdating=false;}
  }
  void OnDeletePreviousCopy(){
   string path=settings.PreviousCopy;if(string.IsNullOrEmpty(path))return;
   bool yes=DeleteConfirm!=null?DeleteConfirm(path):MessageBox.Show(this,L.T("Удалить файл прежней копии?\n")+path+L.T("\n\nУдалится только этот файл программы, больше ничего."),L.T("Прежняя копия"),MessageBoxButtons.YesNo,MessageBoxIcon.Warning)==DialogResult.Yes;
   if(!yes)return;
   var r=AutostartHelper.RemovePreviousCopy(path,Environment.ProcessPath,RunningProbe);
   switch(r){
    case AutostartHelper.CopyRemoval.Removed:Log("Прежняя копия удалена: "+path);settings.PreviousCopy="";settings.Save();break;
    case AutostartHelper.CopyRemoval.AlreadyGone:settings.PreviousCopy="";settings.Save();break;
    case AutostartHelper.CopyRemoval.Running:Tell(L.T("Прежняя копия сейчас запущена. Закройте её и повторите."));break;
    case AutostartHelper.CopyRemoval.NotOurs:Tell(L.T("Это не файл этой программы: ничего не удалено."));settings.PreviousCopy="";settings.Save();break;
    case AutostartHelper.CopyRemoval.ThisCopy:settings.PreviousCopy="";settings.Save();break;
    default:Tell(L.T("Не удалось удалить файл: ")+path);break;
   }
   UpdateAutostartNote();
  }
  internal bool PreviousCopyVisible=>prevCopy.Visible;
  internal string PreviousCopyText=>prevCopy.Text;
  internal void ClickDeletePreviousCopyForTest()=>prevDelete.PerformClickForTest();
  internal void ToggleAutostartForTest(bool on){tgAutostart.Checked=on;}
  internal string AutostartSubtitle=>tgAutostart.Subtitle;
  internal bool AutostartOn=>tgAutostart.Checked;
  internal int PageHeightOf(int tab)=>pages[tab].Height;
  internal string SettingsPreviousCopy()=>settings.PreviousCopy;
  internal void SetPreviousCopyForTest(string path){settings.PreviousCopy=path;UpdateAutostartNote();}
  internal Rectangle[] SettingsRows=>new[]{settingsCardA.Bounds,tgAutostart.Bounds,prevCopy.Bounds,tgQuiet.Bounds,tgCloseTray.Bounds,tgNotify.Bounds,settingsCardB.Bounds};

  // ---- tray / lifecycle ----
  internal void InitTrayForTest(){if(tray==null){InitTray(false);UpdateButtons();}}
  internal string[] TrayTexts=>tray==null?new string[0]:new[]{trayOpen.Text,trayStatus.Text,trayToggle.Text,trayFix.Text,trayExit.Text,tray.Text};
  internal void ShowFailureForTest(string code)=>ShowFailure(code);
  internal string SettingsLanguage=>settings.Language;
  void InitTray(bool visible=true){
   var menu=new ContextMenuStrip{Renderer=new ToolStripProfessionalRenderer(new DarkColors()){RoundedEdges=false},ShowImageMargin=false,BackColor=Theme.Card,ForeColor=Theme.Text,Font=Theme.F(9.5f)};
   var open=trayOpen=new ToolStripMenuItem(L.T("Открыть окно")){ForeColor=Theme.Text,Padding=new Padding(4,5,4,5)};open.Click+=(_,__)=>ShowWindow();
   trayStatus=new ToolStripMenuItem(L.T("Мост: проверяю…")){Enabled=false,ForeColor=Theme.Muted};
   trayToggle=new ToolStripMenuItem(L.T("Подключить мост")){ForeColor=Theme.Text,Padding=new Padding(4,5,4,5)};trayToggle.Click+=async(_,__)=>{try{if(!busy&&known)await Mutate(needsRestore?"restore":"enable");}catch(Exception e){Log("Ошибка: "+e.Message);}};
   trayFix=new ToolStripMenuItem(L.T("Починить мост")){ForeColor=Theme.Amber,Padding=new Padding(4,5,4,5),Visible=false};trayFix.Click+=async(_,__)=>{try{if(!busy&&known&&health!=null&&health.CanFix)await Mutate("enable",true);}catch(Exception e){Log("Ошибка: "+e.Message);}};
   var exit=trayExit=new ToolStripMenuItem(L.T("Выход")){ForeColor=Theme.Text,Padding=new Padding(4,5,4,5)};exit.Click+=(_,__)=>{exiting=true;Close();};
   menu.Items.AddRange(new ToolStripItem[]{open,trayStatus,new ToolStripSeparator(),trayFix,trayToggle,new ToolStripSeparator(),exit});
   trayOn=new Icon(iconOn,SystemInformation.SmallIconSize);trayOff=new Icon(iconOff,SystemInformation.SmallIconSize);
   tray=new NotifyIcon{Icon=trayOff,Text="Antigravity DNS",Visible=visible,ContextMenuStrip=menu};
   tray.MouseClick+=(_,e)=>{if(e.Button==MouseButtons.Left)ToggleWindow();};
  }
  void ToggleWindow(){if(Visible&&WindowState!=FormWindowState.Minimized)Hide();else ShowWindow();}
  public void ShowWindow(){
   bool fade=!Visible&&Motion.Enabled;
   if(fade)Opacity=0;
   if(!Visible)Show();if(WindowState==FormWindowState.Minimized)WindowState=FormWindowState.Normal;Activate();BringToFront();
   if(fade){long st=Stopwatch.GetTimestamp();Motion.Run(()=>{if(IsDisposed)return false;double t=(Stopwatch.GetTimestamp()-st)*1000.0/Stopwatch.Frequency/140;if(t>=1){Opacity=1;return false;}Opacity=Motion.Ease((float)t);return true;});}
  }
  // A short confirmation on the button itself instead of a dialog.
  // The original label is held only while a flash runs, and it comes back in the language the window has then (the language may change meanwhile).
  static void Flash(PillButton b,string text,int ms=1400){
   var prev=b.Tag as Tuple<string,System.Windows.Forms.Timer>;string orig=prev?.Item1??b.Text;
   if(prev!=null){prev.Item2.Stop();prev.Item2.Dispose();}
   var t=new System.Windows.Forms.Timer{Interval=ms};b.Tag=Tuple.Create(orig,t);b.Text=text;
   t.Tick+=(_,__)=>{t.Stop();t.Dispose();if(b.IsDisposed)return;b.Tag=null;b.Text=L.Swap(L.Swap(orig,false),L.En);};t.Start();
  }
  internal static void FlashForTest(PillButton b,string text,int ms)=>Flash(b,text,ms);
  public void RegisterShowEvent(EventWaitHandle evt){ThreadPool.RegisterWaitForSingleObject(evt,(st,to)=>{try{if(IsHandleCreated&&!IsDisposed)BeginInvoke(new Action(ShowWindow));}catch{}},null,-1,false);}
  void Notify(string text){if(tray!=null&&settings.Notify&&!Visible)try{tray.ShowBalloonTip(2500,"Antigravity DNS",text,ToolTipIcon.Info);}catch{}}

  protected override void SetVisibleCore(bool value){
   if(hideOnStart){hideOnStart=false;if(!IsHandleCreated)CreateHandle();value=false;}
   base.SetVisibleCore(value);
  }
  protected override void OnHandleCreated(EventArgs e){
   base.OnHandleCreated(e);
   try{int one=1;DwmSetWindowAttribute(Handle,20,ref one,4);int cap=0x000C0A0A;DwmSetWindowAttribute(Handle,35,ref cap,4);int txt=0x00F2ECEC;DwmSetWindowAttribute(Handle,36,ref txt,4);}catch{}
  }
  void OnFormClosing(object s,FormClosingEventArgs e){
   SaveWindowPosition();
   if(!smoke&&!exiting&&e.CloseReason==CloseReason.UserClosing&&settings.CloseToTray&&tray!=null){
    e.Cancel=true;Hide();
    if(!settings.TrayHintShown){settings.TrayHintShown=true;settings.Save();try{tray.ShowBalloonTip(3000,"Antigravity DNS",L.T("Программа осталась в трее. Выход — в меню значка."),ToolTipIcon.Info);}catch{}}
    return;
   }
   if(busy||benchRunning){if(!smoke&&MessageBox.Show(this,L.T("Операция ещё идёт. Прервать её и закрыть программу?"),Text,MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes){e.Cancel=true;exiting=false;return;}currentCts?.Cancel();benchCts?.Cancel();}
  }
  protected override void Dispose(bool disposing){
   if(disposing){
    try{currentCts?.Cancel();currentCts?.Dispose();}catch(ObjectDisposedException){}
    try{benchCts?.Cancel();benchCts?.Dispose();}catch(ObjectDisposedException){}
    try{lifeCts.Cancel();lifeCts.Dispose();}catch(ObjectDisposedException){}
    StopHealth();
    currentCts=null;benchCts=null;benchTimer.Dispose();if(tray!=null){tray.Visible=false;tray.Dispose();}
    trayOn?.Dispose();trayOff?.Dispose();
   }
   base.Dispose(disposing);
  }

  // ---- state ----
  void Log(string line){
   if(logBox.IsDisposed)return;
   logBox.AppendText("["+DateTime.Now.ToString("HH:mm:ss")+"] "+line+"\r\n");
   if(logBox.TextLength>200000)logBox.Text=logBox.Text.Substring(logBox.TextLength-100000);
  }
  void LogBlock(string text){if(!logBox.IsDisposed){logBox.AppendText(text.Replace("\r\n","\n").Replace("\n","\r\n")+"\r\n");}}
  bool EnsureTrust(ProfileItem item){
   string valueName="Trust_"+(item.Custom?"custom_"+new Uri(item.Template).Host:item.Name);
   try{using(var key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\AntigravityDnsUnlocker",false)){if(key!=null&&key.GetValue(valueName) is int v&&v==1)return true;}}catch{}
   var answer=MessageBox.Show(this,L.T("Провайдер «")+item.Name+L.T("» направит HTTPS-соединения доменов Google через свой сервер-посредник (SNI-прокси). Сертификат TLS проверяется, но провайдер видит имена хостов и объём трафика.\n\nДоверять провайдеру и подключить мост?"),L.T("Подтверждение доверия"),MessageBoxButtons.YesNo,MessageBoxIcon.Warning);
   if(answer!=DialogResult.Yes)return false;
   try{using(var key=Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\AntigravityDnsUnlocker")){key.SetValue(valueName,1,Microsoft.Win32.RegistryValueKind.DWord);}}catch{}
   return true;
  }
  string Scope=>ScopeName(ScopeNow);
  void UpdateDetailsText(){subLabel.Text=statusDetail??(Checking?L.T("Проверяю, что мост работает…"):Current.Name+"  →  "+Scope);}
  // The ping of a profile for its field and its row in the list: the working bridge's own measurement for the profile in use, the list's measurements for the rest.
  // Empty text = not known. A failure to answer is said in words.
  (string text,Color dot) PingDetail(int i){
   if(i<0||i>=profiles.Count)return ("",Color.Empty);
   var p=profiles[i];SpeedResult r=null;
   if(isActive&&!busy&&activeTemplate!=null&&string.Equals(p.Template,activeTemplate,StringComparison.OrdinalIgnoreCase)){
    if(Checking)return ("",Color.Empty);
    r=speed;if(r==null)return speedRunning?("…",Color.Empty):("",Color.Empty);
   }else lock(pings){if(!pings.TryGetValue(p.Template??"",out r)||DateTime.UtcNow-r.Utc>PingShown)r=null;}
   if(r==null)return ("",Color.Empty);
   if(r.Ms>=0)return (r.Ms+L.T(" мс"),r.Ms<=200?Theme.Green:Theme.Amber);
   return (L.T("не ответил"),Theme.Amber);
  }
  void UpdateSummary(){profileSelect.Tip=string.IsNullOrEmpty(PingDetail(profileSelect.SelectedIndex).text)?null:PingTip;profileSelect.RefreshDetails();}
  // The tooltip of the field names what the number is.
  internal string PingTip=>L.T("Задержка до сервера-посредника провайдера: время TCP-соединения с адресом, который провайдер отдаёт для ")+Program.Target+".";
  internal string ProfilePingText(int i)=>PingDetail(i).text;
  internal Color ProfilePingDot(int i)=>PingDetail(i).dot;
  internal string ProfilePingTextNow=>PingDetail(profileSelect.SelectedIndex).text;
  internal Color ProfilePingDotNow=>PingDetail(profileSelect.SelectedIndex).dot;
  internal int ProfileCount=>profiles.Count;
  internal string[] ProfileLabels=>profileSelect.Items.ToArray();
  // The dense block under the logo, at the current scale: the same gap above each caption, the same gap under each caption, fields of one size and one width,
  // equal margins, everything inside the page. Used by the self-test and by the stress run at every scale.
  // Everything the self-test and the scale runs ask of the main page at the scale the window has now: the dense block in five states, no overlaps, the longest
  // profile name beside a ping fits its field.
  internal List<string> ScaleProblems(){
   var bad=new List<string>();string at=DeviceDpi+" dpi ";
   foreach(string mode in new[]{"idle","active","drift","switch","busy"}){
    SmokeState(mode);foreach(var g in MainGeometryProblems())bad.Add(at+mode+": "+g);
    var rs=MainPageRects.ToList();int ph=MainPageHeight;
    for(int i=0;i<rs.Count;i++){
     if(rs[i].Item2.Bottom>ph+1)bad.Add(at+mode+": "+rs[i].Item1+" leaves the page");
     for(int j=i+1;j<rs.Count;j++)if(rs[i].Item2.IntersectsWith(rs[j].Item2))bad.Add(at+mode+": "+rs[i].Item1+" over "+rs[j].Item1);
    }
   }
   SmokeState("idle");float k=DeviceDpi/96f;
   foreach(var name in profileSelect.Items.Where(n=>n.Length<24)){
    int nameW=TextRenderer.MeasureText(name,Theme.F(9.5f),Size.Empty,PillSelect.Flags).Width,pingW=TextRenderer.MeasureText(L.T("не ответил"),Theme.F(9f),Size.Empty,PillSelect.Flags).Width;
    if(nameW+pingW+(14+11+10+36)*k>profileSelect.Width)bad.Add(at+"«"+name+"» and its ping do not fit the field");
   }
   return bad;
  }
  internal List<string> MainGeometryProblems(){
   var bad=new List<string>();float s=LayoutScale;int tol=Math.Max(2,(int)Math.Ceiling(1.5*s));
   var vis=MainButtons.Where(b=>b.Visible).OrderBy(b=>b.Top).ToList();if(vis.Count==0){bad.Add("no button");return bad;}
   var main=vis[0];Rectangle pc=profileCaption.Bounds,pf=profileSelect.Bounds,sc=scopeCaption.Bounds,sf=scopeSel.Bounds;
   void Eq(string what,int a,int b){if(Math.Abs(a-b)>tol)bad.Add(what+": "+a+" vs "+b);}
   Eq("gap button → profile caption, field → levels caption",pc.Top-main.Bottom,sc.Top-pf.Bottom);
   Eq("caption → field (profile, levels)",pf.Top-pc.Bottom,sf.Top-sc.Bottom);
   Eq("field heights",pf.Height,sf.Height);Eq("field left",pf.Left,sf.Left);Eq("field width",pf.Width,sf.Width);Eq("field margins ("+pf.Left+".."+pf.Right+" of "+(int)Math.Round(W*s)+")",pf.Left,(int)Math.Round(W*s)-pf.Right);   // against the designed width, not the window's (a frame can be a pixel or two off)
   if(pc.Top<main.Bottom||sc.Top<pf.Bottom||sf.Bottom>pages[TabBridge].Height)bad.Add("a field leaves its place (button "+main.Top+"–"+main.Bottom+" "+main.Text+", caption "+pc.Top+", field "+pf.Top+"–"+pf.Bottom+", levels caption "+sc.Top+", levels "+sf.Top+"–"+sf.Bottom+", page "+pages[TabBridge].Height+", client "+ClientSize+", dpi "+DeviceDpi+")");
   return bad;
  }
  internal Rectangle ProfileBounds=>profileSelect.Bounds;
  internal Rectangle ScopeBounds=>scopeSel.Bounds;
  internal string ProfileCaptionText=>profileCaption.Text;
  internal bool ScopeBusy=>scopeSel.Busy;
  // The scale the controls have really been multiplied by: the tab bar is placed once in layout units and only ever scaled by WinForms (AutoScale, DPI changes),
  // so its width says it exactly, whether the window is shown yet or not (a window started hidden in the tray has a handle long before it is scaled).
  internal float LayoutScale=>nav.Width/(float)(W-28);
  internal Rectangle BridgeBounds=>bridge.Bounds;
  internal bool BridgeSubtle=>bridge.Subtle;
  internal string DetailsText=>detailsBox.Text;
  internal Control ScopeControl=>scopeSel;
  internal Control ProfileControl=>profileSelect;
  internal Control NavControl=>nav;
  internal ToggleRow[] Toggles=>new[]{tgAutostart,tgQuiet,tgCloseTray,tgNotify};
  internal string[] ToggleTexts=>Toggles.Select(t=>t.Text).ToArray();
  internal LinkRow[] InfoRows=>new[]{patcherRow,patcherTgRow,tgRow};
  internal Label InfoLabel=>infoText;
  internal LinkRow CustomRow=>customRow;
  internal string SubText=>subLabel.Text;
  // Every text the person can see right now (labels, buttons, switch rows, link rows, selectors, chips, the open overlay): the English check walks it.
  internal IEnumerable<string> AllVisibleTexts(){
   var stack=new Stack<Control>();stack.Push(this);
   while(stack.Count>0){
    var c=stack.Pop();if(c!=this&&!c.Visible)continue;
    if(c is Label l&&!string.IsNullOrEmpty(l.Text))yield return l.Text;
    else if(c is PillButton b&&!string.IsNullOrEmpty(b.Text))yield return b.Text;
    else if(c is ToggleRow t){yield return t.Text;if(!string.IsNullOrEmpty(t.Subtitle))yield return t.Subtitle;}
    else if(c is LinkRow r){yield return r.Text;if(!string.IsNullOrEmpty(r.Subtitle))yield return r.Subtitle;}
    else if(c is Segmented sg){foreach(var i in sg.Items)yield return i;foreach(var tp in sg.Tips)yield return tp;}
    else if(c is VerdictLine vl){yield return vl.Headline;}
    else if(c is PathLabel pl&&!string.IsNullOrEmpty(pl.Text))yield return pl.Text;
    else if(c is BrowserList bl){foreach(var it in bl.Items)yield return it.Name;}
    else if(c is PillSelect ps&&ps.Items.Count>0){yield return ps.Items[Math.Max(0,Math.Min(ps.Items.Count-1,ps.SelectedIndex))];var pd=ps.Detail?.Invoke(ps.SelectedIndex);if(pd!=null&&!string.IsNullOrEmpty(pd.Value.text))yield return pd.Value.text;}
    foreach(Control ch in c.Controls)stack.Push(ch);
   }
  }
  internal Segmented LanguageControl=>langSel;
  internal Segmented ScopeSegmented=>scopeSel;
  internal IconKind LanguageIconForTest=>langRow.Icon;
  internal IconKind BrowserRowIconForTest=>browserRow.Icon;
  internal int MainPageHeight=>pages[TabBridge].Height;
  internal IEnumerable<(string,Rectangle)> MainPageRects=>pages[TabBridge].Controls.Cast<Control>().Where(c=>c.Visible).Select(c=>(c.GetType().Name+(c is Label l?":"+l.Text.Length:""),c.Bounds));
  internal bool HeaderControlsVisible=>Controls.Cast<Control>().Any(c=>c.Visible&&(c is Label||c is LogoBox));   // no page has a title or a small logo
  internal int PageTopUnits(int tab)=>(int)Math.Round(pages[tab].Top/LayoutScale);
  internal IEnumerable<Label> LabelsOf(int tab){var stack=new Stack<Control>();stack.Push(pages[tab]);while(stack.Count>0){var c=stack.Pop();foreach(Control ch in c.Controls){if(ch is Label l&&!(ch is LinkLabel))yield return l;stack.Push(ch);}}}
  internal IEnumerable<string> MainTexts=>LabelsOf(TabBridge).Where(l=>l.Visible).Select(l=>l.Text);
  internal bool DetailsLinkVisible=>detailsLink.Visible;
  void UpdateButtons(){
   UpdateSummary();
   bool showFix=health!=null&&health.Kind==HealthKind.Drift&&health.CanFix&&!recoveryNeeded&&needsRestore;
   bool showAlt=offer!=null&&!recoveryNeeded;showFix=showFix&&!showAlt;
   // One main button: "Починить" / "Подключить через …" take the big top slot and push the plain connect/disconnect button to the small bottom slot as a secondary one.
   bool other=showFix||showAlt;float sc=LayoutScale;
   Rectangle Slot(int y,int h)=>new Rectangle((int)Math.Round(28*sc),(int)Math.Round(y*sc),(int)Math.Round((W-56)*sc),(int)Math.Round(h*sc));
   // The main button is a centred pill as wide as its words need (not a full-width slab); secondary actions use the full-width row below the profile.
   Rectangle Main(PillButton b){int w=Math.Min((int)Math.Round((W-56)*sc),Math.Max((int)Math.Round(MainMinW*sc),b.PreferredWidth()));return new Rectangle((ClientSize.Width-w)/2,(int)Math.Round(MainY*sc),w,(int)Math.Round(MainH*sc));}
   bridge.Text=recoveryNeeded?L.T("Восстановить"):needsRestore?L.T("Отключить мост"):L.T("Подключить мост");bridge.Font=other?smallFont:bigFont;bridge.Glyph=recoveryNeeded?"\uE777":"\uE7E8";
   altBtn.Visible=showAlt;altBtn.Enabled=!busy&&known;if(showAlt)altBtn.Text=L.T("Подключить через ")+offer.ProfileId;
   bridge.Bounds=other?Slot(SecondY,SecondH):Main(bridge);fixBtn.Bounds=Main(fixBtn);altBtn.Bounds=Main(altBtn);
   // Connected and healthy: "Отключить мост" stays at hand but quiet (tinted, not a red slab). Recovery keeps the solid colour: that one needs doing.
   bridge.Enabled=!busy&&known;bridge.SetFill(other?Theme.Soft:needsRestore?Theme.Danger:Theme.Accent);bridge.Subtle=!other&&needsRestore&&!recoveryNeeded;
   detailsLink.Visible=!busy&&!Checking&&(statusDetail!=null||recoveryNeeded||!string.IsNullOrWhiteSpace(lastInstruction));
   fixBtn.Visible=showFix;fixBtn.Enabled=!busy&&known;if(trayFix!=null)trayFix.Visible=showFix;
   profileSelect.Enabled=!busy&&!needsRestore;diagBtn.Enabled=!busy;tgAutostart.Enabled=!busy;scopeSel.Busy=scopeApplying;scopeSel.Enabled=scopeApplying||(!busy&&!(needsRestore&&!isActive));langSel.Enabled=!busy;
   saveCustom.Enabled=!busy&&!needsRestore;delCustom.Enabled=!busy&&!needsRestore;findIp.Enabled=!busy&&!needsRestore;
   bigLogo.Active=isActive;bigLogo.Spinning=busy||(refreshing&&checking);
   statusLabel.ForeColor=isActive?Theme.Green:showAlt?Theme.AccentHover:(recoveryNeeded||(statusDetail!=null&&!busy))?Theme.Amber:Theme.Text;
   if(tray!=null){var ti=isActive?trayOn:trayOff;if(tray.Icon!=ti)tray.Icon=ti;tray.Text=isActive?L.T("Antigravity DNS — мост подключён")+(speed!=null&&speed.Ms>=0?L.T(", пинг ")+speed.Ms+L.T(" мс"):""):recoveryNeeded?L.T("Antigravity DNS — требуется восстановление"):L.T("Antigravity DNS — мост отключён");if(trayToggle!=null){trayToggle.Text=recoveryNeeded?L.T("Восстановить"):needsRestore?L.T("Отключить мост"):L.T("Подключить мост");trayToggle.Enabled=!busy&&known;trayStatus.Text=L.T("Мост: ")+(showFix?L.T("требует починки"):Checking?L.T("проверяю…"):isActive?L.T("подключён"):recoveryNeeded?L.T("требуется восстановление"):needsRestore?L.T("требует внимания"):L.T("отключён"));}}
   var want=isActive?iconOn:iconOff;if(Icon!=want)Icon=want;
   UpdateBenchButtons();
  }
  // Origin of the addresses the active bridge uses: "pinned" or "found now, DD.MM HH:MM".
  string AddressSourceText(JsonElement status,string template){
   if(!status.TryGetProperty("Servers",out var arr)||arr.ValueKind!=JsonValueKind.Array)return "";
   var active=arr.EnumerateArray().Select(x=>x.GetString()).Where(x=>x!=null).ToList();if(active.Count==0)return "";
   var last=(settings.LastAddrServers??"").Split(new[]{','},StringSplitOptions.RemoveEmptyEntries);
   if((settings.LastAddrSource=="found"||settings.LastAddrSource=="saved")&&settings.LastAddrTemplate==template&&active.All(a=>last.Contains(a))&&DateTime.TryParse(settings.LastAddrTime,null,System.Globalization.DateTimeStyles.RoundtripKind,out var when))return L.T("Адреса: ")+AddressResolver.SourceText(settings.LastAddrSource,when);
   var item=profiles.FirstOrDefault(p=>p.Template==template);
   if(item!=null&&item.Servers.Length>0&&active.All(a=>item.Servers.Contains(a)))return L.T("Адреса: закреплённые");
   return L.T("Адреса: найдены ранее");
  }
  // A failed operation: one plain sentence and one action from the code table; the log keeps the detail but is not needed for routine failures.
  void ShowFailure(string code,string instruction=null,bool? gemini=null){
   var info=CodeTable.Lookup(code);
   lastCode=string.IsNullOrEmpty(info.Code)?code:info.Code;lastInstruction=instruction;
   string action=CodeTable.ActionFor(info,gemini??Program.Gemini);
   bool gem=gemini??Program.Gemini;
   Detail(()=>{var i=CodeTable.Lookup(code);return i.Text+"\n"+CodeTable.ActionFor(i,gem);});
   if(!recoveryNeeded)Title(()=>CodeTable.Lookup(code).Title);
   // A profile switch where neither the new nor the previous profile could be applied leaves a clean (Restored) journal: nothing to restore, so do not claim it.
   if(info.Code=="RECOVERY_REQUIRED"&&!recoveryNeeded){Title(()=>L.T(CodeTable.ProfileNotAppliedTitle));Detail(()=>L.T(CodeTable.ProfileNotAppliedText)+"\n"+L.T(CodeTable.ProfileNotAppliedAction));}
   subLabel.Text=statusDetail;
   detailsLink.LinkColor=info.Kind==ActionKind.Details||instruction!=null?Theme.Amber:Theme.AccentHover;
   Log(info.Title+": "+info.Text+" "+action+" ["+lastCode+"]");
   if(!string.IsNullOrWhiteSpace(instruction)){Log("Что делать вручную:");LogBlock(instruction);ShowDetails(null);}
  }
  void ShowOperationFailure(string json,bool? gemini=null){
   try{using(var doc=JsonDocument.Parse(json)){var r=doc.RootElement;string code=r.TryGetProperty("Code",out var c)&&c.ValueKind==JsonValueKind.String?c.GetString():"";string ins=r.TryGetProperty("Instruction",out var i)&&i.ValueKind==JsonValueKind.String?i.GetString():null;ShowFailure(code,ins,gemini);}}
   catch{ShowFailure("ENGINE_ERROR");}
  }
  void SetBusy(bool value){busy=value;if(value){opSerial++;checking=false;Title(()=>L.T("Выполняю…"));Detail(null);UpdateDetailsText();}UpdateButtons();}

  void Present(string output){
   last=output;bool wasActive=isActive;
   try{
    using(var doc=JsonDocument.Parse(output)){
     var root=doc.RootElement;var status=root.TryGetProperty("Status",out var st)?st:root;
     bool active=status.TryGetProperty("Active",out var a)&&a.ValueKind==JsonValueKind.True&&status.TryGetProperty("State",out var vs)&&vs.GetString()=="DNS active";
     bool hasPhase=status.TryGetProperty("JournalPhase",out var phase);known=hasPhase;
     string ph=hasPhase?phase.GetString():null;
     // The engine could not answer (WMI hangs, timeout) but the journal file can: a pending restore must keep "Восстановить" available and its manual steps reachable.
     if(!hasPhase){string journal=JournalReader();string fp=Recovery.PhaseOfJournal(journal);if(fp!=null){hasPhase=true;known=true;ph=fp;if(fp!="Active"&&fp!="Restored")lastInstruction=Recovery.BuildInstruction(journal);}}
     if(hasPhase)needsRestore=ph!="Absent"&&ph!="Restored";
     // "Requires recovery": the journal is neither Active nor Restored (an interrupted apply or restore). One button: "Восстановить".
     recoveryNeeded=hasPhase&&(ph=="Applying"||ph=="Restoring"||ph=="Failed");
     if(hasPhase&&!needsRestore)health=null;   // switched off (or never on): there is nothing left to repair and the automation must not suggest otherwise
     string code=(status.TryGetProperty("Code",out var c)&&c.ValueKind==JsonValueKind.String?c.GetString():null)??(root.TryGetProperty("Code",out var rc)&&rc.ValueKind==JsonValueKind.String?rc.GetString():"");
     // A drift the health check found stays visible until a check (or the user's own operation) clears it; a routine status must not repaint it green.
     bool drift=health!=null&&health.Kind==HealthKind.Drift&&hasPhase&&needsRestore&&!recoveryNeeded;
     if(drift)active=false;
     isActive=active;if(!active)checksPassed=false;else offer=null;   // a bridge that works needs no offer to leave it
     Detail(null);
     if(active)Title(()=>L.T("Мост подключён"));
     else if(drift){var hi=StatusInfo(health.Code);lastCode=hi.Code;}
     else if(recoveryNeeded){var ri=StatusInfo("RECOVERY_REQUIRED");lastCode=ri.Code;}
     else if(hasPhase&&!needsRestore&&code!="NRPT_OR_HOSTS_CONFLICT")Title(()=>L.T("Мост отключён"));
     else{var ci=StatusInfo(code);lastCode=ci.Code;}
     string pName=status.TryGetProperty("ProfileName",out var pe)&&pe.ValueKind==JsonValueKind.String?pe.GetString():null;
     string pTmpl=status.TryGetProperty("Template",out var te)&&te.ValueKind==JsonValueKind.String?te.GetString():null;
     int hit=active||needsRestore?profiles.FindIndex(p=>pTmpl!=null&&p.Template==pTmpl):-1;
     if(hit<0&&(active||needsRestore)&&pName!=null){var known0=ProfileCatalog.BuiltIn.Find(pName);hit=profiles.FindIndex(p=>p.Name==pName||(known0!=null&&p.Name==known0.Id));}
     if(hit>=0)profileSelect.SetSelectedSilently(hit);
     // The scope of a bridge that exists is the journal's, not the toggle's: a cancelled or failed change must not leave the toggle on the wrong side.
     if(needsRestore&&status.TryGetProperty("Targets",out var tgs)&&tgs.ValueKind==JsonValueKind.Array){var scope=tgs.EnumerateArray().Select(x=>x.GetString()).Where(x=>x!=null).ToList();var sc=ScopeOf(scope);if(scope.Count>0&&sc!=ScopeNow)SetScope(sc);}
     addrSource=active?AddressSourceText(status,pTmpl):"";
     activeTemplate=active?pTmpl:null;activeServers=active&&status.TryGetProperty("Servers",out var asv)&&asv.ValueKind==JsonValueKind.Array?asv.EnumerateArray().Select(x=>x.GetString()).Where(x=>!string.IsNullOrEmpty(x)).ToArray():new string[0];
     if(!active)speed=null;
     Log("Статус: "+statusLabel.Text+(string.IsNullOrEmpty(code)?"":" ("+code+")"));
    }
   }catch{var ei=StatusInfo("ENGINE_ERROR");Log("Ответ служебного скрипта не удалось разобрать");}
   UpdateDetailsText();UpdateButtons();lastStatus=DateTime.UtcNow;
   if(wasActive!=isActive)Notify(isActive?L.T("Мост подключён"):L.T("Мост отключён"));
   if(isActive)_=MeasureSpeedAsync(false);
  }

  // ---- ping of the working bridge (main page chip) ----
  // Read-only network work (one DoH query, three validated TLS handshakes), in the background, at most once per BridgeSpeed.Fresh unless the person clicks the chip.
  // Never in smoke renders or self-tests without a test source, never while an operation runs; a result from before an operation is dropped.
  async Task MeasureSpeedAsync(bool force){
   if(!isActive||busy||speedRunning||exiting||IsDisposed)return;
   if(smoke&&SpeedSource==null)return;
   string tmpl=activeTemplate;var servers=activeServers;
   if(string.IsNullOrEmpty(tmpl)||servers.Length==0)return;
   string host=Program.Target,key=tmpl+"|"+string.Join(",",servers)+"|"+host;
   bool fresh=speed!=null&&speedKey==key&&DateTime.UtcNow-speed.Utc<BridgeSpeed.Fresh;
   if(fresh&&!force)return;
   bool announce=force||speedKey!=key;
   speedRunning=true;if(announce)speed=null;speedKey=key;int serial=opSerial;UpdateSummary();
   SpeedResult r=null;
   try{
    r=SpeedSource!=null?await SpeedSource(tmpl,servers,lifeCts.Token)
     :await Task.Run(async()=>{using(var live=new BridgeSpeed.Live())return await BridgeSpeed.MeasureAsync(live,tmpl,servers,host,lifeCts.Token);});
   }catch(OperationCanceledException){}
   catch(Exception e){Log("Пинг не измерен: "+e.Message);}
   finally{speedRunning=false;}
   if(IsDisposed)return;
   if(r!=null&&isActive&&serial==opSerial&&speedKey==key){
    speed=r;
    if(announce||r.Ms<0)Log(r.Ms>=0?"Пинг до сервера-посредника провайдера: "+r.Ms+" мс ("+r.Proxy+")":"Пинг не измерен: "+r.Error+(r.Proxy==null?"":" ("+r.Proxy+")"));
   }
   UpdateButtons();
  }
  internal Task MeasureSpeedForTest(bool force)=>MeasureSpeedAsync(force);
  // Where the addresses of a profile to measure come from: its pins, else what an earlier search found. No address = no number (nothing is searched for here).
  string[] ServersFor(ProfileItem p){
   if(p.Servers!=null&&p.Servers.Length>0)return p.Servers;
   return SavedAddresses.Usable(SavedAddresses.Find(settings.SavedAddrs,p.Template),DateTime.UtcNow);
  }
  // Opening the list measures the profiles whose number is missing or older than BridgeSpeed.Fresh (the working bridge's own profile has its own measurement).
  // Read-only, in the background, three at a time, never while an operation runs; the list stays usable and fills in as the answers come.
  async Task MeasureProfilesAsync(bool force){
   if(busy||benchRunning||pingsRunning||exiting||IsDisposed)return;
   if(smoke&&SpeedSource==null)return;
   var jobs=profiles.Where(p=>!(isActive&&string.Equals(p.Template,activeTemplate,StringComparison.OrdinalIgnoreCase))).Select(p=>(Template:p.Template,Servers:ServersFor(p))).Where(j=>j.Servers.Length>0).ToList();
   lock(pings)jobs=jobs.Where(j=>force||!pings.TryGetValue(j.Template,out var old)||DateTime.UtcNow-old.Utc>=BridgeSpeed.Fresh).ToList();
   if(jobs.Count==0)return;
   pingsRunning=true;string host=Program.Target;var gate=new SemaphoreSlim(3);
   try{
    await Task.WhenAll(jobs.Select(async j=>{
     await gate.WaitAsync(lifeCts.Token);
     try{
      SpeedResult r;
      try{r=SpeedSource!=null?await SpeedSource(j.Template,j.Servers,lifeCts.Token):await Task.Run(async()=>{using(var live=new BridgeSpeed.Live())return await BridgeSpeed.MeasureAsync(live,j.Template,j.Servers,host,lifeCts.Token);});}
      catch(OperationCanceledException){return;}
      catch{return;}
      lock(pings)pings[j.Template]=r;
      try{if(IsHandleCreated&&!IsDisposed)BeginInvoke(new Action(()=>profileSelect.RefreshDetails()));}catch{}
     }finally{gate.Release();}
    }));
   }catch(Exception){}   // cancelled, or the window went away while a measurement ran
   finally{pingsRunning=false;gate.Dispose();}
  }
  internal Task MeasureProfilesForTest(bool force)=>MeasureProfilesAsync(force);
  internal SpeedResult CurrentSpeed=>speed;

  // "Network checks passed" = the engine's own diagnosis found the policy effective and the Windows resolver answering with the provider's addresses.
  static bool DiagnosisPassed(string output){
   try{using(var doc=JsonDocument.Parse(output))return doc.RootElement.TryGetProperty("NrptEffective",out var n)&&n.ValueKind==JsonValueKind.String&&n.GetString().StartsWith("Policy + Windows resolver answers match");}catch{return false;}
  }
  async Task Run(string action){
   currentCts?.Cancel();currentCts?.Dispose();currentCts=new CancellationTokenSource();var token=currentCts.Token;SetBusy(true);
   try{
    var p=Current;var result=await Program.Engine(action,false,false,p.Servers,p.Template,p.Custom?null:p.Name,token);
    if(action=="diagnose"){Log("Диагностика:");LogBlock(result.Output);checksPassed=DiagnosisPassed(result.Output);lastDiagnosis=result;history.Append("diagnose",CodeOfOutput(result.Output),result.Code==0,p.Custom?"custom":p.Name);}
    Present(result.Output);
   }catch(OperationCanceledException){Title(()=>L.T("Операция отменена"));}
   finally{SetBusy(false);}
  }
  internal Func<string,bool,Task> MutateHook;   // test seam: stands in for the whole operation (nothing reaches the engine or UAC)
  async Task Mutate(string action,bool repair=false){
   if(MutateHook!=null){await MutateHook(action,repair);return;}
   var p=Current;bool geminiAtStart=Program.Gemini;   // the scope this operation was asked for; a status read in the middle may move the toggle back
   if(action=="enable"&&!EnsureTrust(p))return;
   currentCts?.Cancel();currentCts?.Dispose();currentCts=new CancellationTokenSource();var token=currentCts.Token;SetBusy(true);
   Log(action=="enable"?(repair?"Починка моста: ":"Подключение моста: ")+p.Name+"…":"Отключение моста…");
   try{
    string pn=p.Custom?null:p.Name;
    string[] servers=p.Servers;AddressPlan plan=null;
    // an unelevated, short CIM/WMI pre-check before any UAC prompt; if WMI does not answer nothing is started.
    // the address check is read-only network work, so it runs while the WMI check is still going (one wait instead of two).
    Title(()=>action=="enable"?L.T("Проверяю провайдера…"):L.T("Проверяю службу WMI…"));
    var guardTask=Recovery.CheckCimAsync(p.Servers,p.Template,pn,token);
    Task<AddressPlan> planTask=null;
    if(action=="enable"){var savedAddrs=SavedAddresses.Find(settings.SavedAddrs,p.Template);planTask=Task.Run(()=>AddressResolver.ResolveLiveAsync(p.Template,p.Servers,token,savedAddrs));}
    var guard=await guardTask;
    if(!guard.Allowed&&planTask!=null)_=planTask.ContinueWith(x=>{_=x.Exception;},TaskScheduler.Default);   // nothing is started; the address check just ends on its own
    if(!guard.Allowed){history.Append(action,guard.Code,false,p.Custom?"custom":p.Name);string blocked=Recovery.BlockedJson(action,guard,Recovery.ReadJournal());Log("Проверка WMI: "+guard.Code);LogBlock(blocked);ShowOperationFailure(blocked);return;}
    if(action=="enable"){
     // pins are a hint. If none passes preflight (or the profile has none) the provider name is bootstrapped before UAC;
     // no address found = nothing is started and an already working bridge is left alone.
     plan=await planTask;
     if(plan.Error!=null){history.Append(action,plan.Error,false,p.Custom?"custom":p.Name);Log("Адреса провайдера: "+plan.Error+(string.IsNullOrEmpty(plan.Detail)?"":" — "+plan.Detail));ShowFailure(plan.Error,null,geminiAtStart);if(ProviderSwitch.IsTrigger(plan.Error))await OfferAlternativeAsync(p,plan.Error,token);return;}
     servers=plan.Servers;
     Log("Адреса ("+AddressResolver.SourceText(plan.Source,plan.FoundUtc)+"): "+string.Join(", ",servers));
    }
    Title(()=>L.T("Подтвердите запрос Windows…"));
    var result=await Program.Elevate(action,action=="enable",false,servers,p.Template,pn,token,repair,()=>{Title(()=>action=="enable"?(repair?L.T("Чиню мост…"):L.T("Подключаю мост…")):L.T("Отключаю мост…"));});
    // A restore that ran out of time is not "failed" and not "done": it is unconfirmed, with the manual steps from the journal.
    if(action=="restore"&&result.Code==1460)result=(1,Recovery.UnconfirmedJson("ELEVATION_TIMEOUT",Recovery.ReadJournal()));
    if(plan!=null&&result.Code==0){if(plan.Source=="found")SavedAddresses.Put(settings.SavedAddrs,p.Template,servers,plan.FoundUtc);settings.LastAddrSource=plan.Source;settings.LastAddrTime=plan.FoundUtc==default?DateTime.UtcNow.ToString("o"):plan.FoundUtc.ToString("o");settings.LastAddrServers=string.Join(",",servers);settings.LastAddrTemplate=p.Template;settings.Save();}
    string embedded=StatusFromOperation(action,result.Code,result.Output);
    if(embedded==null)Title(()=>L.T("Проверяю результат…"));
    var current=embedded!=null?(Code:0,Output:embedded):await Program.Engine("status",false,false,p.Servers,p.Template,pn,token);
    if(action=="enable"&&result.Code==0)checksPassed=true;   // the engine only reports success after the Windows resolver answered with the provider's addresses
    using(var status=JsonDocument.Parse(current.Output))using(var operation=JsonDocument.Parse(result.Output))
     Settle(JsonSerializer.Serialize(new{State=status.RootElement.GetProperty("State").GetString(),Status=status.RootElement,Operation=operation.RootElement},Program.JsonOptions));
    history.Append(repair?"repair":action,CodeOfOutput(result.Output),result.Code==0,p.Custom?"custom":p.Name);
    if(result.Code!=0){
     Log("Операция завершилась с кодом "+result.Code);LogBlock(result.Output);
     ShowOperationFailure(result.Output,geminiAtStart);
     if(action=="enable"&&ProviderSwitch.IsTrigger(CodeOfOutput(result.Output)))await OfferAlternativeAsync(p,CodeOfOutput(result.Output),token);
    }else{Log(action=="enable"?(repair?"Мост починен":"Мост подключён"):"Мост отключён");lastCode="";lastInstruction=null;}
   }catch(OperationCanceledException){Title(()=>L.T("Операция отменена"));}
   finally{SetBusy(false);if(action=="enable"&&healthSched!=null)healthSched.Trigger(HealthTrigger.Manual);}
  }

  // The elevated engine ends with the status after its own work (enable: the status itself; restore: a Status field), so a successful operation needs no
  // second engine start (~1.4 s). Anything unexpected = null, and the window asks the engine as before.
  internal static string StatusFromOperation(string action,int code,string output){
   if(code!=0||string.IsNullOrWhiteSpace(output))return null;
   try{
    using(var doc=JsonDocument.Parse(output)){
     var r=doc.RootElement;
     if(action=="restore")r=r.TryGetProperty("Status",out var st)?st:default;
     if(r.ValueKind!=JsonValueKind.Object||!r.TryGetProperty("State",out var s)||s.ValueKind!=JsonValueKind.String||!r.TryGetProperty("JournalPhase",out _))return null;
     return r.GetRawText();
    }
   }catch{return null;}
  }

  // ---- start-up and background status ----
  // The journal (a plain file, readable without elevation) says at once what the program has configured; the engine's status confirms it in the background.
  // Until it does, a configured bridge is shown as "checking" in neutral colours: never green before the engine has confirmed it.
  readonly CancellationTokenSource lifeCts=new CancellationTokenSource();
  bool checking,refreshing;int opSerial;
  bool Checking=>checking&&needsRestore&&!recoveryNeeded&&!busy;
  void ShowJournalHint(){
   string journal=JournalReader();string ph=Recovery.PhaseOfJournal(journal);
   known=true;checking=true;Detail(null);isActive=false;checksPassed=false;addrSource="";
   if(ph==null||ph=="Restored"){needsRestore=false;recoveryNeeded=false;Title(()=>L.T("Мост отключён"));}
   else if(ph=="Active"){needsRestore=true;recoveryNeeded=false;Title(()=>L.T("Мост подключён"));}
   else{needsRestore=true;recoveryNeeded=true;var ri=StatusInfo("RECOVERY_REQUIRED");lastCode=ri.Code;lastInstruction=Recovery.BuildInstruction(journal);}
   UpdateDetailsText();UpdateButtons();
  }
  internal void ShowJournalHintForTest()=>ShowJournalHint();
  internal bool CheckingShown=>Checking;
  internal Func<CancellationToken,Task<(int Code,string Output)>> StatusSource;   // test seam: the engine status of RefreshStatus
  internal Task RefreshForTest()=>RefreshStatus();
  internal bool LogoSpinning=>bigLogo.Spinning;
  internal bool KeyForTest(Keys k){var m=new Message();return ProcessCmdKey(ref m,k);}
  internal int SelectedTabIndex=>nav.Selected;
  internal bool DetailsVisible=>detailsPanel.Visible;
  // A status read that does not lock the window. A user operation started meanwhile wins: its result is newer, so this one is dropped.
  async Task RefreshStatus(){
   if(busy||refreshing||exiting||IsDisposed)return;
   refreshing=true;int serial=opSerial;UpdateButtons();
   try{
    var p=Current;
    var r=StatusSource!=null?await StatusSource(lifeCts.Token):await Program.Engine("status",false,false,p.Servers,p.Template,p.Custom?null:p.Name,lifeCts.Token);
    if(!busy&&serial==opSerial&&!IsDisposed){checking=false;Present(r.Output);}
   }catch(OperationCanceledException){}
   catch(Exception e){Log("Проверка состояния не удалась: "+e.Message);}
   finally{refreshing=false;if(!IsDisposed){if(!busy)checking=false;UpdateDetailsText();UpdateButtons();}}
  }

  // ---- comfort ----
  protected override bool ProcessCmdKey(ref Message msg,Keys keyData){
   if(keyData==Keys.Escape&&browserPanel!=null&&browserPanel.Visible){CloseBrowserPanel(false);return true;}
   if(keyData==Keys.Escape&&detailsPanel!=null&&detailsPanel.Visible){detailsPanel.Visible=false;return true;}
   if(keyData==Keys.F5&&!busy){var _=RefreshStatus();return true;}
   if((keyData&Keys.Control)==Keys.Control){int n=(int)(keyData&Keys.KeyCode)-(int)Keys.D1;if(n>=0&&n<NavBar.Count&&(keyData&~Keys.KeyCode)==Keys.Control){nav.Selected=n;return true;}}
   return base.ProcessCmdKey(ref msg,keyData);
  }
  void SaveWindowPosition(){
   if(smoke||WindowState!=FormWindowState.Normal||!Visible)return;
   if(settings.WinX!=Left||settings.WinY!=Top){settings.WinX=Left;settings.WinY=Top;settings.Save();}
  }
  // A saved position is used only if both ends of the title bar are on a screen that exists now (monitors change between runs).
  internal static bool TitleBarOnScreen(Rectangle r,Rectangle[] screens)=>screens.Any(s=>s.Contains(new Point(r.Left+40,r.Top+10))&&s.Contains(new Point(r.Right-40,r.Top+10)));
  void RestoreWindowPosition(){
   if(settings.WinX==int.MinValue)return;
   var r=new Rectangle(settings.WinX,settings.WinY,Width,Height);
   if(!TitleBarOnScreen(r,Screen.AllScreens.Select(s=>s.WorkingArea).ToArray()))return;
   StartPosition=FormStartPosition.Manual;Location=r.Location;
  }

  static string CodeOfOutput(string json){
   try{using(var doc=JsonDocument.Parse(json)){var r=doc.RootElement;if(r.TryGetProperty("Code",out var c)&&c.ValueKind==JsonValueKind.String)return c.GetString();if(r.TryGetProperty("Status",out var st)&&st.TryGetProperty("Code",out var c2)&&c2.ValueKind==JsonValueKind.String)return c2.GetString();}}catch{}
   return "";
  }

  // ---- 1.5-1: health check in the tray ----
  // Read-only and unelevated: journal file, the engine's `status` with a short deadline (the only CIM access), DoH and Windows-resolver reads. Never a benchmark,
  // never an automatic repair, and a bridge the user switched off is "Off" (nothing to check, nothing to restore).
  void InitHealth(){
   var options=healthOptions=new HealthOptions{Targets=Program.Hosts};
   healthSched=new HealthScheduler(()=>DateTime.UtcNow,options);healthMon=new HealthMonitor(new HealthMonitor.Live(),options);healthCts=new CancellationTokenSource();
   healthTimer=new System.Windows.Forms.Timer{Interval=5000};healthTimer.Tick+=async(_,__)=>await HealthTick();healthTimer.Start();
   try{System.Net.NetworkInformation.NetworkChange.NetworkAddressChanged+=OnNetworkChanged;Microsoft.Win32.SystemEvents.PowerModeChanged+=OnPowerChanged;}catch{}
   healthSched.Trigger(HealthTrigger.Startup);
  }
  void StopHealth(){
   try{System.Net.NetworkInformation.NetworkChange.NetworkAddressChanged-=OnNetworkChanged;Microsoft.Win32.SystemEvents.PowerModeChanged-=OnPowerChanged;}catch{}
   try{healthTimer?.Stop();healthTimer?.Dispose();healthCts?.Cancel();healthCts?.Dispose();}catch(ObjectDisposedException){}
   healthTimer=null;healthCts=null;
  }
  void OnNetworkChanged(object s,EventArgs e)=>healthSched?.Trigger(HealthTrigger.NetworkChange);
  void OnPowerChanged(object s,Microsoft.Win32.PowerModeChangedEventArgs e){if(e.Mode==Microsoft.Win32.PowerModes.Resume)healthSched?.Trigger(HealthTrigger.Resume);}
  async Task HealthTick(){
   if(healthSched==null||healthRunning||busy||benchRunning||exiting)return;
   if(!healthSched.TryStart(out var trigger))return;
   healthRunning=true;HealthReport report=null;var token=healthCts.Token;
   try{report=await Task.Run(()=>healthMon.CheckAsync(trigger,token));}
   catch(OperationCanceledException){healthSched.Finished(HealthKind.Unknown);healthRunning=false;return;}
   catch(Exception e){Log("Проверка моста не удалась: "+e.Message);healthSched.Finished(HealthKind.Unknown);healthRunning=false;return;}
   healthRunning=false;
   healthSched.Finished(report.Kind,report.Kind==HealthKind.Suspect);
   if(IsDisposed)return;
   // An operation started during the check: this report describes the state before it, so it is not shown. The check runs again once the operation has settled.
   if(busy){healthSched.Trigger(HealthTrigger.Manual);return;}
   ApplyHealth(report);
  }
  internal void ApplyHealth(HealthReport r){
   // An offer to switch provider belongs to a provider failure. A healthy report (the provider answers again) or a drift that is not about the provider (the rules)
   // makes it stale: it must neither linger nor hide "Починить". A provider-down drift keeps it (the repair has already failed once).
   if(offer!=null&&(r.Kind==HealthKind.Healthy||(r.Kind==HealthKind.Drift&&r.Code!=HealthMonitor.ProviderDown))){offer=null;UpdateButtons();}
   // The light provider check knows only about the provider. A healthy light report clears an earlier provider-down drift and nothing else;
   // a failing one never replaces a different drift the full check has shown (the repair covers both).
   if(r.Light&&r.Kind==HealthKind.Healthy){
    if(health!=null&&health.Code==HealthMonitor.ProviderDown){lastCheck=r;health=null;lastNotifiedCode=null;if(!string.IsNullOrEmpty(last))Present(last);else{UpdateDetailsText();UpdateButtons();}}
    return;
   }
   if(r.Light&&r.Kind==HealthKind.Drift&&health!=null&&health.Code!=r.Code)return;
   lastCheck=r;
   if(r.Kind==HealthKind.Unknown){Log("Проверка моста не удалась ("+r.Detail+"). Мост не менялся.");return;}
   if(r.Kind==HealthKind.Suspect){Log("Проверка моста: "+r.Code+", подтверждаю повторной проверкой.");return;}
   if(r.Kind==HealthKind.Off||r.Kind==HealthKind.Healthy){
    if(health!=null){health=null;lastNotifiedCode=null;if(r.Kind==HealthKind.Healthy&&!string.IsNullOrEmpty(last))Present(last);else{UpdateDetailsText();UpdateButtons();}}
    return;
   }
   // Drift
   health=r;var info=CodeTable.Lookup(r.Code);
   if(!recoveryNeeded&&r.Code!="RECOVERY_REQUIRED"){isActive=false;checksPassed=false;StatusInfo(r.Code);subLabel.Text=statusDetail;lastCode=info.Code;}
   UpdateButtons();
   if(lastNotifiedCode!=r.Code){
    lastNotifiedCode=r.Code;
    Log("Проверка моста: "+info.Title+". "+info.Text+" "+info.Action+" ["+r.Code+(string.IsNullOrEmpty(r.Detail)?"":": "+r.Detail)+"]");
    history.Append("health",r.Code,false,Current.Custom?"custom":Current.Name);
    Notify(info.Title+". "+info.Action);
   }
  }
  internal HealthReport CurrentHealth=>health;
  internal bool FixVisible=>fixBtn.Visible;
    internal bool AltVisible=>altBtn.Visible;
  // Layout checks of the main page: exactly one main (blue or red) button, it sits in the top slot, and no two visible buttons overlap.
  PillButton[] MainButtons=>new[]{bridge,fixBtn,altBtn};
  internal int PrimaryButtons=>MainButtons.Count(b=>b.Visible&&(b.Fill==Theme.Accent||b.Fill==Theme.Danger));
  internal bool PrimaryButtonOnTop{get{var v=MainButtons.Where(b=>b.Visible).ToList();var p=v.Where(b=>b.Fill==Theme.Accent||b.Fill==Theme.Danger).ToList();return p.Count==1&&v.All(b=>b==p[0]||b.Top>p[0].Top);}}
  internal bool MainButtonsOverlap{get{var v=MainButtons.Where(b=>b.Visible).ToList();for(int i=0;i<v.Count;i++)for(int j=i+1;j<v.Count;j++)if(v[i].Bounds.IntersectsWith(v[j].Bounds))return true;return false;}}
  internal bool OfferActive=>offer!=null;
  internal void SetOfferForTest(string profileId){offer=new SwitchOffer{ProfileId=profileId,FromId=Current.Name,Reason="BOOTSTRAP_NO_ADDRESS"};UpdateButtons();}
  internal string AltText=>altBtn.Text;
  // Smoke only: shows a drift on the main page without touching the system.
  internal void SmokeHealth(HealthReport r){known=true;needsRestore=true;isActive=false;recoveryNeeded=false;checksPassed=false;addrSource="";ApplyHealth(r);Application.DoEvents();}

  // ---- 1.5-2: support report (local file, preview first, nothing is sent) ----
  async Task SupportReportAsync(){
   if(busy){MessageBox.Show(this,L.T("Дождитесь конца текущей операции и повторите."),Text,MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
   await Run("diagnose");   // read-only; its result is part of the report
   var ctx=await Task.Run(()=>RedactionContext.Live());
   string text=SupportReport.Prepare(CollectReportInput(),ctx);
   ShowReportPreview(text);
  }
  internal ReportInput CollectReportInput(){
   string journal=JournalReader();string phase=Recovery.PhaseOfJournal(journal);
   return new ReportInput{GeneratedUtc=DateTime.UtcNow,ExeVersion=AppInfo.Version,CatalogVersion=ProfileCatalog.BuiltIn.Version,CatalogUpdated=ProfileCatalog.BuiltIn.Updated,
    Os=Environment.OSVersion.VersionString+(Environment.Is64BitOperatingSystem?" x64":" x86"),JournalPhase=phase??L.T("нет"),Profile=Current.Custom?L.T("свой профиль"):Current.Name,
    HealthKind=(health??lastCheck)==null?L.T("не выполнялась"):(health??lastCheck).Kind.ToString(),HealthCode=(health??lastCheck)?.Code,HealthUtc=(health??lastCheck)?.Utc.ToString("yyyy-MM-dd HH:mm"),Operations=history.Load(),
    CustomProfile=Current.Custom,DiagnosisJson=lastDiagnosis.Code==0?lastDiagnosis.Output:null,DiagnosisCode=lastDiagnosis.Code==0?null:CodeOfOutput(lastDiagnosis.Output??"")};
  }
  void ShowReportPreview(string text){
   using(var dlg=new ReportPreviewForm(text)){
    if(dlg.ShowDialog(this)==DialogResult.OK){
     using(var sfd=new SaveFileDialog{Title=L.T("Сохранить отчёт для поддержки"),Filter=L.T("Текстовый файл (*.txt)|*.txt"),FileName="AgDns-report-"+DateTime.Now.ToString("yyyyMMdd-HHmm")+".txt",OverwritePrompt=true}){
      if(sfd.ShowDialog(this)==DialogResult.OK){
       try{File.WriteAllText(sfd.FileName,text,new System.Text.UTF8Encoding(true));Log(CodeTable.Lookup("REPORT_SAVED").Title+": "+sfd.FileName);history.Append("report","REPORT_SAVED",true,"");MessageBox.Show(this,CodeTable.Lookup("REPORT_SAVED").Text+"\n"+CodeTable.Lookup("REPORT_SAVED").Action,Text,MessageBoxButtons.OK,MessageBoxIcon.Information);}
       catch(Exception e){Log("Отчёт не сохранён: "+e.Message);MessageBox.Show(this,CodeTable.Lookup("REPORT_SAVE_FAILED").Text+"\n"+CodeTable.Lookup("REPORT_SAVE_FAILED").Action,Text,MessageBoxButtons.OK,MessageBoxIcon.Warning);}
      }
     }
    }
   }
  }
  // ---- another provider when the current one has none ----
  // Starts only after the person's own connect/repair failed with "no usable address". Read-only (DoH queries and TLS handshakes of the benchmark kind).
  // The result is an offer behind a button; the existing bridge is never touched here. Carrying it out = trust dialog + the ordinary enable (compensated profile switch).
  async Task OfferAlternativeAsync(ProfileItem failed,string reason,CancellationToken ct){
   offer=null;Title(()=>L.T("Ищу рабочего провайдера…"));
   Log("Профиль «"+failed.Name+"» не дал рабочих адресов ("+reason+"). Проверяю остальные профили каталога: только чтение, мост не меняется.");
   SwitchOutcome res;
   try{using(var live=new LiveTransport())res=await Task.Run(()=>ProviderSwitch.FindAsync(ProviderSwitch.Pool(ProfileCatalog.BuiltIn,settings.SavedAddrs,DateTime.UtcNow),failed.Name,live,new BenchOptions(),ct));}
   catch(OperationCanceledException){return;}
   catch(Exception e){Log("Другого провайдера найти не удалось: "+e.Message);return;}
   if(res.Kind==SwitchKind.Cancelled)return;
   if(res.Kind==SwitchKind.Offer&&profiles.Any(x=>x.Name==res.Offer.ProfileId)){
    res.Offer.Reason=reason;offer=res.Offer;
    var info=CodeTable.Lookup("PROVIDER_SWITCH_OFFERED");lastCode=info.Code;
    SwitchOfferStatus(failed.Name,res.Offer.ProfileId);subLabel.Text=statusDetail;
    Log(info.Title+": «"+res.Offer.ProfileId+"» (проверено профилей: "+res.Checked+"). "+info.Action);
    history.Append("switch-offer",info.Code,true,res.Offer.ProfileId);
   }else{
    ShowFailure("NO_WORKING_PROVIDER");
    history.Append("switch-offer","NO_WORKING_PROVIDER",false,failed.Custom?"custom":failed.Name);
   }
   UpdateButtons();
  }
  async Task SwitchToOfferedAsync(){
   var o=offer;if(o==null||busy||!known)return;
   int i=profiles.FindIndex(x=>x.Name==o.ProfileId);if(i<0){offer=null;UpdateButtons();return;}
   await ProviderSwitch.ApplyAsync(o,id=>EnsureTrust(profiles[i]),async id=>{
    profileSelect.SetSelectedSilently(i);settings.Profile=profiles[i].Name;settings.Save();offer=null;UpdateDetailsText();
    Log("Переключение на «"+id+"»: доверие подтверждено, дальше обычное подключение.");
    await Mutate("enable");
   });
  }

  // Smoke only: renders the preview dialog with sample text (no system access) so its layout can be inspected.
  internal Bitmap SmokePreview(string text){
   using(var dlg=new ReportPreviewForm(text)){dlg.StartPosition=FormStartPosition.Manual;dlg.Location=new Point(-3000,-3000);dlg.Show();Application.DoEvents();var bmp=new Bitmap(dlg.Width,dlg.Height);dlg.DrawToBitmap(bmp,new Rectangle(Point.Empty,bmp.Size));dlg.Close();return bmp;}
  }
 }
}
