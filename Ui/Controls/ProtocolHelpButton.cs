using System;
using System.Windows;
using System.Windows.Controls;
using Shawn.Utils.Wpf.Controls;

namespace _1RM.Controls;

// A real button keeps vector help icons clickable and keyboard accessible.
// Parameter information uses Command; documentation keeps the existing browser opener.
public sealed class ProtocolHelpButton : Button
{
    public static readonly DependencyProperty NavigateUriProperty = DependencyProperty.Register(
        nameof(NavigateUri), typeof(Uri), typeof(ProtocolHelpButton), new PropertyMetadata(null));

    public Uri? NavigateUri
    {
        get => (Uri?)GetValue(NavigateUriProperty);
        set => SetValue(NavigateUriProperty, value);
    }

    protected override void OnClick()
    {
        base.OnClick();
        if (NavigateUri is { IsAbsoluteUri: true } uri) HyperlinkHelper.OpenUriBySystem(uri.AbsoluteUri);
    }
}
