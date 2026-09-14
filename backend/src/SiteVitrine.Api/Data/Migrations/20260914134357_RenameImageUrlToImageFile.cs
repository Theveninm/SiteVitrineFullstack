using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SiteVitrine.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameImageUrlToImageFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ImageUrl",
                table: "resources",
                newName: "ImageFile");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ImageFile",
                table: "resources",
                newName: "ImageUrl");
        }
    }
}
