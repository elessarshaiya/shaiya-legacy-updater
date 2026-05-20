using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Shaiya_Invasion_Updater
{
    public partial class iButton2 : UserControl
    {
        public static readonly DependencyProperty XamlText = DependencyProperty.Register("Text", typeof(string), typeof(iButton2), new PropertyMetadata(TextChanged));
        public string Text { get { return (string)GetValue(XamlText); } set { SetValue(XamlText, value); } }
        public iButton2() { InitializeComponent(); }
        private static void TextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) { var b = (iButton2)sender; if (b.TextBlockButtonText != null) b.TextBlockButtonText.Text = e.NewValue == null ? "" : e.NewValue.ToString(); }
        private static Brush B(string c) { return (Brush)new BrushConverter().ConvertFrom(c); }
        private static DropShadowEffect G(string c) { return new DropShadowEffect { Color = (Color)ColorConverter.ConvertFromString(c), Direction = 320.0, ShadowDepth = 0.0, Opacity = 1.0, BlurRadius = 5.0 }; }
        private void UserControl_MouseEnter(object sender, MouseEventArgs e) { TextBlockButtonText.Foreground = B("#9A8774"); TextBlockButtonText.Effect = G("#BAA794"); Opacity = 0.9; }
        private void UserControl_MouseLeave(object sender, MouseEventArgs e) { TextBlockButtonText.Foreground = B("#7A6754"); TextBlockButtonText.Effect = null; Opacity = 1.0; }
        private void UserControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { TextBlockButtonText.Foreground = B("#5A4734"); TextBlockButtonText.Effect = G("#7A6754"); Opacity = 0.8; }
        private void UserControl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { TextBlockButtonText.Foreground = B("#7A6754"); TextBlockButtonText.Effect = null; Opacity = 1.0; }
    }
}
