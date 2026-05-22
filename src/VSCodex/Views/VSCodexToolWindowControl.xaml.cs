using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.VisualStudio.PlatformUI;
using VSCodex.Models;
using VSCodex.ViewModels;

namespace VSCodex.Views;

public partial class VSCodexToolWindowControl : UserControl
{
    private bool _isPromptResizeDragging;
    private double _promptResizeStartHeight;
    private double _promptResizeVerticalDelta;
    private Thumb? _promptResizeThumb;
    private INotifyCollectionChanged? _messagesCollection;
    private INotifyPropertyChanged? _viewModelNotifications;

    public VSCodexToolWindowControl()
    {
        InitializeComponent();
        DataObject.AddPastingHandler(PromptTextBox, OnPromptPasting);
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) =>
        {
            AttachViewModel(ViewModel);
            ApplyVisualStudioThemeToComboBoxes();
        };
        Unloaded += (_, _) =>
        {
            AttachViewModel(null);
            FinishPromptResizeSafely(commit: false);
        };
    }

    private VSCodexToolWindowViewModel? ViewModel => DataContext as VSCodexToolWindowViewModel;

    private void OnPromptPasting(object sender, DataObjectPastingEventArgs e)
    {
        var data = e.DataObject;
        if (HasText(data))
        {
            return;
        }

        if (TryAttachFileDrop(data) || TryAttachClipboardImage())
        {
            e.CancelCommand();
        }
    }

    private void OnPromptPreviewDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void OnPromptDrop(object sender, DragEventArgs e)
    {
        if (TryAttachFileDrop(e.Data))
        {
            e.Handled = true;
        }
    }

    private void OnPromptPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            InsertPromptNewLine();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            ExecuteIfAvailable(ViewModel.RunCommand);
            e.Handled = true;
            return;
        }

        if (ViewModel.IsPromptSuggestionOpen)
        {
            if (e.Key == Key.Down)
            {
                MovePromptSuggestionSelection(1);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Up)
            {
                MovePromptSuggestionSelection(-1);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Tab)
            {
                InsertSelectedPromptSuggestion();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                ViewModel.ClosePromptSuggestions();
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.Escape)
        {
            ExecuteIfAvailable(ViewModel.CancelCommand);
            e.Handled = true;
        }
    }

    private void OnOpenToolPanelClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        ViewModel.IsToolPanelOpen = true;
        if (ViewModel.SelectedToolTabIndex < 0)
        {
            ViewModel.SelectedToolTabIndex = 0;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        AttachViewModel(e.NewValue as VSCodexToolWindowViewModel);
    }

    private void AttachViewModel(VSCodexToolWindowViewModel? viewModel)
    {
        if (_viewModelNotifications != null)
        {
            _viewModelNotifications.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModelNotifications = viewModel;
        if (_viewModelNotifications != null)
        {
            _viewModelNotifications.PropertyChanged += OnViewModelPropertyChanged;
        }

        AttachMessagesCollection(viewModel?.Messages);
    }

    private void AttachMessagesCollection(INotifyCollectionChanged? collection)
    {
        if (ReferenceEquals(_messagesCollection, collection))
        {
            return;
        }

        if (_messagesCollection != null)
        {
            _messagesCollection.CollectionChanged -= OnMessagesCollectionChanged;
        }

        _messagesCollection = collection;
        if (_messagesCollection != null)
        {
            _messagesCollection.CollectionChanged += OnMessagesCollectionChanged;
        }
    }

    private void OnMessagesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add && e.Action != NotifyCollectionChangedAction.Reset)
        {
            return;
        }

#pragma warning disable VSTHRD001, VSTHRD110
        _ = Dispatcher.BeginInvoke(new Action(ScrollConversationToLatest), DispatcherPriority.Background);
#pragma warning restore VSTHRD001, VSTHRD110
    }

    private void ScrollConversationToLatest()
    {
        if (ViewModel == null || ViewModel.Messages.Count == 0)
        {
            return;
        }

        ConversationListBox.ScrollIntoView(ViewModel.Messages[ViewModel.Messages.Count - 1]);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(VSCodexToolWindowViewModel.VoiceTranscriptRevision))
        {
            return;
        }

#pragma warning disable VSTHRD001, VSTHRD110
        _ = Dispatcher.BeginInvoke(new Action(SyncPromptTextBoxAfterVoiceTranscript), DispatcherPriority.Background);
