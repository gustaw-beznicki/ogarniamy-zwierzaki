using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ogarniamy_zwierzaki_api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DocumentOriginals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "last_capture_animal_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    animal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_date = table.Column<DateOnly>(type: "date", nullable: false),
                    capture_time_zone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    storage_state = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documents", x => x.id);
                    table.CheckConstraint("ck_documents_capture_time_zone", "char_length(capture_time_zone) > 0");
                    table.CheckConstraint("ck_documents_storage_state", "storage_state IN ('uploading', 'stored')");
                    table.CheckConstraint("ck_documents_uploaded_at", "(storage_state = 'stored') = (uploaded_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_documents_animals_animal_id",
                        column: x => x.animal_id,
                        principalTable: "animals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_files",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    original_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    byte_length = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    blob_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    receipt_length = table.Column<long>(type: "bigint", nullable: true),
                    receipt_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    receipt_etag = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_files", x => x.id);
                    table.CheckConstraint("ck_document_files_blob_key", "blob_key = 'documents/' || document_id::text || '/' || id::text");
                    table.CheckConstraint("ck_document_files_byte_length", "byte_length BETWEEN 1 AND 10485760");
                    table.CheckConstraint("ck_document_files_content_type", "content_type IN ('application/pdf', 'image/jpeg', 'image/png')");
                    table.CheckConstraint("ck_document_files_original_name", "char_length(original_name) > 0");
                    table.CheckConstraint("ck_document_files_position", "position >= 0 AND position < 10 AND (content_type <> 'application/pdf' OR position = 0)");
                    table.CheckConstraint("ck_document_files_receipt", "(receipt_length IS NULL AND receipt_sha256 IS NULL AND receipt_etag IS NULL) OR (receipt_length = byte_length AND receipt_sha256 = sha256 AND receipt_etag IS NOT NULL)");
                    table.CheckConstraint("ck_document_files_sha256", "sha256 ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "fk_document_files_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_last_capture_animal_id",
                table: "users",
                column: "last_capture_animal_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_files_blob_key",
                table: "document_files",
                column: "blob_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_files_document_id_position",
                table: "document_files",
                columns: new[] { "document_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_documents_animal_id_storage_state_event_date_uploaded_at_id",
                table: "documents",
                columns: new[] { "animal_id", "storage_state", "event_date", "uploaded_at", "id" },
                descending: new[] { false, false, true, true, true });

            migrationBuilder.AddForeignKey(
                name: "fk_users_animals_last_capture_animal_id",
                table: "users",
                column: "last_capture_animal_id",
                principalTable: "animals",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_users_animals_last_capture_animal_id",
                table: "users");

            migrationBuilder.DropTable(
                name: "document_files");

            migrationBuilder.DropTable(
                name: "documents");

            migrationBuilder.DropIndex(
                name: "ix_users_last_capture_animal_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "last_capture_animal_id",
                table: "users");
        }
    }
}
