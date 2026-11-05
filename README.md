# Color Picker

A tiny, portable colour picker for Windows: one small window to pick colours from the screen or a picture, and save them into simple folders.

## Download

1. Download **ColorPicker.exe** from the [Releases](../../releases) page.
2. Double-click it. There's nothing to install, and it runs on Windows 10 and 11.
3. If Windows shows **"Windows protected your PC"**, click **More info → Run anyway**.
   This appears for new apps that aren't code-signed yet; it's not a problem with the file.

You can keep the .exe anywhere, Downloads included. Your colours are saved in `Documents\Color Picker`,
so moving or deleting the .exe doesn't lose them.

## Use

Open `ColorPicker.exe`. One small window has everything:

- **Pick colour** (or **Ctrl+Alt+C** from any app while it's open) freezes the screen and shows a magnifier.
  Click to pick. Arrow keys move one pixel. Esc or right-click cancels.
- The colour is copied straight away (HEX by default; change it under ⚙ → Copy on pick).
- ⚙ → Dark theme switches between the dark brand theme (default: warm charcoal with a magenta accent) and light.
- Each format (HEX, RGB, HSB, HSL, CMYK) shows one box per number, like design apps do. Click a box to copy just that number;
  click the row name (e.g. RGB) to copy the whole value, e.g. `rgb(255, 214, 100)`.
- **Colours from a picture:** click the **+ picture** button next to Pick colour, or drop a picture on the window.
  It shows the 5 main colours; **Details** lists more of them with how much of the picture each covers.
  Hover the picture for a magnifier and click to pick an exact pixel. **Save all to folder** keeps the palette, and **Open another…** loads a new picture.
- The right side lists your folders as cards, each with a strip of its 5 newest colours. **Recent colours** is always first.
  Click a card to open it; click a colour to select it, right-click to remove it. **←** goes back.
  **⋯** (or right-clicking a card) renames, duplicates or deletes a folder. **+ folder** makes a new one.
- **Inside a folder**, every colour you pick goes straight into it (and into Recent). The button under the values reads
  **Add to <folder>** for colours from elsewhere, e.g. Recent or a picture.
- **On the folder list**, **Save to folder ▾** puts the colour in any folder (or makes a new one).

## What's new

See [CHANGELOG.md](CHANGELOG.md).

## Build

Run `build.bat`. It uses the C# compiler that ships with Windows (.NET Framework 4.x), so there is nothing to install.
Output: `bin\ColorPicker.exe` (about 150 KB, a third of it the icon). The icon lives in `assets\app.ico` (window-size copy: `assets\window.ico`),
and the version number in `src\AssemblyInfo.cs`.

## Stack and why

C# WinForms on .NET Framework 4.8, which is already part of Windows 10 and 11. That gives one small .exe,
no runtime to bundle, no installer and no admin rights. The code is limited to C# 5 so the built-in compiler can build it.

## Files

| Path | What |
| --- | --- |
| `src/Program.cs` | Entry point, single instance |
| `src/MainWindow.cs` | The app window: pick, values, save, folders |
| `src/Theme.cs` | Dark and light colours, dark title bar, menu styling |
| `src/ImageWindow.cs` | Picture window: magnifier, main colours, details |
| `src/ImagePalette.cs` | Finds a picture's main colours and their share |
| `src/PickerOverlay.cs` | Frozen-screen magnifier and pick |
| `src/Store.cs` | Settings, history, folder files |
| `src/ColorFormats.cs` | HEX / RGB / HSB / HSL / CMYK |
| `src/Native.cs` | Win32 calls (DPI, hotkey, Recycle Bin), clipboard |
| `src/Ui.cs` | App icon loading, name prompt |

## Data

The .exe can sit anywhere (Downloads is fine). Nothing is saved next to it:

- Settings: `%APPDATA%\ColorPicker\settings.ini`
- Folders: `Documents\Color Picker\<name>.colors`, plain text with one HEX value per line.
  ⚙ → Change where colours are saved… moves them somewhere else.
- Deleting a folder sends its file to the Recycle Bin.
