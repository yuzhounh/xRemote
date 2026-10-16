using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using _1RM.Model.Protocol.Base;
using _1RM.View.Editor.Forms;

namespace _1RM.View.Editor;

public partial class ServerEditorPageViewModel
{
    private readonly bool _isExistingConnection;
    private ConnectionAutoSaveSession? _autoSaveSession;
    private int _autoSavePending;
    private bool _isSaving;
    private string _saveStatus = "";
    private long _editRevision;
    private ProtocolBase? _savedProtocolServer;
    private ProtocolBaseFormViewModel? _savedProtocolEditor;
    private Dictionary<Type, ProtocolBase>? _protocolDrafts;
    private Task<bool>? _protocolChangeTask;
    private TaskCompletionSource<bool>? _explicitSaveCompletion;

    public bool IsProtocolPreview => AutoSaveExisting && _savedProtocolServer != null
        && Server.GetType() != _savedProtocolServer.GetType();
    public bool IsConfirmingProtocolChange => _protocolChangeTask is { IsCompleted: false };
    public string ProtocolPreviewText => Service.UiText.Get("Editor.ProtocolPreview", _savedProtocolServer?.ProtocolDisplayName ?? "", Server.ProtocolDisplayName);
    public string ProtocolChangeButtonText => Service.UiText.Get("Editor.ChangeProtocolTo", Server.ProtocolDisplayName);
    public string ReturnProtocolButtonText => Service.UiText.Get("Editor.ReturnToCurrentProtocol", _savedProtocolServer?.ProtocolDisplayName ?? "");
    public string FooterStatus => IsProtocolPreview && SaveStatus == Text("Editor.ProtocolPreviewStatus") ? ProtocolPreviewText : SaveStatus;
    internal string ProtocolChangePrompt => Service.UiText.Get("Editor.ProtocolChangePrompt", Server.DisplayName,
        _savedProtocolServer?.ProtocolDisplayName ?? "", Server.ProtocolDisplayName);

    public event Action? CloseRequested;
    internal Func<bool>? AdditionalValidation { get; set; }
    internal void ReportValidationError() => SaveStatus = Text("Editor.InvalidFields");
    public bool AutoSaveExisting => _isExistingConnection && !IsBuckEdit;
    public bool RequiresExplicitSave => !AutoSaveExisting;
    internal bool HasSameTarget(ServerEditorPageViewModel other) => AutoSaveExisting && other.AutoSaveExisting
        && _orgServer.Id == other._orgServer.Id && ReferenceEquals(_orgServer.DataSource, other._orgServer.DataSource);
    public bool IsSaving
    {
        get => _isSaving;
        private set
        {
            if (SetAndNotifyIfChanged(ref _isSaving, value)) CommandManager.InvalidateRequerySuggested();
        }
    }
    public string SaveStatus
    {
        get => _saveStatus;
        private set
        {
            if (SetAndNotifyIfChanged(ref _saveStatus, value)) RaisePropertyChanged(nameof(FooterStatus));
        }
    }
    public string WindowTitle => AutoSaveExisting ? Text("Editor.EditConnection") + " · " + Server.DisplayName
        : IsBuckEdit ? Text("Editor.BulkEdit") : Text("Editor.CreateConnection");

    internal static string Text(string key) => Application.Current?.TryFindResource(key) as string ?? IoC.Translate(key);

    public void BeginEditSession()
    {
        if (!AutoSaveExisting) return;
        _savedProtocolServer ??= Server;
        _savedProtocolEditor ??= EditorViewModel;
        _protocolDrafts ??= new Dictionary<Type, ProtocolBase> { [Server.GetType()] = Server };
        if (_autoSaveSession != null) return;
        _autoSaveSession = new ConnectionAutoSaveSession(Server, _globalData.UpdateServer);
        Server.PropertyChanged += OnConnectionPropertyChanged;
        SaveStatus = Text("Editor.AutoSaveEnabled");
    }

    private bool SelectExistingProtocol(Type protocolType)
    {
        if (!AutoSaveExisting || EditorViewModel == null) return false;
        BeginEditSession();
        _protocolDrafts![Server.GetType()] = Server;
        if (_protocolDrafts.TryGetValue(protocolType, out var draft)) Server = draft;
        else
        {
            UpdateServerWhenProtocolChanged(protocolType);
            // The legacy conversion copies some mutable fields by reference. Preview drafts must be independent.
            var preview = Server;
            var independent = preview.CreateFromJsonString(preview.ToJsonString())
                ?? throw new InvalidOperationException("Could not create a protocol preview");
            independent.Id = preview.Id;
            independent.DataSource = preview.DataSource;
            Server = independent;
            _protocolDrafts[protocolType] = Server;
        }
        UpdateRunners(Server.Protocol);
        ReflectProtocolEditControl(protocolType);
        if (ReferenceEquals(Server, _savedProtocolServer)) _savedProtocolEditor = EditorViewModel;
        NotifyProtocolPreview();
        return true;
    }

    private void NotifyProtocolPreview()
    {
        RaisePropertyChanged(nameof(IsProtocolPreview));
        RaisePropertyChanged(nameof(ProtocolPreviewText));
        RaisePropertyChanged(nameof(ProtocolChangeButtonText));
        RaisePropertyChanged(nameof(ReturnProtocolButtonText));
        SaveStatus = Text(IsProtocolPreview ? "Editor.ProtocolPreviewStatus" : "Editor.AutoSaveEnabled");
        RaisePropertyChanged(nameof(FooterStatus));
    }

