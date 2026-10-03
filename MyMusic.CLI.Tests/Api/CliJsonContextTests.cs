namespace MyMusic.CLI.Tests.Api;

using MyMusic.CLI.Api;
using MyMusic.CLI.Api.Dtos;
using Shouldly;
using Xunit;

public class CliJsonContextTests
{
    /// <summary>
    /// Every DTO used as a parameter or (awaited) return type by <see cref="IMyMusicClient"/>.
    /// </summary>
    public static TheoryData<Type> ApiTypes()
    {
        var types = typeof(IMyMusicClient).GetMethods()
            .SelectMany(m => m.GetParameters().Select(p => p.ParameterType)
                .Append(m.ReturnType.IsGenericType ? m.ReturnType.GetGenericArguments()[0] : m.ReturnType))
            .Where(t => t.Namespace == typeof(CreateDeviceRequest).Namespace)
            .Distinct();
        return new TheoryData<Type>(types);
    }

    [Fact]
    public void ApiTypes_AreDiscovered() =>
        ApiTypes().Count.ShouldBeGreaterThan(20);

    [Theory]
    [MemberData(nameof(ApiTypes))]
    public void EveryApiType_IsInSourceGeneratedContext(Type type) =>
        CliJsonContext.Default.GetTypeInfo(type).ShouldNotBeNull();
}
