using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VSCodexExtension.Models;
using VSCodexExtension.ViewModels;

namespace VSCodexExtension.Views
{
    public partial class VSCodexToolWindowControl : UserControl
    {
        public VSCodexToolWindowControl()
        {
            InitializeComponent();
            DataObject.AddPastingHandler(PromptTextBox, OnPromptPasting);
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

            if (e.Key == Key.Escape)
            {
                ExecuteIfAvailable(ViewModel.CancelCommand);
                e.Handled = true;
            }
        }

        private void OnReferenceSuggestionDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is ListBox listBox) || !(listBox.SelectedItem is WorkspaceFileReference reference) || ViewModel == null)
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

        private bool TryAttachFileDrop(IDataObject data)
        {
            if (!data.GetDataPresent(DataFormats.FileDrop))
            {
                return false;
            }

            var files = data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0)
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
}
