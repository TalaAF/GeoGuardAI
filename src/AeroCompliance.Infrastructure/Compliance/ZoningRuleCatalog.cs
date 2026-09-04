using AeroCompliance.Domain.Entities;

namespace AeroCompliance.Infrastructure.Compliance;

/// <summary>
/// The spatial inputs a rule needs before it can be measured. Used to decide whether a rule is
/// applicable to a given detection or must record a safe non-failure outcome instead.
/// </summary>
[Flags]
public enum SpatialContext
{
    None = 0,
    /// <summary>Needs at least one road in the municipality to measure against.</summary>
    Road = 1,
    /// <summary>Needs a primary parcel resolved for the detection.</summary>
    Parcel = 2
}

/// <summary>How a rule's measured value is compared against its configured limit.</summary>
public enum Comparison
{
    /// <summary>Fails when measured is below the required value (e.g. a setback).</summary>
    MustBeAtLeast,
    /// <summary>Fails when measured exceeds the required value (e.g. an area cap).</summary>
    MustNotExceed
}

/// <summary>One supported, executable rule type. The catalogue is closed by design.</summary>
public sealed record ZoningRuleDefinition(
    string CanonicalKey,
    string Label,
    string Description,
    string Unit,
    string UnitLabel,
    decimal MinimumValue,
    decimal MaximumValue,
    Comparison Comparison,
    SpatialContext RequiredContext,
    string CalculationSummary)
{
    /// <summary>True when the measured value satisfies the configured limit.</summary>
    public bool Passes(decimal measured, decimal required) => Comparison == Comparison.MustBeAtLeast
        ? measured >= required
        : measured <= required;

    /// <summary>
    /// How far the measurement falls the wrong side of the limit. Zero when the rule passes.
    /// </summary>
    public decimal Exceedance(decimal measured, decimal required)
    {
        var delta = Comparison == Comparison.MustBeAtLeast ? required - measured : measured - required;
        return delta > 0 ? delta : 0m;
    }

    /// <summary>A sentence a municipal officer can read without knowing the rule engine exists.</summary>
    public string Explain(decimal measured, decimal required, string? location)
    {
        var where = string.IsNullOrWhiteSpace(location) ? "" : $" at {location}";
        var m = Format(measured);
        var r = Format(required);
        return Passes(measured, required)
            ? Comparison == Comparison.MustBeAtLeast
                ? $"Complies{where}: measured {m}, which meets the required minimum of {r}."
                : $"Complies{where}: measured {m}, within the permitted maximum of {r}."
            : Comparison == Comparison.MustBeAtLeast
                ? $"Does not comply{where}: measured {m}, which is below the required minimum of {r} " +
                  $"(short by {Format(Exceedance(measured, required))})."
                : $"Does not comply{where}: measured {m}, which exceeds the permitted maximum of {r} " +
                  $"(over by {Format(Exceedance(measured, required))}).";
    }

    /// <summary>Renders a value with its unit, e.g. "3.0 m", "250 m²", "60%".</summary>
    public string Format(decimal value) => Unit switch
    {
        "m" => $"{value:0.##} m",
        "m2" => $"{value:0.##} m²",
        "percent" => $"{value:0.##}%",
        _ => $"{value:0.##} {Unit}"
    };
}

/// <summary>
/// The closed catalogue of rule types the compliance engine can actually execute, plus alias
/// normalisation for rule keys already present in development data.
/// </summary>
/// <remarks>
/// Deliberately not an expression language: every rule type here maps to one fixed, reviewed
/// PostGIS calculation. Anything outside this catalogue is stored and displayed but never
/// evaluated, so an unrecognised or AI-invented rule key can never produce a violation.
/// </remarks>
public static class ZoningRuleCatalog
{
    public const string MinimumRoadSetback = "minimum_road_setback_m";
    public const string MaximumBuildingArea = "maximum_building_area_m2";
    public const string MaximumParcelCoverage = "maximum_parcel_coverage_percent";
    public const string MaximumOutsideParcelArea = "maximum_outside_parcel_area_m2";

    private static readonly ZoningRuleDefinition[] Definitions =
    [
        new(MinimumRoadSetback,
            "Minimum distance from road",
            "The building must stand at least this far back from the nearest road right-of-way. " +
            "A building that touches or overlaps the road measures zero.",
            "m", "metres", 0m, 500m, Comparison.MustBeAtLeast, SpatialContext.Road,
            "Shortest distance from the detected outline to the nearest road right-of-way, measured in EPSG:32636."),

        new(MaximumBuildingArea,
            "Maximum building area",
            "The total footprint of the detected building must not exceed this area.",
            "m2", "square metres", 0m, 1_000_000m, Comparison.MustNotExceed, SpatialContext.None,
            "Area of the detected outline, measured in EPSG:32636."),

        new(MaximumParcelCoverage,
            "Maximum parcel coverage",
            "The share of the parcel covered by the detected building must not exceed this percentage.",
            "percent", "percent", 0m, 100m, Comparison.MustNotExceed, SpatialContext.Parcel,
            "Area of the detected outline inside the parcel, divided by the parcel area, in EPSG:32636."),

        new(MaximumOutsideParcelArea,
            "Maximum area outside the parcel",
            "The part of the detected building that falls outside its parcel boundary must not exceed this area. " +
            "Set to zero to forbid any encroachment beyond the parcel.",
            "m2", "square metres", 0m, 1_000_000m, Comparison.MustNotExceed, SpatialContext.Parcel,
            "Area of the detected outline falling outside the parcel boundary, measured in EPSG:32636.")
    ];

