using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ogarniamy_zwierzaki_api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DocumentReading : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "ak_document_files_document_id_id",
                table: "document_files",
                columns: new[] { "document_id", "id" });

            migrationBuilder.CreateTable(
                name: "document_reading_jobs",
                columns: table => new
                {
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    run_generation = table.Column<int>(type: "integer", nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    lease_token = table.Column<Guid>(type: "uuid", nullable: true),
                    lease_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failure_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    published_run_generation = table.Column<int>(type: "integer", nullable: true),
                    owner_retry_count = table.Column<int>(type: "integer", nullable: false),
                    owner_retry_window_started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_reading_jobs", x => x.document_id);
                    table.CheckConstraint("ck_document_reading_jobs_completed_at", "(state IN ('read', 'failed')) = (completed_at IS NOT NULL)");
                    table.CheckConstraint("ck_document_reading_jobs_counters", "run_generation >= 1 AND attempt_count >= 0 AND owner_retry_count >= 0");
                    table.CheckConstraint("ck_document_reading_jobs_failure_code", "(state = 'failed') = (failure_code IS NOT NULL) AND (failure_code IS NULL OR failure_code ~ '^[a-z0-9_]{1,64}$')");
                    table.CheckConstraint("ck_document_reading_jobs_lease", "(lease_token IS NULL) = (lease_expires_at IS NULL) AND (state = 'reading') = (lease_token IS NOT NULL)");
                    table.CheckConstraint("ck_document_reading_jobs_published", "(published_run_generation IS NULL OR published_run_generation BETWEEN 1 AND run_generation) AND (state <> 'read' OR published_run_generation IS NOT DISTINCT FROM run_generation)");
                    table.CheckConstraint("ck_document_reading_jobs_state", "state IN ('queued', 'reading', 'read', 'failed')");
                    table.ForeignKey(
                        name: "fk_document_reading_jobs_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_reading_run_files",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    run_generation = table.Column<int>(type: "integer", nullable: false),
                    document_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stage = table.Column<string>(type: "text", nullable: false),
                    selected_page_ranges = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    provider_operation_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    fetched_text = table.Column<string>(type: "text", nullable: true),
                    cleanup_state = table.Column<string>(type: "text", nullable: false),
                    cleanup_attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_cleanup_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_reading_run_files", x => x.id);
                    table.CheckConstraint("ck_document_reading_run_files_cleanup_state", "cleanup_state IN ('not_needed', 'pending', 'acknowledged')");
                    table.CheckConstraint("ck_document_reading_run_files_counters", "run_generation >= 1 AND cleanup_attempt_count >= 0");
                    table.CheckConstraint("ck_document_reading_run_files_fetched_text", "stage <> 'retrieved' OR fetched_text IS NOT NULL");
                    table.CheckConstraint("ck_document_reading_run_files_operation", "(provider_operation_id IS NULL) = (cleanup_state = 'not_needed') AND (stage <> 'submitted' OR provider_operation_id IS NOT NULL)");
                    table.CheckConstraint("ck_document_reading_run_files_stage", "stage IN ('pending', 'submitted', 'retrieved', 'failed')");
                    table.ForeignKey(
                        name: "fk_document_reading_run_files_document_files_document_id_docum",
                        columns: x => new { x.document_id, x.document_file_id },
                        principalTable: "document_files",
                        principalColumns: new[] { "document_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_document_reading_run_files_document_reading_jobs_document_id",
                        column: x => x.document_id,
                        principalTable: "document_reading_jobs",
                        principalColumn: "document_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_text_pages",
                columns: table => new
                {
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    run_generation = table.Column<int>(type: "integer", nullable: false),
                    document_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_page_number = table.Column<int>(type: "integer", nullable: false),
                    file_position = table.Column<int>(type: "integer", nullable: false),
                    extraction_method = table.Column<string>(type: "text", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_text_pages", x => new { x.document_id, x.run_generation, x.document_file_id, x.source_page_number });
                    table.CheckConstraint("ck_document_text_pages_extraction_method", "extraction_method IN ('text_layer', 'ocr')");
                    table.CheckConstraint("ck_document_text_pages_provenance", "run_generation >= 1 AND source_page_number >= 1 AND file_position >= 0 AND file_position < 10");
                    table.ForeignKey(
                        name: "fk_document_text_pages_document_files_document_id_document_fil",
                        columns: x => new { x.document_id, x.document_file_id },
                        principalTable: "document_files",
                        principalColumns: new[] { "document_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_document_text_pages_document_reading_jobs_document_id",
                        column: x => x.document_id,
                        principalTable: "document_reading_jobs",
                        principalColumn: "document_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_document_reading_jobs_due_queued",
                table: "document_reading_jobs",
                column: "due_at",
                filter: "state = 'queued'");

            migrationBuilder.CreateIndex(
                name: "ix_document_reading_jobs_lease_reading",
                table: "document_reading_jobs",
                column: "lease_expires_at",
                filter: "state = 'reading'");

            migrationBuilder.CreateIndex(
                name: "ix_document_reading_run_files_cleanup_pending",
                table: "document_reading_run_files",
                column: "next_cleanup_at",
                filter: "cleanup_state = 'pending'");

            migrationBuilder.CreateIndex(
                name: "ix_document_reading_run_files_document_id_document_file_id",
                table: "document_reading_run_files",
                columns: new[] { "document_id", "document_file_id" });

            migrationBuilder.CreateIndex(
                name: "ix_document_reading_run_files_document_id_run_generation_docum",
                table: "document_reading_run_files",
                columns: new[] { "document_id", "run_generation", "document_file_id" });

            migrationBuilder.CreateIndex(
                name: "ix_document_reading_run_files_provider_operation_id",
                table: "document_reading_run_files",
                column: "provider_operation_id",
                unique: true,
                filter: "provider_operation_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_document_text_pages_document_id_document_file_id",
                table: "document_text_pages",
                columns: new[] { "document_id", "document_file_id" });

            // Durable reading work for documents stored before this migration, so no request has to discover them.
            // Uploading documents get their job when their capture completes. Conflict-safe if a job already exists.
            migrationBuilder.Sql("""
                INSERT INTO document_reading_jobs (document_id, state, run_generation, due_at, attempt_count,
                    owner_retry_count, created_at, updated_at)
                SELECT id, 'queued', 1, now(), 0, 0, now(), now()
                FROM documents
                WHERE storage_state = 'stored'
                ON CONFLICT (document_id) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_reading_run_files");

            migrationBuilder.DropTable(
                name: "document_text_pages");

            migrationBuilder.DropTable(
                name: "document_reading_jobs");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_document_files_document_id_id",
                table: "document_files");
        }
    }
}
