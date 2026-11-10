# Development setup

1. Clone the `develop` branch and open the repository in VS Code:

   ```powershell
   git clone --branch develop https://github.com/VainoKarppi/EdenOnline.git
   code EdenOnline
   ```

2. Install the **Hemtt** extension for Visual Studio Code, if it is not already installed.
3. Download the latest Arma3SqfReloader VSIX from [Releases](https://github.com/VainoKarppi/Arma3SqfReloader/releases). In VS Code, open the Extensions view, select **...**, choose **Install from VSIX...**, and select the downloaded file.
4. Check the `TargetFramework` in [`EdenOnline.csproj`](EdenOnline.csproj). It is currently `net10.0`, so install the .NET 10 SDK from the [official .NET downloads page](https://dotnet.microsoft.com/en-us/download/dotnet/10.0). To see which SDKs are installed, run:

   ```powershell
   dotnet --list-sdks
   ```

   Ensure the output includes a .NET 10 SDK.
5. On Windows, install Visual Studio 2022 Build Tools with the **Desktop development with C++** workload. This workload is required to build the NativeAOT extension. You can install it from a terminal with:

   ```powershell
   winget install --id Microsoft.VisualStudio.2022.BuildTools --override "--wait --passive --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended"
   ```

   This opens the Visual Studio Installer, which installs the tools automatically. Keep the installer window open until installation finishes; this can take up to 5 minutes.

   If installation fails, check the Visual Studio Installer logs in your temporary folder (`%TEMP%`). Look for files whose names start with `dd` and end in `.log` (for example, `dd_setup_..._errors.log`).

   To verify that the C++ tools were installed without building the project, run this in PowerShell:

   ```powershell
   $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
   & $vswhere -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
   ```

   If the command prints an installation path, the C++ tools are installed. If it prints nothing, check Visual Studio Installer to confirm the **Desktop development with C++** workload is selected.

6. If you haven't installed it already, download [Arma 3 Tools from Steam](https://store.steampowered.com/app/233800/Arma_3_Tools/) and run it at least once.
7. After installation, close and restart VS Code so it can detect the installed SDK and build tools, or run **Developer: Reload Window** from the Command Palette.
8. Use **Play Arma 3** in the SQF Reloader menu at the bottom of VS Code to start Arma.

## Editing the extension

Extension methods exposed to Arma are defined in [`ArmaMethods.cs`](src/ArmaPlugin/ArmaMethods/ArmaMethods.cs). For example, add this method to the `ArmaMethods` class:

```csharp
public static string MyTest(int myArmaValue)
{
    return $"Response with data: {myArmaValue}";
}
```

Build the extension and restart Arma after changing the extension DLL. Profiling-branch hot reload is not currently supported, so Arma must be relaunched to load DLL updates.

To call the new method, run this in the Arma debug console or the VS Code Arma debug console:

```sqf
_data = ["MyTest", [1]] call EOEX_fnc_callExtension;
_data == "Response with data: 1"
```
