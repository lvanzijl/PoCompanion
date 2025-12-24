using Microsoft.Maui;
using Microsoft.Maui.Hosting;
using Microsoft.UI.Xaml;

namespace PoTool.Maui.WinUI;

public class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Microsoft.UI.Xaml.Application.Start((p) =>
        {
            var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
            System.Threading.SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
    }
}

public class App : MauiWinUIApplication
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
