using Microsoft.EntityFrameworkCore;
using Tickecheck.Application.Abstractions;
using Tickecheck.Domain.Categories;

namespace Tickecheck.Infrastructure.Persistence.Repositories;

public class CategoryRepository(AppDbContext db) : ICategoryRepository
{
    public Task<List<Category>> ListAsync(CancellationToken cancellationToken = default)
    {
        return db.Categories.AsNoTracking().OrderBy(category => category.Name).ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return db.Categories.AnyAsync(category => category.Id == id, cancellationToken);
    }

    public Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        return db.Categories.AnyAsync(category => category.Name == name, cancellationToken);
    }

    public void Add(Category category)
    {
        db.Categories.Add(category);
    }
}
