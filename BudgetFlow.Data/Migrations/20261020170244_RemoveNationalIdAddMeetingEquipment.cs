using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetFlow.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveNationalIdAddMeetingEquipment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_NationalId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Participants_NationalId",
                table: "Participants");

            migrationBuilder.DropColumn(
                name: "NationalId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NationalId",
                table: "Participants");

            migrationBuilder.AddColumn<int>(
                name: "EquipmentId",
                table: "Meetings",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_EquipmentId",
                table: "Meetings",
                column: "EquipmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Meetings_Equipment_EquipmentId",
                table: "Meetings",
                column: "EquipmentId",
                principalTable: "Equipment",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Meetings_Equipment_EquipmentId",
                table: "Meetings");

            migrationBuilder.DropIndex(
                name: "IX_Meetings_EquipmentId",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "EquipmentId",
                table: "Meetings");

            migrationBuilder.AddColumn<string>(
                name: "NationalId",
                table: "Users",
                type: "character(9)",
                fixedLength: true,
                maxLength: 9,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NationalId",
                table: "Participants",
                type: "character(9)",
                fixedLength: true,
                maxLength: 9,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Users_NationalId",
                table: "Users",
                column: "NationalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Participants_NationalId",
                table: "Participants",
                column: "NationalId",
                unique: true);
        }
    }
}
