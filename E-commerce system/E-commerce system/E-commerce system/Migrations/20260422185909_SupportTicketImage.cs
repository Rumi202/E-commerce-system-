using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E_commerce_system.Migrations
{
    /// <inheritdoc />
    public partial class SupportTicketImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "SupportTickets",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "SupportTickets");
        }
    }
}
