using System.Windows;
using System.Windows.Controls;
using Agentstrap.Integrations;
using Wpf.Ui.Controls;
using TextBox = System.Windows.Controls.TextBox;

namespace Agentstrap.UI.Elements.Settings.Pages;

public sealed class GameRoulettePage : UiPage
{
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 16, 0, 0), FontSize = 14 };
    private readonly TextBlock _result = new() { FontSize = 22, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBox _id = ExperiencePageHelpers.Input("Roblox URL or place ID", 330);
    private readonly TextBox _name = ExperiencePageHelpers.Input("Optional experience name", 230);
    private readonly TextBox _search = ExperiencePageHelpers.Input("Filter saved experiences", 240);
    private readonly CheckBox _favorites = new() { Content = "Favorites only", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) };
    private readonly System.Windows.Controls.Button _rollButton;

    public GameRoulettePage()
    {
        var panel = ExperiencePageHelpers.CreatePage("Game Roulette", "Pick something from your own experience library. Add Roblox experiences by URL or place ID; Agentstrap does not fabricate discovery results.");
        var entry = new WrapPanel(); entry.Children.Add(_id); entry.Children.Add(_name);
        entry.Children.Add(ExperiencePageHelpers.Button("Add experience", AddExperience));
        panel.Children.Add(entry);
        var filters = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) }; filters.Children.Add(_search); filters.Children.Add(_favorites); panel.Children.Add(filters);
        panel.Children.Add(ExperiencePageHelpers.Card(new StackPanel { Children = { _result } }));
        var actions = new WrapPanel();
        _rollButton = ExperiencePageHelpers.Button("Roll", Roll, true);
        actions.Children.Add(_rollButton);
        actions.Children.Add(ExperiencePageHelpers.Button("Launch result", LaunchResult));
        actions.Children.Add(ExperiencePageHelpers.Button("Open Roblox page", OpenResult));
        actions.Children.Add(ExperiencePageHelpers.Button("Save result", SaveResult));
        panel.Children.Add(actions); panel.Children.Add(_status);
        panel.Children.Add(new TextBlock { Text = "Recent rolls", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 22, 0, 8) });
        panel.Children.Add(new TextBlock { Text = string.Join("   \u00B7   ", ExperienceLibraryService.GetRouletteHistory().Take(8)), Opacity = .7, TextWrapping = TextWrapping.Wrap });
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private void AddExperience(object sender, RoutedEventArgs e) { if (ExperiencePageHelpers.AddFromInput(_id, _name, _status)) { var count = ExperienceLibraryService.GetRecords().Count; _status.Text += $" Library: {count} experience(s)."; } }
    private async void Roll(object sender, RoutedEventArgs e)
    {
        var search = _search.Text.Trim();
        var candidates = ExperienceLibraryService.GetRecords().Where(x => (!_favorites.IsChecked.GetValueOrDefault() || x.IsFavorite) && (search.Length == 0 || x.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || x.PlaceId.Contains(search, StringComparison.OrdinalIgnoreCase))).ToList();
        if (candidates.Count == 0)
        {
            _result.Text = ExperienceLibraryService.GetRecords().Count == 0 ? "Your roulette is ready when you add an experience." : "No saved experiences match these filters.";
            _status.Text = "Add a Roblox URL or place ID above. Online discovery is unavailable in this build.";
            return;
        }
        var previous = ExperienceLibraryService.GetRouletteHistory().FirstOrDefault();
        var options = candidates.Where(x => x.PlaceId != previous).ToList(); if (options.Count == 0) options = candidates;
        var choice = options[Random.Shared.Next(options.Count)];
        _rollButton.IsEnabled = false;
        _status.Text = "Rolling through your saved experiences\u2026";
        for (var frame = 0; frame < 10; frame++)
        {
            _result.Text = candidates[Random.Shared.Next(candidates.Count)].Name;
            await Task.Delay(65 + frame * 8);
        }
        ExperienceLibraryService.RecordRouletteResult(choice.PlaceId);
        _result.Text = choice.Name;
        _status.Text = $"Place ID {choice.PlaceId}" + (string.IsNullOrWhiteSpace(choice.Creator) ? string.Empty : $" \u00B7 by {choice.Creator}");
        Tag = choice.PlaceId;
        _rollButton.IsEnabled = true;
    }
    private void LaunchResult(object sender, RoutedEventArgs e) { var record = Current(); if (record is null) { _status.Text = "Roll an experience first."; return; } ExperiencePageHelpers.Launch(record); }
    private void OpenResult(object sender, RoutedEventArgs e) { var record = Current(); if (record is null) { _status.Text = "Roll an experience first."; return; } ExperiencePageHelpers.OpenExperiencePage(record); }
    private void SaveResult(object sender, RoutedEventArgs e) { var record = Current(); if (record is null) { _status.Text = "Roll an experience first."; return; } ExperienceLibraryService.SetFavorite(record.PlaceId, true); _status.Text = "Saved to favorites."; }
    private Agentstrap.Models.Persistable.ExperienceRecord? Current() => Tag is string id ? ExperienceLibraryService.GetRecords().FirstOrDefault(x => x.PlaceId == id) : null;
}
