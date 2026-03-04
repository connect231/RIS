using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniCP.Migrations.MskDb
{
    /// <inheritdoc />
    public partial class AddEmailToKullanici : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TXTEMAIL",
                table: "TBL_KULLANICI",
                type: "varchar(256)",
                unicode: false,
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TXTEMAIL",
                table: "TBL_KULLANICI");
        }
    }
}
