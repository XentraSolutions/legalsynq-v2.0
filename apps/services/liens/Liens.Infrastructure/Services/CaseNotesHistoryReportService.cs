using System.Globalization;
using System.Text;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Infrastructure.Services;

public sealed class CaseNotesHistoryReportService : ICaseNotesHistoryReportService
{
    internal const int ExportByteLimit = 10 * 1024 * 1024;
    internal const string ReconciledSourceHashPrefix = "case-note-v2:";

    private readonly LiensDbContext _db;

    public CaseNotesHistoryReportService(LiensDbContext db) => _db = db;

    public async Task<CaseNotesHistoryPage> GetAsync(
        Guid tenantId,
        CaseNotesHistoryQuery query,
        CancellationToken ct = default)
    {
        var eligible = BuildEligibleQuery(tenantId, query.NoteType);
        var unreconciledLegacyNoteIds = BuildUnreconciledLegacyNoteIds(tenantId);
        var excludedCount = await eligible.CountAsync(
            row => unreconciledLegacyNoteIds.Contains(row.NoteId),
            ct);
        var filtered = eligible.Where(row => !unreconciledLegacyNoteIds.Contains(row.NoteId));
        var totalCount = await filtered.CountAsync(ct);
        var offset = ((long)query.Page - 1L) * query.Limit;

        if (totalCount == 0 || offset >= totalCount || offset > int.MaxValue)
        {
            return new CaseNotesHistoryPage
            {
                Page = query.Page,
                Limit = query.Limit,
                TotalCount = totalCount,
                ExcludedUnreconciledLegacyNoteCount = excludedCount,
            };
        }

        var items = await ApplyOrdering(filtered, query)
            .Skip((int)offset)
            .Take(query.Limit)
            .ToListAsync(ct);

        return new CaseNotesHistoryPage
        {
            Items = items,
            Page = query.Page,
            Limit = query.Limit,
            TotalCount = totalCount,
            ExcludedUnreconciledLegacyNoteCount = excludedCount,
        };
    }

    public async Task<CaseNotesHistoryExport> ExportCsvAsync(
        Guid tenantId,
        CaseNotesHistoryQuery query,
        CancellationToken ct = default)
    {
        var eligible = BuildEligibleQuery(tenantId, query.NoteType);
        var unreconciledLegacyNoteIds = BuildUnreconciledLegacyNoteIds(tenantId);
        var excludedCount = await eligible.CountAsync(
            row => unreconciledLegacyNoteIds.Contains(row.NoteId),
            ct);
        var filtered = eligible.Where(row => !unreconciledLegacyNoteIds.Contains(row.NoteId));

        await using var stream = new MemoryStream();
        if (!TryAppendCsvLine(stream, ["Case ID", "Case Name", "Note Type", "Note Date", "Note Author", "Note Content"]))
            return new CaseNotesHistoryExport
            {
                SizeLimitExceeded = true,
                ExcludedUnreconciledLegacyNoteCount = excludedCount,
            };

        await foreach (var row in ApplyOrdering(filtered, query)
                           .AsAsyncEnumerable()
                           .WithCancellation(ct))
        {
            if (!TryAppendCsvLine(stream,
                [
                    row.CaseId,
                    row.CaseName,
                    row.NoteTypeLabel,
                    FormatPacificDate(row.CreatedAtUtc),
                    row.NoteAuthor,
                    row.NoteContent,
                ]))
            {
                return new CaseNotesHistoryExport
                {
                    SizeLimitExceeded = true,
                    ExcludedUnreconciledLegacyNoteCount = excludedCount,
                };
            }
        }

        return new CaseNotesHistoryExport
        {
            Content = stream.ToArray(),
            ExcludedUnreconciledLegacyNoteCount = excludedCount,
        };
    }

