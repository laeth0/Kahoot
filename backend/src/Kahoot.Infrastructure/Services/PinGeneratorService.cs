namespace Kahoot.Infrastructure.Services;

using System.Globalization;
using System.Security.Cryptography;
using Kahoot.Application.Common.Interfaces;

public sealed class PinGeneratorService : IPinGeneratorService
{
    public string GeneratePin()
    {
        int numericPin = RandomNumberGenerator.GetInt32(0, 100_000_000);
        return numericPin.ToString("D8", CultureInfo.InvariantCulture);
    }
}
