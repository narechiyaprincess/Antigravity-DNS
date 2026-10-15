using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AgDns {
 // UI pass v4 (owner's fourth pass, 2026-10-14): autostart without a question or a copy, the levels applied at once, the profile field with its ping,
 // the browser screen (order, one-sentence verdict, «Назад»), the language switched without a restart, the symmetric highlight of the tab bar.
 public static partial class OfflineTests {
  static bool PumpUntil(Func<bool> done,int maxMs=5000){var sw=Stopwatch.StartNew();while(!done()&&sw.ElapsedMilliseconds<maxMs){Application.DoEvents();System.Threading.Thread.Sleep(5);}Application.DoEvents();return done();}
  static bool IsPill(Bitmap b,int x,int y){var c=b.GetPixel(x,y);return Math.Abs(c.R-Theme.NavSel.R)+Math.Abs(c.G-Theme.NavSel.G)+Math.Abs(c.B-Theme.NavSel.B)<=6;}

  static void RunUiV4Tests() {
   RunV4NavTests();RunV4ControlTests();RunV4ScaleTests();RunV4AutostartUiTests();RunV4ScopeTests();RunV4LanguageTests();RunV4BrowserOrderTests();RunV4ReviewTests();
  }

  // ---- 6. the tab bar: the highlight is the same size for every tab, whole pixels, centred on its cell, concentric with the bar; the icon and the word are centred on it ----
  static void RunV4NavTests() {
   bool en0=L.En;
   try {
    var bad=new List<string>();int shots=0;
    foreach(bool en in new[]{false,true}){
     L.En=en;
     foreach(float k in new[]{1f,1.25f,1.5f,1.75f,2f}){
      int w=(int)Math.Round(352*k),h=(int)Math.Round(58*k);float cell=w/(float)NavBar.Count;var widths=new List<int>();var heights=new List<int>();
      for(int sel=0;sel<NavBar.Count;sel++){
       using(var bmp=new Bitmap(w,h))using(var g=Graphics.FromImage(bmp)){
        g.Clear(Theme.Bg);NavBar.Paint(g,w,h,k,sel);shots++;
        string who=(en?"en ":"ru ")+(int)(k*96)+"% tab"+sel;
        float cl=sel*cell,cr=(sel+1)*cell,cc=(cl+cr)/2f;int mid=h/2;
        int l=-1,r=-1;for(int x=(int)Math.Floor(cl);x<(int)Math.Ceiling(cr)&&x<w;x++)if(IsPill(bmp,x,mid)){if(l<0)l=x;r=x;}
        if(l<0){bad.Add(who+" no highlight");continue;}
        int pw=r-l+1;widths.Add(pw);
        double centre=(l+r+1)/2.0;if(Math.Abs(centre-cc)>1.0)bad.Add(who+" highlight off centre by "+(centre-cc).ToString("0.0"));
        double gapL=l-cl,gapR=cr-1-r;if(Math.Abs(gapL-gapR)>1.5)bad.Add(who+" gaps "+gapL.ToString("0.0")+" / "+gapR.ToString("0.0"));
        int cx=(int)Math.Round(centre);int top=-1,bot=-1;for(int y=0;y<h;y++)if(IsPill(bmp,cx,y)){if(top<0)top=y;bot=y;}
        heights.Add(bot-top+1);if(Math.Abs(top-(h-1-bot))>1)bad.Add(who+" top/bottom gaps "+top+" / "+(h-1-bot));
        if(top<(int)Math.Floor(3*k)||l-(int)cl<(int)Math.Floor(3*k))bad.Add(who+" highlight too close to the bar's edge");
        // rounded, not cut square: the corner pixel is background, and the four corners hold the same amount of colour
        int rad=(int)(12*k);int Count(int x0,int y0){int n=0;for(int y=y0;y<y0+rad;y++)for(int x=x0;x<x0+rad;x++)if(IsPill(bmp,x,y))n++;return n;}
        int a=Count(l,top),b=Count(r+1-rad,top),c=Count(l,bot+1-rad),d=Count(r+1-rad,bot+1-rad);
        if(IsPill(bmp,l,top)||IsPill(bmp,r,top)||IsPill(bmp,l,bot)||IsPill(bmp,r,bot))bad.Add(who+" a corner is square");
        int ctol=Math.Max(8,(int)(6*k));if(Math.Abs(a-b)>ctol||Math.Abs(c-d)>ctol){bad.Add(who+" corners differ "+a+","+b+","+c+","+d);}   // left against right (the word may reach the lower corners), and not square
        // the icon (upper half) and the word (lower half) are centred on the highlight and sit inside it
        int InkMin(int y0,int y1,out int max,out int minY,out int maxY){int mn=int.MaxValue;max=-1;minY=int.MaxValue;maxY=-1;for(int y=y0;y<y1;y++)for(int x=(int)cl+1;x<(int)cr-1&&x<w;x++){var px=bmp.GetPixel(x,y);if((px.R+px.G+px.B)/3>=95){mn=Math.Min(mn,x);max=Math.Max(max,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);}}return mn;}
        int iMax,iMinY,iMaxY,wMax,wMinY,wMaxY;int iMin=InkMin(top,top+(bot-top)*45/100,out iMax,out iMinY,out iMaxY),wMin=InkMin(top+(bot-top)*55/100,bot+1,out wMax,out wMinY,out wMaxY);
        if(iMax<0||wMax<0){bad.Add(who+" icon or word missing");continue;}
        double ic=(iMin+iMax+1)/2.0,wc=(wMin+wMax+1)/2.0,tol=1.5+.5*k;
        if(Math.Abs(ic-centre)>tol)bad.Add(who+" icon off centre by "+(ic-centre).ToString("0.0"));
        if(Math.Abs(wc-centre)>tol)bad.Add(who+" word off centre by "+(wc-centre).ToString("0.0"));
        if(wMin<l+1||wMax>r-1)bad.Add(who+" word leaves the highlight ("+wMin+".."+wMax+" in "+l+".."+r+")");
        if(wMaxY>bot-1||iMinY<top+1)bad.Add(who+" content touches the highlight's edge");
       }
      }
      if(widths.Count>0&&widths.Max()-widths.Min()>1)bad.Add((en?"en ":"ru ")+(int)(k*96)+"% widths "+string.Join(",",widths));
      if(heights.Count>0&&heights.Max()-heights.Min()>0)bad.Add((en?"en ":"ru ")+(int)(k*96)+"% heights "+string.Join(",",heights));
     }
    }
    Check(bad.Count==0,"tab bar: the highlight is one size for every tab, centred on its cell (same gap left and right, above and below), rounded with equal corners, and holds the icon and the word centred — 5 scales × 5 tabs × 2 languages ("+shots+" renders)"+(bad.Count==0?"":" — "+string.Join("; ",bad.Take(8))));
    // moving between two tabs the highlight slides without changing size
    int sw0=NavBar.Pill(0,352,58,1f,5).Width,sw1=NavBar.Pill(2.5f,352,58,1f,5).Width,sw2=NavBar.Pill(4,352,58,1f,5).Width;
    Check(sw0==sw1&&sw1==sw2,"tab bar: sliding between tabs keeps the size of the highlight");
   } finally { L.En=en0; }
  }

  // ---- the controls of this pass: levels while busy, the field with its ping ----
  static void RunV4ControlTests() {
   using(var seg=new Segmented{Bounds=new Rectangle(0,0,320,36),Items=new[]{"A","B","C"},Cumulative=true}) {
    seg.SelectedIndex=1;int changes=0;seg.SelectedIndexChanged+=(_,__)=>changes++;
    using(var calm=new Bitmap(320,36))using(var busy=new Bitmap(320,36)) {
     seg.DrawToBitmap(calm,new Rectangle(0,0,320,36));
     seg.Busy=true;seg.DrawToBitmap(busy,new Rectangle(0,0,320,36));
     int diff=0;for(int y=4;y<32;y++)for(int x=8;x<200;x++){var p=calm.GetPixel(x,y);var q=busy.GetPixel(x,y);if(Math.Abs(p.R-q.R)+Math.Abs(p.G-q.G)+Math.Abs(p.B-q.B)>12)diff++;}
     Check(diff>200,"levels: while a choice is being applied the highlight looks different (busy tone), so the control does not look idle or greyed out ("+diff+" px)");
    }
    seg.ClickForTest(2);Check(changes==0&&seg.SelectedIndex==1,"levels: while busy a click changes nothing");
    seg.Busy=false;seg.ClickForTest(2);Check(changes==1&&seg.SelectedIndex==2,"levels: once the work is done a click works again");
   }
   // the profile field: the name on the left, the ping (dot and words) before the chevron; a long name gives way, the ping never does
   float sysK;using(var probe=new Panel())sysK=probe.DeviceDpi/96f;   // a control paints at the scale of the screen it is on
   foreach(float k in new[]{sysK}) {
    int w=(int)Math.Round(332*k),h=(int)Math.Round(38*k);
    using(var host=new Panel{BackColor=Theme.Bg,Bounds=new Rectangle(0,0,w,h)})using(var ps=new PillSelect{Bounds=new Rectangle(0,0,w,h),Font=Theme.F(9.5f)}) {
     host.Controls.Add(ps);
     ps.Items.Add("dns.a-very-long-provider-name.example · P9  ·  эксп.");ps.SetSelectedSilently(0);
     Color dot=Theme.Green;string text="123 мс";ps.Detail=i=>(text,dot);
     using(var bmp=new Bitmap(w,h)) {
      host.DrawToBitmap(bmp,new Rectangle(0,0,w,h));
      int dotMin=int.MaxValue,dotMax=-1;for(int y=0;y<h;y++)for(int x=0;x<w;x++){var p=bmp.GetPixel(x,y);if(Math.Abs(p.R-dot.R)+Math.Abs(p.G-dot.G)+Math.Abs(p.B-dot.B)<40){dotMin=Math.Min(dotMin,x);dotMax=Math.Max(dotMax,x);}}
      int nameMax=-1,detailMin=int.MaxValue,detailMax=-1;for(int y=0;y<h;y++)for(int x=0;x<w;x++){var p=bmp.GetPixel(x,y);if(Math.Min(p.R,Math.Min(p.G,p.B))>=170){if(dotMax>=0&&x<dotMin)nameMax=Math.Max(nameMax,x);else if(dotMax>=0&&x>dotMax){detailMin=Math.Min(detailMin,x);detailMax=Math.Max(detailMax,x);}}}
      Check(dotMax>=0&&dotMin>w*.55&&dotMax<w-30*k&&nameMax>0&&nameMax<=dotMin-3*k&&detailMin>dotMax&&detailMax<w-30*k,"profile field "+(int)(k*96)+"%: the green dot and «123 мс» sit before the chevron and the long name stops short of them (name ends "+nameMax+", dot "+dotMin+"–"+dotMax+", words "+detailMin+"–"+detailMax+" of "+w+")");
      text="";ps.Invalidate();host.DrawToBitmap(bmp,new Rectangle(0,0,w,h));
      bool anyDot=false;for(int y=0;y<h&&!anyDot;y++)for(int x=0;x<w;x++){var p=bmp.GetPixel(x,y);if(Math.Abs(p.R-dot.R)+Math.Abs(p.G-dot.G)+Math.Abs(p.B-dot.B)<40){anyDot=true;break;}}
      Check(!anyDot,"profile field: no ping known -> no dot and no «—» in the field itself");
     }
    }
   }
   // a row of the list: a known ping has its dot and words, an unknown one is «—» without a dot
   using(var bmp=new Bitmap(120,30))using(var g=Graphics.FromImage(bmp))using(var f=Theme.F(9.5f)) {
    g.Clear(Theme.Card);PillSelect.PaintDetail(g,f,("",Color.Empty),new Rectangle(0,0,120,30),1f,true);
    int ink=0,green=0;for(int y=0;y<30;y++)for(int x=0;x<120;x++){var p=bmp.GetPixel(x,y);if((p.R+p.G+p.B)/3>70)ink++;if(Math.Abs(p.R-Theme.Green.R)+Math.Abs(p.G-Theme.Green.G)+Math.Abs(p.B-Theme.Green.B)<40)green++;}
    Check(ink>5&&green==0,"profile list: a row whose ping is not known shows «—» and no dot");
    g.Clear(Theme.Card);PillSelect.PaintDetail(g,f,("42 мс",Theme.Green),new Rectangle(0,0,120,30),1f,true);
    green=0;for(int y=0;y<30;y++)for(int x=0;x<120;x++){var p=bmp.GetPixel(x,y);if(Math.Abs(p.R-Theme.Green.R)+Math.Abs(p.G-Theme.Green.G)+Math.Abs(p.B-Theme.Green.B)<40)green++;}
    Check(green>=20,"profile list: a row with a known ping shows its dot");
   }
  }

  // ---- 3. the dense block under the logo at the scale of this screen (the other scales: a fresh process per scale, --ui-smoke --dpi N, see verify.ps1) ----
  static void RunV4ScaleTests() {
   bool en0=L.En;var bad=new List<string>();
   try {
    foreach(bool en in new[]{false,true}){
     L.En=en;
     using(var form=new MainForm(true)) {
      form.Show();Application.DoEvents();
      foreach(var p in form.ScaleProblems())bad.Add((en?"en ":"ru ")+p);
      form.Close();
     }
    }
   } finally { L.En=en0; }
   Check(bad.Count==0,"main block: at this screen's scale, in both languages, the button, the profile field and the levels keep equal gaps, equal fields and equal margins, nothing overlaps, everything stays on the page"+(bad.Count==0?"":" — "+string.Join("; ",bad.Take(6))));
  }

  // ---- 1. autostart in the window ----
  static void RunV4AutostartUiTests() {
   const string isoKey=@"Software\AgDnsTest\RunUi";string realKey=AutostartHelper.KeyName;
   Microsoft.Win32.Registry.CurrentUser.CreateSubKey(isoKey).Dispose();AutostartHelper.KeyName=isoKey;
   string dir=Path.Combine(Path.GetTempPath(),"agdns-asui-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
   bool en0=L.En;
   try {
    string self=Environment.ProcessPath;string other=Path.Combine(dir,"AntigravityDnsUnlocker.exe");File.Copy(self,other,true);
    string foreign=Path.Combine(dir,"foreign","PING.EXE");Directory.CreateDirectory(Path.GetDirectoryName(foreign));File.Copy(Environment.SystemDirectory+@"\PING.EXE",foreign,true);
    AutostartHelper.SetEnabled(true,other);
    using(var form=new MainForm(true)) {
     form.Show();Application.DoEvents();form.SelectTab(2);Application.DoEvents();
     Check(!form.AutostartOn&&form.AutostartSubtitle=="Автозапуск ведёт на другую копию"&&!form.PreviousCopyVisible&&AutostartHelper.SamePath(AutostartHelper.CurrentTarget(),other),"autostart window: an entry that names another copy shows the switch off and says so; the window asked nothing and changed nothing when it started");
     var rows0=form.SettingsRows;
     form.ToggleAutostartForTest(true);   // a question here would block the test: there is none
     Check(form.AutostartOn&&AutostartHelper.IsEnabled(self)&&form.AutostartSubtitle=="Следить за мостом при входе"&&form.PreviousCopyVisible&&form.PreviousCopyText=="Прежняя копия: "+other,"autostart window: turning it on moves the entry to this exe without a word and shows «Прежняя копия: <путь>» under the switch");
     var rows=form.SettingsRows;float s=form.LayoutScale;int pageH=form.PageHeightOf(2);
     bool apart=rows[1].Bottom<=rows[2].Top+1&&rows[2].Bottom<=rows[3].Top+1&&rows[3].Bottom<=rows[4].Top+1&&rows[4].Bottom<=rows[5].Top+1&&rows[5].Bottom<=rows[0].Bottom&&rows[0].Bottom<=rows[6].Top&&rows[6].Bottom<=pageH&&rows[3].Top>rows0[3].Top&&rows[2].Height>=(int)(30*s);
     Check(apart,"autostart window: the row «Прежняя копия» takes its own room between the switch and the next row; the cards below move down and stay on the page");
     // delete: asked first; refused while running; never a foreign file; only then gone
     string asked=null,notice=null;form.DeleteConfirm=p=>{asked=p;return false;};form.Notice=n=>notice=n;form.RunningProbe=p=>false;
     form.ClickDeletePreviousCopyForTest();
     Check(asked==other&&File.Exists(other)&&form.PreviousCopyVisible,"autostart window: «Удалить» asks first; «Нет» leaves the file and the row");
     form.DeleteConfirm=p=>true;form.RunningProbe=p=>true;form.ClickDeletePreviousCopyForTest();
     Check(File.Exists(other)&&form.PreviousCopyVisible&&notice!=null&&notice.Contains("запущена"),"autostart window: a previous copy that is running is not deleted, the person is told why");
     form.RunningProbe=p=>false;form.ClickDeletePreviousCopyForTest();
     Check(!File.Exists(other)&&File.Exists(self)&&!form.PreviousCopyVisible&&form.SettingsPreviousCopy()=="","autostart window: confirmed and not running -> the file is deleted, the row goes");
     Check(form.SettingsRows[3].Top==rows0[3].Top,"autostart window: the cards go back up when the row is gone");
     notice=null;form.SetPreviousCopyForTest(foreign);Check(form.PreviousCopyVisible,"autostart window: a remembered file that exists is shown");
     form.ClickDeletePreviousCopyForTest();
     Check(File.Exists(foreign)&&notice!=null&&notice.Contains("не файл этой программы")&&!form.PreviousCopyVisible,"autostart window: a file that is not this program is never deleted, even if it was remembered");
     form.SetPreviousCopyForTest(Path.Combine(dir,"vanished.exe"));
     Check(!form.PreviousCopyVisible,"autostart window: a remembered file that has vanished is forgotten");
     form.ToggleAutostartForTest(false);
     Check(!form.AutostartOn&&AutostartHelper.CurrentTarget()==null,"autostart window: turning it off removes the entry");
     form.ToggleAutostartForTest(true);
     Check(form.AutostartOn&&AutostartHelper.IsEnabled(self)&&!form.PreviousCopyVisible,"autostart window: turning it on with no entry names this exe and offers nothing to delete");
    }
    AutostartHelper.SetEnabled(false);
    // the row in English, and a long path still leaves the delete button its room
    L.En=true;
    using(var form=new MainForm(true)) {
     form.Show();Application.DoEvents();form.SelectTab(2);Application.DoEvents();
     string deep=Path.Combine(dir,"a very long folder name that goes on and on","and another one","AntigravityDnsUnlocker.exe");Directory.CreateDirectory(Path.GetDirectoryName(deep));File.Copy(self,deep,true);
     form.SetPreviousCopyForTest(deep);
     Check(form.PreviousCopyText=="Previous copy: "+deep&&form.AllVisibleTexts().Contains("Previous copy: "+deep)&&form.AllVisibleTexts().Contains("Delete"),"autostart window: the row is in English too (a switch rewrites it)");
     var rr=form.SettingsRows;Check(rr[2].Right<=rr[0].Right,"autostart window: the row stays inside its card with a long path");
    }
   } finally { L.En=en0;AutostartHelper.KeyName=realKey;try{Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\AgDnsTest",false);}catch{}try{Directory.Delete(dir,true);}catch{} }
  }

  // ---- 2. the levels are applied at once ----
  static void RunV4ScopeTests() {
   var cat=ProfileCatalog.BuiltIn;bool g0=Program.Gemini,g1=Program.Google;
   using(var form=new MainForm(true)) {
    form.Show();Application.DoEvents();
    try {
     string J(string[] targets)=>"{\"Phase\":\"Active\",\"Targets\":[\""+string.Join("\",\"",targets)+"\"]}";
     string core=J(cat.Targets),gem=J(cat.TargetsFor(true)),all=J(cat.TargetsForGoogle());string journal=core;form.JournalReader=()=>journal;
     int hooked=0;form.MutateHook=(a,r)=>{hooked++;return Task.CompletedTask;};
     // no bridge: remembered only, no operation, no busy state
     form.SmokeState("idle");form.SetScopeForTest(MainForm.BridgeScope.Core);form.ClickScopeForTest(MainForm.BridgeScope.Gemini);
     Check(hooked==0&&Program.Gemini&&!Program.Google&&form.ScopeIndex==1&&!form.ScopeBusy&&!form.BrowserPanelVisible,"levels: with no bridge a click is only remembered for the next connect (no operation, no question)");
     // a working bridge: applied at once, busy meanwhile, a second click is ignored, the new level stands
     form.SmokeState("active");form.SetScopeForTest(MainForm.BridgeScope.Core);journal=core;hooked=0;
     bool busyDuring=false,enabledDuring=false;int reentry=0;bool panelDuring=false;
     form.MutateHook=async(a,r)=>{hooked++;busyDuring=form.ScopeBusy;enabledDuring=form.ScopeControl.Enabled;int before=hooked;form.ClickScopeForTest(MainForm.BridgeScope.Google);reentry+=hooked-before;panelDuring=form.BrowserPanelVisible;await Task.Delay(80);journal=gem;};
     form.ClickScopeForTest(MainForm.BridgeScope.Gemini);
     bool done=PumpUntil(()=>hooked>=1&&!form.ScopeBusy,4000);
     Check(done&&hooked==1&&busyDuring&&enabledDuring&&reentry==0&&!panelDuring,"levels: with a working bridge a click is applied at once (one operation), the levels show they are busy and ignore a second click meanwhile");
     Check(form.ScopeIndex==1&&Program.Gemini&&!Program.Google&&!form.ScopeBusy&&form.ScopeControl.Enabled,"levels: when the operation ends the new level stands and the control is free again");
     // a failure: the usual error, the selector goes back, the old bridge stays
     hooked=0;journal=gem;form.SmokeState("active");form.SetScopeForTest(MainForm.BridgeScope.Gemini);
     form.MutateHook=async(a,r)=>{hooked++;await Task.Delay(40);form.ShowFailureForTest("UAC_CANCELLED");};
     form.ClickScopeForTest(MainForm.BridgeScope.Google);
     if(form.BrowserPanelVisible){Pump(form.BrowserWork);form.SelectBrowserForTest("edge");Pump(form.BrowserWork);form.BrowserContinueForTest();}
     done=PumpUntil(()=>hooked>=1&&!form.ScopeBusy,4000);
     Check(done&&form.ScopeIndex==1&&Program.Gemini&&!Program.Google&&form.StatusText==CodeTable.Lookup("UAC_CANCELLED").Title&&form.BridgeActive,"levels: a refusal puts the selector back, says the usual thing in the status and leaves the bridge as it was");
     // an exception inside the operation: nothing is left busy, the selector is back
     hooked=0;form.SmokeState("active");form.SetScopeForTest(MainForm.BridgeScope.Core);journal=core;
     form.MutateHook=(a,r)=>{hooked++;throw new InvalidOperationException("boom");};
     form.ClickScopeForTest(MainForm.BridgeScope.Gemini);done=PumpUntil(()=>hooked>=1&&!form.ScopeBusy,4000);
     Check(done&&form.ScopeIndex==0&&!Program.Gemini&&!form.ScopeBusy,"levels: an exception in the operation leaves nothing busy and puts the selector back");
     // no journal to read and no bridge afterwards: back to the old level
     hooked=0;form.SmokeState("active");form.SetScopeForTest(MainForm.BridgeScope.Core);journal="";
     form.MutateHook=(a,r)=>{hooked++;form.SmokeState("idle");return Task.CompletedTask;};
     form.ClickScopeForTest(MainForm.BridgeScope.Gemini);done=PumpUntil(()=>hooked>=1&&!form.ScopeBusy,4000);
     Check(done&&form.ScopeIndex==0&&!Program.Gemini,"levels: if the bridge is gone after a failed apply and nothing can be read, the selector returns to the old level");
     // «+ Браузер» with a working bridge: the browser panel, then the work at once; the panel's «Продолжить» is the only thing in between
     hooked=0;form.SmokeState("active");form.SetScopeForTest(MainForm.BridgeScope.Core);journal=core;bool busyGoogle=false;
     form.MutateHook=async(a,r)=>{hooked++;busyGoogle=form.ScopeBusy;await Task.Delay(40);journal=all;};
     form.ClickScopeForTest(MainForm.BridgeScope.Google);Pump(form.BrowserWork);
     Check(form.BrowserPanelVisible&&hooked==0,"levels: «+ Браузер» shows the browser panel first and applies nothing yet");
     form.SelectBrowserForTest("edge");Pump(form.BrowserWork);form.BrowserContinueForTest();done=PumpUntil(()=>hooked>=1&&!form.ScopeBusy,4000);
     Check(done&&busyGoogle&&form.ScopeIndex==2&&Program.Google,"levels: after «Продолжить» «+ Браузер» is applied at once and the selector shows it was busy");
     form.MutateHook=null;form.SmokeState("idle");form.SetScopeForTest(MainForm.BridgeScope.Core);
    } finally { Program.Gemini=g0;Program.Google=g1;form.Close(); }
   }
  }

  // ---- 5. language without a restart ----
  static void RunV4LanguageTests() {
   var shared=L.SharedEnglish().ToList();
   Check(shared.Count==0,"language: no two Russian phrases share one English phrase, so a switch back to Russian is exact"+(shared.Count==0?"":" — "+string.Join(" | ",shared.Take(6))));
   Check(L.Swap("Мост подключён",true)=="Bridge connected"&&L.Swap("Bridge connected",false)=="Мост подключён"&&L.Swap("@your_sister_loves_me",true)=="@your_sister_loves_me"&&L.Swap("",false)==""&&L.Swap(null,true)==null&&L.Swap("42 ms",false)=="42 ms","language: a known phrase swaps both ways; a name, a number or nothing stays");
   bool en0=L.En;
   try {
    L.En=false;
    using(var form=new MainForm(true)) {
     form.Show();Application.DoEvents();var bad=new List<string>();int rounds=0;
     foreach(string mode in new[]{"idle","checking","active","active-unchecked","recovery","conflict","wmi","drift","switch","noprovider"}){
      form.SmokeState(mode);
      for(int tab=0;tab<5;tab++){
       form.SelectTab(tab);Application.DoEvents();rounds++;
       var ru=form.AllVisibleTexts().ToList();
       form.LanguageControl.ClickForTest(1);var en=form.AllVisibleTexts().ToList();
       form.LanguageControl.ClickForTest(0);var ru2=form.AllVisibleTexts().ToList();
       string who=mode+"/tab"+tab;
       var cyr=en.Where(t=>Cyr.IsMatch(t)&&t!="Русский"&&t!="Русский · English").Take(2).ToList();if(cyr.Count>0)bad.Add(who+" still Russian: «"+string.Join("», «",cyr)+"»");
       if(!ru.SequenceEqual(ru2)){int i=0;while(i<ru.Count&&i<ru2.Count&&ru[i]==ru2[i])i++;bad.Add(who+" not restored: «"+(i<ru.Count?ru[i]:"")+"» vs «"+(i<ru2.Count?ru2[i]:"")+"»");}
       if(en.SequenceEqual(ru))bad.Add(who+" did not change");
      }
     }
     Check(bad.Count==0,"language: switching at once rewrites every text of every state and tab in English, and back in Russian, exactly as it was ("+rounds+" screens)"+(bad.Count==0?"":" — "+string.Join("; ",bad.Take(6))));
     // the saved choice, the tooltips, the profile list, nothing restarted
     form.SmokeState("idle");form.LanguageControl.ClickForTest(1);
     Check(L.En&&form.SettingsLanguage=="en"&&form.LanguageControl.SelectedIndex==1,"language: the choice is remembered in the settings and the window stays the same window");
     Check(form.ScopeSegmented.Tips[0]=="Only the Antigravity domains."&&form.ScopeSegmented.Items[2]=="+ Browser"&&form.ProfileLabels.All(l=>!l.Contains("эксп.")),"language: the tooltips, the levels and the profile list follow at once");
     form.LanguageControl.ClickForTest(0);
     Check(!L.En&&form.SettingsLanguage=="ru"&&form.ScopeSegmented.Tips[0]=="Только домены Antigravity.","language: and back");
     // a message from the table (a failure) is written again in the other language, title and text and action
     form.ShowFailureForTest("UAC_CANCELLED");var ruTitle=form.StatusText;var ruSub=form.SubText;
     form.LanguageControl.ClickForTest(1);
     Check(form.StatusText=="Operation cancelled"&&form.SubText.Contains("declined")&&form.SubText.Contains("Try again")&&ruTitle=="Операция отменена"&&ruSub.Contains("отклонён"),"language: a failure shown in the status is rewritten (title, text and action), not left in the old language");
     form.LanguageControl.ClickForTest(0);Check(form.StatusText=="Операция отменена"&&form.SubText==ruSub,"language: and rewritten back to the same Russian");
     // the open screens: the details panel and the browser panel
     form.SmokeDetails("xbox-dns.ru",false);form.LanguageControl.ClickForTest(1);
     Check(form.DetailsVisible&&form.DetailsText.Contains("Profile: ")&&!Cyr.IsMatch(form.DetailsText.Split('\n')[0]),"language: an open «Подробнее» panel is rewritten");
     form.LanguageControl.ClickForTest(0);form.SelectTab(0);
     form.SmokeState("idle");form.SmokeBrowserPanel("chrome",true);var ruHead=form.BrowserHeadline;form.LanguageControl.ClickForTest(1);
     Check(form.BrowserPanelVisible&&form.BrowserBackText=="Back"&&form.BrowserHeadline.Contains("Secure DNS")&&form.BrowserNames.Last()=="Other / not sure"&&ruHead.Contains("Безопасный DNS"),"language: an open browser panel is rewritten (the back button, the sentence, the last row)");
     form.LanguageControl.ClickForTest(0);Check(form.BrowserBackText=="Назад"&&form.BrowserHeadline==ruHead&&form.BrowserNames.Last()=="Другой / не знаю","language: and back");
     form.BrowserCancelForTest();
     // no switch in the middle of an operation
     form.SetBusyForTest(true);form.LanguageControl.ClickForTest(1);
     Check(!L.En&&form.LanguageControl.SelectedIndex==0,"language: no switch while an operation runs, the selector goes back");
     form.SetBusyForTest(false);
     // the tray
     form.SmokeState("idle");form.InitTrayForTest();var trayRu=form.TrayTexts;
     form.LanguageControl.ClickForTest(1);var trayEn=form.TrayTexts;form.LanguageControl.ClickForTest(0);var trayBack=form.TrayTexts;
     Check(trayEn.Length==6&&trayEn[0]=="Open window"&&trayEn[2]=="Connect bridge"&&trayEn[4]=="Exit"&&trayEn[5]=="Antigravity DNS — bridge disconnected"&&trayEn.All(t=>!Cyr.IsMatch(t))&&trayRu.SequenceEqual(trayBack)&&trayRu[0]=="Открыть окно","language: the tray menu and its tooltip follow at once and come back exactly");
     form.SmokeState("active");var trayOnRu=form.TrayTexts;form.LanguageControl.ClickForTest(1);
     Check(form.TrayTexts[1]=="Bridge: connected"&&form.TrayTexts[2]=="Disconnect"&&form.TrayTexts[5].StartsWith("Antigravity DNS — bridge connected"),"language: the tray follows the state of the bridge too (connected)");
     form.LanguageControl.ClickForTest(0);
     // the same, from a progress state that holds a function of the language (checking the provider)
     form.SmokeState("busy");string busyRu=form.StatusText;form.SetBusyForTest(false);
     form.Close();
    }
   } finally { L.En=en0; }
  }

  // ---- 4. the browsers: Chrome and Edge first, the rest as found, «Другой» last ----
  static void RunV4BrowserOrderTests() {
   var env=new MemoryBrowserEnv();
   foreach(var pair in new[]{("waterfox.exe",@"C:\Waterfox\waterfox.exe"),("oddbrowser.exe",@"C:\Odd\oddbrowser.exe"),("firefox.exe",FirefoxExe),("brave.exe",@"C:\b\brave.exe"),("msedge.exe",EdgeExe),("chrome.exe",ChromeExe)})
    env.AddFile(pair.Item2).SetReg(true,@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\"+pair.Item1,null,pair.Item2);
   env.SetReg(true,@"SOFTWARE\Clients\StartMenuInternet\Odd\shell\open\command",null,"\"C:\\Odd\\oddbrowser.exe\"");   // registered first by Windows
   var ids=BrowserCatalog.Detect(env).Select(b=>b.Id).ToList();
   Check(ids.Take(2).SequenceEqual(new[]{"chrome","edge"})&&ids.Count==6&&ids.Contains("exe:oddbrowser.exe"),"browsers: Chrome and Edge come first however Windows lists them, the others follow ("+string.Join(",",ids)+")");
   using(var form=new MainForm(true)) {
    form.Show();Application.DoEvents();
    try {
     form.BrowserEnv=env;form.SelectTab(2);form.ShowBrowserPanelForTest(false);Pump(form.BrowserWork);
     var names=form.BrowserNames;
     Check(names.Length==6&&names[0]=="Google Chrome"&&names[1]=="Microsoft Edge"&&names.Last()=="Другой / не знаю","browser window: the list reads Chrome, Edge, the others found, «Другой / не знаю» last");
     Check(!form.BrowserPanelControls.Any(c=>c.GetType().Name=="VerdictBox"),"browser window: there is no card for the verdict any more");
     // the verdict is one line in the sub-title position: right under the question, above the list, with its dot
     var lay=form.BrowserLayout();
     Check(lay[1].Top>=lay[6].Bottom&&lay[1].Bottom<=lay[0].Top&&form.VerdictControl.Visible&&lay[1].Height<=(int)(40*form.LayoutScale),"browser window: the verdict sits right under the question, above the list, in a line of at most two rows");
     form.BrowserBackForTest();
     Check(!form.BrowserPanelVisible,"browser window: «Назад» closes the screen");
     form.ShowBrowserPanelForTest(false);Pump(form.BrowserWork);form.KeyForTest(Keys.Escape);
     Check(!form.BrowserPanelVisible,"browser window: Esc closes the screen too");
    } finally { form.Close(); }
   }
  }

  // ---- review of 2026-10-14 (evening): a flash on a button and a language switch; the autostart path comparison; a wrong --dpi ----
  static void RunV4ReviewTests() {
   bool en0=L.En;
   try {
    L.En=false;
    using(var b=new PillButton{Text="Копировать"}) {
     MainForm.FlashForTest(b,"Скопировано",30);PumpUntil(()=>b.Text=="Копировать",2000);
     L.En=true;b.Text=L.Swap(b.Text,true);   // what the switch does to a button
     MainForm.FlashForTest(b,"Copied",30);bool back=PumpUntil(()=>b.Text!="Copied",2000);
     Check(back&&b.Text=="Copy","buttons: after a language switch a flash returns to the label in the new language, not the one of the first flash");
     L.En=false;MainForm.FlashForTest(b,"Скопировано",60);L.En=true;b.Text=L.Swap(b.Text,true);PumpUntil(()=>b.Tag==null,2000);
     Check(b.Text=="Copy","buttons: a switch in the middle of a flash ends with the label in the new language");
    }
   } finally { L.En=en0; }
   const string isoKey=@"Software\AgDnsTest\RunReview";string realKey=AutostartHelper.KeyName;
   Microsoft.Win32.Registry.CurrentUser.CreateSubKey(isoKey).Dispose();AutostartHelper.KeyName=isoKey;
   try {
    string self=Environment.ProcessPath;string dir=Path.GetDirectoryName(self);
    using(var k=Microsoft.Win32.Registry.CurrentUser.CreateSubKey(isoKey))k.SetValue(AutostartHelper.ValueName,"\""+Path.Combine(dir,".",Path.GetFileName(self)).ToUpperInvariant()+"\" --minimized");
    Check(AutostartHelper.IsEnabled(self),"autostart: an entry that names this exe in another spelling (./, case) counts as this exe");
   } finally { AutostartHelper.KeyName=realKey;try{Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\AgDnsTest",false);}catch{} }
   int code=Program.Main(new[]{"--ui-smoke",Path.Combine(Path.GetTempPath(),"agdns-dpi-"+Guid.NewGuid().ToString("N")+".png"),"--dpi","abc"});
   Check(code==1,"--ui-smoke: a --dpi that is not a scale is refused, not quietly ignored");
  }
 }
}
