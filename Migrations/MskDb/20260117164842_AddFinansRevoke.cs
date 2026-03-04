using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniCP.Migrations.MskDb
{
    /// <inheritdoc />
    public partial class AddFinansRevoke : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRevoked",
                table: "TBL_FINANS_ONAY",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RevokedBy",
                table: "TBL_FINANS_ONAY",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RevokedDate",
                table: "TBL_FINANS_ONAY",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TBL_FINANS_ONAY_RevokedBy",
                table: "TBL_FINANS_ONAY",
                column: "RevokedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_TBL_FINANS_ONAY_TBL_KULLANICI_RevokedBy",
                table: "TBL_FINANS_ONAY",
                column: "RevokedBy",
                principalTable: "TBL_KULLANICI",
                principalColumn: "LNGKOD");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TBL_FINANS_ONAY_TBL_KULLANICI_RevokedBy",
                table: "TBL_FINANS_ONAY");

            migrationBuilder.DropIndex(
                name: "IX_TBL_FINANS_ONAY_RevokedBy",
                table: "TBL_FINANS_ONAY");

            migrationBuilder.DropColumn(
                name: "IsRevoked",
                table: "TBL_FINANS_ONAY");

            migrationBuilder.DropColumn(
                name: "RevokedBy",
                table: "TBL_FINANS_ONAY");

            migrationBuilder.DropColumn(
                name: "RevokedDate",
                table: "TBL_FINANS_ONAY");
        }
    }
}
