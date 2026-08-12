namespace CareConnect.Application.DTOs;

public class CreateFacilityRequest
{
    public string Name { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? GeoPointSource { get; set; }

    // Phase 4: optional Identity Organization linkage.
    // When supplied, the service calls Facility.LinkOrganization so the
    // facility can be resolved cross-service by its canonical org ID.
    public Guid? OrganizationId { get; set; }
}

public class UpdateFacilityRequest
{
    public string Name { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? GeoPointSource { get; set; }

    // Phase 4: optional Identity Organization linkage.
    // Allows backfilling OrganizationId via an update call without recreating the facility.
    public Guid? OrganizationId { get; set; }
}

public class FacilityResponse
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string AddressLine1 { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string? PostalCode { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public bool IsActive { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string? GeoPointSource { get; init; }
    public DateTime? GeoUpdatedAtUtc { get; init; }
    public bool IsMobile { get; init; }
    public double? ServiceRadiusMiles { get; init; }

    // Phase 4: canonical Identity Organization FK. Null for legacy facilities
    // that predate the org-alignment migration.
    public Guid? OrganizationId { get; init; }
}
