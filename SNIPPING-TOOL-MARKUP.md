# Snipping Tool image handoff - 1.2.1 test build

The previous Windows.File handoff opened Snipping Tool without an image on the
first user's machine and reached our fallback instructions. The screenshot
confirms activation was rejected; the specific HRESULT was not readable in the
old dialog. We cannot prove the exact cause from that screenshot.

This build tries a shared-file-token annotation handoff first:
1. Resolve the selected path with Windows.Storage.StorageFile.GetFileFromPathAsync.
2. Create a file access token with SharedStorageAccessManager.AddFile.
3. Use Windows.System.Launcher.LaunchUriAsync with:
   ms-screensketch:edit?source=Snapline&isTemporary=false&sharedAccessToken=TOKEN

The token, not a raw filename, grants Snipping Tool access to the selected file.
It is URI-encoded. isTemporary=false avoids asking Snipping Tool to delete it.
No clipboard replacement, synthetic keystrokes, file-association edits or Paint.

This is a compatibility route, not a guaranteed current API: Microsoft's present
English documentation marks the legacy protocol deprecated/unsupported. The
historical documentation and a Microsoft Q&A desktop-app example describe this
exact existing-image annotation flow. The new documented protocol is capture/
discovery for packaged apps, not a direct replacement to edit an existing file
from this portable WinForms application. We cannot claim it works on every
current Snipping Tool build until tested. If the protocol is unavailable, the
previous Windows.File contract remains a secondary attempt. If both reject the
handoff, manual-open instructions remain. Right-click > Snipping Tool: open
manually... covers cases where Windows reports successful launch but the image
does not actually appear. An accepted launch is not proof the image was loaded.

The fallback dialog is taller, with readable HRESULTs from both attempted paths.
The file path remains selectable. Save over the original image to refresh its
thumbnail; saving a separate copy preserves the original.

Verification: cross-build succeeded with 0 warnings/errors. Non-Windows tests
cover token URI encoding/isTemporary=false, path routing, missing/invalid-file
guards, rejected activation and the existing GUI/data tests. The actual token,
WinRT launch, Windows file contract and Snipping Tool editing are UNTESTED here.
Please test before treating this build as a working fix or publishing a release.

Test on Windows 11:
- Quit the old Snapline instance before replacing EXE/config.
- Hold a PNG card and confirm the exact image appears in Snipping Tool.
- Test spaces/Unicode in the filename and the OneDrive Screenshots folder.
- Save over the original and confirm the shelf thumbnail updates.
- If it fails, send the full new fallback dialog with both HRESULT lines.
- If no dialog appears but the editor is empty, use the manual-open menu and
  report the Windows/Snipping Tool versions. Do not treat silent launch as success.

Sources:
https://learn.microsoft.com/en-us/answers/questions/815824/how-to-invoke-snipping-tool-or-snip-sketch-to-open
https://learn.microsoft.com/sl-si/windows/apps/develop/launch/launch-screen-snipping
https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-screen-snipping
https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-snipping-tool
