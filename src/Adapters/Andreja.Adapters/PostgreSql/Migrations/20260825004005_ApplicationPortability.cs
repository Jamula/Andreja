using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Andreja.Adapters.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class ApplicationPortability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "portability");

            migrationBuilder.CreateTable(
                name: "application_imports",
                schema: "portability",
                columns: table => new
                {
                    ExportId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManifestSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TenantReference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_imports", x => x.ExportId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_application_imports_ManifestSha256",
                schema: "portability",
                table: "application_imports",
                column: "ManifestSha256",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_imports",
                schema: "portability");
        }
    }
}
