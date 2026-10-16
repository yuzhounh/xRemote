using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Expression=System.Linq.Expressions.Expression;
static class ProtocolConfirmationChecks {
 const BindingFlags M=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static object Get(object o,string n)=>o.GetType().GetProperty(n,M)!.GetValue(o)!;
 static void Set(object o,string n,object? v)=>o.GetType().GetProperty(n,M)!.SetValue(o,v);
 static void Field(object o,string n,object? v)=>o.GetType().GetField(n,M)!.SetValue(o,v);
 static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,M)!.Invoke(o,args)!;
 static void Need(bool ok,string message){if(!ok)throw new Exception(message);}
 static T Done<T>(Task<T> t){var deadline=DateTime.UtcNow.AddSeconds(8);while(!t.IsCompleted&&DateTime.UtcNow<deadline){Application.Current.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);Thread.Sleep(1);}Need(t.IsCompleted,"Fixture timeout");return t.GetAwaiter().GetResult();}
 public static void Run(Assembly asm,Application app){
  foreach(var uri in new[]{"/1Remote;component/Resources/Theme/SettingsTextFallback.xaml","/1Remote;component/Resources/Languages/zh-cn.xaml"}) app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri(uri,UriKind.Relative)});
  var pType=asm.GetType("_1RM.Model.Protocol.Base.ProtocolBase")!;var resultType=asm.GetType("_1RM.Service.DataSource.DAO.Result")!;var sessionType=asm.GetType("_1RM.View.Editor.ConnectionAutoSaveSession")!;var vmType=asm.GetType("_1RM.View.Editor.ServerEditorPageViewModel")!;
  object New(string type)=>Activator.CreateInstance(asm.GetType("_1RM.Model.Protocol."+type)!)!;
  object Success()=>resultType.GetMethod("Success")!.Invoke(null,null)!;
  bool Succeeded(object result)=>(bool)resultType.GetField("IsSuccess")!.GetValue(result)!;
  Delegate Persist(Func<object,object> f){var p=Expression.Parameter(pType);return Expression.Lambda(typeof(Func<,>).MakeGenericType(pType,resultType),Expression.Convert(Expression.Invoke(Expression.Constant(f),Expression.Convert(p,typeof(object))),resultType),p).Compile();}
  object Session(object initial,Func<object,object> f)=>Activator.CreateInstance(sessionType,M,null,new[]{initial,Persist(f)},null)!;
  object Save(object session,string method,object draft)=>DoneResult(Call(session,method,draft,(Func<bool>)(()=>true)));
  object DoneResult(object task){AwaitTask((Task)task);return Get(task,"Result");}
  void AwaitTask(Task t){var deadline=DateTime.UtcNow.AddSeconds(8);while(!t.IsCompleted&&DateTime.UtcNow<deadline){app.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);Thread.Sleep(1);}Need(t.IsCompleted,"Save timeout");t.GetAwaiter().GetResult();}
  var configType=asm.GetType("_1RM.Service.ProtocolConfigurationService")!;var config=RuntimeHelpers.GetUninitializedObject(configType);Field(config,"<ProtocolConfigs>k__BackingField",Activator.CreateInstance(configType.GetProperty("ProtocolConfigs")!.PropertyType));
  var ioc=asm.GetType("_1RM.IoC")!;var getter=ioc.GetField("GetByType")!;var originalGetter=getter.GetValue(null);var argType=Expression.Parameter(typeof(Type));var argKey=Expression.Parameter(typeof(string));Func<Type,string,object?> lookup=(t,k)=>t==configType?config:null;
  getter.SetValue(null,Expression.Lambda(getter.FieldType,Expression.Invoke(Expression.Constant(lookup),argType,argKey),argType,argKey).Compile());
  var written=new List<object>();bool fail=false;var rdp=New("RDP");Set(rdp,"Id","protocol-fixture");Set(rdp,"DisplayName","Fixture");Set(rdp,"Address","127.0.0.1");Set(rdp,"Password","fixture-password");
  var vm=RuntimeHelpers.GetUninitializedObject(vmType);Field(vm,"_server",rdp);Field(vm,"_orgServer",Call(rdp,"Clone"));Field(vm,"_selectedProtocol",rdp);Field(vm,"_isExistingConnection",true);Field(vm,"<Runners>k__BackingField",new ObservableCollection<string>());
  var protocolNames=new[]{"RDP","RdpApp","VNC","SSH","Telnet","Serial","FTP","SFTP","LocalApp"};var protocols=(IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(pType))!;foreach(var name in protocolNames) protocols.Add(name=="RDP"?rdp:New(name));Set(vm,"ProtocolList",protocols);
  Field(vm,"_editorViewModel",Activator.CreateInstance(asm.GetType("_1RM.View.Editor.Forms.RdpFormViewModel")!,rdp));
  var session=Session(rdp,snapshot=>{if(fail)return resultType.GetMethod("Fail",new[]{typeof(string)})!.Invoke(null,new object[]{"fixture failure"})!;written.Add(snapshot);return Success();});Field(vm,"_autoSaveSession",session);Call(vm,"BeginEditSession");
  object Protocol(string name)=>protocols.Cast<object>().Single(p=>p.GetType().Name==name);
  bool Auto()=>Done((Task<bool>)Call(vm,"SaveExistingAsync"));
  Set(vm,"SelectedProtocol",Protocol("FTP"));var isolated=Get(vm,"Server");Need(!ReferenceEquals(Get(isolated,"AlternateCredentials"),Get(rdp,"AlternateCredentials")),"Preview shares mutable credentials with current connection");((IList)Get(isolated,"Tags")).Add("preview-only");Set(isolated,"Password","preview-password");Need(!((IList)Get(rdp,"Tags")).Contains("preview-only")&&(string)Get(rdp,"Password")=="fixture-password","Preview edits leaked into current connection");Call(vm,"ReturnToSavedProtocol");
  foreach(var name in protocolNames.Skip(1)){Set(vm,"SelectedProtocol",Protocol(name));Need((bool)Get(vm,"IsProtocolPreview"),"Missing preview for "+name);Need(Auto()&&written.Count==0,"Browsing wrote protocol "+name);}
  Call(vm,"ReturnToSavedProtocol");Need(ReferenceEquals(Get(vm,"Server"),rdp)&&!(bool)Get(vm,"IsProtocolPreview"),"Return lost original settings");Need((string)Get(rdp,"Port")=="3389"&&(string)Get(rdp,"Password")=="fixture-password","Browsing changed original port/credential");
  Set(rdp,"DisplayName","Edited name");Set(rdp,"Address","127.0.0.2");Set(vm,"SelectedProtocol",Protocol("FTP"));Need(Auto()&&written.Count==1&&(string)Get(written.Last(),"Protocol")=="RDP"&&(string)Get(written.Last(),"Address")=="127.0.0.2","Original ordinary fields did not autosave while previewing");
  var ftp=Get(vm,"Server");Set(ftp,"Address","");Need(Auto()&&written.Count==1,"Invalid preview blocked original save or wrote data");Set(ftp,"Address","127.0.0.3");
  Console.WriteLine("PASS: eight other protocol previews never persist; return restores exact RDP object/port/credential; ordinary name/address edits autosave as RDP even during invalid preview.");
  var windowType=asm.GetType("_1RM.View.Editor.ServerEditorWindowView")!;var ctor=windowType.GetConstructor(M,null,new[]{typeof(FrameworkElement),typeof(bool),typeof(Func<Task<bool>>),typeof(Func<bool>)},null)!;
  Window Window(){var w=(Window)ctor.Invoke(new object[]{new Grid(),true,(Func<Task<bool>>)(()=> (Task<bool>)Call(vm,"SaveExistingAsync")),(Func<bool>)(()=> (bool)Get(vm,"IsSaving"))});Field(w,"_model",vm);return w;}
  Call(vm,"SetNotifyPropertyChangedEnabled",true); // Restore the base constructor default for this uninitialized fixture.
  var cancel=Window();cancel.DataContext=vm;
  var themeType=asm.GetType("_1RM.Service.ThemeService")!;var theme=Activator.CreateInstance(themeType,app.Resources,Activator.CreateInstance(asm.GetType("_1RM.Service.ThemeConfig")!))!;var themes=(IDictionary)Get(theme,"Themes");
  foreach(var palette in new[]{"Light","Dark"}) foreach(var language in new[]{"zh-cn","zh-tw","en-us"}) foreach(var width in new[]{840,940}) {
   themeType.GetMethod("ApplyTheme")!.Invoke(theme,new[]{themes[palette]});var dictionary=new ResourceDictionary{Source=new Uri("/1Remote;component/Resources/Languages/"+language+".xaml",UriKind.Relative)};app.Resources.MergedDictionaries.Add(dictionary);Call(vm,"NotifyProtocolPreview");
   var root=(FrameworkElement)cancel.Content;var body=(FrameworkElement)cancel.FindName("EditorBody");var footer=(Border)cancel.FindName("SaveStatusFooter");var banner=(Border)cancel.FindName("ProtocolPreviewBanner");var grid=(Grid)banner.Child;var buttons=grid.Children.OfType<Button>().ToArray();var label=(TextBlock)cancel.FindName("SaveStatusText");
   void Layout(){app.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);root.Measure(new Size(width,600));root.Arrange(new Rect(0,0,width,600));root.UpdateLayout();}
   Call(vm,"ReturnToSavedProtocol");Layout();var bodyTop=body.TranslatePoint(new Point(),root).Y;var bodyHeight=body.ActualHeight;var footerTop=footer.TranslatePoint(new Point(),root).Y;
   Need(banner.Visibility==Visibility.Collapsed&&footer.ActualHeight==64,"Current protocol must retain the fixed footer without actions");
   foreach(var name in protocolNames.Skip(1)) {
    Set(vm,"SelectedProtocol",Protocol(name));Layout();
    Need(body.TranslatePoint(new Point(),root).Y==bodyTop&&body.ActualHeight==bodyHeight&&footer.TranslatePoint(new Point(),root).Y==footerTop&&footer.ActualHeight==64,"Protocol switch moved the editor: "+name);
    Need(banner.Visibility==Visibility.Visible&&label.ActualWidth>100&&buttons.All(b=>b.ActualHeight>=40),$"Preview controls do not fit {palette}/{language}/{width}/{name}: visibility={banner.Visibility}, textWidth={label.ActualWidth}, buttonHeights={string.Join(",",buttons.Select(b=>b.ActualHeight))}");Need(label.TranslatePoint(new Point(label.ActualWidth,0),footer).X<=banner.TranslatePoint(new Point(),footer).X,"Preview text overlaps return action");Need(buttons[1].TranslatePoint(new Point(buttons[1].ActualWidth,0),footer).X<=footer.ActualWidth-24+.1,"Confirmation action exceeds footer");
    Need(label.Text.Contains("RDP")&&label.Text.Contains((string)Get(Get(vm,"Server"),"ProtocolDisplayName"))&&buttons[0].Content.ToString()!.Contains("RDP"),"Footer lacks current/viewed protocol names");
   }
   Set(vm,"SaveStatus",(string)app.FindResource("Editor.SaveFailed"));Layout();Need(label.Text==(string)Get(vm,"SaveStatus"),"Save error hidden by protocol preview");
   Call(vm,"ReturnToSavedProtocol");Layout();Need(body.ActualHeight==bodyHeight&&banner.Visibility==Visibility.Collapsed&&footer.ActualHeight==64,"Returning to current protocol moved the editor");Set(vm,"SelectedProtocol",Protocol("FTP"));app.Resources.MergedDictionaries.Remove(dictionary);
  }
  Call(vm,"NotifyProtocolPreview");Console.WriteLine("PASS: fixed 64 DIP footer and editor bounds remain stable across nine protocols, 840/940 DIP widths, zh-cn/zh-tw/en-us and Light/Dark; actions retain 40 DIP targets and save errors remain visible.");
  Need(!Done((Task<bool>)Call(cancel,"RequestProtocolChangeAsync",(Func<string,bool>)(prompt=>{Need(prompt.Contains("RDP")&&prompt.Contains("FTP"),"Confirmation lacks old/new protocols");return false;})))&&written.Count==1,"Cancel wrote conversion");
  Need(Done((Task<bool>)Call(cancel,"RequestCloseAsync"))&&written.Count==1,"Closing preview persisted protocol");
  var confirm=Window();fail=true;Need(!Done((Task<bool>)Call(confirm,"RequestProtocolChangeAsync",(Func<string,bool>)(_=>true)))&&(bool)Get(vm,"IsProtocolPreview")&&written.Count==1,"Failed conversion changed committed state");
  fail=false;Need(Done((Task<bool>)Call(confirm,"RequestProtocolChangeAsync",(Func<string,bool>)(_=>true)))&&!(bool)Get(vm,"IsProtocolPreview")&&written.Count==2&&(string)Get(written.Last(),"Protocol")=="FTP","Confirmed conversion not persisted");
  Set(ftp,"Address","127.0.0.4");Need(Auto()&&written.Count==3&&(string)Get(written.Last(),"Protocol")=="FTP","Ordinary edits stopped autosaving after conversion");
  Set(vm,"SelectedProtocol",Protocol("RDP"));Need((bool)Get(vm,"IsProtocolPreview")&&Auto()&&written.Count==3,"Returning to prior protocol silently reverted conversion");Need(Done((Task<bool>)Call(confirm,"RequestCloseAsync")),"Preview close failed");
  Console.WriteLine("PASS: actual editor confirmation entry: No/close do not convert, failure stays preview and can retry, Yes converts once, new-protocol fields continue autosaving, old protocol requires another confirmation.");
  var initial=New("RDP");Set(initial,"Id","queue-fixture");Set(initial,"DisplayName","Queue");var replacement=New("FTP");Set(replacement,"Id","queue-fixture");Set(replacement,"DisplayName","Queue");
  int queueWrites=0;using var release=new ManualResetEventSlim(false);using var started=new ManualResetEventSlim(false);
  var serial=Session(initial,_=>{if(Interlocked.Increment(ref queueWrites)==1){started.Set();Need(release.Wait(5000),"Queue release timeout");}return Success();});
  Need(!Succeeded(Save(serial,"SaveAsync",replacement))&&queueWrites==0,"Persistence layer accepted unconfirmed protocol");
  Set(initial,"DisplayName","First write");var first=Call(serial,"SaveAsync",initial,(Func<bool>)(()=>true));Need(started.Wait(1000),"First write never started");var conversion=Call(serial,"ConfirmProtocolChangeAsync",replacement,(Func<bool>)(()=>true));Set(initial,"DisplayName","Stale old protocol");var stale=Call(serial,"SaveAsync",initial,(Func<bool>)(()=>true));release.Set();Need(Succeeded(DoneResult(first))&&Succeeded(DoneResult(conversion))&&!Succeeded(DoneResult(stale))&&queueWrites==2,"Queued old write reverted confirmed protocol");
  Console.WriteLine("PASS: persistence layer rejects unconfirmed changes and queued old-protocol writes after confirmation; ordered saves preserve final protocol.");
  getter.SetValue(null,originalGetter);
  Console.WriteLine("PASS: isolated fixtures only; no real database, credentials, connections or visible windows.");
 }
}
