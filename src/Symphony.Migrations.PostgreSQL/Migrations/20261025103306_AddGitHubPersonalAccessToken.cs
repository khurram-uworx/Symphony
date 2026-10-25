using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Symphony.Migrations.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddGitHubPersonalAccessToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GitHubPersonalAccessToken",
                table: "UserConfigurations",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GitHubPersonalAccessToken",
                table: "UserConfigurations");
        }
    }
}
