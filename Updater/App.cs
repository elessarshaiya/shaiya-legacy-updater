using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Windows;

namespace Shaiya_Invasion_Updater
{
  public class App : Application
  {
    [GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
    [DebuggerNonUserCode]
    public void InitializeComponent() => this.StartupUri = new Uri("SplashScreen.xaml", UriKind.Relative);

    [GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
    [STAThread]
    [DebuggerNonUserCode]
    public static void Main()
    {
      App app = new App();
      app.InitializeComponent();
      app.Run();
    }
  }
}
