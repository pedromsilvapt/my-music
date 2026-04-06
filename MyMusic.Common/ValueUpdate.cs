namespace MyMusic.Common;

public record ValueUpdate<T>(T? NewValue = default);

public record StructValueUpdate<T>(T? NewValue = default) where T : struct;
