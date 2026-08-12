namespace CareConnect.Domain;

public class ProviderSpecialty
{
    public Guid ProviderId { get; set; }
    public Guid SpecialtyId { get; set; }
    public bool IsPrimary { get; set; }

    public Provider? Provider { get; set; }
    public Specialty? Specialty { get; set; }
}
