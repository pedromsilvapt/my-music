using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MyMusic.Source;

public static class WebApplicationBuilderExtensions
{
    public static WebApplicationBuilder UseMusicSource(this WebApplicationBuilder builder, ISource source)
    {
        builder.Services.AddSingleton(source);
        
        return builder;
    }
} 

