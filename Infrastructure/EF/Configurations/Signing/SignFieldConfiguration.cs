// Infrastructure/EF/Configurations/Signing/SignFieldConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesMetrics.Domain.Signing;

namespace SalesMetrics.Infrastructure.EF.Configurations.Signing;

public sealed class SignFieldConfiguration : IEntityTypeConfiguration<SignField>
{
    public void Configure(EntityTypeBuilder<SignField> b)
    {
        b.ToTable("SignField");
        b.HasKey(x => x.FieldId);
        b.Property(x => x.FieldKey).HasMaxLength(100).IsRequired();
        b.Property(x => x.FieldType).HasMaxLength(20).IsRequired();

        b.HasIndex(x => x.EnvelopeId);
        b.HasIndex(x => new { x.EnvelopeId, x.FieldKey });
    }
}
