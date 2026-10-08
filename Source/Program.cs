using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;
using Microsoft.VisualBasic.FileIO;

namespace Snapline {
 public class Settings {
  public List<string> Folders = new List<string>();
  public List<string> Items = new List<string>();
  public bool WatchClipboard;
  public static Settings Load(string file) {
   try { using(var r=File.OpenRead(file)) return (Settings)new XmlSerializer(typeof(Settings)).Deserialize(r); }
   catch { return new Settings(); }
  }
  public void Save(string file) {
   Directory.CreateDirectory(Path.GetDirectoryName(file));
   var temp=file+".tmp";
   using(var w=File.Create(temp)) new XmlSerializer(typeof(Settings)).Serialize(w,this);
   File.Copy(temp,file,true); File.Delete(temp);
  }
 }
 public static class Helpers {
  public static bool IsImage(string p) { return new[]{".png",".jpg",".jpeg",".bmp",".gif",".tif",".tiff"}.Contains(Path.GetExtension(p).ToLowerInvariant()); }
  public static string Unique(string folder,string name) {
   var dest=Path.Combine(folder,name); int n=2;
   while(File.Exists(dest)) dest=Path.Combine(folder,Path.GetFileNameWithoutExtension(name)+" ("+(n++)+")"+Path.GetExtension(name));
   return dest;
  }
  public static Image LoadImage(string p) { using(var s=File.Open(p,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)) using(var im=Image.FromStream(s)) return new Bitmap(im); }
 }
 internal static class Native {
  [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr h,int id,uint mods,uint key);
  [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr h,int id);
  [DllImport("user32.dll")] internal static extern uint GetClipboardSequenceNumber();
  [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr h,out RECT rect);
  [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
  [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h,IntPtr dc);
  [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
  [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
  [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
  [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
  [DllImport("user32.dll",SetLastError=true)] static extern bool UpdateLayeredWindow(IntPtr h,IntPtr dst,ref POINT pos,ref SIZE size,IntPtr src,ref POINT from,int key,ref BLEND blend,int flags);
  [StructLayout(LayoutKind.Sequential)] struct POINT {public int X,Y;public POINT(int x,int y){X=x;Y=y;}}
  [StructLayout(LayoutKind.Sequential)] struct SIZE {public int W,H;public SIZE(int w,int h){W=w;H=h;}}
  [StructLayout(LayoutKind.Sequential,Pack=1)] struct BLEND {public byte Op,Flags,Alpha,Format;}
  internal static void Layer(IntPtr handle,Bitmap bitmap,int x,int y){IntPtr dc=GetDC(IntPtr.Zero),mem=CreateCompatibleDC(dc),hb=IntPtr.Zero,old=IntPtr.Zero;try{hb=bitmap.GetHbitmap(Color.FromArgb(0));old=SelectObject(mem,hb);var pos=new POINT(x,y);var size=new SIZE(bitmap.Width,bitmap.Height);var origin=new POINT(0,0);var blend=new BLEND{Alpha=255,Format=1};if(!UpdateLayeredWindow(handle,dc,ref pos,ref size,mem,ref origin,0,ref blend,2))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());}finally{if(old!=IntPtr.Zero)SelectObject(mem,old);if(hb!=IntPtr.Zero)DeleteObject(hb);DeleteDC(mem);ReleaseDC(IntPtr.Zero,dc);}}

  [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr h);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern int GetClassName(IntPtr h,System.Text.StringBuilder text,int count);
  [StructLayout(LayoutKind.Sequential)] internal struct RECT { internal int Left,Top,Right,Bottom; }
 }
 internal sealed class ImageCard:Control {
  internal readonly string PathName; readonly Shelf shelf; internal Image Preview;
  internal DateTime Born=DateTime.Now,Copied=DateTime.MinValue,Nudged=DateTime.Now;
  internal RectangleF Hit {get;set;} internal float Tilt,CenterX=float.NaN; internal bool Hover,Pressed,Dragged,Held,Falling; internal DateTime FallStart;
  Point down; System.Windows.Forms.Timer hold=new System.Windows.Forms.Timer();
  internal ImageCard(Shelf owner,string p) {
   shelf=owner;PathName=p;Tilt=(float)(new Random(p.GetHashCode()).NextDouble()*5-2.5);
   using(var original=Helpers.LoadImage(p)){float scale=Math.Min(1f,600f/Math.Max(original.Width,original.Height));Preview=new Bitmap(original,Math.Max(1,(int)(original.Width*scale)),Math.Max(1,(int)(original.Height*scale)));}
   hold.Interval=450;hold.Tick+=(s,e)=>{hold.Stop();if(Pressed&&!Dragged){Held=true;Pressed=false;shelf.EditImage(p);}};
   var menu=new ContextMenuStrip();
   menu.Items.Add("Copy image",null,(s,e)=>shelf.CopyImage(p));menu.Items.Add("Open",null,(s,e)=>shelf.OpenImage(p));
   menu.Items.Add("Markup in Snipping Tool",null,(s,e)=>shelf.EditImage(p));menu.Items.Add("Snipping Tool: open manually...",null,(s,e)=>shelf.OpenMarkupManually(p,"Use this if your Snipping Tool build does not load the image automatically."));menu.Items.Add("Save a copy...",null,(s,e)=>shelf.SaveCopy(p));
   menu.Items.Add("Show in folder",null,(s,e)=>shelf.RevealFile(p));menu.Items.Add("Take down (keep file)",null,(s,e)=>shelf.Remove(p));
   menu.Items.Add("Move to Recycle Bin...",null,(s,e)=>shelf.Recycle(p));ContextMenuStrip=menu;
  }
  internal void Down(Point p,int clicks){if(clicks>=2){hold.Stop();Pressed=false;Held=true;shelf.OpenImage(PathName);return;}down=p;Pressed=true;Dragged=false;Held=false;hold.Start();}
  internal void PointerMove(Point p){if(!Pressed||Dragged||Held)return;if(Math.Abs(p.X-down.X)+Math.Abs(p.Y-down.Y)>5){Dragged=true;hold.Stop();Pressed=false;shelf.Dragging=true;try{var data=new DataObject(DataFormats.FileDrop,new[]{PathName});shelf.DoDragDrop(data,DragDropEffects.Copy|DragDropEffects.Move);}finally{shelf.Dragging=false;Dragged=false;shelf.Prune();}}}
  internal void Up(int clicks){hold.Stop();if(Pressed&&!Dragged&&!Held&&clicks==1)shelf.CopyImage(PathName);Pressed=false;Held=false;}
  protected override void Dispose(bool disposing){if(disposing){hold.Dispose();Preview.Dispose();ContextMenuStrip.Dispose();}base.Dispose(disposing);}
 }
 internal static class GlassArt {
  internal static GraphicsPath Rounded(RectangleF r,float radius){var p=new GraphicsPath();float d=radius*2;d=Math.Min(d,Math.Min(r.Width,r.Height));p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
  internal static float RopeY(float x,float width){float f=x/width;return 10+4*Math.Min(30,width*.018f)*f*(1-f);}
  internal static void Pill(Graphics g,string text,RectangleF r){using(var path=Rounded(r,r.Height/2))using(var b=new SolidBrush(Color.FromArgb(205,242,244,246)))using(var pen=new Pen(Color.FromArgb(190,255,255,255),.7f)){g.FillPath(b,path);g.DrawPath(pen,path);}using(var f=new Font("Segoe UI",10,FontStyle.Regular))using(var b=new SolidBrush(Color.FromArgb(225,50,53,58)))using(var format=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})g.DrawString(text,f,b,r,format);}
  internal static void Card(Graphics g,ImageCard c,float x,float rope,float offset,double now){
   float scale=Math.Min(136f/c.Preview.Width,104f/c.Preview.Height);float w=c.Preview.Width*scale,h=c.Preview.Height*scale;
   float age=(float)(DateTime.Now-c.Born).TotalSeconds;
   float arrival=-46*(float)Math.Exp(-9*age);float angle=c.Tilt+16*(float)(Math.Exp(-4*age)*Math.Cos(10*age));
   float n=(float)(DateTime.Now-c.Nudged).TotalSeconds;if(n<3)angle+=3*(float)(Math.Exp(-2.6*n)*Math.Sin(9*n));
   float y=rope-9.5f+offset+arrival;float alpha=1;float shrink=c.Pressed?.95f:(c.Hover?1.035f:1);
   if(c.Falling){float t=Math.Min(1,(float)(DateTime.Now-c.FallStart).TotalSeconds/.55f);float e=t*t*t;y+=520*e;angle+=20*e;alpha=1-e;}
   c.Hit=new RectangleF(x-(w+8)/2,y+14,w+8,h+8);
   var state=g.Save();g.TranslateTransform(x,y);g.RotateTransform(angle);g.ScaleTransform(shrink,shrink);
   RectangleF frame=new RectangleF(-(w+8)/2,14,w+8,h+8);
   for(int i=9;i>0;i--){using(var shadow=Rounded(new RectangleF(frame.X-i/2f,frame.Y+5-i/2f,frame.Width+i,frame.Height+i),16+i/3f))using(var b=new SolidBrush(Color.FromArgb((int)(alpha*(c.Hover?3:2)),0,0,0)))g.FillPath(b,shadow);}
   using(var path=Rounded(frame,16))using(var b=new LinearGradientBrush(frame,Color.FromArgb((int)(alpha*225),255,255,255),Color.FromArgb((int)(alpha*145),224,230,237),LinearGradientMode.Vertical))using(var pen=new Pen(Color.FromArgb((int)(alpha*220),255,255,255),.8f)){g.FillPath(b,path);g.DrawPath(pen,path);}
   var photo=new RectangleF(-w/2,18,w,h);using(var clip=Rounded(photo,12)){g.SetClip(clip);using(var attributes=new ImageAttributes()){var matrix=new ColorMatrix();matrix.Matrix33=alpha*(c.Dragged?.45f:1);attributes.SetColorMatrix(matrix);g.DrawImage(c.Preview,Rectangle.Round(photo),0,0,c.Preview.Width,c.Preview.Height,GraphicsUnit.Pixel,attributes);}g.ResetClip();}
   using(var path=Rounded(new RectangleF(-4.5f,0,9,26),3.5f))using(var metal=new LinearGradientBrush(new RectangleF(-4.5f,0,9,26),Color.FromArgb((int)(255*alpha),177,177,182),Color.FromArgb((int)(255*alpha),158,158,165),LinearGradientMode.Horizontal)){metal.InterpolationColors=new ColorBlend{Positions=new[]{0f,.35f,.65f,1f},Colors=new[]{Color.FromArgb((int)(255*alpha),177,177,182),Color.FromArgb((int)(255*alpha),239,240,242),Color.FromArgb((int)(255*alpha),208,209,215),Color.FromArgb((int)(255*alpha),158,158,165)}};g.FillPath(metal,path);using(var pen=new Pen(Color.FromArgb((int)(alpha*175),255,255,255),.6f))g.DrawPath(pen,path);}
   using(var pen=new Pen(Color.FromArgb((int)(alpha*90),20,20,20),1.4f))g.DrawLine(pen,-2.5f,9,2.5f,9);
   if(c.Hover&&!c.Dragged&&!c.Falling){var cross=new RectangleF(frame.X+3,frame.Y+3,20,20);using(var b=new SolidBrush(Color.FromArgb(225,247,248,250)))g.FillEllipse(b,cross);using(var pen=new Pen(Color.FromArgb(210,63,65,70),1.1f)){g.DrawLine(pen,cross.X+7,cross.Y+7,cross.X+13,cross.Y+13);g.DrawLine(pen,cross.X+13,cross.Y+7,cross.X+7,cross.Y+13);}}
   if((DateTime.Now-c.Copied).TotalSeconds<1.2)Pill(g,"Copied",new RectangleF(-40,frame.Bottom+4,80,26));g.Restore(state);
  }
 }
 internal sealed class Shelf:Form {
  internal static readonly Color Background=Color.FromArgb(21,37,52);
  internal bool Dragging; readonly bool qa; readonly string dataDir,inbox,settingsFile;
  Settings settings; NotifyIcon tray; FlowLayoutPanel cards; Label status; Button pinButton;
  System.Windows.Forms.Timer tick=new System.Windows.Forms.Timer();
  readonly Dictionary<string,string> observed=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  readonly Dictionary<string,string> pending=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  readonly Dictionary<string,int> failures=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
  Image clipboardImage;uint sequence;bool pinned;DateTime edgeSince=DateTime.MinValue,lastInside=DateTime.Now,waitSnip=DateTime.MinValue;int counter;string message;
  internal Shelf(bool test=false,string dir=null) {
   qa=test;dataDir=dir??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Snapline");
   inbox=Path.Combine(dataDir,"Inbox");settingsFile=Path.Combine(dataDir,"settings.xml");Directory.CreateDirectory(inbox);
   settings=Settings.Load(settingsFile); if(settings.Folders.Count==0){settings.Folders.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),"Screenshots"));settings.Folders.Add(inbox);}
   if(!settings.Folders.Contains(inbox))settings.Folders.Add(inbox);
   Text="Snapline";Font=new Font("Segoe UI",9);BackColor=Background;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;AutoScaleMode=AutoScaleMode.Dpi;
   using(var s=GetType().Assembly.GetManifestResourceStream("Snapline.snapline.ico")){if(s!=null)Icon=new Icon(s);}
   cards=new FlowLayoutPanel();status=new Label();pinButton=new Button();DoubleBuffered=true;
   AllowDrop=true;DragEnter+=(s,e)=>{if(e.Data.GetDataPresent(DataFormats.FileDrop))e.Effect=DragDropEffects.Copy;};DragDrop+=(s,e)=>{foreach(var p in (string[])e.Data.GetData(DataFormats.FileDrop))Add(p);};
   BuildTray();RefreshCards();Prune();RefreshStatus();
   Scan(true);tick.Interval=16;tick.Tick+=(s,e)=>UpdateState();
   Shown+=(s,e)=>{if(!qa){Hide();tick.Start();tray.ShowBalloonTip(5000,"Snapline is running","Touch the top edge or press Ctrl+Alt+T. Right-click the tray icon for setup.",ToolTipIcon.Info);}else pinned=true;};
   FormClosing+=(s,e)=>{if(e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Tuck();}};
   if(!qa){Handle.ToString();sequence=Native.GetClipboardSequenceNumber();var missed=new List<string>();if(!Native.RegisterHotKey(Handle,1,0x4003,(uint)Keys.T))missed.Add("Ctrl+Alt+T");if(!Native.RegisterHotKey(Handle,2,0x4003,(uint)Keys.S))missed.Add("Ctrl+Alt+S");if(!Native.RegisterHotKey(Handle,3,0x4003,(uint)Keys.P))missed.Add("Ctrl+Alt+P");if(missed.Count>0)Notify("Shortcuts busy: "+string.Join(", ",missed)+". Use the tray menu.");}
   Position(Screen.PrimaryScreen);
  }
  protected override CreateParams CreateParams {get{var p=base.CreateParams;if(!qa)p.ExStyle|=0x80000|0x08000000|0x80;return p;}}
  internal bool IsInInbox(string p){return string.Equals(Path.GetDirectoryName(Path.GetFullPath(p)),inbox,StringComparison.OrdinalIgnoreCase);}
  internal void Discard(string p){if(IsInInbox(p))Recycle(p);else Remove(p);}
  IEnumerable<ImageCard> AllCards(){return cards.Controls.OfType<ImageCard>();}
  ImageCard pressedCard;float slide=-222,targetSlide=-222; DateTime peekUntil=DateTime.MinValue,lastFrame=DateTime.Now,nextBreeze=DateTime.Now.AddSeconds(9);bool explicitReveal;int capacity=8;
  void Tuck(){targetSlide=-222;pinned=false;explicitReveal=false;}
  void Toggle(){if(Visible&&targetSlide==0)Tuck();else{explicitReveal=true;Reveal(Screen.FromPoint(Cursor.Position));}}
  void RenderOverlay(Graphics g){g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;
   var path=new GraphicsPath();path.AddBezier(-20,10+slide,Width*.333f,10+slide+Math.Min(30,Width*.018f)*4/3,Width*.667f,10+slide+Math.Min(30,Width*.018f)*4/3,Width+20,10+slide);
   using(path)using(var brush=new LinearGradientBrush(new Rectangle(0,0,Width,Height),Color.Transparent,Color.Transparent,LinearGradientMode.Horizontal)){brush.InterpolationColors=new ColorBlend{Positions=new[]{0f,.08f,.92f,1f},Colors=new[]{Color.Transparent,Color.FromArgb(210,145,145,149),Color.FromArgb(210,145,145,149),Color.Transparent}};using(var pen=new Pen(brush,1.2f))g.DrawPath(pen,path);}
   var live=AllCards().Where(c=>!c.Falling).ToArray();int n=live.Length;int i=0;
   foreach(var c in AllCards()){float x=Width/2f-(Math.Max(n-1,0)*174)/2f+(c.Falling?Math.Max(0,Array.FindIndex(live,k=>k==c)):i)*174;if(!c.Falling){i++;if(float.IsNaN(c.CenterX))c.CenterX=x;c.CenterX+=(x-c.CenterX)*.22f;x=c.CenterX;}if(c.Falling){var hit=c.Hit;x=hit.X+hit.Width/2;}GlassArt.Card(g,c,x,GlassArt.RopeY(x,Width),slide,0);}
   if(n==0&&targetSlide==0)GlassArt.Pill(g,"Take a screenshot and it will hang here",new RectangleF(Width/2f-160,GlassArt.RopeY(Width/2f,Width)+34+slide,320,30));
  }
  protected override void OnPaint(PaintEventArgs e){if(qa){base.OnPaint(e);RenderOverlay(e.Graphics);}}
  void PaintLayer(){if(!Visible)return;if(qa){Invalidate();return;}using(var b=new Bitmap(Width,Height,PixelFormat.Format32bppPArgb)){using(var g=Graphics.FromImage(b)){g.Clear(Color.Transparent);RenderOverlay(g);}Native.Layer(Handle,b,Left,Top);}}
  ImageCard HitCard(Point p){return AllCards().LastOrDefault(c=>!c.Falling&&RectangleF.Inflate(c.Hit,5,5).Contains(p));}
  protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);var hit=HitCard(e.Location);foreach(var c in AllCards())c.Hover=c==hit;if(pressedCard!=null)pressedCard.PointerMove(e.Location);Cursor=hit==null?Cursors.Default:Cursors.Hand;lastInside=DateTime.Now;}
  protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);foreach(var c in AllCards())c.Hover=false;}
  protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);var c=HitCard(e.Location);if(c==null)return;if(e.Button==MouseButtons.Right){c.ContextMenuStrip.Show(PointToScreen(e.Location));return;}if(e.Button!=MouseButtons.Left)return;if(e.X<c.Hit.X+25&&e.Y<c.Hit.Y+25){Discard(c.PathName);return;}pressedCard=c;c.Down(e.Location,e.Clicks);}
  protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(pressedCard!=null){pressedCard.Up(e.Clicks);pressedCard=null;}}
  protected override void OnMouseDoubleClick(MouseEventArgs e){base.OnMouseDoubleClick(e);var c=HitCard(e.Location);if(c!=null&&e.Button==MouseButtons.Left)c.Down(e.Location,2);}

  protected override bool ShowWithoutActivation{get{return !qa;}}
  protected override void WndProc(ref Message m){if(!qa&&m.Msg==0x0021){m.Result=new IntPtr(3);return;}if(m.Msg==0x0312){if(m.WParam.ToInt32()==1){Toggle();}else if(m.WParam.ToInt32()==2)StartSnip();else if(m.WParam.ToInt32()==3)CaptureScreen();}base.WndProc(ref m);}
  void BuildTray(){
   var menu=new ContextMenuStrip();menu.Items.Add("Show / hide   Ctrl+Alt+T",null,(s,e)=>{Toggle();});
   menu.Items.Add("Capture region   Ctrl+Alt+S",null,(s,e)=>StartSnip());menu.Items.Add("Capture this screen   Ctrl+Alt+P",null,(s,e)=>CaptureScreen());menu.Items.Add("Add clipboard image",null,(s,e)=>CaptureClipboard());menu.Items.Add("Import images...",null,(s,e)=>Import());
   var pin=new ToolStripMenuItem("Keep line visible"){CheckOnClick=true};pin.Click+=(s,e)=>{pinned=pin.Checked;if(pinned)Reveal(Screen.FromPoint(Cursor.Position));};menu.Items.Add(pin);
   var clip=new ToolStripMenuItem("Auto-collect clipboard images (opt in)"){Checked=settings.WatchClipboard,CheckOnClick=true};clip.Click+=(s,e)=>{settings.WatchClipboard=clip.Checked;sequence=Native.GetClipboardSequenceNumber();SaveSettings();if(clip.Checked)Notify("Enabled: any new clipboard image may be saved locally, not just screenshots. Turn this off here anytime.");};menu.Items.Add(clip);
   menu.Items.Add("Add a screenshot folder...",null,(s,e)=>ChooseFolder());menu.Items.Add("Manage watched folders...",null,(s,e)=>ManageFolders());menu.Items.Add("Open saved captures",null,(s,e)=>Run(inbox));menu.Items.Add("Clear shelf (keep all files)",null,(s,e)=>{settings.Items.Clear();RefreshCards();SaveSettings();});
   menu.Items.Add("About / help",null,(s,e)=>MessageBox.Show("Snapline for Windows\nAn independent Windows adaptation of Tendedero's screenshot shelf.\n\nTouch the top edge for a moment to reveal it.\nClick: copy image. Double-click: open. Hold: Snipping Tool.\nDrag: send the file. Right-click: save, reveal, remove, recycle.\n\nCtrl+Alt+T: shelf; Ctrl+Alt+S: region; Ctrl+Alt+P: screen.\nFiles stay on your PC. Clipboard auto-collection is off by default.\nNo account, network access or telemetry.\nSee README.md for differences and testing limits.","Snapline",MessageBoxButtons.OK,MessageBoxIcon.Information));
   menu.Items.Add("Quit",null,(s,e)=>{SaveSettings();tick.Stop();tray.Visible=false;Application.Exit();});
   tray=new NotifyIcon{Icon=Icon??SystemIcons.Application,Text="Snapline - screenshot shelf",ContextMenuStrip=menu,Visible=!qa};tray.DoubleClick+=(s,e)=>Reveal(Screen.FromPoint(Cursor.Position));
  }
  internal void Notify(string text){message=text;RefreshStatus();if(tray!=null&&!qa)tray.ShowBalloonTip(4500,"Snapline",text,ToolTipIcon.Info);}
  void RefreshStatus(){status.Text=message??(settings.Items.Count==0?"Take a snip or import an image. No screenshots are uploaded.":settings.Items.Count+" images  |  Click to copy - double-click to open - hold to edit - drag to share");}
  void SaveSettings(){try{settings.Save(settingsFile);}catch(Exception e){Notify("Couldn't save preferences: "+e.Message);}}
  internal void Add(string p,bool persist=true){if(!Helpers.IsImage(p)||!File.Exists(p)||settings.Items.Any(x=>string.Equals(x,p,StringComparison.OrdinalIgnoreCase)))return;ImageCard card;try{card=new ImageCard(this,p);}catch{return;}settings.Items.Add(p);cards.Controls.Add(card);while(settings.Items.Count>capacity){settings.Items.RemoveAt(0);var old=cards.Controls[0];cards.Controls.Remove(old);old.Dispose();}message=null;RefreshStatus();if(persist){SaveSettings();if(!qa){peekUntil=DateTime.Now.AddSeconds(2.5);Reveal(Screen.FromPoint(Cursor.Position));}}}
  void RefreshCards(){foreach(Control c in cards.Controls.Cast<Control>().ToArray())c.Dispose();cards.Controls.Clear();var paths=settings.Items.ToArray();settings.Items.Clear();foreach(var p in paths)Add(p,false);message=null;RefreshStatus();}
  internal void Prune(){var changed=settings.Items.RemoveAll(p=>!File.Exists(p))>0;if(changed){RefreshCards();SaveSettings();}}
  internal void Remove(string p){settings.Items.Remove(p);var c=AllCards().FirstOrDefault(k=>k.PathName==p);if(c!=null&&Visible&&!qa){c.Falling=true;c.FallStart=DateTime.Now;}else if(c!=null){cards.Controls.Remove(c);c.Dispose();}SaveSettings();}
  internal void CopyImage(string p){try{var im=Helpers.LoadImage(p);try{Clipboard.SetDataObject(new DataObject(DataFormats.Bitmap,im),true,5,100);var old=clipboardImage;clipboardImage=im;if(old!=null)old.Dispose();}catch{im.Dispose();throw;}if(!qa)sequence=Native.GetClipboardSequenceNumber();var card=AllCards().FirstOrDefault(c=>c.PathName==p);if(card!=null){card.Copied=DateTime.Now;card.Nudged=DateTime.Now;}}catch(Exception e){Notify("Copy failed: "+e.Message);}}
  internal void OpenImage(string p){Run(p);}
  internal void EditImage(string p){
   // Do not steal the clipboard or send keyboard input to another application.
   Hide();slide=-222;targetSlide=-222;pinned=false;
   string error;
   if(SnippingToolEditor.TryOpen(p,out error))return;
   if(!File.Exists(p)){Notify("Cannot open markup: "+error);return;}
   OpenMarkupManually(p,error);
  }
  internal void OpenMarkupManually(string p,string reason){
   bool launched=false;
   try{Process.Start(new ProcessStartInfo("explorer.exe","shell:AppsFolder\\"+SnippingToolEditor.AppId){UseShellExecute=true});launched=true;}catch{}
   using(var dialog=new Form{Text="Open image in Snipping Tool",Size=new Size(640,250),StartPosition=FormStartPosition.CenterScreen,TopMost=true}){
    var description=new Label{Dock=DockStyle.Top,Height=115,Padding=new Padding(12),Text=(launched?"Windows could not hand this image directly to Snipping Tool. It has been asked to open.":"Snipping Tool could not be launched. Install or update it, then open it from Start.")+"\n\nIn Snipping Tool, press Ctrl+O and open the file below. If the editor opened but no image loaded, use this same step. Save to the original path to refresh its shelf thumbnail.\n\nDetails: "+reason};
    var pathBox=new TextBox{Dock=DockStyle.Top,ReadOnly=true,Text=Path.GetFullPath(p)};
    var done=new Button{Dock=DockStyle.Bottom,Height=36,Text="Close instructions"};done.Click+=(sender,args)=>dialog.Close();
    dialog.Controls.Add(pathBox);dialog.Controls.Add(description);dialog.Controls.Add(done);dialog.ShowDialog();
   }
  }
  internal void RevealFile(string p){Run("explorer.exe","/select,\""+p+"\"");}
  void Run(string file,string args=null){try{Process.Start(new ProcessStartInfo(file,args??""){UseShellExecute=true});}catch(Exception e){Notify("Couldn't open: "+e.Message);}}
  internal void SaveCopy(string p){using(var dialog=new SaveFileDialog{FileName=Path.GetFileName(p),Filter="Image file|*"+Path.GetExtension(p),OverwritePrompt=true}){if(dialog.ShowDialog()!=DialogResult.OK)return;try{if(!string.Equals(Path.GetFullPath(p),Path.GetFullPath(dialog.FileName),StringComparison.OrdinalIgnoreCase))File.Copy(p,dialog.FileName,true);Notify("Copy saved. Original stays on the shelf.");}catch(Exception e){Notify("Save failed: "+e.Message);}}}
  internal void Recycle(string p){if(MessageBox.Show("Move this file to the Recycle Bin?\n"+p,"Snapline",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;try{FileSystem.DeleteFile(p,UIOption.OnlyErrorDialogs,RecycleOption.SendToRecycleBin);Prune();}catch(Exception e){Notify("Couldn't recycle: "+e.Message);}}
  void Import(){using(var d=new OpenFileDialog{Multiselect=true,Filter="Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff"})if(d.ShowDialog()==DialogResult.OK){foreach(var p in d.FileNames)Add(p);Reveal(Screen.FromPoint(Cursor.Position));}}
  void ChooseFolder(){using(var d=new FolderBrowserDialog{Description="Choose the folder where your screenshot tool saves images"})if(d.ShowDialog()==DialogResult.OK&&!settings.Folders.Contains(d.SelectedPath)){settings.Folders.Add(d.SelectedPath);SaveSettings();Scan(true);Notify("Watching "+d.SelectedPath+". Only new or changed images are added.");}}
  void ManageFolders(){using(var dialog=new Form{Text="Watched folders",Size=new Size(660,270),StartPosition=FormStartPosition.CenterScreen}){var list=new ListBox{Dock=DockStyle.Fill};foreach(var f in settings.Folders)list.Items.Add(f);dialog.Controls.Add(list);var b=new Button{Dock=DockStyle.Bottom,Height=40,Text="Stop watching selected folder (does not delete files)"};dialog.Controls.Add(b);b.Click+=(s,e)=>{if(list.SelectedItem==null)return;var p=list.SelectedItem.ToString();if(p==inbox){MessageBox.Show("The app's capture inbox is always watched.");return;}settings.Folders.Remove(p);list.Items.Remove(p);SaveSettings();};dialog.ShowDialog();}}
  void Position(Screen screen){var area=screen.WorkingArea;Width=area.Width;Height=screen.Bounds.Height;Left=area.Left;Top=area.Top;capacity=Math.Max(1,(Width-40)/174);while(settings.Items.Count>capacity){var p=settings.Items[0];Remove(p);}}
  void Reveal(Screen screen){Position(screen);lastInside=DateTime.Now;targetSlide=0;Show();PaintLayer();}
  void StartSnip(){Hide();slide=-222;targetSlide=-222;pinned=false;sequence=Native.GetClipboardSequenceNumber();waitSnip=DateTime.Now.AddSeconds(90);Run("ms-screenclip:");}
  void CaptureClipboard(){try{if(!Clipboard.ContainsImage()){Notify("No image on the clipboard.");return;}using(var image=Clipboard.GetImage()){var p=Helpers.Unique(inbox,"Snap-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".png");image.Save(p,ImageFormat.Png);Add(p);}if(!qa)sequence=Native.GetClipboardSequenceNumber();message="Screenshot saved in the local inbox.";}catch(Exception e){Notify("Capture failed: "+e.Message);}}
  void CaptureScreen(){var screen=Screen.FromPoint(Cursor.Position);bool was=Visible;Hide();Application.DoEvents();Thread.Sleep(160);try{var rect=screen.Bounds;using(var im=new Bitmap(rect.Width,rect.Height))using(var g=Graphics.FromImage(im)){g.CopyFromScreen(rect.Location,Point.Empty,rect.Size);var p=Helpers.Unique(inbox,"Screen-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".png");im.Save(p,ImageFormat.Png);Add(p);}message="Screen saved locally.";}catch(Exception e){Notify("Screen capture failed: "+e.Message);}if(was)Reveal(screen);}
  bool Fullscreen(Screen screen){var handle=Native.GetForegroundWindow();if(handle==IntPtr.Zero||handle==Handle||Native.IsIconic(handle))return false;var name=new System.Text.StringBuilder(128);Native.GetClassName(handle,name,name.Capacity);if(name.ToString()=="Progman"||name.ToString()=="WorkerW")return false;Native.RECT r;if(!Native.GetWindowRect(handle,out r))return false;var b=screen.Bounds;return r.Left<=b.Left&&r.Top<=b.Top&&r.Right>=b.Right&&r.Bottom>=b.Bottom;}
  void UpdateState(){try{
   var now=DateTime.Now;float dt=Math.Min(.1f,(float)(now-lastFrame).TotalSeconds);lastFrame=now;
   slide+=(targetSlide-slide)*Math.Min(1,dt*(targetSlide==0?14:19));if(Math.Abs(targetSlide-slide)<.1f)slide=targetSlide;
   foreach(var c in AllCards().Where(c=>c.Falling&&(now-c.FallStart).TotalSeconds>.56).ToArray()){cards.Controls.Remove(c);c.Dispose();}
   if(now>nextBreeze){foreach(var c in AllCards())c.Nudged=now;nextBreeze=now.AddSeconds(10);}
   if(++counter%65==0){Scan(false);Prune();}
   var seq=Native.GetClipboardSequenceNumber();if(seq!=sequence){sequence=seq;if(settings.WatchClipboard||waitSnip>now){if(Clipboard.ContainsImage()){CaptureClipboard();waitSnip=DateTime.MinValue;}}}
   PaintLayer();if(Visible&&targetSlide<0&&slide==targetSlide)Hide();
   if(Dragging||tray.ContextMenuStrip.Visible||AllCards().Any(c=>c.ContextMenuStrip.Visible)||Application.OpenForms.Count>1)return;
   var p=Cursor.Position;var screen=Screen.FromPoint(p);if(Fullscreen(screen)){Tuck();edgeSince=DateTime.MinValue;return;}
   bool edge=p.Y<=screen.WorkingArea.Top+2&&p.Y>=screen.Bounds.Top;
   if(edge){if(edgeSince==DateTime.MinValue)edgeSince=now;if((now-edgeSince).TotalMilliseconds>250&&!Visible)Reveal(screen);}else edgeSince=DateTime.MinValue;
   if(Visible&&targetSlide==0){var local=PointToClient(p);bool inside=local.Y>=0&&local.Y<210&&local.X>=0&&local.X<Width;
    if(inside){lastInside=now;explicitReveal=false;}else if(!pinned&&!explicitReveal&&now>peekUntil&&(now-lastInside).TotalMilliseconds>500)Tuck();}
  }catch(ExternalException){}catch(Exception e){tick.Stop();Notify("Overlay stopped: "+e.Message+". Quit and restart Snapline.");}}
  void Scan(bool baseline){foreach(var folder in settings.Folders.ToArray()){try{if(!Directory.Exists(folder))continue;foreach(var p in Directory.EnumerateFiles(folder).Where(Helpers.IsImage)){var info=new FileInfo(p);var stamp=info.Length+":"+info.LastWriteTimeUtc.Ticks;string old;if(baseline){observed[p]=stamp;continue;}if(observed.TryGetValue(p,out old)&&old==stamp)continue;string prior;if(!pending.TryGetValue(p,out prior)||prior!=stamp){pending[p]=stamp;continue;}
    if(settings.Items.Contains(p)){var card=AllCards().FirstOrDefault(c=>c.PathName==p);if(card!=null){using(var updated=Helpers.LoadImage(p)){float factor=Math.Min(1f,600f/Math.Max(updated.Width,updated.Height));var thumb=new Bitmap(updated,Math.Max(1,(int)(updated.Width*factor)),Math.Max(1,(int)(updated.Height*factor)));card.Preview.Dispose();card.Preview=thumb;card.Nudged=DateTime.Now;}}observed[p]=stamp;pending.Remove(p);failures.Remove(p);}else{Add(p);if(settings.Items.Contains(p)){observed[p]=stamp;pending.Remove(p);failures.Remove(p);}else{int n=failures.ContainsKey(p)?failures[p]+1:1;failures[p]=n;if(n>=5){observed[p]=stamp;pending.Remove(p);failures.Remove(p);Notify("Skipped an unreadable image: "+Path.GetFileName(p));}}}
   }}catch(IOException){}catch(UnauthorizedAccessException){}}}
  internal void QaScan(){Scan(false);Scan(false);}
  internal int QaCount {get{return settings.Items.Count;}}
  internal void QaAnimations(string prefix){var first=AllCards().First();foreach(var c in AllCards()){c.Born=DateTime.Now.AddSeconds(-4);c.Nudged=DateTime.Now.AddSeconds(-4);}first.Hover=true;first.Copied=DateTime.Now;QaRender(prefix+"-copied.png");first.Copied=DateTime.MinValue;first.Falling=true;first.FallStart=DateTime.Now.AddSeconds(-.38);QaRender(prefix+"-fall.png");first.Falling=false;slide=-222;using(var b=new Bitmap(Width,Height,PixelFormat.Format32bppPArgb)){using(var g=Graphics.FromImage(b)){g.Clear(Color.Transparent);RenderOverlay(g);}if(b.GetPixel(Width/2,50).A!=0)throw new Exception("Hidden line still visible");}slide=0;Console.WriteLine("PASS renderer: hover/copy feedback, falling card frame and fully retracted transparent frame");}
  internal void QaExtra(){using(var bitmap=new Bitmap(Width,Height,PixelFormat.Format32bppPArgb)){using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.Transparent);RenderOverlay(g);}if(bitmap.GetPixel(5,220).A!=0)throw new Exception("Empty space not transparent");bool silver=false;for(int y=0;y<70;y++)if(bitmap.GetPixel(Width/2,y).A>0)silver=true;if(!silver)throw new Exception("Rope/card missing");}var first=settings.Items[0];CopyImage(first);if(!Clipboard.ContainsImage())throw new Exception("Clipboard copy");message=null;SaveSettings();if(Settings.Load(settingsFile).Items.Count!=3)throw new Exception("Persistence");RefreshCards();if(cards.Controls.Count!=3)throw new Exception("Restore");File.Delete(settings.Items[2]);Prune();if(QaCount!=2)throw new Exception("Pruning");Remove(first);if(!File.Exists(first)||QaCount!=1)throw new Exception("Non-destructive removal");for(int i=0;i<26;i++){var p=Path.Combine(inbox,"bound-"+i+".png");File.Copy(first,p);Add(p);}if(QaCount!=capacity||cards.Controls.Count!=capacity)throw new Exception("Shelf limit");Console.WriteLine("PASS GUI: clipboard image, persisted shelf restore, file pruning, non-destructive removal, screen-width card bound");}
  internal void QaRender(string path){slide=0;using(var b=new Bitmap(Width,Math.Min(720,Height))){using(var g=Graphics.FromImage(b)){using(var bg=new LinearGradientBrush(new Rectangle(0,0,b.Width,b.Height),Color.FromArgb(180,194,215),Color.FromArgb(220,187,162),35f))g.FillRectangle(bg,0,0,b.Width,b.Height);using(var brush=new SolidBrush(Color.FromArgb(255,246,247,250)))using(var frame=GlassArt.Rounded(new RectangleF(160,230,Width-280,440),20))g.FillPath(brush,frame);using(var f=new Font("Segoe UI",16,FontStyle.Bold))g.DrawString("Example desktop window",f,Brushes.DimGray,195,265);using(var f=new Font("Segoe UI",11))g.DrawString("Preview backdrop only. No source desktop or screenshots copied.",f,Brushes.Gray,195,305);RenderOverlay(g);}b.Save(path,ImageFormat.Png);}}
  protected override void Dispose(bool disposing){if(disposing){if(!qa){for(int i=1;i<=3;i++)Native.UnregisterHotKey(Handle,i);}tick.Dispose();cards.Dispose();status.Dispose();pinButton.Dispose();if(clipboardImage!=null)clipboardImage.Dispose();if(tray!=null){tray.Visible=false;tray.Dispose();}SaveSettings();}base.Dispose(disposing);}
 }
 internal static class Program {
  [STAThread] static int Main(string[] args){
   if(args.Contains("--self-test"))return Test();
   bool qa=args.Contains("--qa");bool created;using(var mutex=new Mutex(true,"Local\\Snapline-ScreenshotShelf",out created)){if(!created&&!qa){MessageBox.Show("Snapline is already running. Look in the system tray (including hidden icons), or press Ctrl+Alt+T.");return 0;}
    Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
    Application.ThreadException+=(s,e)=>MessageBox.Show("Snapline: "+e.Exception.Message);
    if(qa){var dir=Path.Combine(Path.GetTempPath(),"snapline-qa-"+Guid.NewGuid());Directory.CreateDirectory(dir);using(var shelf=new Shelf(true,dir)){shelf.Show();Application.DoEvents();Thread.Sleep(250);Application.DoEvents();shelf.QaRender(args[1]+"-empty.png");var inbox=Path.Combine(dir,"Inbox");for(int i=0;i<3;i++){using(var im=new Bitmap(i==1?150:500,280))using(var g=Graphics.FromImage(im)){g.Clear(new[]{Color.Teal,Color.SteelBlue,Color.DarkSlateBlue}[i]);g.FillEllipse(Brushes.Gold,350,35,65,65);g.FillPolygon(Brushes.LightSeaGreen,new[]{new Point(0,280),new Point(190,80),new Point(400,280)});g.DrawString(new[]{"Coastal hike","Design notes","Weekend plans"}[i],new Font("DejaVu Sans",22),Brushes.White,20,215);im.Save(Path.Combine(inbox,"Screenshot-"+(i+1)+".png"));}}shelf.QaScan();if(shelf.QaCount!=3)throw new Exception("Watcher failed");Application.DoEvents();Thread.Sleep(250);Application.DoEvents();shelf.QaRender(args[1]+"-shelf.png");shelf.QaAnimations(args[1]);shelf.QaExtra();Console.WriteLine("PASS GUI: startup, stable-file watching, three aspect-ratio image cards, per-pixel-alpha transparency, shared overlay renderer");}Directory.Delete(dir,true);return 0;}
    using(var shelf=new Shelf())Application.Run(shelf);return 0;
   }
  }
  static int Test(){var dir=Path.Combine(Path.GetTempPath(),"snapline-test-"+Guid.NewGuid());Directory.CreateDirectory(dir);try{SnippingToolEditor.SelfTest(dir);if(!Helpers.IsImage("a.PNG")||Helpers.IsImage("a.exe"))throw new Exception("Extensions");File.WriteAllText(Path.Combine(dir,"a.png"),"test");if(Helpers.Unique(dir,"a.png")!=Path.Combine(dir,"a (2).png"))throw new Exception("Collision");var s=new Settings();s.Folders.Add("C:\\Users\\A&B\\Screenshots");s.Items.Add("日本語.png");s.WatchClipboard=true;var file=Path.Combine(dir,"settings.xml");s.Save(file);var restored=Settings.Load(file);if(!restored.WatchClipboard||restored.Items[0]!=s.Items[0]||restored.Folders[0]!=s.Folders[0])throw new Exception("Settings roundtrip");File.WriteAllText(file,"broken");if(Settings.Load(file).Items.Count!=0)throw new Exception("Corrupt settings fallback");using(var b=new Bitmap(20,10))b.Save(Path.Combine(dir,"image.png"),ImageFormat.Png);using(var im=Helpers.LoadImage(Path.Combine(dir,"image.png")))if(im.Width!=20)throw new Exception("Image load");File.Delete(Path.Combine(dir,"image.png"));Console.WriteLine("PASS: image types, collision-safe names, settings roundtrip, corrupt settings recovery, image loading without file locks");return 0;}finally{Directory.Delete(dir,true);}}
 }
}
