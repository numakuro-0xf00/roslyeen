using Microsoft.Build.Locator;

namespace RoslynQuery.Core.Workspace;

/// <summary>
/// MSBuildLocator initialization helper.
/// CRITICAL: Must be called before any MSBuild/Roslyn workspace types are accessed.
/// </summary>
public static class MsBuildInitializer
{
    private static readonly object Lock = new();
    private static bool _initialized;
    private static VisualStudioInstance? _registeredInstance;

    /// <summary>
    /// Ensures MSBuild is initialized. Safe to call multiple times.
    /// </summary>
    /// <returns>The registered Visual Studio instance.</returns>
    public static VisualStudioInstance EnsureInitialized()
    {
        if (_initialized)
        {
            return _registeredInstance!;
        }

        lock (Lock)
        {
            if (_initialized)
            {
                return _registeredInstance!;
            }

            var instances = MSBuildLocator.QueryVisualStudioInstances().ToList();

            if (instances.Count == 0)
            {
                throw new InvalidOperationException(
                    "No MSBuild instances found. Please install Visual Studio 2022, " +
                    "Visual Studio Build Tools, or the .NET SDK.");
            }

            // Prefer VisualStudioSetup (VS/Build Tools) over DotNetSdk for .NET Framework support
            // Then prefer highest version
            _registeredInstance = instances
                .OrderByDescending(x => x.DiscoveryType == DiscoveryType.VisualStudioSetup)
                .ThenByDescending(x => x.Version)
                .First();

            MSBuildLocator.RegisterInstance(_registeredInstance);
            _initialized = true;

            return _registeredInstance;
        }
    }

    /// <summary>
    /// Gets whether MSBuild has been initialized.
    /// </summary>
    public static bool IsInitialized => _initialized;

    /// <summary>
    /// Gets the registered Visual Studio instance, if initialized.
    /// </summary>
    public static VisualStudioInstance? RegisteredInstance => _registeredInstance;
}
