using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Kahoot.Infrastructure.UnitTests")]

namespace Kahoot.Infrastructure;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
