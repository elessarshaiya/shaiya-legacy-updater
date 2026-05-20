using System.Windows;
using System.Windows.Input;

namespace Shaiya_Invasion_Updater
{
    public partial class MessageDialog : Window
    {
        private string ReturnString = "";

        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            try { DragMove(); } catch { }
        }

        private MessageDialog(string title, string text)
        {
            InitializeComponent();
            Title = title;
            TextBlockTitle.Text = title;
            TextBlockText.Text = text;
            iButton ok = new iButton { Text = "OK", Margin = new Thickness(10.0, 0.0, 10.0, 15.0) };
            ok.MouseLeftButtonUp += ButtonOK_Click;
            KeyDown += MessageDialog_KeyDown;
            ButtonContainer.Children.Add(ok);
        }

        private MessageDialog(string title, string text, string buttons)
        {
            InitializeComponent();
            Title = title;
            TextBlockTitle.Text = title;
            TextBlockText.Text = text;
            if (buttons == "YesNo")
            {
                iButton yes = new iButton { Text = "Yes", Margin = new Thickness(10.0, 0.0, 10.0, 15.0) };
                yes.MouseLeftButtonUp += ButtonYes_Click;
                ButtonContainer.Children.Add(yes);
                iButton no = new iButton { Text = "No", Margin = new Thickness(10.0, 0.0, 10.0, 15.0) };
                no.MouseLeftButtonUp += ButtonNo_Click;
                ButtonContainer.Children.Add(no);
            }
            else
            {
                iButton ok = new iButton { Text = "OK", Margin = new Thickness(10.0, 0.0, 10.0, 15.0) };
                ok.MouseLeftButtonUp += ButtonOK_Click;
                ButtonContainer.Children.Add(ok);
            }
        }

        public static void Display(string title, string text) { new MessageDialog(title, text).ShowDialog(); }
        public static string Display(string title, string text, string buttons) { MessageDialog d = new MessageDialog(title, text, buttons); d.ShowDialog(); return d.ReturnString; }
        private void ButtonOK_Click(object sender, MouseButtonEventArgs e) { Close(); }
        private void MessageDialog_KeyDown(object sender, KeyEventArgs e) { Close(); }
        private void ButtonYes_Click(object sender, MouseButtonEventArgs e) { ReturnString = "Yes"; Close(); }
        private void ButtonNo_Click(object sender, MouseButtonEventArgs e) { ReturnString = "No"; Close(); }
    }
}
