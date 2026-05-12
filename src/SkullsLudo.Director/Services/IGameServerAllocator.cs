namespace SkullsLudo.Director.Services;

public interface IGameServerAllocator
{
    Task<GameServerAllocation?> AllocateAsync(string queueName, CancellationToken ct = default);
}

public sealed class GameServerAllocation
{
    public required string Address { get; init; }
    public required int Port { get; init; }
    public string Connection => $"{Address}:{Port}";
}
