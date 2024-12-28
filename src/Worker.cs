using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace soad_csharp;

public class Worker(ILogger<Worker> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting Worker");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
 