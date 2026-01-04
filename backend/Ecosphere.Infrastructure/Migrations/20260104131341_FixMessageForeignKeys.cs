using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixMessageForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_AspNetUsers_ReceiverId1",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_AspNetUsers_SenderId1",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Meetings_MeetingId1",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_MeetingId1",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_ReceiverId1",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_SenderId1",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "MeetingId1",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ReceiverId1",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "SenderId1",
                table: "Messages");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: 1L,
                columns: new[] { "ConcurrencyStamp", "TimeCreated", "TimeUpdated" },
                values: new object[] { "35cf2047-7915-4d29-b8b0-f67218df7eab", new DateTimeOffset(new DateTime(2026, 1, 4, 13, 13, 41, 495, DateTimeKind.Unspecified).AddTicks(8090), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 1, 4, 13, 13, 41, 495, DateTimeKind.Unspecified).AddTicks(8250), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: 2L,
                columns: new[] { "ConcurrencyStamp", "TimeCreated", "TimeUpdated" },
                values: new object[] { "297598dd-c4ed-49e6-ab00-a38d10e2af0f", new DateTimeOffset(new DateTime(2026, 1, 4, 13, 13, 41, 495, DateTimeKind.Unspecified).AddTicks(8410), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 1, 4, 13, 13, 41, 495, DateTimeKind.Unspecified).AddTicks(8410), new TimeSpan(0, 0, 0, 0, 0)) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "MeetingId1",
                table: "Messages",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ReceiverId1",
                table: "Messages",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "SenderId1",
                table: "Messages",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: 1L,
                columns: new[] { "ConcurrencyStamp", "TimeCreated", "TimeUpdated" },
                values: new object[] { "f6e64f02-efc8-4abd-9729-9ae208481c2e", new DateTimeOffset(new DateTime(2026, 1, 3, 21, 18, 9, 454, DateTimeKind.Unspecified).AddTicks(4420), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 1, 3, 21, 18, 9, 454, DateTimeKind.Unspecified).AddTicks(4580), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: 2L,
                columns: new[] { "ConcurrencyStamp", "TimeCreated", "TimeUpdated" },
                values: new object[] { "14229559-2c4b-4dd5-8f57-8f0da624f29b", new DateTimeOffset(new DateTime(2026, 1, 3, 21, 18, 9, 454, DateTimeKind.Unspecified).AddTicks(4720), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 1, 3, 21, 18, 9, 454, DateTimeKind.Unspecified).AddTicks(4720), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_MeetingId1",
                table: "Messages",
                column: "MeetingId1");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ReceiverId1",
                table: "Messages",
                column: "ReceiverId1");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_SenderId1",
                table: "Messages",
                column: "SenderId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_AspNetUsers_ReceiverId1",
                table: "Messages",
                column: "ReceiverId1",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_AspNetUsers_SenderId1",
                table: "Messages",
                column: "SenderId1",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Meetings_MeetingId1",
                table: "Messages",
                column: "MeetingId1",
                principalTable: "Meetings",
                principalColumn: "Id");
        }
    }
}
