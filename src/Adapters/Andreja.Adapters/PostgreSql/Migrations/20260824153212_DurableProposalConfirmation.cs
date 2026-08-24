using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Andreja.Adapters.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class DurableProposalConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_tasks_TenantId_Id_OwnerPrincipalId",
                schema: "open_loops",
                table: "tasks",
                columns: new[] { "TenantId", "Id", "OwnerPrincipalId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_memberships_TenantId_AppUserId_PrincipalId",
                schema: "identity",
                table: "memberships",
                columns: new[] { "TenantId", "AppUserId", "PrincipalId" });

            migrationBuilder.CreateTable(
                name: "proposals",
                schema: "open_loops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorAppUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Operation = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ResourceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CanonicalPayload = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    PayloadDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BeforeCanonical = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    AfterCanonical = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    ActiveTaskId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_proposals", x => x.Id);
                    table.UniqueConstraint("AK_proposals_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_proposals_memberships_TenantId_ActorAppUserId_ActorId",
                        columns: x => new { x.TenantId, x.ActorAppUserId, x.ActorId },
                        principalSchema: "identity",
                        principalTable: "memberships",
                        principalColumns: new[] { "TenantId", "AppUserId", "PrincipalId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_proposals_principals_TenantId_SourceActorId",
                        columns: x => new { x.TenantId, x.SourceActorId },
                        principalSchema: "identity",
                        principalTable: "principals",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_proposals_tasks_TenantId_ActiveTaskId_ActorId",
                        columns: x => new { x.TenantId, x.ActiveTaskId, x.ActorId },
                        principalSchema: "open_loops",
                        principalTable: "tasks",
                        principalColumns: new[] { "TenantId", "Id", "OwnerPrincipalId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "proposal_audit",
                schema: "open_loops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposalVersion = table.Column<long>(type: "bigint", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    SourceKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_proposal_audit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_proposal_audit_principals_TenantId_ActorId",
                        columns: x => new { x.TenantId, x.ActorId },
                        principalSchema: "identity",
                        principalTable: "principals",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_proposal_audit_proposals_TenantId_ProposalId",
                        columns: x => new { x.TenantId, x.ProposalId },
                        principalSchema: "open_loops",
                        principalTable: "proposals",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "proposal_receipts",
                schema: "open_loops",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Intent = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ProposalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposalVersion = table.Column<long>(type: "bigint", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    TaskVersion = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_proposal_receipts", x => new { x.TenantId, x.ActorId, x.IdempotencyKey });
                    table.ForeignKey(
                        name: "FK_proposal_receipts_principals_TenantId_ActorId",
                        columns: x => new { x.TenantId, x.ActorId },
                        principalSchema: "identity",
                        principalTable: "principals",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_proposal_receipts_proposals_TenantId_ProposalId",
                        columns: x => new { x.TenantId, x.ProposalId },
                        principalSchema: "open_loops",
                        principalTable: "proposals",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_proposal_receipts_tasks_TenantId_TaskId_ActorId",
                        columns: x => new { x.TenantId, x.TaskId, x.ActorId },
                        principalSchema: "open_loops",
                        principalTable: "tasks",
                        principalColumns: new[] { "TenantId", "Id", "OwnerPrincipalId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_proposal_audit_TenantId_ActorId",
                schema: "open_loops",
                table: "proposal_audit",
                columns: new[] { "TenantId", "ActorId" });

            migrationBuilder.CreateIndex(
                name: "IX_proposal_audit_TenantId_ProposalId_OccurredAt",
                schema: "open_loops",
                table: "proposal_audit",
                columns: new[] { "TenantId", "ProposalId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_proposal_receipts_TenantId_ProposalId",
                schema: "open_loops",
                table: "proposal_receipts",
                columns: new[] { "TenantId", "ProposalId" });

            migrationBuilder.CreateIndex(
                name: "IX_proposal_receipts_TenantId_TaskId_ActorId",
                schema: "open_loops",
                table: "proposal_receipts",
                columns: new[] { "TenantId", "TaskId", "ActorId" });

            migrationBuilder.CreateIndex(
                name: "IX_proposals_TenantId_ActiveTaskId_ActorId",
                schema: "open_loops",
                table: "proposals",
                columns: new[] { "TenantId", "ActiveTaskId", "ActorId" });

            migrationBuilder.CreateIndex(
                name: "IX_proposals_TenantId_ActorAppUserId_ActorId",
                schema: "open_loops",
                table: "proposals",
                columns: new[] { "TenantId", "ActorAppUserId", "ActorId" });

            migrationBuilder.CreateIndex(
                name: "IX_proposals_TenantId_ActorId_State_ExpiresAt",
                schema: "open_loops",
                table: "proposals",
                columns: new[] { "TenantId", "ActorId", "State", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_proposals_TenantId_SourceActorId",
                schema: "open_loops",
                table: "proposals",
                columns: new[] { "TenantId", "SourceActorId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "proposal_audit",
                schema: "open_loops");

            migrationBuilder.DropTable(
                name: "proposal_receipts",
                schema: "open_loops");

            migrationBuilder.DropTable(
                name: "proposals",
                schema: "open_loops");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_tasks_TenantId_Id_OwnerPrincipalId",
                schema: "open_loops",
                table: "tasks");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_memberships_TenantId_AppUserId_PrincipalId",
                schema: "identity",
                table: "memberships");
        }
    }
}
