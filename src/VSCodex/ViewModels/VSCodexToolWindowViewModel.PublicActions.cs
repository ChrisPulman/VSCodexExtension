// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI;

namespace VSCodex.ViewModels;

/// <summary>Exposes imperative actions used by the VSCodex tool-window control.</summary>
public sealed partial class VSCodexToolWindowViewModel
{
    /// <summary>Sets live Input Area Height.</summary>
    /// <param name="value">The value.</param>
    public void SetLiveInputAreaHeight(double value)
    {
        _ = SetInputAreaHeight(value);
    }

    /// <summary>Performs the commit Input Area Height operation.</summary>
    /// <param name="value">The value.</param>
    public void CommitInputAreaHeight(double value)
    {
        double clamped = SetInputAreaHeight(value);
        SaveInputAreaHeight(clamped);
    }

    /// <summary>Performs the toggle Voice Input operation.</summary>
    public void ToggleVoiceInput()
    {
        if (_voiceInput.IsListening)
        {
            _voiceInput.Stop();
        }
        else
        {
            _voiceInput.Start();
        }

        this.RaisePropertyChanged();
        this.RaisePropertyChanged();
    }

    /// <summary>Performs the show History operation.</summary>
    public void ShowHistory()
    {
        if (IsRunning)
        {
            Status = "VSCodex history is locked while a task is running";
            return;
        }

        RefreshHistory();
        IsToolPanelOpen = true;
        SelectedToolTabIndex = 0;
        Status = "VSCodex history";
    }

    /// <summary>Performs the attach Files operation.</summary>
    /// <param name="fileNames">The file Names.</param>
    public void AttachFiles(IEnumerable<string> fileNames)
    {
        int count = 0;
        foreach (string file in fileNames ?? Enumerable.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(file) && File.Exists(file))
            {
                Attachments.Add(new CodexAttachment
                {
                    Path = file,
                    Kind = InferAttachmentKind(file)
                });
                count++;
            }
        }

        if (count <= 0)
        {
            return;
        }

        Status = $"Attached {count} file(s)";
    }

    /// <summary>Performs the insert File Reference Paths operation.</summary>
    /// <param name="fileNames">The file Names.</param>
    public void InsertFileReferencePaths(IEnumerable<string> fileNames)
    {
        List<string> tokens = (from file in (fileNames ?? Enumerable.Empty<string>()).Where(File.Exists)
                               select _workspace.SearchFiles(file, 1).FirstOrDefault()?.ReferenceKey ?? FormatPromptFileReference(file) into token
                               where !string.IsNullOrWhiteSpace(token)
                               select token).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (tokens.Count == 0)
        {
            return;
        }

        string prompt = Prompt ?? string.Empty;
        Prompt = (string.IsNullOrWhiteSpace(prompt) ? ($"{string.Join(" ", tokens)} ") : ($"{prompt.TrimEnd()} {string.Join(" ", tokens)} "));
        ClosePromptSuggestions();
        Status = $"Referenced {tokens.Count} file(s)";
    }

    /// <summary>Performs the attach Clipboard Image operation.</summary>
    /// <param name="image">The image.</param>
    public void AttachClipboardImage(BitmapSource image)
    {
        if (image is null)
        {
            return;
        }

        string path = Path.Combine(LocalPaths.AttachmentsRoot, $"clipboard-{_timeProvider.GetLocalNow():yyyyMMdd-HHmmss-fff}.png");
        using (FileStream stream = File.Create(path))
        {
            PngBitmapEncoder pngBitmapEncoder = new();
            pngBitmapEncoder.Frames.Add(BitmapFrame.Create(image));
            pngBitmapEncoder.Save(stream);
        }

        Attachments.Add(new CodexAttachment
        {
            Path = path,
            Kind = "image"
        });
        Status = "Attached clipboard image";
    }

    /// <summary>Performs the insert Prompt Suggestion operation.</summary>
    /// <param name="suggestion">The suggestion.</param>
    public void InsertPromptSuggestion(PromptSuggestionItem? suggestion)
    {
        if (suggestion is null || string.IsNullOrWhiteSpace(suggestion.InsertText))
        {
            return;
        }

        string prompt = Prompt ?? string.Empty;
        int tokenStart = LastPromptTokenStart(prompt);
        Prompt = tokenStart >= 0
            ? prompt.Remove(tokenStart) + suggestion.InsertText
            : AppendPromptSuggestion(prompt, suggestion.InsertText);
        IsPromptSuggestionOpen = false;
        Status = $"Inserted {suggestion.DisplayText}";
    }

    /// <summary>Closes prompt Suggestions.</summary>
    public void ClosePromptSuggestions()
    {
        IsPromptSuggestionOpen = false;
    }

    /// <summary>Performs the dispose operation.</summary>
    public void Dispose()
    {
        _lifetime.Cancel();
        FlushPendingModelSettingsSave();
        _subscriptions.Dispose();
        _lifetime.Dispose();
    }

    /// <summary>Appends a prompt Suggestion.</summary>
    /// <param name="prompt">The prompt.</param>
    /// <param name="insertText">The insert Text.</param>
    /// <returns>The prompt with the suggestion appended.</returns>
    private string AppendPromptSuggestion(string prompt, string insertText)
    {
        return string.IsNullOrWhiteSpace(prompt) ? insertText : $"{prompt.TrimEnd()} {insertText}";
    }
}
