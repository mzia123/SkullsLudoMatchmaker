using SkullsLudo.Shared.Configuration;

namespace SkullsLudo.Director.Services;

public sealed class AgonesAllocatorService(
    AgonesSettings settings,
    ILogger<AgonesAllocatorService> logger) : IGameServerAllocator
{
    public async Task<GameServerAllocation?> AllocateAsync(string queueName, CancellationToken ct = default)
    {
        try
        {
            // In production, this creates an mTLS gRPC channel to agones-allocator
            // and calls AllocationService.Allocate with fleet selectors matching the queue.
            //
            // var creds = new SslCredentials(serverCa, new KeyCertificatePair(clientCert, clientKey));
            // var channel = new Channel(settings.AllocatorHost, settings.AllocatorPort, creds);
            // var client = new AllocationService.AllocationServiceClient(channel);
            // var response = await client.AllocateAsync(new AllocationRequest
            // {
            //     Namespace = "default",
            //     GameServerSelectors = { new GameServerSelector { MatchLabels = { { "queue", queueName } } } }
            // });
            // return new GameServerAllocation { Address = response.Address, Port = response.Ports[0].Port };

            logger.LogInformation("Allocating Agones game server for queue {Queue} from {Host}:{Port}",
                queueName, settings.AllocatorHost, settings.AllocatorPort);

            await Task.CompletedTask;

            logger.LogWarning("Agones allocation is stubbed. Returning placeholder address. " +
                "Replace with real AllocationService.Allocate call in production.");

            return new GameServerAllocation
            {
                Address = "game-server.default.svc.cluster.local",
                Port = 7654
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to allocate game server for queue {Queue}", queueName);
            return null;
        }
    }
}
