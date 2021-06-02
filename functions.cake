using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

public class ProjectTaskConfigurationManager
{
	readonly IDictionary<string, HashSet<string>> _taskConfigLookup = new Dictionary<string, HashSet<string>>()
	{
		{ "Clean"	,     new HashSet<string>(new string[] { "App", "Package", "Test" }) },
		{ "Build"	,     new HashSet<string>(new string[] { "App", "Package", "Test" }) },
		{ "Test"	,     new HashSet<string>(new string[] { "Test" }) },
		{ "Pack"    ,     new HashSet<string>(new string[] { "App", "Package" }) },
		{ "Publish" ,     new HashSet<string>(new string[] { "App", "Package" }) },
	};

	bool CanDoTask(string taskName, string projectType) => _taskConfigLookup[taskName].Contains(projectType);

	public bool CanClean(ProjectConfigDescriptor projectConfig) => CanDoTask("Clean", projectConfig.Type);

    public bool CanBuild(ProjectConfigDescriptor projectConfig) => CanDoTask("Build", projectConfig.Type);

    public bool CanTest(ProjectConfigDescriptor projectConfig) => CanDoTask("Test", projectConfig.Type);

    public bool CanPack(ProjectConfigDescriptor projectConfig) => CanDoTask("Pack", projectConfig.Type);

    public bool CanPublish(ProjectConfigDescriptor projectConfig) => CanDoTask("Publish", projectConfig.Type);
}

public static class ProjectLoader
{
	public static ProjectContainerDescriptor Load(ICakeContext context, string configFilePath, string basePath, string configuration)
	{
		var configLookup = context.DeserializeYamlFromFile<IDictionary<string, IDictionary<string, string>>>(configFilePath);

        var container = new ProjectContainerDescriptor();

		foreach(var projectPair in configLookup)
		{
            var configDescriptor = ProjectConfigDescriptor.Create(projectPair.Key, projectPair.Value[nameof(ProjectConfigDescriptor.Type)]);

            var projectFilePath = context.BuildProjectFilePath(basePath, configDescriptor);
            var documentDescriptor = context.ParseProject(projectFilePath, configuration);

            var projectDescriptor = ProjectDescriptor.New()
                .WithConfigDescriptor(configDescriptor)
                .WithDocumentDescriptor(documentDescriptor);

			container.Projects.Add(projectDescriptor);
		}

        return container;
	}
}

public class ProjectContainerDescriptor
{
	public ProjectContainerDescriptor()
	{
		Projects = new List<ProjectDescriptor>();
	}

	public IList<ProjectDescriptor> Projects { get; }

	public void Add(ProjectDescriptor projectDescriptor) => Projects.Add(projectDescriptor);
}

public class ProjectDescriptor
{
    private ProjectDescriptor() {}

    public ProjectConfigDescriptor Config { get; private set; }

    public ProjectDocumentDescriptor Document { get; private set; }

    public static ProjectDescriptor New() => new ProjectDescriptor();

    public ProjectDescriptor WithConfigDescriptor(ProjectConfigDescriptor config)
    {
        Config = config;
        return this;
    }

    public ProjectDescriptor WithDocumentDescriptor(ProjectDocumentDescriptor document)
    {
        Document = document;
        return this;
    }
}

public class ProjectConfigDescriptor
{
	protected ProjectConfigDescriptor(string name, string type)
	{
		Name = name;
		Type = type;
	}

	public string Name { get; }

	public string Type { get; }

	public static ProjectConfigDescriptor Create(string name, string type) => new ProjectConfigDescriptor(name, type);
}

public static string BuildProjectFilePath(this ICakeContext context, string basePath, ProjectConfigDescriptor projectConfig) =>
    $"{basePath}/{projectConfig.Name}/{projectConfig.Name}.csproj";

public static string BuildProjectDirPath(this ICakeContext context, string basePath, ProjectConfigDescriptor projectConfig) =>
    $"{basePath}/{projectConfig.Name}";

public static string BuildNugetPackagePath(this ICakeContext context, DirectoryPath artifactsPath, ProjectDescriptor project, string version) =>
    $"{context.MakeAbsolute(artifactsPath).FullPath}/{project.Document.DotNet.PackageId}.{version}.nupkg";

/////// Start Project Document Descriptor Code via: https://github.com/cake-contrib/Cake.Incubator/blob/develop/src/Cake.Incubator/Project/ProjectParserExtensions.cs ///////

