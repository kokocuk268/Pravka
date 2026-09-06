using System;

namespace Pravka {
 internal static class CorrectionFlow {
  public static bool Apply(Decision decision, string input, string delimiter,
   Func<int,string,bool> replace, Action<ushort> switchLanguage) {
   if (decision == null || decision.Text == input) return false;
   if (!replace(input.Length, decision.Text + delimiter)) return false;
   if (decision.Kind == "layout" && switchLanguage != null)
    switchLanguage(Native.LanguageForText(decision.Text));
   return true;
  }
 }
}
