using SynqComm.Domain.Entities;

namespace SynqComm.Application.Repositories;

public interface IExternalParticipantIdentityRepository
{
    Task<ExternalParticipantIdentity?> FindByEmailAsync(Guid tenantId, string normalizedEmail, CancellationToken ct = default);
    Task AddAsync(ExternalParticipantIdentity entity, CancellationToken ct = default);
    Task UpdateAsync(ExternalParticipantIdentity entity, CancellationToken ct = default);
}
