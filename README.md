# ScriptUnit
A super simple test runner for C# scripts.

Once we start to create reusable scripts that is to be consumed by other scripts, it makes sense to verify that these scripts actually do what they are intended to do. Scripts are really no different from regular code and that makes it a perfect target for unit testing. 

> Fun fact: **ScriptUnit** is also just a script with its own set of unit tests executed by itself. Isn't that nice, code that test itself :)

**ScriptUnit** does not come with an API for assertions so we are free to use any assertion library available. 
In the following examples we will be using [FluentAssertions](http://fluentassertions.com/).

### Writing Tests

The first thing we need to do is to somehow reference **ScriptUnit** along with an assertion library.

```c#
#load "ScriptUnit.0.1.0\contentFiles\csx\any\main.csx"
#r "FluentAssertions.4.19.4\lib\net45\FluentAssertions.dll"
```

If [Dotnet-Script](https://github.com/filipw/dotnet-script) is used to execute the scripts, we can bring in these dependencies as inline NuGet references

```c#
#load "nuget:ScriptUnit, 0.1.0"
#r "nuget:FluentAssertions, 4.19.4"
```

> The advantage of using **Dotnet-Script** is that we can also debug unit tests, but as we can see, **ScriptUnit** does not require a specific script runner.



A test class (fixture) is just a regular class where the default is that we consider all public methods as test methods (cases). **ScriptUnit** will create an instance of the test class and  execute all test methods in no particular order. 

**SampleTests.csx**

```c#
#load "nuget:ScriptUnit, 0.1.0"
#r "nuget:FluentAssertions, 4.19.4"

using static ScriptUnit;   
using FluentAssertions;

return await AddTestsFrom<CalculatorTests>().Execute();

public class SampleTests()
{    
    public void Success()
    {
        "Ok".Should().Be("Ok");
    }
  
  	public void Fail()
    {
        "Ok".Should().NotBe("Ok");
    }
}
```

### Setup and Tear down

Tests classes (fixtures) that shares state across test methods can do initialization in the constructor of the test class. For "tear down", simply implement `IDisposable`.

```c#
public class SampleTests : IDisposable
{   
    public SampleTests()
    {
    	//Do init here..  
    }
    
    public void Dispose()
    {
        //Do "tear down" here--
    }
}
```



### Data driven test

Test method that has parameters can be executed with a set of arguments using the `Arguments` attribute.

```c#
[Arguments(1,2,3)]
[Arguments(2,3,5)]
public void ShouldAddNumbers3(int value1, int value2, int result)
{                
	Add(value1,value2).Should().Be(result);        
}
```

Arguments passed to test methods can also come from a different source.

```c#
.WithArgumentProvider(testMethod => {
    //Return arguments here
});
```

When presenting the test result for data driven tests, we will simply execute `ToString()` on each argument.

```shell
CalculatorTests.ShouldAddNumbers3      33ms
* CalculatorTests.ShouldAddNumbers3(1, 2, 3)
                                       31ms
* CalculatorTests.ShouldAddNumbers3(2, 3, 5)
                                       0ms
Total tests: 2. Passed: 2. Failed: 0.
Test Run Successful.
Test execution time 0,0402419 seconds

```

If we should a more sophisticated formatting of arguments, we can do this using the `WithArgumentsFormatter` method.

```c#
.WithArgumentsFormatter(arguments => {
  // Do something else than ToString() here 
});
```



### Test Execution 

In addition to the actual test class and its test methods we need something to actually run the tests

```c#
return await AddTestsFrom<SampleTests>().Execute();
```

This piece of code is written directly inside the script file and will be executed when we execute the script using script runner of choice.

**csi.exe**

```shell
csi Sampletests.csx
```

**DotNet-Script**

```shell
dotnet script SampleTests.csx
```

The return value from `Execute` and `ExecuteInParallel` is used as the exit code.



### Test Filtering

The default is to execute all public methods found in the test class, but we can also choose to filter these methods into a subset like this.

```c#
return await AddTestsFrom<CalculatorTests>().WithFilter(testMethod => testMethod.Name.StartsWith("Should")).Execute();
```

> Note: We could also use this to filter methods based on an attribute like xUnit does with its `Fact` attribute.



To execute a single test we can filter test methods down to a single test. 

````C#
return await AddTestsFrom<CalculatorTests>().WithFilter<CalculatorTests>(f => c.ShouldAddNumbers()).Execute();
````

### Parallelization

**ScriptUnit** can execute test fixtures in parallel using the `ExecuteInParallel`method. Test methods within the same test class (fixture) are not executed in parallel. 

```c#
return await AddTestsFrom<SomeTestFixture>()
.AddTestsFrom<AnotherTestFixture>()
.ExecuteInParallel();
```



### Standard Out/Error

**ScriptUnit** captures `Console.Out` and `Console.Error` and will by default output these streams when formatting the test results.

```c#
public void WriteToConsole()
{
	Console.WriteLine("This text was written to standard out");
	Console.Error.WriteLine("This text was written to standard error");
}
```

Running this test will yield the following output.

```shell
SampleTests.WriteToConsole      0ms
Standard Out
This text was written to standard out

Standard Error
This text was written to standard error

Total tests: 1. Passed: 0. Failed: 0.
Test Run Successful.
Test execution time 0 seconds
```

We can also get access to the text written to `Console.Out` and ´Console.Error` from within a test method.

```C#
public void WriteToConsole()
{
	Console.WriteLine("This test was written to standard out");
	TestContext.StandardOut.Should().Contain("This text was written to standard out")

```

### Custom Formatting

**ScriptUnit** outputs a console friendly test result summary, but we can still create our own summary formatter that replaces the default output. 

```c#
.WithSummaryFormatter(summary => {
    //Process the summary here
});
```

This can for instance be used to create a different console output or it can be used to output the summary in a different format such as Markdown.