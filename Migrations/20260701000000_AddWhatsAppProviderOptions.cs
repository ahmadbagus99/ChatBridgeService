using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatBridgeService.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppProviderOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WhatsAppProvider",
                table: "CreatioInstances",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "MetaCloud");

            migrationBuilder.AddColumn<string>(
                name: "KirimDevApiKey",
                table: "CreatioInstances",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "KirimDevPhoneNumberId",
                table: "CreatioInstances",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "KirimDevWebhookSecret",
                table: "CreatioInstances",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "WhatsAppProvider", table: "CreatioInstances");
            migrationBuilder.DropColumn(name: "KirimDevApiKey", table: "CreatioInstances");
            migrationBuilder.DropColumn(name: "KirimDevPhoneNumberId", table: "CreatioInstances");
            migrationBuilder.DropColumn(name: "KirimDevWebhookSecret", table: "CreatioInstances");
        }
    }
}
