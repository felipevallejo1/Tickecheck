using FluentValidation;
using Moq;
using Tickecheck.Application.Abstractions;
using Tickecheck.Application.Categories;
using Tickecheck.Application.Common;
using Tickecheck.Application.Dashboard;
using Tickecheck.Application.Tenants;
using Tickecheck.Application.Users;
using Tickecheck.Domain.Categories;
using Tickecheck.Domain.Tenants;
using Tickecheck.Domain.Tickets;
using Tickecheck.Domain.Users;

namespace Tickecheck.Application.Tests.Admin;

/// <summary>
/// Use cases for managing companies, users and categories, plus the dashboard.
/// </summary>
public class AdminHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ITenantRepository> _tenants = new();
    private readonly Mock<ICategoryRepository> _categories = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public AdminHandlerTests()
    {
        _passwordHasher.Setup(hasher => hasher.Hash(It.IsAny<string>())).Returns("hashed-password");
        _tenantProvider.Setup(provider => provider.TenantId).Returns(TestSupport.TenantId);
    }

    // ---------- Tenants ----------

    [Fact]
    public async Task Create_tenant_also_creates_its_first_admin_in_one_save()
    {
        Tenant? createdTenant = null;
        _tenants.Setup(repo => repo.Add(It.IsAny<Tenant>())).Callback((Tenant tenant) => createdTenant = tenant);
        var handler = NewCreateTenantHandler();

        var result = await handler.HandleAsync(new CreateTenantCommand("Initech", "Boss@Initech.com", "The Boss", "Password1!"));

        Assert.Equal("Initech", result.Name);
        _users.Verify(repo => repo.Add(It.Is<User>(user =>
            user.TenantId == createdTenant!.Id
            && user.Role == UserRole.TenantAdmin
            && user.Email == "boss@initech.com"
            && user.PasswordHash == "hashed-password")), Times.Once);
        _unitOfWork.Verify(unit => unit.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Create_tenant_with_an_email_already_in_use_is_a_conflict()
    {
        _users.Setup(repo => repo.EmailExistsIgnoringTenantAsync("boss@initech.com", default)).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(
            () => NewCreateTenantHandler().HandleAsync(new CreateTenantCommand("Initech", "boss@initech.com", "The Boss", "Password1!")));

        _tenants.Verify(repo => repo.Add(It.IsAny<Tenant>()), Times.Never);
    }

    [Fact]
    public async Task Create_tenant_with_a_short_password_fails_validation()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => NewCreateTenantHandler().HandleAsync(new CreateTenantCommand("Initech", "boss@initech.com", "The Boss", "short")));
    }

    [Fact]
    public async Task Get_tenants_maps_entities_to_dtos()
    {
        _tenants.Setup(repo => repo.ListAsync(default)).ReturnsAsync([new Tenant("Initech", TestSupport.Now)]);

        var result = await new GetTenantsHandler(_tenants.Object).HandleAsync();

        Assert.Equal("Initech", Assert.Single(result).Name);
    }

    // ---------- Users ----------

    [Fact]
    public async Task Create_user_takes_the_tenant_from_the_provider_and_hashes_the_password()
    {
        var result = await NewCreateUserHandler().HandleAsync(
            new CreateUserCommand("New.Agent@acme.demo", "New Agent", "Password1!", UserRole.Agent));

        Assert.Equal("new.agent@acme.demo", result.Email);
        Assert.Equal(UserRole.Agent, result.Role);
        _users.Verify(repo => repo.Add(It.Is<User>(user =>
            user.TenantId == TestSupport.TenantId && user.PasswordHash == "hashed-password")), Times.Once);
        _unitOfWork.Verify(unit => unit.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Create_user_with_an_email_already_in_use_is_a_conflict()
    {
        _users.Setup(repo => repo.EmailExistsIgnoringTenantAsync("taken@acme.demo", default)).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(
            () => NewCreateUserHandler().HandleAsync(new CreateUserCommand("taken@acme.demo", "Someone", "Password1!", UserRole.Agent)));
    }

    [Fact]
    public async Task Create_user_cannot_create_a_super_admin()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => NewCreateUserHandler().HandleAsync(new CreateUserCommand("root@acme.demo", "Root", "Password1!", UserRole.SuperAdmin)));

        _users.Verify(repo => repo.Add(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Get_users_passes_the_role_filter_and_maps_to_dtos()
    {
        _users.Setup(repo => repo.ListAsync(UserRole.Agent, default))
            .ReturnsAsync([TestSupport.NewUser(UserRole.Agent, "agent@acme.demo")]);

        var result = await new GetUsersHandler(_users.Object).HandleAsync(UserRole.Agent);

        Assert.Equal("agent@acme.demo", Assert.Single(result).Email);
    }

    // ---------- Categories ----------

    [Fact]
    public async Task Create_category_saves_it()
    {
        var handler = new CreateCategoryHandler(_categories.Object, _unitOfWork.Object);

        var result = await handler.HandleAsync(new CreateCategoryCommand("  Network  "));

        Assert.Equal("Network", result.Name);
        _categories.Verify(repo => repo.Add(It.Is<Category>(category => category.Name == "Network")), Times.Once);
        _unitOfWork.Verify(unit => unit.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Create_category_with_a_repeated_name_is_a_conflict()
    {
        _categories.Setup(repo => repo.NameExistsAsync("Network", default)).ReturnsAsync(true);
        var handler = new CreateCategoryHandler(_categories.Object, _unitOfWork.Object);

        await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(new CreateCategoryCommand("Network")));
    }

    [Fact]
    public async Task Get_categories_maps_entities_to_dtos()
    {
        _categories.Setup(repo => repo.ListAsync(default)).ReturnsAsync([new Category("Hardware")]);

        var result = await new GetCategoriesHandler(_categories.Object).HandleAsync();

        Assert.Equal("Hardware", Assert.Single(result).Name);
    }

    // ---------- Dashboard ----------

    [Fact]
    public async Task Dashboard_asks_the_repository_for_stats_at_the_current_time()
    {
        var tickets = new Mock<ITicketRepository>();
        var stats = new DashboardStatsDto(0, 0, null, new Dictionary<TicketStatus, int>(), new Dictionary<TicketPriority, int>());
        tickets.Setup(repo => repo.GetStatsAsync(TestSupport.Now, default)).ReturnsAsync(stats);

        var result = await new GetDashboardStatsHandler(tickets.Object, TestSupport.Clock()).HandleAsync();

        Assert.Same(stats, result);
    }

    private CreateTenantHandler NewCreateTenantHandler()
    {
        return new CreateTenantHandler(_tenants.Object, _users.Object, _passwordHasher.Object, _unitOfWork.Object, TestSupport.Clock());
    }

    private CreateUserHandler NewCreateUserHandler()
    {
        return new CreateUserHandler(_users.Object, _tenantProvider.Object, _passwordHasher.Object, _unitOfWork.Object, TestSupport.Clock());
    }
}
