using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;

namespace AgDns {
 public sealed class AppSettings {
  public bool CloseToTray=true;
  public bool StartHidden=true;
  public bool Notify=true;
  public bool Gemini;   // "+ Gemini" scope: off by default; the extra hosts come from the catalog
  public bool Google;   // "+ Браузер" scope (implies Gemini): the catalog's Google suffixes as well
  public string Language="ru";   // "ru" or "en"; switching restarts the program
  [System.Text.Json.Serialization.JsonIgnore]public bool NoSave;   // a render run or the self-test works on defaults and never writes the owner's file
  public string Browser="";   // the browser used for Google (BrowserCatalog id, "other" or empty); only read from the system, never changed there
  public bool TrayHintShown;
  public string PreviousCopy="";               // the exe the autostart entry named before it was moved here; offered for deletion, never deleted on its own
  public string Profile=ProfileCatalog.BuiltIn.Default;
  public string CustomName="";
  public string CustomServers="";
  public string CustomTemplate="";
  public int WinX=int.MinValue,WinY=int.MinValue;   // last window position (screen pixels); restored only if it is still on a screen
  // Where the addresses of the last successful connect came from: "pinned" or "found" (bootstrap) at LastAddrTime (UTC, round-trip format).
  public string LastAddrSource="",LastAddrTime="",LastAddrServers="",LastAddrTemplate="";
  // up to four addresses per profile that an earlier bootstrap found, with the date; tried after the pins and before a new bootstrap.
  public List<SavedAddr> SavedAddrs=new List<SavedAddr>();
  static string FilePath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AntigravityDnsUnlocker","settings.json");
  public static AppSettings Load(){
   try{if(File.Exists(FilePath)){var loaded=JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath),Program.JsonOptions)??new AppSettings();if(loaded.SavedAddrs==null)loaded.SavedAddrs=new List<SavedAddr>();return loaded;}}catch{}
   return new AppSettings();
  }
  public void Save(){
   if(NoSave)return;
   try{
    Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
    string tmp=FilePath+".tmp";File.WriteAllText(tmp,JsonSerializer.Serialize(this,Program.JsonOptions));
    if(File.Exists(FilePath))File.Replace(tmp,FilePath,null);else File.Move(tmp,FilePath);
   }catch{}
  }
 }


 // A paint that fails does not stop the program: the control stays as it was and the failure is written down (a short file next to the settings), so the cause can be read later.
 internal static class UiDiag {
  public static bool Enabled;   // on in the real window only; the self-test and the render run write nothing
  static int written;
  static string FilePath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AntigravityDnsUnlocker","ui-errors.log");
  public static void Thread(Exception e){Write("window",e);}
  public static void Paint(string who,Exception e)=>Write("paint of "+who,e);
  static void Write(string who,Exception e){
   if(!Enabled||written>=20)return;
   try{
    written++;Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
    var fi=new FileInfo(FilePath);if(fi.Exists&&fi.Length>64*1024)return;
    File.AppendAllText(FilePath,DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+"  "+who+" failed: "+e.ToString()+Environment.NewLine+Environment.NewLine);
   }catch{}
  }
 }

 internal static class Theme {
  public static readonly Color Bg=Color.FromArgb(10,10,12),Card=Color.FromArgb(24,24,29),Field=Color.FromArgb(14,14,18),Line=Color.FromArgb(48,48,56),
   Text=Color.FromArgb(236,236,242),Muted=Color.FromArgb(138,141,152),Accent=Color.FromArgb(70,134,255),AccentHover=Color.FromArgb(98,156,255),
   Orange=Color.FromArgb(255,138,61),Green=Color.FromArgb(82,224,138),Danger=Color.FromArgb(176,62,58),Soft=Color.FromArgb(40,40,48),NavSel=Color.FromArgb(28,40,70),
   Red=Color.FromArgb(240,98,92),Amber=Color.FromArgb(240,190,70);
  public static GraphicsPath Round(RectangleF r,float rad){
   var p=new GraphicsPath();float d=Math.Min(rad*2,Math.Min(r.Width,r.Height));
   if(d<=0){p.AddRectangle(r);return p;}
   p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
  }
  public static Color Mix(Color a,Color b,float t)=>Color.FromArgb((int)(a.R+(b.R-a.R)*t),(int)(a.G+(b.G-a.G)*t),(int)(a.B+(b.B-a.B)*t));
  static readonly HashSet<string> installed=LoadFamilies();
  static HashSet<string> LoadFamilies(){try{using(var c=new System.Drawing.Text.InstalledFontCollection())return new HashSet<string>(c.Families.Select(f=>f.Name),StringComparer.OrdinalIgnoreCase);}catch{return new HashSet<string>();}}
  public static bool Has(string family)=>installed.Contains(family);
  // Windows 11 ships Segoe UI Variable: its Display cut for large type, Text for the rest. Windows 10 falls back to Segoe UI.
  public static string Family(float pt,bool bold)=>Has("Segoe UI Variable Text")&&Has("Segoe UI Variable Display Semib")
   ?(pt>=14?(bold?"Segoe UI Variable Display Semib":"Segoe UI Variable Display"):(bold?"Segoe UI Variable Text Semibold":"Segoe UI Variable Text"))
   :(bold?"Segoe UI Semibold":"Segoe UI");
  public static Font F(float pt,bool bold=false)=>new Font(Family(pt,bold),pt);
  public static readonly string IconFont=Has("Segoe Fluent Icons")?"Segoe Fluent Icons":"Segoe MDL2 Assets";
  public static readonly string MonoFont=Has("Cascadia Mono")?"Cascadia Mono":"Consolas";
 }

 // One UI-thread clock for every small transition (hover, toggle, tab pill, logo). It runs only while something moves.
 // Off in smoke/self-test renders (instant states) and when Windows "Show animations" is off.
 internal static class Motion {
  public static bool Enabled=true;
  static System.Windows.Forms.Timer timer;
  static readonly List<Func<bool>> steps=new List<Func<bool>>();
  public static void Run(Func<bool> step){
   if(timer==null){timer=new System.Windows.Forms.Timer{Interval=15};timer.Tick+=(_,__)=>{for(int i=steps.Count-1;i>=0;i--){bool more;try{more=steps[i]();}catch{more=false;}if(!more)steps.RemoveAt(i);}if(steps.Count==0)timer.Stop();};}
   if(!steps.Contains(step))steps.Add(step);
   if(!timer.Enabled)timer.Start();
  }
  public static float Ease(float t)=>1f-(1f-t)*(1f-t)*(1f-t);   // ease-out cubic
 }
 internal sealed class Anim {
  readonly Control owner;readonly int ms;float from,to,val;long start;bool running;
  public Anim(Control owner,float initial,int ms){this.owner=owner;val=from=to=initial;this.ms=ms;}
  public float Value=>val;
  public float Target=>to;
  public void To(float target,bool instant=false){
   if(instant||!Motion.Enabled||!owner.IsHandleCreated||!owner.Visible){running=false;val=from=to=target;owner.Invalidate();return;}
   if(target==to)return;
   from=val;to=target;start=Stopwatch.GetTimestamp();
   if(!running){running=true;Motion.Run(Step);}
  }
  bool Step(){
   if(owner.IsDisposed){running=false;return false;}
   float t=(float)((Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency/ms);
   if(t>=1f){val=to;running=false;owner.Invalidate();return false;}
   val=from+(to-from)*Motion.Ease(t);owner.Invalidate();return true;
  }
 }

 public static class Branding {
  [DllImport("user32.dll")]static extern bool DestroyIcon(IntPtr h);
  public static void DrawLogo(Graphics g,float x,float y,float s,bool active)=>DrawLogo(g,x,y,s,active?1f:0f,-1f);
  // act: 0 = off (grey) .. 1 = on (colour). orbit >= 0: angle of the satellite that runs along the orbit while an operation is in progress.
  // px: the size the mark is finally shown at (a supersampled render draws at s = px * factor); stroke weights follow px so small icons stay legible.
  public static void DrawLogo(Graphics g,float x,float y,float s,float act,float orbit,float px=0){
   if(px<=0)px=s;
   var oldSm=g.SmoothingMode;var oldPo=g.PixelOffsetMode;var oldCq=g.CompositingQuality;
   g.SmoothingMode=SmoothingMode.AntiAlias;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.CompositingQuality=CompositingQuality.HighQuality;
   act=Math.Max(0f,Math.Min(1f,act));
   bool tiny=px<=24;
   float k=px<=20?1.75f:px<40?1.5f:px<72?1.18f:1f;   // thin strokes thicken as the icon shrinks
   // The planet grows less than the strokes, and below 40 px the apex of the mark moves down a little, so the two never melt into one blob.
   float kd=px<=24?1.2f:px<40?1.2f:px<72?1.1f:1f,apex=px<=24?.38f:px<40?.34f:.31f;
   // Tile background gradient and upper border.
   var tile=new RectangleF(x+s*.03f,y+s*.03f,s*.94f,s*.94f);float rad=s*.25f;
   using(var tilePath=Theme.Round(tile,rad)){
    using(var br=new LinearGradientBrush(tile,Color.FromArgb(36,41,66),Color.FromArgb(8,9,15),LinearGradientMode.Vertical))g.FillPath(br,tilePath);
    var clip=g.Clip;g.SetClip(tilePath,CombineMode.Intersect);
    // Upper-left highlight; blue glow when the bridge is active.
    using(var sheen=new GraphicsPath()){sheen.AddEllipse(x-s*.25f,y-s*.45f,s*1.1f,s*.95f);using(var pb=new PathGradientBrush(sheen){CenterColor=Color.FromArgb(28,150,170,255),SurroundColors=new[]{Color.FromArgb(0,150,170,255)}})g.FillPath(pb,sheen);}
    if(act>0)using(var glow=new GraphicsPath()){glow.AddEllipse(x+s*.12f,y+s*.22f,s*.76f,s*.66f);using(var pb=new PathGradientBrush(glow){CenterColor=Color.FromArgb((int)(78*act),64,128,255),SurroundColors=new[]{Color.FromArgb(0,64,128,255)}})g.FillPath(pb,glow);}
    g.Clip=clip;
    if(!tiny){
     float rw=Math.Max(1f,s*.009f);
     using(var rimPath=Theme.Round(new RectangleF(tile.X+rw/2,tile.Y+rw/2,tile.Width-rw,tile.Height-rw),rad-rw/2))
     using(var rim=new LinearGradientBrush(tile,Color.FromArgb(70,200,215,255),Color.FromArgb(10,200,215,255),LinearGradientMode.Vertical))
     using(var pen=new Pen(rim,rw))g.DrawPath(pen,rimPath);
    }
   }
   Color blue=Theme.Mix(Color.FromArgb(96,102,118),Color.FromArgb(84,152,255),act),orange=Theme.Mix(Color.FromArgb(128,130,140),Theme.Orange,act);
   float cxo=x+s/2,cyo=y+s*.52f,rx=s*.41f,ry=s*.15f,ow=s*.032f*k;
   var orbitRect=new RectangleF(-rx,-ry,rx*2,ry*2);
   void Satellite(){
    var st=g.Save();g.TranslateTransform(cxo,cyo);g.RotateTransform(-28);
    for(int i=5;i>=0;i--){
     double a=orbit-i*0.16;float qx=(float)(rx*Math.Cos(a)),qy=(float)(ry*Math.Sin(a));float r=s*(i==0?.05f:.04f-.004f*i);
     using(var b=new SolidBrush(Color.FromArgb(i==0?255:(int)(150-24*i),Theme.Orange)))g.FillEllipse(b,qx-r,qy-r,r*2,r*2);
    }
    g.Restore(st);
   }
   bool spinning=orbit>=0,behind=spinning&&Math.Sin(orbit)<=0;
   // Orbit, back half: dimmer and thinner, so the ring reads as passing behind the mark.
   var state=g.Save();g.TranslateTransform(cxo,cyo);g.RotateTransform(-28);
   using(var gb=new LinearGradientBrush(orbitRect,Color.FromArgb(tiny?230:150,blue),Color.FromArgb(tiny?230:150,orange),0f))using(var pen=new Pen(gb,ow*(tiny?1f:.85f)))g.DrawArc(pen,orbitRect,170,200);
   g.Restore(state);
   if(behind)Satellite();
   // A-shaped mark with shadow and a white-to-blue stroke.
   var chev=new[]{new PointF(x+s*.29f,y+s*.70f),new PointF(x+s*.5f,y+s*apex),new PointF(x+s*.71f,y+s*.70f)};
   float cw=s*.095f*k;
   if(!tiny){
    var sh=chev.Select(p=>new PointF(p.X,p.Y+s*.014f)).ToArray();
    using(var pen=new Pen(Color.FromArgb(120,0,0,6),cw*1.32f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round})g.DrawLines(pen,sh);
   }
   using(var gb=new LinearGradientBrush(new RectangleF(x,y+s*.28f,s,s*.46f),Theme.Mix(Color.FromArgb(196,200,210),Color.White,act),Theme.Mix(Color.FromArgb(128,132,146),Color.FromArgb(150,192,255),act),90f))
   using(var pen=new Pen(gb,cw){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round})g.DrawLines(pen,chev);
   // Orbit, front half: full strength, drawn over the mark.
   state=g.Save();g.TranslateTransform(cxo,cyo);g.RotateTransform(-28);
   using(var gb=new LinearGradientBrush(orbitRect,blue,orange,0f))using(var pen=new Pen(gb,ow){StartCap=LineCap.Round,EndCap=LineCap.Round})g.DrawArc(pen,orbitRect,4,166);
   g.Restore(state);
   if(spinning&&!behind)Satellite();
   // Sphere with an upper-left highlight and an active-state halo.
   float r0=s*.058f*kd;float cx=x+s*.5f,cy=y+s*(px<=24?.16f:px<40?.18f:.19f);
   int dotAlpha=spinning?110:255;
   if(act>0&&!spinning&&!tiny)using(var halo=new GraphicsPath()){halo.AddEllipse(cx-r0*2.3f,cy-r0*2.3f,r0*4.6f,r0*4.6f);using(var pb=new PathGradientBrush(halo){CenterColor=Color.FromArgb((int)((px<48?70:120)*act),orange),SurroundColors=new[]{Color.FromArgb(0,orange)}})g.FillPath(pb,halo);}
   using(var ball=new GraphicsPath()){
    ball.AddEllipse(cx-r0,cy-r0,r0*2,r0*2);
    if(tiny)using(var ob=new SolidBrush(Color.FromArgb(dotAlpha,orange)))g.FillPath(ob,ball);
    else using(var pb=new PathGradientBrush(ball){CenterPoint=new PointF(cx-r0*.35f,cy-r0*.4f),CenterColor=Color.FromArgb(dotAlpha,Theme.Mix(orange,Color.White,.45f)),SurroundColors=new[]{Color.FromArgb(dotAlpha,Theme.Mix(orange,Color.FromArgb(150,60,20),.35f))}})g.FillPath(pb,ball);
   }
   g.SmoothingMode=oldSm;g.PixelOffsetMode=oldPo;g.CompositingQuality=oldCq;
  }
  // The Telegram mark for the contact rows: a blue disc with the white paper plane.
  public static void DrawTelegram(Graphics g,RectangleF r){
   var old=g.SmoothingMode;g.SmoothingMode=SmoothingMode.AntiAlias;
   using(var br=new LinearGradientBrush(r,Color.FromArgb(55,174,226),Color.FromArgb(30,150,200),LinearGradientMode.Vertical))g.FillEllipse(br,r);
   PointF P(float x,float y)=>new PointF(r.X+r.Width*x,r.Y+r.Height*y);
   using(var plane=new GraphicsPath()){
    plane.AddPolygon(new[]{P(.225f,.49f),P(.735f,.285f),P(.655f,.715f),P(.505f,.6f),P(.425f,.675f),P(.415f,.55f),P(.66f,.355f),P(.37f,.525f)});
    using(var w=new SolidBrush(Color.White))g.FillPath(w,plane);
   }
   using(var fold=new SolidBrush(Color.FromArgb(200,222,240)))g.FillPolygon(fold,new[]{P(.415f,.55f),P(.425f,.675f),P(.505f,.6f)});
   g.SmoothingMode=old;
  }
  // Small sizes are drawn four times larger and reduced with a bicubic filter: thin strokes and the round ends come out clean instead of stair-stepped.
  public static Bitmap Render(int size,bool active)=>Render(size,active?1f:0f,-1f);
  public static Bitmap Render(int size,float act,float orbit){
   int f=size<=64?4:size<=160?2:1;
   var b=new Bitmap(size,size,PixelFormat.Format32bppArgb);
   using(var big=new Bitmap(size*f,size*f,PixelFormat.Format32bppPArgb)){
    using(var g=Graphics.FromImage(big)){g.Clear(Color.Transparent);DrawLogo(g,0,0,size*f,act,orbit,size);}
    using(var g=Graphics.FromImage(b)){g.Clear(Color.Transparent);g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.CompositingQuality=CompositingQuality.HighQuality;
     using(var ia=new ImageAttributes()){ia.SetWrapMode(WrapMode.TileFlipXY);g.DrawImage(big,new Rectangle(0,0,size,size),0,0,big.Width,big.Height,GraphicsUnit.Pixel,ia);}}
   }
   return b;
  }
  public static Icon ToIcon(int size,bool active){
   using(var b=Render(size,active)){IntPtr h=b.GetHicon();try{return (Icon)Icon.FromHandle(h).Clone();}finally{DestroyIcon(h);}}
  }
  // Every size Windows asks for at 100-200 % scaling (title bar, taskbar, Alt+Tab, Explorer), so it never has to stretch a neighbour.
  public static readonly int[] IconSizes={16,20,24,32,40,48,64,256};
  // The window icon: one Icon with all sizes, Windows picks the right one for each place.
  public static Icon IconSet(bool active){using(var ms=new MemoryStream(IcoBytes(active)))return new Icon(ms);}
  // ICO file: 32-bit DIB frames up to 64 px (read by every Windows API), PNG for 256 px.
  public static byte[] IcoBytes(bool active=true){
   var frames=new List<byte[]>();
   foreach(int sz in IconSizes)using(var b=Render(sz,active)){
    if(sz>=256){using(var ms=new MemoryStream()){b.Save(ms,ImageFormat.Png);frames.Add(ms.ToArray());}continue;}
    frames.Add(Dib(b));
   }
   using(var o=new MemoryStream())using(var w=new BinaryWriter(o)){
    w.Write((short)0);w.Write((short)1);w.Write((short)IconSizes.Length);int offset=6+16*IconSizes.Length;
    for(int i=0;i<IconSizes.Length;i++){int sz=IconSizes[i];w.Write((byte)(sz>=256?0:sz));w.Write((byte)(sz>=256?0:sz));w.Write((byte)0);w.Write((byte)0);w.Write((short)1);w.Write((short)32);w.Write(frames[i].Length);w.Write(offset);offset+=frames[i].Length;}
    foreach(var p in frames)w.Write(p);
    w.Flush();return o.ToArray();
   }
  }
  static byte[] Dib(Bitmap b){
   int n=b.Width,maskStride=(n+31)/32*4;
   using(var o=new MemoryStream())using(var w=new BinaryWriter(o)){
    w.Write(40);w.Write(n);w.Write(n*2);w.Write((short)1);w.Write((short)32);w.Write(0);w.Write(n*n*4+maskStride*n);w.Write(0);w.Write(0);w.Write(0);w.Write(0);
    var data=b.LockBits(new Rectangle(0,0,n,n),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
    try{var row=new byte[n*4];for(int yy=n-1;yy>=0;yy--){Marshal.Copy(IntPtr.Add(data.Scan0,yy*data.Stride),row,0,row.Length);w.Write(row);}}
    finally{b.UnlockBits(data);}
    w.Write(new byte[maskStride*n]);   // AND mask unused: the alpha channel carries the shape
    w.Flush();return o.ToArray();
   }
  }
 }

 internal sealed class DarkColors:ProfessionalColorTable{
  public override Color ToolStripDropDownBackground=>Theme.Card;
  public override Color ImageMarginGradientBegin=>Theme.Card;
  public override Color ImageMarginGradientMiddle=>Theme.Card;
  public override Color ImageMarginGradientEnd=>Theme.Card;
  public override Color MenuBorder=>Theme.Line;
  public override Color MenuItemBorder=>Theme.NavSel;
  public override Color MenuItemSelected=>Theme.Soft;
  public override Color MenuItemSelectedGradientBegin=>Theme.Soft;
  public override Color MenuItemSelectedGradientEnd=>Theme.Soft;
  public override Color SeparatorDark=>Theme.Line;
  public override Color SeparatorLight=>Theme.Line;
 }

 internal class Surface:Control{
  protected Surface(){SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.UserPaint|ControlStyles.SupportsTransparentBackColor|ControlStyles.Selectable,true);BackColor=Color.Transparent;}
  protected float K=>DeviceDpi/96f;
 }

 internal sealed class RoundPanel:Panel{
  public Color Fill=Theme.Card;public float Radius=18;public Color Border=Color.FromArgb(34,34,42);
  public RoundPanel(){SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.UserPaint|ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent;}
  protected override void OnPaint(PaintEventArgs e){
   var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
   using(var p=Theme.Round(new RectangleF(0,0,Width-1,Height-1),Radius*DeviceDpi/96f))using(var b=new SolidBrush(Fill)){g.FillPath(b,p);if(Border.A>0)using(var pen=new Pen(Border))g.DrawPath(pen,p);}
  }
 }

 internal sealed class LogoBox:Surface{
  readonly Anim act;bool spinning;float angle;long last;
  public LogoBox(){act=new Anim(this,0f,320);}
  [Browsable(false),DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]public bool Active{get=>act.Target>0.5f;set=>act.To(value?1f:0f);}
  [Browsable(false),DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]public bool Spinning{get=>spinning;set{if(spinning==value)return;spinning=value;if(value&&Motion.Enabled){last=Stopwatch.GetTimestamp();Motion.Run(Spin);}Invalidate();}}
  bool Spin(){
   if(IsDisposed||!spinning){Invalidate();return false;}
   long now=Stopwatch.GetTimestamp();angle+=(float)((now-last)*Math.PI*2/1.5/Stopwatch.Frequency);last=now;
   if(angle>Math.PI*2)angle-=(float)(Math.PI*2);
   Invalidate();return true;
  }
  protected override void OnPaint(PaintEventArgs e){
   int sz=Math.Min(Width,Height);float o=spinning?(Motion.Enabled?angle:2.3f):-1f;
   if(sz>=80){Branding.DrawLogo(e.Graphics,0,0,sz,act.Value,o);return;}
   // small marks (header, info card) go through the supersampled render, drawn 1:1
   using(var b=Branding.Render(sz,act.Value,o)){var g=e.Graphics;g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;g.DrawImage(b,new Rectangle(0,0,sz,sz));}
  }
 }

 internal sealed class PillButton:Surface{
  public Color Fill=Theme.Accent;Color fromFill=Theme.Accent;readonly Anim hoverA,downA,fillA;
  public PillButton(){Cursor=Cursors.Hand;Font=Theme.F(10,true);ForeColor=Color.White;hoverA=new Anim(this,0f,140);downA=new Anim(this,0f,90);fillA=new Anim(this,1f,220);}
  // The label is kept here, not in the native window text: a button is only painted, and the native text of a window created late came back empty.
  string label="";
  [Browsable(false),DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]public override string Text{get=>label;set{value=value??"";if(label==value)return;label=value;AccessibleName=value;OnTextChanged(EventArgs.Empty);}}
  Color CurrentFill=>Theme.Mix(fromFill,Fill,fillA.Value);
  public void SetFill(Color c){if(c==Fill)return;fromFill=CurrentFill;Fill=c;fillA.To(0f,true);fillA.To(1f);}
  protected override void OnMouseEnter(EventArgs e){hoverA.To(1f);base.OnMouseEnter(e);}
  protected override void OnMouseLeave(EventArgs e){hoverA.To(0f);downA.To(0f);base.OnMouseLeave(e);}
  protected override void OnMouseDown(MouseEventArgs e){downA.To(1f,true);base.OnMouseDown(e);}
  protected override void OnMouseUp(MouseEventArgs e){downA.To(0f);base.OnMouseUp(e);}
  protected override void OnEnabledChanged(EventArgs e){Invalidate();base.OnEnabledChanged(e);}
  protected override void OnTextChanged(EventArgs e){Invalidate();base.OnTextChanged(e);}
  protected override bool IsInputKey(Keys k)=>k==Keys.Space||k==Keys.Enter||base.IsInputKey(k);
  protected override void OnKeyUp(KeyEventArgs e){if(e.KeyCode==Keys.Space||e.KeyCode==Keys.Enter)OnClick(EventArgs.Empty);base.OnKeyUp(e);}
  // Glyph: an icon-font character drawn before the text. Subtle: a quiet tinted button (dark fill, hairline border, coloured text) for an action
  // that is available but should not shout, e.g. "Отключить мост" while everything works.
  string glyph;bool subtle;
  [Browsable(false),DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]public string Glyph{get=>glyph;set{if(glyph!=value){glyph=value;Invalidate();}}}
  [Browsable(false),DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]public bool Subtle{get=>subtle;set{if(subtle!=value){subtle=value;Invalidate();}}}
  const TextFormatFlags One=TextFormatFlags.SingleLine|TextFormatFlags.NoPadding;
  Font GlyphFont()=>new Font(Theme.IconFont,Font.SizeInPoints*.98f);
  // Width the content needs in device pixels (text, glyph, side padding); used to size the centred main button.
  public int PreferredWidth(){
   int w=TextRenderer.MeasureText(Text??"",Font,Size.Empty,One).Width;
   if(!string.IsNullOrEmpty(Glyph))using(var gf=GlyphFont())w+=TextRenderer.MeasureText(Glyph,gf,Size.Empty,One).Width+(int)(9*K);
   return w+(int)(52*K);
  }
  internal void PaintForSnapshot(Graphics g)=>OnPaint(new PaintEventArgs(g,new Rectangle(0,0,Width,Height)));
  internal void PerformClickForTest(){if(Enabled&&Visible)OnClick(EventArgs.Empty);}
  protected override void OnPaint(PaintEventArgs e){
   var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
   Color b=CurrentFill,ink=ForeColor,edge=Color.Empty;
   Color c;
   if(subtle){c=Theme.Mix(Theme.Mix(b,Theme.Bg,.8f),b,.16f*hoverA.Value);edge=Theme.Mix(b,Theme.Bg,.42f-.18f*hoverA.Value);ink=Theme.Mix(b,Color.White,.52f);c=Theme.Mix(c,Color.Black,.15f*downA.Value);}
   else c=Theme.Mix(Theme.Mix(b,Color.White,.14f*hoverA.Value),Color.Black,.15f*downA.Value);
   if(!Enabled){c=subtle?Theme.Mix(b,Theme.Bg,.78f):Theme.Mix(b,Theme.Bg,.45f);ink=Theme.Mix(Theme.Text,Theme.Bg,.3f);if(subtle)edge=Theme.Mix(b,Theme.Bg,.5f);}
   using(var p=Theme.Round(new RectangleF(.5f,.5f,Width-2,Height-2),(Height-2)/2f)){
    using(var br=new SolidBrush(c))g.FillPath(br,p);
    if(edge!=Color.Empty)using(var pen=new Pen(edge,1f))g.DrawPath(pen,p);
   }
   if(Focused&&ShowFocusCues)using(var p=Theme.Round(new RectangleF(2,2,Width-5,Height-5),(Height-5)/2f))using(var pen=new Pen(Color.FromArgb(90,255,255,255)))g.DrawPath(pen,p);
   var flags=TextFormatFlags.VerticalCenter|One;
   if(string.IsNullOrEmpty(Glyph)){TextRenderer.DrawText(g,Text,Font,new Rectangle(0,0,Width,Height),ink,flags|TextFormatFlags.HorizontalCenter|TextFormatFlags.EndEllipsis);return;}
   // glyph and text are centred together as one group
   using(var gf=GlyphFont()){
    int tw=TextRenderer.MeasureText(g,Text??"",Font,Size.Empty,One).Width,gw=TextRenderer.MeasureText(g,Glyph,gf,Size.Empty,One).Width,gap=(int)(9*K);
    int total=gw+gap+tw,x0=Math.Max((int)(12*K),(Width-total)/2);
    TextRenderer.DrawText(g,Glyph,gf,new Rectangle(x0,0,gw,Height),ink,flags);
    TextRenderer.DrawText(g,Text,Font,new Rectangle(x0+gw+gap,0,Math.Max(0,Width-(x0+gw+gap)-(int)(12*K)),Height),ink,flags|TextFormatFlags.EndEllipsis);
   }
  }
 }

 internal sealed class ToggleRow:Surface{
  bool on;readonly Anim onA;string sub;IconKind icon;bool divider;Color subColor=Theme.Muted;
  [Browsable(false),DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]public bool Checked{get=>on;set{if(on!=value){on=value;onA.To(on?1f:0f);CheckedChanged?.Invoke(this,EventArgs.Empty);}}}
  // Optional second line (what the switch does), an icon on the left and a hairline under the row (rows inside one card).
  [Browsable(false),DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]public string Subtitle{get=>sub;set{if(sub!=value){sub=value;Invalidate();}}}
  [Browsable(false),DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]public Color SubtitleColor{get=>subColor;set{if(subColor!=value){subColor=value;Invalidate();}}}
  [Browsable(false),DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]public IconKind Icon{get=>icon;set{icon=value;Invalidate();}}
  [Browsable(false),DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]public bool Divider{get=>divider;set{divider=value;Invalidate();}}
  public event EventHandler CheckedChanged;
  public ToggleRow(){Cursor=Cursors.Hand;Font=Theme.F(10);Height=40;onA=new Anim(this,0f,160);}
  protected override void OnClick(EventArgs e){if(Enabled)Checked=!Checked;base.OnClick(e);}
  protected override void OnEnabledChanged(EventArgs e){Invalidate();base.OnEnabledChanged(e);}
  protected override void OnTextChanged(EventArgs e){Invalidate();base.OnTextChanged(e);}
  protected override bool IsInputKey(Keys k)=>k==Keys.Space||base.IsInputKey(k);
  protected override void OnKeyUp(KeyEventArgs e){if(e.KeyCode==Keys.Space&&Enabled)Checked=!Checked;base.OnKeyUp(e);}
  protected override void OnPaint(PaintEventArgs e){
   var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;float k=K,v=onA.Value;
   int left=RowKit.Icon(g,icon,k,Height,Enabled);
   RowKit.Texts(g,Text,sub,Font,new Rectangle(left,0,(int)(Width-left-58*k),Height),Enabled?Theme.Text:Theme.Muted,subColor,k);
   float w=40*k,h=22*k,x=Width-w-2*k,y=(Height-h)/2;
   Color onColor=Enabled?Theme.Accent:Theme.Mix(Theme.Accent,Theme.Bg,.5f);
   using(var p=Theme.Round(new RectangleF(x,y,w,h),h/2))using(var b=new SolidBrush(Theme.Mix(Color.FromArgb(58,58,68),onColor,v)))g.FillPath(b,p);
   float d=h-6*k,kx=x+3*k+(w-d-6*k)*v;using(var b=new SolidBrush(Enabled?Color.White:Color.FromArgb(170,170,176)))g.FillEllipse(b,kx,y+3*k,d,d);
   if(divider)RowKit.Divider(g,left,Width,Height,k);
  }
 }

 internal enum IconKind{None,Power,Tray,Minimize,Bell,Globe,Server,Compass,Download}

 // Line icons of one family: a 16-unit grid, 1.5 stroke, round caps and joins, about 12 units of ink. Each is drawn symmetrically about the middle of
 // the grid; Nudge only corrects the few shapes that carry more ink on one side (the bell, the power sign), measured by the self-test on the real pixels.
 internal static class Icons{
  public static PointF Nudge(IconKind k){
   switch(k){
    case IconKind.Bell:return new PointF(0f,-.35f);
    case IconKind.Download:return new PointF(0f,-1f);
    default:return PointF.Empty;
   }
  }
  public static void Draw(Graphics g,IconKind kind,RectangleF box,Color ink){
   var saved=g.Save();var sm=g.SmoothingMode;var po=g.PixelOffsetMode;g.SmoothingMode=SmoothingMode.AntiAlias;g.PixelOffsetMode=PixelOffsetMode.Half;   // Half: coordinates are pixel edges, so a shape centred on the tile is centred on its pixels
   float s=box.Width/16f;var nd=Nudge(kind);
   g.TranslateTransform(box.X,box.Y);g.ScaleTransform(s,s);g.TranslateTransform(nd.X,nd.Y);
   using(var pen=new Pen(ink,1.5f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round})using(var fill=new SolidBrush(ink)) {
    switch(kind) {
     case IconKind.Power:
      g.DrawArc(pen,new RectangleF(3f,3.4f,10f,10f),-55f,290f);
      g.DrawLine(pen,8f,1.9f,8f,7.4f);
      break;
     case IconKind.Tray:   // a window over its taskbar, with one small icon in the tray corner
      using(var p=Theme.Round(new RectangleF(1.5f,2.5f,13f,11f),2.2f))g.DrawPath(pen,p);
      g.DrawLine(pen,1.5f,10f,14.5f,10f);
      g.FillEllipse(fill,10.6f,11.05f,1.9f,1.9f);
      break;
     case IconKind.Minimize:   // a window with a chevron that folds it away
      using(var p=Theme.Round(new RectangleF(1.5f,2.5f,13f,11f),2.2f))g.DrawPath(pen,p);
      g.DrawLine(pen,1.5f,5.9f,14.5f,5.9f);
      g.DrawLines(pen,new[]{new PointF(5.4f,8.4f),new PointF(8f,11f),new PointF(10.6f,8.4f)});
      break;
     case IconKind.Bell:
      using(var bell=new GraphicsPath()){
       bell.AddBezier(3.2f,11.2f,4.4f,10.2f,4.6f,8.8f,4.6f,6.6f);
       bell.AddBezier(4.6f,6.6f,4.6f,4.4f,6.1f,3.3f,8f,3.3f);
       bell.AddBezier(8f,3.3f,9.9f,3.3f,11.4f,4.4f,11.4f,6.6f);
       bell.AddBezier(11.4f,6.6f,11.4f,8.8f,11.6f,10.2f,12.8f,11.2f);
       bell.CloseFigure();g.DrawPath(pen,bell);
      }
      g.DrawLine(pen,8f,1.7f,8f,3.3f);
      g.DrawArc(pen,new RectangleF(6.2f,10.6f,3.6f,3.2f),0f,180f);
      break;
     case IconKind.Globe:
      g.DrawEllipse(pen,1.8f,1.8f,12.4f,12.4f);
      g.DrawEllipse(pen,5.1f,1.8f,5.8f,12.4f);
      g.DrawLine(pen,1.8f,8f,14.2f,8f);
      break;
     case IconKind.Server:
      using(var a=Theme.Round(new RectangleF(1.7f,2.2f,12.6f,4.6f),1.7f))g.DrawPath(pen,a);
      using(var b=Theme.Round(new RectangleF(1.7f,9.2f,12.6f,4.6f),1.7f))g.DrawPath(pen,b);
      g.FillEllipse(fill,3.7f,3.6f,1.8f,1.8f);g.FillEllipse(fill,3.7f,10.6f,1.8f,1.8f);
      break;
     case IconKind.Compass:
      g.DrawEllipse(pen,1.7f,1.7f,12.6f,12.6f);
      g.FillPolygon(fill,new[]{new PointF(10.9f,5.1f),new PointF(8.9f,9f),new PointF(5.1f,10.9f),new PointF(7.1f,7f)});
      break;
     case IconKind.Download:   // an arrow into a line
      g.DrawLine(pen,8f,2.5f,8f,10.4f);
      g.DrawLines(pen,new[]{new PointF(4.5f,7f),new PointF(8f,10.5f),new PointF(11.5f,7f)});
      g.DrawLine(pen,4.5f,13.5f,11.5f,13.5f);
      break;
    }
   }
   g.SmoothingMode=sm;g.PixelOffsetMode=po;g.Restore(saved);
  }
 }

 // Shared drawing of list rows (settings, links): a small rounded icon tile, a title with an optional muted second line, a hairline divider.
 internal static class RowKit{
  // The tile and the line icon inside it. The icon is drawn by code on a 16-unit grid and centred on the tile's middle, so every tile looks the same
  // at every scale (no font metrics, no dependence on which icon font Windows has).
  public static readonly Color TileFill=Color.FromArgb(34,36,46);
  public static int Icon(Graphics g,IconKind icon,float k,int h,bool enabled){
   if(icon==IconKind.None)return 0;
   float s=28*k,y=(h-s)/2,box=16*k;
   using(var p=Theme.Round(new RectangleF(0,y,s,s),8*k))using(var b=new SolidBrush(TileFill))g.FillPath(b,p);
   Icons.Draw(g,icon,new RectangleF((s-box)/2,y+(s-box)/2,box,box),enabled?Color.FromArgb(170,190,235):Theme.Muted);
   return (int)(s+12*k);
  }
  public static void Texts(Graphics g,string title,string sub,Font font,Rectangle r,Color ink,Color subInk,float k){
   const TextFormatFlags f=TextFormatFlags.SingleLine|TextFormatFlags.NoPadding|TextFormatFlags.EndEllipsis;
   if(string.IsNullOrEmpty(sub)){TextRenderer.DrawText(g,title,font,r,ink,f|TextFormatFlags.VerticalCenter);return;}
   using(var sf=Theme.F(7.5f)){
    int th=TextRenderer.MeasureText(g,"Ag",font,Size.Empty,f).Height,sh=TextRenderer.MeasureText(g,"Ag",sf,Size.Empty,f).Height,gap=(int)(1*k);
    int top=r.Y+(r.Height-th-sh-gap)/2;
    TextRenderer.DrawText(g,title,font,new Rectangle(r.X,top,r.Width,th),ink,f);
    TextRenderer.DrawText(g,sub,sf,new Rectangle(r.X,top+th+gap,r.Width,sh),subInk,f);
   }
  }
  public static void Divider(Graphics g,int left,int width,int height,float k){using(var pen=new Pen(Color.FromArgb(36,36,44)))g.DrawLine(pen,left,height-1,width,height-1);}
 }

 // One muted line of a settings card that holds a file path: the middle of the path gives way when it is too long, the file name stays.
 internal sealed class PathLabel:Surface{
  public bool Divider;
  public PathLabel(){Font=Theme.F(8f);}
  protected override void OnTextChanged(EventArgs e){Invalidate();base.OnTextChanged(e);}
  protected override void OnPaint(PaintEventArgs e){
   var g=e.Graphics;float k=K;int left=(int)(40*k);
   TextRenderer.DrawText(g,Text,Font,new Rectangle(left,0,Math.Max(0,Width-left-(int)(86*k)),Height),Theme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding|TextFormatFlags.PathEllipsis);
   if(Divider)RowKit.Divider(g,left,Width,Height,k);
  }
 }

 // A clickable list row: icon (a glyph, the Telegram mark or a download arrow), title, muted second line, chevron. Used for links and for opening an editor.
 internal sealed class LinkRow:Surface{
  public enum Mark{Glyph,Telegram}
  public Mark Kind=Mark.Glyph;public IconKind Icon;public bool Divider,ShowChevron=true;string sub;readonly Anim hoverA;
  [Browsable(false),DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]public string Subtitle{get=>sub;set{if(sub!=value){sub=value;Invalidate();}}}
  public LinkRow(){Cursor=Cursors.Hand;Font=Theme.F(9.5f,true);Height=48;hoverA=new Anim(this,0f,140);}
  protected override void OnMouseEnter(EventArgs e){hoverA.To(1f);base.OnMouseEnter(e);}
  protected override void OnMouseLeave(EventArgs e){hoverA.To(0f);base.OnMouseLeave(e);}
  protected override void OnTextChanged(EventArgs e){Invalidate();base.OnTextChanged(e);}
  protected override bool IsInputKey(Keys k)=>k==Keys.Space||k==Keys.Enter||base.IsInputKey(k);
  protected override void OnKeyUp(KeyEventArgs e){if(e.KeyCode==Keys.Space||e.KeyCode==Keys.Enter)OnClick(EventArgs.Empty);base.OnKeyUp(e);}
  internal void ClickForTest()=>OnClick(EventArgs.Empty);
  protected override void OnPaint(PaintEventArgs e){
   var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;float k=K;
   if(hoverA.Value>0&&ShowChevron)using(var p=Theme.Round(new RectangleF(-6*k,2*k,Width+12*k-1,Height-4*k),10*k))using(var b=new SolidBrush(Color.FromArgb((int)(18*hoverA.Value),255,255,255)))g.FillPath(b,p);
   int left;
   if(Kind==Mark.Telegram){float s=28*k;Branding.DrawTelegram(g,new RectangleF(0,(Height-s)/2,s,s));left=(int)(s+12*k);}
   else left=RowKit.Icon(g,Icon,k,Height,true);
   RowKit.Texts(g,Text,sub,Font,new Rectangle(left,0,(int)(Width-left-(ShowChevron?26:4)*k),Height),Theme.Text,Theme.Muted,k);
   if(ShowChevron)using(var f=new Font(Theme.IconFont,8f))TextRenderer.DrawText(g,"\uE76C",f,new Rectangle((int)(Width-20*k),0,(int)(20*k),Height),Theme.Mix(Theme.Muted,Theme.Text,hoverA.Value),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);
   if(Divider)RowKit.Divider(g,left,Width,Height,k);
  }
 }

 // Two or three mutually exclusive choices in one pill track (scope of the bridge, language). The highlight slides to the chosen segment.
 // Cumulative (levels): the highlight stretches from the left edge to the chosen segment, so "+ Gemini" lights Antigravity as well.
 // An item that starts with "+ " gets a drawn plus, a little larger than the letters and centred on their middle line.
 internal sealed class Segmented:Surface{
  string[] items=new string[0],tips=new string[0];int sel;readonly Anim pos;int hot=-1;
  readonly ToolTip tip=new ToolTip{InitialDelay=350,ReshowDelay=100,AutoPopDelay=10000};
  public bool Cumulative;
  // Busy: a choice is being applied. The highlight breathes, the pointer shows work, clicks are ignored; the control still looks alive (not greyed out).
  bool busy;
  [Browsable(false),DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]public bool Busy{get=>busy;set{if(busy==value)return;busy=value;Cursor=value?Cursors.AppStarting:Cursors.Hand;if(value&&Motion.Enabled)Motion.Run(BusyStep);Invalidate();}}
  bool BusyStep(){if(IsDisposed||!busy){Invalidate();return false;}Invalidate();return true;}
  internal float BusyPhase=>!busy?0f:!Motion.Enabled?1f:.5f+.5f*(float)Math.Sin(Stopwatch.GetTimestamp()*Math.PI*2/Stopwatch.Frequency/1.2);   // 0..1; a still frame shows the strongest tone
  protected override void Dispose(bool disposing){if(disposing)tip.Dispose();base.Dispose(disposing);}
  public event EventHandler SelectedIndexChanged;   // raised by the person's click only
  public Segmented(){Cursor=Cursors.Hand;Font=Theme.F(8.5f,true);pos=new Anim(this,0f,200);}
  [Browsable(false),DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]public string[] Items{get=>items;set{items=value??new string[0];Invalidate();}}
  [Browsable(false),DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]public string[] Tips{get=>tips;set{tips=value??new string[0];}}
  [Browsable(false),DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]public int SelectedIndex{get=>sel;set{if(value<0||value>=items.Length)return;sel=value;pos.To(value);}}
  int At(int x)=>items.Length==0?-1:Math.Max(0,Math.Min(items.Length-1,x*items.Length/Math.Max(1,Width)));
  protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);int i=At(e.X);if(i!=hot){hot=i;tip.SetToolTip(this,i>=0&&i<tips.Length?tips[i]:null);Invalidate();}}
  protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hot=-1;tip.SetToolTip(this,null);Invalidate();}
  protected override void OnEnabledChanged(EventArgs e){Invalidate();base.OnEnabledChanged(e);}
  protected override void OnMouseClick(MouseEventArgs e){base.OnMouseClick(e);if(!Enabled||busy)return;int i=At(e.X);if(i>=0&&i!=sel){SelectedIndex=i;SelectedIndexChanged?.Invoke(this,EventArgs.Empty);}}
  internal void ClickForTest(int i){if(busy)return;if(i!=sel&&i>=0&&i<items.Length){SelectedIndex=i;SelectedIndexChanged?.Invoke(this,EventArgs.Empty);}}
  // The rectangle of the highlight in client pixels (the settled one when the animation is over).
  internal RectangleF PillRect(float at){float k=K,w=(Width-6*k)/Math.Max(1,items.Length);return Cumulative?new RectangleF(3*k,3*k,(at+1f)*w,Height-6*k-1):new RectangleF(3*k+at*w,3*k,w,Height-6*k-1);}
  internal int PaintFailures;   // a paint that failed is counted (the self-test requires 0) instead of turning the control into the framework's red cross
  protected override void OnPaint(PaintEventArgs e){try{PaintCore(e.Graphics);}catch(Exception ex){PaintFailures++;UiDiag.Paint("Segmented",ex);}}
  void PaintCore(Graphics g){
   g.SmoothingMode=SmoothingMode.AntiAlias;float k=K;if(items.Length==0)return;
   using(var p=Theme.Round(new RectangleF(0,0,Width-1,Height-1),(Height-1)/2f))using(var b=new SolidBrush(Theme.Field))using(var pen=new Pen(Theme.Line)){g.FillPath(b,p);g.DrawPath(pen,p);}
   float w=(Width-6*k)/items.Length;
   var pill=PillRect(pos.Value);
   float bp=BusyPhase;
   using(var p=Theme.Round(pill,pill.Height/2))using(var b=new SolidBrush(Enabled?Theme.Mix(Theme.NavSel,Theme.Accent,.30f*bp):Theme.Soft))using(var pen=new Pen(Enabled?Theme.Mix(Color.FromArgb(60,90,160),Theme.AccentHover,.8f*bp):Theme.Line)){g.FillPath(b,p);g.DrawPath(pen,p);}
   for(int i=0;i<items.Length;i++){
    float near=Cumulative?Math.Max(0f,Math.Min(1f,pos.Value+1f-i)):Math.Max(0f,1f-Math.Abs(i-pos.Value));
    Color c=!Enabled?Theme.Muted:Theme.Mix(i==hot?Theme.Text:Theme.Muted,Color.White,near);
    DrawItem(g,items[i],new Rectangle((int)(3*k+i*w),0,(int)w,Height),c,k);
   }
  }
  // Where the baseline of a single line drawn vertically centred in a box of this height falls. The font's own metrics give it (GDI lays the line out as
  // ascent + descent, the box centres that height): no device context is taken from the graphics, so nothing in the paint can be left "in use elsewhere".
  (float size,int box,float dpi,float value) baseCache;
  float Baseline(Graphics g,Font font,int boxHeight) {
   if(baseCache.size==font.SizeInPoints&&baseCache.box==boxHeight&&baseCache.dpi==g.DpiY)return baseCache.value;
   var ff=font.FontFamily;float em=font.SizeInPoints*g.DpiY/72f,ue=ff.GetEmHeight(font.Style);
   float asc=(float)Math.Round(ff.GetCellAscent(font.Style)*em/ue),desc=(float)Math.Round(ff.GetCellDescent(font.Style)*em/ue);
   float v=(boxHeight-(asc+desc))/2f+asc;baseCache=(font.SizeInPoints,boxHeight,g.DpiY,v);return v;
  }
  void DrawItem(Graphics g,string text,Rectangle r,Color c,float k){
   const TextFormatFlags f=TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding|TextFormatFlags.EndEllipsis;
   if(!text.StartsWith("+ ")){TextRenderer.DrawText(g,text,Font,r,c,f|TextFormatFlags.HorizontalCenter);return;}
   string rest=text.Substring(2);
   float em=Font.SizeInPoints*g.DpiY/72f;
   int tw=TextRenderer.MeasureText(g,rest,Font,Size.Empty,f&~TextFormatFlags.EndEllipsis).Width;
   // the plus: arms .38 em, stroke a little heavier than the letters; centred on the middle line of the lower-case letters (baseline minus ~.25 em, measured against the drawn letters)
   float arm=.38f*em,gap=.5f*em,total=arm*2+gap+tw,x0=r.X+Math.Max(0f,(r.Width-total)/2f);
   float baseline=r.Y+Baseline(g,Font,r.Height);
   int sw=Math.Max(2,(int)Math.Round(.17f*em));float half=sw%2==0?0f:.5f;
   float cx=(float)Math.Round(x0+arm)+half,cy=(float)Math.Round(baseline-.25f*em)+half,ra=(float)Math.Round(arm);
   var old=g.SmoothingMode;g.SmoothingMode=SmoothingMode.None;
   try{using(var pen=new Pen(c,sw)){g.DrawLine(pen,cx-ra,cy,cx+ra,cy);g.DrawLine(pen,cx,cy-ra,cx,cy+ra);}}catch(Exception){}
   finally{g.SmoothingMode=old;}
   int tx=(int)Math.Round(x0+arm*2+gap);
   TextRenderer.DrawText(g,rest,Font,new Rectangle(tx,r.Y,Math.Max(0,r.Right-tx),r.Height),c,f);
  }
 }

 // The drop-down of the profile: the chosen name, and at its right a short detail with a coloured dot (the ping); every row of the list carries its own detail
 // («—» while it is not known). The detail comes from Detail(i) and can change while the list is open (RefreshDetails).
 internal sealed class PillSelect:Surface{
  public readonly List<string> Items=new List<string>();int sel;bool hover;ContextMenuStrip menu;
  public event EventHandler SelectedIndexChanged;
  public event EventHandler DropDownOpening;   // the list is about to open (the owner may start measuring)
  public Func<int,(string text,Color dot)> Detail;
  readonly ToolTip tip=new ToolTip{InitialDelay=400,ReshowDelay=100,AutoPopDelay=10000};string tipText;
  [Browsable(false),DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]public string Tip{get=>tipText;set{if(tipText==value)return;tipText=value;tip.SetToolTip(this,value);}}
  protected override void Dispose(bool disposing){if(disposing)tip.Dispose();base.Dispose(disposing);}
  [Browsable(false),DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]public int SelectedIndex{get=>sel;set{if(value<0||value>=Items.Count||value==sel)return;sel=value;Invalidate();SelectedIndexChanged?.Invoke(this,EventArgs.Empty);}}
  public void SetSelectedSilently(int i){if(i>=0&&i<Items.Count){sel=i;Invalidate();}}
  internal void OpenForTest()=>OnClick(EventArgs.Empty);
  internal ContextMenuStrip OpenMenu=>menu;
  public void RefreshDetails(){Invalidate();if(menu!=null&&!menu.IsDisposed)menu.Invalidate();}
  public PillSelect(){Cursor=Cursors.Hand;Font=Theme.F(10);}
  protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
  protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
  protected override void OnEnabledChanged(EventArgs e){Invalidate();base.OnEnabledChanged(e);}
  internal const TextFormatFlags Flags=TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding;
  protected override void OnClick(EventArgs e){
   base.OnClick(e);if(!Enabled||Items.Count==0)return;
   DropDownOpening?.Invoke(this,EventArgs.Empty);
   float k=K;
   var m=menu=new ContextMenuStrip{Renderer=new DetailRenderer(new DarkColors(),DetailOf){RoundedEdges=false},ShowImageMargin=false,BackColor=Theme.Card,ForeColor=Theme.Text,Font=Font,MinimumSize=new Size(Width,0)};
   for(int i=0;i<Items.Count;i++){int idx=i;var it=new ToolStripMenuItem(Items[i]){ForeColor=Theme.Text,Checked=i==sel,Padding=new Padding(4,6,(int)(Math.Round(DetailW*k))+(int)(10*k),6),Tag=idx};it.Click+=(s,a)=>SelectedIndex=idx;m.Items.Add(it);}
   m.Closed+=(s,a)=>{if(menu==m)menu=null;try{if(IsHandleCreated&&!IsDisposed)BeginInvoke(new Action(m.Dispose));else m.Dispose();}catch{}};   // one menu per opening: released once it has closed and its click is handled
   m.Show(this,new Point(0,Height+4));
  }
  (string text,Color dot) DetailOf(int i)=>Detail!=null?Detail(i):("",Color.Empty);
  internal const float DetailW=82;   // the column of the list that holds a detail (units)
  static string Shown(string text)=>string.IsNullOrEmpty(text)?"—":text;
  // A detail at the right of a row: the dot in a fixed column, the words right-aligned to the edge.
  internal static void PaintDetail(Graphics g,Font font,(string text,Color dot) d,Rectangle col,float k,bool alwaysShow){
   if(string.IsNullOrEmpty(d.text)&&!alwaysShow)return;
   string text=Shown(d.text);Color ink=string.IsNullOrEmpty(d.text)?Theme.Muted:Theme.Text;
   float dot=7*k;
   if(d.dot!=Color.Empty&&!string.IsNullOrEmpty(d.text)){var old=g.SmoothingMode;g.SmoothingMode=SmoothingMode.AntiAlias;using(var b=new SolidBrush(d.dot))g.FillEllipse(b,col.X,col.Y+(col.Height-dot)/2f,dot,dot);g.SmoothingMode=old;}
   TextRenderer.DrawText(g,text,font,new Rectangle(col.X+(int)(dot+4*k),col.Y,Math.Max(0,col.Width-(int)(dot+4*k)),col.Height),ink,Flags|TextFormatFlags.Right);
  }
  sealed class DetailRenderer:ToolStripProfessionalRenderer{
   readonly Func<int,(string text,Color dot)> detail;
   public DetailRenderer(ProfessionalColorTable t,Func<int,(string text,Color dot)> detail):base(t){this.detail=detail;}
   protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e){
    base.OnRenderItemText(e);
    if(!(e.Item is ToolStripMenuItem mi)||!(mi.Tag is int idx))return;
    float k=e.Item.Owner.DeviceDpi/96f;int colW=(int)Math.Round(DetailW*k);   // the item's own width: its padding on the right is the room kept for this column
    PaintDetail(e.Graphics,e.TextFont,detail(idx),new Rectangle(e.Item.Width-colW-(int)(8*k),0,colW,e.Item.Height),k,true);
   }
  }
  protected override void OnPaint(PaintEventArgs e){
   var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;float k=K;
   using(var p=Theme.Round(new RectangleF(0,0,Width-1,Height-1),12*k))using(var b=new SolidBrush(hover&&Enabled?Color.FromArgb(22,22,28):Theme.Field))using(var pen=new Pen(Theme.Line)){g.FillPath(b,p);g.DrawPath(pen,p);}
   string t=sel<Items.Count?Items[sel]:"";
   (string text,Color dot) d=Detail!=null&&sel<Items.Count?Detail(sel):("",Color.Empty);
   int chevron=(int)(36*k),pad=(int)(14*k);int detailW=0;
   if(!string.IsNullOrEmpty(d.text)){
    using(var df=Theme.F(9f)){
     int tw=TextRenderer.MeasureText(g,d.text,df,Size.Empty,Flags).Width;detailW=(int)(7*k+4*k)+tw;
     PaintDetail(g,df,d,new Rectangle(Width-chevron-detailW,0,detailW,Height),k,false);
    }
   }
   TextRenderer.DrawText(g,t,Font,new Rectangle(pad,0,Math.Max(0,Width-pad-chevron-(detailW>0?detailW+(int)(10*k):0)),Height),Enabled?Theme.Text:Theme.Muted,Flags|TextFormatFlags.EndEllipsis);
   float cx=Width-20*k,cy=Height/2f;using(var pen=new Pen(Theme.Muted,1.8f*k){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round})g.DrawLines(pen,new[]{new PointF(cx-4*k,cy-2*k),new PointF(cx,cy+2*k),new PointF(cx+4*k,cy-2*k)});
  }
 }

 internal sealed class BenchRow {
  public string Id,Name,Slot;public ProfileResult Result;public bool Running;public string Note,CatalogNote;
 }

 // Benchmark results: rank, profile + verdict, availability, median/p95 DoH latency, TLS time. Colour-coded; the best row is outlined.
 internal sealed class BenchTable:Surface{
  public readonly List<BenchRow> Rows=new List<BenchRow>();
  public float Progress=-1;
  readonly ToolTip tip=new ToolTip{InitialDelay=250,ReshowDelay=100,AutoPopDelay=12000};
  int tipRow=-2;
  public BenchTable(){Font=Theme.F(8.5f);}
  static readonly float[] ColX={8,30,166,214,262,308};   // rank, name, availability, median, p95, TLS (dp)
  float HeaderH=>26*K;
  float RowH=>Rows.Count==0?36*K:Math.Min(60*K,(Height-HeaderH-6*K)/Rows.Count);   // the table fills its page; rows grow up to 60 dp
  public static string VerdictText(string v)=>L.T(v=="Good"?"Работает":v=="Slow"?"Медленно":v=="Flaky"?"Нестабильно":v=="Partial"?"Частично":v=="Dead"?"Недоступен":v=="NotMeasured"?"Не измерен":"Измеряю…");
  public static Color VerdictColor(string v)=>v=="Good"?Theme.Green:v=="Slow"||v=="Flaky"||v=="Partial"?Theme.Amber:v=="Dead"?Theme.Red:v=="NotMeasured"?Theme.Muted:Theme.Accent;
  static Color AvailColor(double a)=>a>=0.9?Theme.Green:a>=0.6?Theme.Amber:Theme.Red;
  static Color LatColor(long ms)=>ms<0?Theme.Muted:ms<300?Theme.Green:ms<800?Theme.Amber:Theme.Red;
  static string Ms(long ms)=>ms<0?"—":ms.ToString();
  public string Describe(BenchRow r) {
   var x=r.Result;if(x==null)return r.Name+(r.Note==null?"":"\n"+r.Note);
   var s=new System.Text.StringBuilder();
   s.Append(r.Name+"  ·  "+VerdictText(x.Verdict));
   s.Append("\n"+L.T("Доступность: ")+(int)Math.Round(x.Availability*100)+"% ("+x.Successes+L.T(" из ")+x.Attempts+L.T(" раундов)"));
   s.Append("\n"+L.T("Задержка DoH: медиана ")+Ms(x.MedianMs)+L.T(" мс")+", p95 "+Ms(x.P95Ms)+L.T(" мс")+"  ·  TLS "+Ms(x.TlsMs)+L.T(" мс"));
   s.Append("\n"+L.T("Стабильность ответов: ")+(int)Math.Round(x.Stability*100)+"%  ·  "+L.T("достижимость адресов: ")+(int)Math.Round(x.Reach*100)+"%");
   foreach(var d in x.Domains)s.Append("\n"+d.Host.Split('.')[0]+": "+(d.Resolved&&d.TlsOk?L.T("резолв + TLS ок (")+d.Reachable+"/"+d.Answers+")":d.Resolved?L.T("резолв ок, TLS не прошёл"):L.T("нет ответа")));
   if(!string.IsNullOrEmpty(x.Detail))s.Append("\n"+x.Detail);
   if(r.Note!=null)s.Append("\n"+r.Note);
   return s.ToString();   // long provider notes live in the details panel (click a row), not in this tooltip

  }
  public event Action<BenchRow> RowClicked;
  protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);if(tipRow!=-2){tipRow=-2;tip.SetToolTip(this,null);Invalidate();}}
  protected override void OnMouseClick(MouseEventArgs e){
   base.OnMouseClick(e);
   int i=e.Y<HeaderH?-1:(int)((e.Y-HeaderH)/RowH);
   if(i>=0&&i<Rows.Count)RowClicked?.Invoke(Rows[i]);
  }
  protected override void OnMouseMove(MouseEventArgs e){
   base.OnMouseMove(e);
   int i=e.Y<HeaderH?-1:(int)((e.Y-HeaderH)/RowH);if(i>=Rows.Count)i=-1;
   if(i!=tipRow){tipRow=i;tip.SetToolTip(this,i>=0?Describe(Rows[i]):null);Cursor=i>=0?Cursors.Hand:Cursors.Default;Invalidate();}
  }
  protected override void OnPaint(PaintEventArgs e){
   var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;float k=K;
   using(var p=Theme.Round(new RectangleF(0,0,Width-1,Height-1),16*k))using(var b=new SolidBrush(Theme.Card))g.FillPath(b,p);
   var flags=TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding|TextFormatFlags.EndEllipsis;
   using(var hf=Theme.F(7f,true)) {
    string[] heads={"#",L.T("ПРОФИЛЬ"),L.T("ДОСТ."),L.T("МЕД. МС"),L.T("P95 МС"),L.T("TLS МС")};
    for(int c=0;c<heads.Length;c++){
     float x=ColX[c]*k,w=(c==1?ColX[2]-ColX[1]:c==0?ColX[1]-ColX[0]:(c<5?ColX[c+1]-ColX[c]:Width/k-ColX[c]-6))*k;
     TextRenderer.DrawText(g,heads[c],hf,new Rectangle((int)x,0,(int)w,(int)HeaderH),Theme.Muted,flags|(c>=2?TextFormatFlags.Left:TextFormatFlags.Left));
    }
   }
   float rh=RowH;
   using(var nf=Theme.F(9f,true))using(var sf=Theme.F(7.5f))using(var mf=Theme.F(8.5f,true))
   for(int i=0;i<Rows.Count;i++) {
    var row=Rows[i];var r=row.Result;bool best=r!=null&&r.Best&&r.Final;
    var rect=new RectangleF(5*k,HeaderH+i*rh,Width-10*k,rh-3*k);
    using(var path=Theme.Round(rect,10*k)) {
     using(var b=new SolidBrush(Theme.Mix(best?Theme.Mix(Theme.Field,Theme.Green,.14f):row.Running?Theme.Mix(Theme.Field,Theme.Accent,.10f):Theme.Field,Color.White,i==tipRow?.05f:0f)))g.FillPath(b,path);
     if(best)using(var pen=new Pen(Theme.Green,1.4f*k))g.DrawPath(pen,path);
    }
    int ry=(int)rect.Y,rhh=(int)rect.Height;
    // rank
    string rank=r!=null&&r.Final&&r.Rank>0?(best?"★":r.Rank.ToString()):"";
    TextRenderer.DrawText(g,rank,mf,new Rectangle((int)(ColX[0]*k),ry,(int)((ColX[1]-ColX[0])*k),rhh),best?Theme.Green:Theme.Muted,flags);
    // name + verdict
    int nameW=(int)((ColX[2]-ColX[1]-4)*k);
    TextRenderer.DrawText(g,row.Name,nf,new Rectangle((int)(ColX[1]*k),ry+(int)(3*k),nameW,(int)(rhh*.5f)),Theme.Text,flags);
    string v=r==null?(row.Running?"Pending":"None"):r.Verdict;
    string status=r==null?(row.Running?L.T("Измеряю…"):(row.Note??"—")):(v=="Pending"?L.T("Измеряю…")+" "+r.Attempts+"/…":VerdictText(v)+(r.Final&&r.Domains.Length>0?"  ·  "+L.T("домены")+" "+r.Domains.Count(d=>d.Resolved&&d.TlsOk)+"/"+r.Domains.Length:""));
    TextRenderer.DrawText(g,status,sf,new Rectangle((int)(ColX[1]*k),ry+(int)(rhh*.52f),nameW,(int)(rhh*.42f)),r==null?(row.Running?Theme.Accent:Theme.Muted):VerdictColor(v),flags);
    // metrics
    if(r!=null&&r.Attempts>0) {
     string[] cells={r.Attempts==0?"—":(int)Math.Round(r.Availability*100)+"%",Ms(r.MedianMs),Ms(r.P95Ms),Ms(r.TlsMs)};
     Color[] cols={AvailColor(r.Availability),LatColor(r.MedianMs),LatColor(r.P95Ms),LatColor(r.TlsMs<0?-1:r.TlsMs/2)};
     for(int c=0;c<4;c++){float x=ColX[2+c]*k;float w=(c<3?ColX[3+c]-ColX[2+c]:Width/k-ColX[5]-8)*k;TextRenderer.DrawText(g,cells[c],mf,new Rectangle((int)x,ry,(int)w,rhh),cols[c],flags);}
    } else {
     for(int c=0;c<4;c++){float x=ColX[2+c]*k;TextRenderer.DrawText(g,"—",mf,new Rectangle((int)x,ry,(int)(40*k),rhh),Theme.Muted,flags);}
    }
   }
   if(Progress>=0)using(var b=new SolidBrush(Theme.Accent))g.FillRectangle(b,12*k,Height-5*k,(Width-24*k)*Math.Min(1f,Progress),2.5f*k);
  }
 }

 internal sealed class NavBar:Surface{
  // Segoe MDL2 Assets: power, stopwatch, settings, command prompt, info.
  public static readonly string[] Glyphs={"\uE7E8","\uE916","\uE713","\uE756","\uE946"};
  public static readonly string[] Labels={"Мост","Замер","Настройки","Логи","Инфо"};
  public static int Count=>Labels.Length;
  int sel;public event EventHandler SelectedChanged;
  [Browsable(false),DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]public int Selected{get=>sel;set{if(sel!=value){sel=value;selA.To(value);SelectedChanged?.Invoke(this,EventArgs.Empty);}}}
  readonly Anim selA;public NavBar(){Cursor=Cursors.Hand;selA=new Anim(this,0f,200);}
  protected override void OnMouseClick(MouseEventArgs e){int i=Math.Max(0,Math.Min(Count-1,e.X*Count/Math.Max(1,Width)));Selected=i;base.OnMouseClick(e);}
  protected override void OnPaint(PaintEventArgs e)=>Paint(e.Graphics,Width,Height,K,selA.Value);

  // Geometry of the highlight (also read by the self-test at any scale k): the same size for every tab, whole pixels, centred on the tab's own cell
  // (the same gap on the left and on the right), and the same gap above and below. Between two tabs it slides, so the position may be fractional.
  internal const float InsetX=6,InsetY=6;
  internal static Rectangle Pill(float pos,int width,int height,float k,int count){
   float cell=width/(float)count;int pw=(int)Math.Round(cell-2*InsetX*k),iy=(int)Math.Round(InsetY*k);
   int x=(int)Math.Round(pos*cell+(cell-pw)/2f);
   return new Rectangle(x,iy,pw,height-2*iy);
  }
  internal static void Paint(Graphics g,int width,int height,float k,float pos){
   g.SmoothingMode=SmoothingMode.AntiAlias;g.PixelOffsetMode=PixelOffsetMode.Half;
   float outer=(height-1)/2.4f;
   using(var p=Theme.Round(new RectangleF(0,0,width-1,height-1),outer))using(var b=new SolidBrush(Theme.Card))g.FillPath(b,p);
   int count=Count;float cell=width/(float)count;
   var pill=Pill(pos,width,height,k,count);
   // concentric with the bar: the bar's radius less the gap
   using(var p=Theme.Round(pill,Math.Max(2f,outer-InsetY*k)))using(var b=new SolidBrush(Theme.NavSel))g.FillPath(b,p);
   using(var gf=new Font(Theme.IconFont,13f))using(var lf=Theme.F(7.5f,true)){
    const TextFormatFlags f=TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding|TextFormatFlags.SingleLine|TextFormatFlags.NoClipping;
    int gh=(int)Math.Ceiling(gf.GetHeight(g)),lh=(int)Math.Ceiling(lf.GetHeight(g)),gap=(int)Math.Round(1*k);
    int top=pill.Y+(pill.Height-(gh+gap+lh))/2;
    for(int i=0;i<count;i++){
     var cellPill=Pill(i,width,height,k,count);   // this tab's own highlight rectangle: the icon and the word are centred on it
     Color c=Theme.Mix(Theme.Muted,Theme.AccentHover,Math.Max(0f,1f-Math.Abs(i-pos)));
     TextRenderer.DrawText(g,Glyphs[i],gf,new Rectangle(cellPill.X,top,cellPill.Width,gh),c,f);
     TextRenderer.DrawText(g,L.T(Labels[i]),lf,new Rectangle(cellPill.X-(int)(4*k),top+gh+gap,cellPill.Width+(int)(8*k),lh),c,f);
    }
   }
  }
 }
}
