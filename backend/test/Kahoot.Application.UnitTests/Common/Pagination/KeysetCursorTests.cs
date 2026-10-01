using System.Buffers.Text;
using System.Text;
using Kahoot.Application.Common.Pagination;
using Xunit;

namespace Kahoot.Application.UnitTests.Common.Pagination;

public sealed class KeysetCursorTests
{
    [Fact]
    public void EncodeAndTryDecode_RoundTripsSuccessfully()
    {
        DateTimeOffset createdAt = new(2026, 10, 1, 12, 30, 45, 123, TimeSpan.Zero);
        Guid id = Guid.NewGuid();

        string encoded = KeysetCursor.Encode(createdAt, id);
        bool success = KeysetCursor.TryDecode(encoded, out KeysetCursor? decoded);

        Assert.True(success);
        Assert.NotNull(decoded);
        Assert.Equal(createdAt.UtcTicks, decoded.CreatedAt.UtcTicks);
        Assert.Equal(id, decoded.Id);
    }

    [Fact]
    public void EncodeAndTryDecode_WithTimestampBoundaries_RoundTripsSuccessfully()
    {
        DateTimeOffset minTimestamp = DateTimeOffset.MinValue;
        Guid minId = Guid.Empty;

        string minEncoded = KeysetCursor.Encode(minTimestamp, minId);
        bool minSuccess = KeysetCursor.TryDecode(minEncoded, out KeysetCursor? minDecoded);

        Assert.True(minSuccess);
        Assert.NotNull(minDecoded);
        Assert.Equal(minTimestamp.UtcTicks, minDecoded.CreatedAt.UtcTicks);
        Assert.Equal(minId, minDecoded.Id);

        DateTimeOffset maxTimestamp = DateTimeOffset.MaxValue;
        Guid maxId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

        string maxEncoded = KeysetCursor.Encode(maxTimestamp, maxId);
        bool maxSuccess = KeysetCursor.TryDecode(maxEncoded, out KeysetCursor? maxDecoded);

        Assert.True(maxSuccess);
        Assert.NotNull(maxDecoded);
        Assert.Equal(maxTimestamp.UtcTicks, maxDecoded.CreatedAt.UtcTicks);
        Assert.Equal(maxId, maxDecoded.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void TryDecode_WhenNullOrWhitespace_ReturnsFalse(string? cursor)
    {
        bool success = KeysetCursor.TryDecode(cursor, out KeysetCursor? result);

        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void TryDecode_WhenNotBase64Url_ReturnsFalse()
    {
        bool success = KeysetCursor.TryDecode("invalid!@#$%^&*()", out KeysetCursor? result);

        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void TryDecode_WhenMissingColonDelimiter_ReturnsFalse()
    {
        string encodedWithoutColon = Base64Url.EncodeToString(Encoding.UTF8.GetBytes("6389490000000000000123456789abcdef0123456789abcdef"));

        bool success = KeysetCursor.TryDecode(encodedWithoutColon, out KeysetCursor? result);

        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void TryDecode_WhenColonAtBeginning_ReturnsFalse()
    {
        string encodedColonAtStart = Base64Url.EncodeToString(Encoding.UTF8.GetBytes($":{Guid.NewGuid():N}"));

        bool success = KeysetCursor.TryDecode(encodedColonAtStart, out KeysetCursor? result);

        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void TryDecode_WhenTicksNonNumeric_ReturnsFalse()
    {
        string encodedNonNumeric = Base64Url.EncodeToString(Encoding.UTF8.GetBytes($"notanumber:{Guid.NewGuid():N}"));

        bool success = KeysetCursor.TryDecode(encodedNonNumeric, out KeysetCursor? result);

        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void TryDecode_WhenTicksOutOfRange_ReturnsFalse()
    {
        string encodedNegativeTicks = Base64Url.EncodeToString(Encoding.UTF8.GetBytes($"-100:{Guid.NewGuid():N}"));

        bool success = KeysetCursor.TryDecode(encodedNegativeTicks, out KeysetCursor? result);

        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void TryDecode_WhenIdNotValidGuid_ReturnsFalse()
    {
        string encodedInvalidGuid = Base64Url.EncodeToString(Encoding.UTF8.GetBytes($"{DateTimeOffset.UtcNow.UtcTicks}:not-a-valid-guid-value"));

        bool success = KeysetCursor.TryDecode(encodedInvalidGuid, out KeysetCursor? result);

        Assert.False(success);
        Assert.Null(result);
    }
}
