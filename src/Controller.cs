using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Forms;

namespace Pravka {
 public sealed class Controller : IDisposable {
  readonly Form ui;
  readonly Engine engine;
  readonly Native.Hook keyProc, mouseProc;
  readonly Native.WinEvent focusProc;
  readonly System.Windows.Forms.Timer probeTimer;
  IntPtr keyHook, mouseHook, focusHook, foregroundHook;
  readonly WordBuffer buffer = new WordBuffer();
  bool shiftHeld;
  DateTime importantUntil;
  int probing;
  Permit permit;
  Undo undo;

  static readonly string[] BlockedProcesses = {
   "cmd", "conhost", "powershell", "pwsh", "windowsterminal", "mintty", "wsl", "bash",
   "devenv", "code", "idea64", "studio64", "rider64", "pycharm64", "webstorm64", "clion64",
   "regedit", "taskmgr", "credentialuibroker", "keepass", "keepassxc", "1password", "bitwarden"
  };

  public bool Enabled = true, Spelling = true, Layout = true;
  public volatile int Corrections;
  public string Status = "Проверяю активное поле…";
  public Action<string> Suggest;
  public Action Changed;

  sealed class Permit { public IntPtr Window, Focus; public DateTime Time; public string App; }
  sealed class Undo { public string Before, After, Delimiter; public Permit Target; public IntPtr LayoutBefore; public bool ChangedLayout; }

  public Controller(Form form, Engine correctionEngine) {
   ui = form; engine = correctionEngine;
   keyProc = Keys; mouseProc = Mouse; focusProc = FocusEvent;
   keyHook = Native.SetWindowsHookEx(13, keyProc, Native.GetModuleHandle(null), 0);
   mouseHook = Native.SetWindowsHookEx(14, mouseProc, Native.GetModuleHandle(null), 0);
   focusHook = Native.SetWinEventHook(0x8005, 0x8005, IntPtr.Zero, focusProc, 0, 0, 0);
   foregroundHook = Native.SetWinEventHook(3, 3, IntPtr.Zero, focusProc, 0, 0, 0);
   if (keyHook == IntPtr.Zero || mouseHook == IntPtr.Zero)
    throw new Exception("Не удалось подключить обработку клавиатуры: " + Marshal.GetLastWin32Error());
   probeTimer = new System.Windows.Forms.Timer { Interval = 120 };
   probeTimer.Tick += (s, e) => Probe();
   probeTimer.Start(); Probe();
  }

  void Reset(bool clearUndo = true) { buffer.Reset(); if (clearUndo) undo = null; }
  public void ResetState() { Reset(); permit = null; Probe(); }
  void FocusEvent(IntPtr h, uint e, IntPtr w, int o, int c, uint t, uint time) {
   permit = null; buffer.FocusNotification();
  }
  internal void TestFocusNotification() { FocusEvent(IntPtr.Zero, 0, IntPtr.Zero, 0, 0, 0, 0); }
  IntPtr Mouse(int n, IntPtr w, IntPtr l) {
   if (n >= 0 && (w.ToInt32() == 0x201 || w.ToInt32() == 0x204 || w.ToInt32() == 0x207 || w.ToInt32() == 0x20A)) { permit = null; Reset(); }
   return Native.CallNextHookEx(mouseHook, n, w, l);
  }

  bool SameTarget(Permit value) {
   return Enabled && value != null && DateTime.UtcNow - value.Time < TimeSpan.FromSeconds(1)
    && Native.GetForegroundWindow() == value.Window && Native.Focus(value.Window) == value.Focus;
  }

