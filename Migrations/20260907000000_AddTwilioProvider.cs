using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatBridgeService.Migrations
{
    public partial class AddTwilioProvider : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TwilioAccountSid",
                table: "CreatioInstances",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TwilioAuthToken",
                table: "CreatioInstances",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TwilioWhatsAppFrom",
                table: "CreatioInstances",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TwilioMessagingServiceSid",
                table: "CreatioInstances",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TwilioStatusCallbackUrl",
                table: "CreatioInstances",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "TwilioAccountSid", table: "CreatioInstances");
            migrationBuilder.DropColumn(name: "TwilioAuthToken", table: "CreatioInstances");
            migrationBuilder.DropColumn(name: "TwilioWhatsAppFrom", table: "CreatioInstances");
            migrationBuilder.DropColumn(name: "TwilioMessagingServiceSid", table: "CreatioInstances");
            migrationBuilder.DropColumn(name: "TwilioStatusCallbackUrl", table: "CreatioInstances");
        }
    }
}
