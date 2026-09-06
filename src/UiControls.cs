using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Pravka {
 static class Palette {
  public static readonly Color Window = Color.FromArgb(247, 249, 255);
  public static readonly Color Sidebar = Color.FromArgb(239, 244, 255);
  public static readonly Color Card = Color.FromArgb(255, 255, 255);
  public static readonly Color CardHover = Color.FromArgb(246, 249, 255);
  public static readonly Color Text = Color.FromArgb(14, 23, 62);
  public static readonly Color Muted = Color.FromArgb(121, 136, 174);
  public static readonly Color Border = Color.FromArgb(220, 228, 246);
  public static readonly Color Accent = Color.FromArgb(82, 102, 248);
  public static readonly Color Accent2 = Color.FromArgb(112, 61, 238);
  public static readonly Color AccentSoft = Color.FromArgb(232, 237, 255);
  public static readonly Color Success = Color.FromArgb(55, 197, 91);
 }

 static class Shapes {
  public static GraphicsPath Round(Rectangle bounds, int radius) {
   radius = Math.Max(1, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2));
   int d = radius * 2;
   var path = new GraphicsPath();
   path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
   path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
   path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
   path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
   path.CloseFigure(); return path;
  }
 }

 sealed class SoftPanel : Panel {
  public SoftPanel() { DoubleBuffered = true; }
  protected override void OnPaintBackground(PaintEventArgs e) {
   Rectangle area = ClientRectangle;
   using (var brush = new LinearGradientBrush(area, Color.White, Palette.Window, 35F)) e.Graphics.FillRectangle(brush, area);
  }
 }

 sealed class RoundedPanel : Panel {
  public int Radius = 14;
  public Color Fill = Palette.Card;
  public Color Border = Palette.Border;
  public bool DrawBorder = true;
  public RoundedPanel() {
   SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
   BackColor = Color.Transparent;
  }
  protected override void OnPaintBackground(PaintEventArgs e) {
   base.OnPaintBackground(e);
   e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
   Rectangle box = new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3));
   using (GraphicsPath path = Shapes.Round(box, Radius)) {
    using (var brush = new SolidBrush(Fill)) e.Graphics.FillPath(brush, path);
    if (DrawBorder) using (var pen = new Pen(Border)) e.Graphics.DrawPath(pen, path);
   }
  }
 }

 sealed class Pulse : Control {
  public Pulse() {
   SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer, true);
   BackColor = Color.Transparent;
  }
  protected override void OnPaint(PaintEventArgs e) {
   e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
   using (var glow = new SolidBrush(Color.FromArgb(45, Palette.Success))) e.Graphics.FillEllipse(glow, 0, 0, Width, Height);
   int inset = Math.Max(3, Width / 5);
   using (var core = new SolidBrush(Palette.Success)) e.Graphics.FillEllipse(core, inset, inset, Width - inset * 2, Height - inset * 2);
  }
 }

 sealed class LogoControl : Control {
  public LogoControl() {
   SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer, true);
   BackColor = Color.Transparent;
  }
  protected override void OnPaint(PaintEventArgs e) {
   Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
   Rectangle box = new Rectangle(1, 1, Width - 3, Height - 3);
   using (GraphicsPath shape = Shapes.Round(box, Math.Max(8, Width / 4)))
   using (var gradient = new LinearGradientBrush(box, Color.FromArgb(118, 69, 250), Color.FromArgb(50, 177, 248), 40F)) g.FillPath(gradient, shape);
   using (var pen = new Pen(Color.White, Math.Max(2F, Width * .065F))) {
    pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round;
    g.DrawLines(pen, new[] {
     new PointF(Width * .24F, Height * .52F), new PointF(Width * .42F, Height * .70F), new PointF(Width * .76F, Height * .29F)
    });
   }
   using (var star = new SolidBrush(Color.White)) {
    g.FillEllipse(star, Width * .72F, Height * .14F, Width * .10F, Height * .10F);
    g.FillEllipse(star, Width * .83F, Height * .28F, Width * .045F, Width * .045F);
   }
  }
 }

 sealed class Badge : Control {
  public Badge() {
   SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer, true);
   BackColor = Color.Transparent; ForeColor = Palette.Accent;
   Font = new Font("Segoe UI Semibold", 8F);
  }
  protected override void OnPaint(PaintEventArgs e) {
   e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
   using (GraphicsPath path = Shapes.Round(new Rectangle(0, 0, Width - 1, Height - 1), Height / 2))
   using (var brush = new SolidBrush(Palette.AccentSoft)) e.Graphics.FillPath(brush, path);
   TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor,
    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
  }
 }

 sealed class Toggle : Control {
  bool value, hover;
  readonly string title, note, glyph;
  public event EventHandler CheckedChanged;
  public bool Checked {
   get { return value; }
   set { if (this.value == value) return; this.value = value; Invalidate(); if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty); }
  }
  public Toggle(string titleValue, string noteValue, bool initial) : this(titleValue, noteValue, initial, "") { }
  public Toggle(string titleValue, string noteValue, bool initial, string glyphValue) {
   title = titleValue; note = noteValue; value = initial; glyph = glyphValue;
   SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer, true);
   BackColor = Color.Transparent; Cursor = Cursors.Hand; TabStop = true;
   AccessibleRole = AccessibleRole.CheckButton; AccessibleName = title;
  }
  protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
  protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
  protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }
  protected override void OnKeyDown(KeyEventArgs e) {
   if (e.KeyCode == Keys.Space) { Checked = !Checked; e.Handled = true; }
   base.OnKeyDown(e);
  }
  protected override void OnPaint(PaintEventArgs e) {
   Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
   float d = Math.Max(1F, g.DpiX / 96F);
   Rectangle card = new Rectangle(1, 1, Width - 3, Height - 3);
   using (GraphicsPath path = Shapes.Round(card, (int)(13 * d))) {
    using (var fill = new SolidBrush(hover ? Palette.CardHover : Palette.Card)) g.FillPath(fill, path);
    using (var pen = new Pen(Palette.Border)) g.DrawPath(pen, path);
   }
   bool compact = Width < (int)(260 * d);
   int left = (int)((compact ? 10 : 14) * d);
   if (!String.IsNullOrEmpty(glyph)) {
    int tile = (int)((compact ? 28 : 32) * d), top = (Height - tile) / 2;
    using (GraphicsPath path = Shapes.Round(new Rectangle(left, top, tile, tile), (int)(10 * d)))
    using (var fill = new SolidBrush(Palette.AccentSoft)) g.FillPath(fill, path);
    using (var font = new Font("Segoe UI Symbol", compact ? 9.5F : 11F))
     TextRenderer.DrawText(g, glyph, font, new Rectangle(left, top, tile, tile), Palette.Text,
      TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    left += tile + (int)((compact ? 7 : 10) * d);
   }
   int trackWidth = (int)((compact ? 38 : 42) * d), trackHeight = (int)((compact ? 22 : 24) * d), margin = (int)((compact ? 9 : 13) * d);
   Rectangle track = new Rectangle(Width - trackWidth - margin, (Height - trackHeight) / 2, trackWidth, trackHeight);
   int textWidth = Math.Max(10, track.Left - left - (int)(8 * d));
   using (var font = new Font("Segoe UI Semibold", compact ? 8.1F : 9.5F))
    TextRenderer.DrawText(g, title, font, new Rectangle(left, String.IsNullOrEmpty(note) ? 0 : (int)(8 * d), textWidth, String.IsNullOrEmpty(note) ? Height : (int)(22 * d)), Palette.Text,
     TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
   if (!String.IsNullOrEmpty(note)) using (var font = new Font("Segoe UI", compact ? 7.1F : 7.8F))
    TextRenderer.DrawText(g, note, font, new Rectangle(left, (int)(29 * d), textWidth, (int)(18 * d)), Palette.Muted,
     TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
   using (GraphicsPath trackPath = Shapes.Round(track, trackHeight / 2))
   using (var fill = new LinearGradientBrush(track, Checked ? Palette.Accent : Color.FromArgb(191, 199, 218), Checked ? Palette.Accent2 : Color.FromArgb(191, 199, 218), 0F)) g.FillPath(fill, trackPath);
   int inset = (int)(3 * d), knobSize = trackHeight - inset * 2;
   int knob = Checked ? track.Right - knobSize - inset : track.Left + inset;
   using (var fill = new SolidBrush(Color.White)) g.FillEllipse(fill, knob, track.Top + inset, knobSize, knobSize);
   if (Focused) ControlPaint.DrawFocusRectangle(g, new Rectangle(4, 4, Width - 8, Height - 8), Palette.Accent, Color.Transparent);
  }
 }

 sealed class NavButton : Control {
  bool selected, hover;
  readonly string glyph;
  public bool Selected { get { return selected; } set { selected = value; Invalidate(); } }
  public NavButton(string text, string glyphValue) {
   Text = text; glyph = glyphValue; Cursor = Cursors.Hand; TabStop = true;
   SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer, true);
   BackColor = Color.Transparent; AccessibleRole = AccessibleRole.PushButton; AccessibleName = text;
  }
  protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
  protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
  protected override void OnPaint(PaintEventArgs e) {
   Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
   float d = Math.Max(1F, g.DpiX / 96F);
   if (selected || hover) using (GraphicsPath path = Shapes.Round(new Rectangle(1, 1, Width - 3, Height - 3), (int)(11 * d)))
   using (var fill = new SolidBrush(selected ? Color.FromArgb(221, 230, 255) : Color.FromArgb(233, 238, 250))) g.FillPath(fill, path);
   int icon = (int)(22 * d), top = (Height - icon) / 2;
   using (var font = new Font("Segoe UI Symbol", 11F))
    TextRenderer.DrawText(g, glyph, font, new Rectangle((int)(4 * d), top, icon, icon), selected ? Palette.Accent : Palette.Text,
     TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
   using (var font = new Font("Segoe UI", 7F))
    TextRenderer.DrawText(g, Text, font, new Rectangle((int)(27 * d), 0, Width - (int)(29 * d), Height), Palette.Text,
     TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
  }
 }

 sealed class ActionButton : Control {
  readonly bool primary;
  bool hover, pressed;
  public ActionButton(string text, bool isPrimary) {
   Text = text; primary = isPrimary; Cursor = Cursors.Hand; TabStop = true;
   SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer, true);
   BackColor = Color.Transparent; AccessibleRole = AccessibleRole.PushButton; AccessibleName = text;
  }
  protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
  protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
  protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
  protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
  protected override void OnKeyDown(KeyEventArgs e) {
   if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { OnClick(EventArgs.Empty); e.Handled = true; }
   base.OnKeyDown(e);
  }
  protected override void OnPaint(PaintEventArgs e) {
   Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
   Rectangle box = new Rectangle(1, 1, Width - 3, Height - 3);
   Color left = primary ? (pressed ? Color.FromArgb(68, 84, 220) : Palette.Accent) : (hover ? Palette.CardHover : Palette.Card);
   Color right = primary ? (pressed ? Color.FromArgb(91, 47, 204) : Palette.Accent2) : left;
   using (GraphicsPath path = Shapes.Round(box, Math.Max(8, Height / 3))) {
    using (var fill = new LinearGradientBrush(box, left, right, 0F)) g.FillPath(fill, path);
    if (!primary) using (var pen = new Pen(Palette.Border)) g.DrawPath(pen, path);
   }
   using (var font = new Font("Segoe UI Semibold", 8.7F))
    TextRenderer.DrawText(g, Text, font, ClientRectangle, primary ? Color.White : Palette.Text,
     TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
  }
 }
}