public static ProjectDocumentDescriptor ParseProject(this ICakeContext context, FilePath project, string configuration)
{
    return context.ParseProject(project, configuration, "AnyCPU");
}

public static ProjectDocumentDescriptor ParseProject(this ICakeContext context, FilePath project, string configuration, string platform)
{
    if (project.IsRelative)
    {
        project = project.MakeAbsolute(context.Environment);
    }

    var projectFile = context.FileSystem.GetProjectFile(project);
    var result = projectFile.ParseProjectFile(configuration, platform);

    return result;
}

public static ProjectDocumentDescriptor ParseProjectFile(this IFile projectFile, string configuration, string platform = "AnyCPU")
{
    var document = projectFile.LoadXml();

    return document.ParseSdkProjectFile(projectFile, configuration, platform);
}

public class ProjectDocumentDescriptor
{
    public XDocument ProjectXml { get; set; }
    public string AssemblyName { get; set; }
    public string ProjectFileFullPath { get; set; }
    public FilePath ProjectFilePath { get; set; }
    public string[] TargetFrameworkVersions { get; set; }
    public bool IsNetCore { get; set; }
    public bool IsNetFramework { get; set; }
    public bool IsNetStandard { get; set; }
    public DotNetProjectDescriptor DotNet { get; set; }
}

public class DotNetProjectDescriptor
{
    public string AssemblyTitle { get; set; }
    public bool IsPackable { get; set; } = true;
    public bool IsTool { get; set; }
    public bool IsWeb { get; set; }
    public string NetStandardImplicitPackageVersion { get; set; }
    public string PackageId { get; set; }
    public string Sdk { get; set; }
    public string[] TargetFrameworks { get; set; }
    public bool PackAsTool { get; set; }
}

// internal static class ProjectParserExtensions
// {
    static readonly Regex NetCoreTargetFrameworkRegex = new Regex("([Nn][Ee][Tt])([Cc])\\w+", RegexOptions.Compiled);
    static readonly Regex NetStandardTargetFrameworkRegex = new Regex("([Nn][Ee][Tt])([Ss])\\w+", RegexOptions.Compiled);
    static readonly Regex NetFrameworkTargetFrameworkRegex = new Regex("([Nn][Ee][Tt][0-9*])\\w+", RegexOptions.Compiled);

    public static ProjectDocumentDescriptor ParseSdkProjectFile(this XDocument document, IFile projectFile, string config, string platform)
    {
        var sdk = document.GetSdk();
        var projectName = projectFile.Path.GetFilenameWithoutExtension().ToString();
        var targetFramework = document.GetFirstElementValue(ProjectXElement.TargetFramework);
        var targetFrameworks =
            document.GetFirstElementValue(ProjectXElement.TargetFrameworks)?.SplitIgnoreEmpty(';') ??
            (targetFramework != null ? new[] { targetFramework } : new string[0]);
        var assemblyName = document.GetFirstElementValue(ProjectXElement.AssemblyName) ?? projectName;
        var packageId = document.GetFirstElementValue(ProjectXElement.PackageId) ?? assemblyName;
        var netstandardVersion = document.GetFirstElementValue(ProjectXElement.NetStandardImplicitPackageVersion);
        if (!bool.TryParse(document.GetFirstElementValue(ProjectXElement.IsPackable), out var isPackable))
        {
            isPackable = true;
        }
        bool.TryParse(document.GetFirstElementValue(ProjectXElement.IsTool), out var isTool);
        bool.TryParse(document.GetFirstElementValue(ProjectXElement.PackAsTool), out var packAsTool);
        var isNetCore = targetFrameworks.Any(x => NetCoreTargetFrameworkRegex.IsMatch(x));
        var isNetStandard = targetFrameworks.Any(x => NetStandardTargetFrameworkRegex.IsMatch(x));
        var isNetFramework = targetFrameworks.Any(x => NetFrameworkTargetFrameworkRegex.IsMatch(x));

        return new ProjectDocumentDescriptor
        {
            ProjectXml = document,
            ProjectFilePath = projectFile.Path,
            ProjectFileFullPath = projectFile.Path.FullPath,
            AssemblyName = assemblyName,
            TargetFrameworkVersions = targetFrameworks,
            IsNetCore = isNetCore,
            IsNetStandard = isNetStandard,
            IsNetFramework = isNetFramework,
            DotNet = new DotNetProjectDescriptor
            {
                IsPackable = isPackable,
                IsTool = isTool,
                PackAsTool = packAsTool,
                IsWeb = sdk.EqualsIgnoreCase("Microsoft.NET.Sdk.Web"),
                NetStandardImplicitPackageVersion = netstandardVersion,
                PackageId = packageId,
                Sdk = sdk,
                TargetFrameworks = targetFrameworks
            }
        };
    }
