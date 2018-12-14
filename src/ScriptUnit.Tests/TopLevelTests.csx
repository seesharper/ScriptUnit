#load "../ScriptUnit/ScriptUnit.csx"
#r "nuget:FluentAssertions, 4.19.4"
using FluentAssertions;
using static ScriptUnit;

await AddTestsFrom<TopLevelTests>().Execute();

public class TopLevelTests
{
    public async Task ShouldExecuteTopLevelTest()
    {
         await new TestRunner().AddTopLevelTests().AddFilter(m => m.Name.StartsWith("Should"))
            .WithSummaryFormatter(summary => summary.TestResults.Single().TestCaseResults.Single().StandardOut.Should().Be("Hello from toplevel test")).Execute();
    }
}


public void ShouldWriteToConsole()
{
    Console.Write("Hello from toplevel test");
}