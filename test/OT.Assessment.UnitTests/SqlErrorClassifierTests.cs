using OT.Assessment.Consumer.Services;

namespace OT.Assessment.UnitTests
{
    /// <summary>
    /// Tests the rule the Consumer uses to decide what to do when saving fails: a data error can never
    /// succeed (the message goes to the dead-letter queue), anything else may work later (retry, then
    /// put the messages back on the queue).
    /// </summary>
    public class SqlErrorClassifierTests
    {
        // ---------- SQL Server error numbers ----------

        [Theory]
        [InlineData(220)]   // arithmetic overflow, small types
        [InlineData(232)]   // arithmetic overflow
        [InlineData(242)]   // date out of range
        [InlineData(245)]   // conversion failed
        [InlineData(515)]   // NULL in a NOT NULL column
        [InlineData(547)]   // foreign key or check constraint
        [InlineData(2601)]  // duplicate key (unique index)
        [InlineData(2627)]  // duplicate key (primary key / unique constraint)
        [InlineData(2628)]  // string or binary data would be truncated
        [InlineData(8114)]  // error converting data type
        [InlineData(8115)]  // arithmetic overflow converting a number
        [InlineData(8152)]  // string or binary data would be truncated
        public void DataErrorNumbers_AreDataErrors(int number)
        {
            Assert.True(SqlErrorClassifier.IsDataErrorNumber(number));
        }

        [Theory]
        [InlineData(-2)]    // timeout
        [InlineData(53)]    // server not found or not accessible
        [InlineData(1205)]  // deadlock victim: trying again can work
        [InlineData(4060)]  // cannot open the database
        [InlineData(18456)] // login failed
        public void TemporaryOrInfrastructureErrorNumbers_AreNotDataErrors(int number)
        {
            Assert.False(SqlErrorClassifier.IsDataErrorNumber(number));
        }

        // ---------- exceptions raised while the batch is prepared ----------

        [Theory]
        [InlineData(typeof(ArgumentException))]
        [InlineData(typeof(InvalidCastException))]
        [InlineData(typeof(FormatException))]
        [InlineData(typeof(OverflowException))]
        public void ExceptionsAboutTheDataItself_AreDataErrors(Type exceptionType)
        {
            var ex = (Exception)Activator.CreateInstance(exceptionType);

            Assert.True(SqlErrorClassifier.IsDataError(ex));
        }

        [Theory]
        [InlineData(typeof(InvalidOperationException))]
        [InlineData(typeof(TimeoutException))]
        [InlineData(typeof(IOException))]
        [InlineData(typeof(TaskCanceledException))]
        public void OtherExceptions_AreNotDataErrors(Type exceptionType)
        {
            var ex = (Exception)Activator.CreateInstance(exceptionType);

            Assert.False(SqlErrorClassifier.IsDataError(ex));
        }
    }
}
