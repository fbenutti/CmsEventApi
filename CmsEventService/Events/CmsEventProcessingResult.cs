namespace CmsEventService.Events;

public sealed record CmsEventProcessingResult(
    int Accepted,
    int Failed,
    IReadOnlyCollection<CmsEventFailure> Failures,
    int Ignored = 0);

public sealed record CmsEventFailure(int Index, string Message);
