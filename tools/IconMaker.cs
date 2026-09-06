using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;

static class IconMaker {
 static byte[] Render(int size) {
  using (var image = new Bitmap(size, size, PixelFormat.Format32bppArgb))
  using (Graphics g = Graphics.FromImage(image)) {
   g.SmoothingMode = SmoothingMode.AntiAlias;
   float inset = Math.Max(1F, size * .04F);
   var box = new RectangleF(inset, inset, size - inset * 2, size - inset * 2);
   using (var path = Round(box, size * .25F))
   using (var gradient = new LinearGradientBrush(box, Color.FromArgb(119, 88, 255), Color.FromArgb(41, 194, 255), 38F))
    g.FillPath(gradient, path);
   using (var pen = new Pen(Color.White, Math.Max(2F, size * .075F))) {
    pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round;
    g.DrawLines(pen, new[] {
     new PointF(size * .27F, size * .52F), new PointF(size * .43F, size * .68F), new PointF(size * .75F, size * .31F)
    });
   }
   using (var memory = new MemoryStream()) { image.Save(memory, ImageFormat.Png); return memory.ToArray(); }
  }
 }
 static GraphicsPath Round(RectangleF r, float radius) {
  float d = radius * 2; var path = new GraphicsPath();
  path.AddArc(r.Left, r.Top, d, d, 180, 90); path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
  path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
  path.CloseFigure(); return path;
 }
 static int Main(string[] args) {
  if (args.Length != 1) return 2;
  int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
  var images = new List<byte[]>(); foreach (int size in sizes) images.Add(Render(size));
  Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0])));
  using (var file = File.Create(args[0])) using (var writer = new BinaryWriter(file)) {
   writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
   int offset = 6 + sizes.Length * 16;
   for (int i = 0; i < sizes.Length; i++) {
    writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
    writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32);
    writer.Write(images[i].Length); writer.Write(offset); offset += images[i].Length;
   }
   foreach (byte[] image in images) writer.Write(image);
  }
  return 0;
 }
}
