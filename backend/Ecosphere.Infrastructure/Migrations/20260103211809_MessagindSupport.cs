using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Ecosphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MessagindSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SenderId = table.Column<long>(type: "bigint", nullable: false),
                    SenderId1 = table.Column<long>(type: "bigint", nullable: false),
                    ReceiverId = table.Column<long>(type: "bigint", nullable: false),
                    ReceiverId1 = table.Column<long>(type: "bigint", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MeetingId = table.Column<long>(type: "bigint", nullable: true),
                    MeetingId1 = table.Column<long>(type: "bigint", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    TimeCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TimeUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Messages_AspNetUsers_ReceiverId",
                        column: x => x.ReceiverId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Messages_AspNetUsers_ReceiverId1",
                        column: x => x.ReceiverId1,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Messages_AspNetUsers_SenderId",
                        column: x => x.SenderId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Messages_AspNetUsers_SenderId1",
                        column: x => x.SenderId1,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Messages_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Messages_Meetings_MeetingId1",
                        column: x => x.MeetingId1,
                        principalTable: "Meetings",
                        principalColumn: "Id");
                });

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
                name: "IX_Messages_MeetingId_SentAt",
                table: "Messages",
                columns: new[] { "MeetingId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_MeetingId1",
                table: "Messages",
                column: "MeetingId1");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ReceiverId",
                table: "Messages",
                column: "ReceiverId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ReceiverId1",
                table: "Messages",
                column: "ReceiverId1");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_SenderId_ReceiverId_SentAt",
                table: "Messages",
                columns: new[] { "SenderId", "ReceiverId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_SenderId1",
                table: "Messages",
                column: "SenderId1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Messages");

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
    }
}