  Permit ImmediateTarget(IntPtr window, IntPtr focus) {
   if (!Enabled || window == IntPtr.Zero) return null;
   try {
    uint id; Native.GetWindowThreadProcessId(window, out id);
    string app = Process.GetProcessById((int)id).ProcessName.ToLowerInvariant();
    if (Array.IndexOf(BlockedProcesses, app) >= 0) return null;
    bool safe = focus != IntPtr.Zero && Native.IsRichEdit(focus)
     && ((Native.GetWindowLong(focus, -16) & 0x20) == 0 || app == "notepad" || app == "pravka");
    if (!safe) {
     AutomationElement element = AutomationElement.FocusedElement;
     if (element != null && element.Current.ProcessId == id && element.Current.IsEnabled && element.Current.HasKeyboardFocus && !element.Current.IsPassword) {
      ControlType type = element.Current.ControlType;
      safe = type == ControlType.Edit || type == ControlType.Document;
      object pattern;
      if (element.TryGetCurrentPattern(ValuePattern.Pattern, out pattern) && ((ValuePattern)pattern).Current.IsReadOnly) safe = false;
     }
    }
    return safe ? new Permit { Window = window, Focus = focus, Time = DateTime.UtcNow, App = app } : null;
   } catch { return null; }
  }

  void Probe() {
   if (Interlocked.Exchange(ref probing, 1) != 0) return;
   IntPtr window = Native.GetForegroundWindow(), focus = Native.Focus(Native.GetForegroundWindow());
   Task.Run(() => {
    Permit result = null; string status = "Поле не поддерживается";
    try {
     uint id; Native.GetWindowThreadProcessId(window, out id);
     string app = Process.GetProcessById((int)id).ProcessName.ToLowerInvariant();
     bool blocked = Array.IndexOf(BlockedProcesses, app) >= 0;
     bool safe = false;
     if (!blocked && focus != IntPtr.Zero) {
      if ((app == "notepad" || app == "pravka") && Native.IsRichEdit(focus)) safe = true;
      if (!safe) {
       var element = AutomationElement.FocusedElement;
       if (element != null && element.Current.ProcessId == id && element.Current.IsEnabled && element.Current.HasKeyboardFocus && !element.Current.IsPassword) {
        var type = element.Current.ControlType;
        safe = type == ControlType.Edit || type == ControlType.Document;
        object pattern;
        if (element.TryGetCurrentPattern(ValuePattern.Pattern, out pattern) && ((ValuePattern)pattern).Current.IsReadOnly) safe = false;
       }
      }
     }
     if (blocked) status = "Пауза в коде и терминалах · " + app;
     else if (safe) { result = new Permit { Window = window, Focus = focus, Time = DateTime.UtcNow, App = app }; status = "Активно · " + Friendly(app); }
     else status = "Ожидаю обычное поле ввода · " + Friendly(app);
    } catch { status = "Не могу безопасно проверить поле"; }
    try {
     if (!ui.IsDisposed) ui.BeginInvoke((Action)(() => {
      if (Native.GetForegroundWindow() == window && Native.Focus(window) == focus) {
       if (result == null && permit != null && permit.Window == window && permit.Focus == focus
        && DateTime.UtcNow - permit.Time < TimeSpan.FromSeconds(2)) result = permit;
       permit = result;
       if (DateTime.UtcNow >= importantUntil) Status = Enabled ? status : "Исправления на паузе";
       if (Changed != null) Changed();
      }
     }));
    } catch { }
    Interlocked.Exchange(ref probing, 0);
   });
  }

  static string Friendly(string app) {
   if (app == "notepad") return "Блокнот";
   if (app == "pravka") return "тест Правки";
   if (app == "chrome") return "Chrome";
   if (app == "msedge") return "Edge";
   if (app == "telegram") return "Telegram";
   if (app == "winword") return "Word";
   return app;
  }

