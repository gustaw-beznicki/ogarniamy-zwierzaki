using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace ogarniamy_zwierzaki_api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DocumentEmbedding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "document_chunks",
                columns: table => new
                {
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chunk_index = table.Column<int>(type: "integer", nullable: false),
                    run_generation = table.Column<int>(type: "integer", nullable: false),
                    document_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_position = table.Column<int>(type: "integer", nullable: false),
                    source_page_number = table.Column<int>(type: "integer", nullable: false),
                    start_offset = table.Column<int>(type: "integer", nullable: false),
                    end_offset = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    embedding = table.Column<Vector>(type: "vector(1536)", nullable: false),
                    embedding_version = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_chunks", x => new { x.document_id, x.chunk_index });
                    table.CheckConstraint("ck_document_chunks_provenance", "chunk_index >= 0 AND run_generation >= 1 AND source_page_number >= 1 AND file_position >= 0 AND start_offset >= 0 AND end_offset > start_offset");
                    table.ForeignKey(
                        name: "fk_document_chunks_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_embedding_jobs",
                columns: table => new
                {
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    lease_token = table.Column<Guid>(type: "uuid", nullable: true),
                    lease_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failure_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    embedded_run_generation = table.Column<int>(type: "integer", nullable: true),
                    embedded_animal_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_embedding_jobs", x => x.document_id);
                    table.CheckConstraint("ck_document_embedding_jobs_counters", "attempt_count >= 0 AND (embedded_run_generation IS NULL OR embedded_run_generation >= 1)");
                    table.CheckConstraint("ck_document_embedding_jobs_embedded", "state <> 'embedded' OR (embedded_run_generation IS NOT NULL AND embedded_animal_name IS NOT NULL)");
                    table.CheckConstraint("ck_document_embedding_jobs_failure_code", "(state = 'failed') = (failure_code IS NOT NULL) AND (failure_code IS NULL OR failure_code ~ '^[a-z0-9_]{1,64}$')");
                    table.CheckConstraint("ck_document_embedding_jobs_lease", "(lease_token IS NULL) = (lease_expires_at IS NULL) AND (state = 'embedding') = (lease_token IS NOT NULL)");
                    table.CheckConstraint("ck_document_embedding_jobs_state", "state IN ('queued', 'embedding', 'embedded', 'failed')");
                    table.ForeignKey(
                        name: "fk_document_embedding_jobs_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_document_chunks_document_id",
                table: "document_chunks",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_embedding_jobs_due_queued",
                table: "document_embedding_jobs",
                column: "due_at",
                filter: "state = 'queued'");

            migrationBuilder.CreateIndex(
                name: "ix_document_embedding_jobs_lease_embedding",
                table: "document_embedding_jobs",
                column: "lease_expires_at",
                filter: "state = 'embedding'");

            // Embedding work for documents already read before this migration, so no request has to discover them.
            // Documents whose reading is still queued, running or failed get their job when their text is published.
            // Conflict-safe if a job already exists.
            migrationBuilder.Sql("""
                INSERT INTO document_embedding_jobs (document_id, state, due_at, attempt_count, requested_at, created_at,
                    updated_at)
                SELECT document_id, 'queued', now(), 0, now(), now(), now()
                FROM document_reading_jobs
                WHERE state = 'read'
                ON CONFLICT (document_id) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_chunks");

            migrationBuilder.DropTable(
                name: "document_embedding_jobs");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
