using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MyMusic.Common.Sources;

namespace MyMusic.Source;

public static class WebApplicationBuilderExtensions
{
    public static WebApplicationBuilder UseMusicSource(this WebApplicationBuilder builder, ISource source)
    {
        builder.Services.AddSingleton(source);

        return builder;
    }

    public static WebApplicationBuilder UseMusicSource<T>(this WebApplicationBuilder builder) where T : class, ISource
    {
        builder.Services.AddSingleton<T>();

        return builder;
    }
}