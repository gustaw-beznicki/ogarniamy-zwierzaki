using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ogarniamy_zwierzaki_api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AnimalActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "animals",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "version",
                table: "animals",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_active",
                table: "animals");

            migrationBuilder.DropColumn(
                name: "version",
                table: "animals");
        }
    }
}
