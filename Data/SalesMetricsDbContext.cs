using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using SalesMetrics.Models.EFCore;

namespace SalesMetrics.Data;

public partial class SalesMetricsDbContext : DbContext
{
    public SalesMetricsDbContext()
    {
    }

    public SalesMetricsDbContext(DbContextOptions<SalesMetricsDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<LocationEntity> Locations { get; set; }

    public virtual DbSet<LoginHistoryEntity> LoginHistories { get; set; }

    public virtual DbSet<RoleEntity> Roles { get; set; }

    public virtual DbSet<TaskEntity> Tasks { get; set; }

    public virtual DbSet<UserEntity> Users { get; set; }

    public virtual DbSet<YardiPropertyEntity> YardiProperties { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json")
                .Build();

            var connectionString = config.GetConnectionString("SalesMetrics");
            optionsBuilder
                .UseSqlServer(connectionString)
                .EnableSensitiveDataLogging()
                .EnableDetailedErrors()
                .LogTo(Console.WriteLine, LogLevel.Information);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LocationEntity>(entity =>
        {
            entity.HasKey(e => e.LocationId).HasName("PK__Location__E7FEA4776C521054");

            entity.HasIndex(e => new { e.LocationName, e.LocationNumber }, "UC_Location").IsUnique();

            entity.HasIndex(e => new { e.LocationName, e.LocationAbrv }, "UC_LocationAbvr").IsUnique();

            entity.Property(e => e.LocationId).HasColumnName("LocationID");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.LocationAbrv)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.LocationName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.LocationNumber)
                .HasMaxLength(10)
                .IsUnicode(false);
        });

        modelBuilder.Entity<LoginHistoryEntity>(entity =>
        {
            entity.HasKey(e => e.LoginHistoryId).HasName("PK__LoginHis__2773EAFFA82CC141");

            entity.ToTable("LoginHistory");

            entity.Property(e => e.LoginHistoryId).HasColumnName("LoginHistoryID");
            entity.Property(e => e.Ipaddress)
                .HasMaxLength(20)
                .HasColumnName("IPAddress");
            entity.Property(e => e.LoginDate).HasDefaultValueSql("(CONVERT([date],getdate()))");
            entity.Property(e => e.LoginTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Office)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("office");
            entity.Property(e => e.Success)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.UserName)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<RoleEntity>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__Roles__8AFACE3A7BCC7B90");

            entity.Property(e => e.RoleId).HasColumnName("RoleID");
            entity.Property(e => e.RoleName).HasMaxLength(50);
        });

        modelBuilder.Entity<TaskEntity>(entity =>
        {
            entity.HasKey(e => e.TaskId).HasName("PK__Tasks__7C6949D10FBD462C");

            entity.Property(e => e.TaskId).HasColumnName("TaskID");
            entity.Property(e => e.CancelledDate).HasColumnType("datetime");
            entity.Property(e => e.CompletedDate).HasColumnType("datetime");
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.DueDate).HasColumnType("datetime");
            entity.Property(e => e.ModifiedDate).HasColumnType("datetime");
            entity.Property(e => e.Property).HasMaxLength(50);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Pending");
            entity.Property(e => e.Title).HasMaxLength(255);
            entity.Property(e => e.Type).HasMaxLength(50);
        });

        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.HasKey(e => e.Users_ID).HasName("PK__Users__EB68290DB9B7B8E8");

            entity.HasIndex(e => new { e.Username, e.Location }, "UQ_Username_Location").IsUnique();

            entity.HasIndex(e => new { e.UserId, e.Location }, "UQ_Users_UserID_Location").IsUnique();

            entity.Property(e => e.Users_ID).HasColumnName("Users_ID");
            entity.Property(e => e.AccountLocked).HasDefaultValue(false);
            entity.Property(e => e.AccountLockedDate).HasColumnType("datetime");
            entity.Property(e => e.CreatedDate).HasColumnType("datetime");
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.FirstName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.InActiveDate).HasColumnType("datetime");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.LastLoginDate).HasColumnType("datetime");
            entity.Property(e => e.LastName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.ModifiedDate).HasColumnType("datetime");
            entity.Property(e => e.Password).HasMaxLength(255);
            entity.Property(e => e.PasswordChangedDate).HasColumnType("datetime");
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.RoleId).HasColumnName("RoleID");
            entity.Property(e => e.SalesmanNumber).HasMaxLength(50);
            entity.Property(e => e.Salt)
                .HasMaxLength(32)
                .IsUnicode(false);
            entity.Property(e => e.SessionToken)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.Username).HasMaxLength(50);

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Users_Roles");
        });

        modelBuilder.Entity<YardiPropertyEntity>(entity =>
        {
            entity.HasKey(e => e.Property_ID);

            entity.Property(e => e.Property_ID).HasColumnName("Property_ID");

            // Optionally configure max lengths if needed
            entity.Property(e => e.PropertyName).HasMaxLength(255);
            entity.Property(e => e.PropertyAddress).HasMaxLength(255);
            entity.Property(e => e.PropertyCity).HasMaxLength(100);
            entity.Property(e => e.PropertyState).HasMaxLength(50);
            entity.Property(e => e.PropertyZipCode).HasMaxLength(20);
            entity.Property(e => e.PropertyPhone).HasMaxLength(50);
            entity.Property(e => e.OwnerFName).HasMaxLength(255);
            entity.Property(e => e.OwnerLNname).HasMaxLength(255);
            entity.Property(e => e.OwnerEmail).HasMaxLength(255);
            entity.Property(e => e.Manager).HasMaxLength(255);
            entity.Property(e => e.ManagerWebsite).HasMaxLength(255);
            entity.Property(e => e.PropertyType).HasMaxLength(100);
            entity.Property(e => e.ConstructionType).HasMaxLength(100);
            entity.Property(e => e.ImportedBy).HasMaxLength(100);
        });


        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
