// Infrastructure/EF/Configurations/Signing/SignEventConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesMetrics.Domain.Signing;

namespace SalesMetrics.Infrastructure.EF.Configurations.Signing;

public sealed class SignEventConfiguration : IEntityTypeConfiguration<SignEvent>
{
    public void Configure(EntityTypeBuilder<SignEvent> b)
    {
        b.ToTable("SignEvent", null, t =>
        {
            t.HasCheckConstraint("CK_SignEvent_Type",
                "[EventType] IN ('Sent','Opened','Consented','Signed','Edited','Completed','Voided','Declined','Expired','Downloaded','Reminded','TenantSkipped')");
        });
        b.HasKey(x => x.EventId);
        b.Property(x => x.EventType).HasMaxLength(30).IsRequired();
        b.Property(x => x.OccurredAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasOne(x => x.Recipient)
         .WithMany(r => r.Events)
         .HasForeignKey(x => x.RecipientId)
         .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => new { x.EnvelopeId, x.OccurredAtUtc }).IsDescending(false, true);
        b.HasIndex(x => new { x.RecipientId, x.OccurredAtUtc });
    }
}
