using Tickecheck.Domain.Categories;

namespace Tickecheck.Application.Abstractions;

public interface ICategoryRepository
{
    Task<List<Category>> ListAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken = default);

    void Add(Category category);
}
