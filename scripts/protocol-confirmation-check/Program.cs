using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Windows;
class Program {
 [STAThread] static void Main(string[] args) {
  var bin=Path.GetFullPath(args[0]);
  AssemblyLoadContext.Default.Resolving+=(_,name)=>File.Exists(Path.Combine(bin,name.Name+".dll"))?AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(bin,name.Name+".dll")):null;
  var asm=Assembly.LoadFrom(Path.Combine(bin,"1Remote.dll"));var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
  foreach(var uri in new[]{"/Shawn.Utils.WpfResources;component/Converter/Converter.xaml","/Shawn.Utils.WpfResources;component/Theme/Basic/Default.xaml","/Shawn.Utils.WpfResources;component/Theme/DefaultTheme.xaml","/1Remote;component/Resources/Converter/Converter.xaml","/1Remote;component/Resources/Icons/SVG.xaml","/1Remote;component/Resources/Theme/EditorForm.xaml","/1Remote;component/Resources/Languages/en-us.xaml"}) app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri(uri,UriKind.Relative)});
  if(args.Length>1&&args[1]=="entries")AutoSaveEntryChecks.Run(asm,app);
  else ProtocolConfirmationChecks.Run(asm,app);
 }
}
