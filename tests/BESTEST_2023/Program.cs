/* Program.cs - BESTEST 2023 Validation Runner (entry point)
 *
 * 実体は各セクションの Runner クラス側にあり、ここはディスパッチのみ。
 *   Section 6 (Weather Drivers) -> Section6Runner
 *   Section 7 (Building Thermal Envelope and Fabric Load) -> Section7Runner (未実装)
 */

using System.Text;

namespace BESTEST_2023
{
  internal class Program
  {
    static void Main(string[] args)
    {
      Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

      const string weatherDir = "weather_data";
      const string resultsDir = "results";

      try
      {
        Section6Runner.Run(weatherDir, resultsDir);
      }
      catch (System.IO.IOException ex)
      {
        // 出力 xlsx が Excel 等で開かれていてロックされている場合は警告のみで続行
        System.Console.WriteLine($"  [warn] Section 6 output skipped: {ex.Message}");
      }
      System.Console.WriteLine();
      try
      {
        Section7Runner.Run(weatherDir, resultsDir);
      }
      catch (System.IO.IOException ex)
      {
        System.Console.WriteLine($"  [warn] Section 7 output skipped: {ex.Message}");
      }

      /*// 診断モード: 旧 BESTEST の DRYCOLD.TMY (TMY1) を新ランナーで実行し、
      // 旧版 Popolo 結果と直接比較する。移植による論理差を切り分けるため。
      System.Console.WriteLine();
      string legacyTmy = System.IO.Path.Combine(weatherDir, "DRYCOLD.TMY");
      string legacyXlsx = System.IO.Path.Combine(resultsDir, "classic_bestest_result.xlsx");
      Section7Runner.RunWithLegacyWeather(legacyTmy, resultsDir,
          System.IO.File.Exists(legacyXlsx) ? legacyXlsx : null);*/

      System.Console.WriteLine();
      System.Console.WriteLine("Done.");
    }
  }
}
