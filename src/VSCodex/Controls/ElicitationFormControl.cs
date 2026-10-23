// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.VisualStudio.PlatformUI;
using VSCodex.Models;

namespace VSCodex.Controls;

/// <summary>Renders supported MCP form fields using native, keyboard-accessible WPF editors.</summary>
public sealed class ElicitationFormControl : StackPanel
{
    /// <summary>Identifies the form dependency property.</summary>
    public static readonly DependencyProperty FormProperty = DependencyProperty.Register(
        nameof(Form),
        typeof(ElicitationForm),
        typeof(ElicitationFormControl),
        new(null, OnFormChanged));

    /// <summary>Minimum editor width.</summary>
    private const int EditorMinimumWidth = 100;

    /// <summary>Vertical space between editors.</summary>
    private const int EditorSpacing = 6;

    /// <summary>Vertical prompt margin.</summary>
    private const int PromptMargin = 4;

    /// <summary>Gets or sets the elicitation to display.</summary>
    public ElicitationForm? Form { get => (ElicitationForm?)GetValue(FormProperty); set => SetValue(FormProperty, value); }

    /// <summary>Performs OnFormChanged.</summary>
    /// <param name="sender">The sender value.</param>
    /// <param name="args">The args value.</param>
    private static void OnFormChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var panel = (ElicitationFormControl)sender;
        panel.Children.Clear();
        if (args.NewValue is not ElicitationForm form)
        {
            return;
        }

        _ = panel.Children.Add(new TextBlock { Text = form.Message, TextWrapping = TextWrapping.Wrap, Margin = new(0, PromptMargin, 0, PromptMargin) });
        if (form.UnsupportedReason.Length > 0)
        {
            _ = panel.Children.Add(new TextBlock { Text = form.UnsupportedReason, TextWrapping = TextWrapping.Wrap });
            return;
        }

        foreach (ElicitationField field in form.Fields)
        {
            FrameworkElement editor;
            if (field.Choices.Count > 0)
            {
                var choices = new ComboBox { ItemsSource = field.Choices, MinWidth = EditorMinimumWidth };
                _ = choices.SetBinding(
                    ComboBox.SelectedItemProperty,
                    new Binding(nameof(ElicitationField.Value)) { Source = field, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
                editor = choices;
            }
            else
            {
                var text = new TextBox { MinWidth = EditorMinimumWidth };
                _ = text.SetBinding(
                    TextBox.TextProperty,
                    new Binding(nameof(ElicitationField.Value)) { Source = field, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
                editor = text;
            }

            editor.Margin = new(0, 0, 0, EditorSpacing);
            System.Windows.Automation.AutomationProperties.SetName(editor, field.Label);
            var label = new Label { Content = field.Label, Target = editor };
            label.SetResourceReference(Control.ForegroundProperty, EnvironmentColors.ToolWindowTextBrushKey);
            _ = panel.Children.Add(label);
            if (field.Description.Length > 0)
            {
                _ = panel.Children.Add(new TextBlock { Text = field.Description, TextWrapping = TextWrapping.Wrap });
            }

            _ = panel.Children.Add(editor);
            AddOptionalEmptyToggle(panel, field);
        }
    }

    /// <summary>Adds an explicit choice between omitted and empty optional strings.</summary>
    /// <param name="panel">The form panel.</param>
    /// <param name="field">The field being edited.</param>
    private static void AddOptionalEmptyToggle(StackPanel panel, ElicitationField field)
    {
        if (!field.CanIncludeEmptyValue)
        {
            return;
        }

        var includeEmpty = new CheckBox { Content = new TextBlock { Text = "Send an empty string instead of omitting this field", TextWrapping = TextWrapping.Wrap } };
        _ = includeEmpty.SetBinding(CheckBox.IsCheckedProperty, new Binding(nameof(ElicitationField.IncludeEmptyValue)) { Source = field, Mode = BindingMode.TwoWay });
        System.Windows.Automation.AutomationProperties.SetName(includeEmpty, $"Include an empty value for {field.Name}");
        _ = panel.Children.Add(includeEmpty);
    }
}
