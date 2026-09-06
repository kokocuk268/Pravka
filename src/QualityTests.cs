using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace Pravka {
 static class QualityTests {
  sealed class Result {
   public int Total, Exact, Unchanged, Wrong;
   public readonly List<string> Samples = new List<string>();
  }

  static string Swap(string word) {
   int position = Math.Max(1, word.Length / 2 - 1);
   if (word[position] == word[position + 1]) {
    position = -1;
    for (int i = 1; i < word.Length - 1; i++) if (word[i] != word[i + 1]) { position = i; break; }
   }
   if (position < 0) return null;
   char[] value = word.ToCharArray(); char saved = value[position];
   value[position] = value[position + 1]; value[position + 1] = saved;
   return new string(value);
  }

  static string Drop(string word) { return word.Remove(Math.Max(1, word.Length / 2), 1); }
  static string Double(string word) {
   int position = Math.Max(1, word.Length / 2);
   return word.Insert(position, word[position].ToString());
  }

  static Result Probe(Engine engine, string[] words, string kind) {
   var result = new Result();
   foreach (string target in words) {
    string input = kind == "swap" ? Swap(target) : kind == "drop" ? Drop(target) : Double(target);
    if (String.IsNullOrEmpty(input) || input == target) continue;
    result.Total++;
    Decision decision = engine.Check(input, true, true);
    if (decision.Text == target) result.Exact++;
    else if (decision.Text == input) result.Unchanged++;
    else {
     result.Wrong++;
     if (result.Samples.Count < 8) result.Samples.Add(input + " -> " + decision.Text + " (ожидалось " + target + ")");
    }
   }
   return result;
  }

  static string[] Words(string root, string language, int count) {
   return File.ReadLines(Path.Combine(root, "data", language + "_50k.txt"), Encoding.UTF8)
    .Select(line => line.Split(' ')[0].ToLowerInvariant())
    .Where(word => word.Length >= 6 && word.Length <= 10 && word.All(ch => language == "ru"
     ? ch >= 'а' && ch <= 'я' || ch == 'ё'
     : ch >= 'a' && ch <= 'z'))
    .Distinct().Take(count).ToArray();
  }

  public static int Run(string root, string output) {
   var watch = Stopwatch.StartNew();
   var lines = new List<string>(); int failed = 0, cases = 0;
   var engine = new Engine(root);
   Action<string,bool,string> assert = (name, ok, details) => {
    if (!ok) failed++;
    lines.Add((ok ? "PASS" : "FAIL") + " | " + name + " | " + details);
   };

   string[] russian = Words(root, "ru", 1000), english = Words(root, "en", 500);
   assert("русская выборка загружена", russian.Length == 1000, "слов=" + russian.Length);
   assert("английская выборка загружена", english.Length == 500, "слов=" + english.Length);
   assert("расширенный словарь загружен", engine.Count >= 190000, "форм=" + engine.Count);

   foreach (string language in new[] { "RU", "EN" }) {
    string[] words = language == "RU" ? russian : english;
    foreach (string kind in new[] { "swap", "drop", "double" }) {
     Result result = Probe(engine, words, kind); cases += result.Total;
     string details = "всего=" + result.Total + ", исправлено=" + result.Exact
      + ", оставлено=" + result.Unchanged + ", неверно=" + result.Wrong;
     assert(language + " " + kind + ": нет ошибочных автозамен", result.Wrong == 0, details);
     int minimum = kind == "swap" ? (language == "RU" ? 950 : 460)
      : kind == "double" ? (language == "RU" ? 940 : 460)
      : language == "RU" ? 100 : 0;
     assert(language + " " + kind + ": полезные исправления", result.Exact >= minimum, details + ", минимум=" + minimum);
     foreach (string sample in result.Samples) lines.Add("  " + sample);
    }

    int changed = 0;
    foreach (string word in words) if (engine.Check(word, true, true).Text != word) changed++;
    cases += words.Length;
    assert(language + ": правильные слова не меняются", changed == 0, "изменено=" + changed + "/" + words.Length);
   }

   watch.Stop();
   lines.Add("Quality cases: " + cases);
   lines.Add("Elapsed ms: " + watch.ElapsedMilliseconds);
   lines.Add("Failed: " + failed);
   File.WriteAllLines(output, lines, Encoding.UTF8);
   return failed == 0 ? 0 : 1;
  }
 }
}
