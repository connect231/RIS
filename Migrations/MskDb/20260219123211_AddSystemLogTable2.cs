using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SOS.Migrations.MskDb
{
    /// <inheritdoc />
    public partial class AddSystemLogTable2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TBL_SISTEM_LOGs",
                columns: table => new
                {
                    LNGKOD = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TXTKULLANICIADI = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LNGKULLANICIKOD = table.Column<int>(type: "int", nullable: true),
                    TXTISLEM = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TXTDETAY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TXTIP = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TXTMODUL = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TRHKAYIT = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TBL_SISTEM_LOGs", x => x.LNGKOD);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TBL_SISTEM_LOGs");
        }
    }
}

