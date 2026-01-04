// Services/Signing/IPdfService.cs
using System.Threading.Tasks;

namespace SalesMetrics.Services.Signing;

public interface IPdfService
{
    Task<(byte[] bytes, string storagePath, byte[] sha256)> RenderAndSealAsync(long envelopeId);
    Task<byte[]> GeneratePreviewPdfAsync(long envelopeId, long currentRecipientId);
}
