# FolderFree

![FolderFree logo](assets/FolderFree.png)

**FolderFree is a simple Windows GUI app for the annoying moment when Windows says a folder cannot be renamed, deleted, moved, or modified because it is open in another program.**

Pick the folder, scan it, see which apps are holding it, and free the folder without guessing which background process is blocking you.

## The Problem It Solves

Windows often blocks folder actions with messages like:

- "The action cannot be completed because the folder or a file in it is open in another program."
- "Folder in use."
- "Access is denied."
- "Cannot delete folder because it is being used by another process."
- "Cannot rename this folder."

The usual fix is painful: close random apps, restart Explorer, open Task Manager, or reboot the whole PC.

FolderFree gives you a focused tool for that one job.

## How Easy It Is To Use

1. Download or open `Release/FolderFree.exe`.
2. Select the folder that Windows will not let you rename, delete, move, or edit.
3. Click **Scan locks**.
4. Select the locking apps.
5. Click **Free selected** or **Free all**.
6. Try renaming, deleting, moving, or modifying the folder again.

For stubborn locks, click **Run as administrator** inside the app and scan again.

## How It Works

FolderFree uses the Windows Restart Manager API to ask Windows which processes are using files inside the selected folder.

It then shows those apps in a clean table with:

- app name
- process ID
- number of locks found
- executable or sample locked path
- release status

When you choose to free a folder, FolderFree first asks the app to close normally. If needed, and if **Force close if needed** is enabled, it terminates the selected process so Windows can release the folder handle.

## Why Use FolderFree

- One executable file.
- No command line required.
- No guessing in Task Manager.
- No reboot just to rename a folder.
- Light, modern Windows GUI.
- Uses built-in Windows APIs.
- Works well for locked project folders, download folders, extracted archives, editor workspaces, media folders, and folders blocked by background tools.

## Important Safety Note

Freeing a folder may close apps that have unsaved work. FolderFree shows the locking apps before closing anything, and it asks for confirmation before releasing them.

Use **Free selected** when you want control. Use **Free all** only when you are sure every listed app can be closed.

## Build From Source

FolderFree is a WinForms app built with C# and the Windows Desktop runtime. This repository includes a PowerShell build script that compiles the app with the local C# compiler.

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1
```

The built app is copied to:

```text
Release\FolderFree.exe
```

## Project Structure

```text
FolderFree/
  assets/
    FolderFree.ico
    FolderFree.png
  Release/
    FolderFree.exe
  src/
    Program.cs
  tools/
    Build.ps1
    GenerateAssets.ps1
  app.manifest
```

## Search Terms This Helps With

FolderFree is for Windows users searching for fixes to:

- delete folder in use
- rename folder in use Windows
- folder locked by another process
- find which app is using a folder
- cannot delete folder open in another program
- Windows folder unlocker
- release file handles Windows
- unlock folder without rebooting
- access denied folder used by another process

## License

MIT
