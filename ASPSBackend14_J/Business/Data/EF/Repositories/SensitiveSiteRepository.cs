using Common.Entities;
using Interface.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Business.Data.EF.Repositories;

public class SensitiveSiteRepository : ISensitiveSiteRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<SensitiveSiteRepository> _logger;

    public SensitiveSiteRepository(
        AppDbContext context,
        ILogger<SensitiveSiteRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<SensitiveSite>> GetAllActiveAsync()
    {
        return await _context.SensitiveSites
            .Where(s => s.IsActive && s.DateDeleted == null)
            .ToListAsync();
    }
}
