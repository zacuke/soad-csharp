﻿using Microsoft.Extensions.Hosting;

namespace soad_csharp;

public class Worker : IHostedService
{
    public   Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
 