namespace Ats.ArchitectureTests;

using System;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using Xunit;

public class LayeringRuleTests
{
    [Fact]
    public void Api_DoesNotReferenceDb()
    {
        var apiAssembly = Assembly.Load("Ats.Api");
        var referencedAssemblies = apiAssembly.GetReferencedAssemblies();

        var referencesDb = referencedAssemblies.Any(r =>
            r.Name != null && (r.Name.Equals("Ats.Db", StringComparison.OrdinalIgnoreCase) ||
                               r.Name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase)));

        Assert.False(referencesDb, "Ats.Api must not reference Ats.Db or EntityFrameworkCore directly (Rule 2).");

        // Inspect type references across all public/internal types in Api
        foreach (var type in apiAssembly.GetTypes())
        {
            var members = type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            foreach (var member in members)
            {
                var memberType = member switch
                {
                    FieldInfo f => f.FieldType,
                    PropertyInfo p => p.PropertyType,
                    MethodInfo m => m.ReturnType,
                    _ => null
                };

                if (memberType != null)
                {
                    Assert.DoesNotContain("Ats.Db", memberType.Namespace ?? string.Empty);
                    Assert.DoesNotContain("Microsoft.EntityFrameworkCore", memberType.Namespace ?? string.Empty);
                }
            }
        }
    }

    [Fact]
    public void Service_DoesNotReferenceAspNetCoreHttp()
    {
        var serviceAssembly = Assembly.Load("Ats.Service");
        var referencedAssemblies = serviceAssembly.GetReferencedAssemblies();

        var referencesHttpOrApi = referencedAssemblies.Any(r =>
            r.Name != null && (r.Name.StartsWith("Microsoft.AspNetCore.Http", StringComparison.OrdinalIgnoreCase) ||
                               r.Name.Equals("Ats.Api", StringComparison.OrdinalIgnoreCase)));

        Assert.False(referencesHttpOrApi, "Ats.Service must not reference Microsoft.AspNetCore.Http or Ats.Api (Rule 3).");

        foreach (var type in serviceAssembly.GetTypes())
        {
            Assert.DoesNotContain("Microsoft.AspNetCore.Http", type.Namespace ?? string.Empty);
        }
    }

    [Fact]
    public void Db_DoesNotReferenceAspNetCoreHttpOrClaimsPrincipal()
    {
        var dbAssembly = Assembly.Load("Ats.Db");
        var referencedAssemblies = dbAssembly.GetReferencedAssemblies();

        var referencesForbidden = referencedAssemblies.Any(r =>
            r.Name != null && (r.Name.StartsWith("Microsoft.AspNetCore.Http", StringComparison.OrdinalIgnoreCase) ||
                               r.Name.Equals("Ats.Api", StringComparison.OrdinalIgnoreCase) ||
                               r.Name.Equals("Ats.Service", StringComparison.OrdinalIgnoreCase)));

        Assert.False(referencesForbidden, "Ats.Db must not reference Http, Api, or Service (Rule 4).");

        foreach (var type in dbAssembly.GetTypes())
        {
            Assert.DoesNotContain("Microsoft.AspNetCore.Http", type.Namespace ?? string.Empty);
            Assert.DoesNotContain("System.Security.Claims", type.Namespace ?? string.Empty);

            foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (member is MethodInfo m)
                {
                    foreach (var param in m.GetParameters())
                    {
                        Assert.NotEqual(typeof(ClaimsPrincipal), param.ParameterType);
                    }
                }
            }
        }
    }

    [Fact]
    public void Shared_DoesNotReferenceApiServiceOrDb()
    {
        var sharedAssembly = Assembly.Load("Ats.Shared");
        var referencedAssemblies = sharedAssembly.GetReferencedAssemblies();

        var referencesForbidden = referencedAssemblies.Any(r =>
            r.Name != null && (r.Name.Equals("Ats.Api", StringComparison.OrdinalIgnoreCase) ||
                               r.Name.Equals("Ats.Service", StringComparison.OrdinalIgnoreCase) ||
                               r.Name.Equals("Ats.Db", StringComparison.OrdinalIgnoreCase)));

        Assert.False(referencesForbidden, "Ats.Shared must not reference Api, Service, or Db (Rule 5).");
    }
}
