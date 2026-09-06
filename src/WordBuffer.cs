using System;

namespace Pravka {
 // Keeps the word currently being typed. Async accessibility notifications never
 // mutate this state; real key events decide whether the input context changed.
 internal sealed class WordBuffer {
  string text = "";
  IntPtr window, focus;
  bool skip, shortcut;

  public int Length { get { return text.Length; } }

  public void Reset() {
   text = ""; window = IntPtr.Zero; focus = IntPtr.Zero;
   skip = false; shortcut = false;
  }

  public void FocusNotification() {
   // Deliberately empty. WinEvent callbacks can arrive after the first physical
   // key. PushLetter/Take compare the actual HWND pair synchronously instead.
  }

  bool Same(IntPtr currentWindow, IntPtr currentFocus) {
   return window == currentWindow && focus == currentFocus;
  }

  void Start(IntPtr currentWindow, IntPtr currentFocus) {
   window = currentWindow; focus = currentFocus;
  }

  public void MarkShortcut(IntPtr currentWindow, IntPtr currentFocus) {
   Reset(); Start(currentWindow, currentFocus); skip = true; shortcut = true;
  }

  public void MarkUnknownEdit(IntPtr currentWindow, IntPtr currentFocus) {
   Reset(); Start(currentWindow, currentFocus); skip = true;
  }

  public void PushLetter(char value, IntPtr currentWindow, IntPtr currentFocus) {
   if ((text.Length > 0 || skip) && !Same(currentWindow, currentFocus)) Reset();
   if (skip) return;
   if (text.Length == 0) Start(currentWindow, currentFocus);
   if (text.Length < 32) text += value;
   else MarkUnknownEdit(currentWindow, currentFocus);
  }

  public void Backspace(IntPtr currentWindow, IntPtr currentFocus) {
   if ((text.Length > 0 || skip) && !Same(currentWindow, currentFocus)) Reset();
   if (text.Length > 0) text = text.Substring(0, text.Length - 1);
   else if (shortcut) { skip = false; shortcut = false; Start(currentWindow, currentFocus); }
   else MarkUnknownEdit(currentWindow, currentFocus);
  }

  public string Take(IntPtr currentWindow, IntPtr currentFocus, out bool blocked) {
   bool same = text.Length == 0 || Same(currentWindow, currentFocus);
   string result = same ? text : "";
   blocked = skip || !same;
   Reset();
   return result;
  }
 }
}
