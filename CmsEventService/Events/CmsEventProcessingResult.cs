namespace CmsEventService.Events;

public sealed record CmsEventProcessingResult(
    int Accepted,
    int Failed,
    IReadOnlyCollection<CmsEventFailure> Failures);

public sealed record CmsEventFailure(int Index, string Message);
