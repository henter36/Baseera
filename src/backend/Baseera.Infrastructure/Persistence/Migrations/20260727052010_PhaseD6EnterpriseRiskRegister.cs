using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Baseera.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhaseD6EnterpriseRiskRegister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "RiskRecordReferenceSequence");

            migrationBuilder.CreateTable(
                name: "RiskAssessmentMatrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ScoreFormula = table.Column<int>(type: "int", nullable: false),
                    ImpactWeightingJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    EffectiveFromUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EffectiveToUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    PreviousVersionMatrixId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskAssessmentMatrices", x => x.Id);
                    table.CheckConstraint("CK_RiskAssessmentMatrices_EffectiveRange", "[EffectiveToUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]");
                    table.CheckConstraint("CK_RiskAssessmentMatrices_Version", "[Version] > 0");
                    table.CheckConstraint("CK_RiskAssessmentMatrices_WeightedRequiresWeights", "([ScoreFormula] <> 1) OR ([ImpactWeightingJson] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_RiskAssessmentMatrices_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskAssessmentMatrices_RiskAssessmentMatrices_PreviousVersionMatrixId",
                        column: x => x.PreviousVersionMatrixId,
                        principalTable: "RiskAssessmentMatrices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiskCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ParentCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskCategories_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskCategories_RiskCategories_ParentCategoryId",
                        column: x => x.ParentCategoryId,
                        principalTable: "RiskCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiskImpactDimensions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskImpactDimensions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskImpactDimensions_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiskImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImportKind = table.Column<int>(type: "int", nullable: false),
                    SourceSystem = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    FileHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TotalRows = table.Column<int>(type: "int", nullable: false),
                    ValidRows = table.Column<int>(type: "int", nullable: false),
                    RejectedRows = table.Column<int>(type: "int", nullable: false),
                    DuplicateRows = table.Column<int>(type: "int", nullable: false),
                    AppliedRows = table.Column<int>(type: "int", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskImportBatches", x => x.Id);
                    table.CheckConstraint("CK_RiskImportBatches_AppliedRows", "[AppliedRows] >= 0 AND [AppliedRows] <= [ValidRows]");
                    table.CheckConstraint("CK_RiskImportBatches_RowTotals", "[ValidRows] + [RejectedRows] + [DuplicateRows] = [TotalRows] AND [TotalRows] >= 0");
                    table.ForeignKey(
                        name: "FK_RiskImportBatches_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskImportBatches_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiskReconciliationRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ResolvedBy = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskReconciliationRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskReconciliationRecords_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskReconciliationRecords_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiskLikelihoodLevels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatrixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    NumericValue = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Criteria = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskLikelihoodLevels", x => x.Id);
                    table.CheckConstraint("CK_RiskLikelihoodLevels_NumericValue", "[NumericValue] > 0");
                    table.ForeignKey(
                        name: "FK_RiskLikelihoodLevels_RiskAssessmentMatrices_MatrixId",
                        column: x => x.MatrixId,
                        principalTable: "RiskAssessmentMatrices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiskRatingBands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatrixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    LabelAr = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    MinimumScore = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    MaximumScore = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    ResponseTimeHours = table.Column<int>(type: "int", nullable: true),
                    EscalationRequired = table.Column<bool>(type: "bit", nullable: false),
                    ReviewFrequencyDays = table.Column<int>(type: "int", nullable: true),
                    ColorToken = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskRatingBands", x => x.Id);
                    table.CheckConstraint("CK_RiskRatingBands_ScoreRange", "[MinimumScore] <= [MaximumScore]");
                    table.ForeignKey(
                        name: "FK_RiskRatingBands_RiskAssessmentMatrices_MatrixId",
                        column: x => x.MatrixId,
                        principalTable: "RiskAssessmentMatrices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiskImpactLevels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatrixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImpactDimensionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    NumericValue = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Criteria = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskImpactLevels", x => x.Id);
                    table.CheckConstraint("CK_RiskImpactLevels_NumericValue", "[NumericValue] > 0");
                    table.ForeignKey(
                        name: "FK_RiskImpactLevels_RiskAssessmentMatrices_MatrixId",
                        column: x => x.MatrixId,
                        principalTable: "RiskAssessmentMatrices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskImpactLevels_RiskImpactDimensions_ImpactDimensionId",
                        column: x => x.ImpactDimensionId,
                        principalTable: "RiskImpactDimensions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiskAssessmentImpacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiskAssessmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImpactDimensionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImpactLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RationaleAr = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EvidenceReference = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskAssessmentImpacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskAssessmentImpacts_RiskImpactDimensions_ImpactDimensionId",
                        column: x => x.ImpactDimensionId,
                        principalTable: "RiskImpactDimensions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskAssessmentImpacts_RiskImpactLevels_ImpactLevelId",
                        column: x => x.ImpactLevelId,
                        principalTable: "RiskImpactLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiskAssessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiskRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssessmentType = table.Column<int>(type: "int", nullable: false),
                    MatrixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatrixVersion = table.Column<int>(type: "int", nullable: false),
                    LikelihoodLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OverallImpactLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CalculatedScore = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    RatingBandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rationale = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AssessedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AssessedBy = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewedBy = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SupersedesAssessmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClosureChangeSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskAssessments", x => x.Id);
                    table.CheckConstraint("CK_RiskAssessments_MatrixVersion", "[MatrixVersion] > 0");
                    table.CheckConstraint("CK_RiskAssessments_ScoreNonNegative", "[CalculatedScore] >= 0");
                    table.ForeignKey(
                        name: "FK_RiskAssessments_RiskAssessmentMatrices_MatrixId",
                        column: x => x.MatrixId,
                        principalTable: "RiskAssessmentMatrices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskAssessments_RiskAssessments_SupersedesAssessmentId",
                        column: x => x.SupersedesAssessmentId,
                        principalTable: "RiskAssessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskAssessments_RiskImpactLevels_OverallImpactLevelId",
                        column: x => x.OverallImpactLevelId,
                        principalTable: "RiskImpactLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskAssessments_RiskLikelihoodLevels_LikelihoodLevelId",
                        column: x => x.LikelihoodLevelId,
                        principalTable: "RiskLikelihoodLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskAssessments_RiskRatingBands_RatingBandId",
                        column: x => x.RatingBandId,
                        principalTable: "RiskRatingBands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiskRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiskCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RiskCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiskType = table.Column<int>(type: "int", nullable: false),
                    ScopeLevel = table.Column<int>(type: "int", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FacilityUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RegionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HeadquartersOrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerWorkforceMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TreatmentStrategy = table.Column<int>(type: "int", nullable: true),
                    ConfidentialityLevel = table.Column<int>(type: "int", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    FirstIdentifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NextReviewDueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AcceptedUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedBy = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    ClosureReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReopenedCount = table.Column<int>(type: "int", nullable: false),
                    LastReopenedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastReopenReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RecurrenceKey = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CurrentInherentAssessmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrentAssessmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrentResidualAssessmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrentScore = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    CurrentRatingBandId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrentTrend = table.Column<int>(type: "int", nullable: false),
                    CurrentTrendReasonAr = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DataFreshAsOfUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskRecords", x => x.Id);
                    table.CheckConstraint("CK_RiskRecords_AcceptedRequiresUntil", "([Status] <> 7) OR ([AcceptedUntilUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_RiskRecords_ClosedRequiresClosure", "([Status] <> 9) OR ([ClosedAtUtc] IS NOT NULL AND [ClosedBy] IS NOT NULL AND [ClosureReason] IS NOT NULL)");
                    table.CheckConstraint("CK_RiskRecords_ReopenedCount", "[ReopenedCount] >= 0");
                    table.ForeignKey(
                        name: "FK_RiskRecords_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskRecords_FacilityUnits_FacilityId_FacilityUnitId",
                        columns: x => new { x.FacilityId, x.FacilityUnitId },
                        principalTable: "FacilityUnits",
                        principalColumns: new[] { "FacilityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskRecords_Organizations_HeadquartersOrganizationId",
                        column: x => x.HeadquartersOrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskRecords_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskRecords_Regions_RegionId",
                        column: x => x.RegionId,
                        principalTable: "Regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskRecords_RiskAssessments_CurrentAssessmentId",
                        column: x => x.CurrentAssessmentId,
                        principalTable: "RiskAssessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskRecords_RiskAssessments_CurrentInherentAssessmentId",
                        column: x => x.CurrentInherentAssessmentId,
                        principalTable: "RiskAssessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskRecords_RiskAssessments_CurrentResidualAssessmentId",
                        column: x => x.CurrentResidualAssessmentId,
                        principalTable: "RiskAssessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskRecords_RiskCategories_RiskCategoryId",
                        column: x => x.RiskCategoryId,
                        principalTable: "RiskCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskRecords_RiskRatingBands_CurrentRatingBandId",
                        column: x => x.CurrentRatingBandId,
                        principalTable: "RiskRatingBands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskRecords_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskRecords_WorkforceMembers_OwnerWorkforceMemberId",
                        column: x => x.OwnerWorkforceMemberId,
                        principalTable: "WorkforceMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiskControls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiskRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ControlType = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OwnerWorkforceMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ControlStatus = table.Column<int>(type: "int", nullable: false),
                    ControlEffectiveness = table.Column<int>(type: "int", nullable: false),
                    ImplementedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastTestedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NextTestDueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EvidenceRequired = table.Column<bool>(type: "bit", nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskControls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskControls_RiskRecords_RiskRecordId",
                        column: x => x.RiskRecordId,
                        principalTable: "RiskRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskControls_WorkforceMembers_OwnerWorkforceMemberId",
                        column: x => x.OwnerWorkforceMemberId,
                        principalTable: "WorkforceMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiskReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiskRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewType = table.Column<int>(type: "int", nullable: false),
                    SubjectReferenceType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    SubjectReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedBy = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AssignedReviewerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Decision = table.Column<int>(type: "int", nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RequestedAcceptedUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RequestedReviewFrequencyDays = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskReviews", x => x.Id);
                    table.CheckConstraint("CK_RiskReviews_CompletedRequiresDecision", "([Status] <> 2) OR ([Decision] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_RiskReviews_RiskRecords_RiskRecordId",
                        column: x => x.RiskRecordId,
                        principalTable: "RiskRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskReviews_Users_AssignedReviewerId",
                        column: x => x.AssignedReviewerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiskSourceLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiskRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceEntityType = table.Column<int>(type: "int", nullable: false),
                    SourceEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RelationshipType = table.Column<int>(type: "int", nullable: false),
                    AddedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AddedBy = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Rationale = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RemovalReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskSourceLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskSourceLinks_RiskRecords_RiskRecordId",
                        column: x => x.RiskRecordId,
                        principalTable: "RiskRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiskStatusHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiskRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: false),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskStatusHistories_RiskRecords_RiskRecordId",
                        column: x => x.RiskRecordId,
                        principalTable: "RiskRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiskTreatmentPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiskRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Strategy = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Objective = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    OwnerWorkforceMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    PlannedStartAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TargetLikelihoodLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetImpactLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetScore = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    ApprovalStatus = table.Column<int>(type: "int", nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskTreatmentPlans", x => x.Id);
                    table.CheckConstraint("CK_RiskTreatmentPlans_ApprovedRequiresApprover", "([ApprovalStatus] <> 2) OR ([ApprovedBy] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_RiskTreatmentPlans_RiskImpactLevels_TargetImpactLevelId",
                        column: x => x.TargetImpactLevelId,
                        principalTable: "RiskImpactLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskTreatmentPlans_RiskLikelihoodLevels_TargetLikelihoodLevelId",
                        column: x => x.TargetLikelihoodLevelId,
                        principalTable: "RiskLikelihoodLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskTreatmentPlans_RiskRecords_RiskRecordId",
                        column: x => x.RiskRecordId,
                        principalTable: "RiskRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskTreatmentPlans_WorkforceMembers_OwnerWorkforceMemberId",
                        column: x => x.OwnerWorkforceMemberId,
                        principalTable: "WorkforceMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiskTreatmentActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TreatmentPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AssignedToWorkforceMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedOrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedFacilityUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    StartAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletionEvidenceRequired = table.Column<bool>(type: "bit", nullable: false),
                    CompletionSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    BlockedReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DependencyActionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VerifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VerifiedBy = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskTreatmentActions", x => x.Id);
                    table.CheckConstraint("CK_RiskTreatmentActions_NoSelfDependency", "[DependencyActionId] IS NULL OR [DependencyActionId] <> [Id]");
                    table.ForeignKey(
                        name: "FK_RiskTreatmentActions_FacilityUnits_AssignedFacilityUnitId",
                        column: x => x.AssignedFacilityUnitId,
                        principalTable: "FacilityUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskTreatmentActions_Organizations_AssignedOrganizationId",
                        column: x => x.AssignedOrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskTreatmentActions_RiskTreatmentActions_DependencyActionId",
                        column: x => x.DependencyActionId,
                        principalTable: "RiskTreatmentActions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskTreatmentActions_RiskTreatmentPlans_TreatmentPlanId",
                        column: x => x.TreatmentPlanId,
                        principalTable: "RiskTreatmentPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskTreatmentActions_Users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskTreatmentActions_WorkforceMembers_AssignedToWorkforceMemberId",
                        column: x => x.AssignedToWorkforceMemberId,
                        principalTable: "WorkforceMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RiskAssessmentImpacts_ImpactDimensionId",
                table: "RiskAssessmentImpacts",
                column: "ImpactDimensionId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskAssessmentImpacts_ImpactLevelId",
                table: "RiskAssessmentImpacts",
                column: "ImpactLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskAssessmentImpacts_RiskAssessmentId_ImpactDimensionId",
                table: "RiskAssessmentImpacts",
                columns: new[] { "RiskAssessmentId", "ImpactDimensionId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RiskAssessmentMatrices_OrganizationId",
                table: "RiskAssessmentMatrices",
                column: "OrganizationId",
                unique: true,
                filter: "[IsDefault] = 1 AND [IsDeleted] = 0 AND [Status] = 2");

            migrationBuilder.CreateIndex(
                name: "IX_RiskAssessmentMatrices_OrganizationId_Code_Version",
                table: "RiskAssessmentMatrices",
                columns: new[] { "OrganizationId", "Code", "Version" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RiskAssessmentMatrices_PreviousVersionMatrixId",
                table: "RiskAssessmentMatrices",
                column: "PreviousVersionMatrixId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskAssessments_LikelihoodLevelId",
                table: "RiskAssessments",
                column: "LikelihoodLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskAssessments_MatrixId",
                table: "RiskAssessments",
                column: "MatrixId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskAssessments_OverallImpactLevelId",
                table: "RiskAssessments",
                column: "OverallImpactLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskAssessments_RatingBandId",
                table: "RiskAssessments",
                column: "RatingBandId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskAssessments_RiskRecordId_AssessmentType_Status_ApprovedAtUtc",
                table: "RiskAssessments",
                columns: new[] { "RiskRecordId", "AssessmentType", "Status", "ApprovedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskAssessments_SupersedesAssessmentId",
                table: "RiskAssessments",
                column: "SupersedesAssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskCategories_OrganizationId_Code",
                table: "RiskCategories",
                columns: new[] { "OrganizationId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RiskCategories_ParentCategoryId",
                table: "RiskCategories",
                column: "ParentCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskControls_OwnerWorkforceMemberId",
                table: "RiskControls",
                column: "OwnerWorkforceMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskControls_RiskRecordId_ControlStatus_NextTestDueAtUtc",
                table: "RiskControls",
                columns: new[] { "RiskRecordId", "ControlStatus", "NextTestDueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskImpactDimensions_OrganizationId_Code",
                table: "RiskImpactDimensions",
                columns: new[] { "OrganizationId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RiskImpactLevels_ImpactDimensionId",
                table: "RiskImpactLevels",
                column: "ImpactDimensionId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskImpactLevels_MatrixId_ImpactDimensionId_Code",
                table: "RiskImpactLevels",
                columns: new[] { "MatrixId", "ImpactDimensionId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RiskImportBatches_FacilityId_ImportKind_FileHash",
                table: "RiskImportBatches",
                columns: new[] { "FacilityId", "ImportKind", "FileHash" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RiskImportBatches_OrganizationId",
                table: "RiskImportBatches",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskLikelihoodLevels_MatrixId_Code",
                table: "RiskLikelihoodLevels",
                columns: new[] { "MatrixId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RiskRatingBands_MatrixId_Code",
                table: "RiskRatingBands",
                columns: new[] { "MatrixId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RiskReconciliationRecords_FacilityId_ItemKey",
                table: "RiskReconciliationRecords",
                columns: new[] { "FacilityId", "ItemKey" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RiskReconciliationRecords_OrganizationId",
                table: "RiskReconciliationRecords",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskRecords_CurrentAssessmentId",
                table: "RiskRecords",
                column: "CurrentAssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskRecords_CurrentInherentAssessmentId",
                table: "RiskRecords",
                column: "CurrentInherentAssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskRecords_CurrentRatingBandId",
                table: "RiskRecords",
                column: "CurrentRatingBandId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskRecords_CurrentResidualAssessmentId",
                table: "RiskRecords",
                column: "CurrentResidualAssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskRecords_FacilityId_FacilityUnitId",
                table: "RiskRecords",
                columns: new[] { "FacilityId", "FacilityUnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskRecords_FacilityId_NextReviewDueAtUtc",
                table: "RiskRecords",
                columns: new[] { "FacilityId", "NextReviewDueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskRecords_FacilityId_OwnerWorkforceMemberId",
                table: "RiskRecords",
                columns: new[] { "FacilityId", "OwnerWorkforceMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskRecords_FacilityId_Status_CurrentRatingBandId",
                table: "RiskRecords",
                columns: new[] { "FacilityId", "Status", "CurrentRatingBandId" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskRecords_HeadquartersOrganizationId",
                table: "RiskRecords",
                column: "HeadquartersOrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskRecords_OrganizationId_RiskCode",
                table: "RiskRecords",
                columns: new[] { "OrganizationId", "RiskCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RiskRecords_OwnerUserId",
                table: "RiskRecords",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskRecords_OwnerWorkforceMemberId",
                table: "RiskRecords",
                column: "OwnerWorkforceMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskRecords_RecurrenceKey",
                table: "RiskRecords",
                column: "RecurrenceKey");

            migrationBuilder.CreateIndex(
                name: "IX_RiskRecords_RegionId",
                table: "RiskRecords",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskRecords_RiskCategoryId",
                table: "RiskRecords",
                column: "RiskCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskReviews_AssignedReviewerId",
                table: "RiskReviews",
                column: "AssignedReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskReviews_RiskRecordId_ReviewType_Status",
                table: "RiskReviews",
                columns: new[] { "RiskRecordId", "ReviewType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskSourceLinks_RiskRecordId_SourceEntityType_SourceEntityId_RelationshipType",
                table: "RiskSourceLinks",
                columns: new[] { "RiskRecordId", "SourceEntityType", "SourceEntityId", "RelationshipType" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RiskSourceLinks_SourceEntityType_SourceEntityId",
                table: "RiskSourceLinks",
                columns: new[] { "SourceEntityType", "SourceEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskStatusHistories_RiskRecordId_ChangedAtUtc",
                table: "RiskStatusHistories",
                columns: new[] { "RiskRecordId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskTreatmentActions_AssignedFacilityUnitId",
                table: "RiskTreatmentActions",
                column: "AssignedFacilityUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskTreatmentActions_AssignedOrganizationId",
                table: "RiskTreatmentActions",
                column: "AssignedOrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskTreatmentActions_AssignedToUserId",
                table: "RiskTreatmentActions",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskTreatmentActions_AssignedToWorkforceMemberId",
                table: "RiskTreatmentActions",
                column: "AssignedToWorkforceMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskTreatmentActions_DependencyActionId",
                table: "RiskTreatmentActions",
                column: "DependencyActionId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskTreatmentActions_TreatmentPlanId_Status_DueAtUtc",
                table: "RiskTreatmentActions",
                columns: new[] { "TreatmentPlanId", "Status", "DueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskTreatmentPlans_OwnerWorkforceMemberId",
                table: "RiskTreatmentPlans",
                column: "OwnerWorkforceMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskTreatmentPlans_RiskRecordId_Status_DueAtUtc",
                table: "RiskTreatmentPlans",
                columns: new[] { "RiskRecordId", "Status", "DueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskTreatmentPlans_TargetImpactLevelId",
                table: "RiskTreatmentPlans",
                column: "TargetImpactLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskTreatmentPlans_TargetLikelihoodLevelId",
                table: "RiskTreatmentPlans",
                column: "TargetLikelihoodLevelId");

            migrationBuilder.AddForeignKey(
                name: "FK_RiskAssessmentImpacts_RiskAssessments_RiskAssessmentId",
                table: "RiskAssessmentImpacts",
                column: "RiskAssessmentId",
                principalTable: "RiskAssessments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RiskAssessments_RiskRecords_RiskRecordId",
                table: "RiskAssessments",
                column: "RiskRecordId",
                principalTable: "RiskRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RiskRecords_RiskAssessments_CurrentAssessmentId",
                table: "RiskRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_RiskRecords_RiskAssessments_CurrentInherentAssessmentId",
                table: "RiskRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_RiskRecords_RiskAssessments_CurrentResidualAssessmentId",
                table: "RiskRecords");

            migrationBuilder.DropTable(
                name: "RiskAssessmentImpacts");

            migrationBuilder.DropTable(
                name: "RiskControls");

            migrationBuilder.DropTable(
                name: "RiskImportBatches");

            migrationBuilder.DropTable(
                name: "RiskReconciliationRecords");

            migrationBuilder.DropTable(
                name: "RiskReviews");

            migrationBuilder.DropTable(
                name: "RiskSourceLinks");

            migrationBuilder.DropTable(
                name: "RiskStatusHistories");

            migrationBuilder.DropTable(
                name: "RiskTreatmentActions");

            migrationBuilder.DropTable(
                name: "RiskTreatmentPlans");

            migrationBuilder.DropTable(
                name: "RiskAssessments");

            migrationBuilder.DropTable(
                name: "RiskImpactLevels");

            migrationBuilder.DropTable(
                name: "RiskLikelihoodLevels");

            migrationBuilder.DropTable(
                name: "RiskRecords");

            migrationBuilder.DropTable(
                name: "RiskImpactDimensions");

            migrationBuilder.DropTable(
                name: "RiskCategories");

            migrationBuilder.DropTable(
                name: "RiskRatingBands");

            migrationBuilder.DropTable(
                name: "RiskAssessmentMatrices");

            migrationBuilder.DropSequence(
                name: "RiskRecordReferenceSequence");
        }
    }
}
