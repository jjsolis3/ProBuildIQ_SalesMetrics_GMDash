using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient; // 👈 for SqlConnection, SqlCommand, SqlDataReader
using SalesMetrics.Models;

namespace SalesMetrics.Services.Helpers
{
    public static class UserHelper
    {
        public static List<User> GetActiveUsers(HttpContext httpContext, IConfiguration config)
        {
            var userId = Convert.ToInt32(httpContext.Session.GetString("UserId"));
            var roleId = Convert.ToInt32(httpContext.Session.GetString("RoleId"));
            var locationId = Convert.ToInt32(httpContext.Session.GetString("LocationId"));

            var users = new List<User>();

            using (SqlConnection conn = new SqlConnection(config.GetConnectionString("SalesMetrics")))
            {
                conn.Open();

                string query;

                if (roleId == 1 || roleId == 3 || roleId == 4)
                {
                    query = @"SELECT UserID, FirstName, LastName, RoleID, Location, CreatedDate
                          FROM Users
                          WHERE RoleID = 2 AND Location = @Location";
                }
                else
                {
                    query = @"SELECT UserID, FirstName, LastName, RoleID, Location, CreatedDate
                          FROM Users
                          WHERE UserID = @UserId";
                }

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (roleId == 1 || roleId == 3 || roleId == 4)
                        cmd.Parameters.AddWithValue("@Location", locationId);
                    else
                        cmd.Parameters.AddWithValue("@UserId", userId);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            users.Add(new User
                            {
                                UserID = reader.GetInt32(0),
                                FirstName = reader.GetString(1),
                                LastName = reader.GetString(2),
                                RoleID = reader.GetInt32(3),
                                Location = reader.GetInt32(4),
                                CreatedDate = reader.GetDateTime(5),
                            });
                        }
                    }
                }
            }

            return users;
        }
    }

}
