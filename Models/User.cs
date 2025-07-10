using Azure.Core.Pipeline;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.Identity.Client;

namespace SalesMetrics.Models
{
    public class User
    {
        public int Users_ID { get; set; }  // This maps to the SQL identity column
        public int UserID { get; set; }   // ERP User ID
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? PasswordHash { get; set; }
        public string? Email { get; set; }
        public int RoleID { get; set; }  // Maps to SalesMetrics RoleID
        public int Location { get; set; }  // Maps to SalesMetrics LocationID
        public int SalesmanID { get; set; } // Maps to ERP SalemanID
        public string? SalesmanNumber { get; set; } // Maps to ERP Salesman Number
        public DateTime CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public bool IsActive { get; set; }

        public string FullName() => $"{FirstName} {LastName}";
        public string NameandInitial() => $"{FirstName} {LastName?.Substring(0, 1)}.";
    }

    public class UserProfileViewModel
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }    
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? GoogleEmail { get; set; }
        public string? GoogleAccessToken { get; set; }
        public string? GoogleRefreshToken { get; set; }
        public int SalesmanID { get; set; } // Maps to ERP SalemanID
        public string? SalesmanNumber { get; set; } // Maps to ERP Salesman Number

        public int RoleId { get; set; }  // Maps to SalesMetrics RoleID
        public int LocationId { get; set; }
        public string LocationName()
        {
            return LocationId switch
            {
                1 => "Los Angeles",
                2 => "Las Vegas",
                3 => "Chino",
                4 => "Phoenix",
                5 => "San Diego",
                _ => "Unknown"
            };
        }
    }

    public class RegisterViewModel
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Username { get; set; }
        public string? Password { get; set; }
        public int Users_Id { get; set; }
        public int UserId { get; set; }
        public int RoleId { get; set; }
        public int LocationId { get; set; }
        public int? SalesmanId { get; set; }
        public string? SalesmanNumber { get; set; }
        public List<int> AssignedLocationIds { get; set; } = new();
        public List<SelectListItem> AllLocations { get; set; } = new();
    }

    public class FlaggedUserViewModel
    {
        public int Users_ID { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public int LocationId { get; set; }
        public string Reason { get; set; }

        public string LocationName()
        {
            return LocationId switch
            {
                1 => "Los Angeles",
                2 => "Las Vegas",
                3 => "Chino",
                4 => "Phoenix",
                5 => "San Diego",
                _ => "Unknown"
            };
        }

    }

}
