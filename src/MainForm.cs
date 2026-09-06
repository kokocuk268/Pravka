using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Pravka {
 sealed class MainForm : Form {
  const string Version = "0.4.0";
  readonly string config;
  readonly float scale;
  readonly Toggle master, spelling, layout;
  readonly Toggle settingsMaster, settingsSpelling, settingsLayout;
  readonly Label status, count, counter, placeholder;
  readonly NavButton homeNav, settingsNav, aboutNav;
  readonly Panel homePage, settingsPage, aboutPage;
  readonly Timer uiTimer;
  Controller controller;
  NotifyIcon tray;
  Hint hint;
  bool quitting, syncing;

  int U(int value) { return Math.Max(1, (int)Math.Round(value * scale)); }
  Point P(int x, int y) { return new Point(U(x), U(y)); }
  Size Z(int width, int height) { return new Size(U(width), U(height)); }

  Label TextLabel(string text, float size, Color color, FontStyle style, int x, int y, int width, int height) {
   return new Label {
    Text = text, AutoSize = false, Location = P(x, y), Size = Z(width, height),
    ForeColor = color, BackColor = Color.Transparent, Font = new Font("Segoe UI", size, style)
   };
  }

  public MainForm(string root, bool startHidden) : this(root, startHidden, false) { }

  internal MainForm(string root, bool startHidden, bool preview) {
   AutoScaleMode = AutoScaleMode.None;
   Font = new Font("Segoe UI", 9F);
   using (Graphics graphics = CreateGraphics()) scale = Math.Max(1F, graphics.DpiX / 96F);

   Text = "Правка";
   ForeColor = Palette.Text;
   BackColor = Palette.Window;
   FormBorderStyle = FormBorderStyle.FixedSingle;
   ClientSize = Z(528, 480);
   StartPosition = FormStartPosition.CenterScreen;
   MaximizeBox = false;
   MinimumSize = Size;
   MaximumSize = Size;
   try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { Icon = SystemIcons.Application; }
   config = Path.Combine(root, "settings.txt");

   var sidebar = new Panel { Location = P(0, 0), Size = Z(100, 480), BackColor = Palette.Sidebar };
   var main = new SoftPanel { Location = P(100, 0), Size = Z(428, 480) };
   Controls.Add(sidebar); Controls.Add(main);

   var sideLogo = new LogoControl { Location = P(29, 17), Size = Z(42, 42) };
   sidebar.Controls.Add(sideLogo);
   sidebar.Controls.Add(TextLabel("Правка", 10F, Palette.Text, FontStyle.Bold, 22, 64, 60, 20));
   sidebar.Controls.Add(TextLabel("v" + Version, 7.6F, Palette.Muted, FontStyle.Regular, 22, 84, 60, 16));

   homeNav = new NavButton("Главная", "⌂") { Location = P(8, 118), Size = Z(84, 38), Selected = true };
   settingsNav = new NavButton("Настройки", "⚙") { Location = P(8, 162), Size = Z(84, 38) };
   aboutNav = new NavButton("О Правке", "ⓘ") { Location = P(8, 206), Size = Z(84, 38) };
   sidebar.Controls.Add(homeNav); sidebar.Controls.Add(settingsNav); sidebar.Controls.Add(aboutNav);
   sidebar.Controls.Add(TextLabel("♥", 11F, Color.FromArgb(120, 135, 173), FontStyle.Regular, 17, 423, 18, 20));
   sidebar.Controls.Add(TextLabel("Текст\nлучше", 7.3F, Palette.Muted, FontStyle.Regular, 36, 421, 56, 34));

   var hero = new Panel { Location = P(0, 0), Size = Z(428, 88), BackColor = Color.Transparent };
   hero.Controls.Add(new LogoControl { Location = P(14, 16), Size = Z(52, 52) });
   hero.Controls.Add(new Badge { Text = "ПРАВКА " + Version, Location = P(78, 10), Size = Z(92, 18) });
   hero.Controls.Add(TextLabel("Автокоррекция на лету", 19F, Palette.Text, FontStyle.Bold, 78, 28, 310, 32));
   hero.Controls.Add(TextLabel("Русский + English  ·  без сочетаний клавиш", 8.5F, Palette.Muted, FontStyle.Regular, 79, 61, 306, 20));
   hero.Controls.Add(TextLabel("пиши легко  ♡", 7.5F, Color.FromArgb(111, 92, 226), FontStyle.Italic, 322, 10, 88, 18));
   main.Controls.Add(hero);

   var host = new Panel { Location = P(0, 88), Size = Z(428, 392), BackColor = Color.Transparent };
   main.Controls.Add(host);
   homePage = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
   settingsPage = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Visible = false };
   aboutPage = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Visible = false };
   host.Controls.Add(homePage); host.Controls.Add(settingsPage); host.Controls.Add(aboutPage);

   var statusCard = new RoundedPanel { Location = P(14, 3), Size = Z(400, 46), Radius = U(13) };
   statusCard.Controls.Add(new Pulse { Location = P(14, 12), Size = Z(22, 22) });
   status = TextLabel("Готова к работе", 10F, Palette.Text, FontStyle.Bold, 49, 5, 330, 20);
   count = TextLabel("Исправления применяются автоматически", 7.7F, Palette.Muted, FontStyle.Regular, 49, 25, 330, 16);
   statusCard.Controls.Add(status); statusCard.Controls.Add(count); homePage.Controls.Add(statusCard);

   master = new Toggle("Исправлять при наборе", "После пробела и знаков препинания", true, "⌨") {
    Location = P(14, 55), Size = Z(400, 46)
   };
   spelling = new Toggle("Умные опечатки", "RU + EN · 1–2 ошибки", true, "Aa") {
    Location = P(14, 107), Size = Z(196, 56)
   };
   layout = new Toggle("Автораскладка", "ghbdtn → привет", true, "⚡") {
    Location = P(218, 107), Size = Z(196, 56)
   };
   homePage.Controls.Add(master); homePage.Controls.Add(spelling); homePage.Controls.Add(layout);

   var testCard = new RoundedPanel { Location = P(14, 169), Size = Z(400, 102), Radius = U(13) };
   testCard.Controls.Add(TextLabel("Проверь прямо здесь", 8.5F, Palette.Text, FontStyle.Bold, 12, 7, 245, 18));
   var test = new RichTextBox {
    Location = P(12, 29), Size = Z(376, 47), BorderStyle = BorderStyle.None, Multiline = true,
    DetectUrls = false, BackColor = Color.White, ForeColor = Palette.Text,
    Font = new Font("Segoe UI", 13F), AcceptsTab = false, MaxLength = 1000
   };
   placeholder = TextLabel("Набери текст, и Правка его исправит…", 9F, Color.FromArgb(153, 165, 195), FontStyle.Regular, 17, 35, 310, 24);
   placeholder.Cursor = Cursors.IBeam;
   testCard.Controls.Add(test); testCard.Controls.Add(placeholder); placeholder.BringToFront();
   testCard.Controls.Add(TextLabel("Примеры:  ghbdtn  ·  дила?  ·  wrold", 7.4F, Palette.Muted, FontStyle.Regular, 13, 80, 292, 16));
   counter = TextLabel("0/1000", 7.4F, Palette.Muted, FontStyle.Regular, 330, 80, 55, 16);
   counter.TextAlign = ContentAlignment.TopRight; testCard.Controls.Add(counter);
   placeholder.Click += (s, e) => test.Focus();
   test.TextChanged += (s, e) => { placeholder.Visible = test.TextLength == 0; counter.Text = test.TextLength + "/1000"; };
   homePage.Controls.Add(testCard);

   var tip = new RoundedPanel { Location = P(14, 277), Size = Z(400, 44), Radius = U(12), Fill = Color.FromArgb(243, 248, 255), Border = Color.FromArgb(216, 229, 252) };
   tip.Controls.Add(TextLabel("💡", 11F, Palette.Accent, FontStyle.Regular, 13, 9, 27, 25));
   tip.Controls.Add(TextLabel("Backspace после замены вернёт слово.\nПравка запомнит исключение.", 7.3F, Palette.Text, FontStyle.Regular, 44, 5, 338, 34));
   homePage.Controls.Add(tip);
   AddFooter(homePage);

   settingsPage.Controls.Add(TextLabel("Настройки", 15F, Palette.Text, FontStyle.Bold, 14, 6, 300, 28));
   settingsPage.Controls.Add(TextLabel("Изменения сохраняются автоматически", 8F, Palette.Muted, FontStyle.Regular, 15, 35, 330, 19));
   settingsMaster = new Toggle("Исправлять при наборе", "Главный переключатель", true, "⌨") { Location = P(14, 62), Size = Z(400, 54) };
   settingsSpelling = new Toggle("Умные опечатки", "Русский и английский", true, "Aa") { Location = P(14, 123), Size = Z(400, 54) };
   settingsLayout = new Toggle("Автораскладка", "Исправляет слово и переключает RU/EN", true, "⚡") { Location = P(14, 184), Size = Z(400, 54) };
   settingsPage.Controls.Add(settingsMaster); settingsPage.Controls.Add(settingsSpelling); settingsPage.Controls.Add(settingsLayout);
   var startup = new RoundedPanel { Location = P(14, 246), Size = Z(400, 65), Radius = U(13) };
   startup.Controls.Add(TextLabel("Запуск вместе с Windows", 9F, Palette.Text, FontStyle.Bold, 15, 9, 260, 20));
   startup.Controls.Add(TextLabel("Используется ярлык Pravka.exe --background", 7.7F, Palette.Muted, FontStyle.Regular, 15, 32, 350, 19));
   settingsPage.Controls.Add(startup); AddFooter(settingsPage);

   aboutPage.Controls.Add(TextLabel("О программе", 15F, Palette.Text, FontStyle.Bold, 14, 6, 300, 28));
   var aboutCard = new RoundedPanel { Location = P(14, 48), Size = Z(400, 220), Radius = U(15) };
   aboutCard.Controls.Add(new LogoControl { Location = P(18, 18), Size = Z(58, 58) });
   aboutCard.Controls.Add(TextLabel("Правка " + Version, 14F, Palette.Text, FontStyle.Bold, 90, 17, 250, 28));
   aboutCard.Controls.Add(TextLabel("Бесплатный локальный автокорректор для Windows", 8F, Palette.Muted, FontStyle.Regular, 91, 48, 270, 30));
   aboutCard.Controls.Add(TextLabel("✓  исправляет опечатки RU + EN\n✓  меняет неверную раскладку автоматически\n✓  не отправляет набранный текст в интернет\n✓  исходный код открыт по лицензии MIT", 8.5F, Palette.Text, FontStyle.Regular, 19, 92, 355, 94));
   aboutCard.Controls.Add(TextLabel("95 595 слов  ·  без аккаунта  ·  без телеметрии", 7.7F, Palette.Muted, FontStyle.Regular, 19, 191, 355, 20));
   aboutPage.Controls.Add(aboutCard); AddFooter(aboutPage);

   homeNav.Click += (s, e) => ShowPage(homePage, homeNav);
   settingsNav.Click += (s, e) => ShowPage(settingsPage, settingsNav);
   aboutNav.Click += (s, e) => ShowPage(aboutPage, aboutNav);

   ReadSettings();
   WirePair(master, settingsMaster); WirePair(spelling, settingsSpelling); WirePair(layout, settingsLayout);

   if (preview) {
    status.Text = "Готова к работе";
    count.Text = "Исправления применяются автоматически";
    return;
   }

   var engine = new Engine(root);
   Handle.ToString();
   controller = new Controller(this, engine);
   hint = new Hint();
   controller.Suggest = value => hint.Display("Возможно: " + value);
   controller.Changed = () => {
    status.Text = controller.Status;
    count.Text = "Исправлений: " + controller.Corrections + "  ·  всё локально";
   };
   uiTimer = new Timer { Interval = 250 };
   uiTimer.Tick += (s, e) => controller.Changed();
   uiTimer.Start();

   var menu = new ContextMenuStrip();
   menu.Items.Add("Открыть Правку", null, (s, e) => Restore());
   menu.Items.Add("Пауза / продолжить", null, (s, e) => master.Checked = !master.Checked);
   menu.Items.Add("Выключить", null, (s, e) => Quit());
   tray = new NotifyIcon { Icon = Icon, Text = "Правка · включена", Visible = true, ContextMenuStrip = menu };
   tray.DoubleClick += (s, e) => Restore();
   Apply();
   if (startHidden) Shown += (s, e) => Hide();
   FormClosing += (s, e) => { if (!quitting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
  }

  void AddFooter(Panel page) {
   page.Controls.Add(TextLabel("☾  Автопауза: IDE · терминалы · пароли", 7.6F, Palette.Muted, FontStyle.Regular, 15, 329, 210, 22));
   var hide = new ActionButton("—  В трей", false) { Location = P(222, 347), Size = Z(92, 36) };
   hide.Click += (s, e) => Hide(); page.Controls.Add(hide);
   var exit = new ActionButton("⏻  Выключить", true) { Location = P(320, 347), Size = Z(94, 36) };
   exit.Click += (s, e) => Quit(); page.Controls.Add(exit);
  }

  void ShowPage(Panel page, NavButton nav) {
   homePage.Visible = page == homePage; settingsPage.Visible = page == settingsPage; aboutPage.Visible = page == aboutPage;
   homeNav.Selected = nav == homeNav; settingsNav.Selected = nav == settingsNav; aboutNav.Selected = nav == aboutNav;
   page.BringToFront();
  }

  internal void ShowPreviewPage(string page) {
   if (page == "settings") ShowPage(settingsPage, settingsNav);
   else if (page == "about") ShowPage(aboutPage, aboutNav);
   else ShowPage(homePage, homeNav);
  }

  void WirePair(Toggle first, Toggle second) {
   first.CheckedChanged += (s, e) => SyncPair(first, second);
   second.CheckedChanged += (s, e) => SyncPair(second, first);
  }

  void SyncPair(Toggle source, Toggle target) {
   if (syncing) return;
   syncing = true; target.Checked = source.Checked; syncing = false; Apply();
  }

  void ReadSettings() {
   bool enabled = true, typo = true, autoLayout = true;
   if (File.Exists(config)) foreach (string line in File.ReadAllLines(config)) {
    string[] pair = line.Split('='); if (pair.Length != 2) continue;
    bool value = pair[1] == "true";
    if (pair[0] == "enabled") enabled = value;
    if (pair[0] == "spelling") typo = value;
    if (pair[0] == "layout") autoLayout = value;
   }
   syncing = true;
   master.Checked = settingsMaster.Checked = enabled;
   spelling.Checked = settingsSpelling.Checked = typo;
   layout.Checked = settingsLayout.Checked = autoLayout;
   syncing = false;
  }

  void Apply() {
   if (controller != null) {
    controller.Enabled = master.Checked; controller.Spelling = spelling.Checked; controller.Layout = layout.Checked;
    controller.ResetState();
   }
   if (tray != null) tray.Text = master.Checked ? "Правка · включена" : "Правка · пауза";
   try { File.WriteAllLines(config, new[] {
    "enabled=" + master.Checked.ToString().ToLowerInvariant(),
    "spelling=" + spelling.Checked.ToString().ToLowerInvariant(),
    "layout=" + layout.Checked.ToString().ToLowerInvariant()
   }); } catch { }
  }

  void Restore() { Show(); WindowState = FormWindowState.Normal; Activate(); }
  void Quit() {
   quitting = true;
   if (controller != null) { controller.Dispose(); controller = null; }
   if (uiTimer != null) uiTimer.Stop();
   if (tray != null) { tray.Visible = false; tray.Dispose(); tray = null; }
   if (hint != null) { hint.Dispose(); hint = null; }
   Close();
  }
  protected override void Dispose(bool disposing) {
   if (disposing) {
    if (controller != null) { controller.Dispose(); controller = null; }
    if (uiTimer != null) uiTimer.Dispose();
    if (tray != null) tray.Dispose();
    if (hint != null) hint.Dispose();
   }
   base.Dispose(disposing);
  }
 }

 sealed class Hint : Form {
  readonly Label label;
  readonly Timer life;
  protected override bool ShowWithoutActivation { get { return true; } }
  protected override CreateParams CreateParams { get { CreateParams p = base.CreateParams; p.ExStyle |= 0x08000000 | 0x80; return p; } }
  public Hint() {
   AutoScaleMode = AutoScaleMode.Dpi; FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
   BackColor = Color.White; ClientSize = new Size(340, 52);
   label = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Palette.Text, BackColor = Palette.AccentSoft, Font = new Font("Segoe UI Semibold", 10F) };
   Controls.Add(label);
   life = new Timer { Interval = 2400 };
   life.Tick += (s, e) => { Hide(); label.Text = ""; life.Stop(); };
  }
  public void Display(string value) {
   label.Text = value; Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
   Location = new Point(area.Right - Width - 18, area.Bottom - Height - 18);
   Show(); life.Stop(); life.Start();
  }
  protected override void Dispose(bool disposing) { if (disposing) life.Dispose(); base.Dispose(disposing); }
 }
}
