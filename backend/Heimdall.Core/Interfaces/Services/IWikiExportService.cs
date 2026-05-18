namespace Heimdall.Core.Interfaces.Services;

/// <summary>Wiki export: render wiki pages into downloadable formats.</summary>
public interface IWikiExportService
{
    /// <summary>Export a wiki by its ID to the specified format (e.g., "markdown", "pdf", "html").</summary>
    Task<byte[]> ExportAsync(Guid wikiId, string format);
}
