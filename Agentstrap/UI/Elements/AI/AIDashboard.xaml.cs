using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using Agentstrap.AI;
using Agentstrap.AI.Providers;

namespace Agentstrap.UI.Elements.AI
{
    public partial class AIDashboard : UserControl
    {
        private AIConversation? _conversation;
        private AIChatService? _service;
        private CancellationTokenSource? _cts;
        private bool _showKey = false;

        public AIDashboard()
        {
            InitializeComponent();
            Loaded += AIDashboard_Loaded;
            TitleBar.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                    Window.GetWindow(this)?.DragMove();
            };
        }

        private void AIDashboard_Loaded(object sender, RoutedEventArgs e)
        {
            ProviderCombo.Items.Clear();
            foreach (AIProviderId p in Enum.GetValues(typeof(AIProviderId)))
                ProviderCombo.Items.Add(p.ToString());
            var saved = App.AISettings.Prop.SelectedProvider;
            if (!string.IsNullOrWhiteSpace(saved) && ProviderCombo.Items.Contains(saved))
                ProviderCombo.SelectedItem = saved;
            else if (ProviderCombo.Items.Count > 0) ProviderCombo.SelectedIndex = 0;

            PermissionModeCombo.Items.Clear();
            foreach (AIPermissionMode m in Enum.GetValues(typeof(AIPermissionMode)))
                PermissionModeCombo.Items.Add(m.ToString());
            PermissionModeCombo.SelectedItem = App.AISettings.Prop.PermissionMode;
            if (PermissionModeCombo.SelectedItem is null) PermissionModeCombo.SelectedIndex = 0;

            RefreshModels();
            RefreshConversations();
            UpdateStatus();
            UpdateSelectedModelHeader();
        }

        private AIProviderId CurrentProvider => AIKeyManager.Parse(App.AISettings.Prop.SelectedProvider) ?? AIProviderId.OpenAI;

