namespace Kahoot.Infrastructure.UnitTests.Security;

using System;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Kahoot.Application.Common;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Kahoot.Infrastructure.Security;
using Kahoot.Infrastructure.UnitTests.TestSupport;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

public sealed class JwtTokenGeneratorTests
{
    private const string Issuer = "unit-test-issuer";
    private const string Audience = "unit-test-audience";
    private readonly byte[] _signingKeyBytes = RandomNumberGenerator.GetBytes(32);
    private readonly string _base64SigningKey;
    private readonly ManualTimeProvider _timeProvider = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    public JwtTokenGeneratorTests()
    {
        _base64SigningKey = Convert.ToBase64String(_signingKeyBytes);
    }

    [Theory]
    [InlineData(UserRole.Host)]
    [InlineData(UserRole.SystemAdmin)]
    public void GenerateAccessToken_IncludesIdentityAndSecurityClaims(UserRole role)
    {
        JwtTokenGenerator generator = CreateGenerator();
        User user = CreateUser(role, tokenSecurityVersion: 5);

        AccessTokenResult result = generator.GenerateAccessToken(user);

        JsonWebTokenHandler handler = new();
        JsonWebToken jwt = handler.ReadJsonWebToken(result.Token);

        Assert.Equal(user.Id.ToString(), jwt.Subject);
        Assert.Equal(user.Id.ToString(), jwt.GetClaim("accountId").Value);
        Assert.Equal(user.DisplayUsername, jwt.GetClaim(JwtRegisteredClaimNames.UniqueName).Value);
        Assert.Equal(role.ToString(), jwt.GetClaim("role").Value);
        Assert.Equal("5", jwt.GetClaim("token_security_version").Value);
        Assert.Equal("5", jwt.GetClaim("tokenSecurityVersion").Value);

        bool hasTenantId = jwt.TryGetClaim("tenantId", out Claim? _);
        Assert.False(hasTenantId);
    }

    [Theory]
    [InlineData(15, 900)]
    [InlineData(2, 120)]
    public void GenerateAccessToken_ReturnsMatchingLifetimeMetadata(int lifetimeMinutes, int expectedSeconds)
    {
        JwtTokenGenerator generator = CreateGenerator(lifetimeMinutes);
        User user = CreateUser();
        DateTimeOffset now = _timeProvider.GetUtcNow();

        AccessTokenResult result = generator.GenerateAccessToken(user);

        Assert.Equal(now.AddMinutes(lifetimeMinutes), result.ExpiresAt);
        Assert.Equal(expectedSeconds, result.ExpiresInSeconds);

        JsonWebTokenHandler handler = new();
        JsonWebToken jwt = handler.ReadJsonWebToken(result.Token);

        long expectedIat = now.ToUnixTimeSeconds();
        long expectedExp = now.AddMinutes(lifetimeMinutes).ToUnixTimeSeconds();

        Assert.Equal(expectedIat, jwt.GetPayloadValue<long>("iat"));
        Assert.Equal(expectedIat, jwt.GetPayloadValue<long>("nbf"));
        Assert.Equal(expectedExp, jwt.GetPayloadValue<long>("exp"));
    }

    [Fact]
    public void GenerateAccessToken_UsesInjectedUtcClock()
    {
        DateTimeOffset nonZeroOffsetTime = new(2026, 6, 15, 14, 30, 0, TimeSpan.FromHours(4));
        OffsetTimeProvider offsetTimeProvider = new(nonZeroOffsetTime);
        JwtTokenGenerator generator = CreateGenerator(timeProvider: offsetTimeProvider);
        User user = CreateUser();

        AccessTokenResult result = generator.GenerateAccessToken(user);

        DateTimeOffset expectedUtc = nonZeroOffsetTime.ToUniversalTime();
        Assert.Equal(expectedUtc.AddMinutes(15), result.ExpiresAt);

        JsonWebTokenHandler handler = new();
        JsonWebToken jwt = handler.ReadJsonWebToken(result.Token);

        long expectedIatUtc = expectedUtc.ToUnixTimeSeconds();
        long expectedExpUtc = expectedUtc.AddMinutes(15).ToUnixTimeSeconds();

        Assert.Equal(expectedIatUtc, jwt.GetPayloadValue<long>("iat"));
        Assert.Equal(expectedExpUtc, jwt.GetPayloadValue<long>("exp"));
    }

    [Fact]
    public async Task GenerateAccessToken_UsesHmacSha256AndValidSignature()
    {
        JwtTokenGenerator generator = CreateGenerator();
        User user = CreateUser();

        AccessTokenResult result = generator.GenerateAccessToken(user);

        JsonWebTokenHandler handler = new();
        JsonWebToken jwt = handler.ReadJsonWebToken(result.Token);
        Assert.Equal(SecurityAlgorithms.HmacSha256, jwt.Alg);

        TokenValidationParameters validationParameters = new()
        {
            ValidIssuer = Issuer,
            ValidAudience = Audience,
            IssuerSigningKey = new SymmetricSecurityKey(_signingKeyBytes),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = false,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
        };

        TokenValidationResult validationResult = await handler.ValidateTokenAsync(result.Token, validationParameters);
        Assert.True(validationResult.IsValid);
    }

