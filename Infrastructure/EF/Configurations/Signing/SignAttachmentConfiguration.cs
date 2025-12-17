// Infrastructure/EF/Configurations/Signing/SignAttachmentConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesMetrics.Domain.Signing;

namespace SalesMetrics.Infrastructure.EF.Configurations.Signing;

public sealed class SignAttachmentConfiguration : IEntityTypeConfiguration<SignAttachment>
{
    public void Configure(EntityTypeBuilder<SignAttachment> b)
    {
        b.ToTable("SignAttachment");
        b.HasKey(x => x.AttachmentId);
        b.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        b.Property(x => x.BlobPath).HasMaxLength(400).IsRequired();
        b.Property(x => x.MimeType).HasMaxLength(100);
        b.Property(x => x.UploadedDateUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasIndex(x => x.EnvelopeId);
    }
}
