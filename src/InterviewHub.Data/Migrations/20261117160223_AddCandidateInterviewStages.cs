using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterviewHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateInterviewStages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InterviewStages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InterviewExperienceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewStages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InterviewStages_InterviewExperiences_InterviewExperienceId",
                        column: x => x.InterviewExperienceId,
                        principalTable: "InterviewExperiences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InterviewStageQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InterviewStageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    PerceivedDifficulty = table.Column<byte>(type: "tinyint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewStageQuestions", x => x.Id);
                    table.CheckConstraint("CK_InterviewStageQuestion_Difficulty", "[PerceivedDifficulty] IS NULL OR [PerceivedDifficulty] BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_InterviewStageQuestions_InterviewStages_InterviewStageId",
                        column: x => x.InterviewStageId,
                        principalTable: "InterviewStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InterviewStageQuestions_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewStageQuestions_InterviewStageId_Order",
                table: "InterviewStageQuestions",
                columns: new[] { "InterviewStageId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewStageQuestions_QuestionId",
                table: "InterviewStageQuestions",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewStages_InterviewExperienceId_Order",
                table: "InterviewStages",
                columns: new[] { "InterviewExperienceId", "Order" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InterviewStageQuestions");

            migrationBuilder.DropTable(
                name: "InterviewStages");
        }
    }
}
