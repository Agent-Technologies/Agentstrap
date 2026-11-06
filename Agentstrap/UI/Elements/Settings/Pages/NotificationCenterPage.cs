using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Agentstrap.Integrations;
using Agentstrap.Models.Persistable;
using Wpf.Ui.Controls;
using TextBox = System.Windows.Controls.TextBox;

namespace Agentstrap.UI.Elements.Settings.Pages;

public sealed class NotificationCenterPage : UiPage
{
    private readonly StackPanel _list = new();
    private readonly TextBlock _status = new() { Margin = new Thickness(0, 12, 0, 0), TextWrapping = TextWrapping.Wrap, Opacity = .75 };
    private readonly TextBox _search = ExperiencePageHelpers.Input("Search notifications", 260);
    private readonly TextBlock _details = new() { TextWrapping = TextWrapping.Wrap };
    private string _filter = "All";
    private string? _selectedId;

    public NotificationCenterPage()
    {
        var panel = ExperiencePageHelpers.CreatePage("Notification Center", "A persistent local inbox for notifications Agentstrap generates, including IPWHO and IPINFO server connection events. Notifications remain available offline. IP-based location is approximate.");

        var actions = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        foreach (var f in new[] { "All", "IPWHO", "IPINFO", "Roblox", "Agentstrap", "System", "Unread" })
        {
            var btn = ExperiencePageHelpers.Button(f, (_, _) => { _filter = f; Refresh(); });
            actions.Children.Add(btn);
        }
        actions.Children.Add(ExperiencePageHelpers.Button("Mark all read", (_, _) => { NotificationCenterService.MarkAllRead(); Refresh(); }));
        actions.Children.Add(ExperiencePageHelpers.Button("Clear all", (_, _) =>
        {
            if (System.Windows.MessageBox.Show("Clear all notifications?", "Notification Center", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            { NotificationCenterService.Clear(); _selectedId = null; Refresh(); }
        }));
        panel.Children.Add(actions);

        _search.TextChanged += (_, _) => Refresh();
        panel.Children.Add(_search);
        panel.Children.Add(_status);

        var detailsCard = ExperiencePageHelpers.Card(_details);
        panel.Children.Add(detailsCard);

        panel.Children.Add(_list);
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Refresh();
    }

    private void Refresh()
    {
        _list.Children.Clear();
        var all = NotificationCenterService.GetAll().ToList();
        IEnumerable<NotificationRecord> filtered = _filter switch
        {
            "Unread" => all.Where(x => !x.IsRead),
            "All" => all,
            _ => all.Where(x => string.Equals(x.Source, _filter, StringComparison.OrdinalIgnoreCase))
        };
        var q = _search.Text?.Trim() ?? string.Empty;
        if (q.Length > 0)
            filtered = filtered.Where(x => (x.Title ?? "").Contains(q, StringComparison.OrdinalIgnoreCase) || (x.Message ?? "").Contains(q, StringComparison.OrdinalIgnoreCase));

        var visible = filtered.ToList();
        _status.Text = visible.Count == 0 ? "No notifications match this view." : $"{visible.Count} notification(s) - {_filter}";
        foreach (var n in visible)
        {
            var cardStack = new StackPanel();
            var titleRow = new TextBlock { Text = n.Title, FontSize = 15, FontWeight = FontWeights.SemiBold };
            if (!n.IsRead) titleRow.Foreground = (Brush)Application.Current.FindResource("AccentFillColorDefaultBrush");
            cardStack.Children.Add(titleRow);
            cardStack.Children.Add(new TextBlock { Text = n.Message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 4) });
            cardStack.Children.Add(new TextBlock { Text = $"{n.Source} - {n.CreatedUtc.ToLocalTime():g} - {(n.IsRead ? "Read" : "Unread")}{(n.DeliveryStatus is null ? "" : " - " + n.DeliveryStatus)}", Opacity = .7, FontSize = 12 });

            var btns = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            btns.Children.Add(ExperiencePageHelpers.Button(n.IsRead ? "Mark unread" : "Mark read", (_, _) =>
            {
                if (n.IsRead) NotificationCenterService.MarkUnread(n.Id); else NotificationCenterService.MarkRead(n.Id);
                Refresh();
            }));
            btns.Children.Add(ExperiencePageHelpers.Button("Details", (_, _) => { _selectedId = n.Id; UpdateDetails(); }));
            btns.Children.Add(ExperiencePageHelpers.Button("Delete", (_, _) => { NotificationCenterService.Delete(n.Id); if (_selectedId == n.Id) _selectedId = null; Refresh(); UpdateDetails(); }));
            cardStack.Children.Add(btns);
            _list.Children.Add(ExperiencePageHelpers.Card(cardStack));
        }

        UpdateDetails();
    }

    private void UpdateDetails()
    {
        if (string.IsNullOrWhiteSpace(_selectedId))
        {
            _details.Text = "Select a notification and press Details to view its full information.";
            return;
        }
        var n = NotificationCenterService.GetAll().FirstOrDefault(x => x.Id == _selectedId);
        if (n is null)
        {
            _details.Text = "Notification no longer available.";
            _selectedId = null;
            return;
        }
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Title: {n.Title}");
        sb.AppendLine($"Source: {n.Source}");
        sb.AppendLine($"Created: {n.CreatedUtc.ToLocalTime():g}");
        sb.AppendLine($"Status: {(n.IsRead ? "Read" : "Unread")}");
        if (n.DeliveryStatus is not null) sb.AppendLine($"Phone delivery: {n.DeliveryStatus}");
        sb.AppendLine();
        sb.AppendLine(n.Message);
        if (n.Metadata is not null && n.Metadata.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Details:");
            foreach (var kv in n.Metadata)
                sb.AppendLine($"  {kv.Key}: {kv.Value}");
        }
        sb.AppendLine();
        sb.AppendLine("IP-based location information is approximate and not a precise physical address.");
        _details.Text = sb.ToString();
    }
}