/* Section7Runner.OutputWriter.cs
 *
 * Section7Runner の partial 分割 — Std140_TF_Output.xlsx (Annex B8 informative
 * 結果コンパイル用テンプレート) への結果転記を担う。テンプレートは公式の
 * Std140_TF_Results.xlsx を流用しており、書き込み後は同ファイル内の Tables /
 * Fig B8-* シートで参照ツール群との相対誤差が自動的に可視化される。
 *
 * 本ファイルが取り扱うシート: 'YourData' (テンプレ唯一の入力欄)。
 */

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace BESTEST_2023
{
  internal static partial class Section7Runner
  {
    #region xlsx テンプレ書込み

    /// <summary>
    /// Std140_TF_Output.xlsx の 'YourData' シートの所定セルに各ケースの集計値を書き込む。
    /// 既存のレイアウト・他セルは保持。書き込み後は同ファイル内の Tables / Fig B8-* シートで
    /// 参照値との相対誤差が自動的に評価される。
    /// </summary>
    private static void FillStd140Template(
        string templatePath, string outPath, List<CaseResult> results)
    {
      File.Copy(templatePath, outPath, overwrite: true);
      using var doc = SpreadsheetDocument.Open(outPath, isEditable: true);
      var wbp = doc.WorkbookPart!;

      var sheetA = wbp.Workbook.Descendants<Sheet>().FirstOrDefault(s => s.Name?.Value == "YourData");
      if (sheetA == null)
      {
        System.Console.WriteLine("  WARN: sheet 'YourData' not found in template");
        return;
      }
      var wsp = (WorksheetPart)wbp.GetPartById(sheetA.Id!.Value!);
      var sd = wsp.Worksheet.GetFirstChild<SheetData>()!;

      int written = 0;
      foreach (var r in results)
      {
        WriteCaseResults(sd, r);
        written++;
      }

      var calc = wbp.Workbook.CalculationProperties;
      if (calc == null) { calc = new CalculationProperties(); wbp.Workbook.AppendChild(calc); }
      calc.CalculationMode = new EnumValue<CalculateModeValues>(CalculateModeValues.Auto);
      calc.FullCalculationOnLoad = true;

      // 数式セルの一部を値で上書きしているため、テンプレ由来の calcChain.xml は
      // 古い参照を含み Excel 起動時に「内容に問題が見つかりました」警告を出す。
      // 削除しておけば Excel が次回保存時に再生成する (任意 part)。
      if (wbp.CalculationChainPart != null)
        wbp.DeletePart(wbp.CalculationChainPart);

      wsp.Worksheet.Save();
      wbp.Workbook.Save();
      System.Console.WriteLine($"  {written} case(s) written to '{outPath}'");
    }

    /// <summary>1 ケース分の結果セルを書き込む。</summary>
    private static void WriteCaseResults(SheetData sd, CaseResult r)
    {
      // §7.3.1: 年間負荷 (非 FF ケース行)
      if (!r.IsFreeFloat && AnnualLoadRow.TryGetValue(r.Case, out uint row))
      {
        SetNumericCell(sd, $"C{row}", r.AnnualHeating_MWh, "F4");
        SetNumericCell(sd, $"D{row}", r.AnnualCooling_MWh, "F4");
        SetNumericCell(sd, $"E{row}", r.PeakHeating_kW, "F3");
        if (r.PeakHeating_kW > 0)
        {
          var ph = DecomposeHourEnd(r.PeakHeatingTime);
          SetStringCell (sd, $"F{row}", ph.month);
          SetNumericCell(sd, $"G{row}", ph.day,  "0");
          SetNumericCell(sd, $"H{row}", ph.hour, "0");
        }
        SetNumericCell(sd, $"I{row}", r.PeakCooling_kW, "F3");
        if (r.PeakCooling_kW > 0)
        {
          var pc = DecomposeHourEnd(r.PeakCoolingTime);
          SetStringCell (sd, $"J{row}", pc.month);
          SetNumericCell(sd, $"K{row}", pc.day,  "0");
          SetNumericCell(sd, $"L{row}", pc.hour, "0");
        }
      }

      // §7.3.6: 自由温度ゾーン (FF ケース行)
      if (r.IsFreeFloat && FreeFloatRow.TryGetValue(r.Case, out uint ffRow))
      {
        SetNumericCell(sd, $"C{ffRow}", r.ZoneTempMean_C, "F2");
        SetNumericCell(sd, $"D{ffRow}", r.ZoneTempMin_C,  "F2");
        var nm = DecomposeHourEnd(r.ZoneTempMinTime);
        SetStringCell (sd, $"E{ffRow}", nm.month);
        SetNumericCell(sd, $"F{ffRow}", nm.day,  "0");
        SetNumericCell(sd, $"G{ffRow}", nm.hour, "0");
        SetNumericCell(sd, $"H{ffRow}", r.ZoneTempMax_C, "F2");
        var xm = DecomposeHourEnd(r.ZoneTempMaxTime);
        SetStringCell (sd, $"I{ffRow}", xm.month);
        SetNumericCell(sd, $"J{ffRow}", xm.day,  "0");
        SetNumericCell(sd, $"K{ffRow}", xm.hour, "0");
      }

      // §7.3.2.1: 年間入射日射 (Case 600)
      if (r.Case == Buildings.TestCase.C600)
      {
        SetNumericCell(sd, "C155", r.IncidentH_kWhm2, "F2");
        SetNumericCell(sd, "C156", r.IncidentN_kWhm2, "F2");
        SetNumericCell(sd, "C157", r.IncidentE_kWhm2, "F2");
        SetNumericCell(sd, "C158", r.IncidentS_kWhm2, "F2");
        SetNumericCell(sd, "C159", r.IncidentW_kWhm2, "F2");
      }

      // §7.3.2.2: 年間透過日射 (テンプレ Std140_TF_Output.xlsx の対応行)
      //   r163: 600/South, r164: 660/South, r165: 670/South, r166: 620/West
      //   r170: 610/South (shaded), r171: 630/West (shaded)
      if (r.Case == Buildings.TestCase.C600)
        SetNumericCell(sd, "C163", r.TransmittedSouth_kWhm2, "F2");
      if (r.Case == Buildings.TestCase.C660)
        SetNumericCell(sd, "C164", r.TransmittedSouth_kWhm2, "F2");
      if (r.Case == Buildings.TestCase.C670)
        SetNumericCell(sd, "C165", r.TransmittedSouth_kWhm2, "F2");
      if (r.Case == Buildings.TestCase.C620)
        SetNumericCell(sd, "C166", r.TransmittedSouth_kWhm2, "F2");
      if (r.Case == Buildings.TestCase.C610)
        SetNumericCell(sd, "C170", r.TransmittedSouth_kWhm2, "F2");
      if (r.Case == Buildings.TestCase.C630)
        SetNumericCell(sd, "C171", r.TransmittedSouth_kWhm2, "F2");

      // §7.3.2.3: 天空温度 (Case 600)
      if (r.Case == Buildings.TestCase.C600)
      {
        SetNumericCell(sd, "C178", r.SkyTempMean_C, "F2");
        SetNumericCell(sd, "D178", r.SkyTempMin_C,  "F2");
        var sn = DecomposeHourEnd(r.SkyTempMinTime);
        SetStringCell (sd, "E178", sn.month);
        SetNumericCell(sd, "F178", sn.day,  "0");
        SetNumericCell(sd, "G178", sn.hour, "0");
        SetNumericCell(sd, "H178", r.SkyTempMax_C, "F2");
        var sx = DecomposeHourEnd(r.SkyTempMaxTime);
        SetStringCell (sd, "I178", sx.month);
        SetNumericCell(sd, "J178", sx.day,  "0");
        SetNumericCell(sd, "K178", sx.hour, "0");
      }

      // §7.3.2.4: 月別負荷 (Case 600 / 900)
      if (r.Case == Buildings.TestCase.C600 || r.Case == Buildings.TestCase.C900)
      {
        bool is600 = r.Case == Buildings.TestCase.C600;
        for (int m = 0; m < 12; m++)
        {
          uint mrow = (uint)(190 + m);
          string cTot  = is600 ? "C" : "K";
          string cCool = is600 ? "D" : "L";
          string cPkH  = is600 ? "E" : "M";
          string cPkHd = is600 ? "F" : "N";
          string cPkHh = is600 ? "G" : "O";
          string cPkC  = is600 ? "H" : "P";
          string cPkCd = is600 ? "I" : "Q";
          string cPkCh = is600 ? "J" : "R";

          SetNumericCell(sd, $"{cTot}{mrow}",  r.MonthlyHeating_kWh[m], "F2");
          SetNumericCell(sd, $"{cCool}{mrow}", r.MonthlyCooling_kWh[m], "F2");
          SetNumericCell(sd, $"{cPkH}{mrow}",  r.MonthlyPeakHeating_kW[m], "F3");
          if (r.MonthlyPeakHeating_kW[m] > 0)
          {
            var ph = DecomposeHourEnd(r.MonthlyPeakHeatingTime[m]);
            SetNumericCell(sd, $"{cPkHd}{mrow}", ph.day,  "0");
            SetNumericCell(sd, $"{cPkHh}{mrow}", ph.hour, "0");
          }
          SetNumericCell(sd, $"{cPkC}{mrow}",  r.MonthlyPeakCooling_kW[m], "F3");
          if (r.MonthlyPeakCooling_kW[m] > 0)
          {
            var pc = DecomposeHourEnd(r.MonthlyPeakCoolingTime[m]);
            SetNumericCell(sd, $"{cPkCd}{mrow}", pc.day,  "0");
            SetNumericCell(sd, $"{cPkCh}{mrow}", pc.hour, "0");
          }
        }
      }

      // §7.3.8: 特定日毎時値 (rows 230-253, 262-285, 294-317)
      WriteSpecificDayHourly(sd, r);

      // §7.3.7: Case 900FF 温度ビンヒストグラム (rows 330-477)
      if (r.Case == Buildings.TestCase.C900FF)
      {
        for (int b = 0; b < 148; b++)
          SetNumericCell(sd, $"C{330 + b}", r.TempBinHours[b], "0");
      }
    }

    /// <summary>
    /// §7.3.8 特定日毎時値の書込み。テンプレ ('YourData') の構成:
    ///   rows 230-253: Solar/Sky/Trans (cases 600/660/670)
    ///   rows 262-285: Zone Loads + Zone Air Temps (cases 600/640/660-695/900/940/980-995)
    ///   rows 294-317: FF Zone Temperatures (cases 600FF/650FF/680FF/900FF/950FF/980FF)
    /// </summary>
    private static void WriteSpecificDayHourly(SheetData sd, CaseResult r)
    {
      // Solar/Sky/Trans 行 (Case 600/660/670 のみ)
      if (r.Case == Buildings.TestCase.C600)
      {
        for (int h = 0; h < 24; h++)
        {
          uint row = (uint)(230 + h);
          SetNumericCell(sd, $"C{row}", r.May4IncH_Wm2[h],   "F2");
          SetNumericCell(sd, $"D{row}", r.May4IncS_Wm2[h],   "F2");
          SetNumericCell(sd, $"E{row}", r.May4IncW_Wm2[h],   "F2");
          SetNumericCell(sd, $"F{row}", r.Jul14IncH_Wm2[h],  "F2");
          SetNumericCell(sd, $"G{row}", r.Jul14IncS_Wm2[h],  "F2");
          SetNumericCell(sd, $"H{row}", r.Jul14IncW_Wm2[h],  "F2");
          SetNumericCell(sd, $"I{row}", r.Feb1SkyTemp_C[h],  "F2");
          SetNumericCell(sd, $"J{row}", r.May4SkyTemp_C[h],  "F2");
          SetNumericCell(sd, $"K{row}", r.Jul14SkyTemp_C[h], "F2");
          SetNumericCell(sd, $"L{row}", r.Feb1TransSouth_Wm2[h],  "F2");
          SetNumericCell(sd, $"O{row}", r.May4TransSouth_Wm2[h],  "F2");
          SetNumericCell(sd, $"R{row}", r.Jul14TransSouth_Wm2[h], "F2");
        }
      }
      else if (r.Case == Buildings.TestCase.C660)
      {
        for (int h = 0; h < 24; h++)
        {
          uint row = (uint)(230 + h);
          SetNumericCell(sd, $"M{row}", r.Feb1TransSouth_Wm2[h],  "F2");
          SetNumericCell(sd, $"P{row}", r.May4TransSouth_Wm2[h],  "F2");
          SetNumericCell(sd, $"S{row}", r.Jul14TransSouth_Wm2[h], "F2");
        }
      }
      else if (r.Case == Buildings.TestCase.C670)
      {
        for (int h = 0; h < 24; h++)
        {
          uint row = (uint)(230 + h);
          SetNumericCell(sd, $"N{row}", r.Feb1TransSouth_Wm2[h],  "F2");
          SetNumericCell(sd, $"Q{row}", r.May4TransSouth_Wm2[h],  "F2");
          SetNumericCell(sd, $"T{row}", r.Jul14TransSouth_Wm2[h], "F2");
        }
      }

      // Zone Loads (rows 262-285): 1時間=power×1h なので W → kWh は ×1e-3。
      // 仕様: 暖房=正、冷房=負 (1 セルに heatSupply 直接、negative がそのまま冷房)
      if (Feb1LoadCol.TryGetValue(r.Case, out string? col1))
      {
        for (int h = 0; h < 24; h++)
          SetNumericCell(sd, $"{col1}{262 + h}", r.Feb1HeatSupply_W[h] * 1e-3, "F4");
      }
      if (Jul14LoadCol.TryGetValue(r.Case, out string? col2))
      {
        for (int h = 0; h < 24; h++)
          SetNumericCell(sd, $"{col2}{262 + h}", r.Jul14HeatSupply_W[h] * 1e-3, "F4");
      }

      // Feb 1 Zone Air Temp (controlled cases 640/940, rows 262-285 col Y/Z)
      if (Feb1ZoneTempCol.TryGetValue(r.Case, out string? col3))
      {
        for (int h = 0; h < 24; h++)
          SetNumericCell(sd, $"{col3}{262 + h}", r.Feb1ZoneTemp_C[h], "F2");
      }

      // FF Zone Temperatures (rows 294-317)
      if (FFTempDayCol.TryGetValue(r.Case, out var ffCol))
      {
        double[] src = ffCol.isJul14 ? r.Jul14ZoneTemp_C : r.Feb1ZoneTemp_C;
        for (int h = 0; h < 24; h++)
          SetNumericCell(sd, $"{ffCol.col}{294 + h}", src[h], "F2");
      }
    }

    #endregion

    #region OpenXml セル書込みヘルパー

    private static (string col, uint row) SplitCellRef(string cellRef)
    {
      int i = 0;
      while (i < cellRef.Length && char.IsLetter(cellRef[i])) i++;
      return (cellRef.Substring(0, i), uint.Parse(cellRef.Substring(i)));
    }

    private static int ColIndex(string col)
    {
      int n = 0;
      foreach (char c in col) n = n * 26 + (c - 'A' + 1);
      return n - 1;
    }

    private static Cell GetOrCreateCell(SheetData sd, string cellRef)
    {
      var (colLetter, rowIdx) = SplitCellRef(cellRef);

      Row? row = sd.Elements<Row>().FirstOrDefault(r => r.RowIndex?.Value == rowIdx);
      if (row == null)
      {
        row = new Row { RowIndex = rowIdx };
        var insertBefore = sd.Elements<Row>().FirstOrDefault(r => r.RowIndex != null && r.RowIndex.Value > rowIdx);
        if (insertBefore != null) sd.InsertBefore(row, insertBefore);
        else sd.AppendChild(row);
      }

      Cell? cell = row.Elements<Cell>().FirstOrDefault(c => c.CellReference?.Value == cellRef);
      if (cell == null)
      {
        cell = new Cell { CellReference = cellRef };
        int targetCol = ColIndex(colLetter);
        var insertBefore = row.Elements<Cell>().FirstOrDefault(c =>
        {
          if (c.CellReference?.Value == null) return false;
          var (cc, _) = SplitCellRef(c.CellReference.Value);
          return ColIndex(cc) > targetCol;
        });
        if (insertBefore != null) row.InsertBefore(cell, insertBefore);
        else row.AppendChild(cell);
      }
      return cell;
    }

    private static void SetNumericCell(SheetData sd, string cellRef, double value, string fmt)
    {
      if (double.IsNaN(value)) return;
      var cell = GetOrCreateCell(sd, cellRef);
      cell.RemoveAllChildren();
      cell.DataType = null;
      cell.CellValue = new CellValue(value.ToString(fmt, CultureInfo.InvariantCulture));
    }

    private static void SetNumericCell(SheetData sd, string cellRef, int value, string fmt)
        => SetNumericCell(sd, cellRef, (double)value, fmt);

    private static void SetStringCell(SheetData sd, string cellRef, string value)
    {
      var cell = GetOrCreateCell(sd, cellRef);
      cell.RemoveAllChildren();
      cell.CellValue = null;
      cell.DataType = CellValues.InlineString;
      cell.AppendChild(new InlineString(new Text(value)));
    }

    #endregion
  }
}
