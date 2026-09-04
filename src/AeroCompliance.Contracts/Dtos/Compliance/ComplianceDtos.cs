namespace AeroCompliance.Contracts.Dtos.Compliance;

/// <summary>
/// One supported rule type, as offered by the guided rule form. Everything the frontend needs to
/// render and validate the form comes from here — it never hardcodes units or limits.
/// </summary>
public sealed record RuleTypeDto(
    string Key,
    string Label,
    string Description,
    string Unit,
    string UnitLabel,
    decimal MinimumValue,
    decimal MaximumValue,
    string ComparisonLabel,
    string CalculationSummary,
    string RequiredContext,
    string ExampleValue);

/// <summary>
/// A stored compliance evaluation rendered for a human: rule name, where it applies, what was
/// measured against what limit, and a plain-language verdict.
/// </summary>
public sealed record ComplianceEvaluationDto(
    Guid Id,
    Guid DetectionId,
    string EvaluationType,
    string RuleLabel,
    string? RuleDescription,
    Guid? ZoningRuleId,
    string? RuleEnforcementState,
    string? ZoneNumber,
    string? ZoneName,
    string? BlockNumber,
    string? ParcelNumber,
    string? RoadCode,
    string? RoadName,
    decimal? MeasuredValue,
    string? MeasuredDisplay,
    decimal? RequiredValue,
    string? RequiredDisplay,
    string Unit,
    string Outcome,
    string OutcomeLabel,
    string Explanation,
    int MetricCrs,
    Guid? ViolationId,
    DateTimeOffset EvaluatedAt);

/// <summary>The result of a manual re-evaluation, summarised for display.</summary>
public sealed record ReEvaluationResultDto(
    Guid DetectionId,
    int RulesEvaluated,
    int Passed,
    int Failed,
    int NotAssessed,
    int ViolationsCreated,
    string Summary,
    IReadOnlyList<ComplianceEvaluationDto> Evaluations);
