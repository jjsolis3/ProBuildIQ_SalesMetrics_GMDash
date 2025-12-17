// Infrastructure/EF/Configurations/Signing/SignEnvelopeConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesMetrics.Domain.Signing;

namespace SalesMetrics.Infrastructure.EF.Configurations.Signing;

public sealed class SignEnvelopeConfiguration : IEntityTypeConfiguration<SignEnvelope>
{
    public void Configure(EntityTypeBuilder<SignEnvelope> b)
    {
        b.ToTable("SignEnvelope");
        b.HasKey(x => x.EnvelopeId);

        b.Property(x => x.Subject).HasMaxLength(200).IsRequired();
        b.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("Draft");
        b.Property(x => x.LocationCode).HasMaxLength(10);
        b.Property(x => x.PdfStoragePath).HasMaxLength(400);
        b.Property(x => x.CreatedDateUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.PdfSha256).HasColumnType("varbinary(32)");  // ensure fixed 32-byte SHA-256


        b.HasOne(x => x.Template)
         .WithMany(t => t.Envelopes)
         .HasForeignKey(x => x.TemplateKey)
         .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Recipients)
         .WithOne(r => r.Envelope)
         .HasForeignKey(r => r.EnvelopeId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Events)
         .WithOne(e => e.Envelope)
         .HasForeignKey(e => e.EnvelopeId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.Status);
        b.HasIndex(x => new { x.LocationCode, x.Status });
        b.HasIndex(x => x.SentAtUtc).IncludeProperties(x => new { x.Status, x.CompletedAtUtc, x.ExpiresAtUtc });
    }
}
