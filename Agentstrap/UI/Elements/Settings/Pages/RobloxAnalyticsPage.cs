using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Agentstrap.Integrations;
using Agentstrap.Models.Persistable;
using Wpf.Ui.Controls;
using TextBox = System.Windows.Controls.TextBox;

namespace Agentstrap.UI.Elements.Settings.Pages;

public sealed class RobloxAnalyticsPage : UiPage
{
    private readonly TextBlock _stats = new() { TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _chart = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom };
    private readonly StackPanel _experiences = new();
    private readonly StackPanel _sessions = new();
    private readonly TextBlock _status = new() { Margin = new Thickness(0, 12, 0, 0), TextWrapping = TextWrapping.Wrap, Opacity = .75 };
    private readonly TextBlock _cardTotalPlaytime = new() { FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 0) };
    private readonly TextBlock _cardTotalSessions = new() { FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 0) };
    private readonly TextBlock _cardAvgSession = new() { FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 0) };
    private readonly TextBlock _cardMostPlayed = new() { FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 0) };
    private readonly TextBlock _cardRecentlyPlayed = new() { FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 0) };
    private readonly TextBlock _cardTracked = new() { FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 0) };
    private string _period = "Last30";
    private string _sort = "MostPlayed";

    public RobloxAnalyticsPage()
    {
        var panel = ExperiencePageHelpers.CreatePage("Roblox Analytics", "Local Roblox session tracking, playtime statistics, experience statistics and session history. Data is stored locally and is not transmitted externally.");

        var periodBar = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        foreach (var p in new[] { ("Today", "Today"), ("Last7", "Last 7 Days"), ("Last30", "Last 30 Days"), ("Last90", "Last 90 Days"), ("All", "All Time") })
        {
            periodBar.Children.Add(ExperiencePageHelpers.Button(p.Item2, (_, _) => { _period = p.Item1; Refresh(); }));
        }
        panel.Children.Add(periodBar);

        // summary cards
        var cards = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        cards.Children.Add(MakeCard("Total Playtime", _cardTotalPlaytime));
        cards.Children.Add(MakeCard("Total Sessions", _cardTotalSessions));
        cards.Children.Add(MakeCard("Avg Session", _cardAvgSession));
        cards.Children.Add(MakeCard("Most Played", _cardMostPlayed));
        cards.Children.Add(MakeCard("Recently Played", _cardRecentlyPlayed));
        cards.Children.Add(MakeCard("Tracked Experiences", _cardTracked));
        panel.Children.Add(cards);

        panel.Children.Add(new TextBlock { Text = "Playtime", FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 6) });
        var chartScroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Height = 180 };
        chartScroll.Content = _chart;
        panel.Children.Add(chartScroll);

        var expHeader = new WrapPanel { Margin = new Thickness(0, 12, 0, 6) };
        expHeader.Children.Add(new TextBlock { Text = "Experiences", FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center });
        foreach (var s in new[] { "MostPlayed", "LeastPlayed", "RecentlyPlayed", "TotalPlaytime", "Sessions", "Alphabetical" })
        {
            expHeader.Children.Add(ExperiencePageHelpers.Button(s, (_, _) => { _sort = s; Refresh(); }));
        }
        panel.Children.Add(expHeader);
        panel.Children.Add(_experiences);

        panel.Children.Add(new TextBlock { Text = "Recent Sessions", FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 6) });
        var sessActions = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        sessActions.Children.Add(ExperiencePageHelpers.Button("Export JSON", ExportJson));
        sessActions.Children.Add(ExperiencePageHelpers.Button("Export CSV", ExportCsv));
        sessActions.Children.Add(ExperiencePageHelpers.Button("Clear history", Clear));
        panel.Children.Add(sessActions);
        panel.Children.Add(_status);
        panel.Children.Add(_sessions);

        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Refresh();
    }

    private Border Card(string title, Func<string> valueFactory)
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = title, FontSize = 12, Opacity = .7 });
        var val = new TextBlock { Text = valueFactory(), FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 0) };
        sp.Children.Add(val);
        var border = ExperiencePageHelpers.Card(sp);
        border.Width = 180;
        border.Margin = new Thickness(0, 0, 10, 10);
        return border;
    }

    private void Refresh()
    {
        _cardTotalPlaytime.Text = FormatDuration(RobloxAnalyticsService.TotalPlaytimeSeconds(_period));
        _cardTotalSessions.Text = RobloxAnalyticsService.TotalSessions(_period).ToString();
        _cardAvgSession.Text = FormatDuration(RobloxAnalyticsService.AverageSessionSeconds(_period));
        _cardMostPlayed.Text = RobloxAnalyticsService.MostPlayedExperience();
        _cardRecentlyPlayed.Text = RobloxAnalyticsService.RecentlyPlayedExperience();
        _cardTracked.Text = RobloxAnalyticsService.ExperienceStats().Count.ToString();
        UpdateChart();
        UpdateExperiences();
        UpdateSessions();
        _status.Text = $"{RobloxAnalyticsService.TotalSessions(_period)} session(s) in selected period.";
    }

    private Border MakeCard(string title, TextBlock value)
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = title, FontSize = 12, Opacity = .7 });
        sp.Children.Add(value);
        var border = ExperiencePageHelpers.Card(sp);
        border.Width = 180;
        border.Margin = new Thickness(0, 0, 10, 10);
        return border;
    }

    private void UpdateChart()
    {
        _chart.Children.Clear();
        var days = _period switch { "Today" => 1, "Last7" => 7, "Last30" => 30, "Last90" => 30, _ => 30 };
        var data = RobloxAnalyticsService.DailyPlaytime(days);
        double max = data.Values.DefaultIfEmpty(0).Max();
        if (max <= 0) max = 1;
        foreach (var kv in data)
        {
            var bar = new Border
            {
                Width = 18,
                Height = Math.Max(2, (kv.Value / max) * 140),
                Background = (Brush)Application.Current.FindResource("AccentFillColorDefaultBrush"),
                Margin = new Thickness(2, 0, 2, 0),
                VerticalAlignment = VerticalAlignment.Bottom,
                CornerRadius = new CornerRadius(3, 3, 0, 0),
            };
            var label = new TextBlock { Text = kv.Key.ToString("MM/dd"), FontSize = 9, Opacity = .6, HorizontalAlignment = HorizontalAlignment.Center };
            var col = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 4, 0) };
            col.Children.Add(bar);
            col.Children.Add(label);
            _chart.Children.Add(col);
        }
    }

    private void UpdateExperiences()
    {
        _experiences.Children.Clear();
        var stats = RobloxAnalyticsService.ExperienceStats();
        IEnumerable<KeyValuePair<string, (double TotalSeconds, int Sessions, double Longest, DateTime? LastPlayed)>> ordered = _sort switch
        {
            "LeastPlayed" => stats.OrderBy(x => x.Value.TotalSeconds),
            "RecentlyPlayed" => stats.OrderByDescending(x => x.Value.LastPlayed),
            "TotalPlaytime" => stats.OrderByDescending(x => x.Value.TotalSeconds),
            "Sessions" => stats.OrderByDescending(x => x.Value.Sessions),
            "Alphabetical" => stats.OrderBy(x => x.Key),
            _ => stats.OrderByDescending(x => x.Value.TotalSeconds),
        };
        foreach (var kv in ordered)
        {
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = kv.Key, FontSize = 15, FontWeight = FontWeights.SemiBold });
            sp.Children.Add(new TextBlock { Text = $"Total: {FormatDuration(kv.Value.TotalSeconds)} - Sessions: {kv.Value.Sessions} - Avg: {FormatDuration(kv.Value.Sessions > 0 ? kv.Value.TotalSeconds / kv.Value.Sessions : 0)} - Longest: {FormatDuration(kv.Value.Longest)} - Last: {(kv.Value.LastPlayed.HasValue ? kv.Value.LastPlayed.Value.ToLocalTime().ToString("g") : "n/a")}", Opacity = .7, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            _experiences.Children.Add(ExperiencePageHelpers.Card(sp));
        }
        if (stats.Count == 0) _experiences.Children.Add(new TextBlock { Text = "No completed sessions yet.", Opacity = .7 });
    }

    private void UpdateSessions()
    {
        _sessions.Children.Clear();
        var sessions = RobloxAnalyticsService.SessionsInPeriod(_period).OrderByDescending(x => x.StartUtc).Take(50).ToList();
        foreach (var s in sessions)
        {
            var sp = new StackPanel();
            var name = string.IsNullOrWhiteSpace(s.ExperienceName) ? (string.IsNullOrWhiteSpace(s.PlaceId) ? "Unknown Experience" : $"Experience {s.PlaceId}") : s.ExperienceName!;
            sp.Children.Add(new TextBlock { Text = name, FontSize = 15, FontWeight = FontWeights.SemiBold });
            sp.Children.Add(new TextBlock { Text = $"{s.StartUtc.ToLocalTime():g} - {(s.EndUtc.HasValue ? s.EndUtc.Value.ToLocalTime().ToString("g") : "running")} - {FormatDuration(s.DurationSeconds)} - {s.Status}", Opacity = .7, FontSize = 12 });
            var btns = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            btns.Children.Add(ExperiencePageHelpers.Button("Delete", (_, _) => { RobloxAnalyticsService.DeleteSession(s.Id); Refresh(); }));
            sp.Children.Add(btns);
            _sessions.Children.Add(ExperiencePageHelpers.Card(sp));
        }
        if (sessions.Count == 0) _sessions.Children.Add(new TextBlock { Text = "No sessions in this period.", Opacity = .7 });
    }

    private void ExportJson(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "JSON files (*.json)|*.json", FileName = "Agentstrap-RobloxAnalytics.json" };
        if (dialog.ShowDialog() != true) return;
        try { RobloxAnalyticsService.ExportJson(dialog.FileName); _status.Text = "Analytics exported as JSON."; } catch (Exception ex) { _status.Text = $"Export failed: {ex.Message}"; }
    }

    private void ExportCsv(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = "Agentstrap-RobloxSessions.csv" };
        if (dialog.ShowDialog() != true) return;
        try { RobloxAnalyticsService.ExportCsv(dialog.FileName); _status.Text = "Session history exported as CSV."; } catch (Exception ex) { _status.Text = $"Export failed: {ex.Message}"; }
    }

    private void Clear(object sender, RoutedEventArgs e)
    {
        if (System.Windows.MessageBox.Show("Clear all Roblox analytics history?", "Roblox Analytics", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        RobloxAnalyticsService.Clear(); Refresh(); _status.Text = "Analytics history cleared.";
    }

    private static string FormatDuration(double seconds)
    {
        if (seconds < 60) return $"{seconds:0}s";
        if (seconds < 3600) return $"{seconds / 60:0.0}m";
        return $"{seconds / 3600:0.00}h";
    }
}