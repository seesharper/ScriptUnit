#! "netcoreapp2.0"
#load "../ScriptUnit/ScriptUnit.csx"
#r "nuget:FluentAssertions, 4.19.4"
using FluentAssertions;

using static ScriptUnit;
await AddTestsFrom<ScriptUnitTests>().Execute();

public class ScriptUnitTests
{
    public async Task ShouldReportInnerException()
    {
        await AddTestsFrom<ExceptionTests>()                
                .WithSummaryFormatter(summary => summary.TestResults.SelectMany(tr => tr.TestCaseResults).Should().OnlyContain(tc => tc.Exception is Exception))
                .Execute();
    }

    public async Task ShouldCaptureStandardOutAndStandardError()
    {
        await AddTestsFrom<ConsoleTests>()
                .WithSummaryFormatter(summary => 
                {
                    summary.TestResults.Single().TestCaseResults.Single().StandardOut.Should().Be("This is the output from stdOut");
                    summary.TestResults.Single().TestCaseResults.Single().StandardError.Should().Be("This is the output from stdErr");
                })
            .Execute();                  
    }

    public async Task ShouldCallDisposeMethod()
    {
        await AddTestsFrom<DisposableTests>().Execute();
        DisposableTests.Disposed.Should().BeTrue();        
    }

    public async Task ShouldReportFixtureNameWithOutSubMissionPrefix()
    {
        await AddTestsFrom<ConsoleTests>()
            .WithSummaryFormatter(summary => 
            {
               summary.TestResults.Single().Fixture.Should().NotStartWith("Submission#0+");                
            })
            .Execute();                
    }

    public async Task ShouldRunTestsInParallel()
    {                
            await AddTestsFrom<LongRunningTests>()
        .AddTestsFrom<AnotherLongRunningTests>()
        .WithSummaryFormatter(summary => summary.TotalDuration.TotalMilliseconds.Should().BeLessThan(1000))
        .ExecuteInParallel();
        
    }

    public async Task ShouldExecuteTestsWithParameters()
    {   
        await AddTestsFrom<DataDrivenTests>()
        .WithSummaryFormatter(summary => summary.TestResults.Single().TestCaseResults.Count().Should().Be(3)).Execute();
    }

    public async Task ShouldReportStandardOutAndStandardError()
    {
        await AddTestsFrom<ConsoleTests>().Execute();
        var r = TestContext.StandardOut;
        TestContext.StandardOut.Should().Contain("This is the output from stdOut");
        TestContext.StandardOut.Should().Contain("This is the output from stdErr");
    }
}

public class ExceptionTests
{
    public void FailingTest()
    {
        throw new Exception();
    }

    public Task FailingTestAsync()
    {
        throw new Exception();
    }
}

public class ConsoleTests
{
    public async Task WriteToStandardOutAndStandardError()
    {
        Console.Out.Write("This is the output from stdOut");
        await Task.Delay(100);
        Console.Error.Write("This is the output from stdErr");       
        var test = TestContext.StandardOut;                         
    }
}

public class DisposableTests : IDisposable
{

    public static bool Disposed { get; private set; }
    public void Dispose()
    {
        Disposed = true;
    }
}

public class LongRunningTests
{
    public async Task WaitForHalfASecond()
    {
        await Task.Delay(500);
    }
}

public class AnotherLongRunningTests
{
    public async Task WaitForHalfASecond()
    {
        await Task.Delay(500);
    }
}


public class DataDrivenTests
{
    [Arguments(1,2,3)]
    [Arguments(2,3,5)]
    [Arguments(2,3,5)]
    public async Task AddNumbers(int value1, int value2, int expected)
    {

    }
}
