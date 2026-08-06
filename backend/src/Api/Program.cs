using System;
using System.Text;
using Ats.Api;
using Ats.Service;
using Ats.Shared.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("CRITICAL: Missing required configuration key 'ConnectionStrings:Default'.");
    Environment.Exit(1);
    return;
}

var signingKey = builder.Configuration["Jwt:SigningKey"] ?? "DefaultDevelopmentSigningKeyWithAtLeast32BytesLength!";
var issuer = builder.Configuration["Jwt:Issuer"] ?? "D4FAPE-ATS";
var audience = builder.Configuration["Jwt:Audience"] ?? "D4FAPE-ATS-App";

builder.Services.AddSystemService(builder.Configuration);
builder.Services.AddSingleton<IVersionProvider, AssemblyVersionProvider>();
builder.Services.AddProblemDetails();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
        ClockSkew = TimeSpan.FromSeconds(30)
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthConstants.Policies.CandidateOnly, policy =>
        policy.RequireRole(AuthConstants.Roles.Candidate));

    options.AddPolicy(AuthConstants.Policies.StaffOnly, policy =>
        policy.RequireRole(AuthConstants.Roles.Recruiter, AuthConstants.Roles.HiringManager));

    options.AddPolicy(AuthConstants.Policies.RecruiterOnly, policy =>
        policy.RequireRole(AuthConstants.Roles.Recruiter));

    options.AddPolicy(AuthConstants.Policies.HiringManagerOnly, policy =>
        policy.RequireRole(AuthConstants.Roles.HiringManager));
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapSystemStatus();
app.MapAuthEndpoints();
app.MapRequisitionEndpoints();
app.MapPublicRequisitionEndpoints();
app.MapApplicationEndpoints();
app.MapPipelineEndpoints();

app.Run();

public partial class Program { }
