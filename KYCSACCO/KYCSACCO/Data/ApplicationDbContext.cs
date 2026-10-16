using KYCSACCO.Models;
using KYCSACCO.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace KYCSACCO.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        public DbSet<Company> Companies { get; set; }
        public DbSet<UserGroup> UserGroups { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<RolePrivilege> RolePrivileges { get; set; }
        public virtual DbSet<Usergrp> GroupRights { get; set; }
        public DbSet<UserSession> UserSessions { get; set; }
        public DbSet<UserAccounts1> UserAccounts1 { get; set; }
        public DbSet<AuditTrail> AuditTrails { get; set; }
        public DbSet<County> Counties { get; set; }
        public DbSet<SubCounty> SubCounties { get; set; }
        public DbSet<Ward> Wards { get; set; }
        public DbSet<Block> Blocks { get; set; }
        public DbSet<BlockchainTransaction> BlockchainTransactions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);           

            //modelBuilder.Entity<Member>().Ignore(m => m.Id);

            // Explicitly configure Company entity
            modelBuilder.Entity<Company>(entity =>
            {
                entity.ToTable("Companies");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.CompanyCode)
                    .HasColumnName("CompanyCode")
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(e => e.CompanyName)
                    .HasColumnName("CompanyName")
                    .HasMaxLength(200);
            });

            modelBuilder.Entity<RolePrivilege>()
               .HasKey(rp => new { rp.GroupId, rp.RightId });

            // Block - BlockchainTransaction relationship
            modelBuilder.Entity<BlockchainTransaction>()
                .HasOne(t => t.Block)
                .WithMany(b => b.Transactions)
                .HasForeignKey(t => t.BlockHash)
                .HasPrincipalKey(b => b.BlockHash);

            // Configure default for all decimals
            foreach (var property in modelBuilder.Model.GetEntityTypes()
                .SelectMany(t => t.GetProperties())
                .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            {
                property.SetColumnType("decimal(18,2)");
            }           

            modelBuilder.Entity<BlockchainTransaction>().HasIndex(t => t.TransactionId).IsUnique();
            modelBuilder.Entity<BlockchainTransaction>().HasIndex(t => t.MemberNo);
            modelBuilder.Entity<BlockchainTransaction>().HasIndex(t => t.TransactionType);
            modelBuilder.Entity<BlockchainTransaction>().HasIndex(t => t.Timestamp);

            modelBuilder.Entity<Block>().HasIndex(b => b.BlockHash).IsUnique();
            modelBuilder.Entity<Block>().HasIndex(b => b.PreviousHash);           
        }
    }
}