# Signing with MicroBuild

## How to get MicroBuild/DDSignFiles.dll in a pipeline

Linux pipeline:

```yaml
steps:
- task: MicroBuildSigningPlugin@4
  displayName: Install MicroBuild plugin
  inputs:
    ${{ if parameters.isTest }}:
      signType: test
    ${{ else }}:
      signType: real
    zipSources: false
    feedSource: https://dnceng.pkgs.visualstudio.com/_packaging/MicroBuildToolset/nuget/v3/index.json
    ConnectedServiceName: 'MicroBuild Signing Task (DevDiv)'
    # Linux
    ConnectedPMEServiceName: c24de2a5-cc7a-493d-95e4-8e5ff5cad2bc
    # Windows
    # ConnectedPMEServiceName: 248d384a-b39b-46e3-8ad5-c2c210d5e7ca
  env:
    TeamName: DotNetCore
    MicroBuildOutputFolderOverride: $(Agent.TempDirectory)
    SYSTEM_ACCESSTOKEN: $(System.AccessToken)
  continueOnError: false
```

Then, we need to mount the MicroBuild directory into the ImageBuilder container image.
This will let us use DDSignFiles.dll from within the container.
In Windows we don't need to mount the directory since ImageBuilder runs natively on Windows.
