using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PartyKing.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddedUploadedImagesSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Images",
                columns: new[] { "Id", "ContentType", "DeleteAfterPresentation", "ImageName", "ImageUrl" },
                values: new object[,]
                {
                    { new Guid("01988505-d093-7975-b654-e08ccf29268f"), "IMG_2332.png", false, "image/png", "SampleImages/Uploaded/IMG_2332.png" },
                    { new Guid("01988506-1bb9-7308-a454-183052930ad6"), "IMG_7758.jpeg", false, "image/jpeg", "SampleImages/Uploaded/IMG_7758.jpeg" },
                    { new Guid("01988506-4eee-760b-aea7-9cbab5f126f6"), "IMG_7296.jpeg", false, "image/jpeg", "SampleImages/Uploaded/IMG_7296.jpeg" },
                    { new Guid("01988506-9009-7274-9825-a38a703ec929"), "IMG_6050.jpeg", false, "image/jpeg", "SampleImages/Uploaded/IMG_6050.jpeg" },
                    { new Guid("01988506-bfaf-7985-b161-d1555555a762"), "IMG_5420.jpeg", false, "image/jpeg", "SampleImages/Uploaded/IMG_5420.jpeg" },
                    { new Guid("01988507-0487-7618-bc80-bbecfec7479c"), "IMG_5565.jpeg", false, "image/jpeg", "SampleImages/Uploaded/IMG_5565.jpeg" },
                    { new Guid("01988507-2f0c-758b-b577-65ceeb9b1dbe"), "IMG_4948.png", false, "image/png", "SampleImages/Uploaded/IMG_4948.png" },
                    { new Guid("01988507-52ae-71df-9a53-918a85a61a25"), "IMG_4943.jpeg", false, "image/jpeg", "SampleImages/Uploaded/IMG_4943.jpeg" },
                    { new Guid("01988507-ac6b-73ae-a586-902f2a46f4b0"), "IMG_4965.jpeg", false, "image/jpeg", "SampleImages/Uploaded/IMG_4965.jpeg" },
                    { new Guid("01988507-f7bc-73cc-8505-12313d4419e3"), "IMG_4437.jpeg", false, "image/jpeg", "SampleImages/Uploaded/IMG_4437.jpeg" },
                    { new Guid("01988512-f059-708d-aa86-4c8edb216623"), "IMG_3380.jpeg", false, "image/jpeg", "SampleImages/Uploaded/IMG_3380.jpeg" },
                    { new Guid("01988512-f059-718b-affa-b04bdd32f330"), "IMG_3376.jpeg", false, "image/jpeg", "SampleImages/Uploaded/IMG_3376.jpeg" },
                    { new Guid("01988512-f059-73dd-ae67-29b2edf9f268"), "IMG_3375.jpeg", false, "image/jpeg", "SampleImages/Uploaded/IMG_3375.jpeg" },
                    { new Guid("01988512-f059-744e-9811-eca74c350a98"), "IMG_3377.jpeg", false, "image/jpeg", "SampleImages/Uploaded/IMG_3377.jpeg" },
                    { new Guid("01988512-f059-76f2-a1d2-bfd6c4c213df"), "IMG_3379.jpeg", false, "image/jpeg", "SampleImages/Uploaded/IMG_3379.jpeg" },
                    { new Guid("01988512-f059-7855-9c0c-52c5524614b4"), "IMG_3382.jpeg", false, "image/jpeg", "SampleImages/Uploaded/IMG_3382.jpeg" },
                    { new Guid("01988512-f059-79da-8602-c6c2c11dae72"), "IMG_3374.jpeg", false, "image/jpeg", "SampleImages/Uploaded/IMG_3374.jpeg" },
                    { new Guid("01988512-f059-7be5-a608-95918f46d42c"), "IMG_3381.jpeg", false, "image/jpeg", "SampleImages/Uploaded/IMG_3381.jpeg" },
                    { new Guid("01988512-f059-7bfd-94a8-8d8aad6cdeb2"), "IMG_3378.jpeg", false, "image/jpeg", "SampleImages/Uploaded/IMG_3378.jpeg" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988505-d093-7975-b654-e08ccf29268f"));

            migrationBuilder.DeleteData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988506-1bb9-7308-a454-183052930ad6"));

            migrationBuilder.DeleteData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988506-4eee-760b-aea7-9cbab5f126f6"));

            migrationBuilder.DeleteData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988506-9009-7274-9825-a38a703ec929"));

            migrationBuilder.DeleteData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988506-bfaf-7985-b161-d1555555a762"));

            migrationBuilder.DeleteData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988507-0487-7618-bc80-bbecfec7479c"));

            migrationBuilder.DeleteData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988507-2f0c-758b-b577-65ceeb9b1dbe"));

            migrationBuilder.DeleteData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988507-52ae-71df-9a53-918a85a61a25"));

            migrationBuilder.DeleteData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988507-ac6b-73ae-a586-902f2a46f4b0"));

            migrationBuilder.DeleteData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988507-f7bc-73cc-8505-12313d4419e3"));

            migrationBuilder.DeleteData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988512-f059-708d-aa86-4c8edb216623"));

            migrationBuilder.DeleteData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988512-f059-718b-affa-b04bdd32f330"));

            migrationBuilder.DeleteData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988512-f059-73dd-ae67-29b2edf9f268"));

            migrationBuilder.DeleteData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988512-f059-744e-9811-eca74c350a98"));

            migrationBuilder.DeleteData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988512-f059-76f2-a1d2-bfd6c4c213df"));

            migrationBuilder.DeleteData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988512-f059-7855-9c0c-52c5524614b4"));

            migrationBuilder.DeleteData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988512-f059-79da-8602-c6c2c11dae72"));

            migrationBuilder.DeleteData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988512-f059-7be5-a608-95918f46d42c"));

            migrationBuilder.DeleteData(
                table: "Images",
                keyColumn: "Id",
                keyValue: new Guid("01988512-f059-7bfd-94a8-8d8aad6cdeb2"));
        }
    }
}
