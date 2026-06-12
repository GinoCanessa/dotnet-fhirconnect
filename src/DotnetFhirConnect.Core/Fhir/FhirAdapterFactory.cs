using System;
using System.Collections.Concurrent;

namespace DotnetFhirConnect.Fhir;

/// <summary>
/// Picks an <see cref="IFhirAdapter"/> implementation for a given
/// FHIR release. The mapping bundle's <c>spec.version</c> drives
/// selection at engine construction time.
/// </summary>
/// <remarks>
/// The factory is a registry: it holds no reference to any
/// release-specific adapter type, keeping <c>DotnetFhirConnect.Core</c>
/// free of any Firely model package. Each per-release package
/// (<c>DotnetFhirConnect.FhirR4</c> / <c>.FhirR4B</c> / <c>.FhirR5</c>)
/// self-registers its adapter via a <c>[ModuleInitializer]</c>; hosts
/// referencing multiple releases call <c>Register()</c> explicitly for
/// order-independent determinism.
/// </remarks>
public static class FhirAdapterFactory
{
    private static readonly ConcurrentDictionary<FhirRelease, Func<IFhirAdapter>> s_factories =
        new ConcurrentDictionary<FhirRelease, Func<IFhirAdapter>>();

    /// <summary>
    /// Register (or replace) the adapter factory for
    /// <paramref name="release"/>. Idempotent: calling it repeatedly
    /// with the same release is safe. Per-release packages call this
    /// from a module initializer and expose a public <c>Register()</c>
    /// entry point for explicit, order-independent registration.
    /// </summary>
    public static void Register(FhirRelease release, Func<IFhirAdapter> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        s_factories[release] = factory;
    }

    /// <summary>
    /// Return a new adapter for <paramref name="release"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">If no adapter has
    /// been registered for <paramref name="release"/> — reference the
    /// matching <c>DotnetFhirConnect.Fhir*</c> package (and, in an
    /// agnostic host, call its <c>Register()</c>).</exception>
    public static IFhirAdapter Create(FhirRelease release)
    {
        if (s_factories.TryGetValue(release, out Func<IFhirAdapter>? factory))
        {
            return factory();
        }

        throw new InvalidOperationException(
            $"No FHIR adapter registered for {release} — reference the " +
            $"DotnetFhirConnect.Fhir{release} package (and call " +
            $"{release}FhirSupport.Register() if using the agnostic engine).");
    }

    /// <summary>
    /// Whether an adapter is registered for <paramref name="release"/>.
    /// </summary>
    public static bool IsRegistered(FhirRelease release) => s_factories.ContainsKey(release);
}