using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Symphony.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddJobTelemetryAndTelemetryContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "TelemetryContentEnabled",
                table: "UserConfigurations",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "JobTelemetry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    JobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Signal = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TraceId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    SpanId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ParentSpanId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    DurationMs = table.Column<long>(type: "INTEGER", nullable: true),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobTelemetry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobTelemetry_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobTelemetry_JobId_Timestamp",
                table: "JobTelemetry",
                columns: new[] { "JobId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobTelemetry");

            migrationBuilder.DropColumn(
                name: "TelemetryContentEnabled",
                table: "UserConfigurations");
        }
    }
}
