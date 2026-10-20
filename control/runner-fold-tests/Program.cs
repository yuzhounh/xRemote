using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

class Program
{
    [STAThread] static void Main(string[] args)
    {
        string bin = Path.GetFullPath(args[0]);
        AssemblyLoadContext.Default.Resolving += (_, n) => File.Exists(Path.Combine(bin, n.Name + ".dll"))
            ? AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(bin, n.Name + ".dll")) : null;
        var assembly = Assembly.LoadFrom(Path.Combine(bin, "1Remote.dll"));
        var app = new Application(); int passed = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS: " + message); passed++; }
        foreach (string uri in new[] { "/Shawn.Utils.WpfResources;component/Converter/Converter.xaml", "/Shawn.Utils.WpfResources;component/Theme/Basic/Default.xaml", "/Shawn.Utils.WpfResources;component/Theme/DefaultTheme.xaml", "/1Remote;component/Resources/Converter/Converter.xaml", "/1Remote;component/Resources/Icons/SVG.xaml", "/1Remote;component/Resources/Theme/SettingsTextFallback.xaml", "/1Remote;component/Resources/Languages/zh-cn.xaml" })
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(uri, UriKind.Relative) });
        var model = new Choices();
        var first = new Choice("UltraVNC"); var second = new Choice("TightVNC"); var third = new Choice("Custom program");
        model.Runners.Add(first); model.Runners.Add(second); model.Runners.Add(third); model.SelectedRunner = second;
        var view = (UserControl)Activator.CreateInstance(assembly.GetType("_1RM.View.Settings.ProtocolConfig.ProtocolRunnerSettingsPageView")!)!;
        view.DataContext = model;
        var list = (ListBox)view.FindName("RunnersList"); VirtualizingStackPanel.SetIsVirtualizing(list, false);
        Layout(view);
        Expander[] Sections() => Desc(list).OfType<Expander>().Where(e => e.GetType().Name == "RunnerSettingsExpander").ToArray();
        object? Runner(Expander e) => e.GetType().GetProperty("Runner")!.GetValue(e);
        var sections = Sections();
        Check(sections.Length == 3, "each program retains a heading and settings section");
        Check(sections.Count(e => e.IsExpanded) == 1 && ReferenceEquals(sections.Single(e => e.IsExpanded).DataContext, second), "only configured program expands initially even when it is not first");
        Check(sections.All(e => ReferenceEquals(((ContentPresenter)e.Content).Content, Runner(e))), "each section still presents the original runner settings object");
        var inactive = sections.Single(e => ReferenceEquals(Runner(e), first)); inactive.IsExpanded = true; Layout(view);
        Check(inactive.IsExpanded && model.SelectedRunner == second, "manually opening another program does not select it");
        model.SelectedRunner = third; Layout(view);
        Check(sections.Count(e => e.IsExpanded) == 1 && ReferenceEquals(sections.Single(e => e.IsExpanded).DataContext, third), "changing program closes old and manually opened sections and opens the new one");
        foreach (var section in sections)
        {
            var body = (ContentPresenter)section.Template.FindName("SettingsContent", section);
            Check(body.Visibility == (section.IsExpanded ? Visibility.Visible : Visibility.Collapsed), "content visibility follows expansion for " + ((Choice)section.DataContext).Name);
        }
        var active = sections.Single(e => e.IsExpanded); active.IsExpanded = false; Layout(view);
        Check(!active.IsExpanded && model.SelectedRunner == third, "manual collapse leaves configured program unchanged");
        active.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent)); Layout(view);
        Check(active.IsExpanded, "opening settings again restores selected-only defaults");
        model.Runners.Clear(); var next = new Choice("Next protocol"); model.Runners.Add(next); model.SelectedRunner = next; Layout(view);
        Check(Sections().Length == 1 && Sections()[0].IsExpanded, "switching protocol resets expansion for its own program list");
        model.SelectedRunner = null; Layout(view);
        Check(Sections().All(e => !e.IsExpanded), "no selection does not accidentally expand an unrelated program");
        app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/1Remote;component/View/Settings/ProtocolConfig/ProtocolConfiogDictionary.xaml",UriKind.Relative)});
        var xaml=System.Xml.Linq.XDocument.Load("Ui/App.xaml");
        var key=(System.Xml.Linq.XNamespace)"http://schemas.microsoft.com/winfx/2006/xaml";
        var headingStyle=xaml.Descendants().Single(e=>e.Attribute(key+"Key")?.Value=="BlockTitleColumn");
        app.Resources["BlockTitleColumn"]=System.Windows.Markup.XamlReader.Parse(headingStyle.ToString());
        foreach(string uri in new[]{"/1Remote;component/Resources/Theme/SettingsForm.xaml","/1Remote;component/Resources/Theme/EditorForm.xaml"})
            app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri(uri,UriKind.Relative)});
        var themeType=assembly.GetType("_1RM.Service.ThemeService")!;
        var theme=Activator.CreateInstance(themeType,new[]{app.Resources,Activator.CreateInstance(assembly.GetType("_1RM.Service.ThemeConfig")!)!})!;
        var presets=(System.Collections.IDictionary)themeType.GetProperty("Themes")!.GetValue(theme)!;
        var editor=new Expander{Header="更多连接选项",Style=(Style)app.FindResource("EditorExpanderStyle"),Content=new TextBlock{Text="settings"}};
        var colors=new Expander{Style=(Style)app.FindResource("SettingsColorExpanderStyle"),Content=new TextBlock{Text="colors"}};
        var root=new StackPanel();root.Children.Add(view);root.Children.Add(editor);root.Children.Add(colors);
        var window=new Window{Content=root}; // Logical resource updates only; never shown.
        foreach(string preset in new[]{"Light","Dark"})
        {
            themeType.GetMethod("ApplyTheme")!.Invoke(theme,new[]{presets[preset]});Layout(root);
            Color ColorOf(string resource)=>((SolidColorBrush)app.FindResource(resource)).Color;
            var surfaceBrush=(SolidColorBrush)app.FindResource("ProtocolSettingsSurfaceBrush");
            Check(surfaceBrush.Opacity==1&&surfaceBrush.Color==ColorOf("BackgroundBrush"),preset+" protocol page uses the same opaque background as other settings");
            var cardTemplate=(DataTemplate)view.FindResource(new DataTemplateKey(assembly.GetType("_1RM.Model.ProtocolRunner.Default.InternalDefaultRunner")!));
            var card=(FrameworkElement)cardTemplate.LoadContent();root.Children.Add(card);Layout(root);
            var cardSurface=Desc(card).OfType<Border>().Single(b=>b.Name=="Border1");
            Check(((SolidColorBrush)cardSurface.Background).Color==(preset=="Dark"?Color.FromRgb(37,37,37):ColorOf("BackgroundBrush"))
                &&cardSurface.BorderThickness==new Thickness(preset=="Dark"?1:0),preset+" real built-in card has the expected surface and thin border");
            Check(ColorOf("ProtocolNavSelectedBrush")== (preset=="Dark"?Color.FromRgb(42,42,42):Color.FromRgb(240,240,240))
                &&ColorOf("PrimaryMidBrush")== (preset=="Dark"?Color.FromRgb(50,50,51):Color.FromRgb(245,245,245)),preset+" protocol feedback is independent of the unchanged first sidebar");
            root.Children.Remove(card);
            var action=new Button{Style=(Style)app.FindResource("FormActionButtonStyle"),Content="选择"};
            var input=new TextBox{Tag="占位提示",Width=300};
            var inlineAdd=new Button{Style=(Style)app.FindResource("FormInlineAddButtonStyle")};
            var editorTitle=new TextBlock{Style=(Style)app.FindResource("EditorGroupTextBlockTitle"),Text="连接"};
            var settingsTitle=new TextBlock{Style=(Style)app.FindResource("SettingsFormHeadingStyle"),Text="连接"};
            foreach(var element in new FrameworkElement[]{action,input,inlineAdd,editorTitle,settingsTitle})root.Children.Add(element);Layout(root);
            Check(action.ActualHeight==input.ActualHeight&&action.Padding==new Thickness(10,4,10,4),preset+" action button height matches a normal input");
            Check(inlineAdd.ActualWidth==40&&inlineAdd.ActualHeight==40&&Desc(inlineAdd).OfType<System.Windows.Shapes.Path>().Single().StrokeThickness==1.5,preset+" inline add buttons share the vector and 40 DIP target");
            Check(editorTitle.FontSize==settingsTitle.FontSize&&editorTitle.FontWeight==FontWeights.SemiBold&&settingsTitle.FontWeight==FontWeights.SemiBold&&editorTitle.Margin.Top==settingsTitle.Margin.Top&&editorTitle.Margin.Bottom==settingsTitle.Margin.Bottom,preset+" editor and settings group headings share typography and vertical spacing");
            var hint=Desc(input).OfType<TextBlock>().Single(t=>t.Text=="占位提示");
            Check(hint.Opacity==1&&Contrast(((SolidColorBrush)hint.Foreground).Color,ColorOf("BackgroundBrush"))>=4.5,preset+" input placeholder is opaque and readable");
            Check(Contrast(ColorOf("SecondaryTextBrush"),ColorOf("BackgroundBrush"))>=4.5,preset+" secondary text remains readable without disabled opacity");
            input.Text="已有内容";Layout(root);
            Check(hint.Visibility!=Visibility.Visible||hint.Foreground is SolidColorBrush hidden&&hidden.Color.A==0,preset+" placeholder disappears when typing");
            var addRunner=Desc(view).OfType<Button>().Single(b=>System.Windows.Data.BindingOperations.GetBinding(b,Button.CommandProperty)?.Path?.Path=="CmdAddRunner");
            Check(addRunner.ActualHeight==40&&((FrameworkElement)view.FindName("DefaultRunnerRow")).ActualHeight>=40,preset+" program add button fits the pinned settings row");
            var tags=(UserControl)Activator.CreateInstance(assembly.GetType("_1RM.Controls.TagsEditor")!)!;tags.Width=420;root.Children.Add(tags);Layout(root);
            var tagInput=(FrameworkElement)tags.FindName("TbNewTag");var tagAdd=Desc(tags).OfType<Button>().Single();
            Check(tagInput.ActualHeight==input.ActualHeight&&tagAdd.ActualHeight==40&&tagAdd.TranslatePoint(new Point(tagAdd.ActualWidth,0),tags).X<=tags.ActualWidth+.1,preset+" tag input keeps normal height and the add target stays inside the form");
            root.Children.Remove(tags);
            foreach(var element in new FrameworkElement[]{action,input,inlineAdd,editorTitle,settingsTitle})root.Children.Remove(element);
            foreach(var section in new[]{Sections().Single(),editor,colors})
            {
                var toggle=(System.Windows.Controls.Primitives.ToggleButton)section.Template.FindName("HeaderToggle",section);
                var arrow=(System.Windows.Shapes.Path)toggle.Template.FindName("Chevron",toggle);
                Check(toggle.ActualHeight==40&&toggle.Padding==new Thickness(8)&&toggle.FontSize==(double)app.FindResource("GlobalFontSizeBody")&&toggle.FontWeight==FontWeights.Normal,preset+" shared title size, 40 DIP target and 8 DIP padding: "+section.GetType().Name);
                section.IsExpanded=false;Layout(root);
                Check(arrow.Width==4&&arrow.Height==8&&arrow.StrokeThickness==1.5&&arrow.Data.ToString()=="M0,0L4,4 0,8"&&(arrow.RenderTransform is not RotateTransform rotation||rotation.Angle==0),preset+" collapsed heading uses the shared right-facing arrow");
                toggle.IsChecked=true;Layout(root);
                Check(section.IsExpanded&&arrow.RenderTransform is RotateTransform open&&open.Angle==90,preset+" header toggle expands content and rotates arrow down");
            }
        }
        themeType.GetMethod("ApplyTheme")!.Invoke(theme,new[]{presets["PRemoteM"]});Layout(root);
        Check(((SolidColorBrush)app.FindResource("ProtocolSettingsSurfaceBrush")).Opacity==.25
            &&(Thickness)app.FindResource("ProtocolCardBorderThickness")==new Thickness(0),"custom theme keeps its previous surface and border behavior");
        var surfaceFiles=new[]{"PuttyRunnerSettings.xaml","KittyRunnerSettings.xaml","ExternalRunnerSettings.xaml","ExternalSshRunnerSettings.xaml","ProtocolRunnerSettingsPageView.xaml"};
        Check(surfaceFiles.All(name=>System.Xml.Linq.XDocument.Load("Ui/View/Settings/ProtocolConfig/"+name).Descendants().Single(e=>e.Name.LocalName=="Border"&&(e.Attribute("Name")?.Value=="Border1"||e.Attribute(key+"Name")?.Value=="Border1")).Attribute("Style")?.Value=="{StaticResource ProtocolCardSurfaceStyle}"),"all five runner templates use the shared card surface");
        var pageSource=System.Xml.Linq.XDocument.Load("Ui/View/Editor/ServerEditorPageView.xaml");
        var inlineStyle=pageSource.Descendants().Single(e=>e.Name.LocalName=="Expander"&&e.Attribute("Header")?.Value=="{DynamicResource Editor.ConnectionAdvanced}")
            .Elements().Single(e=>e.Name.LocalName=="Expander.Style").Elements().Single();
        foreach(bool configured in new[]{false,true})
        {
            var more=new Expander{Style=(Style)System.Windows.Markup.XamlReader.Parse(inlineStyle.ToString()),
                DataContext=new{Server=new{CommandBeforeConnected=configured?"configured.cmd":"",CommandAfterDisconnected="",SelectedRunnerName=""}}};
            root.Children.Add(more);Layout(root);
            Check(more.IsExpanded==configured,"more connection options preserve the default for "+(configured?"configured":"empty")+" scripts");
        }
        window.Close();
        Console.WriteLine($"{passed} runner folding checks passed; no real configuration, credentials or connections accessed."); app.Shutdown();
    }
    static void Layout(FrameworkElement view)
    {
        view.Measure(new Size(850, 650)); view.Arrange(new Rect(0, 0, 850, 650)); view.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
    }
    static IEnumerable<DependencyObject> Desc(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { var child = VisualTreeHelper.GetChild(parent, i); yield return child; foreach (var nested in Desc(child)) yield return nested; }
    }
    public record Choice(string Name);
    static double Contrast(Color a,Color b)
    {
        static double L(Color c){static double S(byte v){double x=v/255.0;return x<=.04045?x/12.92:Math.Pow((x+.055)/1.055,2.4);}return .2126*S(c.R)+.7152*S(c.G)+.0722*S(c.B);}
        double x=L(a),y=L(b);return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);
    }
    public class Choices : INotifyPropertyChanged
    {
        public ObservableCollection<Choice> Runners { get; } = new();
        public string[] Protocols { get; } = new[] { "VNC" };
        public string SelectedProtocol { get; set; } = "VNC";
        Choice? selected;
        public Choice? SelectedRunner { get => selected; set { selected = value; PropertyChanged?.Invoke(this, new(nameof(SelectedRunner))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
