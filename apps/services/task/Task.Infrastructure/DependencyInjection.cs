using BuildingBlocks.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Task.Application.Interfaces;
using Task.Application.Services;
using Task.Infrastructure.Persistence;
using Task.Infrastructure.Persistence.Repositories;

namespace Task.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTaskServices(
        this IServiceCollection services,
        IConfiguration          configuration)
    {
        var connectionString = configuration.GetConnectionString("TasksDb")
            ?? throw new InvalidOperationException(
                "Connection string 'TasksDb' is not configured. " +
                "Set it via the environment variable 'ConnectionStrings__TasksDb'.");

        services.AddDbContext<TasksDbContext>(options =>
            options.UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 0, 0))));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentRequestContext, CurrentRequestContext>();

        services.AddScoped<ITaskRepository,        TaskRepository>();
        services.AddScoped<ITaskNoteRepository,     TaskNoteRepository>();
        services.AddScoped<ITaskHistoryRepository,  TaskHistoryRepository>();
        services.AddScoped<IUnitOfWork,             UnitOfWork>();
        services.AddScoped<ITaskService,            TaskService>();

        return services;
    }
}
