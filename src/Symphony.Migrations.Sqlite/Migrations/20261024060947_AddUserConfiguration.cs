using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Symphony.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddUserConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserConfigurations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    TrackerKind = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TrackerEndpoint = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    TrackerApiKey = table.Column<string>(type: "TEXT", nullable: false),
                    TrackerProjectSlug = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    TrackerActiveStates = table.Column<string>(type: "TEXT", nullable: true),
                    TrackerTerminalStates = table.Column<string>(type: "TEXT", nullable: true),
                    WorkspaceRoot = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    HookAfterCreate = table.Column<string>(type: "TEXT", nullable: true),
                    HookBeforeRun = table.Column<string>(type: "TEXT", nullable: true),
                    HookAfterRun = table.Column<string>(type: "TEXT", nullable: true),
                    HookBeforeRemove = table.Column<string>(type: "TEXT", nullable: true),
                    HookTimeoutMs = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxConcurrentAgents = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxRetryBackoffMs = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxTurns = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxConcurrentAgentsByState = table.Column<string>(type: "TEXT", nullable: true),
                    CopilotProvider = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CopilotBaseUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    CopilotModelId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CopilotApiKey = table.Column<string>(type: "TEXT", nullable: true),
                    CopilotWireApi = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    DefaultWorkflowName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserConfigurations_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserConfigurations_UserId",
                table: "UserConfigurations",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserConfigurations");
        }
    }
}
