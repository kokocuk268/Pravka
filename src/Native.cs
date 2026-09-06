using System;
using System.Text;
using System.Runtime.InteropServices;

namespace Pravka {
 internal static class Native {
  public delegate IntPtr Hook(int n, IntPtr w, IntPtr l);
  public delegate void WinEvent(IntPtr hook, uint ev, IntPtr hwnd, int obj, int child, uint thread, uint time);

  [StructLayout(LayoutKind.Sequential)] public struct Kbd { public uint vk, scan, flags, time; public UIntPtr extra; }
  [StructLayout(LayoutKind.Sequential)] public struct Rect { public int l, t, r, b; }
  [StructLayout(LayoutKind.Sequential)] public struct Gui { public uint size, flags; public IntPtr active, focus, capture, menu, move, caret; public Rect rect; }
  [StructLayout(LayoutKind.Sequential)] struct Keyboard { public ushort vk, scan; public uint flags, time; public UIntPtr extra; }
  [StructLayout(LayoutKind.Sequential)] struct Mouse { public int x, y; public uint data, flags, time; public UIntPtr extra; }
  [StructLayout(LayoutKind.Explicit)] struct Union { [FieldOffset(0)] public Keyboard key; [FieldOffset(0)] public Mouse mouse; }
  [StructLayout(LayoutKind.Sequential)] struct Input { public uint type; public Union u; }

  public const uint Mark = 0x5052564B;
  const uint KeyUp = 2, Unicode = 4;

