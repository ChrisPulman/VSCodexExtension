// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;

namespace VSCodex.Options;

/// <summary>Composes property metadata into a searchable editor with an isolated editing transaction.</summary>
internal sealed class OptionsPageControl : UserControl
{
    /// <summary>Layout spacing or size of 6 device independent pixels.</summary>
    private const int Layout6 = 6;

    /// <summary>Layout spacing or size of 8 device independent pixels.</summary>
    private const int Layout8 = 8;

    /// <summary>Layout spacing or size of 10 device independent pixels.</summary>
    private const int Layout10 = 10;

    /// <summary>Layout spacing or size of 12 device independent pixels.</summary>
    private const int Layout12 = 12;

    /// <summary>Layout spacing or size of 16 device independent pixels.</summary>
    private const int Layout16 = 16;

    /// <summary>Layout spacing or size of 20 device independent pixels.</summary>
    private const int Layout20 = 20;

    /// <summary>The persisted automation model.</summary>
    private readonly VSCodexOptionsModel _model;

    /// <summary>Provides _draft.</summary>
    private readonly VSCodexOptionsModel _draft = new();

    /// <summary>Provides _editors.</summary>
    private readonly List<SettingEditor> _editors = new();

    /// <summary>Provides _groups.</summary>
    private readonly StackPanel _groups = new();

    /// <summary>Provides _search.</summary>
    private readonly TextBox _search = new();

