# Development setup

1. Clone the `develop` branch and open the repository in VS Code:

   ```powershell
   git clone --branch develop https://github.com/VainoKarppi/EdenOnline.git
   code EdenOnline
   ```

2. Install the **Hemtt** extension for Visual Studio Code, if it is not already installed.
3. Download the latest Arma3SqfReloader VSIX from [Releases](https://github.com/VainoKarppi/Arma3SqfReloader/releases). In VS Code, open the Extensions view, select **...**, choose **Install from VSIX...**, and select the downloaded file.
4. Install the .NET SDK version specified in [`global.json`](global.json). This project currently requires .NET SDK `10.0.100` (or a compatible later feature band).
5. On Windows, install Visual Studio 2022 Build Tools with the **Desktop development with C++** workload. This workload is required to build the NativeAOT extension. You can install it from a terminal with:

   ```powershell
   winget install --id Microsoft.VisualStudio.2022.BuildTools --override "--wait --passive --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended"
   ```

6. After installation, close and restart VS Code so it can detect the installed SDK and build tools.
7. Use **Play Arma 3** in the SQF Reloader menu at the bottom of VS Code to start Arma.

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