#pragma warning restore VSTHRD001, VSTHRD110
    }

    private void SyncPromptTextBoxAfterVoiceTranscript()
    {
        if (ViewModel == null)
        {
            return;
        }

        PromptTextBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
        var prompt = ViewModel.Prompt ?? string.Empty;
        if (!string.Equals(PromptTextBox.Text, prompt, StringComparison.Ordinal))
        {
            PromptTextBox.SetCurrentValue(TextBox.TextProperty, prompt);
            PromptTextBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }

        PromptTextBox.Focus();
        PromptTextBox.CaretIndex = PromptTextBox.Text.Length;
        PromptTextBox.SelectionLength = 0;
        PromptTextBox.ScrollToEnd();
    }

    private void InsertPromptNewLine()
    {
        var selectionStart = PromptTextBox.SelectionStart;
        var selectionLength = PromptTextBox.SelectionLength;
        var text = PromptTextBox.Text ?? string.Empty;
        var prompt = text.Remove(selectionStart, selectionLength).Insert(selectionStart, Environment.NewLine);
        if (ViewModel != null)
        {
            ViewModel.Prompt = prompt;
        }

        PromptTextBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
        PromptTextBox.SelectionStart = selectionStart + Environment.NewLine.Length;
        PromptTextBox.SelectionLength = 0;
    }

    private void OnCloseToolPanelClick(object sender, RoutedEventArgs e) => ViewModel?.IsToolPanelOpen = false;

    private void OnRunControlClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        ExecuteIfAvailable(ViewModel.IsRunControlInStopMode ? ViewModel.CancelCommand : ViewModel.RunCommand);
    }

    private void OnToggleVoiceInputClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        ViewModel.ToggleVoiceInput();
        PromptTextBox.Focus();
        PromptTextBox.CaretIndex = PromptTextBox.Text.Length;
        e.Handled = true;
    }

    private void OnReferenceSuggestionDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox listBox || listBox.SelectedItem is not WorkspaceFileReference reference || ViewModel == null)
        {
            return;
        }

        var token = reference.ReferenceKey;
        if (string.IsNullOrWhiteSpace(token))
        {
            token = reference.ReferenceKind == "selection" ? "#selection" : "@" + reference.RelativePath;
        }

        ViewModel.Prompt = string.IsNullOrWhiteSpace(ViewModel.Prompt)
            ? token + " "
            : ViewModel.Prompt.TrimEnd() + " " + token + " ";
        PromptTextBox.Focus();
        PromptTextBox.CaretIndex = PromptTextBox.Text.Length;
        e.Handled = true;
    }

    private void OnPromptSuggestionDoubleClick(object sender, MouseButtonEventArgs e)
    {
        InsertSelectedPromptSuggestion();
        e.Handled = true;
    }

    private void OnHistoryItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox listBox || listBox.SelectedItem is not SessionHistoryItem item || ViewModel == null)
        {
            return;
        }

        ExecuteIfAvailable(ViewModel.LoadHistoryCommand, item);
        e.Handled = true;
    }

    private void OnPromptResizeDragStarted(object sender, DragStartedEventArgs e)
    {
        _isPromptResizeDragging = true;
        _promptResizeThumb = sender as Thumb;
        _promptResizeStartHeight = ResolveCurrentPromptHeight();
        _promptResizeVerticalDelta = 0d;
        Mouse.OverrideCursor = Cursors.SizeNS;
        ViewModel?.ClosePromptSuggestions();
        e.Handled = true;
    }

    private void OnPromptResizeDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!_isPromptResizeDragging)
        {
            _isPromptResizeDragging = true;
            _promptResizeThumb = sender as Thumb;
            _promptResizeStartHeight = ResolveCurrentPromptHeight();
            _promptResizeVerticalDelta = 0d;
            Mouse.OverrideCursor = Cursors.SizeNS;
        }

        try
        {
            _promptResizeVerticalDelta += e.VerticalChange;
            ApplyPromptResizeHeight(ClampPromptHeight(_promptResizeStartHeight - _promptResizeVerticalDelta));
        }
        catch
        {
            ResetPromptResizeState();
        }

        e.Handled = true;
    }

    private void OnPromptResizeDragCompleted(object sender, DragCompletedEventArgs e)
    {
        FinishPromptResizeSafely(commit: true);
        e.Handled = true;
    }

    private void OnPromptResizeMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        FinishPromptResizeSafely(commit: true);
        e.Handled = true;
    }

    private void OnPromptResizeLostMouseCapture(object sender, MouseEventArgs e)
    {
        FinishPromptResizeSafely(commit: true);
        e.Handled = true;
    }

    private void FinishPromptResizeSafely(bool commit)
    {
        try
        {
            FinishPromptResize(commit);
        }
        catch
        {
            ResetPromptResizeState();
        }
    }

    private void FinishPromptResize(bool commit)
    {
        if (!_isPromptResizeDragging)
        {
            Mouse.OverrideCursor = null;
            return;
        }

        _isPromptResizeDragging = false;
        _promptResizeVerticalDelta = 0d;
        Mouse.OverrideCursor = null;

        if (_promptResizeThumb?.IsMouseCaptured == true)
        {
            _promptResizeThumb.ReleaseMouseCapture();
        }
        _promptResizeThumb = null;

        var height = double.IsNaN(PromptTextBox.Height) || PromptTextBox.Height <= 0d
            ? PromptTextBox.ActualHeight
            : PromptTextBox.Height;
        var clamped = ClampPromptHeight(height);

        ApplyPromptResizeHeight(clamped);
        if (commit)
        {
            ViewModel?.CommitInputAreaHeight(clamped);
        }
    }

    private void ResetPromptResizeState()
    {
        _isPromptResizeDragging = false;
        _promptResizeVerticalDelta = 0d;
        Mouse.OverrideCursor = null;

        try
        {
            if (_promptResizeThumb?.IsMouseCaptured == true)
            {
                _promptResizeThumb.ReleaseMouseCapture();
            }
        }
        catch
        {
            // The resize path must never let a cleanup failure destabilize Visual Studio.
        }

        _promptResizeThumb = null;
    }

    private void ApplyPromptResizeHeight(double height)
    {
        PromptTextBox.SetCurrentValue(HeightProperty, height);
        ViewModel?.SetLiveInputAreaHeight(height);
    }

    private double ResolveCurrentPromptHeight()
    {
        var currentHeight = double.IsNaN(PromptTextBox.Height) || PromptTextBox.Height <= 0d
            ? PromptTextBox.ActualHeight
            : PromptTextBox.Height;
        if (currentHeight <= 0d)
        {
            currentHeight = ViewModel?.InputAreaHeight ?? PromptTextBox.MinHeight;
        }

        return ClampPromptHeight(currentHeight);
    }

    private double ClampPromptHeight(double height)
    {
        return Math.Max(PromptTextBox.MinHeight, Math.Min(ResolvePromptMaxHeight(), height));
    }

    private double ResolvePromptMaxHeight()
    {
        var maxHeight = double.IsNaN(PromptTextBox.MaxHeight) || double.IsInfinity(PromptTextBox.MaxHeight)
            ? 600d
            : PromptTextBox.MaxHeight;
        var layoutMax = Root.ActualHeight > 0d ? Math.Max(96d, Root.ActualHeight * 0.45d) : maxHeight;
        return Math.Min(maxHeight, layoutMax);
    }

    private void InsertSelectedPromptSuggestion()
    {
        if (ViewModel == null)
        {
            return;
        }

        var suggestion = ViewModel.SelectedPromptSuggestion;
        if (suggestion?.TargetTab == "browse-files")
        {
            BrowseAndInsertFileReferences();
            return;
        }

        ViewModel.InsertPromptSuggestion(suggestion);
        PromptTextBox.Focus();
        PromptTextBox.CaretIndex = PromptTextBox.Text.Length;
    }

    private void BrowseAndInsertFileReferences()
    {
        if (ViewModel == null)
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Reference files for VSCodex",
            Filter = "Code and text files|*.cs;*.xaml;*.json;*.xml;*.md;*.txt;*.props;*.targets;*.csproj;*.sln;*.slnx;*.config;*.yml;*.yaml;*.ps1;*.ts;*.tsx;*.js;*.jsx;*.css;*.html;*.razor|All files|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog() == true)
        {
            ViewModel.InsertFileReferencePaths(dialog.FileNames);
            PromptTextBox.Focus();
            PromptTextBox.CaretIndex = PromptTextBox.Text.Length;
        }
    }

    private void MovePromptSuggestionSelection(int delta)
    {
        if (ViewModel == null || ViewModel.PromptSuggestions.Count == 0)
        {
            return;
        }

        var current = PromptSuggestionList.SelectedIndex;
        if (current < 0)
        {
            current = 0;
        }

        var next = (current + delta + ViewModel.PromptSuggestions.Count) % ViewModel.PromptSuggestions.Count;
        PromptSuggestionList.SelectedIndex = next;
        PromptSuggestionList.ScrollIntoView(PromptSuggestionList.SelectedItem);
    }

    private void ApplyVisualStudioThemeToComboBoxes()
    {
        foreach (var comboBox in FindVisualChildren<ComboBox>(this))
        {
            ApplyComboBoxTheme(comboBox);
            comboBox.Loaded -= OnComboBoxLoaded;
            comboBox.Loaded += OnComboBoxLoaded;
            comboBox.DropDownOpened -= OnComboBoxDropDownOpened;
            comboBox.DropDownOpened += OnComboBoxDropDownOpened;
        }
    }

    private void OnComboBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ComboBox comboBox)
        {
            ApplyComboBoxTheme(comboBox);
        }
    }

    private void OnComboBoxDropDownOpened(object sender, System.EventArgs e)
    {
        if (sender is ComboBox comboBox)
        {
            ApplyComboBoxTheme(comboBox);
        }
    }

    private static void ApplyComboBoxTheme(ComboBox comboBox)
    {
        comboBox.SetResourceReference(Control.BackgroundProperty, EnvironmentColors.ComboBoxBackgroundBrushKey);
        comboBox.SetResourceReference(Control.ForegroundProperty, EnvironmentColors.ComboBoxTextBrushKey);
        comboBox.SetResourceReference(Control.BorderBrushProperty, EnvironmentColors.ComboBoxBorderBrushKey);
        comboBox.SetResourceReference(TextElement.ForegroundProperty, EnvironmentColors.ComboBoxTextBrushKey);
        comboBox.ApplyTemplate();

        if (comboBox.Template.FindName("PART_EditableTextBox", comboBox) is TextBox editableTextBox)
        {
            editableTextBox.SetResourceReference(Control.BackgroundProperty, EnvironmentColors.ComboBoxBackgroundBrushKey);
            editableTextBox.SetResourceReference(Control.ForegroundProperty, EnvironmentColors.ComboBoxTextBrushKey);
            editableTextBox.SetResourceReference(Control.BorderBrushProperty, EnvironmentColors.ComboBoxBorderBrushKey);
            editableTextBox.SetResourceReference(TextBox.CaretBrushProperty, EnvironmentColors.ComboBoxTextBrushKey);
            editableTextBox.SetResourceReference(TextElement.ForegroundProperty, EnvironmentColors.ComboBoxTextBrushKey);
        }
    }

    private static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(DependencyObject root)
        where T : DependencyObject
    {
        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T typedChild)
            {
                yield return typedChild;
            }

            foreach (var nestedChild in FindVisualChildren<T>(child))
            {
                yield return nestedChild;
            }
        }
    }

    private bool TryAttachFileDrop(IDataObject data)
    {
        if (!data.GetDataPresent(DataFormats.FileDrop))
        {
            return false;
        }

        if (data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
        {
            return false;
        }

        ViewModel?.AttachFiles(files.Where(System.IO.File.Exists));
        return true;
    }

    private bool TryAttachClipboardImage()
    {
        if (!Clipboard.ContainsImage())
        {
            return false;
        }

        var image = Clipboard.GetImage();
        if (image == null)
        {
            return false;
        }

        ViewModel?.AttachClipboardImage(image);
        return true;
    }

    private static bool HasText(IDataObject data)
    {
        return data.GetDataPresent(DataFormats.UnicodeText)
            || data.GetDataPresent(DataFormats.Text)
            || data.GetDataPresent(DataFormats.StringFormat);
    }

    private static void ExecuteIfAvailable(ICommand command)
    {
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private static void ExecuteIfAvailable(ICommand command, object parameter)
    {
        if (command.CanExecute(parameter))
        {
            command.Execute(parameter);
        }
    }
}
