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
            Environment.ExitCode = CliRunner.Run(args);
            return;
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
