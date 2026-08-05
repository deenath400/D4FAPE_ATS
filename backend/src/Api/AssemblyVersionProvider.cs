namespace Ats.Api;

using System.Reflection;
using Ats.Service;

public sealed class AssemblyVersionProvider : IVersionProvider
{
    public string GetVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
    }
}
