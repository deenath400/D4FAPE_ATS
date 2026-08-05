using System;
using Ats.Api;
using Ats.Service;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("CRITICAL: Missing required configuration key 'ConnectionStrings:Default'.");
    Environment.Exit(1);
    return;
}

builder.Services.AddSystemService(builder.Configuration);
builder.Services.AddSingleton<IVersionProvider, AssemblyVersionProvider>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.MapSystemStatus();

app.Run();

public partial class Program { }