  IntPtr Keys(int n, IntPtr message, IntPtr data) {
   if (n < 0) return Native.CallNextHookEx(keyHook, n, message, data);
   var key = (Native.Kbd)Marshal.PtrToStructure(data, typeof(Native.Kbd));
   if (key.extra.ToUInt64() == Native.Mark) return Native.CallNextHookEx(keyHook, n, message, data);
   bool down = message.ToInt32() == 0x100 || message.ToInt32() == 0x104;
   if (key.vk == 16 || key.vk == 160 || key.vk == 161) {
    shiftHeld = down;
    if (down && (Native.Down(0x11) || Native.Down(0x12))) Reset();
    return Native.CallNextHookEx(keyHook, n, message, data);
   }
   if (!down) return Native.CallNextHookEx(keyHook, n, message, data);

   try {
    IntPtr window = Native.GetForegroundWindow(), focus = Native.Focus(window);
    bool modifierKey = key.vk == 17 || key.vk == 162 || key.vk == 163 || key.vk == 18 || key.vk == 164 || key.vk == 165 || key.vk == 91 || key.vk == 92;
    if (modifierKey) {
     if ((key.vk == 17 || key.vk == 162 || key.vk == 163 || key.vk == 18 || key.vk == 164 || key.vk == 165) && Native.Down(0x10)) Reset();
     return Native.CallNextHookEx(keyHook, n, message, data);
    }
    if (Native.Modifiers()) {
     // Alt+Shift/Ctrl+Shift contain only modifier keys. Win+Space is also a
     // layout gesture and must not cause the next word to be skipped.
     if (key.vk == 0x20 && (Native.Down(0x5B) || Native.Down(0x5C))) Reset();
     else buffer.MarkShortcut(window, focus);
     return Native.CallNextHookEx(keyHook, n, message, data);
    }

    if (key.vk == 8) {
     if (undo != null && Enabled && undo.Target.Window == window && undo.Target.Focus == focus && !Native.Down(16)) {
      var value = undo; undo = null; Reset(false);
      if (Native.ReplaceBeforeCaret(window, focus, value.After.Length + value.Delimiter.Length, value.Before + value.Delimiter)) {
       if (value.ChangedLayout && value.LayoutBefore != IntPtr.Zero) Native.RequestKeyboardLayout(window, value.LayoutBefore);
       engine.Ignore(value.Before); Status = "Отменено · запомнил это слово как исключение";
       importantUntil = DateTime.UtcNow.AddSeconds(2);
       if (Changed != null) Changed(); return new IntPtr(1);
      }
     }
     undo = null;
     buffer.Backspace(window, focus);
     return Native.CallNextHookEx(keyHook, n, message, data);
    }

    string character = Native.Character(key, window, shiftHeld);
    if (character.Length == 1 && Char.IsLetter(character[0])) {
     undo = null;
     buffer.PushLetter(character[0], window, focus);
     return Native.CallNextHookEx(keyHook, n, message, data);
    }

    bool delimiter = character.Length == 1 && (Char.IsWhiteSpace(character[0]) || ".,!?;:…)]}".IndexOf(character[0]) >= 0);
    if (delimiter) {
     bool blocked; string input = buffer.Take(window, focus, out blocked), ending = character;
     undo = null;
     Permit target = SameTarget(permit) ? permit : ImmediateTarget(window, focus);
     if (target != null) permit = target;
     if (!blocked && target != null && input.Length >= 3) {
      var decision = engine.Check(input, Spelling, Layout);
      IntPtr layoutBefore = Native.CurrentKeyboardLayout(window);
      bool replaced = CorrectionFlow.Apply(decision, input, ending,
       (remove, replacement) => Native.ReplaceBeforeCaret(window, focus, remove, replacement),
       language => Native.RequestKeyboardLanguage(window, language));
      if (replaced) {
       undo = new Undo { Before = input, After = decision.Text, Delimiter = ending, Target = target,
        LayoutBefore = layoutBefore, ChangedLayout = decision.Kind == "layout" };
       Corrections++; Status = "Исправлено: " + input + " → " + decision.Text;
       importantUntil = DateTime.UtcNow.AddSeconds(2);
       if (Changed != null) Changed();
       return new IntPtr(1);
      }
      if (decision.Suggestion != null && Suggest != null) Suggest(decision.Suggestion);
     }
     return Native.CallNextHookEx(keyHook, n, message, data);
    }

    undo = null;
    if (key.vk == 13 || key.vk == 9) Reset(); else buffer.MarkUnknownEdit(window, focus);
   } catch { Reset(); }
   return Native.CallNextHookEx(keyHook, n, message, data);
  }

  public void Dispose() {
   probeTimer.Stop(); probeTimer.Dispose();
   Native.UnhookWindowsHookEx(keyHook); Native.UnhookWindowsHookEx(mouseHook);
   Native.UnhookWinEvent(focusHook); Native.UnhookWinEvent(foregroundHook);
   Reset(); permit = null;
  }
 }
}
