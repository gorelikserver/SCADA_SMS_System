using Microsoft.EntityFrameworkCore;
using SCADASMSSystem.Web.Models;

namespace SCADASMSSystem.Web.Data
{
    public class SCADADbContext : DbContext
    {
        public SCADADbContext(DbContextOptions<SCADADbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<GroupMember> GroupMembers { get; set; }
        public DbSet<SmsAudit> SmsAudits { get; set; }
        public DbSet<DateDimension> DateDimensions { get; set; }
        public DbSet<AlarmAction> AlarmActions { get; set; }
        public DbSet<AlarmActionAudit> AlarmActionAudits { get; set; }
        public DbSet<DBListBlock> DBListBlocks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure relationships
            modelBuilder.Entity<GroupMember>(entity =>
            {
                entity.HasOne(gm => gm.Group)
                    .WithMany(g => g.GroupMembers)
                    .HasForeignKey(gm => gm.GroupId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(gm => gm.User)
                    .WithMany(u => u.GroupMembers)
                    .HasForeignKey(gm => gm.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Create composite index for performance
                entity.HasIndex(gm => new { gm.GroupId, gm.UserId })
                    .IsUnique();
            });

            modelBuilder.Entity<SmsAudit>(entity =>
            {
                entity.HasOne(sa => sa.User)
                    .WithMany(u => u.SmsAudits)
                    .HasForeignKey(sa => sa.UserId)
                    .OnDelete(DeleteBehavior.Restrict); // Don't delete audit records when user is deleted

                // Indexes for performance
                entity.HasIndex(sa => sa.AlarmId);
                entity.HasIndex(sa => sa.CreatedAt);
                entity.HasIndex(sa => sa.UserId);
            });

            modelBuilder.Entity<User>(entity =>
            {
                // Indexes for performance
                entity.HasIndex(u => u.PhoneNumber);
                entity.HasIndex(u => u.SmsEnabled);
                entity.HasIndex(u => u.SpecialDaysEnabled);
            });

            modelBuilder.Entity<DateDimension>(entity =>
            {
                // Unique index on full_date
                entity.HasIndex(dd => dd.FullDate)
                    .IsUnique();

                // Index for sabbatical holiday queries
                entity.HasIndex(dd => dd.IsSabbaticalHoliday);
            });

            // Set default values
            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(u => u.SmsEnabled)
                    .HasDefaultValue(true);

                entity.Property(u => u.SpecialDaysEnabled)
                    .HasDefaultValue(false);

                entity.Property(u => u.CreatedAt)
                    .HasDefaultValueSql("GETDATE()");
            });

            modelBuilder.Entity<Group>(entity =>
            {
                entity.Property(g => g.CreatedAt)
                    .HasDefaultValueSql("GETDATE()");
            });

            modelBuilder.Entity<GroupMember>(entity =>
            {
                entity.Property(gm => gm.CreatedAt)
                    .HasDefaultValueSql("GETDATE()");
            });

            modelBuilder.Entity<SmsAudit>(entity =>
            {
                entity.Property(sa => sa.CreatedAt)
                    .HasDefaultValueSql("GETDATE()");
            });

            modelBuilder.Entity<DateDimension>(entity =>
            {
                entity.Property(dd => dd.IsJewishHoliday)
                    .HasDefaultValue(false);

                entity.Property(dd => dd.IsSabbaticalHoliday)
                    .HasDefaultValue(false);
            });

            // Configure AlarmAction (WinCC OA AlarmConditions table)
            modelBuilder.Entity<AlarmAction>(entity =>
            {
                entity.HasKey(aa => aa.BlockId);
                
                // Explicitly ignore all computed/NotMapped properties
                entity.Ignore(aa => aa.AlarmId);
                entity.Ignore(aa => aa.BlockName);
                entity.Ignore(aa => aa.IsActive);
                entity.Ignore(aa => aa.AssignedGroupIds);
                entity.Ignore(aa => aa.LastModified);
                entity.Ignore(aa => aa.ModifiedBy);
                
                // Configure actual column mappings explicitly
                entity.Property(aa => aa.BlockId).HasColumnName("BLOCK_INDEX");
                entity.Property(aa => aa.AlarmIdInt).HasColumnName("ALARM_ID");
                entity.Property(aa => aa.AlarmDescription).HasColumnName("DESCRIPTION");
                entity.Property(aa => aa.AlarmConditionName).HasColumnName("ALARM_CONDITION_NAME");
                entity.Property(aa => aa.Action).HasColumnName("ACTIONS_ON_ACTIVE");
                entity.Property(aa => aa.Deleted).HasColumnName("DELETED");
                
                // Index on actual database columns
                entity.HasIndex(aa => aa.AlarmIdInt);
                entity.HasIndex(aa => aa.Deleted);
            });

            // Configure DBListBlock (WinCC OA DBLIST table)
            modelBuilder.Entity<DBListBlock>(entity =>
            {
                entity.HasKey(db => db.BlockIndex);
                entity.HasIndex(db => db.BlockName);
            });

            // Configure AlarmActionAudit (Our audit table, not in WinCC OA)
            modelBuilder.Entity<AlarmActionAudit>(entity =>
            {
                entity.HasKey(aaa => aaa.AuditId);
                entity.HasIndex(aaa => aaa.BlockId);
                entity.HasIndex(aaa => aaa.ActionType);
                entity.HasIndex(aaa => aaa.ModifiedAt);
                
                entity.Property(aaa => aaa.ModifiedAt)
                    .HasDefaultValueSql("GETDATE()");
            });
        }
    }
}