    [Fact]
    public async Task GenerateAccessToken_RejectsWrongValidationKeyIssuerOrAudience()
    {
        JwtTokenGenerator generator = CreateGenerator();
        User user = CreateUser();

        AccessTokenResult result = generator.GenerateAccessToken(user);
        JsonWebTokenHandler handler = new();

        byte[] differentKeyBytes = RandomNumberGenerator.GetBytes(32);
        TokenValidationParameters wrongKeyParameters = new()
        {
            ValidIssuer = Issuer,
            ValidAudience = Audience,
            IssuerSigningKey = new SymmetricSecurityKey(differentKeyBytes),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = false,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
        };
        TokenValidationResult wrongKeyResult = await handler.ValidateTokenAsync(result.Token, wrongKeyParameters);
        Assert.False(wrongKeyResult.IsValid);

        TokenValidationParameters wrongIssuerParameters = new()
        {
            ValidIssuer = "wrong-issuer",
            ValidAudience = Audience,
            IssuerSigningKey = new SymmetricSecurityKey(_signingKeyBytes),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = false,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
        };
        TokenValidationResult wrongIssuerResult = await handler.ValidateTokenAsync(result.Token, wrongIssuerParameters);
        Assert.False(wrongIssuerResult.IsValid);

        TokenValidationParameters wrongAudienceParameters = new()
        {
            ValidIssuer = Issuer,
            ValidAudience = "wrong-audience",
            IssuerSigningKey = new SymmetricSecurityKey(_signingKeyBytes),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = false,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
        };
        TokenValidationResult wrongAudienceResult = await handler.ValidateTokenAsync(result.Token, wrongAudienceParameters);
        Assert.False(wrongAudienceResult.IsValid);
    }

    [Fact]
    public void GenerateAccessToken_ProducesDistinctValidJwtIds()
    {
        JwtTokenGenerator generator = CreateGenerator();
        User user = CreateUser();

        AccessTokenResult result1 = generator.GenerateAccessToken(user);
        AccessTokenResult result2 = generator.GenerateAccessToken(user);

        JsonWebTokenHandler handler = new();
        JsonWebToken jwt1 = handler.ReadJsonWebToken(result1.Token);
        JsonWebToken jwt2 = handler.ReadJsonWebToken(result2.Token);

        bool isValidGuid1 = Guid.TryParse(jwt1.Id, out Guid jti1);
        bool isValidGuid2 = Guid.TryParse(jwt2.Id, out Guid jti2);

        Assert.True(isValidGuid1);
        Assert.True(isValidGuid2);
        Assert.NotEqual(Guid.Empty, jti1);
        Assert.NotEqual(Guid.Empty, jti2);
        Assert.NotEqual(jti1, jti2);
    }

    [Fact]
    public void GenerateAccessToken_RejectsNullUserAndConstructorDependencies()
    {
        IOptions<JwtOptions> validOptions = Options.Create(new JwtOptions
        {
            Issuer = Issuer,
            Audience = Audience,
            SigningKey = _base64SigningKey,
            AccessTokenMinutes = 15
        });

        Assert.Throws<ArgumentNullException>(() => new JwtTokenGenerator(null!, _timeProvider));
        Assert.Throws<ArgumentNullException>(() => new JwtTokenGenerator(validOptions, null!));

        JwtTokenGenerator generator = new(validOptions, _timeProvider);
        Assert.Throws<ArgumentNullException>(() => generator.GenerateAccessToken(null!));
    }

    private JwtTokenGenerator CreateGenerator(int accessTokenMinutes = 15, TimeProvider? timeProvider = null)
    {
        IOptions<JwtOptions> options = Options.Create(new JwtOptions
        {
            Issuer = Issuer,
            Audience = Audience,
            SigningKey = _base64SigningKey,
            AccessTokenMinutes = accessTokenMinutes
        });

        return new JwtTokenGenerator(options, timeProvider ?? _timeProvider);
    }

    private static User CreateUser(UserRole role = UserRole.Host, int tokenSecurityVersion = 1)
    {
        return new User
        {
            Id = Guid.Parse("01918a3d-4e2b-7c15-8fa9-33b66479a001"),
            DisplayUsername = "QuizMasterHost",
            NormalizedUsername = "QUIZMASTERHOST",
            PasswordHash = "$argon2id$dummyhash",
            Role = role,
            TokenSecurityVersion = tokenSecurityVersion
        };
    }

    private sealed class OffsetTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _currentTime;

        public OffsetTimeProvider(DateTimeOffset currentTime)
        {
            _currentTime = currentTime;
        }

        public override DateTimeOffset GetUtcNow() => _currentTime;
    }
}
