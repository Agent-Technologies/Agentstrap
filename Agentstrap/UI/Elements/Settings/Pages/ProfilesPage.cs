using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Agentstrap.Profiles;
using Wpf.Ui.Controls;
using TextBox = System.Windows.Controls.TextBox;

namespace Agentstrap.UI.Elements.Settings.Pages;

/// <summary>Simple UI for the profile storage already present in Agentstrap.</summary>
public sealed class ProfilesPage : UiPage
{
    private readonly ProfileManager _profiles = new(Path.Combine(Paths.Base, "Profiles"));
    private readonly StackPanel _items = new();
    private readonly TextBlock _status = new() { Margin = new Thickness(0, 10, 0, 8), Opacity = .72, TextWrapping = TextWrapping.Wrap };
    private readonly TextBox _name = ExperiencePageHelpers.Input("Profile name", 260);
    private readonly TextBox _description = ExperiencePageHelpers.Input("Short description (optional)", 320);

    public ProfilesPage()
    {
        var panel = ExperiencePageHelpers.CreatePage("Profiles", "Save and switch between local Agentstrap settings profiles. Applying a profile changes the current settings; save them from the footer to keep the change.");
        var row = new WrapPanel(); row.Children.Add(_name); row.Children.Add(_description); row.Children.Add(ExperiencePageHelpers.Button("Save current settings", SaveCurrent, true));
        panel.Children.Add(row); panel.Children.Add(_status); panel.Children.Add(_items);
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Refresh();
    }

    private void SaveCurrent(object sender, RoutedEventArgs e)
    {
        var name = _name.Text.Trim();
        if (name.Length == 0) { _status.Text = "Enter a profile name."; return; }
        try
        {
            var profile = _profiles.CreateFromSettings(name, App.Settings.Prop, _description.Text.Trim());
            _profiles.SaveProfile(profile);
            _name.Clear(); _description.Clear(); _status.Text = $"Saved profile “{name}”."; Refresh();
        }
        catch (Exception ex) { _status.Text = $"Could not save profile: {ex.Message}"; }
    }

    private void Refresh()
    {
        _items.Children.Clear();
        var names = _profiles.ListProfiles();
        if (names.Count == 0) { _items.Children.Add(ExperiencePageHelpers.Card(new TextBlock { Text = "No profiles yet. Save your current settings above to create one." })); return; }
        foreach (var name in names)
        {
            var profile = _profiles.LoadProfile(name);
            var card = new StackPanel();
            card.Children.Add(new TextBlock { Text = profile?.Name ?? name, FontSize = 16, FontWeight = FontWeights.SemiBold });
            card.Children.Add(new TextBlock { Text = profile?.Description ?? "", Opacity = .7, Margin = new Thickness(0, 3, 0, 8) });
            var actions = new WrapPanel();
            actions.Children.Add(ExperiencePageHelpers.Button("Apply", (_, _) => Apply(name), true));
            actions.Children.Add(ExperiencePageHelpers.Button("Export", (_, _) => Export(name)));
            actions.Children.Add(ExperiencePageHelpers.Button("Delete", (_, _) => { _profiles.DeleteProfile(name); Refresh(); }));
            card.Children.Add(actions); _items.Children.Add(ExperiencePageHelpers.Card(card));
        }
    }

    private void Apply(string name)
    {
        var profile = _profiles.LoadProfile(name);
        if (profile is null) { _status.Text = "This profile could not be read."; return; }
        App.Settings.Prop = profile.Settings;
        _status.Text = $"Applied “{name}”. Review the settings, then use Save in the footer to persist them.";
    }

    private void Export(string name)
    {
        var dialog = new SaveFileDialog { Filter = "Agentstrap profile (*.json)|*.json", FileName = $"{name}.json" };
        if (dialog.ShowDialog() != true) return;
        try { _profiles.ExportProfile(name, dialog.FileName); _status.Text = "Profile exported."; }
        catch (Exception ex) { _status.Text = $"Could not export profile: {ex.Message}"; }
    }
}
