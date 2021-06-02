#addin nuget:?package=YamlDotNet&version=8.1.2
#addin nuget:?package=System.Xml.XDocument&version=4.3.0
#addin nuget:?package=Cake.MinVer&version=1.0.0-rc0001
#addin nuget:?package=Cake.Yaml&version=3.1.1
#addin nuget:?package=Cake.Docker&version=0.11.1

#load "./functions.cake"

var basePath = "./src";
var organization = "appyway";
var artifactsPath = Context.Directory("./.artifacts");
var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var configFilePath = "config.yml";
var taskConfigManager = new ProjectTaskConfigurationManager();
var projectDescriptors = ProjectLoader.Load(Context, configFilePath, basePath, configuration).Projects;
var version = MinVer(settings => settings
    .WithDefaultPreReleasePhase("preview")
    .WithVerbosity(MinVerVerbosity.Info));

////////////////////////////////////////////////////////////////
// Setup

Setup((context) =>
{
    Information("AppyWay");
    Information($"Version: {version.Version}");
});

////////////////////////////////////////////////////////////////
// Tasks

Task("Clean")
    .Does(context =>
{
    context.CleanDirectory(artifactsPath);
});

Task("Restore")
    .Does(() =>
{
    DotNetCoreRestore(basePath,
        new DotNetCoreRestoreSettings
        {
            Verbosity = DotNetCoreVerbosity.Minimal
        });
});

Task("Build-Project")
    .IsDependentOn("Restore")
    .Does(context =>
{
    foreach(var projectDescriptor in projectDescriptors)
    {
        if (!taskConfigManager.CanBuild(projectDescriptor.Config)) continue;

        var projectFilePath = projectDescriptor.Document.ProjectFileFullPath;

        DotNetCoreBuild(projectFilePath, new DotNetCoreBuildSettings {
            Configuration = configuration,
            NoRestore = true,
            NoIncremental = context.HasArgument("rebuild"),
            MSBuildSettings = new DotNetCoreMSBuildSettings()
                .TreatAllWarningsAs(MSBuildTreatAllWarningsAs.Error)
        });
    }
});

Task("Test")
    .IsDependentOn("Build-Project")
    .Does(context =>
{
    foreach(var projectDescriptor in projectDescriptors)
    {
        if (!taskConfigManager.CanTest(projectDescriptor.Config)) continue;

        var projectFilePath = projectDescriptor.Document.ProjectFileFullPath;

        DotNetCoreTest(projectFilePath, new DotNetCoreTestSettings {
            Configuration = configuration,
            NoRestore = true,
            NoBuild = true,
            TestAdapterPath = ".",
            Loggers = new string[] {
                // $"xunit;LogFilePath={MakeAbsolute(artifactsPath).FullPath}/xunit-{projectDescriptor.Config.Name}.xml",
                "GitHubActions;report-warnings=false"
            },
            Verbosity = DotNetCoreVerbosity.Quiet
        });
    }
});

Task("Package")
    .IsDependentOn("Test")
    .Does(context =>
{
    foreach(var projectDescriptor in projectDescriptors)
    {
        if (!taskConfigManager.CanPack(projectDescriptor.Config)) continue;

        var projectFilePath = projectDescriptor.Document.ProjectFileFullPath;

        context.DotNetCorePack(projectFilePath, new DotNetCorePackSettings {
            Configuration = configuration,
            NoRestore = true,
            NoBuild = true,
            OutputDirectory = artifactsPath,
            MSBuildSettings = new DotNetCoreMSBuildSettings()
                .TreatAllWarningsAs(MSBuildTreatAllWarningsAs.Error)
        });
    }
});

Task("Publish-Package-GitHub")
    .WithCriteria(ctx => BuildSystem.IsRunningOnGitHubActions, "Not running on GitHub Actions")
    .IsDependentOn("Package")
    .Does(context =>
{
    var apiKey = Argument<string>("github-key", null);
    if(string.IsNullOrWhiteSpace(apiKey)) {
        throw new CakeException("No GitHub API key was provided.");
    }

    var exitCode = 0;
    foreach(var projectDescriptor in projectDescriptors)
    {
        if (!taskConfigManager.CanPack(projectDescriptor.Config)) continue;

        var nugetPkgFilePath = context.BuildNugetPackagePath(artifactsPath, projectDescriptor, version.Version);

        context.Information("Publishing {0} to Github", nugetPkgFilePath);
        exitCode += StartProcess("dotnet",
            new ProcessSettings {
                Arguments = new ProcessArgumentBuilder()
                    .Append("gpr")
                    .Append("push")
                    .AppendQuoted(nugetPkgFilePath)
                    .AppendSwitchSecret("-k", " ", apiKey)
            }
        );
    }

    if(exitCode != 0)
    {
        throw new CakeException("Could not push GitHub packages.");
    }
});

Task("Publish-Package-NuGet")
    .WithCriteria(ctx => BuildSystem.IsRunningOnGitHubActions, "Not running on GitHub Actions")
    .IsDependentOn("Package")
    .Does(context =>
{
    var apiKey = Argument<string>("nuget-key", null);
    if(string.IsNullOrWhiteSpace(apiKey)) {
        throw new CakeException("No NuGet API key was provided.");
    }

    foreach(var projectDescriptor in projectDescriptors)
    {
        if (!taskConfigManager.CanPack(projectDescriptor.Config)) continue;

        var nugetPkgFilePath = context.BuildNugetPackagePath(artifactsPath, projectDescriptor, version.Version);

        context.Information("Publishing {0} to Nuget", nugetPkgFilePath);

        DotNetCoreNuGetPush(nugetPkgFilePath, new DotNetCoreNuGetPushSettings
        {
            Source = "https://api.nuget.org/v3/index.json",
            ApiKey = apiKey,
        });
    }
});

////////////////////////////////////////////////////////////////
// Targets

Task("Publish")
    .IsDependentOn("Publish-Package-GitHub")
    .IsDependentOn("Publish-Package-NuGet");

Task("Default")
    .IsDependentOn("Clean")
    .IsDependentOn("Build-Project")
    .IsDependentOn("Package");

////////////////////////////////////////////////////////////////
// Execution

RunTarget(target)