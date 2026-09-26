namespace Kahoot.Application.Common.Seeding;

public interface ISeeder
{
    Task SeedAsync(CancellationToken cancellationToken);
}
