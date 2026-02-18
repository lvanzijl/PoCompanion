using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;

namespace PoTool.Api.Services;

public sealed class ProductOwnerRevisionSourceOverrideProvider : IProductOwnerRevisionSourceOverrideProvider
{
    private readonly PoToolDbContext _dbContext;

    public ProductOwnerRevisionSourceOverrideProvider(PoToolDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RevisionSource?> GetOverrideAsync(int productOwnerId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Profiles
            .Where(profile => profile.Id == productOwnerId)
            .Select(profile => profile.RevisionSourceOverride)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
