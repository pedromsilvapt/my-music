namespace MyMusic.Common.Services.Sync;

public interface IKeepAwake
{
    Task ActivateAsync(CancellationToken ct = default);
    void Deactivate();
}
