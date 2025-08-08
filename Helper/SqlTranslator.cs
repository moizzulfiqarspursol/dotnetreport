using System.Text.RegularExpressions;

namespace ReportBuilder.Web.Helper
{
    public static class SqlTranslator
    {
        public static string TranslateToPostgreSQL(string sqlServerQuery)
        {
            if (string.IsNullOrEmpty(sqlServerQuery))
                return sqlServerQuery;

            string translatedSql = sqlServerQuery;

            // Remove SQL Server specific hints like WITH (READUNCOMMITTED)
            translatedSql = Regex.Replace(translatedSql, @"\s+WITH\s*\([^)]+\)", "", RegexOptions.IgnoreCase);

            // Replace SQL Server brackets [table] with PostgreSQL double quotes "table"
            translatedSql = Regex.Replace(translatedSql, @"\[([^\]]+)\]", "\"$1\"", RegexOptions.IgnoreCase);

            // Replace NEWID() with RANDOM()
            translatedSql = Regex.Replace(translatedSql, @"\bNEWID\(\)", "RANDOM()", RegexOptions.IgnoreCase);

            // Replace SQL Server OFFSET...ROWS FETCH NEXT...ROWS ONLY with PostgreSQL OFFSET...LIMIT
            translatedSql = Regex.Replace(translatedSql,
                @"\s+OFFSET\s+(\d+)\s+ROWS\s+FETCH\s+NEXT\s+(\d+)\s+ROWS\s+ONLY",
                " OFFSET $1 LIMIT $2",
                RegexOptions.IgnoreCase);

            // Replace TOP clause with LIMIT
            translatedSql = Regex.Replace(translatedSql,
                @"\bSELECT\s+TOP\s+(\d+)\s+",
                "SELECT ",
                RegexOptions.IgnoreCase);

            // If we found a TOP clause, we need to add LIMIT at the end
            var topMatch = Regex.Match(sqlServerQuery, @"\bSELECT\s+TOP\s+(\d+)\s+", RegexOptions.IgnoreCase);
            if (topMatch.Success)
            {
                string limitValue = topMatch.Groups[1].Value;
                // Only add LIMIT if there isn't already an OFFSET/LIMIT clause
                if (!Regex.IsMatch(translatedSql, @"\b(OFFSET|LIMIT)\b", RegexOptions.IgnoreCase))
                {
                    translatedSql += $" LIMIT {limitValue}";
                }
            }

            // Replace DATENAME function with PostgreSQL equivalent
            translatedSql = Regex.Replace(translatedSql,
                @"\bDATENAME\s*\(\s*MONTH\s*,\s*([^)]+)\)",
                "TO_CHAR($1, 'Month')",
                RegexOptions.IgnoreCase);

            // Replace MONTH function
            translatedSql = Regex.Replace(translatedSql,
                @"\bMONTH\s*\(([^)]+)\)",
                "EXTRACT(MONTH FROM $1)",
                RegexOptions.IgnoreCase);

            // Replace CONVERT function for date formatting
            translatedSql = Regex.Replace(translatedSql,
                @"\bCONVERT\s*\(\s*VARCHAR\s*\(\s*10\s*\)\s*,\s*([^,)]+)\s*\)",
                "TO_CHAR($1, 'YYYY-MM-DD')",
                RegexOptions.IgnoreCase);

            // Replace LEN with LENGTH
            translatedSql = Regex.Replace(translatedSql, @"\bLEN\s*\(", "LENGTH(", RegexOptions.IgnoreCase);

            // Replace ISNULL with COALESCE
            translatedSql = Regex.Replace(translatedSql, @"\bISNULL\s*\(", "COALESCE(", RegexOptions.IgnoreCase);

            // Replace + string concatenation with ||
            translatedSql = Regex.Replace(translatedSql,
                @"('[^']*')\s*\+\s*('[^']*')",
                "$1 || $2",
                RegexOptions.IgnoreCase);

            return translatedSql;
        }

        public static string GetDatabaseSpecificOrderBy(string dbType, bool hasDistinct = false)
        {
            switch (dbType?.ToUpper())
            {
                case "POSTGRESQL":
                case "POSTGRES":
                    return hasDistinct ? "1" : "RANDOM()";
                case "MYSQL":
                    return hasDistinct ? "1" : "RAND()";
                case "SQLITE":
                    return hasDistinct ? "1" : "RANDOM()";
                default: // SQL Server
                    return hasDistinct ? "1" : "NEWID()";
            }
        }

        public static string GetDatabaseSpecificPaging(string dbType, int offset, int pageSize)
        {
            switch (dbType?.ToUpper())
            {
                case "POSTGRESQL":
                case "POSTGRES":
                case "MYSQL":
                case "SQLITE":
                    return $" OFFSET {offset} LIMIT {pageSize}";
                default: // SQL Server
                    return $" OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY";
            }
        }

        public static string GetDatabaseSpecificTop(string dbType, int count)
        {
            switch (dbType?.ToUpper())
            {
                case "POSTGRESQL":
                case "POSTGRES":
                case "MYSQL":
                case "SQLITE":
                    return ""; // Will use LIMIT at the end
                default: // SQL Server
                    return $"TOP {count} ";
            }
        }

        public static string AddLimitIfNeeded(string sql, string dbType, int count)
        {
            switch (dbType?.ToUpper())
            {
                case "POSTGRESQL":
                case "POSTGRES":
                case "MYSQL":
                case "SQLITE":
                    if (!Regex.IsMatch(sql, @"\b(LIMIT|OFFSET)\b", RegexOptions.IgnoreCase))
                    {
                        return sql + $" LIMIT {count}";
                    }
                    return sql;
                default: // SQL Server
                    return sql;
            }
        }
    }
}
