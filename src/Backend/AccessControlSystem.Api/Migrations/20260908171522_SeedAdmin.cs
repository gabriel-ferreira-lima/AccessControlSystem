using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccessControlSystem.Api.Migrations
{
    /// <inheritdoc />
    public partial class SeedAdmin : Migration
    {
        private static readonly Guid AdminId = Guid.Parse("cc59e349-efbd-4800-9726-f38a325e4d3d");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "operators",
                columns: new[] { "id", "email", "password", "role" },
                values: new object[]
                {
                    AdminId,
                    "admin@admin.com.br",
                    "10000.YpngI/FLvzQE2Rdn/IF7GQ==.cR+2iSPqd0IB2dKA/JoZJ4YuhpASsWM6/75RqDKLzS8=",
                    (short)1
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "operators",
                keyColumn: "id",
                keyValue: AdminId);
        }
    }
}
