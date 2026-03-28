using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Application.Services;
using CareConnect.Infrastructure.Data;
using CareConnect.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CareConnect.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("CareConnectDb")
            ?? throw new InvalidOperationException("Connection string 'CareConnectDb' is not configured.");

        services.AddDbContext<CareConnectDbContext>(options =>
            options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0))));

        services.AddScoped<IProviderRepository, ProviderRepository>();
        services.AddScoped<IReferralRepository, ReferralRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IFacilityRepository, FacilityRepository>();
        services.AddScoped<IServiceOfferingRepository, ServiceOfferingRepository>();
        services.AddScoped<IAvailabilityTemplateRepository, AvailabilityTemplateRepository>();
        services.AddScoped<IAppointmentSlotRepository, AppointmentSlotRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IAppointmentStatusHistoryRepository, AppointmentStatusHistoryRepository>();
        services.AddScoped<IAvailabilityExceptionRepository, AvailabilityExceptionRepository>();
        services.AddScoped<IReferralNoteRepository, ReferralNoteRepository>();
        services.AddScoped<IAppointmentNoteRepository, AppointmentNoteRepository>();
        services.AddScoped<IReferralAttachmentRepository, ReferralAttachmentRepository>();
        services.AddScoped<IAppointmentAttachmentRepository, AppointmentAttachmentRepository>();

        services.AddScoped<IProviderService, ProviderService>();
        services.AddScoped<IReferralService, ReferralService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IFacilityService, FacilityService>();
        services.AddScoped<IServiceOfferingService, ServiceOfferingService>();
        services.AddScoped<IAvailabilityTemplateService, AvailabilityTemplateService>();
        services.AddScoped<ISlotGenerationService, SlotGenerationService>();
        services.AddScoped<IAppointmentService, AppointmentService>();
        services.AddScoped<IAvailabilityExceptionService, AvailabilityExceptionService>();
        services.AddScoped<IReferralNoteService, ReferralNoteService>();
        services.AddScoped<IAppointmentNoteService, AppointmentNoteService>();
        services.AddScoped<IReferralAttachmentService, ReferralAttachmentService>();
        services.AddScoped<IAppointmentAttachmentService, AppointmentAttachmentService>();

        return services;
    }
}
