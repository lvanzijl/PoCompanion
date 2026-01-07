// Global using directives and assembly attributes

using System.Runtime.CompilerServices;

// Make internal members visible to test assemblies
[assembly: InternalsVisibleTo("PoTool.Tests.Unit")]
[assembly: InternalsVisibleTo("PoTool.Tests.Integration")]
