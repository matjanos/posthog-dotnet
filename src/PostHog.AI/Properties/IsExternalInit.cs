#if NETSTANDARD2_1
// Enables record types on netstandard2.1 without pulling additional dependencies.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif
