using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;

namespace Plugin.Maui.Pulse;

sealed class PulseHostInitializer : IMauiInitializeService
{
    public void Initialize(IServiceProvider services)
    {
        services.GetService<PulseHostRuntime>()?.Start(services);
    }
}
