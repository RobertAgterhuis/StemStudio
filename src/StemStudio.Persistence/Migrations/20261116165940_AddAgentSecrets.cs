using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StemStudio.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentSecrets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentSecrets",
                columns: table => new
                {
                    Name = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Ciphertext = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    LastFour = table.Column<string>(type: "TEXT", fixedLength: true, maxLength: 4, nullable: false),
                    PurposeVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    StoredAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentSecrets", x => x.Name);
                    table.CheckConstraint("CK_AgentSecrets_Ciphertext", "length(\"Ciphertext\") BETWEEN 1 AND 2048");
                    table.CheckConstraint("CK_AgentSecrets_LastFour", "length(\"LastFour\") = 4");
                    table.CheckConstraint("CK_AgentSecrets_Name", "length(\"Name\") BETWEEN 1 AND 40");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentSecrets");
        }
    }
}
