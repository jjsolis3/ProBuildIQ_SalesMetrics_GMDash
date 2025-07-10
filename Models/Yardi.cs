namespace SalesMetrics.Models
{
    public class YardiPropertyViewModel
    {
        public int Property_ID { get; set; }

        // Property Info
        public string? Market { get; set; }
        public string? Submarket { get; set; }
        public string? YardiID { get; set; }
        public string? PropertyName { get; set; }
        public string? PropertyAddress { get; set; }
        public string? PropertyCity { get; set; }
        public string? PropertyCounty { get; set; }
        public string? PropertyState { get; set; }
        public string? PropertyZipCode { get; set; }
        public string? PropertyPhone { get; set; }
        public string? PropertyStatus { get; set; }

        // Ratings
        public string? ImprRating { get; set; }
        public string? LocRating { get; set; }

        // Owner Info
        public string? Owner { get; set; }
        public string? OwnerFName { get; set; }
        public string? OwnerLName { get; set; }
        public string? OwnerEmail { get; set; }
        public string? OwnverAddress { get; set; }
        public string? OwnverCity { get; set; }
        public string? OwnverState { get; set; }
        public string? OwnverZipCode { get; set; }
        public string? OwnerPhone { get; set; }
        public string? OwnerWebsite { get; set; }

        // Manager Info
        public string? Manager { get; set; }
        public string? ManagerFName { get; set; }
        public string? ManagerLName { get; set; }
        public string? ManagerAddress { get; set; }
        public string? ManagerCity { get; set; }
        public string? ManagerState { get; set; }
        public string? ManagerZIP { get; set; }
        public string? ManagerPhone { get; set; }
        public string? ManagerWebsite { get; set; }

        // Notes
        public string? PropertyNotes { get; set; }
        public string? OwnerNotes { get; set; }
        public string? ManagerNotes { get; set; }

        // Property Details
        public string? PropertyType { get; set; }
        public string? ConstructionType { get; set; }
        public string? Amenities { get; set; }
        public string? Notes { get; set; }

        // Numerical and Date Fields
        public int? Units { get; set; }
        public double? SqFt { get; set; }
        public DateTime? CompletionDate { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public decimal? OccupancyRate { get; set; }
        public decimal? AvgAskingRent { get; set; }
        public int? YearBuilt { get; set; }
        public int? YearRenovated { get; set; }

        // Metadata
        public string? ImportedBy { get; set; }
        public DateTime? ImportedDate { get; set; }
        public string? Locations { get; set; }

        // Convenience
        public string? Phone => ManagerPhone ?? OwnerPhone;
        
    }
}
