using MatchMaking.Worker.Application.Commands;
using MatchMaking.Worker.Infrastructure.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(AccumulatePlayerCommand).Assembly));

builder.Services.AddInfrastructure(builder.Configuration);

var host = builder.Build();
host.Run();
