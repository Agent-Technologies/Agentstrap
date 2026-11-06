using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Agentstrap.Integrations;
using Agentstrap.Models.Persistable;
using Wpf.Ui.Controls;
using TextBox = System.Windows.Controls.TextBox;

namespace Agentstrap.UI.Elements.Settings.Pages;

public sealed class ExperienceRecordsPage : UiPage
{
    private readonly StackPanel _list = new();
    private readonly TextBlock _status = new() { Margin = new Thickness(0, 12, 0, 0), TextWrapping = TextWrapping.Wrap, Opacity = .75 };
    private readonly TextBox _search = ExperiencePageHelpers.Input("Search your library", 250);
    private readonly TextBox _id = ExperiencePageHelpers.Input("Roblox URL or place ID", 300);
    private readonly TextBox _name = ExperiencePageHelpers.Input("Experience name (optional)", 240);
    private string _view = "recent";

    public ExperienceRecordsPage()
    {
        var panel = ExperiencePageHelpers.CreatePage("Experience Records", "A local library of experiences you launched through Agentstrap or added yourself. Records contain no account credentials or Roblox cookies.");
        var entry = new WrapPanel(); entry.Children.Add(_id); entry.Children.Add(_name); entry.Children.Add(ExperiencePageHelpers.Button("Add", Add)); panel.Children.Add(entry);
        var actions = new WrapPanel { Margin = new Thickness(0, 8, 0, 10) };
        actions.Children.Add(ExperiencePageHelpers.Button("Recent", (_, _) => { _view = "recent"; Refresh(); }));
        actions.Children.Add(ExperiencePageHelpers.Button("Frequently played", (_, _) => { _view = "frequent"; Refresh(); }));
        actions.Children.Add(ExperiencePageHelpers.Button("Favorites", (_, _) => { _view = "favorites"; Refresh(); }));
        actions.Children.Add(ExperiencePageHelpers.Button("All", (_, _) => { _view = "all"; Refresh(); }));
        actions.Children.Add(ExperiencePageHelpers.Button("Export JSON", Export));
        actions.Children.Add(ExperiencePageHelpers.Button("Clear history", Clear));
        panel.Children.Add(actions); _search.TextChanged += (_, _) => Refresh(); panel.Children.Add(_search); panel.Children.Add(_status); panel.Children.Add(_list);
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Refresh();
    }

    private void Add(object sender, RoutedEventArgs e) { if (ExperiencePageHelpers.AddFromInput(_id, _name, _status)) Refresh(); }
    private void Export(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "JSON files (*.json)|*.json", FileName = "Agentstrap-ExperienceRecords.json" };
        if (dialog.ShowDialog() != true) return;
        try { ExperienceLibraryService.Export(dialog.FileName); _status.Text = "Experience records exported."; } catch (Exception ex) { _status.Text = $"Export failed: {ex.Message}"; }
    }
    private void Clear(object sender, RoutedEventArgs e)
    {
        if (System.Windows.MessageBox.Show("Clear all experience records, favorites, and notes?", "Clear experience records", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        ExperienceLibraryService.Clear(); Refresh(); _status.Text = "Experience history cleared.";
    }
    private void Refresh()
    {
        _list.Children.Clear();
        var query = _search.Text?.Trim() ?? string.Empty;
        var all = ExperienceLibraryService.GetRecords();
        var records = all.Where(x => (_view != "favorites" || x.IsFavorite) && (_view != "recent" || x.LastLaunchedUtc >= DateTime.UtcNow.AddDays(-30)) && (query.Length == 0 || x.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || x.PlaceId.Contains(query, StringComparison.OrdinalIgnoreCase)));
        records = _view == "frequent" ? records.OrderByDescending(x => x.LaunchCount).ThenByDescending(x => x.LastLaunchedUtc) : records.OrderByDescending(x => x.LastLaunchedUtc ?? x.AddedUtc);
        var visible = records.ToList();
        _status.Text = visible.Count == 0 ? (all.Count == 0 ? "No records yet. Add an experience or launch one through Agentstrap to start your library." : "No experiences match this view.") : $"{visible.Count} experience(s) · {_view}";
        foreach (var record in visible)
        {
            var card = new StackPanel();
            card.Children.Add(new TextBlock { Text = record.Name, FontSize = 17, FontWeight = FontWeights.SemiBold });
            card.Children.Add(new TextBlock { Text = $"Place {record.PlaceId}  ·  {record.LaunchCount} launch(es)  ·  Last launch: {(record.LastLaunchedUtc is DateTime t ? t.ToLocalTime().ToString("g") : "not launched yet")}", Opacity = .7, Margin = new Thickness(0, 3, 0, 8) });
            var note = ExperiencePageHelpers.Input("Personal note", 360); note.Text = record.Notes; note.LostFocus += (_, _) => ExperienceLibraryService.SetNotes(record.PlaceId, note.Text); card.Children.Add(note);
            var buttons = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            buttons.Children.Add(ExperiencePageHelpers.Button("Launch", (_, _) => ExperiencePageHelpers.Launch(record), true));
            buttons.Children.Add(ExperiencePageHelpers.Button("Open page", (_, _) => ExperiencePageHelpers.OpenExperiencePage(record)));
            buttons.Children.Add(ExperiencePageHelpers.Button(record.IsFavorite ? "Unfavorite" : "Favorite", (_, _) => { ExperienceLibraryService.SetFavorite(record.PlaceId, !record.IsFavorite); Refresh(); }));
            buttons.Children.Add(ExperiencePageHelpers.Button("Remove", (_, _) => { ExperienceLibraryService.Remove(record.PlaceId); Refresh(); }));
            card.Children.Add(buttons); _list.Children.Add(ExperiencePageHelpers.Card(card));
        }
    }
}
