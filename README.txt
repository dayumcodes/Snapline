SNAPLINE FOR WINDOWS 11 - v1.1 - transparent line edition
A small screenshot shelf adapted from the idea and workflows of Tendedero.
Independent Windows implementation, not an official release or endorsement.

START HERE
1. Extract the entire ZIP to a folder, for example Documents\Snapline.
2. Double-click Snapline.exe. Do not run it directly inside the ZIP.
3. The app starts in the system tray beside the clock. Look under the ^ arrow
   if Windows hides the icon. There is no normal taskbar window.
4. Press Ctrl+Alt+T, or rest your pointer against the top edge of your display
   for a moment, to show the shelf. Move away to hide it. Tray > Keep line visible pins it open.
5. Press Ctrl+Alt+S to capture a region using Windows Snipping Tool. Select an
   area; Snapline picks up the resulting clipboard image and saves it locally.
   This waits up to 90 seconds. It needs Snipping Tool's clipboard copy enabled.
   Alternatively press Ctrl+Alt+P to capture the display under your pointer.

No Node, Python, Swift, compiler, administrator access or app installation
is needed to run the provided EXE. It targets .NET Framework 4.8, already
included in Windows 11. No third-party runtime is shipped or required.

This EXE is unsigned. Windows may show an unknown-publisher/SmartScreen warning,
which is not a publisher certification. The complete source is included for
inspection or rebuilding. Keep normal Windows security protections enabled;
do not disable Defender or SmartScreen. If your organization's policy blocks
unsigned programs, ask IT or build/sign the source through your approved process.

USING THE SHELF
- Click a screenshot: copy the image to the clipboard, ready to paste.
- Double-click: open in the default image viewer.
- Hold for about half a second: edit the original with Microsoft Paint.
- Drag into an app or File Explorer: send a real file; the destination chooses
  copy or move. Ordinary copy leaves the original on the shelf. A completed
  move is detected and the missing original drops off the shelf.
- Right-click an image: copy, open, Paint, save a copy, show in folder, remove,
  or explicitly move to the Recycle Bin (asks for confirmation).
- The small x takes a watched/imported image down without deleting it. For an
  image in Snapline's own capture Inbox, x offers to recycle the file and asks
  for confirmation. Right-click > Take down always keeps the original file.
- Import adds existing images. You can also drop files onto the shelf.
- The line fits as many images as your screen width allows (about 8 at 1440px).
  Older items leave the shelf but their files are not deleted.

TRAY MENU AND WINDOWS SCREENSHOTS
The Pictures\Screenshots folder and Snapline's own capture inbox are watched.
New/changed PNG, JPEG, BMP, GIF and TIFF images appear after they finish writing.
Existing images from before launch aren't automatically imported; use Import.
Files restored from your last shelf reappear on the next launch.

Win+PrintScreen normally saves into Pictures\Screenshots. For a different
folder (OneDrive, a custom Snipping Tool location or another capture app), use
tray > Add a screenshot folder. Manage watched folders lets you stop watching
one without deleting anything. Missing default folders are checked later.

Normal Win+Shift+S captures that exist only on the clipboard can be added using
tray > Add clipboard image. Or enable Auto-collect clipboard images in the tray.
That is OFF by default. If enabled, every newly copied image can be saved, even
if it is not a screenshot. Screenshots/clipboard contents remain on your PC.
Copying an image from Snapline itself does not loop back into new captures.

CONTROLS
Ctrl+Alt+T    show/hide on the display under the pointer
Ctrl+Alt+S    capture region (Windows Snipping Tool)
Ctrl+Alt+P    capture the display under the pointer
Tray > Keep line visible   hold shelf open
Ctrl+Alt+T again            slide the line away without quitting
Tray > Quit  stop running
If another app has a shortcut already, Snapline reports it; use the tray instead.

