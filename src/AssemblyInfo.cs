using System.Reflection;

// Without these the compiler emits 0.0.0.0, so the exe's Properties page showed
// an empty version. AssemblyFileVersion is the one FileVersionInfo reports as
// FileVersion, so both stay in sync deliberately. AssemblyTitle also supplies
// the FileDescription shown in Explorer's Properties dialog.
[assembly: AssemblyTitle("DSHView")]
[assembly: AssemblyProduct("DSHView")]
[assembly: AssemblyVersion("1.2.1.3")]
[assembly: AssemblyFileVersion("1.2.1.3")]
