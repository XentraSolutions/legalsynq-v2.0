using CareConnect.Domain;

namespace CareConnect.Application.Repositories;

public interface ICategoryRepository
{
    Task<List<Category>> GetAllActiveAsync(CancellationToken ct = default);
}
