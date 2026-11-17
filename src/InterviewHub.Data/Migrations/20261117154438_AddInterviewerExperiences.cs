using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterviewHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInterviewerExperiences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InterviewerExperiences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InterviewerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ShowInterviewerName = table.Column<bool>(type: "bit", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShowCompanyName = table.Column<bool>(type: "bit", nullable: false),
                    InterviewDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Level = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    JobAdText = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: true),
                    JobAdUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    InterviewRating = table.Column<byte>(type: "tinyint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewerExperiences", x => x.Id);
                    table.CheckConstraint("CK_InterviewerExperience_Rating", "[InterviewRating] BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_InterviewerExperiences_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InterviewerExperiences_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InterviewerExperienceQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InterviewerExperienceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewerExperienceQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InterviewerExperienceQuestions_InterviewerExperiences_InterviewerExperienceId",
                        column: x => x.InterviewerExperienceId,
                        principalTable: "InterviewerExperiences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewerExperienceQuestions_InterviewerExperienceId",
                table: "InterviewerExperienceQuestions",
                column: "InterviewerExperienceId");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewerExperiences_AuthorId_CreatedAt",
                table: "InterviewerExperiences",
                columns: new[] { "AuthorId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewerExperiences_CompanyId_Status_InterviewDate",
                table: "InterviewerExperiences",
                columns: new[] { "CompanyId", "Status", "InterviewDate" });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewerExperiences_Status_InterviewDate",
                table: "InterviewerExperiences",
                columns: new[] { "Status", "InterviewDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InterviewerExperienceQuestions");

            migrationBuilder.DropTable(
                name: "InterviewerExperiences");
        }
    }
}
