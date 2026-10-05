using Microsoft.EntityFrameworkCore;
using RondiTrack.Api.Domain;

namespace RondiTrack.Api.Data;

public sealed class RondiTrackDbContext(DbContextOptions<RondiTrackDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Stokvel> Stokvels => Set<Stokvel>();
    public DbSet<StokvelMember> StokvelMembers => Set<StokvelMember>();
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
            entity.Property(u => u.CreatedAt);
            entity.HasIndex(u => u.Email).IsUnique();
        });

        // ---- Stokvel + its real members collection ----
        modelBuilder.Entity<Stokvel>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Name).HasMaxLength(Stokvel.MaxNameLength).IsRequired();
            entity.Property(s => s.ContributionAmount).HasPrecision(18, 2);
            entity.Property(s => s.CreatedAt);

            entity.HasMany(s => s.Members)
                .WithOne()
                .HasForeignKey(sm => sm.StokvelId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Navigation(s => s.Members)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.Ignore(s => s.MemberCount); // computed from the members, no column
        });

        // ---- StokvelMember: composite key (StokvelId, UserId) ----
        modelBuilder.Entity<StokvelMember>(entity =>
        {
            entity.HasKey(sm => new { sm.StokvelId, sm.UserId });

            entity.Property(sm => sm.JoinedAtUtc);

            entity.Property(sm => sm.Role)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(sm => sm.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        // ---- ContributionCycle ----
        modelBuilder.Entity<ContributionCycle>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.StokvelId);
            entity.Property(c => c.CreatedAt);
            entity.Property(c => c.Label).HasMaxLength(ContributionCycle.MaxLabelLength).IsRequired();
            entity.Property(c => c.TargetAmount).HasPrecision(18, 2);

            entity.HasMany(c => c.Contributions)
                .WithOne()
                .HasForeignKey(c => c.ContributionCycleId);

            entity.Navigation(c => c.Contributions)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });
                       // ---- Contribution ----
        modelBuilder.Entity<Contribution>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.StokvelId);
            entity.Property(c => c.UserId);
            entity.Property(c => c.ContributionCycleId);
            entity.Property(c => c.RecordedAt);
            entity.Property(c => c.Amount).HasPrecision(18, 2);

           entity.HasOne(c => c.User)
                 .WithMany()
                 .HasForeignKey(c => c.UserId)
                 .OnDelete(DeleteBehavior.Restrict);
        });

                // ---- Payout ----
        modelBuilder.Entity<Payout>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.StokvelId);
            entity.Property(p => p.ContributionCycleId);
            entity.Property(p => p.RecipientUserId);
            entity.Property(p => p.ProcessedAt);
            entity.Property(p => p.Amount).HasPrecision(18, 2);
        });
    }
}