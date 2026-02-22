using Xunit;
using TaskApp;

namespace TaskApp.Tests
{
    public class ErrorHelperTests
    {
        [Fact]
        public void ValidatePriority_WithInvalidPriority_ReturnsFalse()
        {
            var result = ErrorHelper.ValidatePriority("urgent", out var errorMessage);

            Assert.False(result);
            Assert.Contains("not a valid priority", errorMessage);
            Assert.Contains("task add --help", errorMessage);
        }

        [Fact]
        public void ValidatePriority_WithValidPriority_ReturnsTrue()
        {
            var result = ErrorHelper.ValidatePriority("high", out var errorMessage);

            Assert.True(result);
            Assert.Null(errorMessage);
        }

        [Fact]
        public void ValidateStatus_WithInvalidStatus_ReturnsFalse()
        {
            var result = ErrorHelper.ValidateStatus("pending", out var errorMessage);

            Assert.False(result);
            Assert.Contains("not a valid status", errorMessage);
            Assert.Contains("task add --help", errorMessage);
        }

        [Fact]
        public void ValidateStatus_WithValidStatus_ReturnsTrue()
        {
            var result = ErrorHelper.ValidateStatus("in_progress", out var errorMessage);

            Assert.True(result);
            Assert.Null(errorMessage);
        }

        [Fact]
        public void ValidateDate_WithInvalidDate_ReturnsFalse()
        {
            var result = ErrorHelper.ValidateDate("not-a-date", out var errorMessage);

            Assert.False(result);
            Assert.Contains("not a valid date", errorMessage);
            Assert.Contains("task add --help", errorMessage);
        }

        [Fact]
        public void ValidateDate_WithValidDate_ReturnsTrue()
        {
            var result = ErrorHelper.ValidateDate("2024-12-31", out var errorMessage);

            Assert.True(result);
            Assert.Null(errorMessage);
        }
    }
}