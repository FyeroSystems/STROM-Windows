using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.ApplicationModel.DynamicDependency;

namespace STROM;

public class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // For self-contained: try to initialize, ignore if it fails (runtime is bundled)
        Bootstrap.TryInitialize(0x00010006, out _);

        Application.Start((p) =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
    }
}
