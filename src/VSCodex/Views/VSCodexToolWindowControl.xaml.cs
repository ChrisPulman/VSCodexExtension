// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.Win32;
using VSCodex.Infrastructure;
using VSCodex.Models;
using VSCodex.Options;
using VSCodex.ViewModels;

namespace VSCodex.Views;

/// <summary>Provides the vS Codex Tool Window Control implementation.</summary>
public partial class VSCodexToolWindowControl : UserControl
{
    /// <summary>Named number used by this type.</summary>
    private const double Numeric0Point45 = 0.45;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric3 = 3;

    /// <summary>Named number used by this type.</summary>
    private const double Numeric600Point0 = 600.0;

    /// <summary>Named number used by this type.</summary>
    private const double Numeric96Point0 = 96.0;

    /// <summary>Defines the setup Settings Tab Index.</summary>
    private const int SetupSettingsTabIndex = 1;

    /// <summary>Defines the skills Settings Tab Index.</summary>
    private const int SkillsSettingsTabIndex = Numeric3;

    /// <summary>Defines the mcp Settings Tab Index.</summary>
    private const int McpSettingsTabIndex = 4;

    /// <summary>Stores the is Prompt Resize Dragging.</summary>
    private bool _isPromptResizeDragging;

    /// <summary>Stores the prompt Resize Start Height.</summary>
    private double _promptResizeStartHeight;

    /// <summary>Stores the prompt Resize Vertical Delta.</summary>
    private double _promptResizeVerticalDelta;

    /// <summary>Stores the prompt Resize Thumb.</summary>
    private Thumb? _promptResizeThumb;

    /// <summary>Stores the activity Roots Collection.</summary>
    private INotifyCollectionChanged? _activityRootsCollection;

    /// <summary>Stores the view Model Notifications.</summary>
    private INotifyPropertyChanged? _viewModelNotifications;

    /// <summary>Stores the settings Open Request Pending.</summary>
    private bool _settingsOpenRequestPending;

    /// <summary>Initializes a new instance of the <see cref="VSCodexToolWindowControl"/> class.</summary>
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

    /// <summary>Gets the view Model.</summary>
    private VSCodexToolWindowViewModel? ViewModel => DataContext as VSCodexToolWindowViewModel;

    /// <summary>Determines whether is Dedicated Settings Tab.</summary>
    /// <param name="tabIndex">The tab Index.</param>
    /// <returns><see langword="true"/> when is Dedicated Settings Tab succeeds; otherwise, <see langword="false"/>.</returns>
    private static bool IsDedicatedSettingsTab(int tabIndex)
    {
        return tabIndex == 1 || (uint)(tabIndex - Numeric3) <= 1U;
    }

