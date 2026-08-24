using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Andreja.Adapters.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class DurableRecentAuthenticationGrants : Migration
    {
        private static readonly string[] UserStateIndexColumns =
            ["UserId", "ConsumedAt", "ExpiresAt"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "recent_authentication_grants",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    NonceHash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recent_authentication_grants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recent_authentication_grants_credential_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "credential_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_recent_authentication_grants_NonceHash",
                schema: "identity",
                table: "recent_authentication_grants",
                column: "NonceHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recent_authentication_grants_UserId_ConsumedAt_ExpiresAt",
                schema: "identity",
                table: "recent_authentication_grants",
                columns: UserStateIndexColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recent_authentication_grants",
                schema: "identity");
        }
    }
}
