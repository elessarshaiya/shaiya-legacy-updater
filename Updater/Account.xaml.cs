using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Shaiya_Invasion_Updater
{
    public partial class Account : UserControl
    {
        public string UserID;

        public Account(string userId)
        {
            InitializeComponent();
            UserID = userId;
            TextBlockUserID.Text = userId;
            GridAccountRemove.ToolTip = "Remove " + userId + " from the list of saved accounts.";
        }

        private void UserControl_MouseEnter(object sender, MouseEventArgs e) { GridAccountRemove.Visibility = Visibility.Visible; }
        private void UserControl_MouseLeave(object sender, MouseEventArgs e) { GridAccountRemove.Visibility = Visibility.Hidden; }

        private static Brush ColorBrush(string color) { return (Brush)new BrushConverter().ConvertFrom(color); }
        private static DropShadowEffect Glow(string color) { return new DropShadowEffect { Color = (Color)ColorConverter.ConvertFromString(color), Direction = 320.0, ShadowDepth = 0.0, Opacity = 1.0, BlurRadius = 5.0 }; }

        private void GridAccountRemove_MouseEnter(object sender, MouseEventArgs e) { TextBlockX.Foreground = ColorBrush("#9A8774"); TextBlockX.Effect = Glow("#BAA794"); }
        private void GridAccountRemove_MouseLeave(object sender, MouseEventArgs e) { TextBlockX.Foreground = ColorBrush("#7A6754"); TextBlockX.Effect = null; }
        private void GridAccountRemove_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { TextBlockX.Foreground = ColorBrush("#5A4734"); TextBlockX.Effect = Glow("#7A6754"); }

        private void GridAccountRemove_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            TextBlockX.Foreground = ColorBrush("#7A6754");
            TextBlockX.Effect = null;
            if (MessageDialog.Display("Confirmation", "Are you sure you want to remove " + UserID + " from your saved accounts ?", "YesNo") != "Yes") return;
            UpdaterManager.RemoveSavedAccount(UserID);
            MainWindow.Accounts.Remove(UserID);
            MainWindow.MW.AccountRectangles.Children.Remove(this);
            MainWindow.MW.RefreshLogInView();
        }

        private void GridAccountName_MouseEnter(object sender, MouseEventArgs e) { TextBlockUserID.Foreground = ColorBrush("#9A8774"); TextBlockUserID.Effect = Glow("#BAA794"); }
        private void GridAccountName_MouseLeave(object sender, MouseEventArgs e) { TextBlockUserID.Foreground = ColorBrush("#7A6754"); TextBlockUserID.Effect = null; }
        private void GridAccountName_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { TextBlockUserID.Foreground = ColorBrush("#5A4734"); TextBlockUserID.Effect = Glow("#7A6754"); }
        private void GridAccountName_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { TextBlockUserID.Foreground = ColorBrush("#7A6754"); TextBlockUserID.Effect = null; MainWindow.MW.LogInId(UserID); }
    }
}
