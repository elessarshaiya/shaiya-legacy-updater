using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Shaiya_Invasion_Updater
{
    public partial class iMenuItem : UserControl
    {
        public static readonly DependencyProperty XamlText = DependencyProperty.Register("Text", typeof(string), typeof(iMenuItem), new PropertyMetadata(TextChanged));
        public static readonly DependencyProperty XamlIsSelected = DependencyProperty.Register("IsSelected", typeof(bool), typeof(iMenuItem), new PropertyMetadata(false, IsSelectedChanged));
        public static readonly DependencyProperty XamlLink = DependencyProperty.Register("Link", typeof(string), typeof(iMenuItem));

        public string Text { get { return (string)GetValue(XamlText); } set { SetValue(XamlText, value); } }
        public bool IsSelected { get { return (bool)GetValue(XamlIsSelected); } set { SetValue(XamlIsSelected, value); } }
        public string Link { get { return (string)GetValue(XamlLink); } set { SetValue(XamlLink, value); } }

        public iMenuItem() { InitializeComponent(); }

        private static void TextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var item = (iMenuItem)sender;
            if (item.TextProperty != null) item.TextProperty.Text = e.NewValue == null ? "" : e.NewValue.ToString();
        }

        private static void IsSelectedChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var item = (iMenuItem)sender;
            item.ApplyVisual((bool)e.NewValue ? "#9A8774" : "#7A6754", (bool)e.NewValue);
        }

        private void ApplyVisual(string color, bool glow)
        {
            if (TextProperty == null) return;
            TextProperty.Foreground = (Brush)new BrushConverter().ConvertFrom(color);
            TextProperty.Effect = glow ? new DropShadowEffect { Color = (Color)ColorConverter.ConvertFromString("#BAA794"), Direction = 320.0, ShadowDepth = 0.0, Opacity = 1.0, BlurRadius = 5.0 } : null;
        }

        private void UserControl_MouseEnter(object sender, MouseEventArgs e)
        {
            if (!IsSelected) ApplyVisual("#9A8774", true);
        }

        private void UserControl_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!IsSelected) ApplyVisual("#7A6754", false);
        }

        private void UserControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsSelected) ApplyVisual("#5A4734", true);
        }

        private void UserControl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!IsSelected) ApplyVisual("#7A6754", false);
            OpenLink();
        }

        private void OpenLink()
        {
            if (string.IsNullOrEmpty(Link)) return;
            try
            {
                switch (Link)
                {
                    case "Updater:LatestNews":
                        MainWindow.MW.ShowLatestNews();
                        break;
                    case "Updater:LogIn":
                        MainWindow.MW.ShowLogIn();
                        break;
                    case "Updater:LogOut":
                        MainWindow.MW.LogOut();
                        break;
                    case "Updater:LogInSavedAccounts":
                        MainWindow.MW.RefreshLogInView(true);
                        break;
                    case "Updater:LogInNewAccount":
                        MainWindow.MW.RefreshLogInView(false);
                        break;
                    case "Updater:Repair":
                        MainWindow.MW.StartRepair();
                        break;
                    default:
                        Process.Start(new ProcessStartInfo(Link) { UseShellExecute = true });
                        break;
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.Submit(ex);
            }
        }
    }
}
