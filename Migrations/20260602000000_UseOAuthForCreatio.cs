using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatBridgeService.Migrations
{
    /// <inheritdoc />
    public partial class UseOAuthForCreatio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CreatioUsername", table: "CreatioInstances");
            migrationBuilder.DropColumn(name: "CreatioPassword",  table: "CreatioInstances");

            migrationBuilder.AddColumn<string>(
                name: "CreatioClientId",
                table: "CreatioInstances",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CreatioClientSecret",
                table: "CreatioInstances",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CreatioClientId",     table: "CreatioInstances");
            migrationBuilder.DropColumn(name: "CreatioClientSecret", table: "CreatioInstances");

            migrationBuilder.AddColumn<string>(
                name: "CreatioUsername",
                table: "CreatioInstances",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CreatioPassword",
                table: "CreatioInstances",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }
    }
}
