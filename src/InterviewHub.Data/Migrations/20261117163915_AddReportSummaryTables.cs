using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterviewHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReportSummaryTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompanyDailySummaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Level = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ExperienceCount = table.Column<int>(type: "int", nullable: false),
                    PassedCount = table.Column<int>(type: "int", nullable: false),
                    RejectedCount = table.Column<int>(type: "int", nullable: false),
                    ConductRatingCount = table.Column<int>(type: "int", nullable: false),
                    ConductRatingSum = table.Column<int>(type: "int", nullable: false),
                    AdAlignmentRatingCount = table.Column<int>(type: "int", nullable: false),
                    AdAlignmentRatingSum = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyDailySummaries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InterviewerPrioritySummaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Skill = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    NormalizedSkill = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ProfileCount = table.Column<int>(type: "int", nullable: false),
                    ImportanceSum = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewerPrioritySummaries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuestionDailySummaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Level = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExperienceCount = table.Column<int>(type: "int", nullable: false),
                    DifficultyCount = table.Column<int>(type: "int", nullable: false),
                    DifficultySum = table.Column<int>(type: "int", nullable: false),
                    Difficulty1Count = table.Column<int>(type: "int", nullable: false),
                    Difficulty2Count = table.Column<int>(type: "int", nullable: false),
                    Difficulty3Count = table.Column<int>(type: "int", nullable: false),
                    Difficulty4Count = table.Column<int>(type: "int", nullable: false),
                    Difficulty5Count = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionDailySummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionDailySummaries_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportRefreshStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LastRefreshedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportRefreshStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyDailySummaries_Day_CompanyId_Level",
                table: "CompanyDailySummaries",
                columns: new[] { "Day", "CompanyId", "Level" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewerPrioritySummaries_NormalizedSkill",
                table: "InterviewerPrioritySummaries",
                column: "NormalizedSkill",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuestionDailySummaries_Day_CompanyId_Level_QuestionId",
                table: "QuestionDailySummaries",
                columns: new[] { "Day", "CompanyId", "Level", "QuestionId" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionDailySummaries_QuestionId",
                table: "QuestionDailySummaries",
                column: "QuestionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyDailySummaries");

            migrationBuilder.DropTable(
                name: "InterviewerPrioritySummaries");

            migrationBuilder.DropTable(
                name: "QuestionDailySummaries");

            migrationBuilder.DropTable(
                name: "ReportRefreshStates");
        }
    }
}
