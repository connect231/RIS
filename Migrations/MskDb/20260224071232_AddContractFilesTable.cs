using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniCP.Migrations.MskDb
{
    /// <inheritdoc />
    public partial class AddContractFilesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The table already exists in the database, so we skip creating it.
            // EF will just record this migration as applied.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TBL_VARUNA_SOZLESME_DOSYALAR");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TBL_VARUNA_SOZLESME",
                table: "TBL_VARUNA_SOZLESME");

            migrationBuilder.AlterColumn<string>(
                name: "ContractNo",
                table: "TBL_VARUNA_SOZLESME",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TBL_VARUNA_SOZLESME",
                table: "TBL_VARUNA_SOZLESME",
                column: "ContractNo");
        }
    }
}
