// Services/Signing/IErpMergeService.cs
using Microsoft.AspNetCore.Mvc;
using SalesMetrics.Domain.Signing;
using SalesMetrics.Models;
using SalesMetrics.Models.Signing;
using System.Threading.Tasks;

namespace SalesMetrics.Services.Signing;

public interface IErpMergeService
{
    Task<string> RenderHtmlAsync(string templateKey, long envelopeId, long? recipientId = null);
    Task<string?> GetPropertyNameAsync(int? propertyId);
    Task<string?> GetPropertyAddressAsync(int? propertyId);
    Task<string?> GetUnitNumberByOrderIdAsync(int? orderId);

    // New: Methods used by the API controllers for searching properties and orders
    Task<IReadOnlyList<CustomerPropertyViewModel>> SearchPropertiesAsync(string term, int take = 20);
    Task<IReadOnlyList<WorkOrderViewModel>> GetOrdersForPropertyAsync(int propertyId, string status = "pending");
}
