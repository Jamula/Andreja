using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Andreja.Adapters.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class ProductionPasskeyIdentity : Migration
    {
        private static readonly string[] RecoveryCodeIndexColumns =
            ["UserId", "ConsumedAt", "ExpiresAt"];
        private static readonly string[] AuditIndexColumns =
            ["UserId", "Operation", "OccurredAt"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bootstrap_state",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bootstrap_state", x => x.Id);
                    table.CheckConstraint("ck_bootstrap_state_singleton", "\"Id\" = '0198d19e-5d34-7000-8000-000000000001'::uuid");
                    table.ForeignKey(
                        name: "FK_bootstrap_state_credential_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "credential_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recovery_codes",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LookupHash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    Salt = table.Column<byte[]>(type: "bytea", maxLength: 16, nullable: false),
                    VerificationHash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recovery_codes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recovery_codes_credential_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "credential_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "security_audit",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Operation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_audit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_security_audit_credential_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "credential_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bootstrap_state_UserId",
                schema: "identity",
                table: "bootstrap_state",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recovery_codes_LookupHash",
                schema: "identity",
                table: "recovery_codes",
                column: "LookupHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recovery_codes_UserId_ConsumedAt_ExpiresAt",
                schema: "identity",
                table: "recovery_codes",
                columns: RecoveryCodeIndexColumns);

            migrationBuilder.CreateIndex(
                name: "IX_security_audit_UserId_Operation_OccurredAt",
                schema: "identity",
                table: "security_audit",
                columns: AuditIndexColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bootstrap_state",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "recovery_codes",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "security_audit",
                schema: "identity");
        }
    }
}