    /// <summary>Provides _status.</summary>
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };

    /// <summary>Initializes a new instance of the <see cref="OptionsPageControl"/> class.</summary>
    /// <param name="model">The automation model committed when Apply succeeds.</param>
    internal OptionsPageControl(VSCodexOptionsModel model)
    {
        _model = model;
        AddThemeStyles();
        SetResourceReference(BackgroundProperty, EnvironmentColors.ToolWindowBackgroundBrushKey);
        SetResourceReference(ForegroundProperty, EnvironmentColors.ToolWindowTextBrushKey);
        var root = new DockPanel { Margin = new(Layout16), LastChildFill = true };
        var header = new StackPanel { Margin = new(0, 0, 0, Layout12) };
        _ = header.Children.Add(new TextBlock { Text = "VSCodex settings", FontSize = Layout20, FontWeight = FontWeights.SemiBold });
        _ = header.Children.Add(new TextBlock { Text = "Search settings by name, category, or description.", Margin = new(0, Layout6, 0, Layout8), TextWrapping = TextWrapping.Wrap });
        AutomationProperties.SetName(_search, "Search VSCodex settings");
        _search.ToolTip = "Search settings";
        _search.Padding = new(Layout6);
        _ = header.Children.Add(_search);
        _ = header.Children.Add(_status);
        DockPanel.SetDock(header, Dock.Top);
        _ = root.Children.Add(header);
        _ = root.Children.Add(new ScrollViewer { Content = _groups, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        Content = root;
        _search.TextChanged += (_, _) => Filter();
        Reload();
    }

    /// <summary>Provides Reload.</summary>
    internal void Reload()
    {
        Copy(_model, _draft);
        _editors.Clear();
        _groups.Children.Clear();
        foreach (var category in Properties().GroupBy(property => property.Category).OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase))
        {
            var panel = new StackPanel();
            var group = new GroupBox { Header = category.Key, Content = panel, Margin = new(0, 0, 0, Layout12), Padding = new(Layout10) };
            _ = _groups.Children.Add(group);
            foreach (var property in category)
            {
                var editor = new SettingEditor(_draft, property);
                _editors.Add(editor);
                _ = panel.Children.Add(editor.Row);
            }
        }

        Filter();
    }

    /// <summary>Validates all editors and commits the draft atomically.</summary>
    /// <returns>Whether all settings are valid.</returns>
    internal bool TryApply()
    {
        var invalid = _editors.Where(editor => !editor.Validate()).ToList();
        if (invalid.Count > 0)
        {
            _search.Clear();
            _status.Text = "Correct the highlighted settings before applying changes.";
            invalid[0].Row.BringIntoView();
            _ = invalid[0].Input.Focus();
            return false;
        }

        Copy(_draft, _model);
        _status.Text = string.Empty;
        return true;
    }

    /// <summary>Gets the visible setting descriptors.</summary>
    /// <returns>The visible settings.</returns>
    private static IEnumerable<PropertyDescriptor> Properties() => TypeDescriptor.GetProperties(typeof(VSCodexOptionsModel)).Cast<PropertyDescriptor>().Where(property => property.IsBrowsable);

    /// <summary>Copies editable settings between models.</summary>
    /// <param name="source">The source settings.</param>
    /// <param name="target">The destination settings.</param>
    private static void Copy(VSCodexOptionsModel source, VSCodexOptionsModel target)
    {
        foreach (var property in Properties().Where(property => !property.IsReadOnly))
        {
            property.SetValue(target, property.GetValue(source));
        }
    }

    /// <summary>Provides AddThemeStyles.</summary>
    private void AddThemeStyles()
    {
        foreach (var type in new[] { typeof(TextBox), typeof(CheckBox), typeof(Button), typeof(GroupBox) })
        {
            var style = new Style(type);
            style.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension(EnvironmentColors.ToolWindowTextBrushKey)));
            style.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension(EnvironmentColors.ToolWindowBackgroundBrushKey)));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, new DynamicResourceExtension(EnvironmentColors.ToolWindowBorderBrushKey)));
            Resources.Add(type, style);
        }
    }

    /// <summary>Provides Filter.</summary>
    private void Filter()
    {
        var query = _search.Text.Trim();
        foreach (var editor in _editors)
        {
            editor.Row.Visibility = editor.SearchText.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        foreach (GroupBox group in _groups.Children)
        {
            group.Visibility = ((StackPanel)group.Content).Children.Cast<FrameworkElement>().Any(row => row.Visibility == Visibility.Visible) ? Visibility.Visible : Visibility.Collapsed;
        }

        var count = _editors.Count(editor => editor.Row.Visibility == Visibility.Visible);
        _status.Text = count == 0 ? "No settings match your search." : $"{count} settings";
    }

    /// <summary>Converts persisted transport identities into concise user-facing choices.</summary>
    private sealed class TransportDisplayConverter : IValueConverter
    {
        /// <summary>Formats the transport label.</summary>
        /// <param name="value">The transport identity.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="parameter">The conversion parameter.</param>
        /// <param name="culture">The display culture.</param>
        /// <returns>The display label.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
        {
            CodexTransportKind.SdkBridge => "Codex app-server",
            CodexTransportKind.CliFallback => "CLI only",
            _ => value?.ToString() ?? string.Empty,
        };

        /// <summary>Keeps display formatting separate from the editor's typed selection.</summary>
        /// <param name="value">The label.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="parameter">The conversion parameter.</param>
        /// <param name="culture">The display culture.</param>
        /// <returns>The no-write binding sentinel.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    /// <summary>Composes a setting descriptor, editor, reset action, and converter context.</summary>
    private sealed class SettingEditor : ITypeDescriptorContext
    {
        /// <summary>Layout spacing or size of 2 device independent pixels.</summary>
        private const int Layout2 = 2;

        /// <summary>Layout spacing or size of 3 device independent pixels.</summary>
        private const int Layout3 = 3;

        /// <summary>Layout spacing or size of 4 device independent pixels.</summary>
        private const int Layout4 = 4;

        /// <summary>Layout spacing or size of 5 device independent pixels.</summary>
        private const int Layout5 = 5;

        /// <summary>Layout spacing or size of 14 device independent pixels.</summary>
        private const int Layout14 = 14;

        /// <summary>Layout spacing or size of 72 device independent pixels.</summary>
        private const int Layout72 = 72;

        /// <summary>Layout spacing or size of 120 device independent pixels.</summary>
        private const int Layout120 = 120;

        /// <summary>Provides _draft.</summary>
        private readonly VSCodexOptionsModel _draft;

        /// <summary>Provides _property.</summary>
        private readonly PropertyDescriptor _property;

        /// <summary>Provides _error.</summary>
        private readonly TextBlock _error = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, Layout4, 0, 0) };

        /// <summary>Provides _defaultValue.</summary>
        private readonly object? _defaultValue;

        /// <summary>Initializes a new instance of the <see cref="SettingEditor"/> class.</summary>
        /// <param name="draft">The isolated editing model.</param>
        /// <param name="property">The setting descriptor.</param>
        internal SettingEditor(VSCodexOptionsModel draft, PropertyDescriptor property)
        {
            _draft = draft;
            _property = property;
            _defaultValue = property.GetValue(new VSCodexOptionsModel());
            SearchText = $"{property.DisplayName} {property.Category} {property.Description}";
            Row = new StackPanel { Margin = new(0, Layout4, 0, Layout14) };
            _ = Row.Children.Add(new TextBlock { Text = property.DisplayName, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            _ = Row.Children.Add(new TextBlock { Text = property.Description, TextWrapping = TextWrapping.Wrap, Margin = new(0, Layout3, 0, Layout6) });
            Input = CreateInput();
            Input.IsEnabled = !property.IsReadOnly;
            AutomationProperties.SetName(Input, property.DisplayName);
            AutomationProperties.SetHelpText(Input, property.Description);
            _ = Row.Children.Add(Input);
            _error.SetResourceReference(TextBlock.ForegroundProperty, EnvironmentColors.ToolWindowTextBrushKey);
            _ = Row.Children.Add(_error);
            if (!property.IsReadOnly)
            {
                var reset = new Button
                {
                    Content = "Reset to default",
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new(0, Layout5, 0, 0),
                    Padding = new(Layout6, Layout2, Layout6, Layout2)
                };
                AutomationProperties.SetName(reset, $"Reset {property.DisplayName} to default");
                reset.Click += (_, _) =>
                {
                    property.SetValue(draft, _defaultValue);
                    ReadValue();
                    _ = Validate();
                };
                _ = Row.Children.Add(reset);
            }

            ReadValue();
        }

        /// <inheritdoc/>
        public IContainer? Container => null;

        /// <inheritdoc/>
        public object Instance => _draft;

        /// <inheritdoc/>
        public PropertyDescriptor PropertyDescriptor => _property;

        /// <summary>Gets the Row.</summary>
        internal StackPanel Row { get; }

        /// <summary>Gets the Input.</summary>
        internal Control Input { get; }

        /// <summary>Gets the SearchText.</summary>
        internal string SearchText { get; }

        /// <inheritdoc/>
        public object? GetService(Type serviceType) => null;

        /// <inheritdoc/>
        public bool OnComponentChanging() => true;

        /// <inheritdoc/>
        public void OnComponentChanged()
        {
        }

        /// <summary>Validates the current editor value and updates the draft.</summary>
        /// <returns>Whether the editor has a valid value.</returns>
        internal bool Validate()
        {
            if (_property.IsReadOnly)
            {
                return true;
            }

            try
            {
                var value = ReadEditorValue();
                ValidateConstraints(value);
                _property.SetValue(_draft, value);
                _error.Text = string.Empty;
                AutomationProperties.SetHelpText(Input, _property.Description);
                return true;
            }
            catch (Exception exception) when (exception is FormatException || exception is ArgumentException || exception is NotSupportedException || exception is OverflowException)
            {
                _error.Text = $"Invalid value: {exception.Message}";
                AutomationProperties.SetHelpText(Input, $"{_property.Description} {_error.Text}");
                return false;
            }
        }

        /// <summary>Converts the editor text using the property's converter.</summary>
        /// <returns>The converted setting value.</returns>
        private object ReadEditorValue()
        {
            object? value = Input switch
            {
                CheckBox check => check.IsChecked == true,
                ComboBox combo when !combo.IsEditable => combo.SelectedItem,
                ComboBox combo => combo.Text,
                TextBox text => text.Text,
                _ => null,
            };
            if (value is string textValue && _property.PropertyType != typeof(string))
            {
                value = _property.Converter.ConvertFrom(this, CultureInfo.CurrentCulture, textValue);
            }

            return value ?? throw new FormatException("Choose a value.");
        }

        /// <summary>Checks setting-specific limits without silently normalizing invalid values.</summary>
        /// <param name="value">The converted setting value.</param>
        private void ValidateConstraints(object value)
        {
            if (_property.PropertyType.IsEnum && !Enum.IsDefined(_property.PropertyType, value))
            {
                throw new FormatException("Choose a supported value.");
            }

            if (_property.Name == nameof(VSCodexOptionsModel.DefaultMaxAgentConcurrency) && (int)value < 1)
            {
                throw new FormatException("Enter a whole number of at least 1.");
            }

            if (_property.Name == nameof(VSCodexOptionsModel.DefaultInputAreaHeight))
            {
                var height = (double)value;
                if (double.IsNaN(height) || double.IsInfinity(height) || height <= 0)
                {
                    throw new FormatException("Enter a finite height greater than zero.");
                }
            }

            ValidateStandardValue(value);
        }

        /// <summary>Checks exclusive converter choices against the current model.</summary>
        /// <param name="value">The requested setting value.</param>
        private void ValidateStandardValue(object value)
        {
            if (!_property.Converter.GetStandardValuesExclusive(this))
            {
                return;
            }

            var values = _property.Converter.GetStandardValues(this);
            if (values?.Cast<object>().Contains(value) != false)
            {
                return;
            }

            throw new FormatException("Choose a value supported by the current model.");
        }

        /// <summary>Creates the appropriate accessible setting editor.</summary>
        /// <returns>The setting input control.</returns>
        private Control CreateInput()
        {
            if (_property.PropertyType == typeof(bool))
            {
                var check = new CheckBox { Content = "Enabled" };
                check.Checked += (_, _) => _ = Validate();
                check.Unchecked += (_, _) => _ = Validate();
                return check;
            }

            if (_property.Converter.GetStandardValuesSupported(this))
            {
                var combo = new ComboBox { IsEditable = !_property.Converter.GetStandardValuesExclusive(this), MinWidth = Layout120, HorizontalAlignment = HorizontalAlignment.Stretch };
                ApplyTransportDisplayTemplate(combo);
                combo.SetResourceReference(FrameworkElement.StyleProperty, VsResourceKeys.ComboBoxStyleKey);
                combo.SetResourceReference(ItemsControl.ItemContainerStyleProperty, VsResourceKeys.ComboBoxItemStyleKey);
                combo.ItemsSource = _property.Converter.GetStandardValues(this);
                combo.DropDownOpened += (_, _) =>
                {
                    var current = _property.GetValue(_draft);
                    combo.ItemsSource = _property.Converter.GetStandardValues(this);
                    combo.SelectedItem = current;
                    if (!combo.IsEditable)
                    {
                        return;
                    }

                    combo.Text = Convert.ToString(current, CultureInfo.CurrentCulture);
                };
                combo.SelectionChanged += (_, _) => _ = Validate();
                return combo;
            }

            var multiline = _property.Name == nameof(VSCodexOptionsModel.CustomModels)
                || _property.Name == nameof(VSCodexOptionsModel.SkillRoots)
                || _property.Name == nameof(VSCodexOptionsModel.EnabledSkillPaths);
            var text = new TextBox
            {
                Padding = new(Layout5),
                AcceptsReturn = multiline,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = multiline ? Layout72 : 0,
                VerticalScrollBarVisibility = multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Hidden
            };
            text.TextChanged += (_, _) => _ = Validate();
            return text;
        }

        /// <summary>Displays transport choices without changing their persisted enum values.</summary>
        /// <param name="combo">The option editor.</param>
        private void ApplyTransportDisplayTemplate(ComboBox combo)
        {
            if (_property.PropertyType != typeof(CodexTransportKind))
            {
                return;
            }

            var text = new FrameworkElementFactory(typeof(TextBlock));
            text.SetBinding(TextBlock.TextProperty, new Binding { Converter = new TransportDisplayConverter() });
            combo.ItemTemplate = new DataTemplate { VisualTree = text };
        }

        /// <summary>Provides ReadValue.</summary>
        private void ReadValue()
        {
            var value = _property.GetValue(_draft);
            if (Input is CheckBox check)
            {
                check.IsChecked = (bool)value!;
                return;
            }

            if (Input is TextBox text)
            {
                text.Text = Convert.ToString(value, CultureInfo.CurrentCulture);
                return;
            }

            var combo = (ComboBox)Input;
            combo.SelectedItem = value;
            if (!combo.IsEditable)
            {
                return;
            }

            combo.Text = Convert.ToString(value, CultureInfo.CurrentCulture);
        }
    }
}
