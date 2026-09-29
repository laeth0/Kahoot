namespace Kahoot.Application.Common.Interfaces;

public interface IPinGeneratorService
{
    // Cryptographically Secure PIN Generation (JOIN-PIN-001) - Generates non-sequential 6-digit game discovery PIN
    string GeneratePin();
}
