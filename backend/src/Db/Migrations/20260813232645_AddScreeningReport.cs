using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ats.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddScreeningReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScreeningReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Score = table.Column<int>(type: "INTEGER", nullable: false),
                    Recommendation = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Strengths = table.Column<string>(type: "TEXT", nullable: false),
                    Concerns = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    FailureReason = table.Column<string>(type: "TEXT", nullable: true),
                    EvaluatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScreeningReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScreeningReports_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningReports_ApplicationId",
                table: "ScreeningReports",
                column: "ApplicationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScreeningReports");
        }
    }
}
