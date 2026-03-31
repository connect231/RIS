using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SOS.Migrations.MskDb
{
    /// <inheritdoc />
    public partial class AddLNGORTAKFIRMAKOD : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Kolon DatabaseMigrationService tarafından uygulama başlangıcında ekleniyor.
            // EF Core bu migration'ı sadece schema takibi için kaydeder.
            migrationBuilder.AddColumn<int>(
                name: "LNGORTAKFIRMAKOD",
                table: "TBL_VARUNA_SOZLESME",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LNGORTAKFIRMAKOD",
                table: "TBL_VARUNA_SOZLESME");
        }
    }
}

