using Microsoft.AspNetCore.RateLimiting;
using MatchMaking.Service.Api.Endpoints;
using MatchMaking.Service.Application.Commands;
using MatchMaking.Service.Infrastructure.Configuration;
using MatchMaking.Service.Infrastructure.Extensions;
using MatchMaking.Service.Infrastructure.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(SearchMatchCommand).Assembly));

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var rateLimiterConfig = builder.Configuration
    .GetSection(RateLimitingOptions.SectionName)
    .Get<RateLimitingOptions>()
    ?? throw new InvalidOperationException($"Configuration section '{RateLimitingOptions.SectionName}' is missing");

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter(RateLimitingOptions.PolicyName, limiterOptions =>
    {
        limiterOptions.Window = TimeSpan.FromMilliseconds(rateLimiterConfig.WindowMs);
        limiterOptions.PermitLimit = rateLimiterConfig.PermitLimit;
        limiterOptions.QueueLimit = rateLimiterConfig.QueueLimit;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseExceptionHandler();

// if (app.Environment.IsDevelopment())
// {
app.UseSwagger();
app.UseSwaggerUI();
//}

app.UseRateLimiter();
app.MapMatchmakingEndpoints();

app.Run();
