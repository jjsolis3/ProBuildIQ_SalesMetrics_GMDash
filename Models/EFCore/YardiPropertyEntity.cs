using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesMetrics.Models.EFCore
{
    [Table("YardiProperties")]
    public class YardiPropertyEntity
    {
        [Key]
        public int Property_ID { get; set; }


        // Region
        [MaxLength(255)]
        public string? Market { get; set; }
        [MaxLength(100)]
        public string? Submarket { get; set; }

        // Property Info
        [MaxLength(100)]
        public string? YardiID { get; set; }
        [MaxLength(255)]
        public string? PropertyName { get; set; }
        [MaxLength(255)]
        public string? PropertyAddress { get; set; }
        [MaxLength(100)]
        public string? PropertyCity { get; set; }
        [MaxLength(100)]
        public string? PropertyCounty { get; set; }
        [MaxLength(50)]
        public string? PropertyState { get; set; }
        [MaxLength(20)]
        public string? PropertyZipCode { get; set; }
        [MaxLength(50)]
        public string? PropertyPhone { get; set; }
        [MaxLength(255)]
        public string? PropertyStatus { get; set; }
        public int? Units { get; set; }
        public double? SqFt { get; set; }
        public DateTime? CompletionDate { get; set; }

        // Rating Info
        [MaxLength(10)]
        public string? ImprRating { get; set; }
        [MaxLength(10)]
        public string? LocRating { get; set; }

        // Owner Info
        [MaxLength(255)]
        public string? Owner { get; set; }
        [MaxLength(255)]
        public string? OwnerFName { get; set; }
        [MaxLength(255)]
        public string? OwnerLNname { get; set; }
        [MaxLength(255)]
        public string? OwnerEmail { get; set; }
        [MaxLength(255)]
        public string? OwnverAddress { get; set; }
        [MaxLength(100)]
        public string? OwnverCity { get; set; }
        [MaxLength(50)]
        public string? OwnverState { get; set; }
        [MaxLength(20)]
        public string? OwnverZipCode { get; set; }
        [MaxLength(50)]
        public string? OwnerPhone { get; set; }
        [MaxLength(255)]
        public string? OwnerWebsite { get; set; }

        // Manager Info
        [MaxLength(255)]
        public string? Manager { get; set; }
        [MaxLength(255)]
        public string? ManagerFName { get; set; }
        [MaxLength(255)]
        public string? ManagerLName { get; set; }
        [MaxLength(255)]
        public string? ManagerAddress { get; set; }
        [MaxLength(100)]
        public string? ManagerCity { get; set; }
        [MaxLength(50)]
        public string? ManagerState { get; set; }
        [MaxLength(20)]
        public string? ManagerZIP { get; set; }
        [MaxLength(50)]
        public string? ManagerPhone { get; set; }
        [MaxLength(255)]
        public string? ManagerWebsite { get; set; }

        // Notes
        [MaxLength(1000)]
        public string? PropertyNotes { get; set; }
        [MaxLength(1000)]
        public string? OwnerNotes { get; set; }
        [MaxLength(1000)]
        public string? ManagerNotes { get; set; }

        // Other Info
        public int? YearBuilt { get; set; }
        public int? YearRenovated { get; set; }
        [Precision(5, 2)]
        public decimal? OccupancyRate { get; set; }
        [Precision(10, 2)]
        public decimal? AvgAskingRent { get; set; }
        [MaxLength(100)]
        public string? PropertyType { get; set; }
        [MaxLength(100)]
        public string? ConstructionType { get; set; }
        public string? Amenities { get; set; }
        public string? Notes { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        // Meta
        [MaxLength(100)]
        public string? ImportedBy { get; set; }
        public DateTime? ImportedDate { get; set; }

        // Navigation properties
        [MaxLength(20)]
        public string Locations { get; set; }
    }
}
