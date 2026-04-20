using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;

using Popolo.Weather;
using Popolo.ThermophysicalProperty;

namespace PopoloTester
{
  class WeatherTester
  {

    #region 日射テスト

    public static void RadiationSeparateTest(bool isCloudy)
    {
      double[] ghRadiation;
      DateTime sTime;
      if (isCloudy)
      {
        //東京都2014/7/20 5-20時の水平面全天日射[W/m2]（曇天日）
        ghRadiation = new double[]
        { 0, 28, 81, 275, 339, 569, 594, 486, 575, 536, 378, 356, 142, 8, 3, 0 };
        sTime = new DateTime(2014, 7, 20, 4, 30, 0);
      }
      else
      {
        //東京都2014/8/6 5-20時の水平面全天日射[W/m2]（晴天日）
        ghRadiation = new double[]
        { 0, 53, 244, 444, 631, 778, 881, 928, 917, 844, 719, 550, 358, 161, 17, 0 };
        sTime = new DateTime(2014, 8, 6, 4, 30, 0);
      }

      //東京地点で初期化
      Sun sun = new Sun(35.7, 139.8, 135);
      using (StreamWriter sWriter = new StreamWriter("sun.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        //直散分離手法一覧を取得
        Array mtds = Enum.GetValues(typeof(Sun.SeparationMethod));

        //タイトル行
        sWriter.Write("時刻, 全天日射[W/m2]");
        foreach (Sun.SeparationMethod mtd in mtds) sWriter.Write(", " + mtd.ToString() + ", ");
        sWriter.WriteLine();
        //直散分離計算実行
        for (int i = 0; i < ghRadiation.Length; i++)
        {
          sun.Update(sTime.AddHours(i));
          sWriter.Write(sun.CurrentDateTime.Hour + ", " + ghRadiation[i]);
          foreach (Sun.SeparationMethod mtd in mtds)
          {
            sun.SeparateGlobalHorizontalRadiation(ghRadiation[i], mtd);
            sWriter.Write(", " + sun.DirectNormalRadiation + ", " + sun.DiffuseHorizontalRadiation);
          }
          sWriter.WriteLine();
        }
      }
    }

    public static void SunRiseTest()
    {
      Sun sun = new Sun(Sun.City.Tokyo);
      sun.Update(new DateTime(1981, 1, 2, 0, 0, 0));
      DateTime dt1 = sun.GetSunRiseTime();
      DateTime dt2 = sun.GetSunSetTime();
    }

    public static void InclineTest(bool isSummer)
    {
      DateTime dTime;
      if (isSummer) dTime = new DateTime(2014, 6, 22, 4, 0, 0);
      else dTime = new DateTime(2014, 12, 22, 4, 0, 0);

      //傾斜面を作成
      Incline[] inc = new Incline[5];
      inc[0] = new Incline(Incline.Orientation.W, 0.5 * Math.PI);
      inc[1] = new Incline(Incline.Orientation.E, 0.5 * Math.PI);
      inc[2] = new Incline(Incline.Orientation.N, 0.5 * Math.PI);
      inc[3] = new Incline(Incline.Orientation.S, 0.5 * Math.PI);
      inc[4] = new Incline(Incline.Orientation.S, 0);

      //東京地点で初期化
      Sun sun = new Sun(35.7, 139.8, 135);

      using (StreamWriter sWriter =
          new StreamWriter("Incline.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        //タイトル用
        sWriter.WriteLine("時刻, 東面, 西面, 北面, 南面, 水平面");

        for (int i = 0; i <= 32; i++)
        {
          sun.Update(dTime.AddMinutes(30 * i));
          double idn = sun.GetDirectNormalRadiation(0.65);
          sWriter.Write(sun.CurrentDateTime.ToShortTimeString());
          for (int j = 0; j < inc.Length; j++)
            sWriter.Write(", " + inc[j].GetDirectSolarRadiationRate(sun) * idn);
          sWriter.WriteLine();
        }
      }
    }

    public static void InclineTest2(bool isSummer)
    {
      DateTime dTime;
      if (isSummer) dTime = new DateTime(2014, 6, 22, 0, 0, 0);
      else dTime = new DateTime(2014, 12, 22, 0, 0, 0);

      //傾斜面を作成
      Incline[] inc = new Incline[5];
      inc[0] = new Incline(Incline.Orientation.W, 0.5 * Math.PI);
      inc[1] = new Incline(Incline.Orientation.E, 0.5 * Math.PI);
      inc[2] = new Incline(Incline.Orientation.N, 0.5 * Math.PI);
      inc[3] = new Incline(Incline.Orientation.S, 0.5 * Math.PI);
      inc[4] = new Incline(Incline.Orientation.S, 0);

      //昭和基地//遊び
      Sun sun = new Sun(-69d + 22d / 3600, 39d + 35d / 60 + 24d / 3600, 39d + 35d / 60 + 24d / 3600);

      using (StreamWriter sWriter =
          new StreamWriter("Incline.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        //タイトル用
        sWriter.WriteLine("時刻, 東面, 西面, 北面, 南面, 水平面, 太陽高度");

        for (int i = 0; i <= 48; i++)
        {
          sun.Update(dTime.AddMinutes(30 * i));
          double idn = sun.GetDirectNormalRadiation(0.65);
          sWriter.Write(sun.CurrentDateTime.ToShortTimeString());
          for (int j = 0; j < inc.Length; j++)
            sWriter.Write(", " + inc[j].GetDirectSolarRadiationRate(sun) * idn);
          sWriter.Write(", " + sun.Altitude * 180d / Math.PI);
          sWriter.WriteLine();
        }
      }
    }

    #endregion

    #region 気象テスト

    public static void RandomWeatherTest(uint seed)
    {
      int[] days =
        new int[] { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
      using (StreamWriter sWriter = new StreamWriter
          ("weather.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        for (int i = 0; i < days.Length; i++)
          sWriter.Write("," + (i + 1).ToString("F0") + "月");
        sWriter.WriteLine();

        foreach (RandomWeather.Location loc
          in Enum.GetValues(typeof(RandomWeather.Location)))
        {
          double[] dbt, hrt, rad;
          bool[] fcf;
          RandomWeather wRnd = new RandomWeather(seed, loc);
          wRnd.MakeWeather(1, out dbt, out hrt, out rad, out fcf);
          double[] dbtAve = new double[12];
          double[] rhdAve = new double[12];
          DateTime dt = new DateTime(1999, 1, 1, 0, 0, 0);
          for (int i = 0; i < dbt.Length; i++)
          {
            dbtAve[dt.Month - 1] += dbt[i];
            double rhd =
              MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio
              (dbt[i], hrt[i] / 1000d, 101.325);
            rhdAve[dt.Month - 1] += rhd;
            dt = dt.AddHours(1);
          }
          sWriter.Write(loc.ToString() + "乾球温度");
          for (int i = 0; i < dbtAve.Length; i++)
            sWriter.Write("," + (dbtAve[i] / (days[i] * 24)).ToString("F2"));
          sWriter.WriteLine();
          sWriter.Write(loc.ToString() + "相対湿度");
          for (int i = 0; i < rhdAve.Length; i++)
            sWriter.Write("," + (rhdAve[i] / (days[i] * 24)).ToString("F2"));
          sWriter.WriteLine();
        }
      }
    }

    public static void RandomWeatherTest2(uint seed)
    {
      double[] dbt, hrt, rad;
      bool[] fcf;
      RandomWeather wRnd = new RandomWeather(seed, RandomWeather.Location.Tokyo);
      wRnd.MakeWeather(20, out dbt, out hrt, out rad, out fcf);

      DateTime dt = new DateTime(2014, 1, 1, 0, 0, 0);
      using (StreamWriter sWriter = new StreamWriter("weather.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        sWriter.WriteLine("日付, 時刻, 乾球温度, 絶対湿度, 水平面全天日射, 雲量<10");
        for (int i = 0; i < 20 * 8760; i++)
        {
          sWriter.WriteLine(dt.ToShortDateString() + ", " + dt.Hour + ", " +
               dbt[i] + ", " + hrt[i] + ", " + rad[i] + ", " + fcf[i]);
          dt = dt.AddHours(1);
          if (dt.Month == 2 && dt.Day == 29) dt.AddDays(1);
        }
      }
    }

    public static void RandomWeatherTest3(uint seed)
    {
      double[] dbt, hrt, rad;
      bool[] fcf;
      RandomWeather wRnd = new RandomWeather(seed, RandomWeather.Location.Tokyo);
      wRnd.MakeWeather(1, out dbt, out hrt, out rad, out fcf);

      using (StreamWriter sWriter = new StreamWriter("weather.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        sWriter.WriteLine("日付, 時刻, 乾球温度, 絶対湿度, 水平面全天日射, 法線面直達日射, 水平面天空日射, 法線面直達照度, 天空照度, 雲量<10");
        Sun sun = new Sun(Sun.City.Tokyo);
        sun.CalculateIlluminance = true;
        DateTime dt = new DateTime(1999, 1, 1, 0, 0, 0);
        for (int i = 0; i < 8760; i++)
        {
          sun.SeparateGlobalHorizontalRadiation(rad[i], Sun.SeparationMethod.Erbs);
          sWriter.WriteLine(dt.ToShortDateString() + ", " + dt.Hour + ", "
            + dbt[i] + ", " + hrt[i] + ", "
            + rad[i] + ", " + sun.DirectNormalRadiation + "," + sun.DiffuseHorizontalRadiation + ","
            + sun.DirectNormalIlluminance + "," + sun.DiffuseIlluminance + "," + fcf[i]);
          dt = dt.AddHours(1);
          sun.Update(dt);
        }
      }
    }

    public static void ConverterTest1()
    {
      using (StreamReader sReader = new StreamReader("TokyoStandard.has"))
      using (StreamWriter sWriter = new StreamWriter
        ("TokyoStandard.csv", false, Encoding.UTF8))
      {
        string haspData = sReader.ReadToEnd();
        sWriter.Write(WeatherConverter.HASPtoCSV(haspData));
      }
    }

    public static void ConverterTest2()
    {
      using (StreamReader sReader = new StreamReader("TokyoStandard.exa", Encoding.GetEncoding("Shift_JIS")))
      using (StreamWriter sWriter = new StreamWriter
        ("TokyoStandard.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        string exaData = sReader.ReadToEnd();
        int locNumber;
        double lng, lat;
        sWriter.Write(WeatherConverter.EXAtoCSV(exaData, out locNumber, out lat, out lng));
        Console.WriteLine(locNumber + ": " + lat + " , " + lng);
      }
    }

    #endregion

  }
}
