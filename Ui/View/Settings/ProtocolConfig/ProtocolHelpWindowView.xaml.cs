using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using _1RM.Model;
using _1RM.View.Utils;

namespace _1RM.View.Settings.ProtocolConfig;

public partial class ProtocolHelpWindowView : Window
{
    private readonly NativeMainTitleBar _nativeTitleBar;
    public IReadOnlyList<ProtocolHelpEntry> Entries { get; }

    public ProtocolHelpWindowView(ProtocolSettings settings)
    {
        Entries = settings.MarcoNames.Select((macro, index) => new ProtocolHelpEntry(
            settings.MarcoDescriptions.ElementAtOrDefault(index) ?? macro, macro)).ToArray();
        InitializeComponent();
        DataContext = this;
        _nativeTitleBar = new NativeMainTitleBar(this, CaptionRow, CaptionArea, CaptionReserve, Close);
        ContentRendered += (_, _) => CompactDialogSizing.Fit(this, MappingBody);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is not (Key.Escape or Key.Enter) || e.Handled) return;
        e.Handled = true;
        Close();
    }
}

public sealed record ProtocolHelpEntry(string Description, string Macro);
