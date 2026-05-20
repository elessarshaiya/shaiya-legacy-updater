using System.Windows.Controls;

namespace Shaiya_Invasion_Updater
{
    public partial class ProgressBar : UserControl
    {
        public ProgressBar() { InitializeComponent(); }
        public void SetProgress(byte percent)
        {
            if (ProgressImage != null) ProgressImage.Width = 893.0 * percent / 100.0;
        }
    }
}
