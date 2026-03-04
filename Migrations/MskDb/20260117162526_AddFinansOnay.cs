using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniCP.Migrations.MskDb
{
    /// <inheritdoc />
    public partial class AddFinansOnay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TBL_FINANS_ONAY",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PONumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TBL_FINANS_ONAY", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TBL_FINANS_ONAY_TBL_KULLANICI_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "TBL_KULLANICI",
                        principalColumn: "LNGKOD");
                });

            migrationBuilder.CreateIndex(
                name: "IX_TBL_FINANS_ONAY_CreatedBy",
                table: "TBL_FINANS_ONAY",
                column: "CreatedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TBL_FINANS_ONAY");
        }
    }
}
