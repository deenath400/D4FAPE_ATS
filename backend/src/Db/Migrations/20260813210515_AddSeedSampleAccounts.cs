using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Ats.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddSeedSampleAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "AspNetUsers",
                columns: new[] { "Id", "AccessFailedCount", "ConcurrencyStamp", "CreatedAtUtc", "Email", "EmailConfirmed", "FirstName", "LastName", "LockoutEnabled", "LockoutEnd", "NormalizedEmail", "NormalizedUserName", "PasswordHash", "PhoneNumber", "PhoneNumberConfirmed", "SecurityStamp", "TwoFactorEnabled", "UserName" },
                values: new object[,]
                {
                    { new Guid("d6b4122d-6228-4e08-bf29-43c3d5e23b01"), 0, "d6b4122d-6228-4e08-bf29-43c3d5e23b01", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), "sample.candidate@d4fape-ats.local", false, "Sample", "Candidate", true, null, "SAMPLE.CANDIDATE@D4FAPE-ATS.LOCAL", "SAMPLE.CANDIDATE@D4FAPE-ATS.LOCAL", "AQAAAAIAAYagAAAAEOtx0o7XBEX91ZfQpzpRzQRdjjNKgXHPuOpNiKax7WTqd/M0OzWUpco0hClbyO1bJQ==", null, false, "d6b4122d-6228-4e08-bf29-43c3d5e23c01", false, "sample.candidate@d4fape-ats.local" },
                    { new Guid("d6b4122d-6228-4e08-bf29-43c3d5e23b02"), 0, "d6b4122d-6228-4e08-bf29-43c3d5e23b02", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), "sample.recruiter@d4fape-ats.local", false, "Sample", "Recruiter", true, null, "SAMPLE.RECRUITER@D4FAPE-ATS.LOCAL", "SAMPLE.RECRUITER@D4FAPE-ATS.LOCAL", "AQAAAAIAAYagAAAAEOtx0o7XBEX91ZfQpzpRzQRdjjNKgXHPuOpNiKax7WTqd/M0OzWUpco0hClbyO1bJQ==", null, false, "d6b4122d-6228-4e08-bf29-43c3d5e23c02", false, "sample.recruiter@d4fape-ats.local" },
                    { new Guid("d6b4122d-6228-4e08-bf29-43c3d5e23b03"), 0, "d6b4122d-6228-4e08-bf29-43c3d5e23b03", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), "sample.hiringmanager@d4fape-ats.local", false, "Sample", "Hiring Manager", true, null, "SAMPLE.HIRINGMANAGER@D4FAPE-ATS.LOCAL", "SAMPLE.HIRINGMANAGER@D4FAPE-ATS.LOCAL", "AQAAAAIAAYagAAAAEOtx0o7XBEX91ZfQpzpRzQRdjjNKgXHPuOpNiKax7WTqd/M0OzWUpco0hClbyO1bJQ==", null, false, "d6b4122d-6228-4e08-bf29-43c3d5e23c03", false, "sample.hiringmanager@d4fape-ats.local" }
                });

            migrationBuilder.InsertData(
                table: "AspNetUserRoles",
                columns: new[] { "RoleId", "UserId" },
                values: new object[,]
                {
                    { new Guid("d6b4122d-6228-4e08-bf29-43c3d5e23a01"), new Guid("d6b4122d-6228-4e08-bf29-43c3d5e23b01") },
                    { new Guid("d6b4122d-6228-4e08-bf29-43c3d5e23a02"), new Guid("d6b4122d-6228-4e08-bf29-43c3d5e23b02") },
                    { new Guid("d6b4122d-6228-4e08-bf29-43c3d5e23a03"), new Guid("d6b4122d-6228-4e08-bf29-43c3d5e23b03") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetUserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("d6b4122d-6228-4e08-bf29-43c3d5e23a01"), new Guid("d6b4122d-6228-4e08-bf29-43c3d5e23b01") });

            migrationBuilder.DeleteData(
                table: "AspNetUserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("d6b4122d-6228-4e08-bf29-43c3d5e23a02"), new Guid("d6b4122d-6228-4e08-bf29-43c3d5e23b02") });

            migrationBuilder.DeleteData(
                table: "AspNetUserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("d6b4122d-6228-4e08-bf29-43c3d5e23a03"), new Guid("d6b4122d-6228-4e08-bf29-43c3d5e23b03") });

            migrationBuilder.DeleteData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: new Guid("d6b4122d-6228-4e08-bf29-43c3d5e23b01"));

            migrationBuilder.DeleteData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: new Guid("d6b4122d-6228-4e08-bf29-43c3d5e23b02"));

            migrationBuilder.DeleteData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: new Guid("d6b4122d-6228-4e08-bf29-43c3d5e23b03"));
        }
    }
}
