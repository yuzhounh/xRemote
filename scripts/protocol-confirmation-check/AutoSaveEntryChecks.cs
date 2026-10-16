using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Expression=System.Linq.Expressions.Expression;
static class AutoSaveEntryChecks {
 const BindingFlags M=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static object Get(object o,string n)=>o.GetType().GetProperty(n,M)!.GetValue(o)!;
 static void Set(object o,string n,object? v)=>o.GetType().GetProperty(n,M)!.SetValue(o,v);
 static void Field(object o,string n,object? v)=>o.GetType().GetField(n,M)!.SetValue(o,v);
 static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,M)!.Invoke(o,args)!;
 static void Need(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Pump()=>Application.Current.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
 static void Wait(Func<bool> done){var end=DateTime.UtcNow.AddSeconds(5);while(!done()&&DateTime.UtcNow<end){Pump();Thread.Sleep(1);}Need(done(),"Fixture timeout");}
 static void Auto(object vm){var t=(Task<bool>)Call(vm,"SaveExistingAsync");Wait(()=>t.IsCompleted);Need(t.GetAwaiter().GetResult(),"Autosave failed");}
 public static void Run(Assembly asm,Application app){
  SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(app.Dispatcher));
  foreach(var uri in new[]{"/1Remote;component/Resources/Theme/SettingsTextFallback.xaml","/1Remote;component/Resources/Languages/zh-cn.xaml"})app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri(uri,UriKind.Relative)});
  var themeType=asm.GetType("_1RM.Service.ThemeService")!;Activator.CreateInstance(themeType,app.Resources,Activator.CreateInstance(asm.GetType("_1RM.Service.ThemeConfig")!));
  var failures=new List<string>();void Check(string name,Action action){try{action();Console.WriteLine("PASS: "+name);}catch(Exception ex){var cause=ex is TargetInvocationException t&&t.InnerException!=null?t.InnerException:ex;failures.Add(name+": "+cause.Message);Console.WriteLine("FAIL: "+name+": "+cause);}}
  var protocolType=asm.GetType("_1RM.Model.Protocol.Base.ProtocolBase")!;var resultType=asm.GetType("_1RM.Service.DataSource.DAO.Result")!;var vmType=asm.GetType("_1RM.View.Editor.ServerEditorPageViewModel")!;var sessionType=asm.GetType("_1RM.View.Editor.ConnectionAutoSaveSession")!;
  object New(string n)=>Activator.CreateInstance(asm.GetType("_1RM.Model.Protocol."+n)!)!;
  var stylet=Assembly.LoadFrom(Path.Combine(Path.GetDirectoryName(asm.Location)!,"Stylet.dll"));
  EntryPopupProxy.ClosedModel=model=>stylet.GetType("Stylet.IScreenState")!.GetMethod("Close")!.Invoke(model,null);
  var manager=DispatchProxy.Create(stylet.GetType("Stylet.IWindowManager")!,typeof(EntryPopupProxy));var windowManager=(EntryPopupProxy)manager;
  windowManager.ShowModel=model=>{var parent=DispatchProxy.Create(stylet.GetType("Stylet.IChildDelegate")!,typeof(EntryPopupProxy));Set(model,"Parent",parent);stylet.GetType("Stylet.IScreenState")!.GetMethod("Activate")!.Invoke(model,null);windowManager.Latest=model;};
  var mainType=asm.GetType("_1RM.View.MainWindowViewModel")!;var main=RuntimeHelpers.GetUninitializedObject(mainType);
  var ioc=asm.GetType("_1RM.IoC")!;var getter=ioc.GetField("GetByType")!;var oldGetter=getter.GetValue(null);Func<Type,string,object?> lookup=(t,k)=>t==mainType?main:t==stylet.GetType("Stylet.IWindowManager")?manager:null;var typeArg=Expression.Parameter(typeof(Type));var keyArg=Expression.Parameter(typeof(string));getter.SetValue(null,Expression.Lambda(getter.FieldType,Expression.Invoke(Expression.Constant(lookup),typeArg,keyArg),typeArg,keyArg).Compile());
  var output=Path.GetFullPath("artifacts/autosave-entry-audit/fixtures");Directory.CreateDirectory(output);int id=0;
  (object Model,List<string> Writes,string File) Model(object protocol){Set(protocol,"Id","entry-fixture-"+(++id));Set(protocol,"DisplayName","Fixture");var writes=new List<string>();var file=Path.Combine(output,"fixture-"+id+".json");
   var vm=RuntimeHelpers.GetUninitializedObject(vmType);Field(vm,"_server",protocol);Field(vm,"_orgServer",Call(protocol,"Clone"));Field(vm,"_isExistingConnection",true);
   var form=Activator.CreateInstance(asm.GetType("_1RM.View.Editor.Forms.ProtocolBaseFormViewModel")!,protocol)!;Field(vm,"_editorViewModel",form);
   Func<object,object> sink=snapshot=>{var json=(string)Call(snapshot,"ToJsonString");File.WriteAllText(file,json);writes.Add(json);return resultType.GetMethod("Success")!.Invoke(null,null)!;};var parameter=Expression.Parameter(protocolType);var persist=Expression.Lambda(typeof(Func<,>).MakeGenericType(protocolType,resultType),Expression.Convert(Expression.Invoke(Expression.Constant(sink),Expression.Convert(parameter,typeof(object))),resultType),parameter).Compile();Field(vm,"_autoSaveSession",Activator.CreateInstance(sessionType,M,null,new[]{protocol,persist},null));Call(vm,"BeginEditSession");return(vm,writes,file);
  }
  void Execute(object model,string cmd)=>((ICommand)Get(model,cmd)).Execute(null);
  object Open(object vm,string cmd,object? argument){((ICommand)Get(vm,cmd)).Execute(argument);Need(windowManager.Latest!=null,"Popup not routed to isolated window manager");return windowManager.Latest!;}
  Check("unchanged APP path focus/blur preserves existing argument list",()=>{var p=New("LocalApp");Set(p,"ExePath",@"C:\fixture\chrome.exe");var form=Activator.CreateInstance(asm.GetType("_1RM.View.Editor.Forms.LocalAppFormViewModel")!,p)!;var before=(string)Call(p,"ToJsonString");Call(form,"ResSetArguments");Need((string)Call(p,"ToJsonString")==before,"Unchanged path reapplied preset arguments");});
  Check("changed APP path applies its preset and still autosaves",()=>{var p=New("LocalApp");Set(p,"ExePath",@"C:\fixture\unknown.exe");var form=Activator.CreateInstance(asm.GetType("_1RM.View.Editor.Forms.LocalAppFormViewModel")!,p)!;var m=Model(p);Set(form,"ExePath",@"C:\fixture\chrome.exe");Call(form,"ResSetArguments");Need(((IList)Get(p,"ArgumentList")).Count>0,"Changed path stopped applying presets");Auto(m.Model);Need(m.Writes.Count==1&&File.ReadAllText(m.File)==(string)Call(p,"ToJsonString"),"Changed executable not persisted");});
  Check("parameter popup cancel preserves parent; confirmed edit autosaves without another click",()=>{var p=New("LocalApp");Set(p,"ExePath",@"C:\fixture\chrome.exe");var argument=Activator.CreateInstance(asm.GetType("_1RM.Model.Protocol.AppArgument")!,new object?[]{true})!;Set(argument,"Name","arg");Set(argument,"Value","before");((IList)Get(p,"ArgumentList")).Add(argument);var m=Model(p);var before=(string)Call(p,"ToJsonString");var cancel=Open(m.Model,"CmdEditArgument",argument);Set(cancel,"Value","cancelled");Execute(cancel,"CmdCancel");Auto(m.Model);Need((string)Call(p,"ToJsonString")==before&&m.Writes.Count==0,"Cancelled argument changed parent");var save=Open(m.Model,"CmdEditArgument",argument);Set(save,"Value","after");Execute(save,"CmdSave");Wait(()=>m.Writes.Count>0);Need(File.ReadAllText(m.File)==(string)Call(p,"ToJsonString"),"Confirmed argument did not reach persistence");});
  Check("alternate credential popup cancel preserves parent; confirmed edit autosaves",()=>{var p=New("RDP");Set(p,"Address","127.0.0.1");var c=Activator.CreateInstance(asm.GetType("_1RM.Model.Protocol.Base.Credential")!,new object?[]{true})!;Set(c,"Name","alternate");Set(c,"Address","127.0.0.2");Set(c,"Port","3389");Set(c,"UserName","fixture-user");Set(c,"Password","fixture-password");((IList)Get(p,"AlternateCredentials")).Add(c);var m=Model(p);var before=(string)Call(p,"ToJsonString");var cancel=Open(m.Model,"CmdEditCredential",c);Set(cancel,"UserName","cancelled");Execute(cancel,"CmdCancel");Auto(m.Model);Need((string)Call(p,"ToJsonString")==before&&m.Writes.Count==0,"Cancelled credential changed parent");var save=Open(m.Model,"CmdEditCredential",c);Set(save,"UserName","confirmed");Execute(save,"CmdSave");Wait(()=>m.Writes.Count>0);Need(File.ReadAllText(m.File)==(string)Call(p,"ToJsonString"),"Confirmed credential not persisted");});
  Check("icon popup cancel preserves parent; confirmed icon autosaves even without main-window mask",()=>{var p=New("RDP");Set(p,"Address","127.0.0.1");string Png(byte b){var bitmap=BitmapSource.Create(1,1,96,96,PixelFormats.Bgra32,null,new byte[]{b,0,0,255},4);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=new MemoryStream();encoder.Save(stream);return Convert.ToBase64String(stream.ToArray());}Set(p,"IconBase64",Png(20));var m=Model(p);var before=(string)Call(p,"ToJsonString");var cancel=Open(m.Model,"CmdSelectImage",null);Set(cancel,"Icon",BitmapSource.Create(1,1,96,96,PixelFormats.Bgra32,null,new byte[]{50,0,0,255},4));Execute(cancel,"CmdCancel");Auto(m.Model);Need((string)Call(p,"ToJsonString")==before&&m.Writes.Count==0,"Cancelled icon changed parent");var save=Open(m.Model,"CmdSelectImage",null);Set(save,"Icon",BitmapSource.Create(1,1,96,96,PixelFormats.Bgra32,null,new byte[]{80,0,0,255},4));Execute(save,"CmdSave");Wait(()=>m.Writes.Count>0);Need(File.ReadAllText(m.File)==(string)Call(p,"ToJsonString"),"Confirmed icon not persisted");});
  Check("view/help click and focus loss do not write; text edits and checkbox changes autosave",()=>{var p=New("RDP");Set(p,"Address","127.0.0.1");var m=Model(p);var stack=new StackPanel();var help=new Button();var text=new TextBox{DataContext=p};text.SetBinding(TextBox.TextProperty,new Binding("DisplayName"){Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged});stack.Children.Add(help);stack.Children.Add(text);var t=asm.GetType("_1RM.View.Editor.ServerEditorWindowView")!;var ctor=t.GetConstructor(M,null,new[]{typeof(FrameworkElement),typeof(bool),typeof(Func<Task<bool>>),typeof(Func<bool>)},null)!;var w=(Window)ctor.Invoke(new object[]{stack,true,(Func<Task<bool>>)(()=> (Task<bool>)Call(m.Model,"SaveExistingAsync")),(Func<bool>)(()=>false)});Field(w,"_model",m.Model);Field(w,"_ready",true);help.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();Need(m.Writes.Count==0,"View click wrote unchanged data");text.Text="Edited fixture";text.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice,0,text,help){RoutedEvent=Keyboard.LostKeyboardFocusEvent});Wait(()=>m.Writes.Count==1);Need(File.ReadAllText(m.File)==(string)Call(p,"ToJsonString"),"Text edit not persisted");Set(p,"EnableClipboard",false);help.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Wait(()=>m.Writes.Count==2);var close=(Task<bool>)Call(w,"RequestCloseAsync");Wait(()=>close.IsCompleted);Need(close.Result&&m.Writes.Count==2,"Unchanged close repeated write");});
  Check("confirmed child edits inside a protocol preview cannot change the saved connection",()=>{var p=New("RDP");Set(p,"Address","127.0.0.1");var m=Model(p);var original=(string)Call(p,"ToJsonString");var preview=New("LocalApp");Set(preview,"Id",Get(p,"Id"));Set(preview,"DisplayName","Fixture");Set(preview,"ExePath",@"C:\fixture\chrome.exe");var argument=Activator.CreateInstance(asm.GetType("_1RM.Model.Protocol.AppArgument")!,new object?[]{true})!;Set(argument,"Name","preview-arg");Set(argument,"Value","before");((IList)Get(preview,"ArgumentList")).Add(argument);Set(m.Model,"Server",preview);Need((bool)Get(m.Model,"IsProtocolPreview"),"Fixture did not enter protocol preview");var child=Open(m.Model,"CmdEditArgument",argument);Set(child,"Value","preview-only");Execute(child,"CmdSave");Auto(m.Model);Need(m.Writes.Count==0&&(string)Call(p,"ToJsonString")==original,"Preview child edit wrote saved connection");});
  getter.SetValue(null,oldGetter);Need(failures.Count==0,string.Join("; ",failures));Console.WriteLine("PASS: isolated event paths and fake persisted files; no real connection records, visible windows or remote sessions used.");
 }
}
public class EntryPopupProxy:DispatchProxy {
 public object? Latest;public Action<object>? ShowModel;public static Action<object>? ClosedModel;
 protected override object? Invoke(MethodInfo? method,object?[]? args){if(method?.Name=="ShowWindow")ShowModel?.Invoke(args![0]!);if(method?.Name=="CloseItem")ClosedModel?.Invoke(args![0]!);if(method?.ReturnType==typeof(bool))return true;return null;}
}
