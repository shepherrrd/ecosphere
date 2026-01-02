using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConnectionModeToMeeting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: 1L,
                columns: new[] { "ConcurrencyStamp", "TimeCreated", "TimeUpdated" },
                values: new object[] { "a20d6f4b-71b1-4c87-9b44-880cffeff254", new DateTimeOffset(new DateTime(2025, 12, 31, 11, 28, 0, 239, DateTimeKind.Unspecified).AddTicks(5310), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2025, 12, 31, 11, 28, 0, 239, DateTimeKind.Unspecified).AddTicks(5520), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: 2L,
                columns: new[] { "ConcurrencyStamp", "TimeCreated", "TimeUpdated" },
                values: new object[] { "7d4855fb-ee7b-4afb-8d6c-4f4119d77b77", new DateTimeOffset(new DateTime(2025, 12, 31, 11, 28, 0, 239, DateTimeKind.Unspecified).AddTicks(5690), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2025, 12, 31, 11, 28, 0, 239, DateTimeKind.Unspecified).AddTicks(5690), new TimeSpan(0, 0, 0, 0, 0)) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: 1L,
                columns: new[] { "ConcurrencyStamp", "TimeCreated", "TimeUpdated" },
                values: new object[] { "1be18ba9-a85b-4e2f-ab4e-6a00a3de374e", new DateTimeOffset(new DateTime(2025, 12, 31, 6, 30, 43, 618, DateTimeKind.Unspecified).AddTicks(6330), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2025, 12, 31, 6, 30, 43, 618, DateTimeKind.Unspecified).AddTicks(6510), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: 2L,
                columns: new[] { "ConcurrencyStamp", "TimeCreated", "TimeUpdated" },
                values: new object[] { "1f8b02c4-010a-4a79-b314-bea9f2345045", new DateTimeOffset(new DateTime(2025, 12, 31, 6, 30, 43, 618, DateTimeKind.Unspecified).AddTicks(6670), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2025, 12, 31, 6, 30, 43, 618, DateTimeKind.Unspecified).AddTicks(6670), new TimeSpan(0, 0, 0, 0, 0)) });
        }
    }
}
