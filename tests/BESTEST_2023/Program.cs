/* Program.cs - BESTEST 2023 Validation Runner (entry point)
 *
 * 実体は各セクションの Runner クラス側にあり、ここはディスパッチのみ。
 *   Section 6 (Weather Drivers) -> Section6Runner
 *   Section 7 (Building Thermal Envelope and Fabric Load) -> Section7Runner
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

      System.Console.WriteLine();
      System.Console.WriteLine("Done.");
    }
  }
}
