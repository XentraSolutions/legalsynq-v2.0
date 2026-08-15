namespace Liens.Application.DTOs;

public sealed class CaseNotesHistoryRequest
{
    public string? NoteType { get; init; }
    public int Page { get; init; } = 1;
    public int Limit { get; init; } = 10;
    public string? SortBy { get; init; } = "noteDate";
    public string? SortDirection { get; init; } = "desc";
}

public sealed class CaseNotesHistoryQuery
{
    public required string NoteType { get; init; }
    public required int Page { get; init; }
    public required int Limit { get; init; }
    public required string SortBy { get; init; }
    public required string SortDirection { get; init; }
}

public sealed class CaseNotesHistoryRow
{
    public Guid NoteId { get; init; }
    public Guid CaseRecordId { get; init; }
    public string CaseId { get; init; } = string.Empty;
    public string CaseName { get; init; } = string.Empty;
    public string NoteType { get; init; } = string.Empty;
    public string NoteTypeLabel { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public string NoteAuthor { get; init; } = string.Empty;
    public string NoteContent { get; init; } = string.Empty;
}

public sealed class CaseNotesHistoryPage
{
    public IReadOnlyList<CaseNotesHistoryRow> Items { get; init; } = [];
    public int Page { get; init; }
    public int Limit { get; init; }
    public int TotalCount { get; init; }
    public int ExcludedUnreconciledLegacyNoteCount { get; init; }
}

public sealed class CaseNotesHistoryExport
{
    public byte[] Content { get; init; } = [];
    public bool SizeLimitExceeded { get; init; }
    public int ExcludedUnreconciledLegacyNoteCount { get; init; }
}
