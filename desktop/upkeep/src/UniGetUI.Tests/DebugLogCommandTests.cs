using UniGetUI.Avalonia.Infrastructure;
using UniGetUI.PackageEngine.Operations.History;

namespace UniGetUI.Tests;

public sealed class DebugLogCommandTests
{
    [Theory]
    [InlineData("-debug")]
    [InlineData("--debug")]
    [InlineData("-DEBUG")]
    public void IsCommandRecognizesDebugFlag(string argument)
    {
        Assert.True(DebugLogCommand.IsCommand([argument]));
    }

    [Fact]
    public void IsCommandDoesNotConsumeOtherArguments()
    {
        Assert.False(DebugLogCommand.IsCommand(["-debug", "extra"]));
        Assert.False(DebugLogCommand.IsCommand([]));
    }

    [Fact]
    public void CreateReportIncludesUpdateOutputAndRedactsCredentials()
    {
        var record = new OperationHistoryRecord
        {
            Kind = "update-package",
            PackageId = "Blizzard.BattleNet",
            PackageName = "Battle.net Setup",
            ManagerName = "winget",
            SourceName = "winget",
            VersionBefore = "1.0",
            VersionAfter = "2.0",
            Status = OperationHistoryRecord.StatusFailed,
            FailureSummary = "Installer returned an error",
            ExitCode = 5,
            RanElevated = false,
            Output =
            [
                new OperationHistoryOutputLine
                {
                    Type = "Error",
                    Text = "Authorization: Bearer example-secret-value",
                },
            ],
        };

        string report = DebugLogCommand.CreateReport(new[] { record });

        Assert.Contains("Battle.net Setup", report);
        Assert.Contains("winget", report);
        Assert.Contains("Installer returned an error", report);
        Assert.Contains("Authorization: [REDACTED]", report);
        Assert.DoesNotContain("example-secret-value", report);
    }

    [Fact]
    public void CreateReportExplainsWhenNoUpdateHistoryExists()
    {
        string report = DebugLogCommand.CreateReport(Array.Empty<OperationHistoryRecord>());

        Assert.Contains("No package update records were found", report);
    }
}
