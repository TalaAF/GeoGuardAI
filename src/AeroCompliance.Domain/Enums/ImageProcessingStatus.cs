namespace AeroCompliance.Domain.Enums;

public enum ImageProcessingStatus
{
    Uploading, Stored, Queued, Analyzing, AnalysisCompleted, EvaluatingCompliance,
    Completed, UploadFailed, AnalysisFailed, ComplianceFailed
}
