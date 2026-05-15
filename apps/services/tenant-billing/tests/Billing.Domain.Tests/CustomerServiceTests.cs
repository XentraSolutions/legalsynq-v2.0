using Billing.Domain.Services;
using Billing.Domain.Tests.Fakes;
using Xunit;

namespace Billing.Domain.Tests;

public class CustomerServiceTests
{
    private static (CustomerService svc, InMemoryCustomerRepository repo) Build()
    {
        var repo = new InMemoryCustomerRepository();
        return (new CustomerService(repo), repo);
    }

    // ---------- CREATE ----------

    [Fact]
    public async Task Create_persists_with_normalized_email_and_trimmed_name()
    {
        var (svc, _) = Build();
        var tenant = Guid.NewGuid();

        var c = await svc.CreateAsync(
            tenant, "  Acme Corp  ", "  Billing@Acme.TEST ",
            phone: "  555-1212  ",
            billingAddress: "1 Main St",
            externalReference: "EXT-1",
            notes: "VIP");

        Assert.Equal(tenant, c.TenantId);
        Assert.Equal("Acme Corp", c.Name);
        Assert.Equal("billing@acme.test", c.Email);
        Assert.Equal("555-1212", c.Phone);
        Assert.False(c.IsDeleted);
        Assert.Equal(c.CreatedAt, c.UpdatedAt);
    }

    [Fact]
    public async Task Create_collapses_blank_optional_fields_to_null()
    {
        var (svc, _) = Build();
        var c = await svc.CreateAsync(
            Guid.NewGuid(), "Acme", "a@b.test",
            phone: "   ", billingAddress: "", externalReference: null, notes: "  ");

        Assert.Null(c.Phone);
        Assert.Null(c.BillingAddress);
        Assert.Null(c.ExternalReference);
        Assert.Null(c.Notes);
    }

