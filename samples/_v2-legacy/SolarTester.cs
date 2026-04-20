using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;

using Popolo.Weather;
using Popolo.HVAC.SolarEnergy;
using Popolo.Electric;

namespace PopoloTester
{
  class SolarTester
  {

    #region 集熱器テスト

    private static void SimpleSolarCollectorTest()
    {
      //集熱器インスタンスの作成
      Incline inc = new Incline(Incline.Orientation.S, 30d / 180d * Math.PI);
      SimpleSolarCollector[] scs = new SimpleSolarCollector[2];
      scs[0] = new SimpleSolarCollector(inc, 1, SimpleSolarCollector.HeatReceiver.FlatPlate);
      scs[1] = new SimpleSolarCollector(inc, 1, SimpleSolarCollector.HeatReceiver.VacuumTube);

      //気象データの作成
      double[] dbt, hrt, rad;
      bool[] fair;
      RandomWeather wRnd = new RandomWeather(1, RandomWeather.Location.Tokyo);
      wRnd.MakeWeather(20, out dbt, out hrt, out rad, out fair);
      Sun sun = new Sun(35, 41.2, 0, 139, 45.9, 0, 135, 0, 0);

      //集熱温度
      double[] cTemp = new double[] { 25, 55, 90 };

      //計算,書き出し処理
      using (StreamWriter sWriter = new StreamWriter
        ("SimpleSolarCollector.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        //タイトル行
        sWriter.Write("月, 傾斜面日射量[MJ/月]");
        for (int i = 0; i < 3; i++) sWriter.Write(",平板型" + cTemp[i] + "C");
        for (int i = 0; i < 3; i++) sWriter.Write(",真空ガラス管型" + cTemp[i] + "C");
        sWriter.WriteLine();

        DateTime dt = new DateTime(1999, 1, 1, 0, 0, 0);
        double[,] sum = new double[12, scs.Length * cTemp.Length + 1];
        for (int i = 0; i < 20; i++)
        {
          for (int j = 0; j < 8760; j++)
          {
            //太陽位置を更新して直散分離
            sun.Update(dt);
            sun.SeparateGlobalHorizontalRadiation(rad[8760 * i + j], Sun.SeparationMethod.Erbs);

            //傾斜面日射量を積算
            sum[dt.Month - 1, 0] += (inc.GetDirectSolarRadiationRate(sun) * sun.DirectNormalRadiation
              + inc.ConfigurationFactorToSky * sun.DiffuseHorizontalRadiation) / 1000d;

            //集熱量[Wh]を計算
            for (int k = 0; k < scs.Length; k++)
            {
              for (int m = 0; m < cTemp.Length; m++)
              {
                if (0 != scs[k].GetWaterFlowRate(sun, cTemp[m], 3, dbt[8760 * i + j]))
                  sum[dt.Month - 1, k * cTemp.Length + m + 1] += scs[k].HeatCollection;
              }
            }
            dt = dt.AddHours(1);
          }
          dt = dt.AddYears(-1);
        }
        //書き出し処理
        for (int i = 0; i < 12; i++)
        {
          sWriter.Write(i + 1);
          sWriter.Write("," + sum[i, 0] / 20d * 3.6); // kWh/20年→MJ/月に変換
          for (int j = 1; j < sum.GetLength(1); j++) sWriter.Write("," + sum[i, j] / sum[i, 0]);
          sWriter.WriteLine();
        }
      }
    }

    private static void FlatPlateSolarCollectorTest()
    {
      double isc = 500;

      Console.WriteLine("平均水温[C], 平均水温/日射量[Cm2K/W], 集熱効率[-]");
      Console.WriteLine("*****基準ケース*****");
      for (int twi = 0; twi < 80; twi++)
      {
        double pt, gt, wt, mt, eta;
        double ht = FlatPlateSolarCollector.GetHeatTransfer
          (0, 0, isc, 0, 0.01, twi, 0.01, 0.1, 0.1, 0, 0.02, 0.05, 2.0, 0.02, 0.013, 0.015, 0.001, 20,
          1.0, 0.90, 0.08, 0.93, out pt, out gt, out wt, out mt, out eta);
        Console.WriteLine((mt / isc).ToString("F3") + ", " + eta.ToString("F2"));
      }
      Console.WriteLine("*****外部風速10m/s*****");
      for (int twi = 0; twi < 80; twi++)
      {
        double pt, gt, wt, mt, eta;
        double ht = FlatPlateSolarCollector.GetHeatTransfer
          (0, 0, isc, 0, 0.01, twi, 0.01, 0.1, 0.1, 10, 0.02, 0.05, 2.0, 0.02, 0.013, 0.015, 0.001, 20,
          1.0, 0.90, 0.08, 0.93, out pt, out gt, out wt, out mt, out eta);
        Console.WriteLine((mt / isc).ToString("F3") + ", " + eta.ToString("F2"));
      }
      Console.WriteLine("*****水量0.005m/s*****");
      for (int twi = 0; twi < 80; twi++)
      {
        double pt, gt, wt, mt, eta;
        double ht = FlatPlateSolarCollector.GetHeatTransfer
          (0, 0, isc, 0, 0.005, twi, 0.01, 0.1, 0.1, 0, 0.02, 0.05, 2.0, 0.02, 0.013, 0.015, 0.001, 20,
          1.0, 0.90, 0.08, 0.93, out pt, out gt, out wt, out mt, out eta);
        Console.WriteLine((mt / isc).ToString("F3") + ", " + eta.ToString("F2"));
      }
    }

    #endregion

    #region 発電パネルテスト

    private static void PhotovoltaicPanelTest()
    {
      //太陽光発電パネルを作成//定格出力1W,架台設置,アモルファス,設置角10度刻み
      PhotovoltaicPanel[] pvs = new PhotovoltaicPanel[10];
      for (int i = 0; i < pvs.Length; i++)
      {
        pvs[i] = new PhotovoltaicPanel
          (1, PhotovoltaicPanel.MountType.MountMode, PhotovoltaicPanel.MaterialType.Amorphous, i * 10);
      }

      //年間日射データを計算（東京・那覇・札幌）
      double[] dbtT, dbtN, dbtS, hrt, radS, radT, radN;
      bool[] fair;
      RandomWeather wRnd = new RandomWeather(1, RandomWeather.Location.Tokyo);
      wRnd.MakeWeather(20, out dbtT, out hrt, out radT, out fair);
      wRnd = new RandomWeather(1, RandomWeather.Location.Sapporo);
      wRnd.MakeWeather(20, out dbtS, out hrt, out radS, out fair);
      wRnd = new RandomWeather(1, RandomWeather.Location.Naha);
      wRnd.MakeWeather(20, out dbtN, out hrt, out radN, out fair);

      //太陽
      Sun sunT = new Sun(35, 41.2, 0, 139, 45.9, 0, 135, 0, 0);
      Sun sunS = new Sun(43, 3.5, 0, 141, 19.9, 0, 135, 0, 0);
      Sun sunN = new Sun(26, 12.2, 0, 127, 41.3, 0, 135, 0, 0);

      //計算,書き出し処理
      using (StreamWriter sWriter = new StreamWriter
        ("PhotovoltaicPanelTest.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        //タイトル行
        sWriter.Write("年");
        for (int i = 0; i < 10; i++) sWriter.Write(",東京" + (i * 10) + "度");
        for (int i = 0; i < 10; i++) sWriter.Write(",札幌" + (i * 10) + "度");
        for (int i = 0; i < 10; i++) sWriter.Write(",那覇" + (i * 10) + "度");
        sWriter.WriteLine();

        DateTime dt = new DateTime(1999, 1, 1, 0, 0, 0);
        double[] pSumT = new double[10];
        double[] pSumS = new double[10];
        double[] pSumN = new double[10];
        for (int i = 0; i < 20; i++)
        {
          for (int j = 0; j < pSumT.Length; j++) pSumT[j] = pSumS[j] = pSumN[j] = 0;
          for (int j = 0; j < 8760; j++)
          {
            //太陽位置を更新して直散分離
            sunT.Update(dt);
            sunS.Update(dt);
            sunN.Update(dt);
            sunT.SeparateGlobalHorizontalRadiation(radT[8760 * i + j], Sun.SeparationMethod.Erbs);
            sunS.SeparateGlobalHorizontalRadiation(radS[8760 * i + j], Sun.SeparationMethod.Erbs);
            sunN.SeparateGlobalHorizontalRadiation(radN[8760 * i + j], Sun.SeparationMethod.Erbs);

            //発電量[W]を計算
            for (int k = 0; k < pvs.Length; k++)
            {
              pSumT[k] += pvs[k].GetPower(dbtT[8760 * i + j], 0.1, sunT);
              pSumS[k] += pvs[k].GetPower(dbtS[8760 * i + j], 0.1, sunS);
              pSumN[k] += pvs[k].GetPower(dbtN[8760 * i + j], 0.1, sunN);
            }
            dt = dt.AddHours(1);
          }
          //書き出し処理
          sWriter.Write(i + 1);
          for (int j = 0; j < pvs.Length; j++) sWriter.Write("," + pSumT[j]);
          for (int j = 0; j < pvs.Length; j++) sWriter.Write("," + pSumS[j]);
          for (int j = 0; j < pvs.Length; j++) sWriter.Write("," + pSumN[j]);
          sWriter.WriteLine();
          dt = dt.AddYears(-1);
        }
      }
    }

    #endregion

  }
}
