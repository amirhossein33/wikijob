using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterviewHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionMergeAlias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MergedIntoQuestionId",
                table: "Questions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Questions_MergedIntoQuestionId",
                table: "Questions",
                column: "MergedIntoQuestionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Questions_Questions_MergedIntoQuestionId",
                table: "Questions",
                column: "MergedIntoQuestionId",
                principalTable: "Questions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Questions_Questions_MergedIntoQuestionId",
                table: "Questions");

            migrationBuilder.DropIndex(
                name: "IX_Questions_MergedIntoQuestionId",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "MergedIntoQuestionId",
                table: "Questions");
        }
    }
}
