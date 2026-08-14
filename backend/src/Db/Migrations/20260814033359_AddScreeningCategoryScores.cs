using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ats.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddScreeningCategoryScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EducationScore",
                table: "ScreeningReports",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExperienceScore",
                table: "ScreeningReports",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SkillsScore",
                table: "ScreeningReports",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EducationScore",
                table: "ScreeningReports");

            migrationBuilder.DropColumn(
                name: "ExperienceScore",
                table: "ScreeningReports");

            migrationBuilder.DropColumn(
                name: "SkillsScore",
                table: "ScreeningReports");
        }
    }
}
