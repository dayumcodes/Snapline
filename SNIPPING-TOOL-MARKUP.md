# Snipping Tool markup change

Hold-to-markup and the context menu now select Windows 11 Snipping Tool, not Paint.

The selected image is passed through the Windows.File activation contract using
`IApplicationActivationManager.ActivateForFile`, `SHCreateItemFromParsingName`
and `SHCreateShellItemArrayFromShellItem`. This supplies a real file item rather
than assuming an undocumented `snippingtool.exe "path"` command-line argument
works. The normal installed app ID is `Microsoft.ScreenSketch_8wekyb3d8bbwe!App`.

If Windows rejects activation, Snapline asks Windows to open Snipping Tool and
shows the original path in a selectable text box, with Ctrl+O instructions.
Right-click > Snipping Tool: open manually... is also available if Windows
accepts activation but that app build does not actually load the image.
No Paint fallback, synthetic keystrokes, clipboard changes, deletion, registry
edits or file-association changes are made. Save over the original image to
refresh its shelf thumbnail; saving a separate copy does not change the original.

Verification: cross-build succeeded, zero warnings/errors. Non-Windows tests
cover exact path routing (spaces/Unicode), missing/non-image/empty-path guards,
rejected-activation handling, plus the existing GUI/data regression tests.
Native COM activation and Snipping Tool UI have NOT been tested on Windows 11.
An accepted activation HRESULT is not confirmation the editor loaded the file.

Windows checks before merging/releasing:
- Hold a PNG/JPEG card: Snipping Tool loads that specific image, not a blank editor.
- Test a path with spaces and non-ASCII characters.
- Test context-menu markup and manual-open fallback.
- Save over the original: thumbnail updates; Save As preserves the original.
- With Snipping Tool unavailable, show clear instructions, not Paint or a crash.
- Check hold does not also copy the image or trigger a drag.

API sources:
- https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-iapplicationactivationmanager-activateforfile
- https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-shcreateitemfromparsingname
- https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-shcreateshellitemarrayfromshellitem

The deprecated ms-screensketch annotation URI is not used. The existing region
capture shortcut is separate and unchanged by this focused markup change.