  [DllImport("user32.dll", SetLastError=true)] public static extern IntPtr SetWindowsHookEx(int id, Hook proc, IntPtr module, uint tid);
  [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr h, int n, IntPtr w, IntPtr l);
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string name);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
  [DllImport("user32.dll", SetLastError=true)] static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
  [DllImport("user32.dll")] public static extern bool GetGUIThreadInfo(uint tid, ref Gui info);
  [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
  [DllImport("user32.dll")] public static extern short GetKeyState(int vk);
  [DllImport("user32.dll")] public static extern IntPtr GetKeyboardLayout(uint tid);
  [DllImport("user32.dll")] static extern int GetKeyboardLayoutList(int count, [Out] IntPtr[] layouts);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern IntPtr LoadKeyboardLayout(string id, uint flags);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int ToUnicodeEx(uint vk, uint scan, byte[] state, StringBuilder text, int capacity, uint flags, IntPtr layout);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);
  [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hwnd, int index);
  [DllImport("user32.dll", SetLastError=true)] static extern uint SendInput(uint count, Input[] inputs, int size);
  [DllImport("user32.dll")] public static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr mod, WinEvent proc, uint pid, uint tid, uint flags);
  [DllImport("user32.dll")] public static extern bool UnhookWinEvent(IntPtr h);
  [DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);

  public static IntPtr Focus(IntPtr hwnd) {
   uint pid; uint tid = GetWindowThreadProcessId(hwnd, out pid);
   var g = new Gui { size = (uint)Marshal.SizeOf(typeof(Gui)) };
   return GetGUIThreadInfo(tid, ref g) ? g.focus : IntPtr.Zero;
  }

  public static bool Down(int key) { return (GetAsyncKeyState(key) & 0x8000) != 0; }
  public static bool Modifiers() { return Down(0x11) || Down(0x12) || Down(0x5B) || Down(0x5C); }

  public static string Character(Kbd key, IntPtr hwnd, bool shift) {
   uint pid; uint tid = GetWindowThreadProcessId(hwnd, out pid);
   var state = new byte[256];
   if (shift || Down(0x10)) state[0x10] = 0x80;
   state[0x14] = (byte)(GetKeyState(0x14) & 1);
   var value = new StringBuilder(8);
   int count = ToUnicodeEx(key.vk, key.scan, state, value, value.Capacity, 4, GetKeyboardLayout(tid));
   return count == 1 ? value.ToString() : "";
  }

  public static IntPtr CurrentKeyboardLayout(IntPtr hwnd) {
   uint pid; uint tid = GetWindowThreadProcessId(hwnd, out pid);
   return GetKeyboardLayout(tid);
  }

  public static ushort CurrentLanguage(IntPtr hwnd) { return Language(CurrentKeyboardLayout(hwnd)); }

  static ushort Language(IntPtr layout) { return unchecked((ushort)(layout.ToInt64() & 0xffff)); }

  public static ushort LanguageForText(string text) {
   foreach (char value in text) {
    char lower = Char.ToLowerInvariant(value);
    if (lower >= 'а' && lower <= 'я' || lower == 'ё') return 0x0419;
   }
   return 0x0409;
  }

  static IntPtr FindKeyboardLayout(ushort language) {
   int count = GetKeyboardLayoutList(0, null);
   if (count > 0) {
    var layouts = new IntPtr[count];
    int read = GetKeyboardLayoutList(layouts.Length, layouts);
    for (int i = 0; i < read; i++) if (Language(layouts[i]) == language) return layouts[i];
   }
   return LoadKeyboardLayout(language == 0x0419 ? "00000419" : "00000409", 0);
  }

  public static bool RequestKeyboardLanguage(IntPtr hwnd, ushort language) {
   IntPtr layout = FindKeyboardLayout(language);
   return RequestKeyboardLayout(hwnd, layout);
  }

  public static bool RequestKeyboardLayout(IntPtr hwnd, IntPtr layout) {
   if (hwnd == IntPtr.Zero || layout == IntPtr.Zero) return false;
   if (Language(CurrentKeyboardLayout(hwnd)) == Language(layout)) return true;
   // WM_INPUTLANGCHANGEREQUEST is explicitly a posted message. DefWindowProc
   // activates the HKL on the target UI thread when its queue handles it.
   return PostMessage(hwnd, 0x50, IntPtr.Zero, layout);
  }

  public static bool IsRichEdit(IntPtr focus) {
   var name = new StringBuilder(256); GetClassName(focus, name, name.Capacity);
   string value = name.ToString().ToLowerInvariant();
   return value.Contains("richedit") || value == "edit" || value.StartsWith("windowsforms10.edit");
  }

  public static bool ReplaceBeforeCaret(IntPtr expectedWindow, IntPtr expectedFocus, int remove, string replacement) {
   if (GetForegroundWindow() != expectedWindow || Focus(expectedWindow) != expectedFocus) return false;
   if (IsRichEdit(expectedFocus) && ReplaceRichEdit(expectedWindow, expectedFocus, remove, replacement)) return true;
   return ReplaceGeneric(expectedWindow, expectedFocus, remove, replacement);
  }

  static bool ReplaceRichEdit(IntPtr window, IntPtr focus, int remove, string replacement) {
   IntPtr result;
   if (SendMessageTimeout(focus, 0xB0, IntPtr.Zero, IntPtr.Zero, 2, 80, out result) == IntPtr.Zero) return false;
   uint selection = unchecked((uint)result.ToInt64());
   int start = (int)(selection & 65535), end = (int)(selection >> 16);
   if (selection == UInt32.MaxValue || start != end || end < remove || GetForegroundWindow() != window) return false;
   if (SendMessageTimeout(focus, 0xB1, new IntPtr(end - remove), new IntPtr(end), 2, 80, out result) == IntPtr.Zero) return false;
   IntPtr data = Marshal.StringToHGlobalUni(replacement);
   try { return SendMessageTimeout(focus, 0xC2, new IntPtr(1), data, 2, 80, out result) != IntPtr.Zero; }
   finally { Marshal.FreeHGlobal(data); }
  }

  static bool ReplaceGeneric(IntPtr window, IntPtr focus, int remove, string replacement) {
   for (int i = 0; i < remove; i++) if (!SendVirtualKey(8)) return false;
   foreach (char c in replacement) {
    if (GetForegroundWindow() != window || Focus(window) != focus || !SendUnicode(c)) return false;
   }
   return true;
  }

  static bool SendVirtualKey(ushort key) {
   var input = new[] { MakeKey(key, (char)0, 0), MakeKey(key, (char)0, KeyUp) };
   return SendInput(2, input, Marshal.SizeOf(typeof(Input))) == 2;
  }

  public static bool SendPhysicalKey(ushort key) {
   return SendPhysicalKeyEvent(key, false) && SendPhysicalKeyEvent(key, true);
  }

  public static bool SendPhysicalKeyEvent(ushort key, bool up) {
   var input = MakeKey(key, (char)0, up ? KeyUp : 0);
   input.u.key.extra = UIntPtr.Zero;
   return SendInput(1, new[] { input }, Marshal.SizeOf(typeof(Input))) == 1;
  }

  static bool SendUnicode(char value) {
   var input = new[] { MakeKey(0, value, Unicode), MakeKey(0, value, Unicode | KeyUp) };
   return SendInput(2, input, Marshal.SizeOf(typeof(Input))) == 2;
  }

  static Input MakeKey(ushort vk, char scan, uint flags) {
   var input = new Input { type = 1 };
   input.u.key = new Keyboard { vk = vk, scan = scan, flags = flags, extra = new UIntPtr(Mark) };
   return input;
  }
 }
}
