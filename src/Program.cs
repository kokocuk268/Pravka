using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Pravka {
 static class Program {
  [STAThread]
  static int Main(string[] args) {
   string root = AppDomain.CurrentDomain.BaseDirectory;
   if (args.Length > 0 && args[0] == "--self-test")
    return Tests.Run(root, args.Length > 1 ? args[1] : Path.Combine(root, "tests.txt"));
   if (args.Length > 0 && args[0] == "--integration-test") {
    Application.EnableVisualStyles();
    Application.SetCompatibleTextRenderingDefault(false);
    return IntegrationTests.Run(root, args.Length > 1 ? args[1] : Path.Combine(root, "integration-tests.txt"));
   }
   if (args.Length > 0 && args[0] == "--render-ui") {
    string output = args.Length > 1 ? args[1] : Path.Combine(root, "ui-preview.png");
    try {
     Application.EnableVisualStyles();
     Application.SetCompatibleTextRenderingDefault(false);
     using (var form = new MainForm(root, false, true)) {
     form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual;
     form.Location = new Point(-3000, -3000); form.Opacity = 0.02;
     form.Show(); Application.DoEvents();
     if (args.Length > 2) { form.ShowPreviewPage(args[2]); Application.DoEvents(); }
      using (var bitmap = new Bitmap(form.Width, form.Height)) {
       form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
       bitmap.Save(output, ImageFormat.Png);
      }
      form.Close();
     }
     return 0;
    }
    catch (Exception error) { File.WriteAllText(output + ".error.txt", error.ToString()); return 1; }
   }

   bool fresh;
   using (var mutex = new Mutex(true, "Local\\Pravka-0.4", out fresh)) {
    if (!fresh) {
     MessageBox.Show("Правка уже работает — ищи её значок рядом с часами.", "Правка");
     return 0;
    }
    Application.EnableVisualStyles();
    Application.SetCompatibleTextRenderingDefault(false);
    try {
     bool background = Array.IndexOf(args, "--background") >= 0;
     Application.Run(new MainForm(root, background)); return 0;
    }
    catch (Exception error) {
     MessageBox.Show("Не удалось запустить Правку.\n\n" + error.Message, "Правка");
     return 1;
    }
   }
  }
 }
}
