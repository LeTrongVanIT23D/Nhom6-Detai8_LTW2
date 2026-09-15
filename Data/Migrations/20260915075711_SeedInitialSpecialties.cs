using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace StudentManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedInitialSpecialties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Specialties",
                columns: new[] { "Id", "Description", "IsActive", "Name" },
                values: new object[,]
                {
                    { 1, "Khám và điều trị các bệnh lý nội khoa.", true, "Nội khoa" },
                    { 2, "Chăm sóc sức khỏe trẻ em.", true, "Nhi khoa" },
                    { 3, "Khám và điều trị các bệnh lý răng hàm mặt.", true, "Răng hàm mặt" },
                    { 4, "Khám và theo dõi sức khỏe tim mạch.", true, "Tim mạch" },
                    { 5, "Khám và điều trị các bệnh lý da liễu.", true, "Da liễu" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Specialties",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Specialties",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Specialties",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Specialties",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Specialties",
                keyColumn: "Id",
                keyValue: 5);
        }
    }
}
