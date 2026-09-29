namespace Kahoot.Infrastructure.Services;

using System.Globalization;
using System.Security.Cryptography;
using Kahoot.Application.Common.Interfaces;

// Cryptographic Game PIN Generator - Generates uniform, non-predictable 8-digit lobby join codes.
public sealed class PinGeneratorService : IPinGeneratorService
{
    // Cryptographically Secure PIN Generation - Uses RandomNumberGenerator.GetInt32 across [0, 100_000_000) to defeat statistical brute-force discovery.
    public string GeneratePin()
    {
        int numericPin = RandomNumberGenerator.GetInt32(0, 100_000_000);
        return numericPin.ToString("D8", CultureInfo.InvariantCulture);
    }
}
