using System.Text;
using GridRunners.Api.Configuration.Bind;
using GridRunners.Api.Configuration.Runtime;
using GridRunners.Core.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Mvc;
using GridRunners.Api.Configuration.ServiceConfigurations;

var builder = WebApplication.CreateBuilder(args);

// Bind CORS options from configuration and create runtime config
var corsOptions = new CorsOptions();
builder.Configuration.GetSection(CorsOptions.ConfigSection).Bind(corsOptions);
var runtimeCorsConfig = new RuntimeCorsConfig(corsOptions);
builder.Services.AddSingleton(runtimeCorsConfig);

// Add services to the container - order matters for dependency injection
builder.Services
    .AddApiServices()
    .AddStorageServices(builder.Configuration, builder.Environment) // Register storage services (includes KeyVault)
    .AddAuthenticationServices(builder.Configuration) // Then auth services that depend on KeyVault
    .AddSwaggerServices();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerWithUI();
}

app.UseApiConfiguration(app.Configuration);

app.MapControllers();

app.Run();
