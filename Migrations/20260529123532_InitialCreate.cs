using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatBridgeService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CreatioInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ApiKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatioBaseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatioUsername = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatioPassword = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    MetaAccessToken = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    MetaPhoneNumberId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MetaVerifyToken = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatioInstances", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreatioInstances_ApiKey",
                table: "CreatioInstances",
                column: "ApiKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreatioInstances");
        }
    }
}
