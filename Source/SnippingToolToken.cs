using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace Snapline {
 internal static class SnippingToolToken {
  internal static async Task OpenAsync(string fullPath) {
   // This token grants the receiving app access to the selected file. A raw
   // path in the URI is NOT a sharedAccessToken and does not open the image.
   var file=await StorageFile.GetFileFromPathAsync(fullPath);
   var token=SharedStorageAccessManager.AddFile(file);
   try {
    var uri=new Uri(SnippingToolEditor.AnnotationUri(token));
    if(!await Launcher.LaunchUriAsync(uri))
     throw new InvalidOperationException("Windows rejected the Snipping Tool annotation protocol.");
    // Keep the token available for asynchronous redemption by Snipping Tool.
    // Windows expires unused tokens; do not revoke it immediately on launch.
   } catch {
    try { SharedStorageAccessManager.RemoveFile(token); } catch {}
    throw;
   }
  }
 }
}
