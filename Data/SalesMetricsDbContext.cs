using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using SalesMetrics.Domain.Signing;
using SalesMetrics.Infrastructure.EF.Configurations.Signing;
using SalesMetrics.Models.EFCore;
using SalesMetrics.Data.Entities.QueryBuilder;

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

    public DbSet<SignTemplate> SignTemplates { get; set; } = default!;
    public DbSet<SignEnvelope> SignEnvelopes { get; set; } = default!;
    public DbSet<SignRecipient> SignRecipients { get; set; } = default!;
    public DbSet<SignEvent> SignEvents { get; set; } = default!;
    public DbSet<SignAttachment> SignAttachments { get; set; } = default!;
    public DbSet<SignField> SignFields { get; set; } = default!;

    public DbSet<NotificationEntity> Notifications { get; set; } = default!;
    public DbSet<NotificationRecipientEntity> NotificationRecipients { get; set; } = default!;
    public DbSet<BroadcastMessageEntity> BroadcastMessages { get; set; } = default!;
    public DbSet<NotificationSettingsEntity> NotificationSettings { get; set; } = default!;
    public DbSet<SecuritySettingsEntity> SecuritySettings { get; set; } = default!;
    public DbSet<FeatureEntity> Features { get; set; } = default!;
    public DbSet<UserFeaturePermissionEntity> UserFeaturePermissions { get; set; } = default!;

    // Query Builder
    public DbSet<ReportDefinitionEntity> ReportDefinitions { get; set; } = default!;
    public DbSet<ReportColumnDefinitionEntity> ReportColumnDefinitions { get; set; } = default!;
    public DbSet<QueryTableReferenceEntity> QueryTableReferences { get; set; } = default!;
    public DbSet<QueryFilterEntity> QueryFilters { get; set; } = default!;
    public DbSet<AllowedTableEntity> AllowedTables { get; set; } = default!;
    public DbSet<AllowedColumnEntity> AllowedColumns { get; set; } = default!;
    public DbSet<TableRelationshipEntity> TableRelationships { get; set; } = default!;
    public DbSet<ReportExecutionLogEntity> ReportExecutionLogs { get; set; } = default!;
    public DbSet<ReportTemplateEntity> ReportTemplates { get; set; } = default!;
    public DbSet<ReportSharingEntity> ReportSharings { get; set; } = default!;
    public DbSet<UserFavoriteReportEntity> UserFavoriteReports { get; set; } = default!;
    public DbSet<DataSourceConfigurationEntity> DataSourceConfigurations { get; set; } = default!;

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

        modelBuilder.ApplyConfiguration(new SignTemplateConfiguration());
        modelBuilder.ApplyConfiguration(new SignEnvelopeConfiguration());
        modelBuilder.ApplyConfiguration(new SignRecipientConfiguration());
        modelBuilder.ApplyConfiguration(new SignEventConfiguration());
        modelBuilder.ApplyConfiguration(new SignAttachmentConfiguration());
        modelBuilder.ApplyConfiguration(new SignFieldConfiguration());

        // Notification System Configuration
        modelBuilder.Entity<NotificationEntity>(entity =>
        {
            entity.HasKey(e => e.NotificationId);
            entity.Property(e => e.NotificationId).HasColumnName("NotificationID");
            entity.Property(e => e.NotificationType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Message).HasMaxLength(1000);
            entity.Property(e => e.ActionUrl).HasMaxLength(500);
            entity.Property(e => e.RelatedTaskId).HasColumnName("RelatedTaskID");
            entity.Property(e => e.RelatedEnvelopeId).HasColumnName("RelatedEnvelopeID");
            entity.Property(e => e.BroadcastMessageId).HasColumnName("BroadcastMessageID");
            entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUserID");
            entity.Property(e => e.CreatedDate).HasColumnType("datetime").HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsSystemGenerated).HasDefaultValue(true);
        });

        modelBuilder.Entity<NotificationRecipientEntity>(entity =>
        {
            entity.HasKey(e => e.NotificationRecipientId);
            entity.Property(e => e.NotificationRecipientId).HasColumnName("NotificationRecipientID");
            entity.Property(e => e.NotificationId).HasColumnName("NotificationID");
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.IsRead).HasDefaultValue(false);
            entity.Property(e => e.ReadDate).HasColumnType("datetime");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.DeletedDate).HasColumnType("datetime");

            entity.HasOne(d => d.Notification)
                .WithMany(p => p.Recipients)
                .HasForeignKey(d => d.NotificationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_NotificationRecipients_Notifications");
        });

        modelBuilder.Entity<BroadcastMessageEntity>(entity =>
        {
            entity.HasKey(e => e.BroadcastMessageId);
            entity.Property(e => e.BroadcastMessageId).HasColumnName("BroadcastMessageID");
            entity.Property(e => e.Title).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Message).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.TargetType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.TargetLocationId).HasColumnName("TargetLocationID");
            entity.Property(e => e.TargetRoleId).HasColumnName("TargetRoleID");
            entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUserID");
            entity.Property(e => e.CreatedDate).HasColumnType("datetime").HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ExpiresDate).HasColumnType("datetime");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Priority).HasMaxLength(20).HasDefaultValue("Normal");
            entity.Property(e => e.ScheduledDate).HasColumnType("datetime");
            entity.Property(e => e.IsSent).HasDefaultValue(false);
            entity.Property(e => e.SentDate).HasColumnType("datetime");
            entity.Property(e => e.SentCount).HasDefaultValue(0);
            entity.Property(e => e.ReadCount).HasDefaultValue(0);
            entity.Property(e => e.IsTemplate).HasDefaultValue(false);
            entity.Property(e => e.TemplateCategory).HasMaxLength(50);
        });

        modelBuilder.Entity<NotificationSettingsEntity>(entity =>
        {
            entity.HasKey(e => e.NotificationSettingsId);
            entity.Property(e => e.NotificationSettingsId).HasColumnName("NotificationSettingsID");
            entity.Property(e => e.CategoryName).HasMaxLength(50).IsRequired();
            entity.Property(e => e.IsEnabled).HasDefaultValue(true);
            entity.Property(e => e.NotifyAssignee).HasDefaultValue(true);
            entity.Property(e => e.NotifyManager).HasDefaultValue(false);
            entity.Property(e => e.NotifyTaskOwner).HasDefaultValue(false);
            entity.Property(e => e.EnableInAppNotification).HasDefaultValue(true);
            entity.Property(e => e.EnableEmailNotification).HasDefaultValue(false);
            entity.Property(e => e.ReminderHoursBefore).HasDefaultValue(24);
            entity.Property(e => e.SpecificStatuses).HasMaxLength(255);
            entity.Property(e => e.LastModifiedDate).HasColumnType("datetime").HasDefaultValueSql("(getdate())");
            entity.Property(e => e.LastModifiedByUserId).HasColumnName("LastModifiedByUserID");

            // Create unique index on CategoryName
            entity.HasIndex(e => e.CategoryName).IsUnique();
        });

        modelBuilder.Entity<SecuritySettingsEntity>(entity =>
        {
            entity.HasKey(e => e.SecuritySettingsId);
            entity.Property(e => e.SecuritySettingsId).HasColumnName("SecuritySettingsID");
            entity.Property(e => e.SettingKey).HasMaxLength(100).IsRequired();
            entity.Property(e => e.SettingValue).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Category).HasMaxLength(50).IsRequired();
            entity.Property(e => e.LastModifiedDate).HasColumnType("datetime").HasDefaultValueSql("(getdate())");
            entity.Property(e => e.LastModifiedByUserId).HasColumnName("LastModifiedByUserID");

            // Create unique index on SettingKey
            entity.HasIndex(e => e.SettingKey).IsUnique();
        });

        // Feature Permissions Configuration
        modelBuilder.Entity<FeatureEntity>(entity =>
        {
            entity.HasKey(e => e.FeatureId);
            entity.Property(e => e.FeatureId).HasColumnName("FeatureID");
            entity.Property(e => e.FeatureCode).HasMaxLength(50).IsRequired();
            entity.Property(e => e.FeatureName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Category).HasMaxLength(50);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.DisplayOrder).HasDefaultValue(0);

            // Create unique index on FeatureCode
            entity.HasIndex(e => e.FeatureCode).IsUnique();
        });

        modelBuilder.Entity<UserFeaturePermissionEntity>(entity =>
        {
            entity.HasKey(e => e.PermissionId);
            entity.Property(e => e.PermissionId).HasColumnName("PermissionID");
            entity.Property(e => e.Users_ID).HasColumnName("Users_ID");
            entity.Property(e => e.FeatureId).HasColumnName("FeatureID");
            entity.Property(e => e.HasAccess).HasDefaultValue(true);
            entity.Property(e => e.GrantedDate).HasColumnType("datetime").HasDefaultValueSql("(getdate())");
            entity.Property(e => e.GrantedByUsers_ID).HasColumnName("GrantedByUsers_ID");
            entity.Property(e => e.ExpiresDate).HasColumnType("datetime");

            // Create unique index on Users_ID + FeatureId combination
            entity.HasIndex(e => new { e.Users_ID, e.FeatureId }).IsUnique();

            // Configure relationships
            entity.HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.Users_ID)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_UserFeaturePermissions_Users");

            entity.HasOne(d => d.Feature)
                .WithMany(p => p.UserPermissions)
                .HasForeignKey(d => d.FeatureId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_UserFeaturePermissions_Features");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    public DbSet<UserLocationAssignment> UserLocationAssignments { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
