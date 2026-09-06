using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace Pravka {
 public sealed class Decision {
  public string Text, Suggestion, Kind;
  public Decision(string text, string kind, string suggestion) { Text = text; Kind = kind; Suggestion = suggestion; }
 }

 public sealed class Engine {
  sealed class WordEntry {
   public string Text; public long Frequency;
   public WordEntry(string text, long frequency) { Text = text; Frequency = frequency; }
  }
  sealed class Candidate {
   public string Text; public long Frequency; public int Distance; public double Score;
  }

  readonly Dictionary<string,long> words = new Dictionary<string,long>(StringComparer.Ordinal);
  readonly Dictionary<int,List<WordEntry>> buckets = new Dictionary<int,List<WordEntry>>();
  readonly Dictionary<string,string> known = new Dictionary<string,string>(StringComparer.Ordinal);
  readonly HashSet<string> ignored = new HashSet<string>(StringComparer.Ordinal);
  readonly string exceptionsPath;
  public int Count { get { return words.Count; } }

  public Engine(string root) {
   exceptionsPath = Path.Combine(root, "exceptions.txt");
   foreach (string language in new[] { "ru", "en" }) {
    foreach (string line in File.ReadLines(Path.Combine(root, "data", language + "_50k.txt"), Encoding.UTF8)) {
     string[] parts = line.Split(' '); long frequency;
     if (parts.Length == 2 && parts[0].All(Char.IsLetter) && Int64.TryParse(parts[1], out frequency))
      words[parts[0]] = frequency;
    }
   }
   foreach (KeyValuePair<string,long> item in words) {
    List<WordEntry> bucket;
    if (!buckets.TryGetValue(item.Key.Length, out bucket)) buckets[item.Key.Length] = bucket = new List<WordEntry>();
    bucket.Add(new WordEntry(item.Key, item.Value));
   }
   if (File.Exists(exceptionsPath)) {
    foreach (string line in File.ReadAllLines(exceptionsPath, Encoding.UTF8)) {
     string value = line.Trim().ToLowerInvariant();
     if (value.Length > 0 && value.All(Char.IsLetter)) ignored.Add(value);
    }
   }

   const string pairs =
    "дила=дела|првиет=привет|привте=привет|превет=привет|приветт=привет|спсибо=спасибо|" +
    "спаисбо=спасибо|спасбио=спасибо|спасиббо=спасибо|пожалуста=пожалуйста|" +
    "пожалуйтса=пожалуйста|пожлауйста=пожалуйста|извени=извини|извените=извините|" +
    "извните=извините|здраствуйте=здравствуйте|здравстуйте=здравствуйте|" +
    "здравтсвуйте=здравствуйте|севодня=сегодня|сегодян=сегодня|сегондя=сегодня|" +
    "сечас=сейчас|сейчса=сейчас|сейчасс=сейчас|завтро=завтра|заврта=завтра|" +
    "вчреа=вчера|есчо=ещё|ещол=ещё|хоршо=хорошо|хорого=хорошо|хоршоо=хорошо|" +
    "канечно=конечно|конешно=конечно|потомучто=потому что|патаму=потому|" +
    "потмоу=потому|пачему=почему|почмеу=почему|ничгео=ничего|ничево=ничего|" +
    "ничго=ничего|незнаю=не знаю|вообщем=в общем|будующем=будущем|" +
    "будующий=будущий|прилжоение=приложение|приложениие=приложение|" +
    "программмa=программа|компютер=компьютер|клавитура=клавиатура|" +
    "клавиатруа=клавиатура|рабоатет=работает|рабоать=работать|делатть=делать|" +
    "ошбика=ошибка|ошибкка=ошибка|настройик=настройки|настроики=настройки|" +
    "teh=the|hte=the|thsi=this|tihs=this|taht=that|thta=that|wiht=with|" +
    "wih=with|adn=and|nad=and|dont=don't|doesnt=doesn't|didnt=didn't|" +
    "cant=can't|isnt=isn't|wasnt=wasn't|wont=won't|recieve=receive|" +
    "recieved=received|definately=definitely|seperate=separate|occured=occurred|" +
    "becuase=because|beacuse=because|becasue=because|tomorow=tomorrow|" +
    "tommorow=tomorrow|tommorrow=tomorrow|helo=hello|helllo=hello|pleae=please|" +
    "plase=please|plese=please|thnaks=thanks|thankyou=thank you|alot=a lot|" +
    "freind=friend|frend=friend|wierd=weird|adress=address|langauge=language|" +
    "keybaord=keyboard";
   foreach (string pair in pairs.Split('|')) {
    int equals = pair.IndexOf('=');
    known[pair.Substring(0, equals)] = pair.Substring(equals + 1);
   }
  }

  public void Ignore(string input) {
   string value = input.ToLowerInvariant();
   lock (ignored) {
    if (!ignored.Add(value)) return;
    try { File.AppendAllText(exceptionsPath, value + Environment.NewLine, Encoding.UTF8); } catch { }
   }
  }
  internal void IgnoreForSession(string input) { lock (ignored) ignored.Add(input.ToLowerInvariant()); }

  static bool Cyrillic(char value) { return value >= 'а' && value <= 'я' || value == 'ё'; }
  static bool SameAlphabet(string a, string b) { return Cyrillic(a[0]) == Cyrillic(b[0]); }

  public static string Flip(string input) {
   const string en = "`qwertyuiop[]asdfghjkl;'zxcvbnm,.";
   const string ru = "ёйцукенгшщзхъфывапролджэячсмитьбю";
   var result = new StringBuilder(input.Length);
   foreach (char original in input) {
    char lower = Char.ToLowerInvariant(original), output;
    int index = en.IndexOf(lower);
    if (index >= 0) output = ru[index];
    else {
     index = ru.IndexOf(lower);
     if (index < 0) return input;
     output = en[index];
    }
    result.Append(Char.IsUpper(original) ? Char.ToUpperInvariant(output) : output);
   }
   return result.ToString();
  }

  static string MatchCase(string input, string output) {
   if (input.All(Char.IsUpper)) return output.ToUpperInvariant();
   if (Char.IsUpper(input[0])) return Char.ToUpperInvariant(output[0]) + output.Substring(1);
   return output;
  }

  long Frequency(string value) { long result; return words.TryGetValue(value, out result) ? result : 0; }

  static bool Neighbours(char a, char b) {
   string[] rows = Cyrillic(a)
    ? new[] { "йцукенгшщзхъ", "фывапролджэ", "ячсмитьбю" }
    : new[] { "qwertyuiop", "asdfghjkl", "zxcvbnm" };
   int ar = -1, br = -1, ac = 0, bc = 0;
   for (int row = 0; row < rows.Length; row++) {
    int column = rows[row].IndexOf(a); if (column >= 0) { ar = row; ac = column; }
    column = rows[row].IndexOf(b); if (column >= 0) { br = row; bc = column; }
   }
   return ar >= 0 && br >= 0 && Math.Abs(ar - br) <= 1 && Math.Abs(ac - bc) <= 1;
  }

  static double TypingBoost(string input, string candidate) {
   if (input.Length == candidate.Length) {
    int first = -1, differences = 0;
    for (int i = 0; i < input.Length; i++) if (input[i] != candidate[i]) { if (first < 0) first = i; differences++; }
    if (differences == 1 && Neighbours(input[first], candidate[first])) return 2.8;
    if (differences == 2 && first + 1 < input.Length
     && input[first] == candidate[first + 1] && input[first + 1] == candidate[first]) return 4.0;
   }
   if (input.Length == candidate.Length + 1) {
    for (int i = 1; i < input.Length; i++) if (input[i] == input[i - 1] && input.Remove(i, 1) == candidate) return 2.5;
   }
   return 1.0;
  }

  // Restricted Damerau-Levenshtein. Arrays are reused for the entire query to avoid
  // allocations while scanning the relevant word-length buckets.
  static int Distance(string a, string b, int limit, int[] older, int[] previous, int[] current) {
   if (Math.Abs(a.Length - b.Length) > limit) return limit + 1;
   int n = b.Length;
   for (int j = 0; j <= n; j++) previous[j] = j;
   for (int i = 1; i <= a.Length; i++) {
    current[0] = i;
    int from = Math.Max(1, i - limit), to = Math.Min(n, i + limit), rowBest = limit + 1;
    for (int j = 1; j < from; j++) current[j] = limit + 1;
    for (int j = from; j <= to; j++) {
     int value = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1), previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
     if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
      value = Math.Min(value, older[j - 2] + 1);
     current[j] = value; if (value < rowBest) rowBest = value;
    }
    for (int j = to + 1; j <= n; j++) current[j] = limit + 1;
    if (rowBest > limit) return limit + 1;
    int[] swap = older; older = previous; previous = current; current = swap;
   }
   return previous[n];
  }

  Candidate[] Nearby(string input, int limit) {
   var best = new List<Candidate>();
   int capacity = input.Length + limit + 2;
   int[] older = new int[capacity], previous = new int[capacity], current = new int[capacity];
   for (int length = Math.Max(3, input.Length - limit); length <= input.Length + limit; length++) {
    List<WordEntry> bucket; if (!buckets.TryGetValue(length, out bucket)) continue;
    foreach (WordEntry entry in bucket) {
     if (!SameAlphabet(input, entry.Text)) continue;
     int distance = Distance(input, entry.Text, limit, older, previous, current);
     if (distance == 0 || distance > limit) continue;
     double score = entry.Frequency * TypingBoost(input, entry.Text) / (distance == 1 ? 1.0 : 7.0);
     var candidate = new Candidate { Text = entry.Text, Frequency = entry.Frequency, Distance = distance, Score = score };
     int position = best.FindIndex(item => score > item.Score);
     if (position < 0) best.Add(candidate); else best.Insert(position, candidate);
     if (best.Count > 3) best.RemoveAt(3);
    }
   }
   return best.ToArray();
  }
  public Decision Check(string input, bool spelling, bool layout) {
   var unchanged = new Decision(input, "none", null);
   if (String.IsNullOrEmpty(input) || input.Length < 3 || input.Length > 32 || !input.All(Char.IsLetter)) return unchanged;
   string word = input.ToLowerInvariant();
   lock (ignored) if (ignored.Contains(word)) return unchanged;
   if (word.Any(Cyrillic) && word.Any(c => c >= 'a' && c <= 'z')) return unchanged;

   if (spelling && input.Length > 2 && Char.IsUpper(input[0]) && Char.IsUpper(input[1])
    && input.Skip(2).All(Char.IsLower)) {
    string fixedCaps = Char.ToUpperInvariant(word[0]) + word.Substring(1);
    if (Frequency(word) > 0) return new Decision(fixedCaps, "caps", null);
   }
   if (input.Skip(1).Any(Char.IsUpper) && !input.All(Char.IsUpper)) return unchanged;

   string replacement;
   if (spelling && known.TryGetValue(word, out replacement))
    return new Decision(MatchCase(input, replacement), "typo", null);
   if (Frequency(word) > 0) return unchanged;

   string flipped = Flip(word);
   long flippedFrequency = flipped == word ? 0 : Frequency(flipped);
   if (layout && flippedFrequency >= (word.Length <= 3 ? 5000 : 300))
    return new Decision(MatchCase(input, flipped), "layout", null);

   if (!spelling || word.Length < 4 || input.All(Char.IsUpper)) return unchanged;
   Candidate[] candidates = Nearby(word, word.Length >= 5 ? 2 : 1);
   if (candidates.Length == 0) return unchanged;
   Candidate first = candidates[0];
   double second = candidates.Length > 1 ? candidates[1].Score : 0;
   bool confident = first.Distance == 1
    ? first.Score >= 1400 && (second == 0 || first.Score >= second * 2.8)
    : first.Score >= 3500 && (second == 0 || first.Score >= second * 5.0);
   if (confident) return new Decision(MatchCase(input, first.Text), "typo", null);
   return new Decision(input, "none", MatchCase(input, first.Text));
  }
 }
}
