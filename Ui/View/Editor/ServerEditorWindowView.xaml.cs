using System;
using System.ComponentModel;
using System.Collections.Generic;
using MessageBox = _1RM.Utils.ThemedMessageBox;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using _1RM.View.Utils;

namespace _1RM.View.Editor;

public partial class ServerEditorWindowView : Window
{
    private readonly bool _autoSave;
    private readonly Func<Task<bool>> _save;
    private readonly Func<bool> _isBusy;
    private readonly NativeMainTitleBar _nativeTitleBar;
    private ServerEditorPageViewModel? _model;
    private bool _ready, _closed, _approved, _saveQueued;
    private Task<bool>? _closeTask;
    private string? _draftBaseline;
    private Func<Task<bool>>? _saveDraft;
    private Func<MessageBoxResult> _draftClosePrompt;
    private static bool _applicationClosePending;
    private readonly HashSet<_1RM.Controls.BindableAvalonEditor> _textEditors = new();
    internal ServerEditorPageViewModel? EditorModel => _model;

    public ServerEditorWindowView(ServerEditorPageViewModel model)
        : this(new ServerEditorPageView { DataContext = model }, model.AutoSaveExisting, model.SaveExistingAsync, () => model.IsSaving)
    {
        _model = model;
        _saveDraft = model.SaveExplicitAsync;
        _draftBaseline = model.Server.ToJsonString();
        model.AdditionalValidation = () => !HasBindingError(EditorBody);
        DataContext = model;
        model.CloseRequested += OnModelCloseRequested;
        model.PropertyChanged += OnEditorModelChanged;
        Loaded += (_, _) =>
        {
            model.BeginEditSession();
            if (model.RequiresExplicitSave)
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    if (!_closed) _draftBaseline = model.Server.ToJsonString();
                }));
        };
        Closed += (_, _) => { model.CloseRequested -= OnModelCloseRequested; model.PropertyChanged -= OnEditorModelChanged; model.AdditionalValidation = null; model.EndEditSession(); };
    }

    internal ServerEditorWindowView(FrameworkElement content, bool autoSave, Func<Task<bool>> save, Func<bool> isBusy)
    {
        InitializeComponent();
        _draftClosePrompt = ShowDraftClosePrompt;
        _autoSave = autoSave; _save = save; _isBusy = isBusy;
        EditorBody.Content = content;
        _nativeTitleBar = new NativeMainTitleBar(this, CaptionRow, CaptionArea, CaptionReserve, OnCloseRequested);
        _ = new WindowWorkArea(this);
        Loaded += (_, _) => _ready = true;
        EditorBody.AddHandler(Keyboard.LostKeyboardFocusEvent, new KeyboardFocusChangedEventHandler((_, _) => QueueSave()), true);
        EditorBody.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler((_, _) => QueueSave()), true);
        EditorBody.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, _) => NoteEditorInput()), true);
        EditorBody.AddHandler(PasswordBox.PasswordChangedEvent, new RoutedEventHandler((_, _) => NoteEditorInput()), true);
        EditorBody.AddHandler(System.Windows.Controls.Primitives.Selector.SelectionChangedEvent,
            new SelectionChangedEventHandler((_, _) => NoteEditorInput()), true);
        EditorBody.AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler((_, _) => NoteEditorInput()), true);
        EditorBody.AddHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler((_, _) => NoteEditorInput()), true);
        EditorBody.Loaded += (_, _) => SubscribeInstructionEditors(EditorBody);
        Closing += OnEditorClosing;
        Closed += (_, _) =>
        {
            _closed = true; _ready = false;
            foreach (var editor in _textEditors) editor.TextChanged -= OnInstructionTextChanged;
            _textEditors.Clear();
            EditorBody.Content = null;
        };
    }

    private void NoteEditorInput()
    {
        if (_ready && !_closed) _model?.NoteEditorInput();
    }

    private void OnInstructionTextChanged(object? sender, EventArgs e) => NoteEditorInput();

    private void OnEditorModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ServerEditorPageViewModel.EditorViewModel))
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (!_closed) SubscribeInstructionEditors(EditorBody);
            }));
    }

    private void SubscribeInstructionEditors(DependencyObject node)
    {
        if (node is _1RM.Controls.BindableAvalonEditor editor && _textEditors.Add(editor))
            editor.TextChanged += OnInstructionTextChanged;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            SubscribeInstructionEditors(VisualTreeHelper.GetChild(node, i));
    }

    private void QueueSave()
    {
        if (!_ready || !_autoSave || _saveQueued || _closed) return;
        _saveQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(async () =>
        {
            _saveQueued = false;
            if (!_closed && _ready) await SaveEditorAsync();
        }));
    }

    private void OnEditorClosing(object? sender, CancelEventArgs e)
    {
        if (_approved) return;
        e.Cancel = true;
        Dispatcher.BeginInvoke(new Action(OnCloseRequested));
    }

    private void OnCloseRequested() => _ = RequestCloseAsync();

    private void OnModelCloseRequested()
    {
        if (!_autoSave && _model is { IsSaving: false } && _model.SaveStatus == ServerEditorPageViewModel.Text("Editor.Saved"))
            _draftBaseline = _model.Server.ToJsonString();
        OnCloseRequested();
    }

    private void ReturnToSavedProtocol_OnClick(object sender, RoutedEventArgs e) => _model?.ReturnToSavedProtocol();

    private async void ConfirmProtocolChange_OnClick(object sender, RoutedEventArgs e)
    {
        await RequestProtocolChangeAsync(prompt => MessageBox.Show(this, prompt, ServerEditorPageViewModel.Text("Editor.ProtocolChangeTitle"),
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes);
    }

    internal async Task<bool> RequestProtocolChangeAsync(Func<string, bool> confirm)
    {
        if (_model?.IsProtocolPreview != true || _model.IsConfirmingProtocolChange) return false;
        Keyboard.ClearFocus();
        _1RM.Controls.EditorValidation.RevealErrors(EditorBody);
        if (HasBindingError(EditorBody)) { _model.ReportValidationError(); return false; }
        if (!confirm(_model.ProtocolChangePrompt)) return false;
        return await _model.ConfirmProtocolChangeAsync();
    }

    internal Task<bool> RequestCloseAsync()
    {
        if (_closeTask is { IsCompleted: false }) return _closeTask;
        // Publish the pending request before a modal prompt can run a nested dispatcher frame.
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _closeTask = completion.Task;
        _ = CompleteCloseRequestAsync(completion);
        return completion.Task;
    }

    private async Task CompleteCloseRequestAsync(TaskCompletionSource<bool> completion)
    {
        try { completion.TrySetResult(await CloseCoreAsync()); }
        catch (Exception exception) { completion.TrySetException(exception); }
    }

    internal bool HasDraftChanges => _model?.RequiresExplicitSave == true
        && _draftBaseline != null && _model.Server.ToJsonString() != _draftBaseline;

    internal Task<bool> RequestSwitchCloseAsync() => RequestCloseAsync();

    private MessageBoxResult ShowDraftClosePrompt() => MessageBox.Show(this,
        ServerEditorPageViewModel.Text("Editor.DraftClosePrompt"), ServerEditorPageViewModel.Text("Editor.UnsavedTitle"),
        MessageBoxButton.YesNoCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel,
        new Dictionary<MessageBoxResult, string>
        {
            [MessageBoxResult.Yes] = ServerEditorPageViewModel.Text("Save"),
            [MessageBoxResult.No] = ServerEditorPageViewModel.Text("Editor.DiscardDraft"),
            [MessageBoxResult.Cancel] = ServerEditorPageViewModel.Text("Editor.KeepEditing")
        });

    private async Task<bool> SaveDraftAsync()
    {
        _1RM.Controls.EditorValidation.RevealErrors(EditorBody);
        if (HasBindingError(EditorBody)) { _model?.ReportValidationError(); return false; }
        if (_saveDraft == null || !await _saveDraft()) return false;
        _draftBaseline = _model?.Server.ToJsonString();
        return true;
    }

    private async Task<bool> CloseCoreAsync()
    {
        if (_closed) return true;
        if (!_autoSave && _isBusy() && !await SaveDraftAsync()) return false;
        Keyboard.ClearFocus(); // Commit LostFocus bindings before capturing the final snapshot.
        if (!_autoSave && HasDraftChanges)
        {
            var choice = _draftClosePrompt();
            if (choice == MessageBoxResult.Yes) { if (!await SaveDraftAsync()) return false; }
            else if (choice != MessageBoxResult.No) return false;
        }
        if (_autoSave && !await SaveEditorAsync())
        {
            _1RM.Controls.EditorValidation.RevealErrors(EditorBody);
            if (MessageBox.Show(this, ServerEditorPageViewModel.Text("Editor.DiscardPrompt"),
                    ServerEditorPageViewModel.Text("Editor.UnsavedTitle"), MessageBoxButton.YesNo,
                    MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return false;
        }
        _approved = true;
        Close();
        return true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled) return;
        if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            _1RM.Controls.EditorValidation.RevealErrors(EditorBody);
            e.Handled = true;
            if (Keyboard.FocusedElement is TextBox textBox)
                textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            if (Keyboard.FocusedElement is ComboBox comboBox)
                comboBox.GetBindingExpression(ComboBox.TextProperty)?.UpdateSource();
            if (_autoSave) _ = SaveEditorAsync();
            else if (_model?.CmdSave.CanExecute(null) == true) _model.CmdSave.Execute(null);
        }
        else if (e.Key == Key.Escape && Keyboard.FocusedElement is not ComboBox { IsDropDownOpen: true })
        {
            e.Handled = true;
            OnCloseRequested();
        }
    }

    private Task<bool> SaveEditorAsync()
    {
        if (_model?.IsProtocolPreview != true && HasBindingError(EditorBody))
        {
            _model?.ReportValidationError();
            return Task.FromResult(false);
        }
        return _save();
    }

    private static bool HasBindingError(DependencyObject node)
    {
        if (node is UIElement element && element.Visibility != Visibility.Visible) return false;
        if (Validation.GetHasError(node)) return true;
        if (node is not Visual) return false;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            if (HasBindingError(VisualTreeHelper.GetChild(node, i))) return true;
        return false;
    }

    internal static bool DeferApplicationClose(Action continueClose)
    {
        var dispatcher = Application.Current.Dispatcher;
        if (!dispatcher.CheckAccess()) return dispatcher.Invoke(() => DeferApplicationClose(continueClose));
        var editors = Application.Current.Windows.OfType<ServerEditorWindowView>().ToArray();
        if (editors.Length == 0) return false;
        if (_applicationClosePending) return true;
        _applicationClosePending = true;
        dispatcher.BeginInvoke(new Action(async () =>
        {
            bool approved = true;
            try
            {
                foreach (var editor in editors)
                    if (!await editor.RequestCloseAsync()) { approved = false; break; }
            }
            finally { _applicationClosePending = false; }
            if (approved) continueClose();
            else IoC.TryGet<MainWindowViewModel>()?.ShowMe();
        }));
        return true;
    }
}
