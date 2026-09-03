using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OsrsMr.Core.Scripting;

namespace osrsmr
{
    public partial class ScriptConfigWindow : Window
    {
        private readonly BotScript _script;
        private readonly Action<BotScript>? _onStartRequested;
        private readonly List<Action> _applyActions = new();
        private readonly List<Action> _resetActions = new();

        public ScriptConfigWindow(BotScript script, Action<BotScript>? onStartRequested = null)
        {
            InitializeComponent();
            _script = script ?? throw new ArgumentNullException(nameof(script));
            _onStartRequested = onStartRequested;

            PopulateScriptHeader();
            BuildSettingsForm();
        }

        private void PopulateScriptHeader()
        {
            var manifest = _script.GetType().GetCustomAttribute<ScriptManifestAttribute>();
            if (manifest != null)
            {
                ScriptNameText.Text = manifest.Name;
                ScriptAuthorText.Text = $"by {manifest.Author} • v{manifest.Version}";
                ScriptCategoryText.Text = manifest.Category.ToString().ToUpperInvariant();
                ScriptDescriptionText.Text = string.IsNullOrWhiteSpace(manifest.Description)
                    ? "No detailed description provided for this bot script."
                    : manifest.Description;
            }
            else
            {
                ScriptNameText.Text = _script.GetType().Name;
                ScriptAuthorText.Text = "Custom Script";
                ScriptCategoryText.Text = "GENERAL";
                ScriptDescriptionText.Text = "No manifest metadata provided.";
            }

            Title = $"{ScriptNameText.Text} - Setup & Configuration";
        }

        private void BuildSettingsForm()
        {
            SettingsFormPanel.Children.Clear();
            _applyActions.Clear();
            _resetActions.Clear();

            var props = _script.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetCustomAttribute<ScriptSettingAttribute>() != null)
                .OrderBy(p => p.GetCustomAttribute<ScriptSettingAttribute>()!.Order)
                .ToList();

