// Infrastructure/EF/Configurations/Signing/SignTemplateConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesMetrics.Domain.Signing;

namespace SalesMetrics.Infrastructure.EF.Configurations.Signing;

public sealed class SignTemplateConfiguration : IEntityTypeConfiguration<SignTemplate>
{
    public void Configure(EntityTypeBuilder<SignTemplate> b)
    {
        b.ToTable("SignTemplate");
        b.HasKey(x => x.TemplateKey);
        b.Property(x => x.TemplateKey).HasMaxLength(100);
        b.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        b.Property(x => x.RazorViewPath).HasMaxLength(260).IsRequired(false);
        b.Property(x => x.HtmlBodyContent).HasColumnType("nvarchar(max)").IsRequired(false);
        b.Property(x => x.DefaultSubject).HasMaxLength(200);
        b.Property(x => x.IsActive).HasDefaultValue(true);
        b.Property(x => x.RequiresTenantSection).HasDefaultValue(true);
        b.Property(x => x.CreatedDateUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        // Indexes if needed
        b.HasIndex(x => x.IsActive);
    }
}
