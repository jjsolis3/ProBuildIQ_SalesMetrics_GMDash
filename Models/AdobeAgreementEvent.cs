using System;

namespace SalesMetrics.Models
{
    public class AdobeAgreementEvent
    {
        public int Id { get; set; }
        public string AgreementId { get; set; } = "";
        public string EventType { get; set; } = "";
        public string? ActorEmail { get; set; }
        public string? Meta { get; set; }         // raw webhook JSON if you like
        public DateTime OccurredAt { get; set; }
        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    }
}
