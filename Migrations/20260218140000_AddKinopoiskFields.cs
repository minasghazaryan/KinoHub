using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KinoHub.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddKinopoiskFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "KinopoiskId",
                table: "Movies",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Rating",
                table: "Movies",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameRu",
                table: "Movies",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KinopoiskId",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "NameRu",
                table: "Movies");
        }
    }
}