    private IQueryable<Guid> BuildUnreconciledLegacyNoteIds(Guid tenantId)
        => _db.LegacyIdCrosswalks
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId &&
                           item.SourceSystem == "SL-CORE" &&
                           item.SourceTable == "SL_CASE_NOTES" &&
                           item.TargetEntity == "CaseNote" &&
                           !item.SourceHash.StartsWith(ReconciledSourceHashPrefix))
            .Select(item => item.TargetId);

    private IQueryable<CaseNotesHistoryRow> BuildEligibleQuery(Guid tenantId, string noteType)
    {
        var notes = _db.LienCaseNotes.AsNoTracking();
        var cases = _db.Cases.AsNoTracking();

        var query =
            from note in notes
            join caseEntity in cases
                on new { note.CaseId, note.TenantId }
                equals new { CaseId = caseEntity.Id, caseEntity.TenantId }
            where note.TenantId == tenantId &&
                  caseEntity.TenantId == tenantId &&
                  !note.IsDeleted &&
                  note.Content.Trim() != string.Empty
            select new { note, caseEntity };

        if (string.Equals(noteType, "TRACKING", StringComparison.Ordinal))
            query = query.Where(item =>
                item.note.Category == CaseNoteCategory.General ||
                item.note.Category == CaseNoteCategory.FollowUp);
        else
            query = query.Where(item => item.note.Category == CaseNoteCategory.Feed);

        return query.Select(item => new CaseNotesHistoryRow
        {
            NoteId = item.note.Id,
            CaseRecordId = item.caseEntity.Id,
            CaseId = item.caseEntity.CaseNumber,
            CaseName = (item.caseEntity.ClientFirstName + " " + item.caseEntity.ClientLastName).Trim(),
            NoteType = noteType,
            NoteTypeLabel = noteType == "TRACKING" ? "Case Tracking Note" : "Feed Note",
            CreatedAtUtc = item.note.CreatedAtUtc,
            NoteAuthor = item.note.CreatedByName,
            NoteContent = item.note.Content,
        });
    }

    private static IOrderedQueryable<CaseNotesHistoryRow> ApplyOrdering(
        IQueryable<CaseNotesHistoryRow> query,
        CaseNotesHistoryQuery request)
    {
        var ascending = string.Equals(request.SortDirection, "asc", StringComparison.Ordinal);
        IOrderedQueryable<CaseNotesHistoryRow> ordered = request.SortBy switch
        {
            "caseId" => ascending ? query.OrderBy(item => item.CaseId) : query.OrderByDescending(item => item.CaseId),
            "caseName" => ascending ? query.OrderBy(item => item.CaseName) : query.OrderByDescending(item => item.CaseName),
            "noteType" => ascending ? query.OrderBy(item => item.NoteType) : query.OrderByDescending(item => item.NoteType),
            "noteAuthor" => ascending ? query.OrderBy(item => item.NoteAuthor) : query.OrderByDescending(item => item.NoteAuthor),
            "noteContent" => ascending ? query.OrderBy(item => item.NoteContent) : query.OrderByDescending(item => item.NoteContent),
            _ => ascending ? query.OrderBy(item => item.CreatedAtUtc) : query.OrderByDescending(item => item.CreatedAtUtc),
        };

        return ascending
            ? ordered.ThenBy(item => item.CreatedAtUtc).ThenBy(item => item.NoteId)
            : ordered.ThenByDescending(item => item.CreatedAtUtc).ThenByDescending(item => item.NoteId);
    }

    private static bool TryAppendCsvLine(Stream stream, IEnumerable<string?> values)
    {
        var line = string.Join(',', values.Select(EscapeCsvField)) + "\r\n";
        var bytes = Encoding.UTF8.GetBytes(line);
        if (stream.Length + bytes.Length > ExportByteLimit)
            return false;

        stream.Write(bytes, 0, bytes.Length);
        return true;
    }

    internal static string EscapeCsvField(string? value)
    {
        if (value is null)
            return string.Empty;

        var firstSignificant = value.FirstOrDefault(character => !char.IsWhiteSpace(character) && !char.IsControl(character));
        var safeValue = firstSignificant is '=' or '+' or '-' or '@' ? $"'{value}" : value;
        return safeValue.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{safeValue.Replace("\"", "\"\"")}\""
            : safeValue;
    }

    private static string FormatPacificDate(DateTime value)
    {
        var utc = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        foreach (var id in new[] { "Pacific Standard Time", "America/Los_Angeles" })
        {
            try
            {
                return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById(id))
                    .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return utc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
