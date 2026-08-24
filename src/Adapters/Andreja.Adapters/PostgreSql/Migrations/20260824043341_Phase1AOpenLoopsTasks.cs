using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Andreja.Adapters.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class Phase1AOpenLoopsTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "open_loops");

            migrationBuilder.CreateTable(
                name: "task_audit",
                schema: "open_loops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_audit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "task_receipts",
                schema: "open_loops",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Intent = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    TaskVersion = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_receipts", x => new { x.TenantId, x.ActorId, x.IdempotencyKey });
                });

            migrationBuilder.CreateTable(
                name: "tasks",
                schema: "open_loops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerPrincipalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Details = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SourceKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tasks", x => x.Id);
                    table.UniqueConstraint("AK_tasks_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_tasks_principals_TenantId_OwnerPrincipalId",
                        columns: x => new { x.TenantId, x.OwnerPrincipalId },
                        principalSchema: "identity",
                        principalTable: "principals",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_task_audit_TenantId_ResourceId_OccurredAt",
                schema: "open_loops",
                table: "task_audit",
                columns: new[] { "TenantId", "ResourceId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_tasks_TenantId_OwnerPrincipalId_Status",
                schema: "open_loops",
                table: "tasks",
                columns: new[] { "TenantId", "OwnerPrincipalId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "task_audit",
                schema: "open_loops");

            migrationBuilder.DropTable(
                name: "task_receipts",
                schema: "open_loops");

            migrationBuilder.DropTable(
                name: "tasks",
                schema: "open_loops");
        }
    }
}
