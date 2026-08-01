namespace Ets2SaveEditor.App;

/// <summary>Single source of truth for app version and GitHub release endpoints.</summary>
internal static class AppInfo
{
    public const string Version = "1.2.0";
    public const string GitHubOwner = "motionarium";
    public const string GitHubRepo = "TSSaveEditor";

    public static string VersionLabel => "v" + Version;
    public static string ReleasesApiUrl =>
        $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
    public static string ReleasesPageUrl =>
        $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases/latest";
}