    internal void ReturnToSavedProtocol()
    {
        if (!IsProtocolPreview || IsConfirmingProtocolChange) return;
        SelectedProtocol = ProtocolList.Find(protocol => protocol.GetType() == _savedProtocolServer!.GetType())!;
    }

    // Only the explicit, confirmed UI action calls this entry point.
    internal Task<bool> ConfirmProtocolChangeAsync()
    {
        if (IsConfirmingProtocolChange) return _protocolChangeTask!;
        if (!IsProtocolPreview) return Task.FromResult(false);
        _protocolChangeTask = PersistProtocolChangeAsync();
        RaisePropertyChanged(nameof(IsConfirmingProtocolChange));
        return _protocolChangeTask;
    }

    private async Task<bool> PersistProtocolChangeAsync()
    {
        var candidate = Server;
        var candidateEditor = EditorViewModel;
        _autoSavePending++;
        IsSaving = true;
        SaveStatus = Text("Editor.Saving");
        try
        {
            var result = await _autoSaveSession!.ConfirmProtocolChangeAsync(candidate, CanPersistConnection);
            if (!result.IsSuccess)
            {
                SaveStatus = Text(result.ErrorInfo == "Editor.InvalidFields" ? "Editor.InvalidFields" : "Editor.SaveFailed");
                return false;
            }
            _savedProtocolServer = candidate;
            _savedProtocolEditor = candidateEditor;
            NotifyProtocolPreview();
            SaveStatus = Text("Editor.Saved");
            return true;
        }
        catch
        {
            SaveStatus = Text("Editor.SaveFailed");
            return false;
        }
        finally
        {
            _autoSavePending--;
            IsSaving = _autoSavePending > 0;
            // The task completes after this continuation; queue notification for the actual completed state.
            Application.Current.Dispatcher.BeginInvoke(new Action(() => RaisePropertyChanged(nameof(IsConfirmingProtocolChange))));
        }
    }

    internal void EndEditSession() => Server.PropertyChanged -= OnConnectionPropertyChanged;

    private void SaveConfirmedEditorChange()
    {
        if (AutoSaveExisting) _ = SaveExistingAsync();
    }

    internal Task<bool> SaveExplicitAsync()
    {
        if (AutoSaveExisting) return SaveExistingAsync();
        if (IsSaving) return _explicitSaveCompletion?.Task ?? Task.FromResult(false);
        if (!CmdSave.CanExecute(null)) { ReportValidationError(); return Task.FromResult(false); }
        CmdSave.Execute(null);
        return _explicitSaveCompletion?.Task ?? Task.FromResult(false);
    }

    internal void NoteEditorInput()
    {
        if (!AutoSaveExisting || _autoSaveSession == null) return;
        _editRevision++;
        SaveStatus = Text(IsProtocolPreview ? "Editor.ProtocolPreviewStatus" : "Editor.Pending");
    }

    private void OnConnectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        NoteEditorInput();
        if (e.PropertyName == nameof(Server.DisplayName)) RaisePropertyChanged(nameof(WindowTitle));
    }

    private bool CanPersistConnection() => ValidateConnection(Server, EditorViewModel, true);

    private bool ValidateConnection(ProtocolBase server, ProtocolBaseFormViewModel? editor, bool validateView)
    {
        if (string.IsNullOrWhiteSpace(server.DisplayName) || editor?.CanSave() != true) return false;
        if (validateView && AdditionalValidation?.Invoke() == false) return false;
        if (server is ProtocolBaseWithAddressPort endpoint && endpoint.ShowPortInput() &&
            !(IsBuckEdit && endpoint.Port == server.ServerEditorDifferentOptions) &&
            (!int.TryParse(endpoint.Port, out var port) || port < 1 || port > 65535)) return false;
        return true;
    }

    public async Task<bool> SaveExistingAsync()
    {
        if (!AutoSaveExisting) return true;
        if (IsConfirmingProtocolChange) return await _protocolChangeTask!;
        BeginEditSession();
        var edited = _savedProtocolServer!;
        var editor = _savedProtocolEditor;
        if (_autoSavePending == 0 && !_autoSaveSession!.HasChanges(edited))
        {
            SaveStatus = Text(IsProtocolPreview ? "Editor.ProtocolPreviewStatus" : "Editor.Saved");
            return true;
        }
        _autoSavePending++;
        IsSaving = true;
        SaveStatus = Text("Editor.Saving");
        var revision = _editRevision;
        try
        {
            var result = await _autoSaveSession!.SaveAsync(edited, () => ValidateConnection(edited, editor, ReferenceEquals(edited, Server)));
            SaveStatus = result.IsSuccess
                ? Text(IsProtocolPreview ? "Editor.ProtocolPreviewStatus"
                    : revision != _editRevision || _autoSaveSession.HasChanges(edited) ? "Editor.Pending" : "Editor.Saved")
                : Text(result.ErrorInfo == "Editor.InvalidFields" ? "Editor.InvalidFields" : "Editor.SaveFailed");
            RaisePropertyChanged(nameof(WindowTitle));
            return result.IsSuccess;
        }
        catch
        {
            SaveStatus = Text("Editor.SaveFailed");
            return false;
        }
        finally { _autoSavePending--; IsSaving = _autoSavePending > 0; }
    }
}
