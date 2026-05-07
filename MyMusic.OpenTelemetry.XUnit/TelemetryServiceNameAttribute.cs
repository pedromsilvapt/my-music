using System.Reflection;

namespace MyMusic.OpenTelemetry.XUnit;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
public class TelemetryServiceNameAttribute : Attribute
{
    public string ServiceName { get; }

    public TelemetryServiceNameAttribute(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            throw new ArgumentException("Service name cannot be null or whitespace.", nameof(serviceName));
        
        ServiceName = serviceName;
    }

    internal static string? GetServiceName(Assembly? assembly)
    {
        if (assembly == null) return null;
        
        var attribute = assembly.GetCustomAttribute<TelemetryServiceNameAttribute>();
        return attribute?.ServiceName;
    }

    public static string DiscoverServiceName(string? fallback = null)
    {
        var entryAssembly = Assembly.GetEntryAssembly();
        var serviceName = GetServiceName(entryAssembly);
        if (serviceName != null) return serviceName;

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            serviceName = GetServiceName(assembly);
            if (serviceName != null) return serviceName;
        }

        return fallback ?? entryAssembly?.GetName().Name ?? throw new Exception("Missing TelemetryServiceName attribute");
    }
}
