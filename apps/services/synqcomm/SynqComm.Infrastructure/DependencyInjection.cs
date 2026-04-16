using BuildingBlocks.Context;
using LegalSynq.AuditClient;
using SynqComm.Application.Interfaces;
using SynqComm.Application.Repositories;
using SynqComm.Application.Services;
using SynqComm.Infrastructure.Audit;
using SynqComm.Infrastructure.Persistence;
using SynqComm.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SynqComm.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSynqCommServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SynqCommDb")
            ?? throw new InvalidOperationException("Connection string 'SynqCommDb' is not configured.");

        services.AddDbContext<SynqCommDbContext>(options =>
            options.UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 0, 0))));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentRequestContext, CurrentRequestContext>();

        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IParticipantRepository, ParticipantRepository>();

        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<IParticipantService, ParticipantService>();

        services.AddAuditEventClient(configuration);
        services.AddScoped<IAuditPublisher, AuditPublisher>();

        return services;
    }
}
