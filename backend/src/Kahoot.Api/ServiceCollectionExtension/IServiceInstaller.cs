using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kahoot.Api.ServiceCollectionExtension;

public interface IServiceInstaller
{
    IServiceCollection Install(IServiceCollection services, IConfiguration configuration);
}
