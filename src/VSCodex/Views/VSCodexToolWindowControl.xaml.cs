using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.VisualStudio.PlatformUI;
using VSCodex.Models;
using VSCodex.ViewModels;

namespace VSCodex.Views;

public partial class VSCodexToolWindowControl : UserControl
{
    public VSCodexToolWindowControl()
    {
        InitializeComponent();
        DataObject.AddPastingHandler(PromptTextBox, OnPromptPasting);
        Loaded += (_, _) => ApplyVisualStudioThemeToComboBoxes();
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

            if (e.Key == Key.Tab || e.Key == Key.Enter)
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

    private void OnCloseSettingsPanelClick(object sender, RoutedEventArgs e) => ViewModel?.IsSettingsPanelOpen = false;

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
}
