namespace CmsEventService.Options;

public sealed class DatabaseOptions
{
    public string WriterConnectionString { get; set; } = "Data Source=cms-events.db";

    public string ReaderConnectionString { get; set; } = "Data Source=cms-events.db;Mode=ReadOnly";
}
