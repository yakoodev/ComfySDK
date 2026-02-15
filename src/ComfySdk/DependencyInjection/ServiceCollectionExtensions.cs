using ComfySdk.Http;
using ComfySdk.Options;
using Microsoft.Extensions.DependencyInjection;

namespace ComfySdk.DependencyInjection;

/// <summary>Service collection extensions for registering ComfySdk runtime services.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers <see cref="ComfyClient"/> and shared options in dependency injection container.</summary>
    public static IServiceCollection AddComfyClient(this IServiceCollection services, ComfyClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(options);
        services
            .AddHttpClient<ComfyHttpClient>((sp, client) =>
            {
                var configured = sp.GetRequiredService<ComfyClientOptions>();
                client.BaseAddress = configured.BaseUrl;
                client.Timeout = Timeout.InfiniteTimeSpan;
            });

        services.AddTransient<ComfyClient>();
        return services;
    }
}
