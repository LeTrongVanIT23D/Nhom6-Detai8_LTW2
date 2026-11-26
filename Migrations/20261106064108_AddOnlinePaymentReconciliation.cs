using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddOnlinePaymentReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentConfirmedAt",
                table: "OtcOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentReference",
                table: "OtcOrders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "BookingFeeForfeited",
                table: "Appointments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "BookingFeeIsPaid",
                table: "Appointments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "BookingFeePaidAt",
                table: "Appointments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BookingFeePaymentMethod",
                table: "Appointments",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BookingFeePaymentReference",
                table: "Appointments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "BookingFeeRefundDue",
                table: "Appointments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "BookingFeeRefunded",
                table: "Appointments",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentConfirmedAt",
                table: "OtcOrders");

            migrationBuilder.DropColumn(
                name: "PaymentReference",
                table: "OtcOrders");

            migrationBuilder.DropColumn(
                name: "BookingFeeForfeited",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "BookingFeeIsPaid",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "BookingFeePaidAt",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "BookingFeePaymentMethod",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "BookingFeePaymentReference",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "BookingFeeRefundDue",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "BookingFeeRefunded",
                table: "Appointments");
        }
    }
}
