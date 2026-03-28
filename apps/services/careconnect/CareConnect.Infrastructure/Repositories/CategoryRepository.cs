using CareConnect.Application.Repositories;
using CareConnect.Domain;
using CareConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CareConnect.Infrastructure.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly CareConnectDbContext _db;

    public CategoryRepository(CareConnectDbContext db)
    {
        _db = db;
    }

    public async Task<List<Category>> GetAllActiveAsync(CancellationToken ct = default)
    {
        return await _db.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
    }
}
