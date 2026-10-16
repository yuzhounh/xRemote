using System;
using System.Windows;
using _1RM.Service;
using _1RM.Service.Locality;

namespace _1RM.View.ServerView;

public partial class ServerListPageViewModel
{
    public bool ShowStatusColumn
    {
        get => LocalityListViewService.Settings.ShowStatusColumn;
        set { if (ShowStatusColumn == value) return; LocalityListViewService.Settings.ShowStatusColumn = value;
            ColumnsChanged(nameof(ShowStatusColumn), nameof(StatusColumnVisibility), nameof(StatusWidth)); }
    }
    public bool ShowAddressColumn
    {
        get => LocalityListViewService.Settings.ShowAddressColumn;
        set { if (ShowAddressColumn == value) return; LocalityListViewService.Settings.ShowAddressColumn = value;
            ColumnsChanged(nameof(ShowAddressColumn), nameof(AddressColumnVisibility), nameof(AddressWidth)); }
    }
    public bool ShowProtocolColumn
    {
        get => LocalityListViewService.Settings.ShowProtocolColumn;
        set { if (ShowProtocolColumn == value) return; LocalityListViewService.Settings.ShowProtocolColumn = value;
            ColumnsChanged(nameof(ShowProtocolColumn), nameof(ProtocolColumnVisibility), nameof(ProtocolWidth)); }
    }
    public bool ShowTagsColumn
    {
        get => LocalityListViewService.Settings.ShowTagsColumn;
        set { if (ShowTagsColumn == value) return; LocalityListViewService.Settings.ShowTagsColumn = value;
            ColumnsChanged(nameof(ShowTagsColumn), nameof(TagsColumnVisibility), nameof(TagsWidth)); }
    }
    public bool ShowNoteColumn
    {
        get => LocalityListViewService.Settings.ShowNoteColumn
            ?? IoC.Get<ConfigurationService>().General.ShowNoteFieldInListView;
        set { if (ShowNoteColumn == value) return; LocalityListViewService.Settings.ShowNoteColumn = value;
            BriefNoteVisibility = value ? Visibility.Visible : Visibility.Collapsed;
            ColumnsChanged(nameof(ShowNoteColumn)); }
    }
    public Visibility StatusColumnVisibility => ShowStatusColumn ? Visibility.Visible : Visibility.Collapsed;
    public Visibility AddressColumnVisibility => ShowAddressColumn ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ProtocolColumnVisibility => ShowProtocolColumn ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TagsColumnVisibility => ShowTagsColumn ? Visibility.Visible : Visibility.Collapsed;
    public double TagsWidth
    {
        get => ShowTagsColumn ? LocalityListViewService.Settings.ServerListTagsWidth : 0;
        set { if (!ShowTagsColumn || !double.IsFinite(value)) return;
            LocalityListViewService.Settings.ServerListTagsWidth = Math.Clamp(value, 80, 1000); RaisePropertyChanged(); }
    }
    private void ColumnsChanged(params string[] properties)
    {
        foreach (var property in properties) RaisePropertyChanged(property);
        LocalityListViewService.Save();
    }
    public void ResetColumns()
    {
        // Restore visibility only; keep the user's carefully adjusted widths.
        ShowStatusColumn = ShowAddressColumn = ShowProtocolColumn = true;
        ShowTagsColumn = ShowNoteColumn = false;
    }
}
