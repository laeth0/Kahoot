using System.Globalization;
using Kahoot.Infrastructure.Services;
using Xunit;

namespace Kahoot.Infrastructure.UnitTests.Services;

public sealed class PinGeneratorServiceTests
{
    [Fact]
    public void GeneratePin_ReturnsEightAsciiDigits()
    {
        PinGeneratorService service = new();

        for (int iteration = 0; iteration < 32; iteration++)
        {
            string pin = service.GeneratePin();

            Assert.Equal(8, pin.Length);
            Assert.All(pin, character => Assert.InRange(character, '0', '9'));
        }
    }

    [Fact]
    public void GeneratePin_UsesInvariantDigitsUnderDifferentCulture()
    {
        PinGeneratorService service = new();
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo arabicCulture = new("ar-SA");
            CultureInfo.CurrentCulture = arabicCulture;
            CultureInfo.CurrentUICulture = arabicCulture;

            for (int iteration = 0; iteration < 32; iteration++)
            {
                string pin = service.GeneratePin();

                Assert.Equal(8, pin.Length);
                Assert.All(pin, character => Assert.InRange(character, '0', '9'));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }
}
