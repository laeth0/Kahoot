using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Kahoot.Application.UnitTests")]

namespace Kahoot.Application;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
