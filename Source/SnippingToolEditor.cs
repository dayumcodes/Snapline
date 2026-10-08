using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Snapline {
 // Shared-file-token annotation first, Windows.File activation second.
 // No default app changes or synthetic keyboard input.
 internal static class SnippingToolEditor {
  internal const string AppId = "Microsoft.ScreenSketch_8wekyb3d8bbwe!App";
  static IApplicationActivationManager manager;

  internal static string AnnotationUri(string token) {
   if(string.IsNullOrWhiteSpace(token))throw new ArgumentException("Missing shared file token.");
   return "ms-screensketch:edit?source=Snapline&isTemporary=false&sharedAccessToken="+Uri.EscapeDataString(token);
  }
  internal static async Task<string> OpenAsync(string path) {
   string full;
   try {
    if(string.IsNullOrWhiteSpace(path))throw new ArgumentException("No image was selected.");
    full=Path.GetFullPath(path);
    if(!File.Exists(full))throw new FileNotFoundException("The selected image no longer exists.");
    if(!Helpers.IsImage(full))throw new ArgumentException("The selected file is not a supported image.");
   }catch(Exception e){return e.Message;}
   string tokenError;
   try {
    await SnippingToolToken.OpenAsync(full);
    return null;
   }catch(Exception e){tokenError="Shared-token annotation: "+e.Message+" (0x"+e.HResult.ToString("X8")+")";}
   // Some installations remove the legacy edit protocol. Retain Windows.File
   // activation as a secondary route, followed by explicit manual instructions.
   try{ActivateFile(full);return null;}catch(Exception e){return tokenError+"\r\nFile activation: "+e.Message+" (0x"+e.HResult.ToString("X8")+")";}
  }

  // Separate validation from native activation so the failure paths can be
  // tested without invoking Windows COM or launching applications.
  internal static bool TryOpen(string path, Action<string> activate, out string error) {
   error = null;
   try {
    if(string.IsNullOrWhiteSpace(path)) throw new ArgumentException("No image was selected.");
    var full = Path.GetFullPath(path);
    if(!File.Exists(full)) throw new FileNotFoundException("The selected image no longer exists.", full);
    if(!Helpers.IsImage(full)) throw new ArgumentException("The selected file is not a supported image.");
    activate(full);
    return true; // OS accepted activation; this does not confirm editor UI loaded it.
   } catch(Exception e) {
    error = e.Message;
    return false;
   }
  }

  static void ActivateFile(string path) {
   if(Environment.OSVersion.Platform != PlatformID.Win32NT)
    throw new PlatformNotSupportedException("Snipping Tool file activation requires Windows.");
   IntPtr item = IntPtr.Zero, array = IntPtr.Zero;
   try {
    var itemId = new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE");
    Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(path, IntPtr.Zero, ref itemId, out item));
    var arrayId = new Guid("B63EA76D-1F85-456F-A19C-48159EFA858B");
    Marshal.ThrowExceptionForHR(SHCreateShellItemArrayFromShellItem(item, ref arrayId, out array));
    // Keep this COM object alive in our long-running process. Windows uses it
    // to serve activation arguments to the receiving packaged application.
    if(manager == null) manager = (IApplicationActivationManager)Activator.CreateInstance(
     Type.GetTypeFromCLSID(new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")));
    uint processId;
    Marshal.ThrowExceptionForHR(manager.ActivateForFile(AppId, array, "open", out processId));
   } finally {
    if(array != IntPtr.Zero) Marshal.Release(array);
    if(item != IntPtr.Zero) Marshal.Release(item);
   }
  }

  [DllImport("shell32.dll", CharSet=CharSet.Unicode, PreserveSig=true)]
  static extern int SHCreateItemFromParsingName(string path, IntPtr binding, ref Guid iid, out IntPtr item);
  [DllImport("shell32.dll", PreserveSig=true)]
  static extern int SHCreateShellItemArrayFromShellItem(IntPtr item, ref Guid iid, out IntPtr array);

  [ComImport, Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  interface IApplicationActivationManager {
   [PreserveSig] int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string appId,
    [MarshalAs(UnmanagedType.LPWStr)] string arguments, uint options, out uint processId);
   [PreserveSig] int ActivateForFile([MarshalAs(UnmanagedType.LPWStr)] string appId,
    IntPtr items, [MarshalAs(UnmanagedType.LPWStr)] string verb, out uint processId);
   [PreserveSig] int ActivateForProtocol([MarshalAs(UnmanagedType.LPWStr)] string appId,
    IntPtr items, out uint processId);
  }

  internal static void SelfTest(string folder) {
   var file=Path.Combine(folder,"snip unicode 日本 & space.png");
   File.WriteAllText(file,"activation fixture");
   string error,received=null;int calls=0;
   var uri=AnnotationUri("token &/日本");
   if(!uri.Contains("isTemporary=false")||!uri.Contains("sharedAccessToken=token%20%26%2F")||uri.Contains(file))throw new Exception("Annotation token encoding/deletion guard failed");
   if(!TryOpen(file,p=>{calls++;received=p;},out error)||received!=Path.GetFullPath(file)||calls!=1)
    throw new Exception("Snipping editor path routing failed");
   if(TryOpen(Path.Combine(folder,"missing.png"),p=>calls++,out error)||calls!=1||error==null)
    throw new Exception("Missing-file guard failed");
   var other=Path.Combine(folder,"not-image.txt");File.WriteAllText(other,"fixture");
   if(TryOpen(other,p=>calls++,out error)||calls!=1)throw new Exception("Image guard failed");
   if(TryOpen(file,p=>{throw new COMException("fixture activation rejected",unchecked((int)0x80040154));},out error)
      ||error!="fixture activation rejected")throw new Exception("Activation-failure fallback routing failed");
   if(TryOpen(null,p=>calls++,out error)||calls!=1)throw new Exception("Empty-path guard failed");
   Console.WriteLine("PASS Snipping Tool editor: exact Unicode/spaces path routing, missing/invalid-file guards, rejected activation handling (native Windows activation NOT tested)");
  }
 }
}
