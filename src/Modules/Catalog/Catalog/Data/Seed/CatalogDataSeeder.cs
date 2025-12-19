
using Shared.Data.Seed;

namespace Catalog.Data.Seed;

public class CatalogDataSeeder (CatalogDbContext dbContext)
    : IDataSeeder
{
    public async Task SeedAllAsync()
    {
       if(!await dbContext.Products.AnyAsync())
       {
           await dbContext.Products.AddRangeAsync(IntialData.Products);
           await dbContext.SaveChangesAsync();
        }
    }
}
