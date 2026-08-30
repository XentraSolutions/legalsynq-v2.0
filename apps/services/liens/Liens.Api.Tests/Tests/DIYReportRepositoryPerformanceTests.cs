using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Liens.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Liens.Api.Tests.Tests;

public sealed class DIYReportRepositoryPerformanceTests
{
    [Fact]
    public async Task Report_reads_are_untracked_and_only_load_required_servicing_task_types()
    {
        var options = new DbContextOptionsBuilder<LiensDbContext>()
            .UseInMemoryDatabase($"diy-report-read-{Guid.CreateVersion7()}")
            .Options;
        await using var db = new LiensDbContext(options);
        var tenantId = Guid.CreateVersion7();
        var orgId = Guid.CreateVersion7();
        var actorId = Guid.CreateVersion7();
        var lien = Lien.Create(
            tenantId,
            orgId,
            $"LIEN-DIY-READ-{Guid.CreateVersion7():N}"[..36],
            LienType.MedicalLien,
            100m,
            actorId,
            isBulk: "N");
        var medicalCode = CreateServicingItem("LegacyMedicalCode", "TASK-DIY-CODE", lien.Id);
        var facilityInfo = CreateServicingItem("LegacyMedicalFacilityInfo", "TASK-DIY-FACILITY", lien.Id);
        var unrelatedTask = CreateServicingItem("LegacyCaseDocument", "TASK-DIY-DOCUMENT", lien.Id);

        db.Liens.Add(lien);
        db.ServicingItems.AddRange(medicalCode, facilityInfo, unrelatedTask);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var liens = await new LienRepository(db).SearchReportAsync(
            tenantId,
            search: null,
            lienStatuses: [],
            caseStatuses: [],
            purchaseDateFrom: null,
            purchaseDateTo: null,
            closedDateFrom: null,
            closedDateTo: null,
            useSettlementDateForClosedFilter: false,
            isBulk: null,
            caseIds: [],
            CancellationToken.None);
        var servicingItems = await new ServicingItemRepository(db).GetByLienIdsAsync(
            tenantId,
            [lien.Id],
            ["LegacyMedicalCode", "LegacyMedicalFacilityInfo"],
            CancellationToken.None);

        liens.Should().ContainSingle(item => item.Id == lien.Id);
        servicingItems.Select(item => item.TaskType).Should().BeEquivalentTo(
            "LegacyMedicalCode",
            "LegacyMedicalFacilityInfo");
        db.ChangeTracker.Entries<Lien>().Should().BeEmpty();
        db.ChangeTracker.Entries<ServicingItem>().Should().BeEmpty();

        ServicingItem CreateServicingItem(string taskType, string taskNumber, Guid lienId) =>
            ServicingItem.Create(
                tenantId,
                orgId,
                taskNumber,
                taskType,
                "DIY report test item",
                "Test User",
                actorId,
                lienId: lienId);
    }

    [Fact]
    public async Task Case_note_report_reads_use_canonical_categories_and_report_index()
    {
        var options = new DbContextOptionsBuilder<LiensDbContext>()
            .UseInMemoryDatabase($"diy-report-note-read-{Guid.CreateVersion7()}")
            .Options;
        await using var db = new LiensDbContext(options);
        var tenantId = Guid.CreateVersion7();
        var caseId = Guid.CreateVersion7();
        var actorId = Guid.CreateVersion7();
        var feedNote = LienCaseNote.Create(
            caseId,
            tenantId,
            "Latest feed note",
            " Feed ",
            actorId,
            "Report Author");

        feedNote.Category.Should().Be(CaseNoteCategory.Feed);
        db.LienCaseNotes.Add(feedNote);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var notes = await new LienCaseNoteRepository(db).GetLatestFeedByCaseIdsAsync(
            tenantId,
            [caseId],
            CancellationToken.None);

        notes.Should().ContainSingle(note =>
            note.CaseId == caseId &&
            note.Category == CaseNoteCategory.Feed &&
            note.Content == "Latest feed note");
        db.ChangeTracker.Entries<LienCaseNote>().Should().BeEmpty();

        var reportIndex = db.Model.FindEntityType(typeof(LienCaseNote))!
            .GetIndexes()
            .Single(index => index.GetDatabaseName() == "IX_CaseNotes_ReportLookup");
        reportIndex.Properties.Select(property => property.Name).Should().Equal(
            nameof(LienCaseNote.TenantId),
            nameof(LienCaseNote.CaseId),
            nameof(LienCaseNote.IsDeleted),
            nameof(LienCaseNote.Category),
            nameof(LienCaseNote.CreatedAtUtc),
            nameof(LienCaseNote.Id));
    }

    [Fact]
    public void Latest_feed_report_query_translates_without_wrapping_the_category_column()
    {
        var options = new DbContextOptionsBuilder<LiensDbContext>()
            .UseMySql(
                "Server=127.0.0.1;Port=3306;Database=query_translation_only;User=root;Password=ignored;",
                new MySqlServerVersion(new Version(8, 0, 0)))
            .Options;
        using var db = new LiensDbContext(options);
        var query = new LienCaseNoteRepository(db).BuildLatestFeedReportQuery(
            Guid.CreateVersion7(),
            [Guid.CreateVersion7()]);

        var sql = query.ToQueryString();

        sql.Should().Contain("Category");
        sql.ToUpperInvariant().Should().NotContain("LOWER(");
    }
}
