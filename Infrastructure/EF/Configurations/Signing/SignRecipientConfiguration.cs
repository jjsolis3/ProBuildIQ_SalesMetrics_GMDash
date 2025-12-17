// Infrastructure/EF/Configurations/Signing/SignRecipientConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesMetrics.Domain.Signing;

namespace SalesMetrics.Infrastructure.EF.Configurations.Signing;

public sealed class SignRecipientConfiguration : IEntityTypeConfiguration<SignRecipient>
{
    public void Configure(EntityTypeBuilder<SignRecipient> b)
    {
        b.ToTable("SignRecipient");
        b.HasKey(x => x.RecipientId);

        b.Property(x => x.Role).HasMaxLength(30).IsRequired();
        b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Email).HasMaxLength(320).IsRequired();
        b.Property(x => x.Phone).HasMaxLength(30);
        b.Property(x => x.AccessToken).HasMaxLength(150).IsRequired();
        b.Property(x => x.IPAddressViewed).HasMaxLength(45);
        b.Property(x => x.IPAddressSigned).HasMaxLength(45);
        b.Property(x => x.UserAgentViewed).HasMaxLength(400);
        b.Property(x => x.UserAgentSigned).HasMaxLength(400);
        b.Property(x => x.SignatureImagePath).HasMaxLength(400);
        b.Property(x => x.CreatedDateUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasIndex(x => x.EnvelopeId);
        b.HasIndex(x => new { x.EnvelopeId, x.SignerOrder });
        b.HasIndex(x => x.Email);
        b.HasIndex(x => x.AccessToken).IsUnique();
    }
}
