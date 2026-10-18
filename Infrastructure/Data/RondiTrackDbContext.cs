using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;

namespace RondiTrack.Infrastructure.Data;

public sealed class RondiTrackDbContext(DbContextOptions<RondiTrackDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Stokvel> Stokvels => Set<Stokvel>();
    public DbSet<StokvelMember> StokvelMembers => Set<StokvelMember>();
    public DbSet<ContributionCycle> ContributionCycles => Set<ContributionCycle>();
    public DbSet<Contribution> Contributions => Set<Contribution>();
    public DbSet<Payout> Payouts => Set<Payout>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();   // 6.1
    public DbSet<ArrearsRecord> ArrearsRecords => Set<ArrearsRecord>();             // 6.4
    public DbSet<IdempotencyKeyEntry> IdempotencyKeys => Set<IdempotencyKeyEntry>(); // 6.4

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
            entity.Property(u => u.PasswordHash).IsRequired();      // 6.1: "" = cannot sign in
            entity.Property(u => u.IsPlatformAdmin);                 // 6.1: platform-level flag
            entity.HasIndex(u => u.Email).IsUnique();               // also what stops two "Lerato@x.com" registrations
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
            entity.Property(c => c.EndsOn);       // 6.4: a plain date
            entity.Property(c => c.ClosedAt);

            // 6.4: one cycle per stokvel per end date, so a "next cycle" can never be created twice.
            // Filtered: older cycles without an end date are not affected.
            entity.HasIndex(c => new { c.StokvelId, c.EndsOn })
                .IsUnique()
                .HasFilter("\"EndsOn\" IS NOT NULL")
                .HasDatabaseName("UX_ContributionCycles_Stokvel_EndsOn");

            // 6.4: the job's query ("open cycles that have ended").
            entity.HasIndex(c => new { c.Status, c.EndsOn })
                .HasDatabaseName("IX_ContributionCycles_Status_EndsOn");

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

                 entity.HasIndex(c => new { c.StokvelId, c.UserId, c.ContributionCycleId })
                       .IsUnique()
                       .HasDatabaseName("UX_Contributions_Stokvel_User_Cycle");

                 entity.HasIndex(c => new { c.StokvelId, c.RecordedAt, c.Id })
                       .HasDatabaseName("IX_Contributions_StokvelId_RecordedAt_Id");
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
            entity.Property(p => p.Version).IsRowVersion();
        });

                // ---- ArrearsRecord (6.4) ----
        modelBuilder.Entity<ArrearsRecord>(entity =>
        {
            entity.ToTable("ArrearsRecords");
            entity.HasKey(a => a.Id);

            // THE guarantee: a member can be put into arrears for a given cycle only once, whatever the code does.
            entity.HasIndex(a => new { a.ContributionCycleId, a.UserId })
                .IsUnique()
                .HasDatabaseName("UX_ArrearsRecords_Cycle_User");

            // A financial record: a cycle or user that has arrears cannot be deleted from under it.
            entity.HasOne<ContributionCycle>().WithMany().HasForeignKey(a => a.ContributionCycleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---- IdempotencyKeyEntry (6.4) ----
        modelBuilder.Entity<IdempotencyKeyEntry>(entity =>
        {
            entity.ToTable("IdempotencyKeys");
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasMaxLength(200);
            entity.Property(e => e.RequestHash).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ResponseBodyJson).IsRequired();
            entity.HasIndex(e => e.CreatedAt).HasDatabaseName("IX_IdempotencyKeys_CreatedAt");   // housekeeping deletes by age
        });

        // ---- RefreshToken (6.1) ----
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();   // SHA-256 as hex = 64 chars
            entity.Property(t => t.CreatedAt);
            entity.Property(t => t.ExpiresAt);
            entity.Property(t => t.RevokedAt);
            entity.Property(t => t.ReplacedByTokenId);
            entity.Property(t => t.Version).IsRowVersion();                    // xmin, like Payout

            // Lookups: by hash (every refresh) and by family (reuse detection).
            entity.HasIndex(t => t.TokenHash).IsUnique();
            entity.HasIndex(t => t.FamilyId);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}