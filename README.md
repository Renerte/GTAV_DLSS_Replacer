# GTAV DLSS Replacer

> Applies to GTAV Enhanced ***only!*** - Legacy doesn't use DLSS.

Rockstar Launcher replaces DLSS file with their own version every time you start Grand Theft Auto V. This app bypasses this problem, so you can use your preferred DLSS version easily.

Run it before launching GTAV, it will replace DLSS with your preferred version, and when you exit GTAV it will restore original DLSS file so that Rockstar Launcher does not update the game every time you launch or exit game.

## How to use
1. Download from: https://github.com/Renerte/GTAV_DLSS_Replacer/releases/latest
2. Extract it.
3. Run `GTAV_DLSS_Replacer.exe`
   - It needs Administrator Privileges to replace `nvngx_dlss.dll` in GTAV directory.
   - It requires .NET Runtime installed. [Download from Microsoft.](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-8.0.13-windows-x64-installer)
   - It will download `nvngx_dlss.dll` automatically when you run it for the first time. 
     - (Optional) If you want to use your own preferred DLSS file then paste it in the folder of GTAV_DLSS_Replacer.exe and name it `nvngx_dlss.dll`
4. Start Grand Theft Auto V game.

`GTAV_DLSS_Replacer.exe` can be kept open for as long as you want, it does not exit with the game, so you can launch or exit the game multiple times and it will keep processing.

_(Optional)_ **Auto launch GTAV** when you start this app.
   - Open `gtaV_location_for_auto_start.txt`
   - Provide absolute location to GTAV.exe at end of file.

## How it works
It monitors `GTAV.exe` process in task manager an as soon as it finds the process, it immediately replaces the game's DLSS in the game's location and creates backup of original DLSS file. Similarly, when the `GTAV.exe` process exits it reverts back DLSS. Your game location can be anywhere, it will be detected automatically by this app.