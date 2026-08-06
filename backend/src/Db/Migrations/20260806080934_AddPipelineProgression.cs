using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ats.Db.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Hand-adjusted from what <c>dotnet ef migrations add</c> generated (LLD §10, erd.md §5):
    /// <c>Applications.CurrentStageId</c> is added nullable, backfilled by raw SQL, then
    /// tightened to <c>NOT NULL</c> — three steps instead of EF's single
    /// <c>AddColumn(nullable:false, defaultValue: Guid.Empty)</c>, because a sentinel default
    /// would never match a real <c>Stages.Id</c> row (HLD D-4). Ordering within <c>Up()</c> is
    /// load-bearing: the Stages backfill (seeding the default 4-Stage set) must run before the
    /// Applications backfill (its subquery needs Stage rows to exist), and the
    /// <c>AlterColumn</c> to <c>NOT NULL</c> must run after both backfills.
    /// </remarks>
    public partial class AddPipelineProgression : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Steps 1-2 (erd.md §5): Stages gains SortOrder + NormalizedName. Cheap,
            // non-rebuilding ALTER TABLE ADD COLUMNs — Stages is empty in every real deployment
            // (0003 shipped no Stage-writing code path).
            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Stages",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "Stages",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            // Step 3: unique (RequisitionId, NormalizedName) index — safe, Stages is empty
            // (FR-24, HLD D-2).
            migrationBuilder.CreateIndex(
                name: "IX_Stages_RequisitionId_NormalizedName",
                table: "Stages",
                columns: new[] { "RequisitionId", "NormalizedName" },
                unique: true);

            // Step 4: new, empty StageTransitions table (FR-12, FR-14) + its FKs and indexes.
            migrationBuilder.CreateTable(
                name: "StageTransitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FromStageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FromStageName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ToStageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ToStageName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    ActorKind = table.Column<string>(type: "TEXT", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ActorDisplayLabel = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StageTransitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StageTransitions_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StageTransitions_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StageTransitions_Stages_FromStageId",
                        column: x => x.FromStageId,
                        principalTable: "Stages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StageTransitions_Stages_ToStageId",
                        column: x => x.ToStageId,
                        principalTable: "Stages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StageTransitions_ApplicationId_OccurredAtUtc",
                table: "StageTransitions",
                columns: new[] { "ApplicationId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StageTransitions_ActorUserId",
                table: "StageTransitions",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StageTransitions_FromStageId",
                table: "StageTransitions",
                column: "FromStageId");

            migrationBuilder.CreateIndex(
                name: "IX_StageTransitions_ToStageId",
                table: "StageTransitions",
                column: "ToStageId");

            // Step 5 (D-4): Applications.CurrentStageId added NULLABLE first, with no FK yet —
            // a sentinel default would never match a real Stages.Id and would risk an FK
            // violation if SQLite's foreign_keys pragma is ever turned on. A plain
            // ALTER TABLE ADD COLUMN, not a table rebuild.
            migrationBuilder.AddColumn<Guid>(
                name: "CurrentStageId",
                table: "Applications",
                type: "TEXT",
                nullable: true);

            // Step 6: Applications.IsRejected (FR-10, FR-11). Also a plain ADD COLUMN.
            migrationBuilder.AddColumn<bool>(
                name: "IsRejected",
                table: "Applications",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            // Step 7: (RequisitionId, CurrentStageId) index — the pipeline board's (FR-15,
            // NFR-1) query shape; also the FK's eventual single-column index. Plain
            // CREATE INDEX statements, not a rebuild — safe to run ahead of the FK/NOT-NULL
            // rebuild below and ahead of the raw-SQL backfill.
            migrationBuilder.CreateIndex(
                name: "IX_Applications_RequisitionId_CurrentStageId",
                table: "Applications",
                columns: new[] { "RequisitionId", "CurrentStageId" });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_CurrentStageId",
                table: "Applications",
                column: "CurrentStageId");

            // Step 8 (FR-25, AC-32, E-11) — data backfill: seed the default 4-Stage set for
            // every Requisition that currently has zero Stages. Must run before step 9 (the
            // Applications backfill's subquery needs Stage rows to exist). GUIDs are generated
            // via randomblob/hex, producing a valid 8-4-4-4-12 hex string Guid.Parse accepts.
            // The literal 4-name list here must match Stage.DefaultStageNames exactly —
            // verified by PipelineMigrationBackfillTests (R-2, HLD §9). Deviation from erd.md
            // §5's literal SQL: rewritten from `VALUES (...) AS v(Name, SortOrder)` to a
            // `UNION ALL SELECT` derived table — the column-aliased VALUES table-value
            // constructor form is not accepted by this project's SQLite version (see
            // implementation/changelog.md CP-1 Deviations; lld.md §10 Deviation Log).
            migrationBuilder.Sql(@"
INSERT INTO Stages (Id, RequisitionId, Name, NormalizedName, SortOrder)
SELECT
  lower(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' ||
        hex(randomblob(2)) || '-' || hex(randomblob(6))),
  r.Id,
  v.Name,
  upper(v.Name),
  v.SortOrder
FROM Requisitions r
CROSS JOIN (
  SELECT 'Applied' AS Name, 0 AS SortOrder
  UNION ALL SELECT 'Screening', 1
  UNION ALL SELECT 'Interview', 2
  UNION ALL SELECT 'Offer', 3
) AS v
WHERE NOT EXISTS (SELECT 1 FROM Stages s WHERE s.RequisitionId = r.Id);
");

            // Step 9 (FR-25, AC-32, E-11) — data backfill: assign every pre-existing
            // Application's CurrentStageId to its Requisition's lowest-SortOrder Stage, and
            // IsRejected = 0. By this point step 8 has already guaranteed every Requisition has
            // at least one Stage, so the subquery always resolves. No transition-log entry is
            // written for this backfill (FR-25's explicit requirement).
            migrationBuilder.Sql(@"
UPDATE Applications
SET CurrentStageId = (
      SELECT s.Id FROM Stages s
      WHERE s.RequisitionId = Applications.RequisitionId
      ORDER BY s.SortOrder ASC
      LIMIT 1
    ),
    IsRejected = 0
WHERE CurrentStageId IS NULL;
");

            // Steps 10-11 (D-4): tighten CurrentStageId to NOT NULL now that every row has a
            // real value, then add the FK. Both require SQLite table rebuilds (SQLite has no
            // native ALTER COLUMN or ADD CONSTRAINT); they are placed back-to-back with no raw
            // SQL between them and nothing after them in this migration — interleaving a raw
            // SqlOperation between a pending rebuild and its flush fails outright (SQLite
            // refuses to toggle PRAGMA foreign_keys mid-transaction), which the erd.md §5
            // sequence (FK declared alongside the nullable AddColumn in step 6) did not
            // anticipate. See implementation/changelog.md CP-1 Deviations; lld.md §10
            // Deviation Log.
            migrationBuilder.AlterColumn<Guid>(
                name: "CurrentStageId",
                table: "Applications",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_Stages_CurrentStageId",
                table: "Applications",
                column: "CurrentStageId",
                principalTable: "Stages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The two raw-SQL backfill steps (8, 9) are not reversed here — reversing them means
            // deleting the newly-seeded default Stages and nulling CurrentStageId back out,
            // which is exactly what dropping the columns/table below already accomplishes; no
            // separate Down() SQL is needed since the rows cease to exist (erd.md §5 rollback
            // plan).
            migrationBuilder.DropForeignKey(
                name: "FK_Applications_Stages_CurrentStageId",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_RequisitionId_CurrentStageId",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_CurrentStageId",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "IsRejected",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "CurrentStageId",
                table: "Applications");

            migrationBuilder.DropTable(
                name: "StageTransitions");

            migrationBuilder.DropIndex(
                name: "IX_Stages_RequisitionId_NormalizedName",
                table: "Stages");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "Stages");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "Stages");
        }
    }
}
