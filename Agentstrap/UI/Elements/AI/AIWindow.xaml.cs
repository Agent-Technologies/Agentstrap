using System.Windows;

namespace Agentstrap.UI.Elements.AI
{
    public partial class AIWindow : Window
    {
        public AIWindow()
        {
            InitializeComponent();
            Loaded += (s, e) => RestorePosition();
            Closing += (s, e) => SavePosition();
            Dashboard.CloseButton.Click += (s, e) => Close();
        }

        private void RestorePosition()
        {
            var left = App.AISettings.Prop.DashboardLeft;
            var top = App.AISettings.Prop.DashboardTop;
            var w = App.AISettings.Prop.DashboardWidth;
            var h = App.AISettings.Prop.DashboardHeight;
            if (w > 0) Width = w;
            if (h > 0) Height = h;
            if (left >= 0 && top >= 0)
            {
                Left = left;
                Top = top;
            }
            else
            {
                // default bottom right of primary screen
                Left = SystemParameters.WorkArea.Width - Width - 16;
                Top = SystemParameters.WorkArea.Height - Height - 16;
            }
            ClampToScreen();
        }

        private void SavePosition()
        {
            App.AISettings.Prop.DashboardLeft = Left;
            App.AISettings.Prop.DashboardTop = Top;
            App.AISettings.Prop.DashboardWidth = Width;
            App.AISettings.Prop.DashboardHeight = Height;
            App.AISettings.Save();
        }

        private void ClampToScreen()
        {
            var area = SystemParameters.WorkArea;
            if (Left < area.Left) Left = area.Left;
            if (Top < area.Top) Top = area.Top;
            if (Left + Width > area.Right) Left = area.Right - Width;
            if (Top + Height > area.Bottom) Top = area.Bottom - Height;
        }

        public void ResetPosition()
        {
            App.AISettings.Prop.DashboardLeft = -1;
            App.AISettings.Prop.DashboardTop = -1;
            App.AISettings.Save();
            RestorePosition();
        }
    }
}
