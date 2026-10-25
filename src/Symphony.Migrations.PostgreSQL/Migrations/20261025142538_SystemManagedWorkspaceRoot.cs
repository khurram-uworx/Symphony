using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Symphony.Migrations.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class SystemManagedWorkspaceRoot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WorkspaceRoot",
                table: "UserConfigurations");

            migrationBuilder.CreateTable(
                name: "RepositoryWorkspaces",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Repository = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryWorkspaces", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryWorkspaces_OwnerUserId_Repository",
                table: "RepositoryWorkspaces",
                columns: new[] { "OwnerUserId", "Repository" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepositoryWorkspaces");

            migrationBuilder.AddColumn<string>(
                name: "WorkspaceRoot",
                table: "UserConfigurations",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");
        }
    }
}
