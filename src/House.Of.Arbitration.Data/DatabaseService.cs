using House.Of.Arbitration.Data.Abstractions;

namespace House.Of.Arbitration.Data;

public class DatabaseService : IDatabaseService
{
    private readonly AppDbContext _context;

    public DatabaseService(AppDbContext context)
    {
        _context = context;
    }

    public async Task ResetUserDataAsync()
    {
        await _context.ResetUserDataAsync();
    }
}
