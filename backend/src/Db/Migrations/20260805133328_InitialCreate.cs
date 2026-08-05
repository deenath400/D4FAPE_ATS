namespace Ats.Db.Migrations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260805133328_InitialCreate")]
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
