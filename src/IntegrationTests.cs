using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Pravka {
 static class IntegrationTests {
  sealed class Harness : Form {
   readonly string root;
   readonly RichTextBox editor;
   readonly TextBox generic;
   readonly TextBox dummy;
   readonly List<string> lines = new List<string>();
   Controller controller;
   int failed;

   public Harness(string applicationRoot) {
    root = applicationRoot;
    Text = "Pravka integration test";
    FormBorderStyle = FormBorderStyle.None;
    ShowInTaskbar = false;
    TopMost = true;
    StartPosition = FormStartPosition.Manual;
    Location = new Point(40, 40);
    ClientSize = new Size(240, 145);
    Opacity = 0.08;
    dummy = new TextBox { Location = new Point(2, 2), Size = new Size(20, 20) };
    editor = new RichTextBox { Location = new Point(25, 2), Size = new Size(200, 80) };
    generic = new TextBox { Location = new Point(25, 88), Size = new Size(200, 40) };
    Controls.Add(dummy); Controls.Add(editor); Controls.Add(generic);
    Shown += (s, e) => BeginInvoke((Action)RunScenarios);
   }

   void Check(string name, bool ok, string details) {
    if (!ok) failed++;
    lines.Add((ok ? "PASS" : "FAIL") + " | " + name + (String.IsNullOrEmpty(details) ? "" : " | " + details));
   }

   void Pump() { Application.DoEvents(); }

   bool WaitForLanguage(ushort language) {
    DateTime limit = DateTime.UtcNow.AddSeconds(1);
    while (DateTime.UtcNow < limit) {
     Pump();
     if (Native.CurrentLanguage(Handle) == language) return true;
     Thread.Sleep(10);
    }
    return Native.CurrentLanguage(Handle) == language;
   }

   void Key(ushort value) {
    if (!Native.SendPhysicalKey(value)) throw new Exception("SendInput failed for VK " + value);
    Pump();
   }

   void ChordAltShift() {
    Native.SendPhysicalKeyEvent(0x12, false); Pump();
    Native.SendPhysicalKeyEvent(0x10, false); Pump();
    Native.SendPhysicalKeyEvent(0x10, true); Pump();
    Native.SendPhysicalKeyEvent(0x12, true); Pump();
   }

   void SelectAllAndDelete() {
    Native.SendPhysicalKeyEvent(0x11, false); Pump();
    Key(0x41);
    Native.SendPhysicalKeyEvent(0x11, true); Pump();
    Key(0x08); Pump();
   }

   void TypePhysical(string keys) {
    foreach (char value in keys) Key((ushort)Char.ToUpperInvariant(value));
    Key(0x20);
   }

   void SelectLanguage(ushort language) {
    Native.RequestKeyboardLanguage(Handle, language); Pump();
   }

   void RunScenarios() {
    bool caps = (Native.GetKeyState(0x14) & 1) != 0;
    try {
     Activate(); BringToFront(); Native.SetForegroundWindow(Handle); dummy.Focus(); Pump();
     controller = new Controller(this, new Engine(root));

     Activate(); BringToFront(); editor.Focus(); Native.FocusForTest(Handle, editor.Handle); Pump();
     Check("test field has keyboard focus", Native.GetForegroundWindow() == Handle && Native.Focus(Handle) == editor.Handle,
      "foreground=" + Native.GetForegroundWindow() + ", form=" + Handle + ", focus=" + Native.Focus(Handle));

     if (caps) Key(0x14);
     controller.ResetState(); editor.Clear(); SelectLanguage(0x0409); editor.Focus(); Pump();
     TypePhysical("ghbdtn"); Pump();
     Check("ghbdtn is replaced", editor.Text == "привет ", editor.Text);
     Check("ghbdtn switches layout to RU", WaitForLanguage(0x0419), Native.CurrentLanguage(Handle).ToString("x4"));

     controller.ResetState(); editor.Clear();
     dummy.Focus(); Pump(); editor.Focus();
     // Reproduce the real failure: an input-language hotkey and a late focus
     // notification immediately before the first word.
     ChordAltShift(); SelectLanguage(0x0409);
     controller.TestFocusNotification();
     TypePhysical("xnj");
     Pump();
     Check("first word after focus and Alt+Shift", editor.Text == "что ", editor.Text);
     Check("layout switched to RU", WaitForLanguage(0x0419), Native.CurrentLanguage(Handle).ToString("x4"));

     TypePhysical("ltkftim"); Pump();
     Check("next physical word uses RU layout", editor.Text == "что делаешь ", editor.Text);

     controller.ResetState(); editor.Clear(); SelectLanguage(0x0419); editor.Focus(); Pump();
     TypePhysical("hello"); Pump();
     Check("reverse layout correction", editor.Text == "hello ", editor.Text);
     Check("layout switched to EN", WaitForLanguage(0x0409), Native.CurrentLanguage(Handle).ToString("x4"));

     controller.ResetState(); editor.Clear(); SelectLanguage(0x0409); editor.Focus(); Pump();
     TypePhysical("wrold"); Pump();
     Check("English typo is replaced", editor.Text == "world ", editor.Text);
     Check("typo keeps EN layout", Native.CurrentLanguage(Handle) == 0x0409, Native.CurrentLanguage(Handle).ToString("x4"));

     SelectAllAndDelete();
     Check("Ctrl+A Backspace clears instead of undoing", editor.Text == "", editor.Text);
     TypePhysical("xnj"); Pump();
     Check("word still corrects after Ctrl+A Backspace", editor.Text == "что ", editor.Text);
     Check("layout still switches after Ctrl+A Backspace", WaitForLanguage(0x0419), Native.CurrentLanguage(Handle).ToString("x4"));

     controller.ResetState(); generic.Text = ""; SelectLanguage(0x0409);
     generic.Focus(); Native.FocusForTest(Handle, generic.Handle); Pump();
     Native.GenericForTest = generic.Handle;
     TypePhysical("ghbdtn"); Pump();
     Check("generic accessibility field is replaced", generic.Text == "привет ", generic.Text);
     Check("generic field switches layout to RU", WaitForLanguage(0x0419), Native.CurrentLanguage(Handle).ToString("x4"));
     Native.GenericForTest = IntPtr.Zero;
    }
    catch (Exception error) {
     failed++; lines.Add("FAIL | integration exception | " + error);
    }
    finally {
     if (caps != ((Native.GetKeyState(0x14) & 1) != 0)) try { Key(0x14); } catch { }
     Native.GenericForTest = IntPtr.Zero;
     if (controller != null) { controller.Dispose(); controller = null; }
     lines.Add("Failed: " + failed);
     Close();
    }
   }

   public int Failed { get { return failed; } }
   public string[] Report { get { return lines.ToArray(); } }
  }

  public static int Run(string root, string output) {
   var harness = new Harness(root);
   Application.Run(harness);
   File.WriteAllLines(output, harness.Report, Encoding.UTF8);
   return harness.Failed == 0 ? 0 : 1;
  }
 }
}
