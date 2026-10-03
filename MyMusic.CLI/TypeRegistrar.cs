using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace MyMusic.CLI;

public class TypeRegistrar : ITypeRegistrar
{
    private readonly IServiceCollection _services;
    private readonly IServiceProvider? _provider;

    public TypeRegistrar(IServiceCollection services)
    {
        _services = services;
    }

    public TypeRegistrar(IServiceCollection services, IServiceProvider provider)
    {
        _services = services;
        _provider = provider;
    }

    public ITypeResolver Build() => new TypeResolver(_provider ?? _services.BuildServiceProvider());

    [UnconditionalSuppressMessage("Trimming", "IL2067",
        Justification = "Spectre registers command/settings types from rooted assemblies (my-music, Spectre.Console.Cli).")]
    public void Register(Type service, Type implementation)
    {
        _services.AddSingleton(service, implementation);
    }

    public void RegisterInstance(Type service, object instance)
    {
        _services.AddSingleton(service, instance);
    }

    public void RegisterLazy(Type service, Func<object> factory)
    {
        _services.AddSingleton(service, _ => factory());
    }
}