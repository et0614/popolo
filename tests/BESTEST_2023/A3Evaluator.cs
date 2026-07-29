/* A3Evaluator.cs - ANSI/ASHRAE Standard 140-2023 Annex A3 (Normative) 合否判定
 *
 * Std 140-2023 規範 Annex A3 「Software Acceptance Criteria」に基づき、
 * Popolo の Section 7 (Thermal Fabric) BESTEST 実行結果が公式の認証基準を
 * 満たすかを判定する。
 *
 * 範囲ケース (range case) は 2 種類:
 *   - 絶対値: Case 600 の AH が [3.75, 4.98] MWh/yr など
 *   - 差分 (delta): Case 610 − Case 600 の AH が [-0.14, 0.29] MWh/yr など
 *
 * bound 計算根拠 (Annex B12.1):
 *   acceptance bounds = wider of:
 *     (a) Statistical Bounds: median ± 2.024 × MAD (≈ 3σ 信頼区間)
 *     (b) Nonstatistical Bounds: median ± 0.05 × median_BaseCase
 *
 * 比較公差 (B12.2):
 *   - relative tolerance: 1%
 *   - absolute tolerance: 1e-4
 *   いずれかの公差内なら upper/lower bound に「等価」とみなす
 *
 * 合格基準 (B12.4):
 *   - Low Mass: 21 範囲ケース中 18 以上が pass で合格
 *   - High Mass: 19 範囲ケース中 17 以上が pass で合格
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace BESTEST_2023
{
  /// <summary>Std 140-2023 Annex A3 (Normative) Software Acceptance Criteria 評価。</summary>
  internal static class A3Evaluator
  {
    #region Tolerances (B12.2)

    private const double RelativeTolerance = 0.01;   // 1%
    private const double AbsoluteTolerance = 1e-4;

    /// <summary>値が [lower, upper] の範囲内か (公差適用)。</summary>
    private static bool IsWithin(double value, double lower, double upper)
    {
      // 下限チェック: value >= lower - tolerance
      double lowTol = Math.Max(Math.Abs(lower) * RelativeTolerance, AbsoluteTolerance);
      if (value < lower - lowTol) return false;
      // 上限チェック: value <= upper + tolerance
      double highTol = Math.Max(Math.Abs(upper) * RelativeTolerance, AbsoluteTolerance);
      if (value > upper + highTol) return false;
      return true;
    }

    #endregion

    #region A3 range tables (Std 140-2023 Tables A3-1 to A3-4)

    /// <summary>範囲ケース定義: (ラベル, 値計算関数, 下限, 上限)。</summary>
    private sealed record RangeCase(
        string Label,
        Func<Dictionary<string, (double AH, double AC)>, double> Value,
        double Lower,
        double Upper);

    /// <summary>Table A3-1 Low Mass Annual Heating (MWh/yr)。</summary>
    private static readonly RangeCase[] TableA3_1 = new[]
    {
      new RangeCase("Case 600",              v => v["C600"].AH,                  3.75,  4.98),
      new RangeCase("Case 610 − Case 600",   v => v["C610"].AH - v["C600"].AH,  -0.14,  0.29),
      new RangeCase("Case 620 − Case 600",   v => v["C620"].AH - v["C600"].AH,  -0.08,  0.40),
      new RangeCase("Case 630 − Case 620",   v => v["C630"].AH - v["C620"].AH,   0.02,  0.74),
      new RangeCase("Case 640 − Case 600",   v => v["C640"].AH - v["C600"].AH,  -2.17, -1.22),
      new RangeCase("Case 660 − Case 600",   v => v["C660"].AH - v["C600"].AH,  -1.07, -0.16),
      new RangeCase("Case 670 − Case 600",   v => v["C670"].AH - v["C600"].AH,   0.25,  2.98),
      new RangeCase("Case 680 − Case 600",   v => v["C680"].AH - v["C600"].AH,  -2.54, -1.90),
      new RangeCase("Case 685 − Case 600",   v => v["C685"].AH - v["C600"].AH,   0.33,  0.77),
      new RangeCase("Case 695 − Case 685",   v => v["C695"].AH - v["C685"].AH,  -2.38, -1.94),
    };

    /// <summary>Table A3-2 Low Mass Annual Sensible Cooling (MWh/yr)。</summary>
    private static readonly RangeCase[] TableA3_2 = new[]
    {
      new RangeCase("Case 600",              v => v["C600"].AC,                  5.00,  6.83),
      new RangeCase("Case 610 − Case 600",   v => v["C610"].AC - v["C600"].AC,  -2.26, -0.80),
      new RangeCase("Case 620 − Case 600",   v => v["C620"].AC - v["C600"].AC,  -2.24, -1.64),
      new RangeCase("Case 630 − Case 620",   v => v["C630"].AC - v["C620"].AC,  -1.68, -0.77),
      new RangeCase("Case 640 − Case 600",   v => v["C640"].AC - v["C600"].AC,  -0.56,  0.03),
      new RangeCase("Case 650 − Case 600",   v => v["C650"].AC - v["C600"].AC,  -1.54, -0.95),
      new RangeCase("Case 660 − Case 600",   v => v["C660"].AC - v["C600"].AC,  -3.09, -2.50),
      new RangeCase("Case 670 − Case 600",   v => v["C670"].AC - v["C600"].AC,   0.05,  0.84),
      new RangeCase("Case 680 − Case 600",   v => v["C680"].AC - v["C600"].AC,   0.13,  0.87),
      new RangeCase("Case 685 − Case 600",   v => v["C685"].AC - v["C600"].AC,   2.70,  3.31),
      new RangeCase("Case 695 − Case 685",   v => v["C695"].AC - v["C685"].AC,  -0.21,  0.44),
    };

    /// <summary>Table A3-3 High Mass Annual Heating (MWh/yr)。</summary>
    private static readonly RangeCase[] TableA3_3 = new[]
    {
      new RangeCase("Case 900",              v => v["C900"].AH,                  1.04,  2.28),
      new RangeCase("Case 900 − Case 910",   v => v["C900"].AH - v["C910"].AH,  -0.52, -0.02),
      new RangeCase("Case 920 − Case 900",   v => v["C920"].AH - v["C900"].AH,   1.51,  1.92),
      new RangeCase("Case 930 − Case 920",   v => v["C930"].AH - v["C920"].AH,   0.20,  1.15),
      new RangeCase("Case 940 − Case 900",   v => v["C940"].AH - v["C900"].AH,  -0.82, -0.37),
      new RangeCase("Case 960 − Case 900",   v => v["C960"].AH - v["C900"].AH,   0.96,  1.12),
      new RangeCase("Case 980 − Case 900",   v => v["C980"].AH - v["C900"].AH,  -1.65, -1.00),
      new RangeCase("Case 985 − Case 900",   v => v["C985"].AH - v["C900"].AH,   0.64,  0.81),
      new RangeCase("Case 995 − Case 985",   v => v["C995"].AH - v["C985"].AH,  -1.83, -1.07),
    };

    /// <summary>Table A3-4 High Mass Annual Sensible Cooling (MWh/yr)。</summary>
    private static readonly RangeCase[] TableA3_4 = new[]
    {
      new RangeCase("Case 900",              v => v["C900"].AC,                  2.35,  2.60),
      new RangeCase("Case 900 − Case 910",   v => v["C900"].AC - v["C910"].AC,   0.35,  1.74),
      new RangeCase("Case 920 − Case 900",   v => v["C920"].AC - v["C900"].AC,   0.08,  0.48),
      new RangeCase("Case 930 − Case 920",   v => v["C930"].AC - v["C920"].AC,  -1.19, -0.44),
      new RangeCase("Case 940 − Case 900",   v => v["C940"].AC - v["C900"].AC,  -0.19,  0.06),
      new RangeCase("Case 950 − Case 900",   v => v["C950"].AC - v["C900"].AC,  -2.00, -1.56),
      new RangeCase("Case 960 − Case 900",   v => v["C960"].AC - v["C900"].AC,  -1.81, -1.27),
      new RangeCase("Case 980 − Case 900",   v => v["C980"].AC - v["C900"].AC,   1.09,  1.41),
      new RangeCase("Case 985 − Case 900",   v => v["C985"].AC - v["C900"].AC,   3.52,  4.18),
      new RangeCase("Case 995 − Case 985",   v => v["C995"].AC - v["C985"].AC,   0.63,  1.15),
    };

    #endregion

    #region Evaluation body

    /// <summary>1 つの test group (Tables) を評価し、レポート文字列に書き込む。</summary>
    /// <returns>(合格数, 全数)</returns>
    private static (int passed, int total) EvaluateTable(
        string title,
        RangeCase[] table,
        Dictionary<string, (double AH, double AC)> values,
        StringBuilder report)
    {
      report.AppendLine();
      report.AppendLine($"--- {title} ---");
      report.AppendLine(string.Format(CultureInfo.InvariantCulture,
          "  {0,-25} {1,9}  {2,9}  {3,9}   {4}",
          "Range Case", "Lower", "Upper", "Popolo", "Result"));
      int passed = 0;
      foreach (var rc in table)
      {
        double val;
        try { val = rc.Value(values); }
        catch (KeyNotFoundException)
        {
          report.AppendLine(string.Format(CultureInfo.InvariantCulture,
              "  {0,-25} {1,9:F4}  {2,9:F4}  {3,>9}   N/A (missing case)",
              rc.Label, rc.Lower, rc.Upper, "--"));
          continue;
        }
        bool pass = IsWithin(val, rc.Lower, rc.Upper);
        if (pass) passed++;
        string mark = pass ? "PASS" : "NG  ";
        string gap = "";
        if (!pass)
        {
          if (val < rc.Lower) gap = $" (below lower by {(rc.Lower - val):+0.0000;-0.0000;0.0000})";
          else                gap = $" (above upper by {(val - rc.Upper):+0.0000;-0.0000;0.0000})";
        }
        report.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "  {0,-25} {1,9:F4}  {2,9:F4}  {3,9:F4}   {4}{5}",
            rc.Label, rc.Lower, rc.Upper, val, mark, gap));
      }
      return (passed, table.Length);
    }

    /// <summary>
    /// Std 140-2023 Annex A3 評価を実行し、Console とテキストファイルに書き出す。
    /// </summary>
    /// <param name="caseValues">ケース名 (例 "C600") → (AH MWh, AC MWh) の辞書。</param>
    /// <param name="outputPath">レポート出力先テキストパス (null/空ならファイル出力なし)。</param>
    public static void EvaluateAndReport(
        Dictionary<string, (double AH, double AC)> caseValues,
        string outputPath)
    {
      var report = new StringBuilder();
      report.AppendLine("========================================================================");
      report.AppendLine(" ANSI/ASHRAE Standard 140-2023 Annex A3 (Normative)");
      report.AppendLine(" Software Acceptance Criteria — Thermal Fabric (Section 7)");
      report.AppendLine($" Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
      report.AppendLine("========================================================================");
      report.AppendLine();
      report.AppendLine(" Comparison tolerance (B12.2): rel 1% OR abs 1e-4 (whichever larger)");
      report.AppendLine(" Pass threshold (B12.4): 90% of range cases per test group");

      var (p1, t1) = EvaluateTable("Table A3-1: Low Mass Annual Heating Load [MWh/yr]",   TableA3_1, caseValues, report);
      var (p2, t2) = EvaluateTable("Table A3-2: Low Mass Annual Cooling Load [MWh/yr]",   TableA3_2, caseValues, report);
      var (p3, t3) = EvaluateTable("Table A3-3: High Mass Annual Heating Load [MWh/yr]",  TableA3_3, caseValues, report);
      var (p4, t4) = EvaluateTable("Table A3-4: High Mass Annual Cooling Load [MWh/yr]",  TableA3_4, caseValues, report);

      int lowPassed = p1 + p2;
      int lowTotal = t1 + t2;
      int highPassed = p3 + p4;
      int highTotal = t3 + t4;
      int lowRequired = (int)Math.Floor(lowTotal * 0.9);     // 18
      int highRequired = (int)Math.Floor(highTotal * 0.9);   // 17
      bool lowPass = lowPassed >= lowRequired;
      bool highPass = highPassed >= highRequired;

      report.AppendLine();
      report.AppendLine("========================================================================");
      report.AppendLine(" Test Group Summary");
      report.AppendLine("========================================================================");
      report.AppendLine(string.Format(CultureInfo.InvariantCulture,
          "  {0,-30} {1,8} {2,12} {3,8}",
          "Test Group", "Result", "Required", "Status"));
      report.AppendLine(string.Format(CultureInfo.InvariantCulture,
          "  {0,-30} {1,4}/{2,-3} {3,12} {4,8}",
          "Thermal Fabric Low Mass",  lowPassed,  lowTotal,  $"≥ {lowRequired}/{lowTotal}",  lowPass  ? "PASS" : "FAIL"));
      report.AppendLine(string.Format(CultureInfo.InvariantCulture,
          "  {0,-30} {1,4}/{2,-3} {3,12} {4,8}",
          "Thermal Fabric High Mass", highPassed, highTotal, $"≥ {highRequired}/{highTotal}", highPass ? "PASS" : "FAIL"));
      report.AppendLine();
      report.AppendLine($"  OVERALL: {((lowPass && highPass) ? "PASS — Software meets ANSI/ASHRAE Standard 140-2023 Annex A3 acceptance criteria" : "FAIL — One or more test groups did not meet the minimum pass count")}");
      report.AppendLine("========================================================================");

      string text = report.ToString();
      Console.Write(text);
      if (!string.IsNullOrEmpty(outputPath))
      {
        try
        {
          File.WriteAllText(outputPath, text, new UTF8Encoding(false));
          Console.WriteLine($" A3 evaluation written to: {outputPath}");
        }
        catch (IOException ex)
        {
          Console.WriteLine($" [warn] A3 evaluation file write failed: {ex.Message}");
        }
      }
    }

    #endregion
  }
}
