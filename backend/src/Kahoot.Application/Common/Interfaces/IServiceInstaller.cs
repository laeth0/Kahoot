using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kahoot.Application.Common.Interfaces;

public interface IServiceInstaller
{
    IServiceCollection Install(IServiceCollection services, IConfiguration configuration);
}
