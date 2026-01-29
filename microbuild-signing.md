# Wrapping DDSignFiles.dll

Example wrapper for DDSignFiles.dll:

```cs
public partial class SigningService
{
    private readonly SigningServiceConfig _config;
    private readonly ILogger<SigningService> _logger;
    private readonly IRunCommand _runCommand;
    private const string MBSignAppFolderEnv = "MBSIGN_APPFOLDER";
    private const string DDSignFilesDllName = "DDSignFiles.dll";

    public SigningService(
        ILogger<SigningService> logger,
        IRunCommand runCommand,
        IConfigProvider configProvider)
    {
        _logger = logger;
        _runCommand = runCommand;
        _config = configProvider.GetConfigObject<SigningServiceConfig>();
    }

    public async Task ESRPSignAsync(string filesToSignDirectory, int[] certs)
    {
        _logger.LogInformation("Signing files in '{Directory}' with ESRP certificates '{}'",
            filesToSignDirectory,
            string.Join(",", certs));

        string mbsignAppFolder = Environment.GetEnvironmentVariable(MBSignAppFolderEnv)
            ?? throw new InvalidOperationException(
                $"{MBSignAppFolderEnv} environment variable is not set." +
                " Was the MicroBuild signing plugin installed?");

        string signType = _config.DryRun ? "test" : "real";
        if (signType == "test" && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _logger.LogWarning("Test signing is only available on Windows. Skipping signing in this configuration.");
            return;
        }

        string signListTempPath = Path.Combine(Path.GetTempPath(), $"SignList_{Guid.NewGuid()}.json");
        try
        {
            JsonObject signListJson = GenerateFileSignListJsonObject(Directory.GetFiles(filesToSignDirectory), certs);
            await File.WriteAllTextAsync(signListTempPath, signListJson.ToJsonString());
            RunCommandOptions esrpSignOptions = new()
            {
                Command = "dotnet",
                Arguments = new[]
                {
                    "--roll-forward", "major",
                    Path.Combine(mbsignAppFolder, DDSignFilesDllName),
                    "--",
                    $"/filelist:{signListTempPath}",
                    $"/signType:{signType}"
                },
                ThrowOnError = false
            };

            RunCommandResult esrpSignResult = await _runCommand.ExecuteAsync(esrpSignOptions);
            if (esrpSignResult.ExitCode != 0)
            {
                _logger.LogError("ESRP signing failed with exit code {ExitCode}:\n{Output}", esrpSignResult.ExitCode, esrpSignResult.Output);
                throw new InvalidOperationException($"Failed to sign. See logs for details.");
            }
            _logger.LogInformation("ESRP signing completed successfully.");
        }
        finally
        {
            if (File.Exists(signListTempPath))
            {
                File.Delete(signListTempPath);
            }
        }
    }
}
```
