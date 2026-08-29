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
}
