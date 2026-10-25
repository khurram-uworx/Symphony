using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Symphony.Migrations.PostgreSQL.Migrations
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
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "JobTelemetry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Signal = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TraceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SpanId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ParentSpanId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Severity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    PayloadJson = table.Column<string>(type: "text", nullable: true)
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
