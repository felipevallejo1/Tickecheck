using Microsoft.EntityFrameworkCore;
using Tickecheck.Application.Abstractions;
using Tickecheck.Domain.Categories;
using Tickecheck.Domain.Tenants;
using Tickecheck.Domain.Tickets;
using Tickecheck.Domain.Users;

namespace Tickecheck.Infrastructure.Persistence.Seed;

/// <summary>
/// Fills an empty database with demo data: a SuperAdmin and two companies, each with
/// users of every role, categories, tickets in different states and comments.
/// </summary>
public class DbSeeder(
    DbContextOptions<AppDbContext> options,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider)
{
    // Demo credentials, documented in the README. Never use this seeder with real data.
    public const string DemoPassword = "Demo123!";
    public const string SuperAdminEmail = "superadmin@tickecheck.demo";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        // Tenants and users are platform-level data, so they are written with no tenant in context.
        await using var platformDb = new AppDbContext(options, new FixedTenantProvider(null));

        if (await platformDb.Tenants.AnyAsync(cancellationToken))
        {
            return;
        }

        var passwordHash = passwordHasher.Hash(DemoPassword);
        platformDb.Users.Add(new User(null, SuperAdminEmail, passwordHash, "Platform Admin", UserRole.SuperAdmin, utcNow));

        var companies = new[]
        {
            new SeedCompany("Acme Corp", "acme.demo"),
            new SeedCompany("Globex Inc", "globex.demo")
        };

        foreach (var company in companies)
        {
            company.Tenant = new Tenant(company.Name, utcNow.AddDays(-30));
            platformDb.Tenants.Add(company.Tenant);

            company.Admin = NewUser(company, "admin", "Admin", UserRole.TenantAdmin);
            company.Agents = [NewUser(company, "agent1", "Agent One", UserRole.Agent), NewUser(company, "agent2", "Agent Two", UserRole.Agent)];
            company.Requesters = [NewUser(company, "user1", "User One", UserRole.Requester), NewUser(company, "user2", "User Two", UserRole.Requester)];

            platformDb.Users.AddRange([company.Admin, .. company.Agents, .. company.Requesters]);
        }

        await platformDb.SaveChangesAsync(cancellationToken);

        foreach (var company in companies)
        {
            // Tickets are tenant data: a context scoped to the company stamps the TenantId,
            // exactly as it happens in a real request.
            await using var tenantDb = new AppDbContext(options, new FixedTenantProvider(company.Tenant.Id));
            SeedTickets(tenantDb, company, utcNow);
            await tenantDb.SaveChangesAsync(cancellationToken);
        }

        User NewUser(SeedCompany company, string alias, string name, UserRole role)
        {
            return new User(
                company.Tenant.Id,
                $"{alias}@{company.EmailDomain}",
                passwordHash,
                $"{name} ({company.Name})",
                role,
                utcNow.AddDays(-30));
        }
    }

    private static void SeedTickets(AppDbContext db, SeedCompany company, DateTime utcNow)
    {
        var hardware = new Category("Hardware");
        var software = new Category("Software");
        var access = new Category("Access and accounts");
        db.Categories.AddRange(hardware, software, access);

        var agent1 = company.Agents[0].Id;
        var agent2 = company.Agents[1].Id;
        var user1 = company.Requesters[0].Id;
        var user2 = company.Requesters[1].Id;

        // Open and unassigned, still within its SLA.
        var monitor = NewTicket("Second monitor is flickering", TicketPriority.Low, hardware, user1, hoursAgo: 5);

        // Open and already past its deadline (High = 8 hours).
        var vpn = NewTicket("Cannot connect to the VPN", TicketPriority.High, access, user2, hoursAgo: 20);

        // Critical, being worked on, overdue.
        var server = NewTicket("File server is down", TicketPriority.Critical, hardware, user1, hoursAgo: 9);
        Assign(server, agent1, hoursAfterCreation: 0.5);
        Move(server, TicketStatus.InProgress, agent1, hoursAfterCreation: 1);

        // Waiting for an answer from the customer.
        var license = NewTicket("Request for a design software license", TicketPriority.Medium, software, user2, hoursAgo: 30);
        Assign(license, agent2, hoursAfterCreation: 1);
        Move(license, TicketStatus.InProgress, agent2, hoursAfterCreation: 2);
        Move(license, TicketStatus.WaitingOnCustomer, agent2, hoursAfterCreation: 3);

        // Resolved inside the SLA.
        var password = NewTicket("Password reset for the payroll system", TicketPriority.High, access, user1, hoursAgo: 50);
        Assign(password, agent1, hoursAfterCreation: 0.5);
        Move(password, TicketStatus.InProgress, agent1, hoursAfterCreation: 1);
        Move(password, TicketStatus.Resolved, agent1, hoursAfterCreation: 3);

        // Resolved and then closed.
        var printer = NewTicket("Printer on the 2nd floor has no toner", TicketPriority.Low, hardware, user2, hoursAgo: 120);
        Assign(printer, agent2, hoursAfterCreation: 2);
        Move(printer, TicketStatus.InProgress, agent2, hoursAfterCreation: 4);
        Move(printer, TicketStatus.Resolved, agent2, hoursAfterCreation: 20);
        Move(printer, TicketStatus.Closed, company.Admin.Id, hoursAfterCreation: 44);

        // Resolved, but the requester reopened it.
        var email = NewTicket("Emails to external addresses bounce", TicketPriority.Medium, software, user1, hoursAgo: 70);
        Assign(email, agent1, hoursAfterCreation: 1);
        Move(email, TicketStatus.InProgress, agent1, hoursAfterCreation: 2);
        Move(email, TicketStatus.Resolved, agent1, hoursAfterCreation: 10);
        Move(email, TicketStatus.Reopened, user1, hoursAfterCreation: 30);

        // Priority raised after creation, which leaves a history entry and a new deadline.
        var laptop = NewTicket("Laptop is very slow after the update", TicketPriority.Low, software, user2, hoursAgo: 3);
        laptop.ChangePriority(TicketPriority.Medium, company.Admin.Id, laptop.CreatedAt.AddHours(1));
        Assign(laptop, agent2, hoursAfterCreation: 1.5);

        db.Tickets.AddRange(monitor, vpn, server, license, password, printer, email, laptop);

        db.Comments.AddRange(
            new Comment(server.Id, agent1, "We are restoring the server from last night's backup.", false, server.CreatedAt.AddHours(1.5)),
            new Comment(server.Id, agent1, "RAID controller failed. A replacement part was ordered from the vendor.", true, server.CreatedAt.AddHours(2)),
            new Comment(license.Id, agent2, "Which version do you need, Standard or Pro?", false, license.CreatedAt.AddHours(3)),
            new Comment(email.Id, user1, "It worked for a day, but the emails are bouncing again.", false, email.CreatedAt.AddHours(30)),
            new Comment(email.Id, agent1, "Probably the SPF record. Checking with the DNS provider.", true, email.CreatedAt.AddHours(31)));

        Ticket NewTicket(string title, TicketPriority priority, Category category, Guid requesterId, double hoursAgo)
        {
            return new Ticket(title, $"{title}. Reported from the {company.Name} office.", priority, category.Id, requesterId, utcNow.AddHours(-hoursAgo));
        }

        void Assign(Ticket ticket, Guid agentId, double hoursAfterCreation)
        {
            ticket.AssignTo(agentId, company.Admin.Id, ticket.CreatedAt.AddHours(hoursAfterCreation));
        }

        void Move(Ticket ticket, TicketStatus status, Guid changedBy, double hoursAfterCreation)
        {
            ticket.ChangeStatus(status, changedBy, ticket.CreatedAt.AddHours(hoursAfterCreation));
        }
    }

    private class SeedCompany(string name, string emailDomain)
    {
        public string Name { get; } = name;
        public string EmailDomain { get; } = emailDomain;
        public Tenant Tenant { get; set; } = null!;
        public User Admin { get; set; } = null!;
        public User[] Agents { get; set; } = [];
        public User[] Requesters { get; set; } = [];
    }
}
