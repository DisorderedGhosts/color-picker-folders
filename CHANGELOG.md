# Changelog

## 1.1.0

### New
- **Folder cards.** The right side now lists your folders as cards, each with a strip of its 5 newest colours.
  **Recent colours** is a card too. Click a card to open it, and **←** to go back.
- **Picks go into the open folder.** While a folder is open, every colour you pick is added to it (and to Recent).
  The save button reads **Add to *folder name*** for colours from elsewhere.
- **Duplicate folder.** Right-click a folder card (or use **⋯** inside one) to copy a folder and its colours under a new name.
- **Brand look.** New "Spectrum" theme: warm charcoal with a magenta accent and a rainbow line on Pick colour.
  The light theme matches it.
- **New icons.** Rainbow line icons for add picture, new folder, settings, save to folder, open another and back.
- **New app icon.** The ghost in a rainbow hexagon, with a see-through background.

### Improved
- Both sides of the window now line up, and the new-folder button matches the picture button.
- The picture window's **Open another…** and **Save all to folder** buttons are the same size and sit on one line.
- Clearer wording in settings: "Copy on pick", "Change where colours are saved…", "Show saved colours in File Explorer".
- "Remove from *folder name*" instead of "Remove from folder".
- The magnifier in the picture window stays smooth on large photos.
- Colours inside a folder wrap onto new rows instead of scrolling sideways.

### Fixed
- A folder file that was edited by hand no longer gets two colours stuck on one line when you add to it.
- A missing or busy folder location (for example an unplugged USB drive) no longer crashes the app; it shows no folders until it's back.
- Unreadable settings no longer stop the app from starting; it uses the defaults instead.
- Errors while making a new folder now show a message instead of the crash window.
- Small memory leak when the folder list refreshed many times.

## 1.0.0

First release: pick any colour on screen with a magnifier, copy it as HEX, RGB, HSB, HSL or CMYK
(whole value or one number at a time), save colours into folders, and get the main colours of a picture.
