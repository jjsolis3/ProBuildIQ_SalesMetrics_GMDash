using System;
using System.Collections.Generic;

namespace SalesMetrics.Models.EFCore;

public partial class UserEntity
{
    public int Users_ID { get; set; }

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public string Username { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string? PasswordHash { get; set; }

    public DateTime? PasswordChangedDate { get; set; }

    public string? Email { get; set; }

    public int UserId { get; set; }

    public int RoleId { get; set; }

    public int Location { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? ModifiedDate { get; set; }

    public bool IsActive { get; set; }

    public DateTime? InActiveDate { get; set; }

    public DateTime? LastLoginDate { get; set; }

    public int? FailedLoginAttempts { get; set; }

    public bool? AccountLocked { get; set; }

    public DateTime? AccountLockedDate { get; set; }

    public string? SessionToken { get; set; }

    public string? Salt { get; set; }
    public int? SalesmanId { get; set; }
    public string? SalesmanNumber { get; set; }

    public virtual RoleEntity Role { get; set; } = null!;
}
