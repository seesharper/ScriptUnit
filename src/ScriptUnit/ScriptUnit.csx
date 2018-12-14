/*********************************************************************************
    The MIT License (MIT)
    Copyright (c) 2016 bernhard.richter@gmail.com
    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:
    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.
    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
******************************************************************************
    ScriptUnit version 0.1.3
    https://github.com/seesharper/ScriptUnit
    http://twitter.com/bernhardrichter
******************************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


Action action = () => Dummy();

private void Dummy(){}

ScriptUnit.TestRunner.Initialize(action.Target);

/// <summary>
/// Serves as a namespace since there are no namespaces in scripts.
/// </summary>
public static class ScriptUnit
{
    /// <summary>
    /// Adds tests from the given <typeparamref name="TFixture"/> type.
    /// </summary>
    /// <typeparam name="TFixture">The type for which to add tests.</typeparam>
    /// <returns>The <see cref="TestRunner"/> used to execute the tests.</returns>
    public static TestRunner AddTestsFrom<TFixture>()
    {
        return new TestRunner().AddTestsFrom<TFixture>();
    }

    /// <summary>
    /// Adds top level test methods.
    /// </summary>
    /// <returns>The <see cref="TestRunner"/> used to execute the tests.</returns>
    public static TestRunner AddTopLevelTests()
    {
        return new TestRunner().AddTopLevelTests();
    }

    /// <summary>
    /// A test runner capable of running unit tests.
    /// </summary>
    public class TestRunner
    {
        private readonly List<Type> _testFixtures;

        private readonly Action<Summary> _formatter;

        private readonly Func<MethodInfo, bool> _filter;

        private readonly Func<MethodInfo, object[][]> _argumentProvider;

        private readonly Func<object[], string> _argumentsFormatter;

        private static object _submission;

        /// <summary>
        /// Initialize the runner with the submission instance to support top level test methods.
        /// </summary>
        /// <param name="submission"></param>
        internal static void Initialize(object submission)
        {
            _submission = submission;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TestRunner"/> class.
        /// </summary>
        public TestRunner()
        {
            _testFixtures = new List<Type>();
            _formatter = summary => ProcessTestResult(summary);
            _filter = method => true;
            _argumentProvider = GetArguments;
            _argumentsFormatter = arguments =>
            {
                var argumentList = arguments.Select(a => a.ToString())
                    .Aggregate((current, next) => $"{current}, {next}");
                return $"({argumentList})";
            };
        }

        private TestRunner(
            List<Type> testFixtures,
            Action<Summary> formatter,
            Func<MethodInfo, bool> filter,
            Func<MethodInfo, object[][]> argumentProvider,
            Func<object[], string> argumentsFormatter)
        {
            _testFixtures = testFixtures;
            _formatter = formatter;
            _filter = filter;
            _argumentProvider = argumentProvider;
        }

        /// <summary>
        /// Executes the tests that has been added to this <see cref="TestRunner"/>.
        /// </summary>
        /// <returns>0 if the test run succeeds, otherwise 1</returns>
        public async Task<int> Execute()
        {
            List<TestMethodResult> testResults = new List<TestMethodResult>();
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                foreach (var fixtureType in _testFixtures)
                {
                    testResults.AddRange(await ExecuteTestMethods(fixtureType));
                }
            }
            finally
            {
                stopwatch.Stop();
            }

            _formatter(new Summary(stopwatch.Elapsed, testResults.ToArray()));
            return testResults.SelectMany(r => r.TestCaseResults).Any(tcr => tcr.Exception != null) ? 1 : 0;
        }

        /// <summary>
        /// Executes test fixtures (test classes) in parallel.
        /// </summary>
        /// <returns>0 if the test run succeeds, otherwise 1</returns>
        public async Task<int> ExecuteInParallel()
        {
            List<TestMethodResult> testResults = new List<TestMethodResult>();
            Stopwatch stopwatch = Stopwatch.StartNew();
            List<Task<TestMethodResult[]>> tasks = new List<Task<TestMethodResult[]>>();
            try
            {
                foreach (var fixtureType in _testFixtures)
                {
                    tasks.Add(ExecuteTestMethods(fixtureType));
                }

                var allResults = await Task.WhenAll(tasks);
                foreach (var result in allResults)
                {
                    testResults.AddRange(result);
                }
            }
            finally
            {
                stopwatch.Stop();
            }

            _formatter(new Summary(stopwatch.Elapsed, testResults.ToArray()));
            return testResults.SelectMany(r => r.TestCaseResults).Any(tcr => tcr.Exception != null) ? 1 : 0;
        }

        /// <summary>
        /// Adds top level test methods.
        /// </summary>
        /// <returns>The <see cref="TestRunner"/> used to execute the tests.</returns>
        public TestRunner AddTopLevelTests()
        {
            return AddTestsFrom(_submission.GetType());
        }

        /// <summary>
        /// Adds tests from the given <typeparamref name="TFixture"/> type.
        /// </summary>
        /// <typeparam name="TFixture">The type for which to add tests.</typeparam>
        /// <returns>The <see cref="TestRunner"/> used to execute the tests.</returns>
        public TestRunner AddTestsFrom<TFixture>()
        {
            return AddTestsFrom(typeof(TFixture));
        }

        public TestRunner AddTestsFrom(Type fixtureType)
        {
            var testFixtures = new List<Type>(new[] { fixtureType });
            testFixtures.AddRange(_testFixtures);
            return new TestRunner(testFixtures, _formatter, _filter, _argumentProvider, _argumentsFormatter);
        }


        /// <summary>
        /// Returns a new <see cref="TestRunner"/> with a new <paramref name="summaryFormatter"/>
        /// that is used to process the <see cref="Summary"/> representing the result of the test run.
        /// </summary>
        /// <param name="summaryFormatter">The new summary formatter to be used for this <see cref="TestRunner"/>.</param>
        /// <returns>A new <see cref="TestRunner"/> with the new <paramref name="summaryFormatter"/>.</returns>
        public TestRunner WithSummaryFormatter(Action<Summary> summaryFormatter)
        {
            return new TestRunner(_testFixtures, summaryFormatter, _filter, _argumentProvider, _argumentsFormatter);
        }

        /// <summary>
        /// Returns a new <see cref="TestRunner"/> with an additional <paramref name="methodFilter"/>.
        /// The new filter is "anded" with the previous filter.
        /// </summary>
        /// <param name="methodFilter">A function delegate used to determine if the target method is a test method.</param>
        /// <returns>A new <see cref="TestRunner"/> with the additional <paramref name="methodFilter "/>.</returns>
        public TestRunner AddFilter(Func<MethodInfo, bool> methodFilter)
        {
            Func<MethodInfo, bool> newFilter = method => _filter(method) && methodFilter(method);
            return new TestRunner(_testFixtures, _formatter, newFilter, _argumentProvider, _argumentsFormatter);
        }

        /// <summary>
        /// Returns a new <see cref="TestRunner"/> that filters test methods based on a single <paramref name="methodSelector"/>.
        /// This is useful for executing a single test when debugging.
        /// </summary>
        /// <typeparam name="TFixture">The type of fixture for which to select a single test method.</typeparam>
        /// <param name="methodSelector">The expression used to select a test method from the given test fixture.</param>
        /// <returns>A new <see cref="TestRunner"/> with a method filter that selects a single method using the <paramref name="methodSelector"/>.</returns>
        public TestRunner AddFilter<TFixture>(Expression<Action<TFixture>> methodSelector)
        {
            if (methodSelector.Body is MethodCallExpression testMethod)
            {
                return AddFilter(targetTestMethod => targetTestMethod == testMethod.Method);
            }

            throw new ArgumentOutOfRangeException(nameof(methodSelector), "Must be a method");
        }

        /// <summary>
        /// Returns a new <see cref="TestRunner"/> with a new <paramref name="argumentProvider"/>
        /// that is used to provide the arguments for data driven tests.
        /// The default here is to get these values from the <see cref="ArgumentsAttribute"/>.
        /// </summary>
        /// <param name="argumentProvider">The new argument provider for this <see cref="TestRunner"/>.</param>
        /// <returns>A new <see cref="TestRunner"/> that uses the given <paramref name="argumentProvider"/> to provide arguments to data driven tests.</returns>
        public TestRunner WithArgumentProvider(Func<MethodInfo, object[][]> argumentProvider)
        {
            return new TestRunner(_testFixtures, _formatter, _filter, argumentProvider, _argumentsFormatter);
        }

        /// <summary>
        /// Returns a new <see cref="TestRunner"/> with a new <paramref name="argumentsFormatter"/>
        /// that is used to format test method arguments in the test result summary.
        /// </summary>
        /// <param name="argumentsFormatter">The new arguments formatter for this <see cref="TestRunner"/>.</param>
        /// <returns>A new <see cref="TestRunner"/> that used the given <paramref name="argumentsFormatter"/> to
        /// format test method arguments in the test result summary.</returns>
        public TestRunner WithArgumentsFormatter(Func<object[], string> argumentsFormatter)
        {
            return new TestRunner(_testFixtures, _formatter, _filter, _argumentProvider, argumentsFormatter);
        }

        private static object[][] GetArguments(MethodInfo testMethod)
        {
            var argumentsAttributes = testMethod.GetCustomAttributes<ArgumentsAttribute>(inherit: true).ToArray();
            if (argumentsAttributes.Length == 0)
            {
                return Array.Empty<object[]>();
            }

            List<object[]> argumentsLists = new List<object[]>();
            foreach (var argumentsAttribute in argumentsAttributes)
            {
                argumentsLists.Add(argumentsAttribute.Arguments);
            }

            return argumentsLists.ToArray();
        }

        private static async Task<TestMethodResult> ExecuteTestMethod(object fixture, TestMethod testMethod)
        {
            var testCaseResults = new List<TestCaseResult>();
            var stopwatch = Stopwatch.StartNew();

            foreach (var testCase in testMethod.TestCases)
            {
                testCaseResults.Add(await ExecuteTestCase(fixture, testMethod, testCase));
            }

            stopwatch.Stop();

            return new TestMethodResult(
                fixture.GetType().Name,
                testMethod.Method.Name,
                stopwatch.Elapsed,
                testCaseResults.ToArray());
        }

        private static async Task<TestCaseResult> ExecuteTestCase(object fixture, TestMethod testMethod, TestCase testCase)
        {
            Exception exception = null;
            var stopwatch = Stopwatch.StartNew();
            RedirectedConsole.Clear();
            try
            {
                if (testMethod.Method.ReturnType == typeof(Task))
                {
                    await (Task)testMethod.Method.Invoke(fixture, testCase.Arguments);
                }
                else
                {
                    testMethod.Method.Invoke(fixture, testCase.Arguments);
                }
            }
            catch (TargetInvocationException targetInvocationException)
            {
                exception = targetInvocationException.InnerException;
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                stopwatch.Stop();
            }

            return new TestCaseResult(
                stopwatch.Elapsed,
                RedirectedConsole.CurrentStandardOut,
                RedirectedConsole.CurrentStandardError,
                testCase.Arguments,
                exception);
        }

        private async Task<TestMethodResult[]> ExecuteTestMethods(Type fixtureType)
        {
            var testResults = new List<TestMethodResult>();
            RedirectedConsole.Capture();

            object instance;

            if (fixtureType.Name.Contains("Submission"))
            {
                instance = _submission;
            }
            else
            {
                instance = Activator.CreateInstance(fixtureType);
            }

            try
            {
                var testMethods = GetTestMethods(fixtureType).Where(tm => _filter(tm.Method));
                foreach (var testMethod in testMethods)
                {
                    testResults.Add(await ExecuteTestMethod(instance, testMethod));
                }
            }
            finally
            {
                RedirectedConsole.Release();
                CallDisposeMethod(instance);
            }

            return testResults.ToArray();

            void CallDisposeMethod(object fixture)
            {
                if (fixture is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }

        private TestMethod[] GetTestMethods(Type fixtureType)
        {
            var exceptTheseMethods = new List<MethodInfo>();
            if (typeof(IDisposable).IsAssignableFrom(fixtureType))
            {
                var disposableMap = fixtureType.GetInterfaceMap(typeof(IDisposable));
                exceptTheseMethods.AddRange(disposableMap.TargetMethods);
            }

            var targetTestMethods = fixtureType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.DeclaringType != typeof(object) && !exceptTheseMethods.Any(tm => tm == m));

            var testMethods = new List<TestMethod>();
            foreach (var targetTestMethod in targetTestMethods)
            {
                var argumentSets = _argumentProvider(targetTestMethod).ToList();
                if (argumentSets.Count == 0)
                {
                    argumentSets.Add(Array.Empty<object>());
                }

                var testCases = argumentSets.Select(arguments => new TestCase(arguments));
                testMethods.Add(new TestMethod(targetTestMethod, testCases.ToArray()));
            }

            return testMethods.ToArray();
        }

        private void ProcessTestResult(Summary summary)
        {
            var results = summary.TestResults;
            if (results.Length == 0)
            {
                return;
            }

            var allTestCases = results.SelectMany(r => r.TestCaseResults).ToArray();
            var totalCount = allTestCases.Length;
            var failedCount = allTestCases.Count(r => r.Exception != null);
            var passedCount = totalCount - failedCount;
            var totalElapsed = summary.TotalDuration;
            var maxWidth = results.Select(r => $"{r.Fixture}.{r.Name}".Length).OrderBy(l => l).Last() + 5;

            foreach (var testResult in results)
            {
                var name = $"{testResult.Fixture}.{testResult.Name}";
                Console.WriteLine($"{name.PadRight(maxWidth, ' ')} {(int)testResult.Duration.TotalMilliseconds}ms");

                if (testResult.TestCaseResults.Length > 1 && testResult.TestCaseResults[0].Arguments.Length > 0)
                {
                    foreach (var testCase in testResult.TestCaseResults)
                    {
                        FormatTestCase(testResult, testCase);
                    }
                }
                else
                {
                    WriteOutput(testResult.TestCaseResults.Single());
                }
            }

            Console.WriteLine($"Total tests: {totalCount}. Passed: {passedCount}. Failed: {failedCount}.");
            if (failedCount > 0)
            {
                WriteFailed("Test Run Failed.");
            }
            else
            {
                WriteSuccessful("Test Run Successful.");
            }

            Console.WriteLine($"Test execution time {totalElapsed.TotalSeconds} seconds");

            void FormatTestCase(TestMethodResult testMethodResult, TestCaseResult testCase)
            {
                if (testCase.Arguments.Length > 0)
                {
                    var argumentString = _argumentsFormatter(testCase.Arguments);
                    var name = $"* {testMethodResult.Fixture}.{testMethodResult.Name}{argumentString}";
                    Console.WriteLine(name);
                    Console.WriteLine($"{string.Empty.PadRight(maxWidth, ' ')} {(int)testCase.Duration.TotalMilliseconds}ms");
                }
                else
                {
                    var name = $"{testMethodResult.Fixture}.{testMethodResult.Name}";
                    Console.WriteLine($"{name.PadRight(maxWidth, ' ')} {(int)testCase.Duration.TotalMilliseconds}ms");
                }

                WriteOutput(testCase);
            }

            void WriteOutput(TestCaseResult testCase)
            {
                if (!string.IsNullOrWhiteSpace(testCase.StandardOut))
                {
                    Console.WriteLine("Standard Out");
                    Console.WriteLine(testCase.StandardOut);
                }

                if (!string.IsNullOrWhiteSpace(testCase.StandardError))
                {
                    Console.WriteLine("Standard Error");
                    Console.WriteLine(testCase.StandardError);
                }

                if (testCase.Exception != null)
                {
                    Console.WriteLine("Exception");
                    WriteFailed(testCase.Exception.ToString());
                }
            }

            void WriteSuccessful(string value)
            {
                var oldColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(value);
                Console.ForegroundColor = oldColor;
            }

            void WriteFailed(string value)
            {
                var oldColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(value);
                Console.ForegroundColor = oldColor;
            }
        }

        internal static class RedirectedConsole
        {
            private static AsyncTextWriter _standardOutputWriter;
            private static AsyncTextWriter _standardErrorWriter;
            private static TextWriter _oldStandardOutWriter;
            private static TextWriter _oldStandardErrorWriter;

            private static int _captureCount;

            public static string CurrentStandardOut => _standardOutputWriter.CurrentValue;

            public static string CurrentStandardError => _standardErrorWriter.CurrentValue;

            public static void Capture()
            {
                if (_captureCount > 0)
                {
                    _captureCount++;
                    return;
                }

                _standardOutputWriter = new AsyncTextWriter();
                _standardErrorWriter = new AsyncTextWriter();
                _oldStandardOutWriter = Console.Out;
                _oldStandardErrorWriter = Console.Error;
                Console.SetOut(_standardOutputWriter);
                Console.SetError(_standardErrorWriter);
                _captureCount++;
            }

            public static void Release()
            {
                _captureCount--;
                if (_captureCount == 0)
                {
                    Console.SetOut(_oldStandardOutWriter);
                    Console.SetError(_oldStandardErrorWriter);
                }
            }

            public static void Clear()
            {
                _standardOutputWriter.Clear();
                _standardErrorWriter.Clear();
            }
        }
    }

    /// <summary>
    /// Represents a test case.
    /// </summary>
    public class TestCase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TestCase"/> class.
        /// </summary>
        /// <param name="arguments">A list of values to be passed as arguments to the target test method.</param>
        public TestCase(object[] arguments)
        {
            Arguments = arguments;
        }

        /// <summary>
        /// Gets a list of values to be passed as arguments to the target test method.
        /// </summary>
        public object[] Arguments { get; }
    }

    /// <summary>
    /// Represents a test method.
    /// </summary>
    public class TestMethod
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TestMethod"/> class.
        /// </summary>
        /// <param name="testMethod">The <see cref="MethodInfo"/> representing the test method.</param>
        /// <param name="testCases">A list of test cases to be executed as part of this test method.</param>
        public TestMethod(MethodInfo testMethod, TestCase[] testCases)
        {
            Method = testMethod;
            TestCases = testCases;
        }

        /// <summary>
        /// Gets the <see cref="MethodInfo"/> representing the test method.
        /// </summary>
        public MethodInfo Method { get; }

        /// <summary>
        /// Gets a list of test cases to be executed as part of this test method.
        /// </summary>
        public TestCase[] TestCases { get; }
    }

    /// <summary>
    /// Represents the result of a test run.
    /// </summary>
    public class Summary
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Summary"/> class.
        /// </summary>
        /// <param name="totalDuration">The total duration of the test run.</param>
        /// <param name="testResults">A list of test method result that contains the result for each test method.</param>
        public Summary(TimeSpan totalDuration, TestMethodResult[] testResults)
        {
            TotalDuration = totalDuration;
            TestResults = testResults;
        }

        /// <summary>
        /// Gets total duration of the test run.
        /// </summary>
        public TimeSpan TotalDuration { get; }

        /// <summary>
        /// Gets a list of test method result that contains the result for each test method.
        /// </summary>
        public TestMethodResult[] TestResults { get; }
    }

    /// <summary>
    /// Represents the result of executing a <see cref="TestMethod"/>.
    /// </summary>
    public class TestMethodResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TestMethodResult"/> class.
        /// </summary>
        /// <param name="fixture">The name of the test fixture (test class type).</param>
        /// <param name="name">The name of the test method.</param>
        /// <param name="duration">The duration of the test method execution.</param>
        /// <param name="testCaseResults">A list of test cases executed as part of this test method. <seealso cref="TestCase"/>.</param>
        public TestMethodResult(string fixture, string name, TimeSpan duration, TestCaseResult[] testCaseResults)
        {
            Fixture = fixture;
            Name = name;
            Duration = duration;
            TestCaseResults = testCaseResults;
        }

        /// <summary>
        /// Gets the name of the test fixture (test class type).
        /// </summary>
        public string Fixture { get; }

        /// <summary>
        /// Gets the name of the test method.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the duration of the test method execution.
        /// </summary>
        public TimeSpan Duration { get; }

        /// <summary>
        /// Gets a list of test cases executed as part of this test method. <seealso cref="TestCase"/>.
        /// </summary>
        public TestCaseResult[] TestCaseResults { get; }
    }

    /// <summary>
    /// Represents the result of executing a <see cref="TestCase"/>.
    /// </summary>
    public class TestCaseResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TestCaseResult"/> class.
        /// </summary>
        /// <param name="duration">The duration of the test case execution.</param>
        /// <param name="standardOut">The standard output captured during test case execution.</param>
        /// <param name="standardError">The standard error captured during test case execution.</param>
        /// <param name="arguments">The arguments passed to the target test method.</param>
        /// <param name="exception">The <see cref="Exception "/> caught, if any, during test case execution.</param>
        public TestCaseResult(
            TimeSpan duration,
            string standardOut,
            string standardError,
            object[] arguments,
            Exception exception = null)
        {
            Duration = duration;
            StandardOut = standardOut;
            StandardError = standardError;
            Arguments = arguments;
            Exception = exception;
        }

        /// <summary>
        /// Gets the duration of the test case execution.
        /// </summary>
        public TimeSpan Duration { get; }

        /// <summary>
        /// Gets the standard output captured during test case execution.
        /// </summary>
        public string StandardOut { get; }

        /// <summary>
        /// Gets the standard error captured during test case execution.
        /// </summary>
        public string StandardError { get; }

        /// <summary>
        /// Gets the arguments passed to the target test method.
        /// </summary>
        public object[] Arguments { get; }

        /// <summary>
        /// Gets the <see cref="Exception "/> caught, if any, during test case execution.
        /// </summary>
        public Exception Exception { get; }
    }

    /// <summary>
    /// A <see cref="TextWriter"/> that writes to the current logical thread context.
    /// </summary>
    public class AsyncTextWriter : TextWriter
    {
        private readonly AsyncLocal<StringBuilder> _output = new AsyncLocal<StringBuilder>();

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncTextWriter"/> class.
        /// </summary>
        public AsyncTextWriter()
        {
            Clear();
        }

        /// <summary>
        /// Gets the current value from the logical thread context.
        /// </summary>
        public string CurrentValue => _output.Value.ToString();

        /// <summary>
        /// Gets the <see cref="Encoding"/>.
        /// </summary>
        public override Encoding Encoding { get; } = Encoding.Default;

        /// <summary>
        /// Write the <see cref="char"/> value to the underlying <see cref="StringBuilder"/>.
        /// </summary>
        /// <param name="value">The value to write.</param>
        public override void Write(char value)
        {
            _output.Value.Append(value);
        }

        /// <summary>
        /// Clears the current underlying <see cref="StringBuilder"/>.
        /// </summary>
        public void Clear() => _output.Value = new StringBuilder();
    }

    /// <summary>
    /// Provides access to "standard out" and "standard error" from with a test method.
    /// </summary>
    public class TestContext
    {
        /// <summary>
        /// Gets the "standard out" for the currently executing test.
        /// </summary>
        public static string StandardOut => TestRunner.RedirectedConsole.CurrentStandardOut;

        /// <summary>
        /// Gets the "standard error" for the currently executing test.
        /// </summary>
        public static string StandardError => TestRunner.RedirectedConsole.CurrentStandardError;
    }

    /// <summary>
    /// Specifies the arguments to be used when executing the test method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class ArgumentsAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ArgumentsAttribute"/> class.
        /// </summary>
        /// <param name="arguments">A list of values to be passed as arguments to the target test method.</param>
        public ArgumentsAttribute(params object[] arguments)
        {
            Arguments = arguments;
        }

        /// <summary>
        /// Gets a list of values to be passed as arguments to the target test method.
        /// </summary>
        public object[] Arguments { get; }
    }

     public class DisposableFolder : IDisposable
    {
        public DisposableFolder()
        {
            var tempFolder = System.IO.Path.GetTempPath();
            this.Path = System.IO.Path.Combine(tempFolder, System.IO.Path.GetFileNameWithoutExtension(System.IO.Path.GetTempFileName()));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            RemoveDirectory(Path);

            void RemoveDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }
        NormalizeAttributes(path);

        foreach (string directory in Directory.GetDirectories(path))
        {
            RemoveDirectory(directory);
        }

        try
        {
            Directory.Delete(path, true);
        }
        catch (IOException)
        {
            Directory.Delete(path, true);
        }
        catch (UnauthorizedAccessException)
        {
            Directory.Delete(path, true);
        }

        void NormalizeAttributes(string directoryPath)
        {
            string[] filePaths = Directory.GetFiles(directoryPath);
            string[] subdirectoryPaths = Directory.GetDirectories(directoryPath);

            foreach (string filePath in filePaths)
            {
                File.SetAttributes(filePath, FileAttributes.Normal);
            }
            foreach (string subdirectoryPath in subdirectoryPaths)
            {
                NormalizeAttributes(subdirectoryPath);
            }
            File.SetAttributes(directoryPath, FileAttributes.Normal);
        }
    }
        }
    }
}