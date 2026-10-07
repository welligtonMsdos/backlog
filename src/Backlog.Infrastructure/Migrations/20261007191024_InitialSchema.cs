using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backlog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.CheckConstraint("ck_user_role", "role IN ('tesouraria', 'desenvolvedor', 'gestor')");
                });

            migrationBuilder.CreateTable(
                name: "auth_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auth_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_auth_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    creator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_developer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_developer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    current_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tasks", x => x.id);
                    table.CheckConstraint("ck_task_status", "current_status IN ('backlog', 'desenvolvimento', 'homologação', 'em implantação', 'finalizado')");
                    table.ForeignKey(
                        name: "FK_tasks_users_creator_id",
                        column: x => x.creator_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tasks_users_current_developer_id",
                        column: x => x.current_developer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tasks_users_target_developer_id",
                        column: x => x.target_developer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "task_stage_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    started_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ended_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_stage_entries", x => x.id);
                    table.CheckConstraint("ck_stage_dates", "ended_at IS NULL OR ended_at >= started_at");
                    table.ForeignKey(
                        name: "FK_task_stage_entries_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_stage_entries_users_ended_by_user_id",
                        column: x => x.ended_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_stage_entries_users_started_by_user_id",
                        column: x => x.started_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stage_attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    stage_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stage_attachments", x => x.id);
                    table.ForeignKey(
                        name: "FK_stage_attachments_task_stage_entries_stage_entry_id",
                        column: x => x.stage_entry_id,
                        principalTable: "task_stage_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stage_attachments_users_uploaded_by_user_id",
                        column: x => x.uploaded_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stage_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    stage_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stage_notes", x => x.id);
                    table.ForeignKey(
                        name: "FK_stage_notes_task_stage_entries_stage_entry_id",
                        column: x => x.stage_entry_id,
                        principalTable: "task_stage_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stage_notes_users_author_user_id",
                        column: x => x.author_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_auth_sessions_user_id_expires_at",
                table: "auth_sessions",
                columns: new[] { "user_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "IX_stage_attachments_stage_entry_id",
                table: "stage_attachments",
                column: "stage_entry_id");

            migrationBuilder.CreateIndex(
                name: "IX_stage_attachments_uploaded_by_user_id",
                table: "stage_attachments",
                column: "uploaded_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_stage_notes_author_user_id",
                table: "stage_notes",
                column: "author_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_stage_notes_stage_entry_id",
                table: "stage_notes",
                column: "stage_entry_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_stage_entries_ended_by_user_id",
                table: "task_stage_entries",
                column: "ended_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_stage_entries_started_by_user_id",
                table: "task_stage_entries",
                column: "started_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_stage_entries_task_id_sequence",
                table: "task_stage_entries",
                columns: new[] { "task_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_task_open_stage",
                table: "task_stage_entries",
                column: "task_id",
                unique: true,
                filter: "ended_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_creator_id_current_status",
                table: "tasks",
                columns: new[] { "creator_id", "current_status" });

            migrationBuilder.CreateIndex(
                name: "IX_tasks_current_developer_id_current_status",
                table: "tasks",
                columns: new[] { "current_developer_id", "current_status" });

            migrationBuilder.CreateIndex(
                name: "IX_tasks_target_developer_id_current_status",
                table: "tasks",
                columns: new[] { "target_developer_id", "current_status" });

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auth_sessions");

            migrationBuilder.DropTable(
                name: "stage_attachments");

            migrationBuilder.DropTable(
                name: "stage_notes");

            migrationBuilder.DropTable(
                name: "task_stage_entries");

            migrationBuilder.DropTable(
                name: "tasks");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
