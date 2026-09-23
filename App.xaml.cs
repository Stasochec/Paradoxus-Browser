using System.Threading;
using System.Windows;

namespace ParadoxusBrowser;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static Mutex? _appMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _appMutex = new Mutex(true, "ParadoxusBrowser_SingleInstance_AppMutex", out bool isFirstInstance);

        var mainWindow = new MainWindow(shouldRestoreSession: isFirstInstance);
        MainWindow = mainWindow;
        mainWindow.Show();
    }
}
