using KeyAutomator.Services;

namespace KeyAutomator.Tests;

[TestClass]
public sealed class TestStartup
{
    [AssemblyInitialize]
    public static void InitializeLanguage(TestContext context)
    {
        _ = context;
        Loc.Initialize(Loc.Japanese);
    }
}
