using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OsrsMr.Core.Scripting;

namespace OsrsMr
{
    public partial class ScriptStudioWindow : Window
    {
        public ScriptStudioWindow()
        {
            InitializeComponent();
            TxtCodeEditor.Text = CustomScriptTemplates.BasicLoopScriptTemplate;
            UpdateStats();
        }

        private void CmbTemplates_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Optional auto-switch or manual via button
        }

        private void BtnApplyTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (CmbTemplates.SelectedIndex == 0)
            {
                TxtCodeEditor.Text = CustomScriptTemplates.BasicLoopScriptTemplate;
            }
            else if (CmbTemplates.SelectedIndex == 1)
            {
                TxtCodeEditor.Text = CustomScriptTemplates.SkillingScriptTemplate;
            }
            else if (CmbTemplates.SelectedIndex == 2)
            {
                TxtCodeEditor.Text = CustomScriptTemplates.CombatScriptTemplate;
            }

            TxtDiagnostics.Text = $"[Studio] Inserted {((ComboBoxItem)CmbTemplates.SelectedItem).Content} template.";
            UpdateStats();
        }

        private void BtnSaveScript_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string customDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "Custom");
                if (!Directory.Exists(customDir))
                {
                    Directory.CreateDirectory(customDir);
                }

                string filePath = Path.Combine(customDir, "MyCustomScript.cs");
                File.WriteAllText(filePath, TxtCodeEditor.Text);

                TxtFilePath.Text = filePath;
                TxtDiagnostics.Text = $"[Studio] Successfully saved script to: {filePath}\n[Studio] Ready for build or compilation.";
                TxtCompileStatus.Text = "Saved (.cs)";
                TxtCompileStatus.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            }
            catch (Exception ex)
            {
                TxtDiagnostics.Text = $"[Studio Error] Failed to save script: {ex.Message}";
                TxtCompileStatus.Text = "Save Error";
                TxtCompileStatus.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            }
        }

        private void BtnCompileReload_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var scripts = ScriptLoader.LoadAllScripts();
                TxtDiagnostics.Text = $"[Studio] Reloaded script catalog. Found {scripts.Count} active scripts across built-in and external assemblies.";
                TxtCompileStatus.Text = $"{scripts.Count} Scripts Loaded";
                TxtCompileStatus.Foreground = new SolidColorBrush(Color.FromRgb(0, 229, 255));
            }
            catch (Exception ex)
            {
                TxtDiagnostics.Text = $"[Studio Error] Reload failed: {ex.Message}";
                TxtCompileStatus.Text = "Reload Error";
                TxtCompileStatus.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            }
        }

        private void TxtCodeEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateStats();
        }

        private void UpdateStats()
        {
            if (TxtCodeEditor == null || TxtLineStats == null) return;

            int lineCount = TxtCodeEditor.LineCount > 0 ? TxtCodeEditor.LineCount : TxtCodeEditor.Text.Split('\n').Length;
            int charCount = TxtCodeEditor.Text.Length;

            TxtLineStats.Text = $"Lines: {lineCount} | Characters: {charCount}";
        }
    }
}