        private void ProviderCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProviderCombo.SelectedItem is null) return;
            App.AISettings.Prop.SelectedProvider = ProviderCombo.SelectedItem.ToString()!;
            App.AISettings.Save();
            RefreshModels();
            UpdateStatus();
            UpdateSelectedModelHeader();
            if (App.AISettings.Prop.SelectedProvider == "Groq")
            {
                var key = AIKeyManager.Load(AIProviderId.Groq);
                if (!string.IsNullOrWhiteSpace(key))
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var gp = new GroqProvider();
                            await gp.DiscoverModelsAsync(key!, CancellationToken.None);
                            Dispatcher.Invoke(() => { RefreshModels(); });
                        }
                        catch { }
                    });
                }
            }
        }

        private void RefreshModels()
        {
            var savedModel = App.AISettings.Prop.SelectedModel;
            if (!string.IsNullOrWhiteSpace(savedModel))
                ModelIdBox.Text = savedModel;
        }

        private void PermissionModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PermissionModeCombo.SelectedItem is null) return;
            App.AISettings.Prop.PermissionMode = PermissionModeCombo.SelectedItem.ToString()!;
            App.AISettings.Save();
            UpdateStatus();
        }

        private void ShowKeyButton_Click(object sender, RoutedEventArgs e)
        {
            _showKey = !_showKey;
            ShowKeyButton.Content = _showKey ? "Hide" : "Show";
        }

        private void SaveKeyButton_Click(object sender, RoutedEventArgs e)
        {
            var provider = CurrentProvider;
            var key = ApiKeyBox.Password;
            if (string.IsNullOrWhiteSpace(key)) { MessageBox.Show("Enter an API key."); return; }
            AIKeyManager.Save(provider, key);
            ApiKeyBox.Clear();
            UpdateStatus();
            EnvFileService.Save(provider.ToString(), ModelIdBox.Text, key);
            App.AISettings.Prop.SelectedModel = ModelIdBox.Text;
            App.AISettings.Save();
            UpdateSelectedModelHeader();
            MessageBox.Show("API key saved securely.");
        }

        private void RemoveKeyButton_Click(object sender, RoutedEventArgs e)
        {
            var provider = CurrentProvider;
            AIKeyManager.Remove(provider);
            UpdateStatus();
            MessageBox.Show("API key removed.");
        }

        private async void TestButton_Click(object sender, RoutedEventArgs e)
        {
            var provider = CurrentProvider;
            var key = AIKeyManager.Load(provider);
            if (string.IsNullOrWhiteSpace(key)) { key = ApiKeyBox.Password; }
            if (string.IsNullOrWhiteSpace(key)) { MessageBox.Show("No API key."); return; }
            var model = ModelIdBox.Text;
            TestButton.IsEnabled = false;
            try
            {
                var p = AIProviderManager.Get(provider);
                bool ok = await p.TestConnectionAsync(key!, model, CancellationToken.None);
                MessageBox.Show(ok ? "Connection successful." : "Connection failed.");
            }
            catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
            finally { TestButton.IsEnabled = true; }
            UpdateStatus();
        }

        private void AIControlToggle_Click(object sender, RoutedEventArgs e)
        {
            if (!App.AISettings.Prop.AIControlApproved)
            {
                var res = MessageBox.Show("Enable AI Control? AI can navigate and modify approved settings according to permission mode.", "AI Control", MessageBoxButton.YesNo);
                if (res != MessageBoxResult.Yes) return;
                App.AISettings.Prop.AIControlApproved = true;
            }
            App.AISettings.Prop.AIControlEnabled = !App.AISettings.Prop.AIControlEnabled;
            App.AISettings.Save();
            UpdateStatus();
        }

        private void DisableControlButton_Click(object sender, RoutedEventArgs e)
        {
            App.AISettings.Prop.AIControlEnabled = false;
            App.AISettings.Save();
            _cts?.Cancel();
            UpdateStatus();
            AppendSystemMessage("AI Control disabled.");
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Configure provider, key, model, and permission mode in this panel.");
        }

        private void NewChatButton_Click(object sender, RoutedEventArgs e)
        {
            _conversation = ConversationManager.Create();
            ChatPanel.Children.Clear();
            _service = new AIChatService(_conversation);
            RefreshConversations();
            SelectConversation(_conversation.Id);
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            if (_conversation is null) return;
            _conversation.Messages.Clear();
            ChatPanel.Children.Clear();
            ConversationManager.Save();
        }

        private void DeleteConvButton_Click(object sender, RoutedEventArgs e)
        {
            if (_conversation is null) return;
            ConversationManager.Delete(_conversation.Id);
            _conversation = null;
            _service = null;
            ChatPanel.Children.Clear();
            RefreshConversations();
        }

        private void RefreshConversations()
        {
            ConversationCombo.Items.Clear();
            foreach (var c in ConversationManager.All)
                ConversationCombo.Items.Add(c.Title + "||" + c.Id);
            if (_conversation is not null)
                SelectConversation(_conversation.Id);
        }

        private void SelectConversation(string id)
        {
            foreach (var item in ConversationCombo.Items)
            {
                if (item.ToString()!.EndsWith(id))
                {
                    ConversationCombo.SelectedItem = item;
                    break;
                }
            }
        }

        private void ConversationCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ConversationCombo.SelectedItem is null) return;
            var parts = ConversationCombo.SelectedItem.ToString()!.Split("||");
            if (parts.Length < 2) return;
            var id = parts[1];
            var conv = ConversationManager.Get(id);
            if (conv is null) return;
            _conversation = conv;
            _service = new AIChatService(_conversation);
            ChatPanel.Children.Clear();
            foreach (var m in conv.Messages)
            {
                if (m.Role == "system") continue;
                AppendMessage(m.Role, m.Content);
            }
        }

        private void UpdateStatus()
        {
            var provider = App.AISettings.Prop.SelectedProvider;
            var keyExists = false;
            var pid = AIKeyManager.Parse(provider);
            if (pid is not null) keyExists = AIKeyManager.Exists(pid.Value);
            StatusText.Text = $"{provider} {(keyExists ? "connected" : "no key")}";
            StatusDot.Background = keyExists ? Brushes.Green : Brushes.Gray;
            if (App.AISettings.Prop.AIControlEnabled)
            {
                StatusText.Text += " | AI Control ON";
                StatusDot.Background = Brushes.Orange;
            }
        }

        private void UpdateSelectedModelHeader()
        {
            SelectedModelHeader.Text = $"{App.AISettings.Prop.SelectedProvider} · {App.AISettings.Prop.SelectedModel}";
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            var text = InputBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(text)) return;
            InputBox.Clear();
            if (_conversation is null) { _conversation = ConversationManager.Create(); _service = new AIChatService(_conversation); RefreshConversations(); }
            AppendMessage("user", text);

            _cts = new CancellationTokenSource();
            SendButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            var assistantBlock = AppendMessage("assistant", "");
            var onText = new Action<string>(delta =>
            {
                Dispatcher.Invoke(() =>
                {
                    var tb = assistantBlock.Tag as TextBlock;
                    if (tb is not null) { tb.Text += delta; ChatScroll.ScrollToEnd(); }
                });
            });
            var onStatus = new Action<string>(s => Dispatcher.Invoke(() => AppendSystemMessage(s)));
            Func<AIToolCall, Task<bool>> confirm = async (tc) =>
            {
                var res = MessageBox.Show($"Allow action '{tc.Name}'?", "Confirm", MessageBoxButton.YesNo);
                return res == MessageBoxResult.Yes;
            };

            try
            {
                await _service!.RunTurnAsync(text, onText, onStatus, confirm, _cts.Token);
            }
            catch (Exception ex)
            {
                AppendSystemMessage($"Error: {ex.Message}");
            }
            finally
            {
                SendButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                ChatScroll.ScrollToEnd();
                ConversationManager.Save();
                UpdateStatus();
                UpdateSelectedModelHeader();
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
        }

        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                e.Handled = true;
                SendButton_Click(sender, e);
            }
        }

        private Border AppendMessage(string role, string text)
        {
            var border = new Border { Margin = new Thickness(0, 4, 0, 4), Padding = new Thickness(8), CornerRadius = new CornerRadius(6) };
            var tb = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White };
            if (role == "user") border.Background = new SolidColorBrush(Color.FromArgb(60, 100, 100, 255));
            else if (role == "assistant") border.Background = new SolidColorBrush(Color.FromArgb(60, 80, 80, 80));
            else border.Background = new SolidColorBrush(Color.FromArgb(40, 255, 200, 0));
            border.Child = tb;
            border.Tag = tb;
            ChatPanel.Children.Add(border);
            ChatScroll.ScrollToEnd();
            return border;
        }

        private void AppendSystemMessage(string text)
        {
            AppendMessage("system", text);
        }

        private void OpenEnvButton_Click(object sender, RoutedEventArgs e)
        {
            EnvFileService.Save(CurrentProvider.ToString(), App.AISettings.Prop.SelectedModel, AIKeyManager.Load(CurrentProvider));
            EnvFileService.OpenInEditor();
        }

        private void ModelIdBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            App.AISettings.Prop.SelectedModel = ModelIdBox.Text;
            App.AISettings.Prop.CustomModelId = ModelIdBox.Text;
            App.AISettings.Save();
            UpdateSelectedModelHeader();
            EnvFileService.Save(CurrentProvider.ToString(), ModelIdBox.Text, AIKeyManager.Load(CurrentProvider));
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this)?.Close();
        }

        private void ResetPosButton_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is AIWindow w)
                w.ResetPosition();
        }
    }
}
