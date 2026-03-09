using System.Collections.Concurrent;
using System.IO.Abstractions;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using MyMusic.Common.Services;
using MyMusic.Server.Services;
using Scalar.AspNetCore;
using MvcJsonOptions = Microsoft.AspNetCore.Mvc.JsonOptions;

namespace MyMusic.Server;

public static class HostBuilderExtensions
{
    public static T UseMyMusicServer<T>(this T builder) where T : IHostApplicationBuilder
    {
        builder.Configuration.AddEnvironmentVariables("MYMUSIC_");

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
        builder.Services.AddSingleton<IFileSystem, FileSystem>();
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi(options =>
        {
            options.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_1;

            TypeTransformer.MapType<decimal>(new OpenApiSchema { Type = JsonSchemaType.Number, Format = "decimal" });
            TypeTransformer.MapType<decimal?>(new OpenApiSchema
                { Type = JsonSchemaType.Number | JsonSchemaType.Null, Format = "decimal" });
            TypeTransformer.MapType<double>(new OpenApiSchema { Type = JsonSchemaType.Number, Format = "double" });
            TypeTransformer.MapType<double?>(new OpenApiSchema
                { Type = JsonSchemaType.Number | JsonSchemaType.Null, Format = "double" });
            TypeTransformer.MapType<int>(new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" });
            TypeTransformer.MapType<int?>(new OpenApiSchema
                { Type = JsonSchemaType.Integer | JsonSchemaType.Null, Format = "int32" });
            TypeTransformer.MapType<long>(new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int64" });
            TypeTransformer.MapType<long?>(new OpenApiSchema
                { Type = JsonSchemaType.Integer | JsonSchemaType.Null, Format = "int64" });

            options.AddSchemaTransformer<TypeTransformer>();

            // options.AddSchemaTransformer((schema, context, cancellationToken) =>
            // {
            //     if (schema.Type?.Contains("integer") == true)
            //     {
            //         schema.Type = "integer";
            //         schema.Pattern = null;
            //     }
            //     return Task.CompletedTask;
            // });
        });
        builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

        builder.Services.Configure<ServerConfig>(builder.Configuration.GetSection("MyMusicServer"));

        return builder;
    }

    public static T BuildMyMusicServer<T>(this T app) where T : IHost, IApplicationBuilder, IEndpointRouteBuilder
    {
        var environment = app.Services.GetRequiredService<IWebHostEnvironment>();

        app.UseExceptionHandler("/error");

        // Configure the HTTP request pipeline.
        if (environment.IsDevelopment())
        {
            var serverConfig = app.Services.GetRequiredService<IOptions<ServerConfig>>().Value;

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

        app.Map("/error", (HttpContext context) =>
        {
            var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
            return Results.Problem(
                exception?.Message,
                statusCode: 500,
                title: "An error occurred");
        });

        app.UseAuthorization();

        app.MapControllers();

        return app;
    }
}

public sealed class TypeTransformer : IOpenApiSchemaTransformer
{
    private static readonly ConcurrentDictionary<Type, (JsonSchemaType Type, string? Format)> _typeMappings = new();

    public static void MapType<T>(OpenApiSchema schema)
    {
        _typeMappings[typeof(T)] = (schema.Type ?? JsonSchemaType.Null, schema.Format);
    }

    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        var clrType = context.JsonTypeInfo.Type;

        if (_typeMappings.TryGetValue(clrType, out var mapping))
        {
            schema.Type = mapping.Type;
            schema.Format = mapping.Format;
        }

        return Task.CompletedTask;
    }
}