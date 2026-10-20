using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetFlow.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGuidesMeetingAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MeetingGuideAttendance",
                columns: table => new
                {
                    MeetingId = table.Column<int>(type: "integer", nullable: false),
                    GuideId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingGuideAttendance", x => new { x.MeetingId, x.GuideId });
                    table.ForeignKey(
                        name: "FK_MeetingGuideAttendance_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MeetingGuideAttendance_Users_GuideId",
                        column: x => x.GuideId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingGuideAttendance_GuideId",
                table: "MeetingGuideAttendance",
                column: "GuideId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MeetingGuideAttendance");
        }
    }
}
