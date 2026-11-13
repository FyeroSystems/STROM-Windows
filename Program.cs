using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.ApplicationModel.DynamicDependency;

namespace STROM;

public class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // Bootstrap the Windows App SDK for unpackaged self-contained apps
        Bootstrap.Initialize(0x00010006); // 1.6

        Application.Start((p) =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
    }
}
