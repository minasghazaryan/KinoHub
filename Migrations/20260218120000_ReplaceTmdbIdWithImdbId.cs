using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KinoHub.Web.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceTmdbIdWithImdbId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImdbId",
                table: "Movies",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(@"
                UPDATE Movies SET ImdbId = 'legacy-' + CAST(Id AS NVARCHAR(20)) WHERE 1=1;
            ");

            migrationBuilder.DropColumn(
                name: "TmdbId",
                table: "Movies");

            migrationBuilder.CreateIndex(
                name: "IX_Movies_ImdbId",
                table: "Movies",
                column: "ImdbId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Movies_ImdbId",
                table: "Movies");

            migrationBuilder.AddColumn<int>(
                name: "TmdbId",
                table: "Movies",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.DropColumn(
                name: "ImdbId",
                table: "Movies");
        }
    }
}
