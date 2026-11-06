using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Agentstrap.Integrations;
using Agentstrap.Models.Persistable;

namespace Agentstrap.UI.Elements.Settings.Pages;

internal static class ExperiencePageHelpers
{
    public static StackPanel CreatePage(string title, string description)
    {
        var panel = new StackPanel { Margin = new Thickness(22, 12, 24, 24) };
        panel.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, "TextFillColorPrimaryBrush");
        panel.Children.Add(new TextBlock { Text = title, FontFamily = (System.Windows.Media.FontFamily)Application.Current.FindResource("Rubik"), FontSize = 28, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 5) });
        panel.Children.Add(new TextBlock { Text = description, FontSize = 13.5, Foreground = (Brush)Application.Current.FindResource("TextFillColorSecondaryBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 20) });
        return panel;
    }

    public static TextBox Input(string placeholder, double width = 280) => new Wpf.Ui.Controls.TextBox { Width = width, Padding = new Thickness(10, 7, 10, 7), Margin = new Thickness(0, 0, 8, 0), PlaceholderText = placeholder };

    public static System.Windows.Controls.Button Button(string text, RoutedEventHandler click, bool primary = false)
    {
        var button = new System.Windows.Controls.Button { Content = text, Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 8, 0), MinWidth = 86, Cursor = System.Windows.Input.Cursors.Hand };
        button.Click += click;
        if (primary)
        {
            button.Background = (Brush)Application.Current.FindResource("AccentFillColorDefaultBrush");
            button.Foreground = (Brush)Application.Current.FindResource("TextOnAccentFillColorPrimaryBrush");
        }
        return button;
    }

    public static Border Card(UIElement content) => new() { Child = content, Padding = new Thickness(18, 16, 18, 16), Margin = new Thickness(0, 0, 0, 12), CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), BorderBrush = (Brush)Application.Current.FindResource("CardStrokeColorDefaultBrush"), Background = (Brush)Application.Current.FindResource("SolidBackgroundFillColorTertiaryBrush") };

    public static bool AddFromInput(TextBox id, TextBox name, TextBlock status)
    {
        if (!ExperienceLibraryService.TryParsePlaceId(id.Text, out var placeId)) { status.Text = "Enter a Roblox experience URL or numeric place ID."; return false; }
        ExperienceLibraryService.AddOrUpdate(placeId, name.Text);
        var displayName = name.Text.Trim();
        status.Text = $"Added {(displayName.Length > 0 ? displayName : "experience " + placeId)} to your library.";
        id.Clear(); name.Clear();
        return true;
    }

    public static void Launch(ExperienceRecord record)
    {
        if (!ExperienceLibraryService.TryParsePlaceId(record.PlaceId, out var id)) return;
        var start = new ProcessStartInfo { FileName = Paths.Process, UseShellExecute = false };
        start.ArgumentList.Add("-player");
        start.ArgumentList.Add($"roblox://experiences/start?placeId={id}");
        Process.Start(start);
    }

    public static void OpenExperiencePage(ExperienceRecord record)
    {
        if (!ExperienceLibraryService.TryParsePlaceId(record.PlaceId, out var id)) return;
        Process.Start(new ProcessStartInfo { FileName = $"https://www.roblox.com/games/{id}", UseShellExecute = true });
    }
}
