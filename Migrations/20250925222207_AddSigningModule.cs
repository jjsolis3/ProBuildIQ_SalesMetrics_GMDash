using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesMetrics.Migrations
{
    /// <inheritdoc />
    public partial class AddSigningModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Locations",
                columns: table => new
                {
                    LocationID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LocationNumber = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    LocationName = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false),
                    LocationAbrv = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Location__E7FEA4776C521054", x => x.LocationID);
                });

            migrationBuilder.CreateTable(
                name: "LoginHistory",
                columns: table => new
                {
                    LoginHistoryID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    UserName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    LoginDate = table.Column<DateOnly>(type: "date", nullable: false, defaultValueSql: "(CONVERT([date],getdate()))"),
                    LoginTime = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    Success = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    IPAddress = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    office = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__LoginHis__2773EAFFA82CC141", x => x.LoginHistoryID);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    RoleID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Roles__8AFACE3A7BCC7B90", x => x.RoleID);
                });

            migrationBuilder.CreateTable(
                name: "SignTemplate",
                columns: table => new
                {
                    TemplateKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RazorViewPath = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    MergeSpecJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DefaultSubject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DefaultMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedByUsers_ID = table.Column<int>(type: "int", nullable: false),
                    CreatedDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ModifiedByUsers_ID = table.Column<int>(type: "int", nullable: true),
                    ModifiedDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignTemplate", x => x.TemplateKey);
                });

            migrationBuilder.CreateTable(
                name: "Tasks",
                columns: table => new
                {
                    TaskID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true, defaultValue: "Pending"),
                    AssignedTo = table.Column<int>(type: "int", nullable: false),
                    Location = table.Column<int>(type: "int", nullable: false),
                    Property = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PropertyID = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    ModifiedDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    CancelledDate = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Tasks__7C6949D10FBD462C", x => x.TaskID);
                });

            migrationBuilder.CreateTable(
                name: "YardiProperties",
                columns: table => new
                {
                    Property_ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Market = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Submarket = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    YardiID = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PropertyName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PropertyAddress = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PropertyCity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PropertyCounty = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PropertyState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PropertyZipCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PropertyPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PropertyStatus = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Units = table.Column<int>(type: "int", nullable: true),
                    SqFt = table.Column<double>(type: "float", nullable: true),
                    CompletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ImprRating = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    LocRating = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Owner = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    OwnerFName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    OwnerLNname = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    OwnerEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    OwnverAddress = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    OwnverCity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OwnverState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OwnverZipCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    OwnerPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OwnerWebsite = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Manager = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ManagerFName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ManagerLName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ManagerAddress = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ManagerCity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ManagerState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ManagerZIP = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ManagerPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ManagerWebsite = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PropertyNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OwnerNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ManagerNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    YearBuilt = table.Column<int>(type: "int", nullable: true),
                    YearRenovated = table.Column<int>(type: "int", nullable: true),
                    OccupancyRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    AvgAskingRent = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    PropertyType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ConstructionType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Amenities = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    ImportedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ImportedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Locations = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YardiProperties", x => x.Property_ID);
                });

            migrationBuilder.CreateTable(
                name: "UserLocationAssignments",
                columns: table => new
                {
                    AssignmentID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    LocationID = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateAssigned = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLocationAssignments", x => x.AssignmentID);
                    table.ForeignKey(
                        name: "FK_UserLocationAssignments_Locations_LocationID",
                        column: x => x.LocationID,
                        principalTable: "Locations",
                        principalColumn: "LocationID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Users_ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirstName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Username = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Password = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PasswordChangedDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    RoleID = table.Column<int>(type: "int", nullable: false),
                    Location = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    InActiveDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    LastLoginDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    FailedLoginAttempts = table.Column<int>(type: "int", nullable: true),
                    AccountLocked = table.Column<bool>(type: "bit", nullable: true, defaultValue: false),
                    AccountLockedDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    SessionToken = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    Salt = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: true),
                    SalesmanId = table.Column<int>(type: "int", nullable: false),
                    SalesmanNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Users__EB68290DB9B7B8E8", x => x.Users_ID);
                    table.ForeignKey(
                        name: "FK_Users_Roles",
                        column: x => x.RoleID,
                        principalTable: "Roles",
                        principalColumn: "RoleID");
                });

            migrationBuilder.CreateTable(
                name: "SignEnvelope",
                columns: table => new
                {
                    EnvelopeId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TemplateKey = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MessageBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PropertyID = table.Column<int>(type: "int", nullable: true),
                    OrderId = table.Column<int>(type: "int", nullable: true),
                    CustomerNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LocationCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Draft"),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeclinedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VoidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PdfStoragePath = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    PdfSha256 = table.Column<byte[]>(type: "varbinary(32)", nullable: true),
                    CreatedByUsers_ID = table.Column<int>(type: "int", nullable: false),
                    CreatedDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ModifiedByUsers_ID = table.Column<int>(type: "int", nullable: true),
                    ModifiedDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignEnvelope", x => x.EnvelopeId);
                    table.CheckConstraint("CK_SignEnvelope_Status", "Status IN ('Draft','Sent','Viewed','Completed','Expired','Declined','Voided')");
                    table.ForeignKey(
                        name: "FK_SignEnvelope_SignTemplate_TemplateKey",
                        column: x => x.TemplateKey,
                        principalTable: "SignTemplate",
                        principalColumn: "TemplateKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SignAttachment",
                columns: table => new
                {
                    AttachmentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnvelopeId = table.Column<long>(type: "bigint", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    BlobPath = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    MimeType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UploadedByUsers_ID = table.Column<int>(type: "int", nullable: false),
                    UploadedDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignAttachment", x => x.AttachmentId);
                    table.ForeignKey(
                        name: "FK_SignAttachment_SignEnvelope_EnvelopeId",
                        column: x => x.EnvelopeId,
                        principalTable: "SignEnvelope",
                        principalColumn: "EnvelopeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SignRecipient",
                columns: table => new
                {
                    RecipientId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnvelopeId = table.Column<long>(type: "bigint", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SignerOrder = table.Column<int>(type: "int", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    AccessToken = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    AccessTokenExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequiresOtp = table.Column<bool>(type: "bit", nullable: false),
                    LastOtpSentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ViewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeclinedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IPAddressViewed = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    IPAddressSigned = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgentViewed = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UserAgentSigned = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    SignatureImagePath = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    SignatureTyped = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SignatureMetaJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignRecipient", x => x.RecipientId);
                    table.CheckConstraint("CK_SignRecipient_Role", "Role IN ('Manager','Tenant','Other')");
                    table.ForeignKey(
                        name: "FK_SignRecipient_SignEnvelope_EnvelopeId",
                        column: x => x.EnvelopeId,
                        principalTable: "SignEnvelope",
                        principalColumn: "EnvelopeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SignEvent",
                columns: table => new
                {
                    EventId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnvelopeId = table.Column<long>(type: "bigint", nullable: false),
                    RecipientId = table.Column<long>(type: "bigint", nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    MetaJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignEvent", x => x.EventId);
                    table.ForeignKey(
                        name: "FK_SignEvent_SignEnvelope_EnvelopeId",
                        column: x => x.EnvelopeId,
                        principalTable: "SignEnvelope",
                        principalColumn: "EnvelopeId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SignEvent_SignRecipient_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "SignRecipient",
                        principalColumn: "RecipientId");
                });

            migrationBuilder.CreateTable(
                name: "SignField",
                columns: table => new
                {
                    FieldId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnvelopeId = table.Column<long>(type: "bigint", nullable: false),
                    RecipientId = table.Column<long>(type: "bigint", nullable: true),
                    FieldKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FieldType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FieldValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignField", x => x.FieldId);
                    table.CheckConstraint("CK_SignField_Type", "FieldType IN ('text','checkbox','initials','date')");
                    table.ForeignKey(
                        name: "FK_SignField_SignEnvelope_EnvelopeId",
                        column: x => x.EnvelopeId,
                        principalTable: "SignEnvelope",
                        principalColumn: "EnvelopeId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SignField_SignRecipient_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "SignRecipient",
                        principalColumn: "RecipientId");
                });

            // ENVELOPE TEMPLATES  
            migrationBuilder.InsertData(
                table: "SignTemplate",
                columns: new[] { "TemplateKey", "CreatedByUsers_ID", "CreatedDateUtc", "DefaultMessage", "DefaultSubject", "DisplayName", "IsActive", "MergeSpecJson", "ModifiedByUsers_ID", "ModifiedDateUtc", "RazorViewPath" },
                values: new object[] { "TenantConsentV1", 1, new DateTime(2025, 9, 25, 22, 22, 7, 375, DateTimeKind.Utc).AddTicks(8179), "Hi {{Recipient.FirstName}}, please review and sign to proceed.", "Please review and sign: Tenant Work Consent", "Tenant Work Consent (v1)", true, "{\r\n  \"merge\":\"property,order,customer\",\r\n  \"fields\":{\r\n    \"PropertyName\":\"ERP.Properties.Name\",\r\n    \"PropertyAddress\":\"ERP.Properties.Address\",\r\n    \"UnitNumber\":\"ERP.Orders.Unit\",\r\n    \"ManagerName\":\"ERP.PropertyManager.Name\",\r\n    \"TenantName\":\"ERP.Tenant.Name\",\r\n    \"OrderId\":\"ERP.Orders.Id\",\r\n    \"City\":\"ERP.Properties.City\",\r\n    \"State\":\"ERP.Properties.State\",\r\n    \"Zip\":\"ERP.Properties.Zip\"\r\n  }\r\n}", null, null, "/Views/SignTemplates/TenantConsent.cshtml" });

            migrationBuilder.InsertData(
                table: "SignTemplate",
                columns: new[]
                {
                    "TemplateKey", "CreatedByUsers_ID", "CreatedDateUtc",
                    "DefaultMessage", "DefaultSubject", "DisplayName",
                    "IsActive", "MergeSpecJson", "ModifiedByUsers_ID",
                    "ModifiedDateUtc", "RazorViewPath"
                },
                values: new object[]
                {
                    "OccupiedReleaseV1_4",
                    1,
                    new DateTime(2025, 9, 26, 7, 26, 2, DateTimeKind.Utc), // static!
                    "Hi {{Recipient.FirstName}}, please review the attached release and sign.",
                    "Please review and sign: Occupied Release",
                    "Occupied Release Form (v1.4)",
                    true,
                    "{\r\n  \"merge\":\"property,order,customer\",\r\n  \"fields\":{\r\n    \"PropertyName\":\"ERP.Properties.Name\",\r\n    \"PropertyAddress\":\"ERP.Properties.Address\",\r\n    \"UnitNumber\":\"ERP.Orders.Unit\",\r\n    \"ManagerName\":\"ERP.PropertyManager.Name\",\r\n    \"TenantName\":\"ERP.Tenant.Name\",\r\n    \"OrderId\":\"ERP.Orders.Id\",\r\n    \"City\":\"ERP.Properties.City\",\r\n    \"State\":\"ERP.Properties.State\",\r\n    \"Zip\":\"ERP.Properties.Zip\"\r\n  }\r\n}",
                    null,
                    null,
                    "/Views/SignTemplates/OccupiedRelease.cshtml" // <-- make sure this exists OR point to an existing view for now
                });


            migrationBuilder.CreateIndex(
                name: "UC_Location",
                table: "Locations",
                columns: new[] { "LocationName", "LocationNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UC_LocationAbvr",
                table: "Locations",
                columns: new[] { "LocationName", "LocationAbrv" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SignAttachment_EnvelopeId",
                table: "SignAttachment",
                column: "EnvelopeId");

            migrationBuilder.CreateIndex(
                name: "IX_SignEnvelope_LocationCode_Status",
                table: "SignEnvelope",
                columns: new[] { "LocationCode", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SignEnvelope_SentAtUtc",
                table: "SignEnvelope",
                column: "SentAtUtc")
                .Annotation("SqlServer:Include", new[] { "Status", "CompletedAtUtc", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SignEnvelope_Status",
                table: "SignEnvelope",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SignEnvelope_TemplateKey",
                table: "SignEnvelope",
                column: "TemplateKey");

            migrationBuilder.CreateIndex(
                name: "IX_SignEvent_EnvelopeId_OccurredAtUtc",
                table: "SignEvent",
                columns: new[] { "EnvelopeId", "OccurredAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_SignEvent_RecipientId_OccurredAtUtc",
                table: "SignEvent",
                columns: new[] { "RecipientId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SignField_EnvelopeId",
                table: "SignField",
                column: "EnvelopeId");

            migrationBuilder.CreateIndex(
                name: "IX_SignField_EnvelopeId_FieldKey",
                table: "SignField",
                columns: new[] { "EnvelopeId", "FieldKey" });

            migrationBuilder.CreateIndex(
                name: "IX_SignField_RecipientId",
                table: "SignField",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_SignRecipient_AccessToken",
                table: "SignRecipient",
                column: "AccessToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SignRecipient_Email",
                table: "SignRecipient",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_SignRecipient_EnvelopeId",
                table: "SignRecipient",
                column: "EnvelopeId");

            migrationBuilder.CreateIndex(
                name: "IX_SignRecipient_EnvelopeId_SignerOrder",
                table: "SignRecipient",
                columns: new[] { "EnvelopeId", "SignerOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_SignTemplate_IsActive",
                table: "SignTemplate",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_UserLocationAssignments_LocationID",
                table: "UserLocationAssignments",
                column: "LocationID");

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleID",
                table: "Users",
                column: "RoleID");

            migrationBuilder.CreateIndex(
                name: "UQ_Username_Location",
                table: "Users",
                columns: new[] { "Username", "Location" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Users_UserID_Location",
                table: "Users",
                columns: new[] { "UserID", "Location" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdobeAgreementEvents");

            migrationBuilder.DropTable(
                name: "AdobeAgreements");

            migrationBuilder.DropTable(
                name: "LoginHistory");

            migrationBuilder.DropTable(
                name: "SignAttachment");

            migrationBuilder.DropTable(
                name: "SignEvent");

            migrationBuilder.DropTable(
                name: "SignField");

            migrationBuilder.DropTable(
                name: "Tasks");

            migrationBuilder.DropTable(
                name: "UserLocationAssignments");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "YardiProperties");

            migrationBuilder.DropTable(
                name: "SignRecipient");

            migrationBuilder.DropTable(
                name: "Locations");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "SignEnvelope");

            migrationBuilder.DropTable(
                name: "SignTemplate");

            migrationBuilder.DeleteData(
                table: "SignTemplate",
                keyColumn: "TemplateKey",
                keyValue: "OccupiedReleaseV1_4");

        }
    }
}
