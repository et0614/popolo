using System;
using System.Text;

using System.IO;
using Popolo.ThermophysicalProperty;

namespace PopoloTester
{
  class ThermoPhysicalPropertiesTester
  {

    #region 蒸気物性テスト

    public static void SteamTableTest()
    {
      using (StreamWriter sWriter =
          new StreamWriter("SteamTable.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        //タイトル行
        sWriter.WriteLine("飽和温度[℃], 飽和圧力[kPa], 飽和水比体積[m3/kg]," +
            " 飽和蒸気比体積[m3/kg], 飽和水エンタルピー[kJ/kg], " +
            "飽和蒸気エンタルピー[kJ/kg], 蒸発潜熱[kJ/kg], " +
            "飽和水エントロピー[kJ/kgK], 飽和蒸気エントロピー[kJ/kgK]");

        //蒸気物性の計算
        for (int i = 0; i < 19; i++)
        {
          double ts;
          if (i == 0) ts = 0.01;
          else ts = i * 20;
          double ps = Water.GetSaturationPressure(ts);
          sWriter.WriteLine(ts + "," + ps + ","
              + Water.GetSaturatedLiquidSpecificVolume(ts) + ","
              + Water.GetSaturatedVaporSpecificVolume(ts, ps) + ","
              + Water.GetSaturatedLiquidEnthalpy(ts) + ","
              + Water.GetSaturatedVaporEnthalpy(ts) + ","
              + Water.GetVaporizationLatentHeat(ts) + ","
              + Water.GetSaturatedLiquidEntropy(ts) + ","
              + Water.GetSaturatedVaporEntropy(ts));
        }
      }
    }

    #endregion

    #region 水物性計算テスト

    /// <summary>水温・直径・水量から配管内対流熱伝達率を計算する</summary>
    /// <param name="waterTemperature">水温[C]</param>
    /// <param name="diameter">直径[m]</param>
    /// <param name="waterFlowRate">水量[L/min]</param>
    /// <returns>対流熱伝達率[W/(m2·K)]</returns>
    public static double GetConvectiveHeatTransferCoefficientOfTube
        (double waterTemperature, double diameter, double waterFlowRate)
    {
      //動粘性係数[m2/s]・熱拡散率[m2/s]・熱伝導率[W/(m·K)]を計算
      double v = Water.GetLiquidDynamicViscosity(waterTemperature);
      double a = Water.GetLiquidThermalDiffusivity(waterTemperature);
      double lambda = Water.GetLiquidThermalConductivity(waterTemperature);

      //配管内流速[m/s]を計算
      double u = (waterFlowRate / 60 / 1000) / (Math.Pow(diameter / 2, 2) * Math.PI);

      //ヌセルト数を計算
      double reNumber = u * diameter / v;
      double prNumber = v / a;
      double nuNumber = 0.023 * Math.Pow(reNumber, 0.8) * Math.Pow(prNumber, 0.4);

      //ヌセルト数から対流熱伝達率を計算
      return nuNumber * lambda / diameter;
    }

    /// <summary>臭化リチウムの物性テスト1</summary>
    public static void LithiumBromideTest1()
    {
      using (StreamWriter sWriter = new StreamWriter("LithiumBromide.csv"))
      {
        for (int i = 0; i < 10; i++)
        {
          //タイトル行
          if (i == 0)
          {
            sWriter.Write("MassFraction,");
            for (int j = 270; j <= 500; j += 10)
            {
              sWriter.Write(j + ",");
            }
            sWriter.WriteLine();
          }

          double massFraction = 0.3 + 0.05 * i;
          sWriter.Write(massFraction + ",");
          for (int j = 270; j <= 500; j += 10)
          {
            double satT = LithiumBromide.GetVaporTemperatureFromLiquidTemperatureAndMassFraction
                (j, massFraction);
            sWriter.Write(satT + ",");
          }
          sWriter.WriteLine();
        }
      }
    }

    /// <summary>臭化リチウムの物性テスト2</summary>
    public static void LithiumBromideTest2()
    {
      using (StreamWriter sWriter = new StreamWriter("LithiumBromide.csv"))
      {
        for (int i = 40; i <= 70; i += 5)
        {
          //タイトル行
          if (i == 40)
          {
            sWriter.Write("MassFraction,");
            for (int j = 15; j <= 165; j += 10)
            {
              sWriter.Write(j + ",");
            }
            sWriter.WriteLine();
          }

          double massFraction = 0.01 * i;
          sWriter.Write(massFraction + ",");
          for (int j = 15; j <= 165; j += 10)
          {
            double enthalpy = LithiumBromide.GetEnthalpyFromLiquidTemperatureAndMassFraction
                (j + 273.15, massFraction);
            sWriter.Write(enthalpy + ",");
          }
          sWriter.WriteLine();
        }
      }
    }

    /// <summary>臭化リチウムの物性テスト3</summary>
    public static void LithiumBromideTest3()
    {
      using (StreamWriter sWriter = new StreamWriter("LithiumBromide.csv"))
      {
        for (int i = 0; i <= 10; i++)
        {
          //タイトル行
          if (i == 0)
          {
            sWriter.Write("MassFraction,");
            for (int j = 270; j <= 440; j += 10)
            {
              sWriter.Write(j + ",");
            }
            sWriter.WriteLine();
          }

          double massFraction = 0.46 + 0.02 * i;
          sWriter.Write(massFraction + ",");
          for (int j = 270; j <= 440; j += 10)
          {
            double satT = LithiumBromide.GetVaporTemperatureFromLiquidTemperatureAndMassFraction
                (j, massFraction);
            sWriter.Write(satT + ",");
          }
          sWriter.WriteLine();
        }
      }
    }

    #endregion

    #region 湿り空気物性テスト

    public static void moistAirTest()
    {
      //標準気圧とする
      const double ATM = 101.325;

      //表計算ソフトでの描画に備え、計算結果をCSV形式で書き出す
      using (StreamWriter sWriter = new StreamWriter("MoistAirTable.csv"))
      {
        //ヘッダ行書き出し
        sWriter.Write("T, SatT, RH10, RH30, RH50, RH70, RH90, ");
        sWriter.Write("H20, H40, H60, H80, H100, ");
        sWriter.WriteLine("WB5, WB10, WB15, WB20, WB25, WB30, WB35");

        //乾球温度は0～50℃で1℃刻みで描画する
        for (int t = 0; t <= 50; t++)
        {
          //飽和絶対湿度[kg/kg]の計算
          double ps = Water.GetSaturationPressure(t);
          double satW = MoistAir.GetHumidityRatioFromWaterVaporPartialPressure(ps, ATM);
          //結果書き出し
          sWriter.Write(t + "," + satW);

          //等相対湿度線を10～90%の範囲で20%刻みで描画する
          for (int i = 0; i < 5; i++)
          {
            //相対湿度[%]
            double rhd = 10 + i * 20;
            double rhdW = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(t, rhd, ATM);
            if ((rhdW < satW) && (0 < rhdW)) sWriter.Write("," + rhdW);
            else sWriter.Write(",");
          }

          //等比エンタルピー線を20～120kJ/kgの範囲で20kJ/kg刻みで描画する
          for (int i = 0; i < 5; i++)
          {
            //比エンタルピー[kJ/kg]
            double ent = 20 + i * 20;
            double entW = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndEnthalpy(t, ent);
            if ((entW < satW) && (0 < entW)) sWriter.Write("," + entW);
            else sWriter.Write(",");
          }

          //等湿球温度線を5～35℃の範囲で5℃刻みで描画する
          for (int i = 0; i < 7; i++)
          {
            //湿球温度[℃]
            double wbt = 5 + i * 5;
            double wbtW = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(t, wbt, ATM);
            if ((wbtW < satW) && (0 < wbtW)) sWriter.Write("," + wbtW);
            else sWriter.Write(",");
          }
          sWriter.WriteLine();
        }
      }
    }

    public static void moistAirTest2()
    {
      using (StreamWriter sWriter = new StreamWriter("MoistAirTable.csv"))
      {
        sWriter.WriteLine("温度,粘性係数,動粘性係数,熱伝導率,膨張率");
        for (int i = 0; i <= 100; i++)
        {
          sWriter.WriteLine(
              i + ", " +
              MoistAir.GetViscosity(i) + ", " +
              MoistAir.GetDynamicViscosity(i, 0, 101.325) + ", " +
              MoistAir.GetThermalConductivity(i) + ", " +
              MoistAir.GetExpansionCoefficient(i)
              );
        }
      }
    }

    #endregion

    #region 冷媒物性テスト

    public static void RefrigerantTest1()
    {
      using (StreamWriter sWriter = new StreamWriter
        ("RefrigerantTest1.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        //冷媒種類はR410A
        Refrigerant r410a = new Refrigerant(Refrigerant.Fluid.R410A);
        //double a, b, c;
        //r410a.GetSaturatedPropertyFromPressure(4000, out a, out b, out c);

        //飽和液と飽和蒸気の密度
        double rhoSl, rhoSv;

        //飽和液線と飽和蒸気線の描画
        sWriter.WriteLine("飽和P[kPa], 飽和液h[kJ/kg], 飽和蒸気h[kJ/kg]");
        //圧力範囲[kPa]
        const double minPressure = 700;
        const double maxPressure = 4000;
        //飽和線の描画点数
        const int plotNumber = 50;
        double delta = (maxPressure - minPressure) / plotNumber;
        for (int i = 0; i <= plotNumber; i++)
        {
          double satTemp;
          double pressure = minPressure + delta * i;
          r410a.GetSaturatedPropertyFromPressure
            (pressure, out rhoSl, out rhoSv, out satTemp);
          double rhoHl = r410a.GetEnthalpyFromTemperatureAndDensity(satTemp, rhoSl);
          double rhoHv = r410a.GetEnthalpyFromTemperatureAndDensity(satTemp, rhoSv);
          sWriter.WriteLine(pressure + "," + rhoHl + "," + rhoHv);
        }

        sWriter.WriteLine();
        sWriter.WriteLine("冷凍サイクルの計算");

        //冷凍サイクルの計算
        double evapP;    //蒸発圧力[kPa]
        double evapT = 5; //蒸発温度[C]
        r410a.GetSaturatedPropertyFromTemperature
          (evapT + 273.15, out rhoSl, out rhoSv, out evapP);
        double tCmpIn = evapT + 273.15 + 5;  //圧縮機入口温度[K]（過熱度5C）
        double rhoCmpIn = r410a.GetDensityFromPressureAndTemperature(evapP, tCmpIn);
        double hCmpIn = r410a.GetEnthalpyFromTemperatureAndDensity(tCmpIn, rhoCmpIn);
        double sCmpIn = r410a.GetEntropyFromTemperatureAndDensity(tCmpIn, rhoCmpIn);
        sWriter.WriteLine("圧縮機入口比エンタルピー[kJ/kg], 圧縮機入口圧力[kPa]");
        sWriter.WriteLine(hCmpIn + "," + evapP);

        double condP = 3500; //凝縮圧力[kPa]
        double tCmpOut, rhoCmpOut, hCmpOut, uCmpOut;
        r410a.GetStateFromPressureAndEntropy
          (condP, sCmpIn, out tCmpOut, out rhoCmpOut, out hCmpOut, out uCmpOut);
        sWriter.WriteLine("圧縮機出口比エンタルピー[kJ/kg], 圧縮機出口圧力[kPa]");
        sWriter.WriteLine(hCmpOut + "," + condP);

        double condT; //凝縮温度[K]
        r410a.GetSaturatedPropertyFromPressure(condP, out rhoSl, out rhoSv, out condT);
        double tEvpIn = condT - 10;  //圧縮機入口温度[K]（過冷却度10C）
        double rhoEvpIn = r410a.GetDensityFromPressureAndTemperature(condP, tEvpIn);
        double hEvpIn = r410a.GetEnthalpyFromTemperatureAndDensity(tEvpIn, rhoEvpIn);
        double sEvpIn = r410a.GetEntropyFromTemperatureAndDensity(tEvpIn, rhoEvpIn);
        sWriter.WriteLine("膨張弁入口比エンタルピー[kJ/kg], 膨張弁入口圧力[kPa]");
        sWriter.WriteLine(hEvpIn + "," + condP);

        sWriter.WriteLine("膨張弁出口比エンタルピー[kJ/kg], 膨張弁出口圧力[kPa]");
        sWriter.WriteLine(hEvpIn + "," + evapP);
      }
    }

    public static void RefrigerantTest2()
    {
      //モリエル線図を描画する
      //冷媒種類はR134a
      Refrigerant r134a = new Refrigerant(Refrigerant.Fluid.R134a);

      using (StreamWriter sWriter = new StreamWriter("PHDiagram.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        //飽和液と飽和蒸気の密度
        double rhoSl, rhoSv;

        //ヘッダ行
        sWriter.Write
            ("飽和圧力[kPa], 飽和液比エンタルピー[kJ/kg], 飽和蒸気比エンタルピー[kJ/kg],");
        //温度
        for (int i = 0; i <= 100; i += 10) sWriter.Write(i.ToString() + " C,");
        //エントロピー
        for (int i = 170; i <= 200; i += 5) sWriter.Write((i / 100d).ToString() + " kJ/kgK,");
        sWriter.WriteLine();

        //圧力範囲[kPa]
        const double minPressure = 200;
        const double maxPressure = 2000;
        //飽和線の描画点数
        const int plotNumber = 50;
        double delta = (maxPressure - minPressure) / plotNumber;
        for (int i = 0; i <= plotNumber; i++)
        {
          //飽和線の描画
          double satTemp;
          double pressure = minPressure + delta * i;
          r134a.GetSaturatedPropertyFromPressure
              (pressure, out rhoSl, out rhoSv, out satTemp);
          double rhoHl = r134a.GetEnthalpyFromTemperatureAndDensity
              (satTemp, rhoSl);
          double rhoHv = r134a.GetEnthalpyFromTemperatureAndDensity
              (satTemp, rhoSv);
          sWriter.Write(pressure + "," + rhoHl + "," + rhoHv + ",");

          double h, rho, u;
          //等温度線の描画
          for (int j = 0; j <= 100; j += 10)
          {
            //計算範囲外の場合
            if ((j < satTemp - 273.15 - 10))// || (satTemp - 273.15 + 80 < j))
              sWriter.Write("-,");
            else
            {
              double s;
              r134a.GetStateFromPressureAndTemperature
                (pressure, j + 273.15, out s, out rho, out h, out u);
              sWriter.Write(h.ToString("F2") + ",");
            }
          }

          //等比エントロピー線の描画
          for (int j = 170; j <= 200; j += 5)
          {
            //気相域の場合のみ出力
            double satS = r134a.GetEntropyFromTemperatureAndDensity
              (satTemp, rhoSv);
            if (j / 100d < satS) sWriter.Write("-,");
            else
            {
              double t;
              r134a.GetStateFromPressureAndEntropy
                (pressure, j / 100d, out t, out rho, out h, out u);
              sWriter.Write(h.ToString("F2") + ",");
            }
          }
          sWriter.WriteLine();
        }
      }
    }

    #endregion

  }
}
