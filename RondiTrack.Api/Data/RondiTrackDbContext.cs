using Microsoft.EntityFrameworkCore;
using RondiTrack.Api.Domain;
namespace RondiTrack.Api.Data;
public sealed class RondiTrackDbContext(DbContextOptions<RondiTrackDbContext> options) : DbContext(options)
{
     public DbSet<User> Users => Set<User>();
     public DbSet<Stokvel> Stokvels => Set<Stokvel>();
     public DbSet<Membership> StokvelMembers => Set<Membership>();
     public DbSet<ContributionCycle> ContributionCycles => Set<ContributionCycle>();
     public DbSet<Contribution> Contributions => Set<Contribution>();
     public DbSet<Payout> Payouts => Set<Payout>();

     protected override void OnModelCreating(ModelBuilder modelBuilder)
 {
 // ---- User ----
modelBuilder.Entity<User>(entity =>
{
    entity.HasKey(u => u.Id);

    entity.Property(u => u.FirstName)
        .HasMaxLength(User.MaxNameLength)
        .IsRequired();

    entity.Property(u => u.LastName)
        .HasMaxLength(User.MaxNameLength)
        .IsRequired();

    entity.Property(u => u.Email)
        .IsRequired();

    entity.Property(u => u.CreatedAt);

    entity.HasIndex(u => u.Email)
        .IsUnique(); // The DATABASE now also enforces uniqueness
});
// ---- Stokvel + its membership collection ----
modelBuilder.Entity<Stokvel>(entity =>
{
    entity.HasKey(s => s.Id);

    entity.Property(s => s.Name)
        .HasMaxLength(Stokvel.MaxNameLength)
        .IsRequired();

    entity.Property(s => s.ContributionAmount)
        .HasPrecision(18, 2);

    entity.Property(s => s.CreatedAt);

    entity.HasMany(s => s.Members)
        .WithOne()
        .HasForeignKey("StokvelId");

    entity.Navigation(s => s.Members)
        .UsePropertyAccessMode(PropertyAccessMode.Field);
});
 modelBuilder.Entity<Membership>(entity =>
 {
 entity.HasKey("StokvelId", "UserId"); // a composite key: one row per (stokvel, user) pair
 entity.Property<Guid>("StokvelId");
 });
 
modelBuilder.Entity<ContributionCycle>(entity =>
{
    entity.HasKey(c => c.Id);

    entity.Property(c => c.StokvelId);

    entity.Property(c => c.Label)
        .IsRequired();

    entity.Property(c => c.TargetAmount)
        .HasPrecision(18, 2);

    entity.Property(c => c.CreatedAt);
});
 
 // ---- Contribution ----
modelBuilder.Entity<Contribution>(entity =>
{
    entity.HasKey(c => c.Id);

    entity.Property(c => c.StokvelId);

    entity.Property(c => c.UserId);

    entity.Property(c => c.ContributionCycleId);

    entity.Property(c => c.Amount)
        .HasPrecision(18, 2);

    entity.Property(c => c.RecordedAt);
});
 
 // ---- Payout ----
 modelBuilder.Entity<Payout>(entity =>
 {
     entity.HasKey(p => p.Id);

     entity.Property(p => p.StokvelId);

     entity.Property(p => p.ContributionCycleId);

     entity.Property(p => p.RecipientUserId);

     entity.Property(p => p.Amount)
         .HasPrecision(18, 2);

     entity.Property(p => p.ProcessedAt);
 });
 }
}