    /// <summary>Opens settings Page.</summary>
    private static void OpenSettingsPage()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        VsShellUtilities.ShowToolsOptionsPage<OptionsProvider.GeneralOptions>();
    }

    /// <summary>Applies combo Box Theme.</summary>
    /// <param name="comboBox">The comboBox.</param>
    private static void ApplyComboBoxTheme(ComboBox comboBox)
    {
        comboBox.SetResourceReference(Control.BackgroundProperty, EnvironmentColors.ComboBoxBackgroundBrushKey);
        comboBox.SetResourceReference(Control.ForegroundProperty, EnvironmentColors.ComboBoxTextBrushKey);
        comboBox.SetResourceReference(Control.BorderBrushProperty, EnvironmentColors.ComboBoxBorderBrushKey);
        comboBox.SetResourceReference(TextElement.ForegroundProperty, EnvironmentColors.ComboBoxTextBrushKey);
        _ = comboBox.ApplyTemplate();
        if (comboBox.Template.FindName("PART_EditableTextBox", comboBox) is not TextBox editableTextBox)
        {
            return;
        }

        editableTextBox.SetResourceReference(Control.BackgroundProperty, EnvironmentColors.ComboBoxBackgroundBrushKey);
        editableTextBox.SetResourceReference(Control.ForegroundProperty, EnvironmentColors.ComboBoxTextBrushKey);
        editableTextBox.SetResourceReference(Control.BorderBrushProperty, EnvironmentColors.ComboBoxBorderBrushKey);
        editableTextBox.SetResourceReference(TextBoxBase.CaretBrushProperty, EnvironmentColors.ComboBoxTextBrushKey);
        editableTextBox.SetResourceReference(TextElement.ForegroundProperty, EnvironmentColors.ComboBoxTextBrushKey);
    }

    /// <summary>Finds visual Children.</summary>
    /// <typeparam name="T">The t type.</typeparam>
    /// <param name="root">The root.</param>
    /// <returns>The find Visual Children result.</returns>
    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root)
        where T : DependencyObject
    {
        int childCount = VisualTreeHelper.GetChildrenCount(root);
        for (int index = 0; index < childCount; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T typedChild)
            {
                yield return typedChild;
            }

            foreach (T item in FindVisualChildren<T>(child))
            {
                yield return item;
            }
        }
    }

    /// <summary>Determines whether has Text.</summary>
    /// <param name="data">The data.</param>
    /// <returns><see langword="true"/> when has Text succeeds; otherwise, <see langword="false"/>.</returns>
    private static bool HasText(IDataObject data)
    {
        return !data.GetDataPresent(DataFormats.UnicodeText) && !data.GetDataPresent(DataFormats.Text) ? data.GetDataPresent(DataFormats.StringFormat) : true;
    }

    /// <summary>Executes if Available.</summary>
    /// <param name="command">The command.</param>
    private static void ExecuteIfAvailable(ICommand command)
    {
        if (!command.CanExecute(null))
        {
            return;
        }

        command.Execute(null);
    }

    /// <summary>Executes if Available.</summary>
    /// <param name="command">The command.</param>
    /// <param name="parameter">The parameter.</param>
    private static void ExecuteIfAvailable(ICommand command, object parameter)
    {
        if (!command.CanExecute(parameter))
        {
            return;
        }

        command.Execute(parameter);
    }

    /// <summary>Handles the prompt Pasting event.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void OnPromptPasting(object sender, DataObjectPastingEventArgs e)
    {
        IDataObject data = e.DataObject;
        if (HasText(data) || (!TryAttachFileDrop(data) && !TryAttachClipboardImage()))
        {
            return;
        }

        e.CancelCommand();
    }

    /// <summary>Handles the prompt Preview Drag Over event.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void OnPromptPreviewDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    /// <summary>Handles the prompt Drop event.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void OnPromptDrop(object sender, DragEventArgs e)
    {
        if (!TryAttachFileDrop(e.Data))
        {
            return;
        }

        e.Handled = true;
    }

    /// <summary>Handles the prompt Preview Key Down event.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void OnPromptPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (e.Key == Key.Return)
        {
            HandlePromptReturn(e);
            return;
        }

        if (ViewModel.IsPromptSuggestionOpen && TryHandlePromptSuggestionKeyDown(e))
        {
            return;
        }

        if (e.Key != Key.Escape)
        {
            return;
        }

        ExecuteIfAvailable(ViewModel.CancelCommand);
        e.Handled = true;
    }

    /// <summary>Handles a return key in the prompt editor.</summary>
    /// <param name="e">The key event.</param>
    private void HandlePromptReturn(KeyEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            InsertPromptNewLine();
        }
        else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && ViewModel?.IsRunning == true)
        {
            ExecuteIfAvailable(ViewModel.AlternateFollowUpCommand);
        }
        else if (ViewModel is not null)
        {
            ExecuteIfAvailable(ViewModel.RunCommand);
        }

        e.Handled = true;
    }

    /// <summary>Attempts to handle the selected prompt Suggestion key.</summary>
    /// <param name="e">The key event.</param>
    /// <returns><see langword="true"/> when the key was handled; otherwise, <see langword="false"/>.</returns>
    private bool TryHandlePromptSuggestionKeyDown(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
            {
                MovePromptSuggestionSelection(1);
                break;
            }

            case Key.Up:
            {
                MovePromptSuggestionSelection(-1);
                break;
            }

            case Key.Tab:
            {
                InsertSelectedPromptSuggestion();
                break;
            }

            case Key.Escape:
            {
                ViewModel?.ClosePromptSuggestions();
                break;
            }

            default:
                return false;
        }

        e.Handled = true;
        return true;
    }

    /// <summary>Handles the open Settings Click event.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void OnOpenSettingsClick(object sender, RoutedEventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        QueueOpenSettingsPage();
        e.Handled = true;
    }

    /// <summary>Handles the data Context Changed event.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        _ = sender;
        AttachViewModel(e.NewValue as VSCodexToolWindowViewModel);
    }

    /// <summary>Performs the attach View Model operation.</summary>
    /// <param name="viewModel">The view Model.</param>
    private void AttachViewModel(VSCodexToolWindowViewModel? viewModel)
    {
        if (_viewModelNotifications is not null)
        {
            _viewModelNotifications.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModelNotifications = viewModel;
        if (_viewModelNotifications is not null)
        {
            _viewModelNotifications.PropertyChanged += OnViewModelPropertyChanged;
        }

        AttachActivityRootsCollection(viewModel?.RunActivityRoots);
    }

    /// <summary>Performs the attach Activity Roots Collection operation.</summary>
    /// <param name="collection">The collection.</param>
    private void AttachActivityRootsCollection(INotifyCollectionChanged? collection)
    {
        if (_activityRootsCollection == collection)
        {
            return;
        }

        if (_activityRootsCollection is not null)
        {
            _activityRootsCollection.CollectionChanged -= OnActivityRootsCollectionChanged;
        }

        _activityRootsCollection = collection;
        if (_activityRootsCollection is null)
        {
            return;
        }

        _activityRootsCollection.CollectionChanged += OnActivityRootsCollectionChanged;
    }

    /// <summary>Handles the activity Roots Collection Changed event.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void OnActivityRootsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add && e.Action != NotifyCollectionChangedAction.Reset)
        {
            return;
        }

        TaskObserver.FireAndForget(ScrollConversationToLatestAsync());
    }

    /// <summary>Performs the scroll Conversation To Latest operation asynchronously.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task ScrollConversationToLatestAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        ScrollConversationToLatest();
    }

    /// <summary>Performs the scroll Conversation To Latest operation.</summary>
    private void ScrollConversationToLatest()
    {
        if (ViewModel is null || ViewModel.RunActivityRoots.Count == 0)
        {
            return;
        }

        ConversationScrollViewer.ScrollToEnd();
    }

    /// <summary>Handles the conversation Mouse Wheel event.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void OnConversationMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ConversationScrollViewer.ScrollToVerticalOffset(ConversationScrollViewer.VerticalOffset - (double)e.Delta);
        e.Handled = true;
    }

    /// <summary>Handles the view Model Property Changed event.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        TaskObserver.FireAndForget(HandleViewModelPropertyChangedAsync(e));
    }

    /// <summary>Handles a view Model Property Changed event asynchronously.</summary>
    /// <param name="e">The event.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task HandleViewModelPropertyChangedAsync(PropertyChangedEventArgs e)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        if (e.PropertyName == "IsToolPanelOpen" || e.PropertyName == "SelectedToolTabIndex")
        {
            QueueSettingsRedirectCheck();
        }

        if (e.PropertyName != "VoiceTranscriptRevision")
        {
            return;
        }

        SyncPromptTextBoxAfterVoiceTranscript();
    }

    /// <summary>Performs the queue Settings Redirect Check operation.</summary>
    private void QueueSettingsRedirectCheck()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        QueueSettingsRedirectWhenRequired();
    }

    /// <summary>Performs the queue Settings Redirect When Required operation.</summary>
    private void QueueSettingsRedirectWhenRequired()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        VSCodexToolWindowViewModel? viewModel = ViewModel;
        if (viewModel?.IsToolPanelOpen != true || !IsDedicatedSettingsTab(viewModel.SelectedToolTabIndex))
        {
            return;
        }

        viewModel.IsToolPanelOpen = false;
        QueueOpenSettingsPage();
    }

    /// <summary>Performs the queue Open Settings Page operation.</summary>
    private void QueueOpenSettingsPage()
    {
        if (_settingsOpenRequestPending)
        {
            return;
        }

        _settingsOpenRequestPending = true;
        TaskObserver.FireAndForget(OpenSettingsPageAsync());
    }

    /// <summary>Opens the settings Page asynchronously.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task OpenSettingsPageAsync()
    {
        await Task.Yield();
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        _settingsOpenRequestPending = false;
        OpenSettingsPage();
    }

    /// <summary>Performs the sync Prompt Text Box After Voice Transcript operation.</summary>
    private void SyncPromptTextBoxAfterVoiceTranscript()
    {
        if (ViewModel is null)
        {
            return;
        }

        PromptTextBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
        string prompt = ViewModel.Prompt ?? string.Empty;
        if (!string.Equals(PromptTextBox.Text, prompt, StringComparison.Ordinal))
        {
            PromptTextBox.SetCurrentValue(TextBox.TextProperty, prompt);
            PromptTextBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }

        _ = PromptTextBox.Focus();
        PromptTextBox.CaretIndex = PromptTextBox.Text.Length;
        PromptTextBox.SelectionLength = 0;
        PromptTextBox.ScrollToEnd();
    }

    /// <summary>Performs the insert Prompt New Line operation.</summary>
    private void InsertPromptNewLine()
    {
        int selectionStart = PromptTextBox.SelectionStart;
        int selectionLength = PromptTextBox.SelectionLength;
        string prompt = (PromptTextBox.Text ?? string.Empty).Remove(selectionStart, selectionLength).Insert(selectionStart, Environment.NewLine);
        if (ViewModel is not null)
        {
            ViewModel.Prompt = prompt;
        }

        PromptTextBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
        PromptTextBox.SelectionStart = selectionStart + Environment.NewLine.Length;
        PromptTextBox.SelectionLength = 0;
    }

    /// <summary>Handles the close Tool Panel Click event.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void OnCloseToolPanelClick(object sender, RoutedEventArgs e)
    {
        VSCodexToolWindowViewModel? viewModel = ViewModel;
        if (viewModel is null)
        {
            return;
        }

        viewModel.IsToolPanelOpen = false;
    }

    /// <summary>Handles the run Control Click event.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void OnRunControlClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        ExecuteIfAvailable(ViewModel.IsRunControlInStopMode ? ViewModel.CancelCommand : ViewModel.RunCommand);
    }

    /// <summary>Handles the stop Control Click event.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void OnStopControlClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        ExecuteIfAvailable(ViewModel.CancelCommand);
        e.Handled = true;
    }

    /// <summary>Handles the toggle Voice Input Click event.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void OnToggleVoiceInputClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.ToggleVoiceInput();
        _ = PromptTextBox.Focus();
        PromptTextBox.CaretIndex = PromptTextBox.Text.Length;
        e.Handled = true;
    }

    /// <summary>Handles the reference Suggestion Double Click event.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void OnReferenceSuggestionDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (!(sender is ListBox { SelectedItem: WorkspaceFileReference reference }) || ViewModel is null)
        {
            return;
        }

        string token = reference.ReferenceKey;
        if (string.IsNullOrWhiteSpace(token))
        {
            token = ((reference.ReferenceKind == "selection") ? "#selection" : ($"@{reference.RelativePath}"));
        }

        ViewModel.Prompt = (string.IsNullOrWhiteSpace(ViewModel.Prompt) ? ($"{token} ") : ($"{ViewModel.Prompt.TrimEnd()} {token} "));
        _ = PromptTextBox.Focus();
        PromptTextBox.CaretIndex = PromptTextBox.Text.Length;
        e.Handled = true;
    }

    /// <summary>Handles the prompt Suggestion Double Click event.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void OnPromptSuggestionDoubleClick(object sender, MouseButtonEventArgs e)
    {
        InsertSelectedPromptSuggestion();
        e.Handled = true;
    }

    /// <summary>Handles the history Item Double Click event.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void OnHistoryItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (!(sender is ListBox { SelectedItem: SessionHistoryItem item }) || ViewModel is null)
        {
            return;
        }

        ExecuteIfAvailable(ViewModel.LoadHistoryCommand, item);
        e.Handled = true;
    }

    /// <summary>Handles the prompt Resize Drag Started event.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void OnPromptResizeDragStarted(object sender, DragStartedEventArgs e)
    {
        _isPromptResizeDragging = true;
        _promptResizeThumb = sender as Thumb;
        _promptResizeStartHeight = ResolveCurrentPromptHeight();
        _promptResizeVerticalDelta = 0.0;
        Mouse.OverrideCursor = Cursors.SizeNS;
        ViewModel?.ClosePromptSuggestions();
        e.Handled = true;
    }

    /// <summary>Handles the prompt Resize Drag Delta event.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void OnPromptResizeDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!_isPromptResizeDragging)
        {
            _isPromptResizeDragging = true;
            _promptResizeThumb = sender as Thumb;
            _promptResizeStartHeight = ResolveCurrentPromptHeight();
            _promptResizeVerticalDelta = 0.0;
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

    /// <summary>Handles the prompt Resize Drag Completed event.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void OnPromptResizeDragCompleted(object sender, DragCompletedEventArgs e)
    {
        CompletePromptResize(e);
    }

    /// <summary>Handles the prompt Resize Mouse Left Button Up event.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void OnPromptResizeMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        CompletePromptResize(e);
    }

    /// <summary>Handles the prompt Resize Lost Mouse Capture event.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void OnPromptResizeLostMouseCapture(object sender, MouseEventArgs e)
    {
        CompletePromptResize(e);
    }

    /// <summary>Completes prompt Resize.</summary>
    /// <param name="e">The event.</param>
    private void CompletePromptResize(RoutedEventArgs e)
    {
        FinishPromptResizeSafely(commit: true);
        e.Handled = true;
    }

    /// <summary>Performs the finish Prompt Resize Safely operation.</summary>
    /// <param name="commit">The commit.</param>
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

    /// <summary>Performs the finish Prompt Resize operation.</summary>
    /// <param name="commit">The commit.</param>
    private void FinishPromptResize(bool commit)
    {
        if (!_isPromptResizeDragging)
        {
            Mouse.OverrideCursor = null;
            return;
        }

        _isPromptResizeDragging = false;
        _promptResizeVerticalDelta = 0.0;
        Mouse.OverrideCursor = null;
        Thumb? promptResizeThumb = _promptResizeThumb;
        if (promptResizeThumb?.IsMouseCaptured == true)
        {
            promptResizeThumb.ReleaseMouseCapture();
        }

        _promptResizeThumb = null;
        double height = ((double.IsNaN(PromptTextBox.Height) || PromptTextBox.Height <= 0.0) ? PromptTextBox.ActualHeight : PromptTextBox.Height);
        double clamped = ClampPromptHeight(height);
        ApplyPromptResizeHeight(clamped);
        if (!commit)
        {
            return;
        }

        ViewModel?.CommitInputAreaHeight(clamped);
    }

    /// <summary>Resets prompt Resize State.</summary>
    private void ResetPromptResizeState()
    {
        _isPromptResizeDragging = false;
        _promptResizeVerticalDelta = 0.0;
        Mouse.OverrideCursor = null;
        try
        {
            Thumb? promptResizeThumb = _promptResizeThumb;
            if (promptResizeThumb?.IsMouseCaptured == true)
            {
                promptResizeThumb.ReleaseMouseCapture();
            }
        }
        catch (InvalidOperationException)
        {
        }

        _promptResizeThumb = null;
    }

    /// <summary>Applies prompt Resize Height.</summary>
    /// <param name="height">The height.</param>
    private void ApplyPromptResizeHeight(double height)
    {
        PromptTextBox.SetCurrentValue(FrameworkElement.HeightProperty, height);
        ViewModel?.SetLiveInputAreaHeight(height);
    }

    /// <summary>Resolves current Prompt Height.</summary>
    /// <returns>The resolve Current Prompt Height result.</returns>
    private double ResolveCurrentPromptHeight()
    {
        double currentHeight = ((double.IsNaN(PromptTextBox.Height) || PromptTextBox.Height <= 0.0) ? PromptTextBox.ActualHeight : PromptTextBox.Height);
        if (currentHeight <= 0.0)
        {
            currentHeight = ViewModel?.InputAreaHeight ?? PromptTextBox.MinHeight;
        }

        return ClampPromptHeight(currentHeight);
    }

    /// <summary>Performs the clamp Prompt Height operation.</summary>
    /// <param name="height">The height.</param>
    /// <returns>The clamp Prompt Height result.</returns>
    private double ClampPromptHeight(double height)
    {
        return Math.Max(PromptTextBox.MinHeight, Math.Min(ResolvePromptMaxHeight(), height));
    }

    /// <summary>Resolves prompt Max Height.</summary>
    /// <returns>The resolve Prompt Max Height result.</returns>
    private double ResolvePromptMaxHeight()
    {
        double maxHeight = ((double.IsNaN(PromptTextBox.MaxHeight) || double.IsInfinity(PromptTextBox.MaxHeight)) ? Numeric600Point0 : PromptTextBox.MaxHeight);
        double layoutMax = ((Root.ActualHeight > 0.0) ? Math.Max(Numeric96Point0, Root.ActualHeight * Numeric0Point45) : maxHeight);
        return Math.Min(maxHeight, layoutMax);
    }

    /// <summary>Performs the insert Selected Prompt Suggestion operation.</summary>
    private void InsertSelectedPromptSuggestion()
    {
        if (ViewModel is null)
        {
            return;
        }

        PromptSuggestionItem? suggestion = ViewModel.SelectedPromptSuggestion;
        if (suggestion?.TargetTab == "browse-files")
        {
            BrowseAndInsertFileReferences();
            return;
        }

        ViewModel.InsertPromptSuggestion(suggestion);
        _ = PromptTextBox.Focus();
        PromptTextBox.CaretIndex = PromptTextBox.Text.Length;
    }

    /// <summary>Performs the browse And Insert File References operation.</summary>
    private void BrowseAndInsertFileReferences()
    {
        if (ViewModel is null)
        {
            return;
        }

        OpenFileDialog dialog = new OpenFileDialog
        {
            Title = "Reference files for VSCodex",
            Filter = "Code and text files|*.cs;*.xaml;*.json;*.xml;*.md;*.txt;*.props;*.targets;*.csproj;*.sln;*.slnx;" +
                "*.config;*.yml;*.yaml;*.ps1;*.ts;*.tsx;*.js;*.jsx;*.css;*.html;*.razor|All files|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        ViewModel.InsertFileReferencePaths(dialog.FileNames);
        _ = PromptTextBox.Focus();
        PromptTextBox.CaretIndex = PromptTextBox.Text.Length;
    }

    /// <summary>Moves prompt Suggestion Selection.</summary>
    /// <param name="delta">The delta.</param>
    private void MovePromptSuggestionSelection(int delta)
    {
        if (ViewModel is null || ViewModel.PromptSuggestions.Count == 0)
        {
            return;
        }

        int current = PromptSuggestionList.SelectedIndex;
        if (current < 0)
        {
            current = 0;
        }

        PromptSuggestionList.SelectedIndex = (current + delta + ViewModel.PromptSuggestions.Count) % ViewModel.PromptSuggestions.Count;
        PromptSuggestionList.ScrollIntoView(PromptSuggestionList.SelectedItem);
    }

    /// <summary>Applies visual Studio Theme To Combo Boxes.</summary>
    private void ApplyVisualStudioThemeToComboBoxes()
    {
        foreach (ComboBox item in FindVisualChildren<ComboBox>(this))
        {
            ApplyComboBoxTheme(item);
            item.Loaded -= OnComboBoxLoaded;
            item.Loaded += OnComboBoxLoaded;
            item.DropDownOpened -= OnComboBoxDropDownOpened;
            item.DropDownOpened += OnComboBoxDropDownOpened;
        }
    }

    /// <summary>Handles the combo Box Loaded event.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void OnComboBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ComboBox comboBox)
        {
            return;
        }

        ApplyComboBoxTheme(comboBox);
    }

    /// <summary>Handles the combo Box Drop Down Opened event.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void OnComboBoxDropDownOpened(object sender, EventArgs e)
    {
        OnComboBoxLoaded(sender, new());
    }

    /// <summary>Attempts to attach File Drop.</summary>
    /// <param name="data">The data.</param>
    /// <returns><see langword="true"/> when try Attach File Drop succeeds; otherwise, <see langword="false"/>.</returns>
    private bool TryAttachFileDrop(IDataObject data)
    {
        if (!data.GetDataPresent(DataFormats.FileDrop))
        {
            return false;
        }

        if (!(data.GetData(DataFormats.FileDrop) is string[] files) || files.Length == 0)
        {
            return false;
        }

        ViewModel?.AttachFiles(files.Where(File.Exists));
        return true;
    }

    /// <summary>Attempts to attach Clipboard Image.</summary>
    /// <returns><see langword="true"/> when try Attach Clipboard Image succeeds; otherwise, <see langword="false"/>.</returns>
    private bool TryAttachClipboardImage()
    {
        if (!Clipboard.ContainsImage())
        {
            return false;
        }

        BitmapSource image = Clipboard.GetImage();
        if (image is null)
        {
            return false;
        }

        ViewModel?.AttachClipboardImage(image);
        return true;
    }
}
