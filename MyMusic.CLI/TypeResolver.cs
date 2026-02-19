using Spectre.Console.Cli;

namespace MyMusic.CLI;

public class TypeResolver : ITypeResolver
{
    private readonly IServiceProvider _provider;

    public TypeResolver(IServiceProvider provider)
    {
        _provider = provider;
    }

    public object? Resolve(Type? type) => type is not null ? _provider.GetService(type) : null;
}