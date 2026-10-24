namespace Symphony.Config;

internal sealed class StaticServiceConfigProvider : IServiceConfigProvider
{
    readonly ServiceConfig config;

    public StaticServiceConfigProvider(ServiceConfig config)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public ServiceConfig GetConfig() => config;
}