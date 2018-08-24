using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace azure2elasticstack.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MetricLastDates",
                columns: table => new
                {
                    Key = table.Column<string>(maxLength: 500, nullable: false),
                    LastDate = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetricLastDates", x => x.Key);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MetricLastDates");
        }
    }
}
