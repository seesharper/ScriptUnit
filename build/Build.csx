#! "netcoreapp2.0"
#load "Command.csx"

using System.Runtime.CompilerServices;

var scriptFolder = GetScriptFolder();
var tempFolder = Path.Combine(scriptFolder,"tmp");
RemoveDirectory(tempFolder);

var contentFolder = Path.Combine(tempFolder,"contentFiles","csx","any");
Directory.CreateDirectory(contentFolder);

File.Copy(Path.Combine(scriptFolder,"..","src","ScriptUnit","ScriptUnit.csx"), Path.Combine(contentFolder,"main.csx"));
File.Copy(Path.Combine(scriptFolder,"ScriptUnit.nuspec"),Path.Combine(tempFolder,"ScriptUnit.nuspec"));

string pathToUnitTests = Path.Combine(scriptFolder,"..","src","ScriptUnit.Tests","ScriptUnitTests.csx");
Command.Execute("dotnet", $"script {pathToUnitTests}");

string pathToTopLevelTests = Path.Combine(scriptFolder,"..","src","ScriptUnit.Tests","TopLevelTests.csx");
Command.Execute("dotnet", $"script {pathToTopLevelTests}");

Command.Execute("nuget",$"pack {Path.Combine(tempFolder,"ScriptUnit.nuspec")} -OutputDirectory {tempFolder}");



static string GetScriptPath([CallerFilePath] string path = null) => path;
static string GetScriptFolder() => Path.GetDirectoryName(GetScriptPath());

static void RemoveDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        // http://stackoverflow.com/questions/329355/cannot-delete-directory-with-directory-deletepath-true
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
    }