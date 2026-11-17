using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterviewHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class LinkInterviewerQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "QuestionId",
                table: "InterviewerExperienceQuestions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewerExperienceQuestions_QuestionId",
                table: "InterviewerExperienceQuestions",
                column: "QuestionId");

            migrationBuilder.AddForeignKey(
                name: "FK_InterviewerExperienceQuestions_Questions_QuestionId",
                table: "InterviewerExperienceQuestions",
                column: "QuestionId",
                principalTable: "Questions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InterviewerExperienceQuestions_Questions_QuestionId",
                table: "InterviewerExperienceQuestions");

            migrationBuilder.DropIndex(
                name: "IX_InterviewerExperienceQuestions_QuestionId",
                table: "InterviewerExperienceQuestions");

            migrationBuilder.DropColumn(
                name: "QuestionId",
                table: "InterviewerExperienceQuestions");
        }
    }
}
