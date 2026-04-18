/* AnnualSimulationDemo.cs
 *
 * Copyright (C) 2026 E.Togashi
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or (at
 * your option) any later version.
 *
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;
using System.IO;
using System.Text;

using Popolo.Core.Building;
using Popolo.Core.Climate;

using Popolo.Webpro.Conversion;
using Popolo.Webpro.Domain;
using Popolo.Webpro.Json;

namespace Popolo.Samples.Demos.Webpro
{
  /// <summary>
  /// Annual thermal-load simulation driven by a WEBPRO (省エネ法) input JSON file.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Pipeline:
  /// </para>
  /// <list type="number">
  ///   <item><description>Read the WEBPRO JSON file via <see cref="WebproJsonReader"/>.</description></item>
  ///   <item><description>Convert to <see cref="BuildingThermalModel"/> via
  ///     <see cref="WebproToBuildingThermalModel"/>.</description></item>
  ///   <item><description>Generate a year of synthetic weather with <see cref="RandomWeather"/>.</description></item>
  ///   <item><description>Run an 8760-step annual simulation and write results to CSV.</description></item>
  /// </list>
  /// <para>
  /// <b>Usage:</b>
  /// </para>
  /// <code>
  /// dotnet run --project samples/Popolo.Samples -- webpro-annual &lt;input.json&gt; [output.csv]
  /// </code>
  /// </remarks>
  public sealed class AnnualSimulationDemo : IDemo
  {

    #region IDemo 実装

    public string Name => "webpro-annual";
    public string Category => "Webpro";
    public string Description => "Annual thermal load simulation from a WEBPRO JSON.";

    public int Run(string[] args)
    {
      if (args.Length < 1)
      {
        Console.Error.WriteLine($"Usage: {Name} <input.json> [output.csv]");
        return 1;
      }

      string inputPath = args[0];
      string outputPath = args.Length >= 2 ? args[1] : "webproConvertResult.csv";

      if (!File.Exists(inputPath))
      {
        Console.Error.WriteLine($"Input file not found: {inputPath}");
        return 2;
      }

      RunSimulation(inputPath, outputPath);
      Console.WriteLine($"Done. Results written to: {outputPath}");
      return 0;
    }

    #endregion

    #region シミュレーション本体

    private static void RunSimulation(string inputPath, string outputPath)
    {
      // 1. WEBPRO JSON を読み込む
      Console.WriteLine($"Reading WEBPRO input: {inputPath}");
      var webproModel = WebproJsonReader.ReadFromFile(inputPath);

      // 2. Popolo.Core の熱モデルに変換 (HeatGainScheduler も自動設置される)
      Console.WriteLine($"Converting: {webproModel.AirConditionedRoomNames.Count} air-conditioned rooms");
      var conversion = WebproToBuildingThermalModel.Convert(webproModel);
      var model = conversion.Model;
      if (conversion.UnmappedRoomNames.Count > 0)
      {
        Console.WriteLine(
          $"Warning: {conversion.UnmappedRoomNames.Count} room(s) have no heat-gain scheduler:");
        foreach (var name in conversion.UnmappedRoomNames)
          Console.WriteLine($"  - {name}");
      }

      // 初期条件: 20℃・絶対湿度 0 kg/kg、地中温度 20℃
      model.InitializeAirState(20, 0);
      model.SetGroundTemperature(20);

      // 3. WEBPRO の地域番号に合わせて気象データを合成
      int regionNumber = ParseRegionNumber(webproModel.Building.Region);
      Console.WriteLine($"Region: {regionNumber} ({webproModel.Building.Region})");
      (var weather, var sun) = CreateWeatherAndSun(regionNumber);

      weather.MakeWeather(1,
        out double[] dbTemp,
        out double[] hmdRatio,
        out double[] radiation,
        out _);

      var zones = model.GetZones();
      Console.WriteLine($"Simulating {dbTemp.Length} hours over {zones.Length} zones...");

      // 4. 年間シミュレーションと CSV 出力
      // WEBPRO のスケジュールに合わせて 1/1 が木曜である 2015 年を基準にする
      var startDate = new DateTime(2015, 1, 1, 0, 0, 0);
      RunAnnual(model, sun, dbTemp, hmdRatio, radiation, zones, startDate, outputPath);
    }

    private static void RunAnnual(
      BuildingThermalModel model,
      Sun sun,
      double[] dbTemp, double[] hmdRatio, double[] radiation,
      IReadOnlyZone[] zones,
      DateTime startDate,
      string outputPath)
    {
      using var writer = new StreamWriter(outputPath, append: false, Encoding.UTF8);

      // ヘッダ行
      writer.Write("日時");
      foreach (var zn in zones) writer.Write($",{zn.Name}_乾球温度[C]");
      foreach (var zn in zones) writer.Write($",{zn.Name}_絶対湿度[g/kg]");
      foreach (var zn in zones) writer.Write($",{zn.Name}_顕熱負荷[kW]");
      foreach (var zn in zones) writer.Write($",{zn.Name}_潜熱負荷[kW]");
      writer.WriteLine();

      var dTime = startDate;
      int lastReportedDay = -1;

      for (int i = 0; i < dbTemp.Length; i++)
      {
        // 進捗表示 (日が変わる時刻)
        if (dTime.DayOfYear != lastReportedDay)
        {
          Console.WriteLine(dTime.ToString("MM/dd"));
          lastReportedDay = dTime.DayOfYear;
        }

        // 外気条件の更新 (直達・散乱分離)
        sun.Update(dTime);
        sun.SeparateGlobalHorizontalRadiation(radiation[i], Sun.SeparationMethod.Erbs);
        model.UpdateOutdoorCondition(dTime, sun, dbTemp[i], 0.001 * hmdRatio[i], 100);

        // WEBPRO のスケジュールで各ゾーンの空調制御
        foreach (var readonlyZone in model.MultiRoom[0].Zones)
        {
          // ControlACSystem は具象 Zone が必要
          var zone = (Zone)readonlyZone;

          // 空調スケジューラを見つけて呼び出す
          foreach (var hg in zone.GetHeatGains())
          {
            if (hg is WebproHeatGainScheduler sch)
            {
              sch.ControlACSystem(zone);
              break;
            }
          }
        }

        // 熱収支計算
        model.ForecastHeatTransfer();
        model.ForecastWaterTransfer();
        model.FixState();

        // 結果出力
        writer.Write(dTime.ToString("MM/dd HH:mm"));
        foreach (var zn in zones) writer.Write($",{zn.Temperature:F2}");
        foreach (var zn in zones) writer.Write($",{zn.HumidityRatio * 1000.0:F2}");
        foreach (var zn in zones) writer.Write($",{zn.HeatSupply * 0.001:F3}");
        // 潜熱負荷 = 水分供給 [kg/s] × 蒸発潜熱 2500 [kJ/kg] = [kW]
        foreach (var zn in zones) writer.Write($",{zn.MoistureSupply * 2500.0:F3}");
        writer.WriteLine();

        dTime = dTime.AddHours(1);
      }
    }

    #endregion

    #region ヘルパー

    /// <summary>Parses the WEBPRO region code string into an integer 1–8.</summary>
    private static int ParseRegionNumber(string region)
    {
      if (int.TryParse(region, out var n)) return n;
      throw new FormatException(
        $"Cannot parse WEBPRO region '{region}' as an integer 1..8.");
    }

    /// <summary>
    /// Creates a <see cref="RandomWeather"/> and <see cref="Sun"/> for the
    /// given WEBPRO region number (1–8, representing the Japanese climatic
    /// regions from Hokkaido to Okinawa).
    /// </summary>
    /// <remarks>
    /// WEBPRO uses 8 regions; Popolo's <see cref="RandomWeather"/> ships with
    /// a coarser 5-city selection, so regions are grouped:
    /// <list type="bullet">
    ///   <item><description>1, 2 → Sapporo</description></item>
    ///   <item><description>3, 4 → Sendai</description></item>
    ///   <item><description>5, 6 → Tokyo</description></item>
    ///   <item><description>7 → Fukuoka</description></item>
    ///   <item><description>8 → Naha</description></item>
    /// </list>
    /// </remarks>
    private static (RandomWeather weather, Sun sun) CreateWeatherAndSun(int regionNumber)
    {
      switch (regionNumber)
      {
        case 1:
        case 2:
          return (
            new RandomWeather(100, RandomWeather.Location.Sapporo),
            new Sun(43 + 3d / 60d, 141 + 20d / 60d, 135d));
        case 3:
        case 4:
          return (
            new RandomWeather(100, RandomWeather.Location.Sendai),
            new Sun(38 + 16d / 60d, 140 + 52d / 60d, 135d));
        case 5:
        case 6:
          return (
            new RandomWeather(100, RandomWeather.Location.Tokyo),
            new Sun(Sun.City.Tokyo));
        case 7:
          return (
            new RandomWeather(100, RandomWeather.Location.Fukuoka),
            new Sun(33 + 35d / 60d, 130 + 24d / 60d, 135d));
        case 8:
          return (
            new RandomWeather(100, RandomWeather.Location.Naha),
            new Sun(26 + 12d / 60d, 127 + 40d / 60d, 135d));
        default:
          Console.Error.WriteLine(
            $"Warning: Unknown region '{regionNumber}', falling back to Tokyo.");
          return (
            new RandomWeather(100, RandomWeather.Location.Tokyo),
            new Sun(Sun.City.Tokyo));
      }
    }

    #endregion

  }
}
