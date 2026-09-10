using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SiteVitrine.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "resources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SiteUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Avis = table.Column<string>(type: "text", nullable: false),
                    Positifs = table.Column<List<string>>(type: "text[]", nullable: false),
                    Negatifs = table.Column<List<string>>(type: "text[]", nullable: false),
                    Note = table.Column<decimal>(type: "numeric(2,1)", precision: 2, scale: 1, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resources", x => x.Id);
                    table.CheckConstraint("ck_resources_note_range", "\"Note\" >= 0 AND \"Note\" <= 5");
                });

            migrationBuilder.CreateIndex(
                name: "IX_resources_Slug",
                table: "resources",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "resources");
        }
    }
}
