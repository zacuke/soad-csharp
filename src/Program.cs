using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using soad_csharp;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddSimpleConsole();
builder.Services.AddHostedService<Worker>();
builder.Build().Run();
