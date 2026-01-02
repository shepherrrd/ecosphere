using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Ecosphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class turncreds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TurnCredentials",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "text", nullable: false),
                    Credential = table.Column<string>(type: "text", nullable: false),
                    Urls = table.Column<List<string>>(type: "text[]", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TimeCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TimeUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TurnCredentials", x => x.Id);
                });

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TurnCredentials");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: 1L,
                columns: new[] { "ConcurrencyStamp", "TimeCreated", "TimeUpdated" },
                values: new object[] { "4dc3c893-1598-43a7-b3cd-7ff97dc76d20", new DateTimeOffset(new DateTime(2025, 12, 31, 3, 26, 53, 451, DateTimeKind.Unspecified).AddTicks(7600), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2025, 12, 31, 3, 26, 53, 451, DateTimeKind.Unspecified).AddTicks(7780), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: 2L,
                columns: new[] { "ConcurrencyStamp", "TimeCreated", "TimeUpdated" },
                values: new object[] { "ac18d2f8-e75c-4ff7-9bfe-9d7d7a2afb5b", new DateTimeOffset(new DateTime(2025, 12, 31, 3, 26, 53, 451, DateTimeKind.Unspecified).AddTicks(7930), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2025, 12, 31, 3, 26, 53, 451, DateTimeKind.Unspecified).AddTicks(7930), new TimeSpan(0, 0, 0, 0, 0)) });
        }
    }
}
