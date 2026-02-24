# Arma Native C# AOT Extension

## Prerequisites

Before you begin, ensure you have the following installed:

1. **Visual Studio Build Tools**:
    - Download the Visual Studio Installer from the following link:
      [Visual Studio Build Tools](https://aka.ms/vs/17/release/vs_BuildTools.exe)
    - During installation, select the following components:
        - **MSVC v143 - VS 2022 C++ x64/x86 build tools** (or newer)
        - **Windows 11 or 10 SDK** (latest version)
        - **C++ CMake Tools for Windows**

## Setup

1. **Clone the Repository**:
    - Clone this repository to your local machine using:
      ```sh
      git clone https://github.com/VainoKarppi/ArmaExtension
      ```

2. **Configure Project**:
    - Open the `ArmaExtension.csproj` file.
    - Modify the `AssemblyName` and `Version` fields as per your requirements.

3. **Build the Project**:
    - Execute the `Tools\BuildExtension.ps1` script to build the project:
    - Alternatively, you can run the script from VSCode by selecting **Tasks: Run Build Task**.
    - If the script gets stuck at "Building the project...", press `Ctrl+C` and try running the script again.


## Running Tests
1. **Edit `base_test.sqf`**:
    - You can edit the `Tools\base_test.sqf` file as you wish, but keep the `XXXX` placeholder since this is automatically updated, when a copy of this file is moved to the `Tools\callExtension` folder.

2. **Test via using Killzone Kid's callExtension Tool**:
[KK's blog: callExtension v2.0](http://killzonekid.com/callextension-v2-0/)

    - Execute the `Tools\TestExtension.ps1` script to run the tests:
    - Alternatively, you can run the script from **VSCode** using the test task named **Test Extension**.
    - If the script gets stuck at "Building the project...", press `Ctrl+C` and try running the script again.

2. **Test via Arma**:
    - You can also run the test task named **Test With Arma** to open Arma with this extension loaded.  
    - This automatically loads the **.SQF** files into a **.PBO** from the **Examples\\@ArmaExtension\\addons** folder.  
    - **WARNING:** If you change the **AssemblyName** inside the ***.csproj*** file, you need to manually update this folder’s contents (**.sqf scripts, mod.cpp, etc.**)!



## License

This project is licensed under the MIT License. You are allowed to do whatever you want with it. See the [LICENSE](./LICENSE.MD) file for more details.
