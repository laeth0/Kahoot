namespace Kahoot.Infrastructure.UnitTests.Security;

using System;
using System.Threading.Tasks;
using Kahoot.Infrastructure.Security;
using Xunit;

[Collection("PasswordHashing")]
public sealed class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public async Task HashPasswordAsync_ProducesConfiguredArgon2idFormat()
    {
        const string password = "TestPassword#2026";

        string hash = await _hasher.HashPasswordAsync(password);

        string[] parts = hash.Split('$');
        Assert.Equal(6, parts.Length);
        Assert.Equal(string.Empty, parts[0]);
        Assert.Equal("argon2id", parts[1]);
        Assert.Equal("v=19", parts[2]);
        Assert.Equal("m=65536,t=3,p=1", parts[3]);

        byte[] salt = Convert.FromBase64String(parts[4]);
        Assert.Equal(16, salt.Length);

        byte[] digest = Convert.FromBase64String(parts[5]);
        Assert.Equal(32, digest.Length);
    }

    [Fact]
    public async Task HashPasswordAsync_UsesDistinctSaltsForRepeatedPassword()
    {
        const string password = "SameRepeatedPassword!123";

        string hash1 = await _hasher.HashPasswordAsync(password);
        string hash2 = await _hasher.HashPasswordAsync(password);

        string[] parts1 = hash1.Split('$');
        string[] parts2 = hash2.Split('$');

        Assert.NotEqual(parts1[4], parts2[4]);
        Assert.NotEqual(hash1, hash2);

        bool isValid1 = await _hasher.VerifyPasswordAsync(password, hash1);
        bool isValid2 = await _hasher.VerifyPasswordAsync(password, hash2);

        Assert.True(isValid1);
        Assert.True(isValid2);
    }

    [Fact]
    public async Task VerifyPasswordAsync_AcceptsMatchingPasswordAndRejectsMismatch()
    {
        const string correctPassword = "CorrectHorseBatteryStaple!";
        const string wrongPassword = "WrongHorseBatteryStaple!";

        string hash = await _hasher.HashPasswordAsync(correctPassword);

        bool matchResult = await _hasher.VerifyPasswordAsync(correctPassword, hash);
        bool mismatchResult = await _hasher.VerifyPasswordAsync(wrongPassword, hash);

        Assert.True(matchResult);
        Assert.False(mismatchResult);
    }

    [Theory]
    [InlineData("  PasswordWithLeadingAndTrailingSpaces  ", "PasswordWithLeadingAndTrailingSpaces")]
    [InlineData("Pä$$wörd_日本語_Ключ_2026! 🔒", "Pä$$wörd_日本語_Ключ_2026! 🔓")]
    public async Task VerifyPasswordAsync_PreservesUnicodeAndWhitespaceInput(string originalPassword, string alteredPassword)
    {
        string hash = await _hasher.HashPasswordAsync(originalPassword);

        bool originalMatches = await _hasher.VerifyPasswordAsync(originalPassword, hash);
        bool alteredMatches = await _hasher.VerifyPasswordAsync(alteredPassword, hash);

        Assert.True(originalMatches);
        Assert.False(alteredMatches);
    }

    [Fact]
    public async Task HashPasswordAsync_RejectsNullPassword()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _hasher.HashPasswordAsync(null!));
    }

    [Fact]
    public async Task VerifyPasswordAsync_RejectsNullPassword()
    {
        const string validHash = "$argon2id$v=19$m=65536,t=3,p=1$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

        bool result = await _hasher.VerifyPasswordAsync(null!, validHash);

        Assert.False(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("argon2id$v=19$m=65536,t=3,p=1$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("$argon2id$v=19$m=65536,t=3,p=1$AAAAAAAAAAAAAAAAAAAAAA==")]
    [InlineData("$argon2id$v=19$m=65536,t=3,p=1$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=$extra")]
    [InlineData("$argon2i$v=19$m=65536,t=3,p=1$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("$argon2id$v=18$m=65536,t=3,p=1$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("$argon2id$v=20$m=65536,t=3,p=1$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("$argon2id$v=19$m=32768,t=3,p=1$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("$argon2id$v=19$m=65536,t=2,p=1$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("$argon2id$v=19$m=65536,t=3,p=2$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("$argon2id$v=19$t=3,m=65536,p=1$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("$argon2id$v=19$m=65536,t=3,p=1$!not-base64!$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("$argon2id$v=19$m=65536,t=3,p=1$AAAAAAAAAAAAAAAAAAAAAA==$!not-base64!")]
    public async Task VerifyPasswordAsync_RejectsMalformedHash(string? malformedHash)
    {
        bool result = await _hasher.VerifyPasswordAsync("AnyPassword123!", malformedHash!);

        Assert.False(result);
    }

    [Theory]
    [InlineData(15, 32)]
    [InlineData(17, 32)]
    [InlineData(16, 31)]
    [InlineData(16, 33)]
    public async Task VerifyPasswordAsync_RejectsMalformedHashWithWrongDecodedLength(int saltLength, int hashLength)
    {
        string saltBase64 = Convert.ToBase64String(new byte[saltLength]);
        string hashBase64 = Convert.ToBase64String(new byte[hashLength]);
        string malformedHash = $"$argon2id$v=19$m=65536,t=3,p=1${saltBase64}${hashBase64}";

        bool result = await _hasher.VerifyPasswordAsync("AnyPassword123!", malformedHash);

        Assert.False(result);
    }

    [Fact]
    public async Task VerifyDummyPasswordAsync_PerformsVerificationForNonMatchingInput()
    {
        const string nonMatchingPassword = "SyntheticNonMatchingPassword#987";

        bool result = await _hasher.VerifyDummyPasswordAsync(nonMatchingPassword);

        Assert.False(result);
    }
}
