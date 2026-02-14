using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using MyMusic.Common.Services;
using MyMusic.Server.Services;
using Scalar.AspNetCore;
using MvcJsonOptions = Microsoft.AspNetCore.Mvc.JsonOptions;

namespace MyMusic.Server;

public static class HostBuilderExtensions
{
    public static T UseMyMusicServer<T>(this T builder) where T : IHostApplicationBuilder
    {
        builder.Logging.AddSimpleConsole(c => c.SingleLine = true);
        // builder.Services.ConfigureHttpJsonOptions(opts =>
        // {
        //     var enumConverter = new JsonStringEnumConverter();
        //     opts.SerializerOptions.Converters.Add(enumConverter);
        // });
        builder.Services.Configure<JsonOptions>(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.Configure<MvcJsonOptions>(o =>
            o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddControllers();
        builder.Services.AddHttpContextAccessor();
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();
        builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

        builder.Services.Configure<ServerConfig>(builder.Configuration.GetSection("MyMusicServer"));

        return builder;
    }

    public static T BuildMyMusicServer<T>(this T app) where T : IHost, IApplicationBuilder, IEndpointRouteBuilder
    {
        var environment = app.Services.GetRequiredService<IWebHostEnvironment>();

        // Configure the HTTP request pipeline.
        if (environment.IsDevelopment())
        {
            var serverConfig = app.Services.GetRequiredService<IOptions<ServerConfig>>().Value;

            app.UseDeveloperExceptionPage();
            app.MapOpenApi();
            app.MapScalarApiReference(opts =>
            {
                opts.Servers = [new ScalarServer(serverConfig.ServerUrl)];
                opts.EnabledTargets =
                    [ScalarTarget.JavaScript, ScalarTarget.Shell, ScalarTarget.Python, ScalarTarget.CSharp];
                opts.Theme = ScalarTheme.Purple;
                opts.DefaultHttpClient =
                    new KeyValuePair<ScalarTarget, ScalarClient>(ScalarTarget.JavaScript, ScalarClient.Fetch);
            });
        }


        app.UseAuthorization();

        app.MapControllers();

        return app;
    }
}