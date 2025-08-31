using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace MyMusic.Source;

public static class WebApplicationBuilderExtensions
{
    public static WebApplicationBuilder UseMusicSource(this WebApplicationBuilder builder, ISource source)
    {
        builder.Services.AddSingleton(source);
        
        return builder;
    }
} 

