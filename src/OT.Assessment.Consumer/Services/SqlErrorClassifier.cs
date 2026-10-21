using Microsoft.Data.SqlClient;

namespace OT.Assessment.Consumer.Services
{
    /// <summary>
    /// Decides whether a failure while saving can ever succeed. A data error (the bet itself cannot be
    /// stored) will fail again, so its message goes to the dead-letter queue; any other error (timeout,
    /// deadlock, database down) may work later, so the Consumer retries and puts the messages back.
    /// </summary>
    public static class SqlErrorClassifier
    {
        // SQL Server error numbers that mean "this data cannot be stored" (retrying will not help).
        private static readonly int[] DataErrorNumbers =
            { 220, 232, 242, 245, 515, 547, 2601, 2627, 2628, 8114, 8115, 8152 };

        public static bool IsDataErrorNumber(int number)
        {
            return DataErrorNumbers.Contains(number);
        }

        public static bool IsDataError(Exception ex)
        {
            return ex switch
            {
                SqlException sql => sql.Errors.Cast<SqlError>().Any(e => IsDataErrorNumber(e.Number)),
                ArgumentException or InvalidCastException or FormatException or OverflowException => true,
                _ => false
            };
        }
    }
}
