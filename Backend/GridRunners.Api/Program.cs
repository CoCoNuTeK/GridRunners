using System.Text;
using GridRunners.Api.Configuration;
using GridRunners.Api.Data;
using GridRunners.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using GridRunners.Api.Hubs;
using Microsoft.AspNetCore.Mvc;
using GridRunners.Api.Configuration.ServiceConfigurations;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services
    .AddApiServices()
    .AddAuthenticationServices(builder.Configuration)
    .AddStorageServices(builder.Configuration, builder.Environment)
    .AddSwaggerServices();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerWithUI();
}

app.UseApiConfiguration(app.Configuration);

app.MapControllers();
app.MapHub<MazeGameHub>("/hubs/game");

app.Run();
