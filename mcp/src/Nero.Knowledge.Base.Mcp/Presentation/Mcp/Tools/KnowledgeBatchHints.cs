namespace Nero.Knowledge.Base.Mcp.Presentation.Mcp.Tools;

internal static class KnowledgeBatchHints
{
    public const string WriteRecommendation =
        "The Markdown was written, but the SQLite index may be stale. Finish the write batch, then prefer nero_admin_finalize_batch with every returned relative path; otherwise run nero_admin_reindex once and nero_admin_validate.";

    public const string ReindexRecommendation =
        "Run nero_admin_validate next before trusting the index or committing knowledge changes.";

    public const string ValidRecommendation =
        "Validation passed. Optionally run nero_admin_check_index_consistency when filesystem/index drift is suspected.";

    public const string InvalidRecommendation =
        "Fix each actionable gap, run nero_admin_reindex if Markdown changed, then run nero_admin_validate again.";

    public const string CompliantRecommendation =
        "No active P0 compliance hits. Review pii_suspect warnings if present, then treat IsCompliant as ready.";

    public const string NonCompliantRecommendation =
        "Fix or quarantine active P0 hits (compliance_status: quarantined + compliance_reason), then re-run nero_admin_compliance_scan / nero_admin_validate.";

    public static string RecommendValidation(bool isValid, bool isCompliant)
    {
        if (isValid && isCompliant)
        {
            return "Structure and compliance both passed. Ready for reindex/commit when the batch is finished.";
        }

        if (!isValid && !isCompliant)
        {
            return "Fix structural Errors and active ComplianceGaps (or quarantine notes), then re-run nero_admin_validate.";
        }

        if (!isValid)
        {
            return InvalidRecommendation;
        }

        return NonCompliantRecommendation;
    }
}
