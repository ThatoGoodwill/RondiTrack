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
 entity.Property(u => u.FirstName).HasMaxLength(User.MaxNameLength).IsRequired();
 entity.Property(u => u.LastName).HasMaxLength(User.MaxNameLength).IsRequired();
 entity.Property(u => u.Email).IsRequired();
 entity.HasIndex(u => u.Email).IsUnique(); // the DATABASE now also enforces uniqueness
 });
 // ---- Stokvel + its membership collection ----
 modelBuilder.Entity<Stokvel>(entity =>
 {
 entity.HasKey(s => s.Id);
 entity.Property(s => s.Name).HasMaxLength(Stokvel.MaxNameLength).IsRequired();
 entity.Property(s => s.ContributionAmount).HasPrecision(18, 2); // exact decimal storage, no rounding  // See Step 6: this is almost certainly where YOUR mapping problem lives.
 entity.HasMany(typeof(Membership), "_members")
 .WithOne()
 .HasForeignKey("StokvelId");
 entity.Navigation("_members").UsePropertyAccessMode(PropertyAccessMode.Field);
 });
 modelBuilder.Entity<Membership>(entity =>
 {
 entity.HasKey("StokvelId", "UserId"); // a composite key: one row per (stokvel, user) pair
 entity.Property<Guid>("StokvelId");
 });
 // ---- ContributionCycle ----
 modelBuilder.Entity<ContributionCycle>(entity =>

 {
 entity.HasKey(c => c.Id);
 entity.Property(c => c.Label).HasMaxLength(ContributionCycle.MaxLabelLength).IsRequired();
 entity.Property(c => c.TargetAmount).HasPrecision(18, 2);
 });
 // ---- Contribution ----
 modelBuilder.Entity<Contribution>(entity =>
 {
 entity.HasKey(c => c.Id);
 entity.Property(c => c.Amount).HasPrecision(18, 2);
 });
 // ---- Payout (new -- see Step 5) ----
 modelBuilder.Entity<Payout>(entity =>
 {
 entity.HasKey(p => p.Id);
 entity.Property(p => p.Amount).HasPrecision(18, 2);
 });
 }
}