using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Kahoot.Api.UnitTests")]

namespace Kahoot.Api;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
