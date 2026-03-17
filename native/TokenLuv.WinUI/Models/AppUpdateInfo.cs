namespace TokenLuv.WinUI.Models;

public sealed record AppUpdateInfo(
    string CurrentVersion,
    string LatestVersion,
    string ReleaseUrl);
