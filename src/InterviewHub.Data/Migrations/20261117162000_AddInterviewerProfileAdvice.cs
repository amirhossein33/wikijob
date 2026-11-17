using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterviewHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInterviewerProfileAdvice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdviceText",
                table: "InterviewerProfiles",
                type: "nvarchar(max)",
                maxLength: 5000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "InterviewerProfiles",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "PublicId",
                table: "InterviewerProfiles",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "InterviewerProfiles",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "InterviewerProfiles",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "InterviewerPriorities",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            // Backfill any existing profiles before enforcing a unique public ID.
            migrationBuilder.Sql("""
                UPDATE p
                SET p.PublicId = NEWID(),
                    p.DisplayName = COALESCE(NULLIF(u.UserName, ''), 'Interviewer'),
                    p.Status = 'Pending',
                    p.UpdatedAt = TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')
                FROM InterviewerProfiles AS p
                INNER JOIN Users AS u ON u.Id = p.UserId;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewerProfiles_PublicId",
                table: "InterviewerProfiles",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewerProfiles_Status_UpdatedAt",
                table: "InterviewerProfiles",
                columns: new[] { "Status", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InterviewerProfiles_PublicId",
                table: "InterviewerProfiles");

            migrationBuilder.DropIndex(
                name: "IX_InterviewerProfiles_Status_UpdatedAt",
                table: "InterviewerProfiles");

            migrationBuilder.DropColumn(
                name: "AdviceText",
                table: "InterviewerProfiles");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "InterviewerProfiles");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "InterviewerProfiles");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "InterviewerProfiles");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "InterviewerProfiles");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "InterviewerPriorities");
        }
    }
}
