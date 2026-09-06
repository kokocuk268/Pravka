using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace Pravka {
 static class Tests {
  public static int Run(string root, string output) {
   var engine = new Engine(root);
   var lines = new List<string>();
   int failed = 0;
   Action<string,string,bool,bool> check = (input, expected, spelling, layout) => {
    Decision result = engine.Check(input, spelling, layout);
    bool ok = result.Text == expected;
    if (!ok) failed++;
    lines.Add((ok ? "PASS" : "FAIL") + " | " + input + " -> " + result.Text + " | expected " + expected
     + (result.Suggestion == null ? "" : " | suggestion " + result.Suggestion));
   };
   Action<string,bool,string> state = (name, ok, details) => {
    if (!ok) failed++;
    lines.Add((ok ? "PASS" : "FAIL") + " | " + name + (String.IsNullOrEmpty(details) ? "" : " | " + details));
   };
   string[] cases = {
    "деоа|дела", "Деоа|Дела", "дила|дела", "слвоо|слово", "дела|дела", "тело|тело",
    "мама|мама", "првиет|привет", "Првиет|Привет", "ПРВИЕТ|ПРИВЕТ", "спсибо|спасибо",
    "есчо|ещё", "пожалуста|пожалуйста", "becuase|because", "Recieve|Receive",
    "ghbdtn|привет", "xnj|что", "ltkftim|делаешь", "руддщ|hello", "привет|привет", "работает|работает", "мир|мир",
    "hello|hello", "test|test", "wrold|world", "progarm|program", "мошина|машина",
    "каробка|коробка", "пирветт|привет", "becasee|because", "ПРивет|Привет", "HEllo|Hello",
    "малако|молоко", "сабака|собака", "машына|машина", "харошо|хорошо",
    "работта|работа", "теккс|текст", "интерфес|интерфейс", "пользоваватель|пользователь",
    "приложние|приложение", "испровить|исправить", "расскладка|раскладка", "пичатаю|печатаю",
    "непонял|не понял", "НЕПОНЯЛ|НЕ ПОНЯЛ", "ситсема|система", "системаа|система",
    "систнма|система", "компьютре|компьютер", "порграмма|программа", "современынй|современный",
    "корова|корова", "длина|длина", "artist|artist", "userName|userName", "abc123|abc123",
    "hello@example.com|hello@example.com", "C:\\Users|C:\\Users", "12345|12345",
    "прivет|прivет", "тест_код|тест_код", "API|API", "кот|кот",
    "кирпичный|кирпичный", "перемалывает|перемалывает", "неопасен|неопасен",
    "автокоррекция|автокоррекция", "гитхаб|гитхаб", "нейросеть|нейросеть"
   };
   foreach (string item in cases) {
    string[] pair = item.Split('|');
    check(pair[0], pair[1], true, true);
   }
   var phrase = new List<string>();
   foreach (string word in "xnj ltkftim".Split(' ')) phrase.Add(engine.Check(word, true, true).Text);
   string phraseResult = String.Join(" ", phrase.ToArray());
   bool phraseOk = phraseResult == "что делаешь";
   if (!phraseOk) failed++;
   lines.Add((phraseOk ? "PASS" : "FAIL") + " | xnj ltkftim -> " + phraseResult + " | expected что делаешь");
   check("првиет", "првиет", false, false);
   check("ghbdtn", "ghbdtn", true, false);
   check("becuase", "becuase", false, false);
   check("малако", "малако", false, true);
   engine.IgnoreForSession("првиет");
   check("првиет", "првиет", true, true);
   Decision cautious = engine.Check("компютре", true, true);
   state("ambiguous omission stays a suggestion", cautious.Text == "компютре" && cautious.Suggestion == "компьютер",
    cautious.Text + " / " + cautious.Suggestion);
   state("recognition vocabulary is expanded", engine.Count >= 190000, "entries=" + engine.Count);

   IntPtr window = new IntPtr(101), focus = new IntPtr(202);
   var buffer = new WordBuffer();
   buffer.PushLetter('x', window, focus);
   buffer.FocusNotification();
   buffer.PushLetter('n', window, focus); buffer.PushLetter('j', window, focus);
   bool blocked; string buffered = buffer.Take(window, focus, out blocked);
   state("delayed focus event keeps first word", buffered == "xnj" && !blocked, buffered);

   buffer.PushLetter('x', window, focus);
   buffer.PushLetter('n', new IntPtr(303), focus);
   buffered = buffer.Take(new IntPtr(303), focus, out blocked);
   state("real target change starts a new word", buffered == "n" && !blocked, buffered);

   buffer.MarkShortcut(window, focus); buffer.Backspace(window, focus);
   buffer.PushLetter('x', window, focus); buffer.PushLetter('n', window, focus); buffer.PushLetter('j', window, focus);
   buffered = buffer.Take(window, focus, out blocked);
   state("Ctrl+A Backspace allows first word", buffered == "xnj" && !blocked, buffered);

   buffer.MarkUnknownEdit(window, focus); buffer.PushLetter('x', window, focus);
   buffered = buffer.Take(window, focus, out blocked);
   state("unknown edit skips only current fragment", buffered == "" && blocked, buffered);

   int replacements = 0, switches = 0; ushort language = 0;
   bool applied = CorrectionFlow.Apply(new Decision("привет", "layout", null), "ghbdtn", " ",
    (remove, replacement) => { replacements++; return remove == 6 && replacement == "привет "; },
    value => { switches++; language = value; });
   state("layout correction replaces and switches", applied && replacements == 1 && switches == 1 && language == 0x0419, language.ToString("x4"));

   switches = 0;
   applied = CorrectionFlow.Apply(new Decision("привет", "layout", null), "ghbdtn", " ",
    (remove, replacement) => false, value => switches++);
   state("failed replacement never switches layout", !applied && switches == 0, "switches=" + switches);

   switches = 0;
   applied = CorrectionFlow.Apply(new Decision("world", "typo", null), "wrold", " ",
    (remove, replacement) => true, value => switches++);
   state("typo does not switch layout", applied && switches == 0, "switches=" + switches);
   state("Russian text selects RU", Native.LanguageForText("привет") == 0x0419, "");
   state("English text selects EN", Native.LanguageForText("hello") == 0x0409, "");
   lines.Add("Dictionary entries: " + engine.Count);
   lines.Add("Failed: " + failed);
   File.WriteAllLines(output, lines, Encoding.UTF8);
   return failed == 0 ? 0 : 1;
  }
 }
}
