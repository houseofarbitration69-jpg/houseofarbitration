using House.Of.Arbitration.Data.Abstractions;

namespace House.Of.Arbitration.Data;

public static class BuilderExtensions
{
    /// <summary>
    /// Registers localization services with the dependency injection container.
    /// </summary>
    /// <param name="builder">The <see cref="MauiAppBuilder"/> to add the services to.</param>
    /// <returns>The configured <see cref="MauiAppBuilder"/>.</returns>
    public static MauiAppBuilder RegisterDbContext(this MauiAppBuilder builder)
    {
        builder.Services.AddDbContext<AppDbContext>();
        builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        return builder;
    }
}
