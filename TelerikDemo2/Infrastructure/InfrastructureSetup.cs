using Microsoft.EntityFrameworkCore;
using TelerikDemo2.Infrastructure.Persistence;
using TelerikDemo2.Infrastructure.Storage;

namespace TelerikDemo2.Infrastructure;

public static class InfrastructureSetup
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.Configure<AttachmentOptions>(configuration.GetSection(AttachmentOptions.SectionName));
        services.AddScoped<IAttachmentStore, FileSystemAttachmentStore>();

        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
