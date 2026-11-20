// Unity's .NET Standard 2.1 reference assemblies lack the marker used by C# init accessors.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
