using System.Windows;

namespace PresentationOverlay;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var configPath = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "config.json");
        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        app.Run(new SidebarWindow(configPath));
    }
}
