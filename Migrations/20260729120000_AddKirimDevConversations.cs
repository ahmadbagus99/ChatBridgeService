using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatBridgeService.Migrations
{
    /// <inheritdoc />
    public partial class AddKirimDevConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KirimDevConversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ConversationId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KirimDevConversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KirimDevConversations_CreatioInstances_InstanceId",
                        column: x => x.InstanceId,
                        principalTable: "CreatioInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KirimDevConversations_InstanceId_PhoneNumber",
                table: "KirimDevConversations",
                columns: new[] { "InstanceId", "PhoneNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KirimDevConversations");
        }
    }
}
