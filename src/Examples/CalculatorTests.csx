#load "Calculator.csx"
#load "../ScriptUnit/ScriptUnit.csx"
#r "nuget:FluentAssertions, 4.19.4"

using System.Threading;
using FluentAssertions;
using static ScriptUnit;
return await AddTestsFrom<CalculatorTests>().Execute();

public class CalculatorTests
{
    
    public void ShouldAddNumbers()
    {
        var result = Add(2,2);        
        result.Should().Be(4);
    }

    [Arguments(1,2,3)]
    [Arguments(2,3,5)]
    public void ShouldAddNumbersUsingArguments(int value1, int value2, int result)
    {                    
        Add(value1,value2).Should().Be(result);        
    }       
}