    [Fact]
    public async Task Create_rejects_blank_name()
    {
        var (svc, _) = Build();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CreateAsync(Guid.NewGuid(), "   ", "a@b.test", null, null, null, null));
    }

    [Fact]
    public async Task Create_rejects_invalid_email()
    {
        var (svc, _) = Build();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CreateAsync(Guid.NewGuid(), "Acme", "not-an-email", null, null, null, null));
    }

    [Fact]
    public async Task Create_rejects_empty_tenant()
    {
        var (svc, _) = Build();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CreateAsync(Guid.Empty, "Acme", "a@b.test", null, null, null, null));
    }

    [Fact]
    public async Task Create_rejects_duplicate_email_within_same_tenant_case_insensitive()
    {
        var (svc, _) = Build();
        var tenant = Guid.NewGuid();
        await svc.CreateAsync(tenant, "Acme", "billing@acme.test", null, null, null, null);

        await Assert.ThrowsAsync<DuplicateCustomerEmailException>(() =>
            svc.CreateAsync(tenant, "Acme 2", "BILLING@ACME.TEST", null, null, null, null));
    }

    [Fact]
    public async Task Create_allows_same_email_across_tenants()
    {
        var (svc, _) = Build();
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();

        var c1 = await svc.CreateAsync(t1, "Acme", "billing@acme.test", null, null, null, null);
        var c2 = await svc.CreateAsync(t2, "Acme Two", "billing@acme.test", null, null, null, null);

        Assert.NotEqual(c1.Id, c2.Id);
        Assert.Equal(c1.Email, c2.Email);
    }

    // ---------- UPDATE ----------

    [Fact]
    public async Task Update_modifies_fields_and_bumps_updated_at()
    {
        var (svc, _) = Build();
        var tenant = Guid.NewGuid();
        var created = await svc.CreateAsync(tenant, "Old", "old@test.com", null, null, null, null);
        var originalCreated = created.CreatedAt;

        await Task.Delay(10); // ensure UpdatedAt strictly progresses
        var updated = await svc.UpdateAsync(
            tenant, created.Id, "New", "new@test.com",
            phone: "555-9", billingAddress: "Addr", externalReference: "X-1", notes: "N");

        Assert.NotNull(updated);
        Assert.Equal("New", updated!.Name);
        Assert.Equal("new@test.com", updated.Email);
        Assert.Equal(originalCreated, updated.CreatedAt);
        Assert.True(updated.UpdatedAt > originalCreated);
    }

    [Fact]
    public async Task Update_returns_null_for_wrong_tenant()
    {
        var (svc, _) = Build();
        var owning = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var c = await svc.CreateAsync(owning, "A", "a@t.com", null, null, null, null);

        var result = await svc.UpdateAsync(stranger, c.Id, "B", "b@t.com", null, null, null, null);
        Assert.Null(result);
    }

    [Fact]
    public async Task Update_returns_null_for_soft_deleted_customer()
    {
        var (svc, _) = Build();
        var tenant = Guid.NewGuid();
        var c = await svc.CreateAsync(tenant, "A", "a@t.com", null, null, null, null);
        Assert.True(await svc.DeleteAsync(tenant, c.Id));

        var result = await svc.UpdateAsync(tenant, c.Id, "B", "b@t.com", null, null, null, null);
        Assert.Null(result);
    }

    [Fact]
    public async Task Update_rejects_duplicate_email_within_tenant()
    {
        var (svc, _) = Build();
        var tenant = Guid.NewGuid();
        await svc.CreateAsync(tenant, "First", "first@t.com", null, null, null, null);
        var second = await svc.CreateAsync(tenant, "Second", "second@t.com", null, null, null, null);

        await Assert.ThrowsAsync<DuplicateCustomerEmailException>(() =>
            svc.UpdateAsync(tenant, second.Id, "Second", "first@t.com", null, null, null, null));
    }

    [Fact]
    public async Task Update_allows_keeping_same_email()
    {
        var (svc, _) = Build();
        var tenant = Guid.NewGuid();
        var c = await svc.CreateAsync(tenant, "First", "first@t.com", null, null, null, null);

        var updated = await svc.UpdateAsync(tenant, c.Id, "First Updated", "first@t.com", null, null, null, null);
        Assert.NotNull(updated);
        Assert.Equal("First Updated", updated!.Name);
    }

    // ---------- GET ----------

    [Fact]
    public async Task Get_returns_null_for_wrong_tenant()
    {
        var (svc, _) = Build();
        var owning = Guid.NewGuid();
        var c = await svc.CreateAsync(owning, "A", "a@t.com", null, null, null, null);

        var result = await svc.GetAsync(Guid.NewGuid(), c.Id);
        Assert.Null(result);
    }

    [Fact]
    public async Task Get_returns_null_for_deleted_customer()
    {
        var (svc, _) = Build();
        var tenant = Guid.NewGuid();
        var c = await svc.CreateAsync(tenant, "A", "a@t.com", null, null, null, null);
        await svc.DeleteAsync(tenant, c.Id);

        Assert.Null(await svc.GetAsync(tenant, c.Id));
    }

    [Fact]
    public async Task Get_rejects_empty_customer_id()
    {
        var (svc, _) = Build();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.GetAsync(Guid.NewGuid(), Guid.Empty));
    }

    [Fact]
    public async Task Delete_rejects_empty_customer_id()
    {
        var (svc, _) = Build();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.DeleteAsync(Guid.NewGuid(), Guid.Empty));
    }

    // ---------- LIST ----------

    [Fact]
    public async Task List_excludes_soft_deleted_and_other_tenants()
    {
        var (svc, _) = Build();
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        var keep = await svc.CreateAsync(t1, "Keep Me", "keep@t.com", null, null, null, null);
        var del = await svc.CreateAsync(t1, "Delete Me", "del@t.com", null, null, null, null);
        await svc.CreateAsync(t2, "Other Tenant", "other@t.com", null, null, null, null);
        await svc.DeleteAsync(t1, del.Id);

        var page = await svc.ListAsync(t1, null, 1, 25);
        Assert.Single(page.Items);
        Assert.Equal(keep.Id, page.Items[0].Id);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task List_search_matches_name_and_external_reference_case_insensitive()
    {
        var (svc, _) = Build();
        var t = Guid.NewGuid();
        await svc.CreateAsync(t, "Acme Corporation", "ops@acme.test", null, null, "EXT-100", null);
        await svc.CreateAsync(t, "Globex", "ops@globex.test", null, null, "EXT-200", null);

        var byName = await svc.ListAsync(t, "acme", 1, 25);
        var byExternal = await svc.ListAsync(t, "ext-200", 1, 25);

        Assert.Single(byName.Items);
        Assert.Equal("Acme Corporation", byName.Items[0].Name);
        Assert.Single(byExternal.Items);
        Assert.Equal("Globex", byExternal.Items[0].Name);
    }

    [Fact]
    public async Task List_pagination_clamps_pageSize_to_100()
    {
        var (svc, _) = Build();
        var t = Guid.NewGuid();
        for (var i = 0; i < 3; i++)
            await svc.CreateAsync(t, $"C{i}", $"c{i}@t.com", null, null, null, null);

        var page = await svc.ListAsync(t, null, page: 1, pageSize: 1000);
        Assert.Equal(100, page.PageSize);
    }

    [Fact]
    public async Task List_pagination_defaults_when_pageSize_below_one()
    {
        var (svc, _) = Build();
        var t = Guid.NewGuid();
        await svc.CreateAsync(t, "C", "c@t.com", null, null, null, null);

        var page = await svc.ListAsync(t, null, page: 0, pageSize: 0);
        Assert.Equal(1, page.Page);
        Assert.Equal(ICustomerService.DefaultPageSize, page.PageSize);
    }

    [Fact]
    public async Task List_returns_correct_total_pages()
    {
        var (svc, _) = Build();
        var t = Guid.NewGuid();
        for (var i = 0; i < 5; i++)
            await svc.CreateAsync(t, $"C{i}", $"c{i}@t.com", null, null, null, null);

        var page = await svc.ListAsync(t, null, page: 1, pageSize: 2);
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(2, page.Items.Count);
    }

    // ---------- DELETE ----------

    [Fact]
    public async Task Delete_soft_deletes_and_removes_from_list()
    {
        var (svc, _) = Build();
        var t = Guid.NewGuid();
        var c = await svc.CreateAsync(t, "A", "a@t.com", null, null, null, null);

        Assert.True(await svc.DeleteAsync(t, c.Id));

        var page = await svc.ListAsync(t, null, 1, 25);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task Delete_returns_false_for_wrong_tenant()
    {
        var (svc, _) = Build();
        var owning = Guid.NewGuid();
        var c = await svc.CreateAsync(owning, "A", "a@t.com", null, null, null, null);

        Assert.False(await svc.DeleteAsync(Guid.NewGuid(), c.Id));
    }

    [Fact]
    public async Task Delete_returns_false_when_already_deleted()
    {
        var (svc, _) = Build();
        var t = Guid.NewGuid();
        var c = await svc.CreateAsync(t, "A", "a@t.com", null, null, null, null);
        Assert.True(await svc.DeleteAsync(t, c.Id));
        Assert.False(await svc.DeleteAsync(t, c.Id));
    }

    [Fact]
    public async Task Create_after_soft_delete_with_same_email_succeeds()
    {
        // Once a customer is soft-deleted, the same email should be reusable
        // because the email-uniqueness check filters out IsDeleted=true.
        var (svc, _) = Build();
        var t = Guid.NewGuid();
        var first = await svc.CreateAsync(t, "First", "shared@t.com", null, null, null, null);
        await svc.DeleteAsync(t, first.Id);

        var second = await svc.CreateAsync(t, "Second", "shared@t.com", null, null, null, null);
        Assert.NotEqual(first.Id, second.Id);
    }
}
