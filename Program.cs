using KeyAutomator.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WinRT;

namespace KeyAutomator;

public static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (CliRunner.IsCliMode(args))
        {
            // WinExe でも呼び出し元へ確実にコードを返す
            Environment.Exit(CliRunner.Run(args));
        }

        ComWrappersSupport.InitializeComWrappers();
        Application.Start(p =>
        {
            var context = new DispatcherQueueSynchronizationContext(
                DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
    }
}
