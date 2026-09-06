using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Pravka {
 static class IntegrationTests {
  sealed class Harness : Form {
   readonly string root;
   readonly RichTextBox editor;
   readonly TextBox dummy;
   readonly List<string> lines = new List<string>();
   Controller controller;
   int failed;

   public Harness(string applicationRoot) {
    root = applicationRoot;
    Text = "Pravka integration test";
    FormBorderStyle = FormBorderStyle.None;
    ShowInTaskbar = false;
    StartPosition = FormStartPosition.Manual;
    Location = new Point(-2000, -2000);
    ClientSize = new Size(240, 100);
    Opacity = 0.02;
    dummy = new TextBox { Location = new Point(2, 2), Size = new Size(20, 20) };
    editor = new RichTextBox { Location = new Point(25, 2), Size = new Size(200, 80) };
    Controls.Add(dummy); Controls.Add(editor);
    Shown += (s, e) => BeginInvoke((Action)RunScenarios);
   }

   void Check(string name, bool ok, string details) {
    if (!ok) failed++;
    lines.Add((ok ? "PASS" : "FAIL") + " | " + name + (String.IsNullOrEmpty(details) ? "" : " | " + details));
   }

   void Pump() { Application.DoEvents(); }

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
     Activate(); Native.SetForegroundWindow(Handle); dummy.Focus(); Pump();
     controller = new Controller(this, new Engine(root));

     if (caps) Key(0x14);
     controller.ResetState(); editor.Clear(); SelectLanguage(0x0409); editor.Focus(); Pump();
     TypePhysical("ghbdtn"); Pump();
     Check("ghbdtn is replaced", editor.Text == "привет ", editor.Text);
     Check("ghbdtn switches layout to RU", Native.CurrentLanguage(Handle) == 0x0419, Native.CurrentLanguage(Handle).ToString("x4"));

     controller.ResetState(); editor.Clear();
     dummy.Focus(); Pump(); editor.Focus();
     // Reproduce the real failure: an input-language hotkey and a late focus
     // notification immediately before the first word.
     ChordAltShift(); SelectLanguage(0x0409);
     controller.TestFocusNotification();
     TypePhysical("xnj");
     Pump();
     Check("first word after focus and Alt+Shift", editor.Text == "что ", editor.Text);
     Check("layout switched to RU", Native.CurrentLanguage(Handle) == 0x0419, Native.CurrentLanguage(Handle).ToString("x4"));

     TypePhysical("ltkftim"); Pump();
     Check("next physical word uses RU layout", editor.Text == "что делаешь ", editor.Text);

     controller.ResetState(); editor.Clear(); SelectLanguage(0x0419); editor.Focus(); Pump();
     TypePhysical("hello"); Pump();
     Check("reverse layout correction", editor.Text == "hello ", editor.Text);
     Check("layout switched to EN", Native.CurrentLanguage(Handle) == 0x0409, Native.CurrentLanguage(Handle).ToString("x4"));

     controller.ResetState(); editor.Clear(); SelectLanguage(0x0409); editor.Focus(); Pump();
     TypePhysical("wrold"); Pump();
     Check("English typo is replaced", editor.Text == "world ", editor.Text);
     Check("typo keeps EN layout", Native.CurrentLanguage(Handle) == 0x0409, Native.CurrentLanguage(Handle).ToString("x4"));
    }
    catch (Exception error) {
     failed++; lines.Add("FAIL | integration exception | " + error);
    }
    finally {
     if (caps != ((Native.GetKeyState(0x14) & 1) != 0)) try { Key(0x14); } catch { }
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