// }

// internal static class XDocumentExtensions
// {

    internal static bool IsDotNetSdk(this XDocument document) => document.GetSdk() != null;

    internal static string GetSdk(this XDocument document) => document.Root?.Attribute("Sdk", true)?.Value;

    internal static string GetFirstElementValue(this XDocument document, XName elementName, string config = null, string platform = "AnyCPU")
    {
        var elements = document.Descendants(elementName);
        if (!elements.Any()) return null;

        if (string.IsNullOrEmpty(config))
            return elements.FirstOrDefault(x => !x.WithConfigCondition())?.Value;

        return elements.FirstOrDefault(x => x.WithConfigCondition(config, platform))
            ?.Value ?? elements.FirstOrDefault(x => !x.WithConfigCondition())?.Value;
    }
// }

// internal static class XElementExtensions
// {
    internal static bool WithConfigCondition(this XElement element, string config = null, string platform = null)
    {
        var configAttribute = element.Attribute("Condition")?.Value.HasConfigPlatformCondition(config, platform);
        if(!configAttribute.HasValue) configAttribute = element.Parent?.Attribute("Condition")?.Value.HasConfigPlatformCondition(config, platform);
        return configAttribute ?? false;
    }

    internal static XAttribute Attribute(this XElement element, XName name, bool ignoreCase)
    {
        var el = element.Attribute(name);
        if (el != null)
            return el;

        if (!ignoreCase)
            return null;

        var attributes = element.Attributes().Where(e => e.Name.LocalName.EqualsIgnoreCase(name.ToString()));
        return !attributes.Any() ? null : attributes.First();
    }
// }

// internal static class FileExtensions
// {
    public static XDocument LoadXml(this IFile xmlFile)
    {
        XDocument document;
        using (var stream = xmlFile.OpenRead())
        {
            document = XDocument.Load(stream);
        }
        return document;
    }
// }

// internal static class FileSystemExtensions
// {
    internal static IFile GetProjectFile(this IFileSystem fs, FilePath projectPath)
    {
        var file = fs.GetFile(projectPath);
        if (!file.Exists)
        {
            const string format = "Project file '{0}' does not exist.";
            var message = string.Format(CultureInfo.InvariantCulture, format, projectPath.FullPath);
            throw new CakeException(message);
        }

        if (!file.Path.HasExtension)
        {
            throw new CakeException("Project file type could not be determined by extension.");
        }
        return file;
    }
// }

// internal static class StringExtensions
// {

    private const string ConfigPlatformCondition = "'$(Configuration)|$(Platform)'==";

    public static bool IsNullOrEmpty(this string value) => string.IsNullOrEmpty(value);

    public static bool EqualsIgnoreCase(this string source, string value) =>
        source.Equals(value, StringComparison.OrdinalIgnoreCase);

    public static string[] SplitIgnoreEmpty(this string value, params char[] separator) =>
        value.IsNullOrEmpty() ? new string[0] : value.Split(separator, StringSplitOptions.RemoveEmptyEntries);

    internal static bool HasConfigPlatformCondition(this string condition, string config = null, string platform = null) =>
        config.IsNullOrEmpty()
            ? condition.StartsWith(ConfigPlatformCondition)
            : condition.EqualsIgnoreCase($"{ConfigPlatformCondition}'{config}|{platform}'");
// }

internal static class ProjectXElement
{
    internal const string AssemblyName = "AssemblyName";
    internal const string IsPackable = "IsPackable";
    internal const string IsTool = "IsTool";
    internal const string NetStandardImplicitPackageVersion = "NetStandardImplicitPackageVersion";
    internal const string PackAsTool = "PackAsTool";
    internal const string PackageId = "PackageId";
    internal const string TargetFramework = "TargetFramework";
    internal const string TargetFrameworks = "TargetFrameworks";
}

/////// End Project Document Descriptor Code ///////