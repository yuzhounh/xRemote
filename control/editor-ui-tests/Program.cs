using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

class Program
{
    [STAThread] static void Main(string[] args)
    {
        string bin=Path.GetFullPath(args[0]);
        AssemblyLoadContext.Default.Resolving+=(_,n)=>File.Exists(Path.Combine(bin,n.Name+".dll"))?AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(bin,n.Name+".dll")):null;
        var assembly=Assembly.LoadFrom(Path.Combine(bin,"1Remote.dll")); var app=new Application(); int passed=0;
        void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS: "+label);passed++;}
        foreach(string uri in new[]{"/Shawn.Utils.WpfResources;component/Converter/Converter.xaml","/Shawn.Utils.WpfResources;component/Theme/Basic/Default.xaml","/Shawn.Utils.WpfResources;component/Theme/DefaultTheme.xaml","/1Remote;component/Resources/Converter/Converter.xaml","/1Remote;component/Resources/Icons/SVG.xaml","/1Remote;component/Resources/Theme/SettingsTextFallback.xaml","/1Remote;component/Resources/Languages/en-us.xaml","/1Remote;component/Resources/Languages/zh-cn.xaml","/1Remote;component/Resources/Theme/EditorForm.xaml","/1Remote;component/Resources/Theme/CompactMainMenu.xaml"})
            app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri(uri,UriKind.Relative)});
        var themeType=assembly.GetType("_1RM.Service.ThemeService")!;
        var config=Activator.CreateInstance(assembly.GetType("_1RM.Service.ThemeConfig")!)!;
        var theme=Activator.CreateInstance(themeType,new object[]{app.Resources,config})!;
        var presets=(System.Collections.IDictionary)themeType.GetProperty("Themes")!.GetValue(theme)!;
        var caption=(FrameworkElement)Activator.CreateInstance(assembly.GetType("_1RM.Controls.WindowCaptionTitle")!)!;
        caption.GetType().GetProperty("Text")!.SetValue(caption,"编辑连接 · 布局检查"); Layout(caption,500);
        Check(Desc(caption).Any(c=>c.GetType().Name=="AppLogo")&&Desc(caption).OfType<TextBlock>().Single().FontSize==16,"shared vector caption logo and 16 DIP typography");
        var compact=new MenuItem{Header="搜索",InputGestureText="Ctrl+F",Style=(Style)app.FindResource("CompactMainMenuItemStyle")}; Layout(compact,250);
        var header=Desc(compact).OfType<ContentPresenter>().First(c=>Equals(c.Content,"搜索"));
        var menuSurface=(Border)compact.Template.FindName("Surface",compact);
        Check(compact.Padding.Left==10&&Math.Abs(header.TranslatePoint(new Point(),compact).X-compact.Padding.Left-menuSurface.BorderThickness.Left)<0.1,"top-level menu keeps compact text inset inside its focus border");
        Check(compact.Template.FindName("PART_Popup",compact)!=null&&compact.Template.FindName("Arrow",compact)!=null,"compact menu preserves submenu host and navigation arrow");
        var host=(UserControl)Activator.CreateInstance(assembly.GetType("_1RM.View.Editor.Forms.Utils.HostView")!)!;
        if(host.Content==null)host.GetType().GetMethod("InitializeComponent")!.Invoke(host,null);
        host.DataContext=new{New=new AddressModel()};Layout(host,780);
        var address=Desc(host).OfType<TextBox>().Single(c=>BindingOperations.GetBinding(c,TextBox.TextProperty)?.Path?.Path=="New.Address");
        var port=Desc(host).OfType<TextBox>().Single(c=>BindingOperations.GetBinding(c,TextBox.TextProperty)?.Path?.Path=="New.Port");
        Check(Math.Abs(port.TranslatePoint(new Point(port.ActualWidth,0),host).X-576)<0.1&&address.ActualWidth>250,"address and port fit shared 420 DIP input region");
        foreach(string name in new[]{"RdpFormView","RdpAppFormView","VncFormView","SshFormView","TelnetFormView","SerialFormView","SftpFormView","FtpFormView","LocalAppFormView"})
        {
            var control=(UserControl)Activator.CreateInstance(assembly.GetType("_1RM.View.Editor.Forms."+name)!)!;
            if(control.Content==null)control.GetType().GetMethod("InitializeComponent")!.Invoke(control,null);
            Layout(control,780);
            Check(control.Content!=null,name+" compiled view constructs");
        }
        var validation=assembly.GetType("_1RM.Controls.EditorValidation")!;
        var xml=System.Xml.Linq.XDocument.Load(Path.Combine(Environment.CurrentDirectory,"Ui/View/Settings/ProtocolConfig/ExternalRunnerSettings.xaml"));
        var sourceField=new System.Xml.Linq.XElement(xml.Descendants().Single(e=>e.Name.LocalName=="TextBox"&&e.Attribute("Text")?.Value.StartsWith("{Binding ExternalRunner.ExePath") == true));
        sourceField.SetAttributeValue(System.Xml.Linq.XNamespace.Xmlns+"layout","clr-namespace:_1RM.Controls;assembly=1Remote");
        var program=(TextBox)System.Windows.Markup.XamlReader.Parse(sourceField.ToString());
        program.DataContext=new{ExternalRunner=new ProgramPathModel()};Layout(program,420);
        Check(program.BorderBrush is not SolidColorBrush beforeBrush||beforeBrush.Color!=Colors.Red,"unused missing external program is not marked red on initial display");
        validation.GetMethod("SetTouched")!.Invoke(null,new object[]{program,true});Layout(program,420);
        Check(program.BorderBrush is SolidColorBrush afterBrush&&afterBrush.Color==((SolidColorBrush)app.FindResource("ValidationErrorBrush")).Color&&program.BorderThickness.Left==1,"edited missing program uses the common thin error outline");
        var scope=new StackPanel(); validation.GetMethod("SetEnabled")!.Invoke(null,new object[]{scope,true});
        var rightLabels=new Style(typeof(TextBlock));rightLabels.Setters.Add(new Setter(TextBlock.TextAlignmentProperty,TextAlignment.Right));rightLabels.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Right));scope.Resources[typeof(TextBlock)]=rightLabels;
        var row=new StackPanel{Orientation=Orientation.Horizontal};scope.Children.Add(row);
        row.Children.Add(new TextBlock{Text="程序路径"});
        var field=new TextBox{Width=300,Height=30};row.Children.Add(field);
        field.SetBinding(TextBox.TextProperty,new Binding(nameof(ValueModel.Value)){Source=new ValueModel(),Mode=BindingMode.TwoWay,ValidatesOnDataErrors=true});
        var expression=field.GetBindingExpression(TextBox.TextProperty)!;
        Validation.MarkInvalid(expression,new ValidationError(new ExceptionValidationRule(),expression,"不能为空！",null));
        field.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(()=>{}));
        Check(Validation.GetErrorTemplate(field)==null,"untouched required field has no legacy red overlay");
        validation.GetMethod("RevealErrors")!.Invoke(null,new object[]{scope});Layout(scope,700);
        var error=Desc(scope).OfType<TextBlock>().Single(t=>t.Text=="请输入程序路径");
        Check(error.TextAlignment==TextAlignment.Left&&error.HorizontalAlignment==HorizontalAlignment.Stretch,"inline errors stay left-aligned even inside right-aligned dialog label styles");
        Check(error.IsVisible==false||error.Visibility==Visibility.Visible,"specific required-field message is available after validation");
        var parent=(Grid)field.Parent;
        Check(Grid.GetRow(error)==1&&error.TranslatePoint(new Point(),parent).Y>=field.ActualHeight,"error occupies an independent layout row below input");
        Check(Validation.GetHasError(field),"presentation changes preserve actual validation failure");
        // Register a logical window so WPF propagates application resource changes; never show it.
        var validationWindow=new Window{Content=scope};
        void Apply(string name){themeType.GetMethod("ApplyTheme")!.Invoke(theme,new[]{presets[name]});Layout(scope,700);}
        Color BrushColor(object value)=>((SolidColorBrush)value).Color;
        Apply("Dark");
        var darkBackground=BrushColor(app.FindResource("BackgroundBrush"));
        Check(BrushColor(error.Foreground)==Color.FromRgb(255,138,128),"dark validation text uses readable light red");
        Check(Contrast(BrushColor(error.Foreground),darkBackground)>=4.5,"dark validation text contrast meets 4.5 to 1");
        Check(Contrast(BrushColor(app.FindResource("ReadableAccentTextBrush")),darkBackground)>=4.5,"dark help and notice text contrast meets 4.5 to 1");
        var protocolPage=(UserControl)Activator.CreateInstance(assembly.GetType("_1RM.View.Settings.ProtocolConfig.ProtocolRunnerSettingsPageView")!)!;
        var noOptions=(FrameworkElement)((DataTemplate)protocolPage.FindResource(new DataTemplateKey(assembly.GetType("_1RM.Model.ProtocolRunner.Default.InternalDefaultRunner")!))).LoadContent();Layout(noOptions,600);
        Check(Contrast(BrushColor(Desc(noOptions).OfType<TextBlock>().Single().Foreground),darkBackground)>=4.5,"built-in program description stays readable on a dark surface");
        var rdp=(UserControl)Activator.CreateInstance(assembly.GetType("_1RM.View.Editor.Forms.RdpFormView")!)!;
        if(rdp.Content==null)rdp.GetType().GetMethod("InitializeComponent")!.Invoke(rdp,null);Layout(rdp,780);
        Check(BrushColor(Desc(rdp).OfType<Expander>().Single().Foreground)==BrushColor(app.FindResource("BackgroundTextBrush")),"RDP help expander follows the text color of the theme");
        Apply("Light");
        Check(BrushColor(error.Foreground)==Color.FromRgb(178,34,34),"existing error text updates when restoring the light theme");
        Check(Contrast(BrushColor(error.Foreground),BrushColor(app.FindResource("BackgroundBrush")))>=4.5,"light validation text contrast remains readable");
        validationWindow.Close();
        Console.WriteLine($"{passed} editor/menu/validation checks passed; no windows shown or connection data changed.");app.Shutdown();
    }
    static void Layout(FrameworkElement element,double width){element.Measure(new Size(width,double.PositiveInfinity));element.Arrange(new Rect(0,0,width,Math.Max(40,element.DesiredSize.Height)));element.UpdateLayout();}
    static IEnumerable<DependencyObject> Desc(DependencyObject parent){for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);yield return child;foreach(var d in Desc(child))yield return d;}}
    public class ValueModel{public string Value{get;set;}="";}
    public class AddressModel{public string Address{get;set;}="127.0.0.1";public string Port{get;set;}="22";public bool IsPingBeforeConnect{get;set;}=true;}
    public class ProgramPathModel{public string Name{get;set;}="fixture";public string ExePath{get;set;}="missing-program.exe";public bool IsExeExisted{get;set;}=false;}
    static double Contrast(Color a,Color b)
    {
        static double L(Color c){static double S(byte v){double x=v/255.0;return x<=.04045?x/12.92:Math.Pow((x+.055)/1.055,2.4);}return .2126*S(c.R)+.7152*S(c.G)+.0722*S(c.B);}
        double x=L(a),y=L(b);return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);
    }
}
