using System.Reflection;

// Without these the compiler emits 0.0.0.0, so the exe's Properties page showed
// an empty version. AssemblyFileVersion is the one FileVersionInfo reports as
// FileVersion, so both stay in sync deliberately.
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
