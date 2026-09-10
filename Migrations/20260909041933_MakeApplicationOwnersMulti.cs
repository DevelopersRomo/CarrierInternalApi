using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InternalCarrierApp.API.Migrations
{
    /// <inheritdoc />
    public partial class MakeApplicationOwnersMulti : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationOwner",
                columns: table => new
                {
                    ApplicationId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationOwner", x => new { x.ApplicationId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ApplicationOwner_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApplicationOwner_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationOwner_UserId",
                table: "ApplicationOwner",
                column: "UserId");

            migrationBuilder.Sql("INSERT INTO ApplicationOwner (ApplicationId, UserId) SELECT Id, OwnerId FROM Applications");

            migrationBuilder.DropForeignKey(
                name: "FK_Applications_AspNetUsers_OwnerId",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_OwnerId",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Applications");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Applications",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE applications SET OwnerId = owners.UserId FROM Applications applications INNER JOIN (SELECT ApplicationId, MIN(UserId) AS UserId FROM ApplicationOwner GROUP BY ApplicationId) owners ON applications.Id = owners.ApplicationId");

            migrationBuilder.DropTable(
                name: "ApplicationOwner");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_OwnerId",
                table: "Applications",
                column: "OwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_AspNetUsers_OwnerId",
                table: "Applications",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
