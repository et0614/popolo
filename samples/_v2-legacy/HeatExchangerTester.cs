using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;

using Popolo.Numerics;
using Popolo.ThermophysicalProperty;
using Popolo.HVAC.HeatExchanger;
using Popolo.HVAC.AirConditioner;
using Popolo.HVAC.Circuit;

namespace PopoloTester
{
  class HeatExchangerTester
  {

    #region 熱交換テスト

    public static void heatExchangeTest1()
    {
      int tci = 0;
      int tco = 1;
      double[] rFac = new double[] { 4.0, 3.0, 2.0, 1.5, 1.1, 0.8, 0.6, 0.4, 0.2 };
      HeatExchange.FlowType[] fTypes = new HeatExchange.FlowType[] {
        HeatExchange.FlowType.CrossFlow_BothFluidMixed,
        HeatExchange.FlowType.CrossFlow_BothFluidsUnmixed,
        HeatExchange.FlowType.CrossFlow_CminMixed };

      //最小化関数の定義
      using (StreamWriter sWriter =
        new StreamWriter("hexTest1.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        for (int i = 0; i < 9; i++) sWriter.Write("," + rFac[i]);
        sWriter.WriteLine();

        for (int i = 0; i < fTypes.Length; i++)
        {
          sWriter.WriteLine(fTypes[i].ToString());
          for (int j = 0; j <= 100; j++)
          {
            double qq = 0.01 * j;
            sWriter.Write(qq);
            for (int k = 0; k < 9; k++)
            {
              if (qq == 0) sWriter.Write("," + 1);
              else
              {
                double thi = (tco + tci * (qq - 1)) / qq;
                double tho = thi - rFac[k] * (tco - tci);
                double dt1 = thi - tco;
                double dt2 = tho - tci;
                if (dt1 <= 0 || dt2 <= 0) sWriter.Write("," + 0);
                else
                {
                  double lmtd = (dt1 - dt2) / Math.Log(dt1 / dt2);
                  double tm = HeatExchange.GetMeanTemperatureDifference
                    (thi, tci, tho, tco, fTypes[i]);
                  sWriter.Write("," + tm / lmtd);
                }
              }
            }
            sWriter.WriteLine();
          }
        }
      }
    }

    public static void heatExchangeTest2()
    {
      using (StreamWriter sWriter =
          new StreamWriter("hexTest2.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        double[] ntu = new double[]
        { 0, 0.25, 0.5, 0.75, 1, 1.25, 1.5, 1.75, 2, 2.5, 3, 3.5, 4, 4.5, 5, 6, 7 };
        double[] cmcm = new double[] { 0, 0.25, 0.5, 0.75, 1 };

        sWriter.WriteLine("NTU, 0.00, 0.25, 0.50, 0.75, 1.00");
        foreach (HeatExchange.FlowType ft in Enum.GetValues
          (typeof(HeatExchange.FlowType)))
        {
          sWriter.WriteLine(ft.ToString());
          for (int i = 0; i < ntu.Length; i++)
          {
            sWriter.Write(ntu[i].ToString() + ",");
            for (int j = 0; j < cmcm.Length; j++)
              sWriter.Write(HeatExchange.GetEffectiveness(ntu[i], cmcm[j], ft) + ",");
            sWriter.WriteLine();
          }
        }
      }
    }

    public static void heatExchangeTest3()
    {
      double heatTransfer = 500;
      double heatTransferCoefficient = 4;
      double hotInletTemperature = 12;
      double coldInletTemperature = 6;
      //熱容量流量[kW/K]
      double mh = 500 / (12 - 7);
      double mc = 500 / (10 - 6);

      //熱通過有効度の計算
      double epsilon = heatTransfer / Math.Min(mh, mc) /
          (hotInletTemperature - coldInletTemperature);

      //NTUの計算
      double rmc = Math.Min(mh, mc) / Math.Max(mh, mc);
      double ntu = HeatExchange.GetNTU(epsilon, rmc,
          HeatExchange.FlowType.CrossFlow_BothFluidsUnmixed);

      //伝熱面積の計算
      double surfaceArea = ntu / heatTransferCoefficient * Math.Min(mh, mc);
      Console.WriteLine("伝熱面積=" + surfaceArea.ToString("F0") + "m2");
    }

    public static void heatExchangeTest4()
    {
      double surfaceArea = 89;
      double heatTransferCoefficient = 4;
      double hotInletTemperature = 12;
      double coldInletTemperature = 6;
      //熱容量流量[kW/K]
      double mh = 500 / (12 - 7);
      double mc = 500 / (10 - 6);

      //二分法を用いて求根
      Roots.ErrorFunction eFnc = delegate (double heatTransfer)
      {
        //出口温度[C]を計算
        double coldOutletTemperature = coldInletTemperature + heatTransfer / mc;
        double hotOutletTemperature = hotInletTemperature - heatTransfer / mh;
        //平均温度差[K]を計算
        double tm = HeatExchange.GetMeanTemperatureDifference
            (hotInletTemperature, coldInletTemperature, hotOutletTemperature,
            coldOutletTemperature, HeatExchange.FlowType.CrossFlow_BothFluidsUnmixed);
        //誤差を評価
        return tm * surfaceArea * heatTransferCoefficient - heatTransfer;
      };
      double ht1 = Roots.Bisection(eFnc, 1, 500, 0.001, 0.00001, 20);
      Console.WriteLine("熱交換量=" + ht1.ToString("F0") + "kW");

      double rmc = Math.Min(mh, mc) / Math.Max(mh, mc);
      double ntu = 4d * 89d / Math.Min(mh, mc);
      double epsilon = HeatExchange.GetEffectiveness
          (ntu, rmc, HeatExchange.FlowType.CrossFlow_BothFluidsUnmixed);
      double ht2 = epsilon * Math.Min(mh, mc) *
          (hotInletTemperature - coldInletTemperature);
      Console.WriteLine("熱交換量=" + ht2.ToString("F0") + "kW");
    }

    #endregion

    #region プレート熱交換器テスト

    public static void plateHeatExchangerTest()
    {
      PlateHeatExchanger pHex = new PlateHeatExchanger(500, 6, 1792d / 60, 7, 1433d / 60);

      Console.WriteLine("冷却テスト");
      pHex.SupplyTemperatureSetpoint = 7;
      for (int trw = 10; trw <= 20; trw += 2)
      {
        for (int i = 0; i <= 20; i++)
        {
          pHex.ControlSupplyTemperature(6, trw, 1433d / 60 * (0.05 * i));
          Console.Write(pHex.HeatSourceFlowRate.ToString("F2") + ", ");
        }
        Console.WriteLine();
      }

      Console.WriteLine("加熱テスト");
      pHex.SupplyTemperatureSetpoint = 45;
      for (int trw = 30; trw <= 40; trw += 2)
      {
        for (int i = 0; i <= 20; i++)
        {
          pHex.ControlSupplyTemperature(50, trw, 1433d / 60 * (0.05 * i));
          Console.Write(pHex.HeatSourceFlowRate.ToString("F2") + ", ");
        }
        Console.WriteLine();
      }
    }

    #endregion

    #region プレートフィン型熱交換器テスト

    /// <summary>簡易モデルで感度解析を行う</summary>
    public static void PlateFinHeatExchangerTest1()
    {
      //モデルパラメータ
      double kd, kw;  //熱貫流率
      double sArea;   //伝熱面積

      //出力
      double outletAirTemperature;
      double outletAirHumidity;
      double outletWaterTemperature;
      double dryRate;

      //定格条件と能力
      const double inletAirTemperature = 32;          //C
      const double inletAirHumidityRatio = 0.0125;    //kg/kg
      const double borderRelativeHumidity = 90;       //%
      const double inletWaterTemperature = 5;         //C
      const double heatExchange = 70;                 //kW
      const double waterFlowRate = 300d / 60;         //kg/s
      const double airFlowRate = 6750d * 1.2 / 3600;  //kg/s

      //簡易モデルの計算////////////////////////////////////////////////
      double velocity = 2.5;
      double waterSpeed = 1.5;
      //熱貫流率の計算
      CrossFinHeatExchanger.GetHeatTransferCoefficient(waterSpeed, velocity, out kd, out kw);
      //伝熱面積の計算
      sArea = CrossFinHeatExchanger.GetSurfaceArea
        (inletAirTemperature, inletAirHumidityRatio, borderRelativeHumidity,
        inletWaterTemperature, airFlowRate, waterFlowRate, heatExchange, kd, kw);
      //感度解析1（入口水温に対する出口状態の変化）
      Console.WriteLine("簡易モデル（水温変化）");
      Console.WriteLine("入口水温[C], 出口空気温度[C], 出口水温[C], 熱交換量[kW]");
      for (int i = 0; i < 10; i++)
      {
        double iwt = 3 + 0.5 * i;
        //簡易モデルでは温度変化によって熱貫流率は変わらない
        CrossFinHeatExchanger.GetOutletState
          (inletAirTemperature, inletAirHumidityRatio, borderRelativeHumidity, iwt,
          airFlowRate, waterFlowRate, kd, kw, sArea, out outletAirTemperature,
          out outletAirHumidity, out outletWaterTemperature, out dryRate);
        //熱量計算[kW]
        double hl = (outletWaterTemperature - iwt) * 4.186 * waterFlowRate;
        //書き出し処理
        Console.WriteLine(iwt.ToString("F2") + ", " + outletAirTemperature.ToString("F2") + ", " +
          outletWaterTemperature.ToString("F2") + ", " + hl.ToString("F2"));
      }
      //感度解析2（空気温度設定値に対する必要水量の変化）
      Console.WriteLine();
      Console.WriteLine("簡易モデル（空気温度設定値変化）");
      Console.WriteLine("空気温度設定値[kW], 出口空気温度[C], 水量[L/min], 熱交換量[kW]");
      for (int i = 0; i < 10; i++)
      {
        double oats = 12 + 0.5 * i;

        //必要水量[L/min]の計算
        double wf = CrossFinHeatExchanger.GetWaterFlowRate
          (inletAirTemperature, inletAirHumidityRatio, borderRelativeHumidity, inletWaterTemperature,
          velocity, waterSpeed, airFlowRate, waterFlowRate, waterFlowRate, sArea, oats);
        //熱貫流率の計算
        CrossFinHeatExchanger.GetHeatTransferCoefficient
          (waterSpeed * (wf / waterFlowRate), velocity, out kd, out kw);
        //出口状態の計算
        CrossFinHeatExchanger.GetOutletState
          (inletAirTemperature, inletAirHumidityRatio, borderRelativeHumidity,
          inletWaterTemperature, airFlowRate, wf, kd, kw, sArea, out outletAirTemperature,
          out outletAirHumidity, out outletWaterTemperature, out dryRate);
        //熱量計算[kW]
        double hl = (outletWaterTemperature - inletWaterTemperature) * 4.186 * wf;
        //書き出し処理
        Console.WriteLine(oats.ToString("F2") + ", " + outletAirTemperature.ToString("F2") + ", " +
          (wf * 60).ToString("F2") + ", " + hl.ToString("F2"));
      }
    }

    /// <summary>詳細モデルで感度解析を行う</summary>
    public static void PlateFinHeatExchangerTest2()
    {
      //定格条件・能力等
      const double thermalConductivity = 237;     //熱伝導率[W/(mK)]
      const double inletAirTemperature = 32;      //C
      const double inletAirHumidityRatio = 0.0125;//kg/kg
      const double borderRelativeHumidity = 90;   //%
      const double inletWaterTemperature = 5;     //C
      const double waterFlowRate = 300d / 60;     //kg/s
      const double airFlowRate = 6750d * 1.2 / 3600; //kg/s
      double[] cap = new double[] { 52, 70, 79 }; //kW

      //形状
      const double width = 1.0;             //m
      const double height = 0.75;           //m
      const int columnNumber = 20;          //段
      const double finPitch = 0.0029;       //m
      const double finThickness = 0.0002;   //m
      const double innerDiameter = 0.0146;  //m
      const double outerDiameter = 0.0158;  //m

      //プレート熱交換器をインスタンス化
      CrossFinHeatExchanger[] pl = new CrossFinHeatExchanger[3];
      for (int i = 0; i < 3; i++)
      {
        int rNum = 4 + i * 2;
        double depth = rNum * 0.0329;
        pl[i] = new CrossFinHeatExchanger
          (depth, width, height, rNum, columnNumber, finPitch, finThickness, thermalConductivity,
          innerDiameter, outerDiameter, airFlowRate, inletAirTemperature, inletAirHumidityRatio,
          borderRelativeHumidity, waterFlowRate, waterFlowRate, inletWaterTemperature,
          CrossFinHeatExchanger.WaterFlowType.SingleFlow, cap[i], true);
      }

      Console.WriteLine("水量[L/min], 熱交換量(4列)[kW], 出口温度(4列)[C]," +
        "熱交換量(6列)[kW], 出口温度(6列)[C],熱交換量(8列)[kW], 出口温度(8列)[C]");
      for (int i = 0; i <= 30; i++)
      {
        //水量[L/min]を計算
        double wf = 10 * i;
        Console.Write(wf.ToString("F1"));
        //出口状態を更新して書き出し
        for (int j = 0; j < 3; j++)
        {
          pl[j].UpdateOutletState
            (inletAirTemperature, inletAirHumidityRatio, inletWaterTemperature, airFlowRate, wf / 60d);
          Console.Write(", " + pl[j].HeatTransfer + ", " + pl[j].OutletAirTemperature);
        }
        Console.WriteLine();
      }
    }

    /// <summary>詳細モデルで感度解析を行う2</summary>
    public static void PlateFinHeatExchangerTest3()
    {
      //モデルパラメータ
      double kd, kw;  //熱貫流率
      double sArea;   //伝熱面積
      double thermalConductivity = 220; //熱伝導率[W/(mK)]

      //出力
      double outletAirTemperature;
      double outletAirHumidity;
      double outletWaterTemperature;
      double dryRate;

      //定格条件と能力
      const double inletAirTemperature = 32;      //C
      const double inletAirHumidityRatio = 0.0125;//kg/kg
      const double borderRelativeHumidity = 90;   //%
      const double inletWaterTemperature = 5;     //C
      const double heatExchange = 70;             //kW
      const double waterFlowRate = 300d / 60;     //kg/s
      const double airFlowRate = 6750d * 1.2 / 3600; //kg/s

      //形状
      const double depth = 0.197;           //m
      const double width = 1.0;             //m
      const double height = 0.75;           //m
      const int rowNumber = 6;              //列
      const int columnNumber = 20;          //段
      const double finPitch = 0.0029;       //m
      const double finThickness = 0.0002;   //m
      const double innerDiameter = 0.0146;  //m
      const double outerDiameter = 0.0158;  //m

      //幾何学形状の計算
      double airWaterSurfaceRatio, coreArea, equivalentFinRadius,
        equivalentDiameter, waterPath, airSurfaceArea;
      waterPath = columnNumber * 1;
      CrossFinHeatExchanger.GetGeometricCompfigulation
        (depth, width, height, rowNumber, columnNumber, finPitch, finThickness,
        innerDiameter, outerDiameter, out airWaterSurfaceRatio, out coreArea,
        out equivalentFinRadius, out equivalentDiameter, out airSurfaceArea);

      //熱貫流率の計算
      CrossFinHeatExchanger.GetHeatTransferCoefficient
        (airWaterSurfaceRatio, coreArea, equivalentFinRadius, equivalentDiameter,
        waterPath, finThickness, thermalConductivity, innerDiameter, outerDiameter,
        airFlowRate, inletAirTemperature, inletAirHumidityRatio,
        borderRelativeHumidity, waterFlowRate, inletWaterTemperature, out kd, out kw);
      //伝熱面積の計算
      sArea = CrossFinHeatExchanger.GetSurfaceArea(
        inletAirTemperature, inletAirHumidityRatio, borderRelativeHumidity,
        inletWaterTemperature, airFlowRate, waterFlowRate, heatExchange, kd, kw);
      //感度解析1（入口水温に対する出口状態の変化）
      Console.WriteLine("詳細モデル（水温変化）");
      Console.WriteLine("入口水温[C], 出口空気温度[C], 出口水温[C], 熱交換量[kW]");
      for (int i = 0; i < 10; i++)
      {
        double iwt = 3 + 0.5 * i;
        //熱貫流率の計算
        CrossFinHeatExchanger.GetHeatTransferCoefficient
          (airWaterSurfaceRatio, coreArea, equivalentFinRadius, equivalentDiameter,
          waterPath, finThickness, thermalConductivity, innerDiameter, outerDiameter,
          airFlowRate, inletAirTemperature, inletAirHumidityRatio,
          borderRelativeHumidity, waterFlowRate, iwt, out kd, out kw);
        //出口状態の計算
        CrossFinHeatExchanger.GetOutletState
          (inletAirTemperature, inletAirHumidityRatio, borderRelativeHumidity, iwt,
          airFlowRate, waterFlowRate, kd, kw, sArea, out outletAirTemperature,
          out outletAirHumidity, out outletWaterTemperature, out dryRate);
        //熱量計算[kW]
        double hl = (outletWaterTemperature - iwt) * 4.186 * waterFlowRate;
        //書き出し処理
        Console.WriteLine(iwt.ToString("F1") + ", " +
          outletAirTemperature.ToString("F1") + ", " +
          outletWaterTemperature.ToString("F1") + ", " + hl.ToString("F1"));
      }
      //感度解析2（空気温度設定値に対する必要水量の変化）
      Console.WriteLine();
      Console.WriteLine("詳細モデル（空気温度設定値変化）");
      Console.WriteLine("空気温度設定値[kW], 出口空気温度[C], 水量[L/min], 熱交換量[kW]");
      for (int i = 0; i < 10; i++)
      {
        double oats = 12 + 0.5 * i;

        //必要水量[L/min]の計算
        double wf = CrossFinHeatExchanger.GetWaterFlowRate
          (airWaterSurfaceRatio, coreArea, equivalentFinRadius, equivalentDiameter,
          waterPath, finThickness, thermalConductivity, innerDiameter, outerDiameter,
          airFlowRate, inletAirTemperature, inletAirHumidityRatio,
          borderRelativeHumidity, waterFlowRate, inletWaterTemperature,
          waterFlowRate, sArea, oats);
        //熱貫流率の計算
        CrossFinHeatExchanger.GetHeatTransferCoefficient
          (airWaterSurfaceRatio, coreArea, equivalentFinRadius, equivalentDiameter,
          waterPath, finThickness, thermalConductivity, innerDiameter, outerDiameter,
          airFlowRate, inletAirTemperature, inletAirHumidityRatio,
          borderRelativeHumidity, wf, inletWaterTemperature, out kd, out kw);
        //出口状態の計算
        CrossFinHeatExchanger.GetOutletState
          (inletAirTemperature, inletAirHumidityRatio, borderRelativeHumidity,
          inletWaterTemperature, airFlowRate, wf, kd, kw, sArea,
          out outletAirTemperature, out outletAirHumidity,
          out outletWaterTemperature, out dryRate);
        //熱量計算[kW]
        double hl = (outletWaterTemperature - inletWaterTemperature) * 4.186 * wf;
        //書き出し処理
        Console.WriteLine(oats.ToString("F1") + ", " +
          outletAirTemperature.ToString("F1") + ", " +
          (wf * 60).ToString("F1") + ", " + hl.ToString("F1"));
      }
    }

    /// <summary>詳細モデルで感度解析を行う3</summary>
    public static void PlateFinHeatExchangerTest4()
    {
      CrossFinHeatExchanger phex1 = new CrossFinHeatExchanger(3600d * 1.2 / 3600 * 0.75 * 2.5, 3, 32, 0.0125, 95, 300d / 60, 1.5, 300d / 60, 5, 70);
      CrossFinHeatExchanger phex2 = new CrossFinHeatExchanger(0.197, 1.0, 0.75, 6, 20, 0.0029, 0.0002, 220, 0.0146, 0.0158, 1.0 * 0.75 * 3600 * 2.5 * 1.2 / 3600,
          32, 0.0125, 95, 300d / 60, 300d / 60, 5, CrossFinHeatExchanger.WaterFlowType.SingleFlow, 70, true);
      for (int i = 0; i < 6; i++)
      {
        double afl = 300 - i * 50;
        phex1.UpdateOutletState(32, 0.0125, 5, 3600 * 0.75 * 2.5 * 1.2 / 3600, afl / 60d);
        phex2.UpdateOutletState(32, 0.0125, 5, 3600 * 0.75 * 2.5 * 1.2 / 3600, afl / 60d);
        Console.WriteLine(afl + ", " + phex1.OutletAirTemperature.ToString("F1") + ", " + phex2.OutletAirTemperature.ToString("F1"));
      }
      phex1.ControlOutletAirTemperature(32, 0.0125, 5, 3600 * 0.75 * 2.5 * 1.2 / 3600, 19);
      phex2.ControlOutletAirTemperature(32, 0.0125, 5, 3600 * 0.75 * 2.5 * 1.2 / 3600, 19);
    }

    /// <summary>詳細モデルで感度解析を行う4</summary>
    public static void PlateFinHeatExchangerTest5()
    {
      double[] af = new double[] { 0.01, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 4.0, 4.5, 5.0 };

      for (int i = 0; i < af.Length; i++)
      {
        double vel = af[i];
        double kd, kw;
        CrossFinHeatExchanger.GetHeatTransferCoefficient(0.2, vel, out kd, out kw);
        Console.Write(kd.ToString("F1") + ",");
        CrossFinHeatExchanger.GetHeatTransferCoefficient(0.3, vel, out kd, out kw);
        Console.Write(kd.ToString("F1") + ",");
        CrossFinHeatExchanger.GetHeatTransferCoefficient(0.4, vel, out kd, out kw);
        Console.Write(kd.ToString("F1") + ",");
        CrossFinHeatExchanger.GetHeatTransferCoefficient(0.5, vel, out kd, out kw);
        Console.Write(kd.ToString("F1") + ",");
        CrossFinHeatExchanger.GetHeatTransferCoefficient(0.6, vel, out kd, out kw);
        Console.Write(kd.ToString("F1") + ",");
        CrossFinHeatExchanger.GetHeatTransferCoefficient(0.7, vel, out kd, out kw);
        Console.Write(kd.ToString("F1") + ",");
        CrossFinHeatExchanger.GetHeatTransferCoefficient(0.8, vel, out kd, out kw);
        Console.Write(kd.ToString("F1") + ",");
        CrossFinHeatExchanger.GetHeatTransferCoefficient(1.0, vel, out kd, out kw);
        Console.Write(kd.ToString("F1") + ",");
        CrossFinHeatExchanger.GetHeatTransferCoefficient(1.2, vel, out kd, out kw);
        Console.Write(kd.ToString("F1") + ",");
        CrossFinHeatExchanger.GetHeatTransferCoefficient(1.5, vel, out kd, out kw);
        Console.Write(kd.ToString("F1") + ",");
        CrossFinHeatExchanger.GetHeatTransferCoefficient(2.0, vel, out kd, out kw);
        Console.WriteLine(kd.ToString("F1") + ",");
      }
    }

    #region 冷媒コイル

    /// <summary>冷却・除湿・着霜コイルの感度解析を行う</summary>
    private static void CrossFinEvaporatorTest()
    {
      //蒸発器初期化
      double hr = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(7, 6, 101.325);
      CrossFinEvaporator evp = new CrossFinEvaporator(2, 13, 167.0 / 60 * 1.2, 7, hr, 95);

      using (StreamWriter sWriter = new StreamWriter
        ("CrossFinEvaporatorTest.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        //乾球温度・蒸発温度・能力の関係
        double[] te = new double[] { -18, -13, -8, -3, 2, 7 };
        sWriter.WriteLine("乾球温度・蒸発温度・能力の関係");
        sWriter.Write("乾球温度[C]");
        for (int i = 0; i < te.Length; i++)
          sWriter.Write("," + te[i] + "C 交換熱量," + te[i] + "C 除霜," + te[i] + "C 出口温度");
        sWriter.WriteLine();

        for (int i = -15; i <= 15; i++)
        {
          sWriter.Write(i);
          for (int j = 0; j < te.Length; j++)
          {
            hr = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(i, 85, 101.325);
            sWriter.Write("," + evp.GetHeatTransfer(te[j], evp.NominalAirFlowRate, i, hr));
            sWriter.Write("," + evp.DefrostLoad + "," + evp.OutletAirTemperature);
          }
          sWriter.WriteLine();
        }

        sWriter.WriteLine();
        //乾球温度・相対湿度・能力の関係
        double[] rh = new double[] { 50, 60, 70, 80, 90, 100 };
        sWriter.WriteLine("乾球温度・蒸発温度・能力の関係");
        sWriter.Write("乾球温度[C]");
        for (int i = 0; i < rh.Length; i++)
          sWriter.Write("," + rh[i] + "% 交換熱量," + rh[i] + "% 除霜," + rh[i] + "% 出口温度");
        sWriter.WriteLine();

        for (int i = -5; i <= 15; i++)
        {
          sWriter.Write(i);
          for (int j = 0; j < rh.Length; j++)
          {
            hr = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(i, rh[j], 101.325);
            sWriter.Write("," + evp.GetHeatTransfer(-10, evp.NominalAirFlowRate, i, hr));
            sWriter.Write("," + evp.DefrostLoad + "," + evp.OutletAirTemperature);
          }
          sWriter.WriteLine();
        }
      }
    }

    /// <summary>冷却・除湿・着霜コイルの感度解析を行う2</summary>
    private static void CrossFinEvaporatorTest2()
    {
      //蒸発器初期化
      double hr = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature
        (7, 6, 101.325);
      CrossFinEvaporator evp = new CrossFinEvaporator
        (2, 13, 167.0 / 60 * 1.2, 7, hr, 95);

      using (StreamWriter sWriter = new StreamWriter
        ("CrossFinEvaporatorTest2.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        //乾球温度・蒸発温度・能力の関係
        double[] qe = new double[] { 11, 12, 13, 14, 15, 16 };
        sWriter.WriteLine("乾球温度・蒸発温度・能力の関係");
        sWriter.Write("乾球温度[C]");
        for (int i = 0; i < qe.Length; i++)
          sWriter.Write("," + qe[i] + "kW 蒸発温度," + qe[i] + "kW 除霜," + qe[i] + "kW 出口温度");
        sWriter.WriteLine();

        for (int i = -15; i <= 15; i++)
        {
          sWriter.Write(i);
          for (int j = 0; j < qe.Length; j++)
          {
            hr = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(i, 85, 101.325);
            sWriter.Write("," + evp.GetEvaporatingTemperature(qe[j], evp.NominalAirFlowRate, i, hr, false));
            sWriter.Write("," + evp.DefrostLoad);
            sWriter.Write("," + evp.OutletAirTemperature);
          }
          sWriter.WriteLine();
        }
      }
    }

    /// <summary>水噴霧形凝縮器の感度解析を行う</summary>
    private static void CrossFinCondensorTest()
    {
      //凝縮器初期化
      double hr = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(35, 55, 101.325);
      CrossFinCondensor cnd = new CrossFinCondensor(45, 25, 167d / 60 * 1.2, 35, hr);
      cnd.SprayEffectiveness = 0;

      using (StreamWriter sWriter = new StreamWriter
        ("CrossFinCondenserTest.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        //乾球温度・凝縮温度・能力の関係
        double[] rh = new double[] { 35, 55, 75 };
        sWriter.WriteLine("乾球温度・凝縮温度・能力の関係");
        sWriter.Write("乾球温度[C]");
        for (int i = 0; i < rh.Length; i++)
          sWriter.Write("," + rh[i] + "% 交換熱量1," + rh[i] + "% 交換熱量2," + rh[i] + "% 水噴霧");
        sWriter.WriteLine();

        for (int i = 20; i <= 40; i++)
        {
          sWriter.Write(i);
          for (int j = 0; j < rh.Length; j++)
          {
            hr = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(i, rh[j], 101.325);
            cnd.SprayEffectiveness = 0.0;
            sWriter.Write("," + cnd.GetHeatTransfer(45, cnd.NominalAirFlowRate, i, hr));
            cnd.SprayEffectiveness = 0.6;
            sWriter.Write("," + cnd.GetHeatTransfer(45, cnd.NominalAirFlowRate, i, hr));
            sWriter.Write("," + cnd.WaterSupply * 3600);
          }
          sWriter.WriteLine();
        }
      }
    }

    #endregion

    #endregion

    #region 全熱交換器テスト

    public static void RotaryRegeneratorTest()
    {
      RotaryRegenerator rg = new RotaryRegenerator(0.72, 2.15, 0.4, 210, 10000, 8000, true, 35, 0.0195, 26, 0.0105, 0.4);
      using (StreamWriter sWriter = new StreamWriter
        ("RotaryRegenerator.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        //タイトル行
        //流量変更
        for (int i = 0; i <= 10; i++) sWriter.Write("," + (100 - 5 * i) + "%");
        sWriter.WriteLine();

        //回転数変更
        for (int i = 0; i <= 10; i++)
        {
          sWriter.Write((100 - 5 * i) + "%, ");
          double rotatingRate = 1 - 0.05 * i;
          //流量変更
          for (int j = 0; j <= 10; j++)
          {
            double oaf = 10000 * (1 - 0.05 * j);
            rg.UpdateState(oaf, 8000, rotatingRate, 35, 0.0195, 26, 0.0105);
            sWriter.Write(rg.Efficiency.ToString("F3") + ", ");
          }
          sWriter.WriteLine();
        }
      }
    }

    public static void RotaryRegeneratorTest2()
    {
      RotaryRegenerator rg = new RotaryRegenerator
        (0.72, 2.15, 0.4, 210, 10000, 8000, true, 35, 0.0195, 26, 0.0105, 0.4);
      using (StreamWriter sWriter = new StreamWriter
        ("RotaryRegenerator2.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        //タイトル行
        sWriter.WriteLine("外気温度, 外気湿度, 外気比エンタルピー, 成行温度, 成行湿度, 成行比エンタルピー, 制御温度, 制御湿度, 制御比エンタルピー, 消費電力");

        //外気温度変更
        for (int i = 0; i <= 15; i++)
        {
          double dbt = 15 + i;
          double hrt = 0.0045 + 0.001 * i;
          double ent = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(dbt, hrt);
          sWriter.Write(dbt + "," + hrt + "," + ent);
          rg.UpdateState(10000, 8000, 1.0, dbt, hrt, 26, 0.0105);
          ent = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio
            (rg.SupplyAirOutletDrybulbTemperature, rg.SupplyAirOutletHumidityRatio);
          sWriter.Write(", " + rg.SupplyAirOutletDrybulbTemperature);
          sWriter.Write(", " + rg.SupplyAirOutletHumidityRatio);
          sWriter.Write(", " + ent);
          //rg.ControlOutletTemperature(10000, 8000, 1.0, dbt, hrt, 26, 0.0105, 24);
          //rg.ControlOutletHumidity(10000, 8000, 1.0, dbt, hrt, 26, 0.0105, 0.0135);
          rg.ControlOutletEnthalpy(10000, 8000, 1.0, dbt, hrt, 26, 0.0105, 45);
          ent = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio
            (rg.SupplyAirOutletDrybulbTemperature, rg.SupplyAirOutletHumidityRatio);
          sWriter.Write(", "
            + rg.SupplyAirOutletDrybulbTemperature + ", "
            + rg.SupplyAirOutletHumidityRatio + ", " + ent + ","
            + rg.Electricity);

          sWriter.WriteLine();
        }
      }
    }

    public static void AirToAirFlatPlateHeatExchangerTest()
    {
      //初期化処理
      AirToAirFlatPlateHeatExchanger aHex = new AirToAirFlatPlateHeatExchanger
        (150, 150, 34.5, 0.0263, 26.5, 0.014, 0.74, 0.67,
        AirToAirFlatPlateHeatExchanger.AirFlow.CrossFlow, true);

      using (StreamWriter sWriter = new StreamWriter
        ("AToAFlatPlateHeatExchanger.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        //タイトル行
        //流量変更
        for (int i = 0; i <= 7; i++)
          sWriter.Write("," + (150 - (10 * i)));
        sWriter.WriteLine();

        for (int i = 0; i <= 7; i++)
        {
          //排気風量を変更（50%～100%）
          double afEA = (150 - (10 * i));
          sWriter.Write(afEA.ToString());
          for (int j = 0; j <= 7; j++)
          {
            //給気風量を変更（50%～100%）
            double afSA = (150 - (10 * j));
            aHex.UpdateState(afSA, afEA, 34.5, 0.0263, 26.5, 0.014);
            //書き出し処理
            sWriter.Write(", " + aHex.LatentEfficiency.ToString("F3"));
          }
          sWriter.WriteLine();
        }
      }
    }

    #endregion

    #region 空調機テスト

    public static void AirHandlingUnitVAVTest()
    {
      //給気・還気・外気量[m3/s]
      double qsa = 7476d / 3600;
      double qra = 6946d / 3600;
      double qoa = 1513d / 3600;

      //冷水・温水コイル作成
      CrossFinHeatExchanger cCoil = new CrossFinHeatExchanger
        (0.82, 0.910, 6, 24, qsa * 1.2, 27.46, 0.01206, 95, 127d / 60, 127d / 60, 7,
        CrossFinHeatExchanger.WaterFlowType.HalfFlow, 49.6, true);
      CrossFinHeatExchanger hCoil = new CrossFinHeatExchanger
        (0.82, 0.910, 4, 24, qsa * 1.2, 17.46, 0.00554, 95, 105d / 60, 105d / 60, 50,
        CrossFinHeatExchanger.WaterFlowType.HalfFlow, 46.9, true);

      //給気・還気ファン作成
      CentrifugalFan saFan = new CentrifugalFan(0.8, qsa, 0.8, qsa, 3, true);
      CentrifugalFan raFan = new CentrifugalFan(0.3, qra, 0.3, qra, 3, true);
      saFan.MinimumRotationRatio = raFan.MinimumRotationRatio = 0.2;

      //全熱交換器作成
      RotaryRegenerator regenerator = new RotaryRegenerator
        (0.34, qoa * 3600, (qoa + qra - qsa) * 3600, true, 34.4, 0.0194, 26, 0.0105);

      //AHU作成
      AirHandlingUnit ahu = new AirHandlingUnit
        (cCoil, hCoil, AirHandlingUnit.HumidifierType.DropPervaporation, saFan, raFan, regenerator);
      ahu.SetOutdoorAirFlowRange(qoa * 1.2, qsa * 1.2);
      ahu.SetAirFlowRate(qra, qsa * 1.2);
      ahu.MinimizeAirFlow = true;
      ahu.UpperTemperatureLimit_H = 40;
      ahu.LowerTemperatureLimit_H = 30;
      ahu.UpperTemperatureLimit_C = 15;
      ahu.LowerTemperatureLimit_C = 10;

      //運転状態設定
      const double CNV = 1.2 / 3600d; //m3/h→kg/s 換算係数
      bool[] off = new bool[] { false, false, false };
      double[] zTemp = new double[] { 22, 24, 26 };
      double[] zHumid = new double[] { 0.008, 0.008, 0.008 };
      double[] zHLoad = new double[3];
      double cf = 6946d / 7476d;
      double[] maxSA = new double[] { 4151d * CNV, 1782d * CNV, 1543d * CNV };
      double[] maxRA = new double[] { maxSA[0] * cf, maxSA[1] * cf, maxSA[2] * cf };
      double[] minSA = new double[3];
      for (int i = 0; i < minSA.Length; i++) minSA[i] = maxSA[i] * 0.4;

      //冷温水入口温度設定
      ahu.ChilledWaterInletTemperature = 7;
      ahu.HotWaterInletTemperature = 50;

      Console.WriteLine("風量1,風量2,風量3,給気量,給気温度,熱処理,消費電力");
      //冷却運転テスト
      Console.WriteLine("--冷却運転--------------------------------");
      ahu.OATemperature = 34.4;
      ahu.OAHumidityRatio = 0.0194;
      bool suc;
      for (int i = 0; i < 8; i++)
      {
        zHLoad[0] = -16 * (1 - 0.1 * i) * 1000;
        zHLoad[1] = -7 * (1 - 0.1 * i) * 1000;
        zHLoad[2] = -6 * (1 - 0.1 * i) * 1000;
        double[] af = ahu.OptimizeVAV(true, 0, off, zTemp, zHumid, zHLoad, minSA, maxSA, maxRA, out suc);
        Console.WriteLine(
          (af[0] / CNV).ToString("F0") + "," + (af[1] / CNV).ToString("F0") + "," +
          (af[2] / CNV).ToString("F0") + "," + (ahu.SAFlowRate / CNV).ToString("F0") + "," +
          ahu.SATemperature.ToString("F1") + "," + ahu.CoolingCoil.HeatTransfer.ToString("F1") + "," +
          ahu.SupplyAirFan.GetElectricConsumption().ToString("F2"));
      }
      //加熱運転テスト
      Console.WriteLine("--加熱運転--------------------------------");
      ahu.OATemperature = 2.0;
      ahu.OAHumidityRatio = 0.0014;
      for (int i = 0; i < 8; i++)
      {
        zHLoad[0] = 12.1 * (1 - 0.1 * i) * 1000;
        zHLoad[1] = 5.3 * (1 - 0.1 * i) * 1000;
        zHLoad[2] = 4.6 * (1 - 0.1 * i) * 1000;
        double[] af = ahu.OptimizeVAV(false, 0.0105, off, zTemp, zHumid, zHLoad, minSA, maxSA, maxRA, out suc);
        Console.WriteLine(
          (af[0] / CNV).ToString("F0") + "," + (af[1] / CNV).ToString("F0") + "," +
          (af[2] / CNV).ToString("F0") + "," + (ahu.SAFlowRate / CNV).ToString("F0") + "," +
          ahu.SATemperature.ToString("F1") + "," + ahu.HeatingCoil.HeatTransfer.ToString("F1") + "," +
          ahu.SupplyAirFan.GetElectricConsumption().ToString("F2"));
      }
      //外気冷房テスト
      Console.WriteLine("--外気冷房--------------------------------");
      ahu.OATemperature = 10.0;
      ahu.OAHumidityRatio = 0.006;
      ahu.OutdoorAirCooling = AirHandlingUnit.OutdoorAirCoolingControl.DrybulbTemperature;
      for (int i = 0; i < 8; i++)
      {
        zHLoad[0] = -16 * (1 - 0.1 * i) * 1000;
        zHLoad[1] = -7 * (1 - 0.1 * i) * 1000;
        zHLoad[2] = -6 * (1 - 0.1 * i) * 1000;
        double[] af = ahu.OptimizeVAV(true, 0, off, zTemp, zHumid, zHLoad, minSA, maxSA, maxRA, out suc);
        Console.WriteLine(
          (af[0] / CNV).ToString("F0") + "," + (af[1] / CNV).ToString("F0") + "," +
          (af[2] / CNV).ToString("F0") + "," + (ahu.SAFlowRate / CNV).ToString("F0") + "," +
          ahu.SATemperature.ToString("F1") + "," + ahu.CoolingCoil.HeatTransfer.ToString("F1") + "," +
          ahu.SupplyAirFan.GetElectricConsumption().ToString("F2"));
      }
    }

    public static void AirHandlingUnitTest()
    {
      //給気・還気・外気量
      double msa = 7476d / 3600 * 1.2;
      double mra = 6946d / 3600 * 1.2;
      double moa = 1513d / 3600 * 1.2;

      //冷水・温水コイル作成
      CrossFinHeatExchanger cCoil = new CrossFinHeatExchanger
        (0.82, 0.910, 6, 24, msa, 27.46, 0.01206, 95, 127d / 60, 127d / 60, 7,
        CrossFinHeatExchanger.WaterFlowType.HalfFlow, 49.6, true);
      CrossFinHeatExchanger hCoil = new CrossFinHeatExchanger
        (0.82, 0.910, 4, 24, msa, 17.46, 0.00554, 95, 105d / 60, 105d / 60, 50,
        CrossFinHeatExchanger.WaterFlowType.HalfFlow, 46.9, true);

      //給気・還気ファン作成
      CentrifugalFan saFan = new CentrifugalFan(0.4, msa / 1.2, 0.4, msa / 1.2, 3, true);
      CentrifugalFan raFan = new CentrifugalFan(0.2, mra / 1.2, 0.2, mra / 1.2, 3, true);

      //全熱交換器作成
      RotaryRegenerator regenerator = new RotaryRegenerator
        (0.34, moa * 3600 / 1.2, (moa + mra - msa) * 3600 / 1.2, true,
        34.4, 0.0194, 26, 0.0105);

      //AHU作成
      AirHandlingUnit ahu = new AirHandlingUnit
        (cCoil, hCoil, AirHandlingUnit.HumidifierType.DropPervaporation,
        saFan, raFan, regenerator);
      ahu.SetOutdoorAirFlowRange(moa, msa);
      ahu.SetAirFlowRate(mra, msa);

      //冷温水入口温度設定
      ahu.ChilledWaterInletTemperature = 7;
      ahu.HotWaterInletTemperature = 43;

      //還気温湿度設定
      ahu.RATemperature = 26.0;
      ahu.RAHumidityRatio = 0.0105;

      //外気条件リスト
      double[] tOA = new double[30];
      double[] wOA = new double[30];
      double MAX_TOA = tOA[0] = 50;
      double MIN_TOA = tOA[10] = -10;
      double MAX_WOA = wOA[0] = 0.025;
      double MIN_WOA = wOA[10] = 0.003;
      for (int i = 1; i < 10; i++)
      {
        tOA[i] = tOA[20 - i] = MAX_TOA - (MAX_TOA - MIN_TOA) / 10 * i;
        wOA[i] = wOA[30 - i] = MAX_WOA - (MAX_WOA - MIN_WOA) / 10 * i;
      }
      for (int i = 0; i < 10; i++)
      {
        tOA[20 + i] = MAX_TOA;
        wOA[11 + i] = MIN_WOA;
      }

      using (StreamWriter sWriter = new StreamWriter
        ("ahuTest.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        sWriter.Write("外気温度,外気湿度");
        for (int i = 0; i < 6; i++) sWriter.Write(
            ",冷水流量,冷水出温,冷水処理熱,温水流量,温水出温,温水処理熱," +
            "外気量,水消費量,全熱交回収,外気冷房,給気温度,給気湿度");
        sWriter.WriteLine();

        for (int i = 0; i < tOA.Length; i++)
        {
          sWriter.Write(tOA[i] + ", " + wOA[i]);
          ahu.OATemperature = tOA[i];
          ahu.OAHumidityRatio = wOA[i];

          ahu.OutdoorAirCooling = AirHandlingUnit.OutdoorAirCoolingControl.None;
          ahu.BypassRegenerator = true;
          ahu.CoolAir(15, 0);
          outputAHUState(sWriter, ahu);

          ahu.OutdoorAirCooling = AirHandlingUnit.OutdoorAirCoolingControl.None;
          ahu.BypassRegenerator = false;
          ahu.CoolAir(15, 0);
          outputAHUState(sWriter, ahu);

          ahu.OutdoorAirCooling = AirHandlingUnit.OutdoorAirCoolingControl.DrybulbTemperature;
          ahu.BypassRegenerator = true;
          ahu.CoolAir(15, 0);
          outputAHUState(sWriter, ahu);

          ahu.OutdoorAirCooling = AirHandlingUnit.OutdoorAirCoolingControl.DrybulbTemperature;
          ahu.BypassRegenerator = false;
          ahu.CoolAir(15, 0);
          outputAHUState(sWriter, ahu);

          ahu.BypassRegenerator = true;
          ahu.HeatAir(35, 0.010);
          outputAHUState(sWriter, ahu);

          ahu.BypassRegenerator = false;
          ahu.HeatAir(35, 0.010);
          outputAHUState(sWriter, ahu);

          sWriter.WriteLine();
        }
      }
    }

    public static void outputAHUState(StreamWriter sWriter, AirHandlingUnit ahu)
    {
      ImmutableCrossFinHeatExchanger cCoil = ahu.CoolingCoil;
      ImmutableCrossFinHeatExchanger hCoil = ahu.HeatingCoil;
      double recov;
      if (ahu.Regenerator != null) recov = ahu.Regenerator.GetHeatRecovery();
      else recov = 0;
      sWriter.Write(", " +
        cCoil.WaterFlowRate + ", " + cCoil.OutletWaterTemperature + ", " + cCoil.HeatTransfer + ", " +
        hCoil.WaterFlowRate + ", " + hCoil.OutletWaterTemperature + ", " + hCoil.HeatTransfer + ", " +
        ahu.OAFlowRate + ", " + ahu.WaterConsumption + ", " + recov + ", " +
        ahu.GetOutdoorCoolingHeat() + ", " + ahu.SATemperature + ", " + ahu.SAHumidityRatio);
    }

    #endregion

    #region 冷却塔テスト

    private static void CoolingTowerTest1()
    {
      double twb = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio(35, 0.0195, 101.325);
      CoolingTower ct = new CoolingTower(37, 32, twb, 43.3, 35.7, CoolingTower.AirFlowDirection.CrossFlow, 7.5, false);

      using (StreamWriter sWriter = new StreamWriter
        ("CoolingTowerTest.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        //入口冷却水温度に対する感度
        sWriter.WriteLine("冷却水温度[C], 除去熱量[kW], 出口水温[C]");
        double wbt = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio(35, 0.0195, 101.325);
        ct.SetOutdoorAirState(wbt, 0.0195);
        for (int i = 0; i < 10; i++)
        {
          double wit = 37 - i * 0.5;
          ct.Update(wit, false);
          sWriter.WriteLine(wit.ToString("F1") + ", " + ct.HeatRejection.ToString("F1")
            + ", " + ct.OutletWaterTemperature.ToString("F2"));
        }

        //入口空気相対湿度に対する感度
        sWriter.WriteLine("相対湿度[%], 除去熱量[kW], 出口水温[C]");
        for (int i = 0; i < 10; i++)
        {
          double rhd = 55 - 2 * i;
          double hrt = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(35, rhd, 101.325);
          double wbt2 = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio(35, hrt, 101.325);
          ct.SetOutdoorAirState(wbt2, hrt);
          ct.Update(37, false);
          sWriter.WriteLine(rhd.ToString("F0") + ", " + ct.HeatRejection.ToString("F1")
            + ", " + ct.OutletWaterTemperature.ToString("F2"));
        }

        //冷却水流量に対する感度
        sWriter.WriteLine("冷却水流量比[-], 除去熱量[kW], 出口水温[C]");
        ct.SetOutdoorAirState(wbt, 0.0195);
        for (int i = 0; i < 10; i++)
        {
          double pl = (1 - 0.05 * i);
          ct.WaterFlowRate = 43.3 * pl;
          ct.Update(37, false);
          sWriter.WriteLine(pl.ToString("F2") + ", " + ct.HeatRejection.ToString("F1")
            + ", " + ct.OutletWaterTemperature.ToString("F2"));
        }

        //風量に対する感度
        sWriter.WriteLine("風量比[-], 除去熱量[kW], 出口水温[C]");
        ct.WaterFlowRate = 43.3;
        for (int i = 0; i < 10; i++)
        {
          double pl = (1 - 0.05 * i);
          ct.Update(37, ct.MaxAirFlowRate * pl);
          sWriter.WriteLine(pl.ToString("F2") + ", " + ct.HeatRejection.ToString("F1")
            + ", " + ct.OutletWaterTemperature.ToString("F2"));
        }
      }
    }

    private static void CoolingTowerTest2()
    {
      double twb = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio(35, 0.0195, 101.325);
      CoolingTower ct_INV = new CoolingTower
        (37, 32, twb, 43.3, 35.7, CoolingTower.AirFlowDirection.CrossFlow, 7.5, true);
      CoolingTower ct_NON = new CoolingTower
        (37, 32, twb, 43.3, 35.7, CoolingTower.AirFlowDirection.CrossFlow, 7.5, false);

      using (StreamWriter sWriter = new StreamWriter
        ("CoolingTowerTest.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        //負荷とファン消費電力・水消費量との関係
        sWriter.WriteLine
          ("負荷率[-], 風量[kg/s], 消費電力(発停), 消費電力(INV), 蒸発量[kg/s], 飛散量[kg/s], ブロー量[kg/s]");
        for (int i = 0; i < 100; i++)
        {
          //負荷率によって冷却水入口温度を調整
          double pLoad = 1 - i * 0.01;
          double wInletTemp = 32 + 5 * pLoad;

          //状態更新処理
          ct_INV.Update(wInletTemp, true); //INV制御
          ct_NON.Update(wInletTemp, true); //発停制御

          //結果出力
          sWriter.WriteLine(pLoad.ToString("F2") + ", " + ct_INV.AirFlowRate.ToString("F2") + ", "
            + ct_NON.ElectricConsumption.ToString("F2") + ", " + ct_INV.ElectricConsumption.ToString("F2") + ", "
            + ct_INV.EvaporationWater.ToString("F3") + ", " + ct_INV.DriftWater.ToString("F3") + ", "
            + ct_INV.BlowDownWater.ToString("F3"));
        }
      }
    }

    private static void CoolingTowerTest3()
    {
      double twb = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio(35, 0.0195, 101.325);
      CoolingTower ct = new CoolingTower
        (37, 32, twb, 43.3, 35.7, CoolingTower.AirFlowDirection.CrossFlow, 7.5, false);

      using (StreamWriter sWriter = new StreamWriter
        ("CoolingTowerTest.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        //タイトル行
        sWriter.Write(" ");
        for (int j = -5; j <= 35; j++) sWriter.Write("," + j.ToString("F0"));
        sWriter.WriteLine(" ");

        for (int i = 40; i <= 100; i++)
        {
          sWriter.Write(i.ToString("F0"));
          for (int j = -5; j <= 35; j++)
          {
            //外気条件を更新して成り行き出口状態を計算
            double hrti =
              MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(j, i, 101.325);
            double wbti = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio(j, hrti, 101.325);
            ct.SetOutdoorAirState(wbti, hrti);
            ct.Update(37, false);

            //熱交換量から出口空気状態を計算            
            double enth = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(j, hrti)
              + ct.HeatRejection / ct.AirFlowRate;
            double hrto = MoistAir.GetHumidityRatioFromEnthalpyAndRelativeHumidity(enth, 100, 101.325);

            //蒸発潜熱と絶対湿度から潜熱交換量を計算
            double lh = (hrto - hrti) * MoistAir.LatentHeatOfVaporization * ct.AirFlowRate;
            double lhr = lh / ct.HeatRejection;

            sWriter.Write("," + lhr.ToString("F3"));
          }
          sWriter.WriteLine();
        }
      }
    }

    private static void CoolingTowerTest4()
    {
      double twb = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio
        (35, 0.0195, 101.325);
      CoolingTower ct = new CoolingTower
        (37, 32, twb, 43.3, 35.7, CoolingTower.AirFlowDirection.CrossFlow, 7.5, false);
      double wbt = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio(28, 0.010, 101.325);
      ct.SetOutdoorAirState(wbt, 0.010);

      using (StreamWriter sWriter = new StreamWriter
        ("CoolingTowerTest.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        for (int i = 0; i <= 24; i++)
        {
          double cdto = 25 + i * 0.5;
          sWriter.Write(cdto);
          ct.OutletWaterSetPointTemperature = cdto;
          for (int j = 60; j <= 100; j += 5)
          {
            double pl = j / 100d;
            double cdti = cdto + 2.5d / pl;
            ct.WaterFlowRate = 43.3 * pl;
            ct.Update(cdti, true);
            double er = ct.ElectricConsumption / ct.NominalPowerConsumption;
            sWriter.Write("," + er.ToString("F3"));
          }
          sWriter.WriteLine();
        }
      }
    }

    private static void CoolingTowerTest5()
    {
      double wbt = 27;
      CoolingTower ct = new CoolingTower
        (37, 32, wbt, 43.3, 35.7, CoolingTower.AirFlowDirection.CrossFlow, 7.5, false);
      ct.SetOutdoorAirState(wbt, 0.010);

      for (int i = 0; i <= 9; i++)
      {
        double fRatio = 1.0 - 0.1 * i;
        ct.WaterFlowRate = 43.3 * fRatio;
        ct.Update(37, false);
        Console.WriteLine(fRatio + " : " + ct.OutletWaterTemperature + " : " + ct.HeatRejection.ToString("F1"));
      }
    }

    #endregion

  }
}