STORAGE, PRIVACY AND UNINSTALL
Settings and the shelf list: %LOCALAPPDATA%\Snapline\settings.xml
Captures made by Snapline: %LOCALAPPDATA%\Snapline\Inbox
Imported/watched files stay in their original folders.
No account, telemetry, network requests or uploads. File actions/opening images
may launch your chosen external applications; their behavior is separate.
Captures aren't automatically purged. Use Open saved captures to review them.
To uninstall: Quit from the tray and delete the extracted program folder.
Optionally delete %LOCALAPPDATA%\Snapline AFTER keeping captures you want.
Startup isn't enabled automatically. If wanted, create a shortcut to the EXE,
press Win+R, type shell:startup, and put the shortcut in that folder.

DIFFERENCES FROM MACOS TENDEDERO
The original is Swift/AppKit/SwiftUI and cannot be recompiled unchanged for
Windows. Snapline is a native C#/WinForms + Win32 layered-window counterpart.
Version 1.1 replaces the old opaque toolbar with a per-pixel transparent line,
small aluminium clips, aspect-ratio rounded cards and glass-like inset borders.
Slide-in/out, landing sway, periodic breeze, hover growth, pressed shrink,
copied badge, falling discard and smooth recentering are included.
No toolbar, title, filenames or status bar are drawn on the line.
Transparent blank pixels are intended to pass clicks through to desktop/apps.
Actual Apple ultrathin-material live blur/refraction is NOT reproduced. The
frame uses translucent gradients/specular edges instead.
Windows Paint/default viewer replace Apple Markup/Preview. The top edge replaces
the macOS menu bar. Snapline has its own name/icon as required by the license.
The line follows the monitor where you reveal it; it is not duplicated on every
monitor. Edge reveal is suppressed and the line hides for foreground full-screen
windows. See VIDEO-MATCH.txt for a timestamped match list and remaining gaps.
No screenshot-system settings are changed. No translations, HEIC/WebP decoding,
automatic screenshot redirection or Windows startup installation is included.

VERIFICATION AND LIMITS - PLEASE READ
The Windows executable cross-build succeeded with 0 warnings and 0 errors.
Non-Windows Mono/Xvfb tests passed for GUI startup, real image-file watching,
clipboard-image copy, restored shelf, safe removal, deleted-file pruning, the
screen-width card limit, image decoding, collision-safe names and settings recovery.
The preview uses the actual app's overlay renderer with generated sample images
and a drawn example desktop backdrop. It is NOT a Windows 11 screenshot and
NOT proof that Windows layered-window composition/click-through works.

A Windows 11 machine was not available. Windows per-pixel-alpha layered-window composition/click-through,
non-activating overlay, tray integration, native global hotkeys, Snipping Tool launching, Windows clipboard interoperability, Explorer
file-drop/move behavior, Recycle Bin, Paint, native full-screen detection,
multi-monitor/DPI behavior and SmartScreen were implemented but NOT tested
on real Windows. Treat this as a first Windows build, not a Windows-certified
release. Protected/DRM content might not be capturable. Paint and Snipping Tool
must be installed for those two actions; the direct screen capture and Import
are alternatives. Source and a Windows smoke-test checklist are included.

BUILD FROM SOURCE (optional, only for developers)
Install a .NET SDK (8 or newer), then in Source run:
  dotnet build -c Release
Output: Source\bin\Release\net48\Snapline.exe
NuGet downloads Microsoft's .NET Framework reference assemblies during build.
Build dependencies aren't needed to RUN the supplied EXE. No paid services.
Self-check from a console: Snapline.exe --self-test
QA preview harness: Snapline.exe --qa C:\Temp\snapline
(creates a temporary sample inbox and screenshots, not normal app mode).

CREDITS AND SOURCES
Tendedero by Alejandro Bujan:
https://github.com/alejandrobujan/tendedero
Reviewed upstream commit: ed0618a67cc59e0a8621b97bb5e32ab9af3b0b69
Original MIT license and name/icon restrictions: LICENSE-UPSTREAM.txt
Windows implementation: LICENSE.txt
.NET Framework included on Windows 11:
https://learn.microsoft.com/en-us/dotnet/framework/install/on-windows-and-server
