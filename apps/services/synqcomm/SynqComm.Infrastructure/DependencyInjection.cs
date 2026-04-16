using BuildingBlocks.Context;
using LegalSynq.AuditClient;
using SynqComm.Application.Interfaces;
using SynqComm.Application.Repositories;
using SynqComm.Application.Services;
using SynqComm.Infrastructure.Audit;
using SynqComm.Infrastructure.Documents;
using SynqComm.Infrastructure.Notifications;
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
        services.AddScoped<IConversationReadStateRepository, ConversationReadStateRepository>();
        services.AddScoped<IMessageAttachmentRepository, MessageAttachmentRepository>();
        services.AddScoped<IEmailMessageReferenceRepository, EmailMessageReferenceRepository>();
        services.AddScoped<IExternalParticipantIdentityRepository, ExternalParticipantIdentityRepository>();
        services.AddScoped<IEmailDeliveryStateRepository, EmailDeliveryStateRepository>();
        services.AddScoped<IEmailRecipientRecordRepository, EmailRecipientRecordRepository>();
        services.AddScoped<ITenantEmailSenderConfigRepository, TenantEmailSenderConfigRepository>();
        services.AddScoped<IEmailTemplateConfigRepository, EmailTemplateConfigRepository>();
        services.AddScoped<IConversationQueueRepository, ConversationQueueRepository>();
        services.AddScoped<IConversationAssignmentRepository, ConversationAssignmentRepository>();
        services.AddScoped<IConversationSlaStateRepository, ConversationSlaStateRepository>();

        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<IParticipantService, ParticipantService>();
        services.AddScoped<IReadTrackingService, ReadTrackingService>();
        services.AddScoped<IMessageAttachmentService, MessageAttachmentService>();
        services.AddScoped<IEmailIntakeService, EmailIntakeService>();
        services.AddScoped<IOutboundEmailService, OutboundEmailService>();
        services.AddScoped<ISenderConfigService, SenderConfigService>();
        services.AddScoped<IEmailTemplateService, EmailTemplateService>();
        services.AddScoped<IQueueService, QueueService>();
        services.AddScoped<IAssignmentService, AssignmentService>();
        services.AddScoped<IOperationalService, OperationalService>();

        var notifBaseUrl = configuration["Services:NotificationsUrl"] ?? "http://localhost:5008";
        services.AddHttpClient("NotificationsService", client =>
        {
            client.BaseAddress = new Uri(notifBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<INotificationsServiceClient, NotificationsServiceClient>();

        var docsBaseUrl = configuration["Services:DocumentsUrl"] ?? "http://localhost:5006";
        services.AddHttpClient("DocumentsService", client =>
        {
            client.BaseAddress = new Uri(docsBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IDocumentServiceClient, DocumentServiceClient>();

        services.AddAuditEventClient(configuration);
        services.AddScoped<IAuditPublisher, AuditPublisher>();

        return services;
    }
}
