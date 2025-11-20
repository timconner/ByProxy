using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByProxy.Migrations
{
    /// <inheritdoc />
    public partial class v100 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AcmeAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    Hidden = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcmeAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AcmeDnsProviders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Draft = table.Column<string>(type: "TEXT", nullable: true),
                    Script = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcmeDnsProviders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuthRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FriendlyName = table.Column<string>(type: "TEXT", nullable: true),
                    Xml = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FullName = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    PasswordResetRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    PasswordLastSet = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Culture = table.Column<string>(type: "TEXT", nullable: true),
                    PreferredTheme = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Certificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Hidden = table.Column<bool>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 13, nullable: false),
                    AcmeAccountId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LastAttempt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IssuingCAId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Certificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Certificates_AcmeAccounts_AcmeAccountId",
                        column: x => x.AcmeAccountId,
                        principalTable: "AcmeAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Certificates_Certificates_IssuingCAId",
                        column: x => x.IssuingCAId,
                        principalTable: "Certificates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuthSessions",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthSessions", x => x.Key);
                    table.ForeignKey(
                        name: "FK_AuthSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    RolesId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserEntityId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.RolesId, x.UserEntityId });
                    table.ForeignKey(
                        name: "FK_UserRoles_AuthRoles_RolesId",
                        column: x => x.RolesId,
                        principalTable: "AuthRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserEntityId",
                        column: x => x.UserEntityId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AcmeHosts",
                columns: table => new
                {
                    CertificateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Host = table.Column<string>(type: "TEXT", nullable: false),
                    ChallengeType = table.Column<string>(type: "TEXT", maxLength: 13, nullable: false),
                    DnsProviderId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcmeHosts", x => new { x.CertificateId, x.Host });
                    table.ForeignKey(
                        name: "FK_AcmeHosts_AcmeDnsProviders_DnsProviderId",
                        column: x => x.DnsProviderId,
                        principalTable: "AcmeDnsProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AcmeHosts_Certificates_CertificateId",
                        column: x => x.CertificateId,
                        principalTable: "Certificates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Configurations",
                columns: table => new
                {
                    Revision = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BasedOnRevision = table.Column<int>(type: "INTEGER", nullable: false),
                    Committed = table.Column<bool>(type: "INTEGER", nullable: false),
                    Confirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    Reverted = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReversionReason = table.Column<string>(type: "TEXT", nullable: true),
                    CommittedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ConfirmSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    FallbackCertId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AdminListenAny = table.Column<bool>(type: "INTEGER", nullable: false),
                    AdminPort = table.Column<int>(type: "INTEGER", nullable: false),
                    AdminCertId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UnmatchedStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    Tarpit = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Configurations", x => x.Revision);
                    table.ForeignKey(
                        name: "FK_Configurations_Certificates_AdminCertId",
                        column: x => x.AdminCertId,
                        principalTable: "Certificates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Configurations_Certificates_FallbackCertId",
                        column: x => x.FallbackCertId,
                        principalTable: "Certificates",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Clusters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConfigRevision = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    LoadBalancing = table.Column<string>(type: "TEXT", nullable: true),
                    StickySessions = table.Column<bool>(type: "INTEGER", nullable: false),
                    AcceptAnyCert = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clusters", x => new { x.Id, x.ConfigRevision });
                    table.ForeignKey(
                        name: "FK_Clusters_Configurations_ConfigRevision",
                        column: x => x.ConfigRevision,
                        principalTable: "Configurations",
                        principalColumn: "Revision",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SniMaps",
                columns: table => new
                {
                    ConfigRevision = table.Column<int>(type: "INTEGER", nullable: false),
                    Host = table.Column<string>(type: "TEXT", nullable: false),
                    CertificateId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SniMaps", x => new { x.Host, x.ConfigRevision });
                    table.ForeignKey(
                        name: "FK_SniMaps_Certificates_CertificateId",
                        column: x => x.CertificateId,
                        principalTable: "Certificates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SniMaps_Configurations_ConfigRevision",
                        column: x => x.ConfigRevision,
                        principalTable: "Configurations",
                        principalColumn: "Revision",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Destinations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConfigRevision = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ClusterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClusterConfigRevision = table.Column<int>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Address = table.Column<string>(type: "TEXT", nullable: false),
                    Health = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Destinations", x => new { x.Id, x.ConfigRevision });
                    table.ForeignKey(
                        name: "FK_Destinations_Clusters_ClusterId_ClusterConfigRevision",
                        columns: x => new { x.ClusterId, x.ClusterConfigRevision },
                        principalTable: "Clusters",
                        principalColumns: new[] { "Id", "ConfigRevision" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Destinations_Configurations_ConfigRevision",
                        column: x => x.ConfigRevision,
                        principalTable: "Configurations",
                        principalColumn: "Revision",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Routes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConfigRevision = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    ResponseType = table.Column<string>(type: "TEXT", nullable: false),
                    ClusterId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ClusterConfigRevision = table.Column<int>(type: "INTEGER", nullable: true),
                    HttpStatusCode = table.Column<int>(type: "INTEGER", nullable: true),
                    RedirectLocation = table.Column<string>(type: "TEXT", nullable: true),
                    Methods = table.Column<string>(type: "TEXT", nullable: true),
                    Hosts = table.Column<string>(type: "TEXT", nullable: true),
                    PreserveHostHeader = table.Column<bool>(type: "INTEGER", nullable: false),
                    PreserveClientHeaders = table.Column<bool>(type: "INTEGER", nullable: false),
                    AllowedHeaders = table.Column<string>(type: "TEXT", nullable: true),
                    XForwardedPrefix = table.Column<string>(type: "TEXT", nullable: true),
                    XForwardedForAction = table.Column<string>(type: "TEXT", nullable: true),
                    XForwardedProtoAction = table.Column<string>(type: "TEXT", nullable: true),
                    XForwardedHostAction = table.Column<string>(type: "TEXT", nullable: true),
                    XForwardedPrefixAction = table.Column<string>(type: "TEXT", nullable: true),
                    Path = table.Column<string>(type: "TEXT", nullable: true),
                    PathIsPrefix = table.Column<bool>(type: "INTEGER", nullable: false),
                    HttpPort = table.Column<int>(type: "INTEGER", nullable: true),
                    HttpsPort = table.Column<int>(type: "INTEGER", nullable: true),
                    SuppressHttpsRedirect = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Routes", x => new { x.Id, x.ConfigRevision });
                    table.ForeignKey(
                        name: "FK_Routes_Clusters_ClusterId_ClusterConfigRevision",
                        columns: x => new { x.ClusterId, x.ClusterConfigRevision },
                        principalTable: "Clusters",
                        principalColumns: new[] { "Id", "ConfigRevision" });
                    table.ForeignKey(
                        name: "FK_Routes_Configurations_ConfigRevision",
                        column: x => x.ConfigRevision,
                        principalTable: "Configurations",
                        principalColumn: "Revision",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RouteTransforms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConfigRevision = table.Column<int>(type: "INTEGER", nullable: false),
                    RouteId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RouteConfigRevision = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 21, nullable: false),
                    HeaderOperation = table.Column<string>(type: "TEXT", nullable: true),
                    HeaderName = table.Column<string>(type: "TEXT", nullable: true),
                    HeaderValue = table.Column<string>(type: "TEXT", nullable: true),
                    FromHttpMethod = table.Column<string>(type: "TEXT", nullable: true),
                    ToHttpMethod = table.Column<string>(type: "TEXT", nullable: true),
                    PathMode = table.Column<string>(type: "TEXT", nullable: true),
                    PathString = table.Column<string>(type: "TEXT", nullable: true),
                    QueryOperation = table.Column<string>(type: "TEXT", nullable: true),
                    QueryKey = table.Column<string>(type: "TEXT", nullable: true),
                    QueryValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteTransforms", x => new { x.Id, x.ConfigRevision });
                    table.ForeignKey(
                        name: "FK_RouteTransforms_Configurations_ConfigRevision",
                        column: x => x.ConfigRevision,
                        principalTable: "Configurations",
                        principalColumn: "Revision",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RouteTransforms_Routes_RouteId_RouteConfigRevision",
                        columns: x => new { x.RouteId, x.RouteConfigRevision },
                        principalTable: "Routes",
                        principalColumns: new[] { "Id", "ConfigRevision" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcmeHosts_DnsProviderId",
                table: "AcmeHosts",
                column: "DnsProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_AuthSessions_UserId",
                table: "AuthSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Certificates_AcmeAccountId",
                table: "Certificates",
                column: "AcmeAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Certificates_IssuingCAId",
                table: "Certificates",
                column: "IssuingCAId");

            migrationBuilder.CreateIndex(
                name: "IX_Clusters_ConfigRevision",
                table: "Clusters",
                column: "ConfigRevision");

            migrationBuilder.CreateIndex(
                name: "IX_Configurations_AdminCertId",
                table: "Configurations",
                column: "AdminCertId");

            migrationBuilder.CreateIndex(
                name: "IX_Configurations_FallbackCertId",
                table: "Configurations",
                column: "FallbackCertId");

            migrationBuilder.CreateIndex(
                name: "IX_Destinations_ClusterId_ClusterConfigRevision",
                table: "Destinations",
                columns: new[] { "ClusterId", "ClusterConfigRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_Destinations_ConfigRevision",
                table: "Destinations",
                column: "ConfigRevision");

            migrationBuilder.CreateIndex(
                name: "IX_Routes_ClusterId_ClusterConfigRevision",
                table: "Routes",
                columns: new[] { "ClusterId", "ClusterConfigRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_Routes_ConfigRevision",
                table: "Routes",
                column: "ConfigRevision");

            migrationBuilder.CreateIndex(
                name: "IX_RouteTransforms_ConfigRevision",
                table: "RouteTransforms",
                column: "ConfigRevision");

            migrationBuilder.CreateIndex(
                name: "IX_RouteTransforms_RouteId_RouteConfigRevision",
                table: "RouteTransforms",
                columns: new[] { "RouteId", "RouteConfigRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_SniMaps_CertificateId",
                table: "SniMaps",
                column: "CertificateId");

            migrationBuilder.CreateIndex(
                name: "IX_SniMaps_ConfigRevision",
                table: "SniMaps",
                column: "ConfigRevision");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_UserEntityId",
                table: "UserRoles",
                column: "UserEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcmeHosts");

            migrationBuilder.DropTable(
                name: "AuthSessions");

            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropTable(
                name: "Destinations");

            migrationBuilder.DropTable(
                name: "RouteTransforms");

            migrationBuilder.DropTable(
                name: "SniMaps");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "AcmeDnsProviders");

            migrationBuilder.DropTable(
                name: "Routes");

            migrationBuilder.DropTable(
                name: "AuthRoles");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Clusters");

            migrationBuilder.DropTable(
                name: "Configurations");

            migrationBuilder.DropTable(
                name: "Certificates");

            migrationBuilder.DropTable(
                name: "AcmeAccounts");
        }
    }
}
