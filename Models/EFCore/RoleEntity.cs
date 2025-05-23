using System;
using System.Collections.Generic;

namespace SalesMetrics.Models.EFCore;

public partial class RoleEntity
{
    public int RoleId { get; set; }

    public string RoleName { get; set; } = null!;

    public virtual ICollection<UserEntity> Users { get; set; } = new List<UserEntity>();
}
