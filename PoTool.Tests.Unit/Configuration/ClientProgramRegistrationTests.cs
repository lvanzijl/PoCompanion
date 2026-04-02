using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PoTool.Tests.Unit.Configuration;

[TestClass]
public class ClientProgramRegistrationTests
{
    [TestMethod]
    public void ClientProgram_RegistersBugTriageClientInterface()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var programPath = Path.Combine(repositoryRoot, "PoTool.Client", "Program.cs");
        var programText = File.ReadAllText(programPath);

        StringAssert.Contains(
            programText,
            "builder.Services.AddScoped<IBugTriageClient>",
            "Bugs Triage route requires IBugTriageClient to be registered for direct page activation.");
    }
}
