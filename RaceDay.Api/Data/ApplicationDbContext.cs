using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

/// <summary>
/// EF Core Code-First model. Table names, column sizes, keys, unique indexes,
/// check constraints and delete behaviour mirror docs/RaceDay_Database.sql from Part 1.
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<Result> Results => Set<Result>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ---------- Roles ----------
        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("Roles");
            e.HasKey(r => r.RoleId);
            e.Property(r => r.RoleName).HasMaxLength(50).IsRequired();
            e.Property(r => r.Description).HasMaxLength(200);
            e.HasIndex(r => r.RoleName).IsUnique();

            // The two roles required by the system
            e.HasData(
                new Role { RoleId = 1, RoleName = "Organiser", Description = "Can create and manage events, categories and capture results" },
                new Role { RoleId = 2, RoleName = "Participant", Description = "Can browse events, enrol and view personal results" });
        });

        // ---------- Users ----------
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(u => u.UserId);
            e.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
            e.Property(u => u.LastName).HasMaxLength(100).IsRequired();
            e.Property(u => u.Email).HasMaxLength(256).IsRequired();
            e.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
            e.Property(u => u.PhoneNumber).HasMaxLength(20);
            e.Property(u => u.ProfilePictureUrl).HasMaxLength(500);
            e.Property(u => u.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasIndex(u => u.Email).IsUnique();

            e.HasOne(u => u.Role).WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Events ----------
        modelBuilder.Entity<Event>(e =>
        {
            e.ToTable("Events", t =>
            {
                t.HasCheckConstraint("CK_Events_Status", "[Status] IN ('Draft', 'Published', 'Cancelled', 'Completed')");
                t.HasCheckConstraint("CK_Events_EventType", "[EventType] IN ('Run', 'Walk', 'Cycle')");
            });
            e.HasKey(x => x.EventId);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Location).HasMaxLength(300).IsRequired();
            e.Property(x => x.City).HasMaxLength(100).IsRequired();
            e.Property(x => x.Province).HasMaxLength(50).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("Draft");
            e.Property(x => x.EventType).HasMaxLength(20).HasDefaultValue("Run");
            e.Property(x => x.DistanceKm).HasPrecision(6, 2);
            e.Property(x => x.ImageUrl).HasMaxLength(500);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasIndex(x => x.OrganiserId).HasDatabaseName("IX_Events_OrganiserId");
            e.HasIndex(x => x.EventDate).HasDatabaseName("IX_Events_EventDate");

            e.HasOne(x => x.Organiser).WithMany()
                .HasForeignKey(x => x.OrganiserId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Categories ----------
        modelBuilder.Entity<Category>(e =>
        {
            e.ToTable("Categories", t =>
            {
                t.HasCheckConstraint("CK_Categories_Gender", "[GenderRestriction] IN ('None', 'Male', 'Female')");
                t.HasCheckConstraint("CK_Categories_Distance", "[DistanceKm] > 0");
            });
            e.HasKey(c => c.CategoryId);
            e.Property(c => c.Name).HasMaxLength(100).IsRequired();
            e.Property(c => c.DistanceKm).HasPrecision(6, 2);
            e.Property(c => c.EntryFee).HasPrecision(10, 2);
            e.Property(c => c.GenderRestriction).HasMaxLength(10).HasDefaultValue("None");
            e.Property(c => c.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasIndex(c => c.EventId).HasDatabaseName("IX_Categories_EventId");

            // Cascade delete: deleting an event deletes its categories (as in Part 1 SQL)
            e.HasOne(c => c.Event).WithMany(ev => ev.Categories)
                .HasForeignKey(c => c.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- Enrolments ----------
        modelBuilder.Entity<Enrolment>(e =>
        {
            e.ToTable("Enrolments", t =>
            {
                t.HasCheckConstraint("CK_Enrolments_Status", "[Status] IN ('Pending', 'Confirmed', 'Cancelled', 'Completed')");
                t.HasCheckConstraint("CK_Enrolments_Payment", "[PaymentStatus] IN ('Pending', 'Paid', 'Refunded')");
            });
            e.HasKey(x => x.EnrolmentId);
            e.Property(x => x.EnrolmentDate).HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("Confirmed");
            e.Property(x => x.BibNumber).HasMaxLength(20);
            e.Property(x => x.PaymentStatus).HasMaxLength(20).HasDefaultValue("Pending");
            e.Property(x => x.Notes).HasMaxLength(500);

            // A participant can only enter the same category once
            e.HasIndex(x => new { x.ParticipantId, x.CategoryId })
                .IsUnique().HasDatabaseName("UQ_Enrolments_Participant_Category");
            e.HasIndex(x => x.ParticipantId).HasDatabaseName("IX_Enrolments_ParticipantId");
            e.HasIndex(x => x.CategoryId).HasDatabaseName("IX_Enrolments_CategoryId");

            e.HasOne(x => x.Participant).WithMany()
                .HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Category).WithMany(c => c.Enrolments)
                .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Results ----------
        modelBuilder.Entity<Result>(e =>
        {
            e.ToTable("Results", t => t.HasCheckConstraint("CK_Results_Status",
                "[Status] IN ('Finished', 'DNF', 'DNS', 'DQ')"));
            e.HasKey(r => r.ResultId);
            e.Property(r => r.Status).HasMaxLength(20).HasDefaultValue("Finished");
            e.Property(r => r.CapturedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            // One result per enrolment (unique FK)
            e.HasOne(r => r.Enrolment).WithOne(en => en.Result)
                .HasForeignKey<Result>(r => r.EnrolmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.CapturedBy).WithMany()
                .HasForeignKey(r => r.CapturedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
