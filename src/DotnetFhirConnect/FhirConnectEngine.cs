using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using DotnetFhirConnect.Engine;
using DotnetFhirConnect.Fhir;
using DotnetFhirConnect.Mappings;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.Composition;

namespace DotnetFhirConnect;

/// <summary>
/// Bidirectional openEHR ↔ FHIR transformation engine for FHIRconnect
/// v1.0.0 mapping bundles. The walking-skeleton release (v0.x) covers
/// the openEHR → FHIR direction; FHIR → openEHR is wired into the
/// public surface but throws <see cref="NotSupportedException"/> until
/// a follow-on slot lands the inverse-resolver work.
/// </summary>
/// <remarks>
/// The engine traffics in <see cref="object"/> for FHIR resources
/// because the three Firely packages each define their own
/// <c>Resource</c> base type with no shared assembly. Typed
/// convenience wrappers live in <c>DotnetFhirConnect.Fhir.R4.R4Engine</c>
/// (and pending R4B / R5 facades).
/// </remarks>
public sealed class FhirConnectEngine
{
    private readonly MappingBundle _bundle;
    private readonly ModelMapping _model;
    private readonly IFhirAdapter _adapter;

    /// <summary>
    /// Initialize the engine with a pre-loaded bundle. The bundle
    /// must contain at least one <see cref="ModelMapping"/>; if a
    /// context file is present, the model whose name matches
    /// <c>context.start</c> drives transform. Otherwise the first
    /// model in the bundle is used.
    /// </summary>
    /// <exception cref="ArgumentException">If the bundle has no
    /// usable model mapping.</exception>
    public FhirConnectEngine(MappingBundle bundle)
    {
        _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
        if (bundle.Models.Count == 0)
        {
            throw new ArgumentException(
                "FhirConnectEngine: bundle contains no model mapping. Load the model directory alongside the project bundle.",
                nameof(bundle));
        }
        _model = SelectStartModel(bundle);
        _adapter = FhirAdapterFactory.Create(_model.Spec.Version);
    }

    /// <summary>The FHIR adapter chosen from <c>spec.version</c>.</summary>
    public IFhirAdapter Adapter => _adapter;

    /// <summary>The model mapping driving the transform.</summary>
    public ModelMapping Model => _model;

    /// <summary>
    /// Transform a typed openEHR <see cref="Composition"/> to the
    /// FHIR resource declared by the model mapping's
    /// <c>spec.fhirConfig.structureDefinition</c>. v0.x only handles
    /// <c>Observation</c>; other targets throw.
    /// </summary>
    [RequiresUnreferencedCode(
        "Traverses Firely + DotnetOpenEhr.Aql typed graphs. Library is not AOT-publishable in v0.x.")]
    public object ToFhir(Composition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        Locatable? archetypeEntry = FindStartArchetype(composition);
        if (archetypeEntry is not Pathable archetypePathable)
        {
            throw new InvalidOperationException(
                $"FhirConnectEngine: start archetype '{_model.Spec.OpenEhrConfig?.Archetype}' not found in composition content.");
        }

        string resourceType = ExtractResourceType(_model.Spec.FhirConfig?.StructureDefinition)
            ?? throw new InvalidOperationException(
                $"FhirConnectEngine: cannot determine FHIR resource type from structure definition '{_model.Spec.FhirConfig?.StructureDefinition}'.");

        object resource = _adapter.CreateResource(resourceType);
        BindingContext ctx = new BindingContext(
            Composition: composition,
            Archetype: archetypePathable,
            OpenEhrRoot: archetypePathable,
            Resource: resource,
            FhirRoot: "$resource");

        MappingRuleExecutor executor = new MappingRuleExecutor(_adapter, TransformDirection.ToFhir);
        executor.ExecuteAll(ctx, _model.Mappings);
        return resource;
    }

    /// <summary>
    /// Transform a FHIR resource back into a typed openEHR
    /// <see cref="Composition"/>. <strong>Phase 4 not landed in v0.x:</strong>
    /// the public overload requires a Composition skeleton-bootstrap
    /// step that lands in a later phase; use the internal overload
    /// <see cref="ToOpenEhr(object, Composition)"/> until then.
    /// </summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public Composition ToOpenEhr(object resource)
    {
        throw new NotSupportedException(
            "FhirConnectEngine.ToOpenEhr: Phase 4 not landed. The public overload " +
            "requires a Composition skeleton-bootstrap step that lands in a later phase; " +
            "use the internal ToOpenEhr(object, Composition) overload until then.");
    }

    /// <summary>
    /// Bind a FHIR resource against a caller-supplied
    /// <see cref="Composition"/> skeleton and execute the model
    /// mapping in the ToOpenEhr direction. Internal-only until
    /// Phase 4 lands the public skeleton bootstrap.
    /// </summary>
    [RequiresUnreferencedCode(
        "Traverses Firely + DotnetOpenEhr.Aql typed graphs. Library is not AOT-publishable in v0.x.")]
    internal Composition ToOpenEhr(object resource, Composition skeleton)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(skeleton);

        Locatable? archetypeEntry = FindStartArchetype(skeleton);
        if (archetypeEntry is not Pathable archetypePathable)
        {
            throw new InvalidOperationException(
                $"FhirConnectEngine.ToOpenEhr: start archetype '{_model.Spec.OpenEhrConfig?.Archetype}' not found in skeleton content.");
        }

        BindingContext ctx = new BindingContext(
            Composition: skeleton,
            Archetype: archetypePathable,
            OpenEhrRoot: archetypePathable,
            Resource: resource,
            FhirRoot: "$resource");

        MappingRuleExecutor executor = new MappingRuleExecutor(_adapter, TransformDirection.ToOpenEhr);
        executor.ExecuteAll(ctx, _model.Mappings);
        return skeleton;
    }

    private ModelMapping SelectStartModel(MappingBundle bundle)
    {
        if (bundle.Context is ContextMapping ctx && bundle.Models.TryGetValue(ctx.Context.Start, out ModelMapping? byName))
        {
            return byName;
        }
        return bundle.Models.Values.First();
    }

    private Locatable? FindStartArchetype(Composition composition)
    {
        string? expectedArchetypeId = _model.Spec.OpenEhrConfig?.Archetype;
        if (string.IsNullOrEmpty(expectedArchetypeId))
        {
            return composition.Content?.FirstOrDefault();
        }
        if (composition.Content is null)
        {
            return null;
        }
        foreach (object item in composition.Content)
        {
            if (item is Locatable l && IsArchetypeMatch(l, expectedArchetypeId))
            {
                return l;
            }
        }
        return null;
    }

    private static bool IsArchetypeMatch(Locatable entry, string archetypeId)
    {
        string? entryId = entry.ArchetypeDetails?.ArchetypeId?.Value;
        return string.Equals(entryId, archetypeId, StringComparison.Ordinal);
    }

    private static string? ExtractResourceType(string? structureDefinitionUrl)
    {
        if (string.IsNullOrEmpty(structureDefinitionUrl))
        {
            return null;
        }
        int slash = structureDefinitionUrl.LastIndexOf('/');
        return slash >= 0 ? structureDefinitionUrl.Substring(slash + 1) : structureDefinitionUrl;
    }
}