            if (props.Count == 0)
            {
                var emptyCard = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(30, 30, 34)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(14),
                    Margin = new Thickness(0, 10, 0, 10)
                };
                var emptyText = new TextBlock
                {
                    Text = "ℹ️ This script is ready to run with automated default settings. No additional manual configuration is required.",
                    Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 160)),
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap
                };
                emptyCard.Child = emptyText;
                SettingsFormPanel.Children.Add(emptyCard);
                return;
            }

            // Group properties by Group name
            var groups = props.GroupBy(p => p.GetCustomAttribute<ScriptSettingAttribute>()!.Group ?? "General");

            foreach (var group in groups)
            {
                var groupHeader = new TextBlock
                {
                    Text = $"📁 {group.Key.ToUpperInvariant()} SETTINGS",
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 229, 255)),
                    FontWeight = FontWeights.Bold,
                    FontSize = 12,
                    Margin = new Thickness(2, 8, 2, 6)
                };
                SettingsFormPanel.Children.Add(groupHeader);

                foreach (var prop in group)
                {
                    var attr = prop.GetCustomAttribute<ScriptSettingAttribute>()!;
                    var settingCard = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(30, 30, 34)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(55, 55, 60)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(10, 8, 10, 8),
                        Margin = new Thickness(0, 0, 0, 8)
                    };

                    var settingStack = new StackPanel();

                    var labelText = new TextBlock
                    {
                        Text = attr.Label,
                        Foreground = Brushes.White,
                        FontWeight = FontWeights.SemiBold,
                        FontSize = 12
                    };
                    settingStack.Children.Add(labelText);

                    if (!string.IsNullOrWhiteSpace(attr.Description))
                    {
                        var descText = new TextBlock
                        {
                            Text = attr.Description,
                            Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 140)),
                            FontSize = 10,
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 2, 0, 6)
                        };
                        settingStack.Children.Add(descText);
                    }

                    // 1. Enum dropdown
                    if (prop.PropertyType.IsEnum)
                    {
                        var combo = new ComboBox
                        {
                            Height = 30,
                            Margin = new Thickness(0, 4, 0, 2)
                        };
                        var enumValues = Enum.GetValues(prop.PropertyType);
                        int selectedIndex = 0;
                        object? currentVal = prop.GetValue(_script);

                        int i = 0;
                        foreach (var val in enumValues)
                        {
                            combo.Items.Add(val.ToString());
                            if (val.Equals(currentVal))
                                selectedIndex = i;
                            i++;
                        }
                        if (combo.Items.Count > 0)
                            combo.SelectedIndex = selectedIndex;

                        _applyActions.Add(() =>
                        {
                            if (combo.SelectedIndex >= 0 && combo.SelectedIndex < enumValues.Length)
                            {
                                var selectedEnum = enumValues.GetValue(combo.SelectedIndex);
                                prop.SetValue(_script, selectedEnum);
                            }
                        });

                        _resetActions.Add(() =>
                        {
                            var def = attr.DefaultValue ?? enumValues.GetValue(0);
                            if (def != null)
                            {
                                int idx = 0;
                                foreach (var v in enumValues)
                                {
                                    if (v.ToString() == def.ToString())
                                    {
                                        combo.SelectedIndex = idx;
                                        break;
                                    }
                                    idx++;
                                }
                            }
                        });

                        settingStack.Children.Add(combo);
                    }
                    // 2. String Options Dropdown
                    else if (attr.Options != null && attr.Options.Length > 0)
                    {
                        var combo = new ComboBox
                        {
                            Height = 30,
                            Margin = new Thickness(0, 4, 0, 2)
                        };
                        string currentStr = prop.GetValue(_script)?.ToString() ?? "";
                        int selectedIdx = 0;

                        for (int i = 0; i < attr.Options.Length; i++)
                        {
                            combo.Items.Add(attr.Options[i]);
                            if (string.Equals(attr.Options[i], currentStr, StringComparison.OrdinalIgnoreCase))
                                selectedIdx = i;
                        }
                        if (combo.Items.Count > 0)
                            combo.SelectedIndex = selectedIdx;

                        _applyActions.Add(() =>
                        {
                            if (combo.SelectedItem != null)
                                prop.SetValue(_script, combo.SelectedItem.ToString());
                        });

                        _resetActions.Add(() =>
                        {
                            string def = attr.DefaultValue?.ToString() ?? attr.Options[0];
                            for (int i = 0; i < combo.Items.Count; i++)
                            {
                                if (string.Equals(combo.Items[i]?.ToString(), def, StringComparison.OrdinalIgnoreCase))
                                {
                                    combo.SelectedIndex = i;
                                    break;
                                }
                            }
                        });

                        settingStack.Children.Add(combo);
                    }
                    // 3. Boolean CheckBox
                    else if (prop.PropertyType == typeof(bool))
                    {
                        bool currentVal = (bool)(prop.GetValue(_script) ?? false);
                        var check = new CheckBox
                        {
                            Content = "Enabled",
                            IsChecked = currentVal,
                            Margin = new Thickness(0, 4, 0, 2)
                        };

                        _applyActions.Add(() => prop.SetValue(_script, check.IsChecked == true));
                        _resetActions.Add(() =>
                        {
                            bool def = attr.DefaultValue is bool b ? b : true;
                            check.IsChecked = def;
                        });

                        settingStack.Children.Add(check);
                    }
                    // 4. Integer / Number
                    else if (prop.PropertyType == typeof(int) || prop.PropertyType == typeof(double))
                    {
                        var text = new TextBox
                        {
                            Text = prop.GetValue(_script)?.ToString() ?? "0",
                            Height = 28,
                            Margin = new Thickness(0, 4, 0, 2)
                        };

                        _applyActions.Add(() =>
                        {
                            if (prop.PropertyType == typeof(int) && int.TryParse(text.Text, out int intVal))
                                prop.SetValue(_script, intVal);
                            else if (prop.PropertyType == typeof(double) && double.TryParse(text.Text, out double dblVal))
                                prop.SetValue(_script, dblVal);
                        });

                        _resetActions.Add(() =>
                        {
                            text.Text = attr.DefaultValue?.ToString() ?? "0";
                        });

                        settingStack.Children.Add(text);
                    }
                    // 5. General String
                    else
                    {
                        var text = new TextBox
                        {
                            Text = prop.GetValue(_script)?.ToString() ?? "",
                            Height = 28,
                            Margin = new Thickness(0, 4, 0, 2)
                        };

                        _applyActions.Add(() => prop.SetValue(_script, text.Text));
                        _resetActions.Add(() =>
                        {
                            text.Text = attr.DefaultValue?.ToString() ?? "";
                        });

                        settingStack.Children.Add(text);
                    }

                    settingCard.Child = settingStack;
                    SettingsFormPanel.Children.Add(settingCard);
                }
            }
        }

        private void ApplyAllSettings()
        {
            foreach (var action in _applyActions)
            {
                try { action.Invoke(); }
                catch { /* ignore binding errors */ }
            }
        }

        private void SaveApplyBtn_Click(object sender, RoutedEventArgs e)
        {
            ApplyAllSettings();
            DialogResult = true;
            Close();
        }

        private void StartBotBtn_Click(object sender, RoutedEventArgs e)
        {
            ApplyAllSettings();
            DialogResult = true;
            _onStartRequested?.Invoke(_script);
            Close();
        }

        private void ResetDefaultsBtn_Click(object sender, RoutedEventArgs e)
        {
            foreach (var action in _resetActions)
            {
                try { action.Invoke(); }
                catch { /* ignore */ }
            }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
