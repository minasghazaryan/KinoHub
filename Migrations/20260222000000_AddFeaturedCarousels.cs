using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KinoHub.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddFeaturedCarousels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeaturedCarousels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KinopoiskId = table.Column<int>(type: "int", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    NameRu = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NameEn = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PosterUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReleaseYear = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Rating = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeaturedCarousels", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "FeaturedCarousels");
        }
    }
}
