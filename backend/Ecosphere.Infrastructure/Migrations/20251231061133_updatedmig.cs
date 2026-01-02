using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class updatedmig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: 1L,
                columns: new[] { "ConcurrencyStamp", "TimeCreated", "TimeUpdated" },
                values: new object[] { "6b26b411-e379-4e28-a03f-5761dfd75fc8", new DateTimeOffset(new DateTime(2025, 12, 31, 6, 11, 32, 330, DateTimeKind.Unspecified).AddTicks(6950), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2025, 12, 31, 6, 11, 32, 330, DateTimeKind.Unspecified).AddTicks(7150), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: 2L,
                columns: new[] { "ConcurrencyStamp", "TimeCreated", "TimeUpdated" },
                values: new object[] { "add18600-786f-4abd-813e-75e441de6a97", new DateTimeOffset(new DateTime(2025, 12, 31, 6, 11, 32, 330, DateTimeKind.Unspecified).AddTicks(7330), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2025, 12, 31, 6, 11, 32, 330, DateTimeKind.Unspecified).AddTicks(7330), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingJoinRequests_MeetingId",
                table: "MeetingJoinRequests",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingJoinRequests_UserId",
                table: "MeetingJoinRequests",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_MeetingJoinRequests_AspNetUsers_UserId",
                table: "MeetingJoinRequests",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MeetingJoinRequests_Meetings_MeetingId",
                table: "MeetingJoinRequests",
                column: "MeetingId",
                principalTable: "Meetings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MeetingJoinRequests_AspNetUsers_UserId",
                table: "MeetingJoinRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_MeetingJoinRequests_Meetings_MeetingId",
                table: "MeetingJoinRequests");

            migrationBuilder.DropIndex(
                name: "IX_MeetingJoinRequests_MeetingId",
                table: "MeetingJoinRequests");

            migrationBuilder.DropIndex(
                name: "IX_MeetingJoinRequests_UserId",
                table: "MeetingJoinRequests");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: 1L,
                columns: new[] { "ConcurrencyStamp", "TimeCreated", "TimeUpdated" },
                values: new object[] { "4ac1ec3b-026d-432e-acb7-bb21445d8a99", new DateTimeOffset(new DateTime(2025, 12, 31, 4, 13, 33, 653, DateTimeKind.Unspecified).AddTicks(6590), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2025, 12, 31, 4, 13, 33, 653, DateTimeKind.Unspecified).AddTicks(6750), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: 2L,
                columns: new[] { "ConcurrencyStamp", "TimeCreated", "TimeUpdated" },
                values: new object[] { "a6ac78f9-4e7d-4f7f-99a9-eff304a35c6f", new DateTimeOffset(new DateTime(2025, 12, 31, 4, 13, 33, 653, DateTimeKind.Unspecified).AddTicks(6910), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2025, 12, 31, 4, 13, 33, 653, DateTimeKind.Unspecified).AddTicks(6910), new TimeSpan(0, 0, 0, 0, 0)) });
        }
    }
}
