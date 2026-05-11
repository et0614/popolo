/* Section7Runner.ReferenceCompare.cs
 *
 * Section7Runner の partial 分割 — Std140_TF_Results.xlsx (Informative Annex B8)
 * の参照ツール 6 本分集計 (Tables 1-2 の Min/Max/Mean) を読み出し、Popolo の結果と
 * 突合して envelope 内外・偏差をコンソールにプリントする。
 *
 * 規範的合否判定は <see cref="A3Evaluator"/> 側で別途実行される (Annex A3, Normative)。
 * 本ファイルが行う比較は §4.4.1 が明示するとおり formal acceptance criteria では
 * なく、診断目的の参照値突合である。
 */

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace BESTEST_2023
{
  internal static partial class Section7Runner
  {
    #region 参照値突合 (Std140_TF_Results.xlsx)

    /// <summary>1 ケース・1 メトリックの参照範囲 (Min/Max/Mean of 6 ref tools)。</summary>
    private struct RefRange
    {
      public double Min;
      public double Max;
      public double Mean;
      public bool HasValue;
    }

    /// <summary>
    /// Std140_TF_Results.xlsx (Informative Annex B8) の参照ツール 6 本分集計と Popolo の
    /// 結果を突合し、envelope 内/外と Mean からの偏差をプリントする。
    /// </summary>
    private static void CompareWithReference(List<CaseResult> results, string xlsxPath)
    {
      Console.WriteLine($"=== Reference comparison vs {Path.GetFileName(xlsxPath)} ===");
      Console.WriteLine($"  (envelope = [Min, Max] of 6 ref tools: BSIMAC/CSE/DeST/EnergyPlus/ESP-r/TRNSYS)");

      Dictionary<int, RefRange> heating, cooling, peakH, peakC;
      using (var doc = SpreadsheetDocument.Open(xlsxPath, false))
      {
        var wbp = doc.WorkbookPart!;
        var sst = wbp.SharedStringTablePart?.SharedStringTable;
        SheetData? sd1 = GetSheetData(wbp, "Tables 1");
        SheetData? sd2 = GetSheetData(wbp, "Tables 2");
        if (sd1 == null || sd2 == null)
        {
          Console.WriteLine("  WARN: 'Tables 1' or 'Tables 2' sheet not found in reference xlsx");
          return;
        }
        // Tables 1: B8-1 (heating, header r7), B8-2 (cooling, header r59)
        // 列レイアウト: 各ツール 1 列 → Min/Max/Mean = I/J/K
        heating = ParseRefTable(sd1, sst, headerRow: 7,  minCol: "I",  maxCol: "J",  meanCol: "K");
        cooling = ParseRefTable(sd1, sst, headerRow: 59, minCol: "I",  maxCol: "J",  meanCol: "K");
        // Tables 2: B8-3 (peak h, r7), B8-4 (peak c, r59)
        // 列レイアウト: 各ツール 4 列 (kW/Mo/Day/Hr) → Min/Max/Mean = AA/AB/AC
        peakH = ParseRefTable(sd2, sst, headerRow: 7,  minCol: "AA", maxCol: "AB", meanCol: "AC");
        peakC = ParseRefTable(sd2, sst, headerRow: 59, minCol: "AA", maxCol: "AB", meanCol: "AC");
      }

      PrintComparison("Annual Heating Load [MWh]", results,
          r => !r.IsFreeFloat, r => r.AnnualHeating_MWh, heating);
      PrintComparison("Annual Cooling Load [MWh]", results,
          r => !r.IsFreeFloat, r => r.AnnualCooling_MWh, cooling);
      PrintComparison("Annual Peak Heating [kW]", results,
          r => !r.IsFreeFloat && r.PeakHeating_kW > 0, r => r.PeakHeating_kW, peakH);
      PrintComparison("Annual Peak Cooling [kW]", results,
          r => !r.IsFreeFloat && r.PeakCooling_kW > 0, r => r.PeakCooling_kW, peakC);
    }

    /// <summary>
    /// 表をケース番号で辞書化。データは <paramref name="headerRow"/>+4 から始まり、
    /// Min/Max/Mean は <paramref name="minCol"/>/<paramref name="maxCol"/>/<paramref name="meanCol"/> の列にあるとする。
    /// </summary>
    private static Dictionary<int, RefRange> ParseRefTable(
        SheetData sd, SharedStringTable? sst, uint headerRow,
        string minCol, string maxCol, string meanCol)
    {
      var dict = new Dictionary<int, RefRange>();
      uint dataStart = headerRow + 4;
      for (uint r = dataStart; r < dataStart + 50; r++)
      {
        var row = sd.Elements<Row>().FirstOrDefault(x => x.RowIndex?.Value == r);
        if (row == null) continue;
        string desc = GetCellText(row, $"B{r}", sst);
        if (string.IsNullOrEmpty(desc)) break;
        if (desc.StartsWith("Table ") || desc.StartsWith("**") || desc.StartsWith("Simulation "))
          break;

        int sp = desc.IndexOf(' ');
        if (sp <= 0) continue;
        if (!int.TryParse(desc.Substring(0, sp), out int caseNum)) continue;

        double min  = ParseDouble(GetCellText(row, $"{minCol}{r}",  sst));
        double max  = ParseDouble(GetCellText(row, $"{maxCol}{r}",  sst));
        double mean = ParseDouble(GetCellText(row, $"{meanCol}{r}", sst));
        bool has = !double.IsNaN(min) && !double.IsNaN(max) && !double.IsNaN(mean);

        dict[caseNum] = new RefRange { Min = min, Max = max, Mean = mean, HasValue = has };
      }
      return dict;
    }

    private static void PrintComparison(string title,
        List<CaseResult> results,
        Func<CaseResult, bool> includePredicate,
        Func<CaseResult, double> selector,
        Dictionary<int, RefRange> refDict)
    {
      Console.WriteLine();
      Console.WriteLine($"  ── {title} ──");
      Console.WriteLine($"  {"Case",-7} {"Popolo",10} {"Ref Min",10} {"Ref Max",10} {"Ref Mean",10}  {"Δ%(Mean)",10}  Status");

      int inEnv = 0, above = 0, below = 0, noRef = 0;
      foreach (var r in results)
      {
        if (!includePredicate(r)) continue;
        int? cn = GetCaseNumber(r.Case);
        if (cn == null) continue;
        double pop = selector(r);
        if (!refDict.TryGetValue(cn.Value, out RefRange rr) || !rr.HasValue)
        {
          Console.WriteLine($"  {r.Case,-7} {pop,10:F3}    --       --        --          --     no-ref");
          noRef++;
          continue;
        }
        double devPct = rr.Mean != 0 ? (pop - rr.Mean) / Math.Abs(rr.Mean) * 100.0 : 0;
        string status;
        if      (pop < rr.Min) { status = "BELOW";    below++; }
        else if (pop > rr.Max) { status = "ABOVE";    above++; }
        else                   { status = "in env";   inEnv++; }
        Console.WriteLine($"  {r.Case,-7} {pop,10:F3} {rr.Min,10:F3} {rr.Max,10:F3} {rr.Mean,10:F3}  {devPct,9:+0.0;-0.0;0.0}%  {status}");
      }
      Console.WriteLine($"  Summary: {inEnv} in env, {above} above, {below} below, {noRef} no-ref");
    }

    /// <summary>
    /// Buildings.TestCase enum (例: C600, C600FF) から Std140 ケース番号 (600) を取得。
    /// 末尾 "FF" は除去して数値部分のみ返す。
    /// </summary>
    private static int? GetCaseNumber(Buildings.TestCase c)
    {
      string name = c.ToString();
      if (name.StartsWith("C")) name = name.Substring(1);
      if (name.EndsWith("FF")) name = name.Substring(0, name.Length - 2);
      return int.TryParse(name, out int n) ? n : null;
    }

    #endregion

    #region OpenXml 読込ヘルパー

    private static SheetData? GetSheetData(WorkbookPart wbp, string sheetName)
    {
      var sheet = wbp.Workbook.Descendants<Sheet>().FirstOrDefault(s => s.Name?.Value == sheetName);
      if (sheet == null) return null;
      var wsp = (WorksheetPart)wbp.GetPartById(sheet.Id!.Value!);
      return wsp.Worksheet.GetFirstChild<SheetData>();
    }

    private static string GetCellText(Row row, string cellRef, SharedStringTable? sst)
    {
      var c = row.Elements<Cell>().FirstOrDefault(x => x.CellReference?.Value == cellRef);
      if (c == null) return "";
      if (c.DataType?.Value == CellValues.SharedString && sst != null)
      {
        if (int.TryParse(c.CellValue?.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int idx)
            && idx >= 0 && idx < sst.ChildElements.Count)
          return sst.ChildElements[idx].InnerText;
      }
      if (c.DataType?.Value == CellValues.InlineString)
        return c.InnerText;
      return c.CellValue?.Text ?? "";
    }

    private static double ParseDouble(string text)
    {
      if (string.IsNullOrEmpty(text)) return double.NaN;
      return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
          ? v : double.NaN;
    }

    #endregion
  }
}