    /// <summary>
    /// Rule keys seen in existing development data, mapped onto the canonical type. Normalising
    /// here means historical rows keep working without a data migration.
    /// </summary>
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["setback_from_road"] = MinimumRoadSetback,
        ["road_setback"] = MinimumRoadSetback,
        ["minimum_road_setback_m"] = MinimumRoadSetback,
        ["minimumroadsetbackmeters"] = MinimumRoadSetback,
        ["maximum_building_area_m2"] = MaximumBuildingArea,
        ["max_building_area"] = MaximumBuildingArea,
        ["maximumbuildingareasquaremeters"] = MaximumBuildingArea,
        ["maximum_parcel_coverage_percent"] = MaximumParcelCoverage,
        ["max_coverage"] = MaximumParcelCoverage,
        ["maximumparcelcoveragepercent"] = MaximumParcelCoverage,
        ["maximum_outside_parcel_area_m2"] = MaximumOutsideParcelArea,
        ["parcel_overflow"] = MaximumOutsideParcelArea,
        ["maximumoutsideparcelareasquaremeters"] = MaximumOutsideParcelArea
    };

    public static IReadOnlyList<ZoningRuleDefinition> All => Definitions;

    /// <summary>Resolves a stored rule key (canonical or alias) to its definition, or null.</summary>
    public static ZoningRuleDefinition? Find(string? ruleKey)
    {
        if (string.IsNullOrWhiteSpace(ruleKey)) return null;
        var key = ruleKey.Trim();
        if (!Aliases.TryGetValue(key, out var canonical))
        {
            // Tolerate spacing/casing variants of a canonical key before giving up.
            canonical = key.Replace(' ', '_').Replace('-', '_').ToLowerInvariant();
            if (!Aliases.ContainsKey(canonical)) return Definitions.FirstOrDefault(d => d.CanonicalKey == canonical);
            canonical = Aliases[canonical];
        }
        return Definitions.FirstOrDefault(d => d.CanonicalKey == canonical);
    }

    public static bool IsSupported(string? ruleKey) => Find(ruleKey) is not null;

    /// <summary>
    /// Whether a rule's stored value and unit are usable. A rule can be validated and active yet
    /// still be unenforceable because its configuration is out of range or in the wrong unit.
    /// </summary>
    public static bool IsConfigurationValid(ZoningRule rule, ZoningRuleDefinition definition) =>
        string.Equals(rule.Unit?.Trim(), definition.Unit, StringComparison.OrdinalIgnoreCase) &&
        rule.RuleValue >= definition.MinimumValue &&
        rule.RuleValue <= definition.MaximumValue;

    /// <summary>
    /// The single state shown to users and used by the engine. Order matters: an unsupported or
    /// misconfigured rule is reported as such even when it is active and validated, so nobody
    /// believes a rule is being enforced when it is not.
    /// </summary>
    public static string EnforcementState(ZoningRule rule)
    {
        var definition = Find(rule.RuleKey);
        if (definition is null) return EnforcementStates.Unsupported;
        if (!IsConfigurationValid(rule, definition)) return EnforcementStates.InvalidConfiguration;
        if (!rule.IsActive) return EnforcementStates.Inactive;
        if (!rule.IsValidated) return EnforcementStates.AwaitingValidation;
        return EnforcementStates.Enforced;
    }

    /// <summary>Only a fully enforceable rule may ever produce a violation.</summary>
    public static bool IsEnforceable(ZoningRule rule) => EnforcementState(rule) == EnforcementStates.Enforced;
}

public static class EnforcementStates
{
    public const string Enforced = "Enforced";
    public const string AwaitingValidation = "Awaiting validation";
    public const string Inactive = "Inactive";
    public const string Unsupported = "Unsupported";
    public const string InvalidConfiguration = "Invalid configuration";
}

/// <summary>Canonical evaluation outcomes stored in <c>compliance_evaluations.outcome</c>.</summary>
public static class ComplianceOutcomes
{
    public const string Pass = "pass";
    public const string Fail = "fail";
    public const string NotApplicable = "not_applicable";
    public const string InsufficientData = "insufficient_data";

    public static string Label(string outcome) => outcome switch
    {
        Pass => "Complies",
        Fail => "Does not comply",
        NotApplicable => "Not applicable",
        InsufficientData => "Insufficient data",
        // Retained so the two pre-Phase-7 heuristic outcomes still render as words.
        "candidate_violation" => "Does not comply",
        _ => outcome.Replace('_', ' ')
    };
}
