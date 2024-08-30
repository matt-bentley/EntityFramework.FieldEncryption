using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EntityFramework.FieldEncryption.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EncryptedFinancialEntities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TaxIdentificationNumber = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    IncorportationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amounts = table.Column<byte[]>(type: "varbinary(max)", unicode: false, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncryptedFinancialEntities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinancialEntities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TaxIdentificationNumber = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    IncorportationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amounts = table.Column<string>(type: "varchar(max)", unicode: false, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialEntities", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EncryptedFinancialEntities");

            migrationBuilder.DropTable(
                name: "FinancialEntities");
        }
    }
}
