using Alpaca.Markets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using soad_csharp;
using soad_csharp.brokers;
using soad_csharp.database;
using soad_csharp.strategies;
using System.Diagnostics;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json").AddUserSecrets<Program>();
builder.Logging.AddSimpleConsole();
var connectionString = builder.Configuration.GetConnectionString("TradeDb");

builder.Services.AddDbContext<TradeDbContext>(options =>
{
    options.UseSqlite(connectionString);
});

//log httpclient requests
AppContext.SetSwitch("System.Net.Http.EnableActivityPropagation", true);
var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ConsoleDiagnosticListener>();
DiagnosticListener.AllListeners.Subscribe(new ConsoleDiagnosticListener(logger));

builder.Services.AddHostedService<Worker>();
  
//builder.Services.AddSingleton<AlpacaBroker>();
//builder.Services.AddSingleton<ConstantPercentageStrategy>();

builder.Build().Run();
