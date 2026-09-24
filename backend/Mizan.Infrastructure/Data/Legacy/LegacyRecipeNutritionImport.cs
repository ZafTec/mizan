namespace Mizan.Infrastructure.Data.Legacy;

/// <summary>
/// The v1 recipe nutrition import (issue #85). It runs once, by hand, against
/// a database holding the restored v1 tables in a legacy_v1 schema - never at
/// startup. Embedded so the integration tests run the exact file an operator
/// pipes into psql.
/// </summary>
public static class LegacyRecipeNutritionImport
{
    public static string Sql
    {
        get
        {
            using var stream = typeof(LegacyRecipeNutritionImport).Assembly
                .GetManifestResourceStream("Mizan.Legacy.ImportLegacyRecipeNutrition.sql")
                ?? throw new InvalidOperationException("The legacy nutrition import script is not embedded.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
