using System.Windows;
using System.Windows.Controls;
using Agentstrap.Integrations;
using Wpf.Ui.Controls;

namespace Agentstrap.UI.Elements.Settings.Pages;

/// <summary>Creates a short player chosen challenge for an experience in their library.</summary>
public sealed class SidequestPage : UiPage
{
    private readonly TextBlock _result = new() { FontSize = 18, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _status = new() { Opacity = .72, Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap };
    private readonly string[] _quests =
    {
        "Explore somewhere you have never visited and find one detail you would have missed at first glance.",
        "Try a route or strategy you normally avoid, then see how far it gets you.",
        "Complete one round without using your usual loadout or favorite approach.",
        "Find a helpful player and work together toward one shared goal.",
        "Set a small in game goal and finish it before switching experiences.",
        "Look for a hidden area, unusual interaction, or piece of environmental storytelling.",
        "Play one session focused on helping a teammate rather than your own score.",
        "Try the experience with a friend and let them choose the plan.",
        "Take a different path through the experience and compare what changes.",
        "Learn one mechanic you have not used before and put it to practice."
    };

    public SidequestPage()
    {
        var panel = ExperiencePageHelpers.CreatePage("Sidequest Generator", "A lightweight challenge for a game you already know. Quests are generated locally; choose an experience from your library and Agentstrap will not pretend to know its game rules.");
        panel.Children.Add(ExperiencePageHelpers.Card(_result));
        var actions = new WrapPanel(); actions.Children.Add(ExperiencePageHelpers.Button("Generate sidequest", Generate, true)); actions.Children.Add(ExperiencePageHelpers.Button("Launch experience", Launch));
        panel.Children.Add(actions); panel.Children.Add(_status);
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private void Generate(object sender, RoutedEventArgs e)
    {
        var records = ExperienceLibraryService.GetRecords();
        if (records.Count == 0) { _result.Text = "Add or launch an experience to begin."; _status.Text = "Sidequests use your local experience library and do not require online discovery."; Tag = null; return; }
        var record = records[Random.Shared.Next(records.Count)];
        Tag = record.PlaceId;
        _result.Text = $"{record.Name}\n\n{_quests[Random.Shared.Next(_quests.Length)]}";
        _status.Text = $"For place {record.PlaceId}. This is a general player challenge, not a description of the game's mechanics.";
    }

    private void Launch(object sender, RoutedEventArgs e)
    {
        var id = Tag as string;
        var record = id is null ? null : ExperienceLibraryService.GetRecords().FirstOrDefault(x => x.PlaceId == id);
        if (record is null) { _status.Text = "Generate a sidequest first."; return; }
        ExperiencePageHelpers.Launch(record);
    }
}
