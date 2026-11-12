using FluentValidation;
using Tickecheck.Application.Abstractions;
using Tickecheck.Application.Common;
using Tickecheck.Domain.Categories;

namespace Tickecheck.Application.Categories;

public record CategoryDto(Guid Id, string Name);

public record CreateCategoryCommand(string Name);

public class CreateCategoryValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(100);
    }
}

public class CreateCategoryHandler(ICategoryRepository categories, IUnitOfWork unitOfWork)
{
    public async Task<CategoryDto> HandleAsync(CreateCategoryCommand command, CancellationToken cancellationToken = default)
    {
        new CreateCategoryValidator().ValidateAndThrow(command);

        if (await categories.NameExistsAsync(command.Name.Trim(), cancellationToken))
        {
            throw new ConflictException("A category with that name already exists.");
        }

        var category = new Category(command.Name);
        categories.Add(category);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CategoryDto(category.Id, category.Name);
    }
}

public class GetCategoriesHandler(ICategoryRepository categories)
{
    public async Task<List<CategoryDto>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var list = await categories.ListAsync(cancellationToken);
        return list.Select(category => new CategoryDto(category.Id, category.Name)).ToList();
    }
}
