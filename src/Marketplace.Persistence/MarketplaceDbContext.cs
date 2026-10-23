using Marketplace.Core;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Persistence;

/// <summary>PostgreSQL persistence boundary for the marketplace.</summary>
public sealed class MarketplaceDbContext(DbContextOptions<MarketplaceDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Contractor> Contractors => Set<Contractor>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobOffer> Offers => Set<JobOffer>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasPostgresExtension("pg_trgm");
        builder.Entity<Customer>(entity =>
        {
            entity.ToTable("customers"); entity.HasKey(x => x.Id); entity.Property(x => x.Id).UseIdentityAlwaysColumn();
            entity.Property(x => x.FirstName).HasColumnType("text").IsRequired(); entity.Property(x => x.LastName).HasColumnType("text").IsRequired();
            entity.HasIndex(x => x.UserId).IsUnique(); entity.HasIndex(x => x.LastName);
            entity.ToTable(t => t.HasCheckConstraint("ck_customer_first_name", "length(first_name) between 1 and 100"));
            entity.ToTable(t => t.HasCheckConstraint("ck_customer_last_name", "length(last_name) between 1 and 100"));
        });
        builder.Entity<Contractor>(entity =>
        {
            entity.ToTable("contractors"); entity.HasKey(x => x.Id); entity.Property(x => x.Id).UseIdentityAlwaysColumn();
            entity.Property(x => x.Name).HasColumnType("text").IsRequired(); entity.Property(x => x.Rating).HasPrecision(3, 2);
            entity.HasIndex(x => x.UserId).IsUnique(); entity.HasIndex(x => x.Name);
            entity.ToTable(t => t.HasCheckConstraint("ck_contractor_name", "length(name) between 1 and 200"));
            entity.ToTable(t => t.HasCheckConstraint("ck_contractor_rating", "rating >= 0 and rating <= 5"));
        });
        builder.Entity<Job>(entity =>
        {
            entity.ToTable("jobs"); entity.HasKey(x => x.Id); entity.Property(x => x.Id).UseIdentityAlwaysColumn(); entity.Property(x => x.Budget).HasPrecision(18, 2);
            entity.Property(x => x.Description).HasColumnType("text").IsRequired(); entity.Property(x => x.Status).HasConversion<string>().HasColumnType("text");
            entity.HasOne(x => x.Customer).WithMany(x => x.Jobs).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.AcceptedOffer).WithMany().HasForeignKey(x => x.AcceptedOfferId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.CustomerId, x.IsDeleted }); entity.HasIndex(x => new { x.Status, x.DueDate, x.Id });
            entity.ToTable(t => t.HasCheckConstraint("ck_job_budget", "budget > 0"));
            entity.ToTable(t => t.HasCheckConstraint("ck_job_dates", "due_date >= start_date"));
            entity.ToTable(t => t.HasCheckConstraint("ck_job_description", "length(description) between 1 and 5000"));
        });
        builder.Entity<JobOffer>(entity =>
        {
            entity.ToTable("job_offers"); entity.HasKey(x => x.Id); entity.Property(x => x.Id).UseIdentityAlwaysColumn(); entity.Property(x => x.Price).HasPrecision(18, 2);
            entity.HasOne(x => x.Job).WithMany(x => x.Offers).HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Contractor).WithMany(x => x.Offers).HasForeignKey(x => x.ContractorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.JobId, x.ContractorId }).IsUnique().HasFilter("NOT is_deleted"); entity.HasIndex(x => new { x.ContractorId, x.IsDeleted }); entity.HasIndex(x => new { x.JobId, x.IsDeleted });
            entity.ToTable(t => t.HasCheckConstraint("ck_offer_price", "price > 0"));
        });
    }
}
