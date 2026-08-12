using System.Globalization;
using Liens.Api.Serialization;
using Liens.Domain;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Api.Endpoints;

internal static class LawFirmChangeHistory
{
    private const string LegacyMetadataMarker = "[legacy-meta]";
    private static readonly Guid ScheduledSwitchActorUserId =
        Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static async Task<bool> RecordAsync(
        LiensDbContext db,
        Guid tenantId,
        Guid caseId,
        string? previousLawFirmId,
        string? newLawFirmId,
        string? switchedDate,
        Guid userId,
        string actorName,
        CancellationToken ct,
        string? previousPendingLawFirmId = null,
        string? previousSwitchedDate = null)
    {
        var previousLawFirm = await ResolveLawFirmAsync(
            db,
            tenantId,
            previousLawFirmId,
            "Unassigned",
            ct);
        var newLawFirm = await ResolveLawFirmAsync(
            db,
            tenantId,
            newLawFirmId,
            "Unassigned",
            ct);

        if (IsFutureSwitch(switchedDate) &&
            HaveSameSwitchDate(switchedDate, previousSwitchedDate) &&
            !string.IsNullOrWhiteSpace(previousPendingLawFirmId))
        {
            var previousPendingLawFirm = await ResolveLawFirmAsync(
                db,
                tenantId,
                previousPendingLawFirmId,
                "Unassigned",
                ct);
            if (string.Equals(
                    previousPendingLawFirm.CanonicalId,
                    newLawFirm.CanonicalId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (string.Equals(previousLawFirm.CanonicalId, newLawFirm.CanonicalId, StringComparison.OrdinalIgnoreCase))
            return false;

        // A legacy identifier can be an organization ID rather than a contact ID.
        // If both identifiers fall back to the same organization label, retain the
        // identifiers in the history entry so the change is not misleading.
        var previousLawFirmLabel = previousLawFirm.Label;
        var newLawFirmLabel = newLawFirm.Label;
        if (string.Equals(previousLawFirmLabel, newLawFirmLabel, StringComparison.OrdinalIgnoreCase))
        {
            previousLawFirmLabel = FirstNonEmpty(previousLawFirmId, previousLawFirmLabel) ?? "Unassigned";
            newLawFirmLabel = FirstNonEmpty(newLawFirmId, newLawFirmLabel) ?? "Unassigned";
        }

        db.LienCaseNotes.Add(LienCaseNote.Create(
            caseId,
            tenantId,
            BuildDescription(previousLawFirmLabel, newLawFirmLabel, switchedDate, actorName),
            CaseNoteCategory.SettlementHistory,
            userId,
            actorName));
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static async Task<ResolvedLawFirm> ResolveLawFirmAsync(
        LiensDbContext db,
        Guid tenantId,
        string? identifier,
        string fallback,
        CancellationToken ct)
    {
        if (!Guid.TryParse(identifier, out var parsedId))
        {
            var normalized = FirstNonEmpty(identifier, fallback) ?? fallback;
            return new ResolvedLawFirm(normalized, normalized);
        }

        var candidates = await db.Contacts.AsNoTracking()
            .Where(contact =>
                contact.TenantId == tenantId &&
                (contact.Id == parsedId || contact.OrgId == parsedId))
            .Select(contact => new
            {
                contact.Id,
                contact.OrgId,
                contact.ContactType,
                contact.Organization,
                contact.DisplayName,
            })
            .ToListAsync(ct);

        var contact = candidates.FirstOrDefault(candidate => candidate.Id == parsedId) ?? candidates
            .OrderByDescending(candidate => candidate.ContactType == ContactType.LawFirm)
            .ThenBy(candidate => candidate.Id)
            .FirstOrDefault();
        var canonicalId = contact?.Id.ToString() ?? parsedId.ToString();
        var label = FirstNonEmpty(contact?.Organization, contact?.DisplayName, identifier, fallback) ?? fallback;
        return new ResolvedLawFirm(canonicalId, label);
    }

    private static string BuildDescription(
        string previousLawFirm,
        string newLawFirm,
        string? switchedDate,
        string actorName)
    {
        if (TryParseSwitchDate(switchedDate, out var parsedSwitchDate) && IsFutureSwitch(parsedSwitchDate))
        {
            return $"Scheduled law firm switch from {previousLawFirm} to {newLawFirm} " +
                   $"on {parsedSwitchDate:MM/dd/yyyy} by {actorName}";
        }

        return $"Law firm switched from {previousLawFirm} to {newLawFirm} by {actorName}";
    }

    public static bool IsFutureSwitch(string? switchedDate) =>
        TryParseSwitchDate(switchedDate, out var parsedSwitchDate) && IsFutureSwitch(parsedSwitchDate);

    public static async Task<bool> IsSamePendingSwitchAsync(
        LiensDbContext db,
        Guid tenantId,
        string? existingPendingLawFirmId,
        string? existingSwitchedDate,
        string? requestedLawFirmId,
        string? requestedSwitchedDate,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(existingPendingLawFirmId) ||
            string.IsNullOrWhiteSpace(requestedLawFirmId) ||
            !HaveSameSwitchDate(existingSwitchedDate, requestedSwitchedDate))
        {
            return false;
        }

        var existingPending = await ResolveLawFirmAsync(
            db,
            tenantId,
            existingPendingLawFirmId,
            "Unassigned",
            ct);
        var requested = await ResolveLawFirmAsync(
            db,
            tenantId,
            requestedLawFirmId,
            "Unassigned",
            ct);
        return string.Equals(existingPending.CanonicalId, requested.CanonicalId, StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<int> ApplyDueScheduledSwitchesAsync(
        LiensDbContext db,
        DateOnly today,
        CancellationToken ct)
    {
        var candidates = await db.Cases.AsNoTracking()
            .Where(item =>
                item.Notes != null &&
                item.Notes.Contains("pendingLawFirmId=") &&
                item.Notes.Contains("switchedDate="))
            .ToListAsync(ct);

        var applied = 0;
        foreach (var caseEntity in candidates)
        {
            var (noteBody, metadata) = ParseCaseNotes(caseEntity.Notes);
            if (!metadata.TryGetValue("pendingLawFirmId", out var pendingLawFirmId) ||
                string.IsNullOrWhiteSpace(pendingLawFirmId) ||
                !metadata.TryGetValue("switchedDate", out var switchedDate) ||
                !TryParseSwitchDate(switchedDate, out var parsedSwitchDate) ||
                parsedSwitchDate > today)
            {
                continue;
            }

            metadata["lawFirmId"] = pendingLawFirmId.Trim();
            metadata.Remove("lawFirm");
            metadata.Remove("pendingLawFirmId");
            metadata.Remove("switchedDate");
            var originalNotes = caseEntity.Notes;
            var promotedNotes = SerializeCaseNotes(noteBody, metadata);
            var actorUserId = caseEntity.UpdatedByUserId ?? caseEntity.CreatedByUserId ?? ScheduledSwitchActorUserId;

            if (db.Database.IsRelational())
            {
                var updatedAtUtc = DateTime.UtcNow;
                applied += await db.Cases
                    .Where(item =>
                        item.TenantId == caseEntity.TenantId &&
                        item.Id == caseEntity.Id &&
                        item.Notes == originalNotes)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(item => item.Notes, promotedNotes)
                            .SetProperty(item => item.UpdatedByUserId, actorUserId)
                            .SetProperty(item => item.UpdatedAtUtc, updatedAtUtc),
                        ct);
                continue;
            }

            var currentCase = await db.Cases.SingleOrDefaultAsync(
                item =>
                    item.TenantId == caseEntity.TenantId &&
                    item.Id == caseEntity.Id &&
                    item.Notes == originalNotes,
                ct);
            if (currentCase is null)
                continue;

            currentCase.Update(
                currentCase.ClientFirstName,
                currentCase.ClientLastName,
                actorUserId,
                currentCase.Title,
                currentCase.ExternalReference,
                currentCase.ClientDob,
                currentCase.ClientPhone,
                currentCase.ClientEmail,
                currentCase.ClientAddress,
                currentCase.DateOfIncident,
                currentCase.InsuranceCarrier,
                currentCase.PolicyNumber,
                currentCase.ClaimNumber,
                currentCase.Description,
                promotedNotes);
            applied++;
        }

        if (applied > 0)
            await db.SaveChangesAsync(ct);

        return applied;
    }

    private static bool IsFutureSwitch(DateOnly switchedDate) =>
        switchedDate > DateOnly.FromDateTime(PacificTimeHelper.Convert(DateTime.UtcNow).Date);

    private static bool HaveSameSwitchDate(string? left, string? right) =>
        TryParseSwitchDate(left, out var leftDate) &&
        TryParseSwitchDate(right, out var rightDate) &&
        leftDate == rightDate;

    private static (string? NoteBody, Dictionary<string, string> Metadata) ParseCaseNotes(string? notes)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(notes))
            return (null, metadata);

        var markerIndex = notes.IndexOf(LegacyMetadataMarker, StringComparison.Ordinal);
        var noteBody = markerIndex >= 0 ? FirstNonEmpty(notes[..markerIndex]) : null;
        var rawMetadata = markerIndex >= 0
            ? notes[(markerIndex + LegacyMetadataMarker.Length)..].Trim()
            : notes;

        foreach (var segment in rawMetadata.Split("; ", StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = segment.IndexOf('=');
            if (separatorIndex <= 0)
                continue;

            var key = segment[..separatorIndex].Trim();
            var value = segment[(separatorIndex + 1)..].Trim();
            if (key.Length > 0)
                metadata[key] = value;
        }

        return (noteBody, metadata);
    }

    private static string? SerializeCaseNotes(string? noteBody, Dictionary<string, string> metadata)
    {
        var serialized = string.Join("; ", metadata.Select(pair => $"{pair.Key}={pair.Value}"));
        if (string.IsNullOrWhiteSpace(serialized))
            return FirstNonEmpty(noteBody);

        return string.IsNullOrWhiteSpace(noteBody)
            ? $"{LegacyMetadataMarker}{Environment.NewLine}{serialized}"
            : $"{noteBody.Trim()}{Environment.NewLine}{Environment.NewLine}{LegacyMetadataMarker}{Environment.NewLine}{serialized}";
    }

    private static bool TryParseSwitchDate(string? value, out DateOnly parsedDate)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            parsedDate = default;
            return false;
        }

        return DateOnly.TryParseExact(
                   value.Trim(),
                   ["yyyy-MM-dd", "MM/dd/yyyy", "M/d/yyyy"],
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.None,
                   out parsedDate) ||
               DateOnly.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate);
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private sealed record ResolvedLawFirm(string CanonicalId, string Label);
}
