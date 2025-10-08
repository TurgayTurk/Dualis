using Microsoft.CodeAnalysis.Testing;
using PackageIdentity = Microsoft.CodeAnalysis.Testing.PackageIdentity;

namespace Dualis.Analyzers.Tests;

/// <summary>
/// Provides ReferenceAssemblies configured for .NET 9 using the GA reference pack via NuGet.
/// </summary>
internal static class TestReferenceAssemblies
{
    public static ReferenceAssemblies Net90 => new ReferenceAssemblies("net9.0")
        .WithPackages([new PackageIdentity("Microsoft.NETCore.App.Ref", "9.0.0")]);
}
