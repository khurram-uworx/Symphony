using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Symphony.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddJobAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExternalId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    OwnerUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    WorkflowName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    RetryOfJobId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Repository = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    EncryptedRequestSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                    State = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    QueuedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    SummaryJson = table.Column<string>(type: "TEXT", nullable: true),
                    ErrorCode = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobIssues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    JobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IssueId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Identifier = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    State = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    TurnCount = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    WorkspacePath = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobIssues_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    JobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    JobIssueId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EventName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobEvents_JobIssues_JobIssueId",
                        column: x => x.JobIssueId,
                        principalTable: "JobIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_JobEvents_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobEvents_JobId_Timestamp",
                table: "JobEvents",
                columns: new[] { "JobId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_JobEvents_JobIssueId",
                table: "JobEvents",
                column: "JobIssueId");

            migrationBuilder.CreateIndex(
                name: "IX_JobIssues_JobId",
                table: "JobIssues",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_OwnerUserId_QueuedAt",
                table: "Jobs",
                columns: new[] { "OwnerUserId", "QueuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_State_QueuedAt",
                table: "Jobs",
                columns: new[] { "State", "QueuedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobEvents");

            migrationBuilder.DropTable(
                name: "JobIssues");

            migrationBuilder.DropTable(
                name: "Jobs");
        }
    }
}
