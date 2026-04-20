using Popolo.HVAC.Circuit;
using Popolo.HVAC.HeatExchanger;
using Popolo.HVAC.HeatSource;
using Popolo.HVAC.SubSystem;
using Popolo.HVAC.ThermalStorage;
using Popolo.Numerics;
using Popolo.Numerics.MatrixOperation;
using Popolo.ThermophysicalProperty;
using Popolo.Weather;
using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PopoloTester
{
  class HeatSourceTester
  {

    #region 水熱源ヒートポンプテスト

    public static void waterHeatPumpTest1()
    {
      double mcEvpC = 178.3 / 60;
      double mcCndC = 216.7 / 60;
      double mcEvpH = 155.0 / 60;
      double mcCndH = 206.7 / 60;
      double qC = mcEvpC * 5 * 4.186;
      double qH = mcEvpH * 5 * 4.186;
      double[] tcd = new double[] { 20, 30, 40 };
      double[] ths = new double[] { -5, 0, 10, 15, 20, 25 };
      WaterHeatPump whp = new WaterHeatPump(62.4, mcEvpC, mcCndC, 7, 26, 13.3, 72.3, mcCndH, mcEvpH, 45, 12, 18.6);

      using (StreamWriter sWriter = new StreamWriter
        ("waterHeatPumpTest1.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        //タイトル行
        sWriter.Write("負荷率[-]");
        for (int i = 0; i < tcd.Length; i++) sWriter.Write("," + tcd[i] + "C（流量固定：冷却）");
        for (int i = 0; i < tcd.Length; i++) sWriter.Write("," + tcd[i] + "C（流量可変：冷却）");
        for (int i = 0; i < ths.Length; i++) sWriter.Write("," + ths[i] + "C（流量固定：加熱）");
        for (int i = 0; i < ths.Length; i++) sWriter.Write("," + ths[i] + "C（流量可変：加熱）");
        sWriter.WriteLine();

        for (int i = 0; i < 20; i++)
        {
          double pl = 0.05 * (i + 1);
          sWriter.Write(pl);

          //冷却運転
          double ti = 7 + 5 * pl;
          //流量固定
          for (int j = 0; j < tcd.Length; j++)
          {
            whp.CoolWater(mcEvpC, mcCndC, ti, tcd[j]);
            if (whp.IsOverLoad || (whp.CoolingLoad / whp.MaxCoolingCapacity) < 0.5) sWriter.Write(",=na()");
            //else sWriter.Write("," + (qC * pl) / whp.EnergyConsumption);
            else sWriter.Write("," + whp.EnergyConsumption);
          }
          //流量可変
          for (int j = 0; j < tcd.Length; j++)
          {
            whp.CoolWater(mcEvpC, mcCndC * Math.Max(0.5, pl), ti, tcd[j]);
            if (whp.IsOverLoad || (whp.CoolingLoad / whp.MaxCoolingCapacity) < 0.5) sWriter.Write(",=na()");
            //else sWriter.Write("," + (qC * pl) / whp.EnergyConsumption);
            else sWriter.Write("," + whp.EnergyConsumption);
          }

          //加熱運転
          ti = 45 - 5 * pl;
          //流量固定
          for (int j = 0; j < ths.Length; j++)
          {
            whp.HeatWater(mcCndC, mcEvpC, ti, ths[j]);
            if (whp.IsOverLoad || (whp.HeatingLoad / whp.MaxHeatingCapacity) < 0.5) sWriter.Write(",=na()");
            //else sWriter.Write("," + (qH * pl) / whp.EnergyConsumption);
            else sWriter.Write("," + whp.EnergyConsumption);
          }
          //流量可変
          for (int j = 0; j < ths.Length; j++)
          {
            whp.HeatWater(mcCndC, mcEvpC * Math.Max(0.5, pl), ti, ths[j]);
            if (whp.IsOverLoad || (whp.HeatingLoad / whp.MaxHeatingCapacity) < 0.5) sWriter.Write(",=na()");
            //else sWriter.Write("," + (qH * pl) / whp.EnergyConsumption);
            else sWriter.Write("," + whp.EnergyConsumption);
          }

          sWriter.WriteLine();
        }
      }
    }

    public static void waterHeatPumpTest2()
    {
      double MAX_TSCEI = 40;
      double mEvpC = 116d / 60;
      double mCndC = 143d / 60;
      double qC = mEvpC * 5 * 4.186;
      WaterHeatPump whp = new WaterHeatPump
        (qC, mEvpC, mCndC, 17, 32, 9.4, 72.3, 206.7 / 60, 155.0 / 60, 45, 12, 18.6);

      CentrifugalPump pump = new CentrifugalPump
        (400, mCndC / 1000, 399, mCndC / 1000, CentrifugalPump.ControlMethod.MinimumPressure, 0);

      using (StreamWriter sWriter = new StreamWriter
        ("waterHeatPumpTest2.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        //地中温度変化
        for (int tGND = 20; tGND < 40; tGND += 5)
        {
          sWriter.Write("地中温度=" + tGND + "C");
          for (int j = 0; j <= 10; j++) { sWriter.Write("," + (0.5 + j * 0.05)); }
          sWriter.WriteLine();
          //負荷率変化
          for (int i = 0; i <= 50; i++)
          {
            double pl = (0.5 + i * 0.01);
            double tEvpi = 17 + 5 * pl;  //100%で22C
            sWriter.Write(pl);
            //流量変化
            for (int j = 0; j <= 10; j++)
            {
              double mCndRate = 0.5 + j * 0.05; //流量比

              Roots.ErrorFunction eFnc = delegate (double tScei)
              {
                double mc = mCndRate * mCndC;
                whp.CoolWater(mEvpC, mc, tEvpi, tScei);
                double mcc = 4.19 * mc;
                pump.UpdateState(mc * 0.001);
                double eps = 1 - Math.Exp(-12.5 / mcc);
                double qGnd = eps * mcc * (whp.CoolingWaterOutletTemperature - tGND);
                double tScei2 = whp.CoolingWaterOutletTemperature + (pump.GetElectricConsumption() - qGnd) / mcc;
                return tScei2 - tScei;
              };

              if (0 < eFnc(MAX_TSCEI)) sWriter.Write(",=na()");
              else
              {
                double ts = Roots.Bisection(eFnc, tGND, MAX_TSCEI, 1e-5, 1e-5, 30);
                if (whp.IsOverLoad) sWriter.Write(",=na()");
                else
                {
                  double ec = whp.EnergyConsumption + pump.GetElectricConsumption();
                  //sWriter.Write("," + (qC * pl) / ec);
                  sWriter.Write("," + (qC * pl) / whp.EnergyConsumption);
                  //sWriter.Write("," + whp.CoolingWaterOutletTemperature);
                }
              }
            }
            sWriter.WriteLine();
          }
        }
      }
    }

    public static void waterHeatPumpTest3()
    {
      double mcEvpC = 178.3 / 60;
      double mcCndC = 216.7 / 60;
      double mcEvpH = 155.0 / 60;
      double mcCndH = 206.7 / 60;
      double qC = mcEvpC * 5 * 4.186;
      double qH = mcEvpH * 5 * 4.186;
      double[] tcd = new double[] { 25, 27, 29, 31, 33, 35, 37, 39, 41, 43, 45, 47, 49 };
      double[] ths = new double[] { -5, -3, -1, 1, 3, 5, 7, 9, 11, 13, 15, 17, 19, 21 };
      double[] tchSP = new double[] { 5, 15, 25 };
      double[] thwSP = new double[] { 30, 40, 50 };
      double[] mRate = new double[] { 1.0, 0.5 };
      double[] pRate = new double[] { 0.8, 0.5 };
      WaterHeatPump whp = new WaterHeatPump(62.4, mcEvpC, mcCndC, 7, 26, 13.3, 72.3, mcCndH, mcEvpH, 45, 12, 18.6);

      using (StreamWriter sWriter = new StreamWriter
        ("waterHeatPumpTest3.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        //タイトル行
        sWriter.Write("冷却水温度[C]");
        for (int i = 0; i < tchSP.Length; i++) sWriter.Write("," + tchSP[i] + "C（負荷率80%-冷却水流量100%）");
        for (int i = 0; i < tchSP.Length; i++) sWriter.Write("," + tchSP[i] + "C（負荷率80%-冷却水流量50%）");
        for (int i = 0; i < tchSP.Length; i++) sWriter.Write("," + tchSP[i] + "C（負荷率50%-冷却水流量100%）");
        for (int i = 0; i < tchSP.Length; i++) sWriter.Write("," + tchSP[i] + "C（負荷率50%-冷却水流量50%）");
        sWriter.WriteLine();
        //冷却水入口温度変更
        for (int i = 0; i < tcd.Length; i++)
        {
          sWriter.Write(tcd[i]);
          //冷水流量変更
          for (int iSpy = 0; iSpy < pRate.Length; iSpy++)
          {
            //冷却流量変更
            for (int iSce = 0; iSce < mRate.Length; iSce++)
            {
              //冷水出口温度設定値変更
              for (int j = 0; j < tchSP.Length; j++)
              {
                double tchIn = tchSP[j] + pRate[iSpy] * qC / (4.186 * mcEvpC * mRate[iSpy]);
                whp.ChilledWaterSetPoint = tchSP[j];
                whp.CoolWater(mcEvpC * mRate[iSpy], mcCndC * mRate[iSce], tchIn, tcd[i]);
                if (whp.IsOverLoad) sWriter.Write(",=na()");
                else sWriter.Write("," + whp.EnergyConsumption);
              }
            }
          }
          sWriter.WriteLine();
        }

        sWriter.WriteLine();
        //タイトル行
        sWriter.Write("熱源水温度[C]");
        for (int i = 0; i < tchSP.Length; i++) sWriter.Write("," + tchSP[i] + "C（負荷率80%-熱源水流量100%）");
        for (int i = 0; i < tchSP.Length; i++) sWriter.Write("," + tchSP[i] + "C（負荷率80%-熱源水流量50%）");
        for (int i = 0; i < tchSP.Length; i++) sWriter.Write("," + tchSP[i] + "C（負荷率50%-熱源水流量100%）");
        for (int i = 0; i < tchSP.Length; i++) sWriter.Write("," + tchSP[i] + "C（負荷率50%-熱源水流量50%）");
        sWriter.WriteLine();
        //冷却水入口温度変更
        for (int i = 0; i < ths.Length; i++)
        {
          sWriter.Write(ths[i]);
          //冷水流量変更
          for (int iSpy = 0; iSpy < pRate.Length; iSpy++)
          {
            //冷却流量変更
            for (int iSce = 0; iSce < mRate.Length; iSce++)
            {
              //冷水出口温度設定値変更
              for (int j = 0; j < thwSP.Length; j++)
              {
                double thwIn = thwSP[j] - pRate[iSpy] * qH / (4.186 * mcCndH * mRate[iSpy]);
                whp.HotWaterSetPoint = thwSP[j];
                whp.HeatWater(mcCndH * mRate[iSpy], mcEvpH * mRate[iSce], thwIn, ths[i]);
                //if (whp.IsOverLoad || (whp.CoolingLoad / whp.MaxCoolingCapacity) < 0.5) sWriter.Write(",=na()");
                if (whp.IsOverLoad) sWriter.Write(",=na()");
                else sWriter.Write("," + whp.EnergyConsumption);
              }
            }
          }
          sWriter.WriteLine();
        }
      }
    }

    #endregion

    #region ボイラテスト

    public static void HotWaterBoilerTest()
    {
      HotWaterBoiler hBoiler = new HotWaterBoiler
        (50, 70, 710d / 60, 83.6 / 3600, 0, 25, 1.15, Boiler.Fuel.Gas13A, 80);

      using (StreamWriter sWriter = new StreamWriter
        ("HotWaterBoilerTest.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        sWriter.WriteLine
          ("負荷率, 空気比1.1, 空気比1.2, 空気比1.3, 40->60C, 50->70C, 60->80C");
        for (int i = 0; i < 10; i++)
        {
          double pl = 1 - 0.1 * i;
          sWriter.Write(pl);
          double wFlow = (710d / 60) * pl;

          hBoiler.OutletWaterSetPointTemperature = 70;
          for (int j = 0; j < 3; j++)
          {
            hBoiler.AirRatio = 1.1 + 0.1 * j;
            hBoiler.Update(50, wFlow);
            sWriter.Write("," + hBoiler.COP.ToString("F4"));
          }

          hBoiler.AirRatio = 1.15;
          for (int j = 0; j < 3; j++)
          {
            hBoiler.OutletWaterSetPointTemperature = 60 + j * 10;
            hBoiler.Update(40 + j * 10, wFlow);
            sWriter.Write("," + hBoiler.COP.ToString("F4"));
          }
          sWriter.WriteLine();
        }
      }
    }

    public static void SteamBoilerTest()
    {
      SteamBoiler sBoiler = new SteamBoiler(100, 490, 1500d / 3600, 81.7 / 3600, 0, 25, 1.15, Boiler.Fuel.Gas13A, 80);

      double[] sf = new double[3];
      for (int i = 0; i < sf.Length; i++) sf[i] = sBoiler.NominalCapacity
                 / (Water.GetSaturatedVaporEnthalpy(Water.GetSaturationTemperature(490 + 200 * i)) - 418.6);

      using (StreamWriter sWriter = new StreamWriter
        ("SteamBoilerTest.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        sWriter.WriteLine("負荷率, 空気比1.1, 空気比1.2, 空気比1.3, 490kPa, 690kPa, 890kPa");
        for (int i = 0; i < 10; i++)
        {
          double pl = 1.0 - 0.1 * i;
          sWriter.Write(pl);

          sBoiler.SteamPressure = 490;
          double sFlow = (1500d / 3600) * pl;
          for (int j = 0; j < 3; j++)
          {
            sBoiler.AirRatio = 1.1 + 0.1 * j;
            sBoiler.Update(100, sFlow);
            sWriter.Write("," + sBoiler.COP);
          }

          sBoiler.AirRatio = 1.15;
          for (int j = 0; j < 3; j++)
          {
            sBoiler.SteamPressure = 470 + j * 200;
            sBoiler.Update(100, sf[j] * pl);
            sWriter.Write("," + sBoiler.COP);
          }
          sWriter.WriteLine();
        }
      }
    }

    #endregion

    #region 圧縮式冷凍機テスト

    public static void SimpleCentrifugalChillerTest()
    {
      using (StreamWriter sWriter = new StreamWriter
        ("SimpleCentrifugalChillerTest.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        //定格冷水量[kg/s]
        const double NCH_FLOW = 500d / (12 - 7) / 4.186;
        //定格冷却水量[kg/s]
        const double NCD_FLOW = 1670d / 60;

        //固定速機とINV機のインスタンスを生成
        SimpleCentrifugalChiller cR = new SimpleCentrifugalChiller(500d / 6d, 0.2, 12, 7, 37, NCH_FLOW, false);
        SimpleCentrifugalChiller iR = new SimpleCentrifugalChiller(500d / 6d, 0.2, 12, 7, 37, NCH_FLOW, true);
        cR.IsOperating = iR.IsOperating = true;

        sWriter.Write("負荷率");
        for (int i = 0; i < 6; i++) sWriter.Write(",固+固" + (32 - 4 * i));
        for (int i = 0; i < 6; i++) sWriter.Write(",INV+固" + (32 - 4 * i));
        for (int i = 0; i < 6; i++) sWriter.Write(",固+変" + (32 - 4 * i));
        for (int i = 0; i < 6; i++) sWriter.Write(",INV+変" + (32 - 4 * i));
        sWriter.WriteLine();
        for (int i = 0; i < 10; i++)
        {
          //負荷率[-]
          double pl = 1.0 - 0.1 * i;
          sWriter.Write(pl);

          //固定速機・冷却水量固定
          for (int j = 0; j < 6; j++)
          {
            cR.Update(32 - 4 * j, 12, NCD_FLOW, NCH_FLOW * pl);
            sWriter.Write(", " + cR.COP.ToString("F2"));
          }
          //INV機・冷却水量固定
          for (int j = 0; j < 6; j++)
          {
            iR.Update(32 - 4 * j, 12, NCD_FLOW, NCH_FLOW * pl);
            sWriter.Write(", " + iR.COP.ToString("F2"));
          }
          //固定速機・冷却水量変動
          for (int j = 0; j < 6; j++)
          {
            cR.Update(32 - 4 * j, 12, NCD_FLOW * pl, NCH_FLOW * pl);
            sWriter.Write(", " + cR.COP.ToString("F2"));
          }
          //INV機・冷却水量変動
          for (int j = 0; j < 6; j++)
          {
            iR.Update(32 - 4 * j, 12, NCD_FLOW * pl, NCH_FLOW * pl);
            sWriter.Write(", " + iR.COP.ToString("F2"));
          }
          sWriter.WriteLine();
        }
      }
    }

    public static void DetailedCentrifugalInverterChillerTest()
    {
      using (StreamWriter sWriter = new StreamWriter
        ("DetailedCentrifugalInverterChillerTest.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        //インスタンスを生成
        DetailedCentrifugalInverterChiller iR =
          new DetailedCentrifugalInverterChiller(178.8, 0.2, 12, 7, 32, 3023d / 60d, 3567d / 60d);
        iR.IsOperating = true;

        const double NCH_FLOW = 3023d / 60d;  //定格冷水量[kg/s]        
        const double NCD_FLOW = 3567d / 60d;  //定格冷却水量[kg/s]

        sWriter.Write("負荷率");
        for (int i = 0; i < 6; i++) sWriter.Write(",INV+固" + (32 - 4 * i));
        for (int i = 0; i < 6; i++) sWriter.Write(",INV+変" + (32 - 4 * i));
        sWriter.WriteLine();
        for (int i = 0; i < 10; i++)
        {
          //負荷率[-]
          double pl = 1.0 - 0.1 * i;
          sWriter.Write(pl);

          //冷却水量固定
          for (int j = 0; j < 6; j++)
          {
            iR.Update(32 - 4 * j, 12, NCD_FLOW, NCH_FLOW * pl);
            sWriter.Write(", " + iR.COP.ToString("F2"));
          }
          //冷却水量変動
          for (int j = 0; j < 6; j++)
          {
            iR.Update(32 - 4 * j, 12, NCD_FLOW * pl, NCH_FLOW * pl);
            sWriter.Write(", " + iR.COP.ToString("F2"));
          }
          sWriter.WriteLine();
        }
      }
    }

    public static void DetailedCentrifugalInverterChillerTest1()
    {
      using (StreamReader sReader = new StreamReader("input.csv"))
      using (StreamWriter sWriter = new StreamWriter
        ("DetailedCentrifugalInverterChillerTest1.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        //INV機のインスタンスを生成
        DetailedCentrifugalInverterChiller iR =
          new DetailedCentrifugalInverterChiller(178.8, 0.2, 12, 7, 32, 3023d / 60d, 3567d / 60d);

        string buff;
        while ((buff = sReader.ReadLine()) != null)
        {
          string[] bf = buff.Split(',');
          double mcd = double.Parse(bf[0]) / 60d;
          double mch = double.Parse(bf[2]) / 60d;
          double tcd = double.Parse(bf[1]);
          double tch = double.Parse(bf[3]);
          iR.ChilledWaterOutletSetPointTemperature = tch - 5;
          iR.Update(tcd, tch, mch, mcd);
          sWriter.WriteLine(iR.COP.ToString("F2"));
        }
      }
    }

    public static void DetailedCentrifugalInverterChillerTest2()
    {
      //ETI-Z40用パラメータ
      DetailedCentrifugalInverterChiller.Parameters param = new DetailedCentrifugalInverterChiller.Parameters
        (0.734, 0.092, 1.746, -3.976, 3.615, -0.047, 0.027);

      const double NCH_FLOW = 4022d / 60d;  //定格冷水量[kg/s]        
      const double NCD_FLOW = 4693d / 60d;  //定格冷却水量[kg/s]

      //インスタンスを生成
      DetailedCentrifugalInverterChiller iR =
        new DetailedCentrifugalInverterChiller(250.5, 0.1, 12, 7, 32, NCH_FLOW, NCD_FLOW, param);
      iR.IsOperating = true;

      Console.Write("負荷率");
      for (int i = 0; i < 6; i++) Console.Write(",INV+固" + (32 - 4 * i));
      for (int i = 0; i < 6; i++) Console.Write(",INV+変" + (32 - 4 * i));
      Console.WriteLine();
      for (int i = 0; i < 10; i++)
      {
        //負荷率[-]
        double pl = 1.0 - 0.1 * i;
        Console.Write(pl);

        //冷却水量固定
        for (int j = 0; j < 6; j++)
        {
          iR.Update(32 - 4 * j, 12, NCD_FLOW, NCH_FLOW * pl);
          Console.Write(", " + iR.COP.ToString("F2"));
        }
        //冷却水量変動
        for (int j = 0; j < 6; j++)
        {
          iR.Update(32 - 4 * j, 12, NCD_FLOW * pl, NCH_FLOW * pl);
          Console.Write(", " + iR.COP.ToString("F2"));
        }
        Console.WriteLine();
      }
    }

    #endregion

    #region 空気熱源ヒートポンプテスト

    public static void AirSourceHeatPumpTest()
    {
      //空気熱源HPのインスタンスを生成
      AirHeatSourceModularChillers ahp = new AirHeatSourceModularChillers
        (150, 7, 430d / 60, 35, 850d / 60 * 1.2, 49.8, 150, 45, 430d / 60, 7, 850d / 60 * 1.2, 50.0, 3, 1.9);
      ahp.MaximizeEfficiency = true;
      ahp.MinimumPartialLoadRate = 0.2;

      //タイトル行
      for (int i = 0; i <= 30; i++) Console.Write(", " + (10 * i).ToString("F0"));
      Console.WriteLine();

      //冷房運転
      Console.WriteLine("Cooling");
      ahp.Mode = AirHeatSourceModularChillers.OperatingMode.Cooling;
      ahp.WaterOutletSetPointTemperature = 7;
      double[] tai = new double[] { 20, 25, 30, 35 };
      for (int i = 0; i < tai.Length; i++)
      {
        Console.Write(tai[i]);
        for (int j = 0; j <= 30; j++)
        {
          double pl = 0.1 * j;
          double mw = 430d * 3 / 60;
          double twi = 7 + (150d * pl) / (4.186 * mw);
          ahp.Update(twi, mw, tai[i]);
          Console.Write(", " + ahp.COP.ToString("F3"));
        }
        Console.WriteLine();
      }

      //暖房運転
      Console.WriteLine("Heating");
      ahp.Mode = AirHeatSourceModularChillers.OperatingMode.Heating;
      ahp.WaterOutletSetPointTemperature = 45;
      tai = new double[] { 20, 15, 10, 7 };
      for (int i = 0; i < tai.Length; i++)
      {
        Console.Write(tai[i]);
        for (int j = 0; j <= 30; j++)
        {
          double pl = 0.1 * j;
          double mw = 430d * 3 / 60;
          double twi = 45 - (150d * pl) / (4.186 * mw);
          ahp.Update(twi, mw, tai[i]);
          Console.Write(", " + ahp.COP.ToString("F3"));
        }
        Console.WriteLine();
      }
    }

    #endregion

    #region 吸収式冷凍機テスト

    public static void HotWaterAbsorptionChillerTest()
    {
      HotWaterAbsorptionChiller ar = new HotWaterAbsorptionChiller
        (12.5, 7, 274.9 / 60, 31, 35, 918d / 60, 88, 83, 432d / 60);

      using (StreamWriter sWriter = new StreamWriter
        ("HotWaterAbsorptionChillerTest.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        sWriter.WriteLine("負荷率・冷却水温度・COPの関係");
        sWriter.WriteLine("負荷率, 31C, 28C, 25C, 22C");
        for (int i = 0; i < 13; i++)
        {
          double pl = (1 - 0.05 * i);
          double fl = (274.9 / 60) * pl;

          sWriter.Write((pl * 100).ToString("F0"));
          for (int j = 0; j < 4; j++)
          {
            ar.Update(12.5, fl, 31 - j * 3, 918d / 60, 88, 432d / 60);
            sWriter.Write("," + ar.COP.ToString("F3"));
          }
          sWriter.WriteLine();
        }

        sWriter.WriteLine();
        sWriter.WriteLine("温水温度・温水流量・処理熱量の関係");
        sWriter.WriteLine("温水温度, 100%, 75%, 50%");
        ar.ChilledWaterOutletSetPointTemperature = 0;
        for (int i = 0; i < 26; i++)
        {
          double thw = 70 + i;
          sWriter.Write(thw.ToString("F0"));
          for (int j = 0; j < 3; j++)
          {
            ar.Update(12.5, 274.9 / 60, 31, 918d / 60, thw, 432d / 60 * (1 - 0.25 * j));
            sWriter.Write("," + ar.CoolingLoad.ToString("F1"));
          }
          sWriter.WriteLine();
        }
      }
    }

    private static void DirectFiredAbsorptionChillerTest1()
    {
      DirectFiredAbsorptionChiller ar = new DirectFiredAbsorptionChiller(103d / 3600, 103d / 3600,
        15, 7, 32, 37, 54.7, 60, 189 / 3.6, 500 / 3.6, 189 / 3.6, 0, Boiler.Fuel.Gas13A);

      using (StreamWriter sWriter = new StreamWriter
        ("DirectFiredAbsorptionChillerTest.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        sWriter.WriteLine("負荷率,COP(INV),COP(定速)");

        for (int i = 0; i < 19; i++)
        {
          //JIS B8622 条件
          double pl = (1 - 0.05 * i);
          double tcd = 27 + 5 * pl;
          double tch = 7 + 8 * pl;

          sWriter.Write((pl * 100).ToString("F0"));

          ar.HasSolutionInverterPump = true;
          ar.Update(tcd, tch, 500 / 3.6, 189 / 3.6);
          sWriter.Write("," + ar.COP.ToString("F3"));

          ar.HasSolutionInverterPump = false;
          ar.Update(tcd, tch, 500 / 3.6, 189 / 3.6);
          sWriter.Write("," + ar.COP.ToString("F3"));

          sWriter.WriteLine();
        }
      }
    }

    private static void DirectFiredAbsorptionChillerTest2()
    {
      DirectFiredAbsorptionChiller ar = new DirectFiredAbsorptionChiller(103d / 3600, 103d / 3600,
        15, 7, 32, 37, 54.7, 60, 189 / 3.6, 500 / 3.6, 189 / 3.6, 0, Boiler.Fuel.Gas13A);
      ar.HasSolutionInverterPump = true;

      using (StreamWriter sWriter = new StreamWriter
        ("DirectFiredAbsorptionChillerTest.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        sWriter.WriteLine("条件,蒸発温度,凝縮温度,再生温度,COP,稀溶液濃度,濃溶液濃度");

        sWriter.Write("定格性能,");
        ar.Update(32, 15, 500 / 3.6, 189 / 3.6);
        sWriter.WriteLine(ar.EvaporatingTemperature + "," + ar.CondensingTemperature + "," + ar.DesorbTemperature
          + "," + ar.COP + "," + ar.ThinSolutionMassFraction + "," + ar.ThickSolutionMassFraction);

        sWriter.Write("負荷率50%,");
        ar.Update(32, 11, 500 / 3.6, 189 / 3.6);
        sWriter.WriteLine(ar.EvaporatingTemperature + "," + ar.CondensingTemperature + "," + ar.DesorbTemperature
          + "," + ar.COP + "," + ar.ThinSolutionMassFraction + "," + ar.ThickSolutionMassFraction);

        sWriter.Write("冷却水温度25度,");
        ar.Update(25, 15, 500 / 3.6, 189 / 3.6);
        sWriter.WriteLine(ar.EvaporatingTemperature + "," + ar.CondensingTemperature + "," + ar.DesorbTemperature
          + "," + ar.COP + "," + ar.ThinSolutionMassFraction + "," + ar.ThickSolutionMassFraction);

        sWriter.Write("冷水流量50%,");
        ar.Update(32, 15, 500 / 3.6, 189 / 3.6 * 0.5);
        sWriter.WriteLine(ar.EvaporatingTemperature + "," + ar.CondensingTemperature + "," + ar.DesorbTemperature
          + "," + ar.COP + "," + ar.ThinSolutionMassFraction + "," + ar.ThickSolutionMassFraction);

        sWriter.Write("冷水出口温度10度,");
        ar.OutletWaterSetPointTemperature = 10;
        ar.Update(32, 18, 500 / 3.6, 189 / 3.6);
        sWriter.WriteLine(ar.EvaporatingTemperature + "," + ar.CondensingTemperature + "," + ar.DesorbTemperature
          + "," + ar.COP + "," + ar.ThinSolutionMassFraction + "," + ar.ThickSolutionMassFraction);
      }
    }

    private static void AbsorptionChillerTest3_sub
      (DirectFiredAbsorptionChiller ar, StreamWriter sWriter)
    {
      double tv = LithiumBromide.GetVaporTemperatureFromLiquidTemperatureAndMassFraction
        (ar.DesorbTemperature + 273.15, ar.ThickSolutionMassFraction) - 273.15;
      double tl = LithiumBromide.GetLiquidTemperatureFromVaporTemperatureAndMassFraction
        (tv + 273.15, ar.ThinSolutionMassFraction) - 273.15;
      sWriter.WriteLine(tl + "," + tv);
      sWriter.WriteLine(tv + "," + tv);
      sWriter.WriteLine(ar.CondensingTemperature + "," + ar.CondensingTemperature);
      sWriter.WriteLine(ar.EvaporatingTemperature + "," + ar.EvaporatingTemperature);
      sWriter.WriteLine(ar.CondensingTemperature + "," + ar.EvaporatingTemperature);
      tl = LithiumBromide.GetLiquidTemperatureFromVaporTemperatureAndMassFraction
         (ar.CondensingTemperature + 273.15, ar.ThinSolutionMassFraction) - 273.15;
      sWriter.WriteLine(tl + "," + ar.CondensingTemperature);
      tl = LithiumBromide.GetLiquidTemperatureFromVaporTemperatureAndMassFraction
         (tv + 273.15, ar.ThinSolutionMassFraction) - 273.15;
      sWriter.WriteLine(tl + "," + tv);
      sWriter.WriteLine(ar.DesorbTemperature + "," + tv);
      tl = LithiumBromide.GetLiquidTemperatureFromVaporTemperatureAndMassFraction
         (ar.CondensingTemperature + 273.15, ar.ThickSolutionMassFraction) - 273.15;
      sWriter.WriteLine(tl + "," + ar.CondensingTemperature);
      tl = LithiumBromide.GetLiquidTemperatureFromVaporTemperatureAndMassFraction
         (ar.EvaporatingTemperature + 273.15, ar.ThickSolutionMassFraction) - 273.15;
      sWriter.WriteLine(tl + "," + ar.EvaporatingTemperature);
      sWriter.WriteLine(ar.COP);
    }

    private static void AbsorptionChillerTest5()
    {
      MersenneTwister mt = new MersenneTwister(1);

      DirectFiredAbsorptionChiller ar1 = new DirectFiredAbsorptionChiller
        (103d / 3600, 103d / 3600, 15, 7, 32, 37, 54.7, 60, 189 / 3.6, 500 / 3.6, 189 / 3.6, 0, Boiler.Fuel.Gas13A);
      for (int i = 0; i < 1000; i++)
      {
        double pl = mt.NextDouble();
        double tcdi = 27 + 5 * mt.NextDouble();
        double wfl = pl + (1 - pl) * mt.NextDouble();
        ar1.OutletWaterSetPointTemperature = 6 + 9 * mt.NextDouble();
        double tchi = ar1.OutletWaterSetPointTemperature + 8 * pl / wfl;
        ar1.Update(tcdi, tchi, 500 / 3.6 * pl, 189 / 3.6 * wfl);
        Console.WriteLine(ar1.COP.ToString("F2"));
      }

      ar1.IsCoolingMode = false;
      for (int i = 0; i < 1000; i++)
      {
        double pl = mt.NextDouble();
        double wfl = pl + (1 - pl) * mt.NextDouble();
        ar1.OutletWaterSetPointTemperature = 60 - 6.3 * mt.NextDouble();
        double hi = ar1.OutletWaterSetPointTemperature - 5.3 * pl / wfl;
        ar1.Update(32, hi, 500 / 3.6 * pl, 189 / 3.6 * wfl);
        Console.WriteLine(ar1.COP.ToString("F2"));
      }

      HotWaterAbsorptionChiller ar0 = new HotWaterAbsorptionChiller
        (12.5, 7, 274.9 / 60, 31, 35, 918d / 60, 88, 83, 432d / 60);
      for (int i = 0; i < 1000; i++)
      {
        double pl = mt.NextDouble();
        double tcdi = 27 + 5 * mt.NextDouble();
        double wfl = pl + (1 - pl) * mt.NextDouble();
        ar0.ChilledWaterOutletSetPointTemperature = 6 + 4 * mt.NextDouble();
        double tchi = ar0.ChilledWaterOutletSetPointTemperature + 8 * pl / wfl;
        double thi = 82 + mt.NextDouble() * 8d;
        ar0.Update(tchi, 274.9 / 60 * wfl, tcdi, 918d / 60 * pl, thi, 432d / 60);
        Console.WriteLine(ar0.COP.ToString("F2"));
      }
    }

    #endregion

    #region 吸着式テスト

    public static void adsorptionChillerTest2()
    {
      AdsorptionChiller adr1 = new AdsorptionChiller
      // (21, 14, 11.1 / 3.6, 30, 35, 49.5 / 3.6, 70, 64.5, 30 / 3.6);
      //(21, 14, 11.1 / 3.6, 30, 34.7, 49.5 / 3.6, 60, 55, 30 / 3.6);
        (21, 14, 11.1 / 3.6, 29, 33.7, 49.5 / 3.6, 60, 55, 30 / 3.6);
      adr1.ChilledWaterOutletSetPointTemperature = 0;

      //adr1.Update(21, 11.1 / 3.6, 29, 49.5 / 3.6, 60, 30 / 3.6);

      using (StreamReader sReader = new StreamReader("inputs.csv"))
      using (StreamWriter sWriter = new StreamWriter("outputs.csv"))
      {
        string buff;
        sReader.ReadLine();//タイトル行
        sWriter.WriteLine("Tcdo,Tho,Tcho,Qch,COP,wsad0,wsadt");
        while ((buff = sReader.ReadLine()) != null)
        {
          string[] buff2 = buff.Split(',');
          double tcdi = double.Parse(buff2[0]);
          double thi = double.Parse(buff2[2]);
          double tchi = double.Parse(buff2[4]);
          double mcd = double.Parse(buff2[6]) / 60d;
          double mh = double.Parse(buff2[7]) / 60d;
          double mch = double.Parse(buff2[8]) / 60d;

          adr1.Update(tchi, mch, tcdi, mcd, thi, mh);
          sWriter.WriteLine(
            adr1.CoolingWaterOutletTemperature + "," +
            adr1.HotWaterOutletTemperature + "," +
            adr1.ChilledWaterOutletTemperature + "," +
            adr1.CoolingLoad + "," + adr1.COP + "," + adr1.WaterContent_Desorption + "," + adr1.WaterContent_Adsorption);
        }
      }
    }

    public static void adsorptionChillerTest()
    {
      double mchw = 54d / 60;
      double mcdw = 127d / 60d;
      double mhw = 64.5 / 60;
      double cop = 0.47;
      double qch = 8.8;
      double qh = qch / cop;
      double qcd = qch + qh;
      double tchi = 22;
      double thi = 55;
      double tcdi = 27;
      double tcho = tchi - qch / (4.186 * mchw);
      double tho = thi - qh / (4.186 * mhw);
      double tcdo = tcdi + qcd / (4.186 * mcdw);
      AdsorptionChiller ads = new AdsorptionChiller(tchi, tcho, mchw, tcdi, tcdo, mcdw, thi, tho, mhw);

      //ads.CyclingTimeRate = 3.0;
      ads.Update(tchi, mchw, tcdi, mcdw, thi, mhw);

      ads.Update(tchi, mchw, 27, mcdw, 45, mhw);

      using (StreamWriter sWriter = new StreamWriter
        ("adsorptionChillerTest.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {

        sWriter.WriteLine("温水入口温度, 冷水流量比[-], COP, 冷凍能力, 吸着温度, 脱着温度, 冷水出口温度, 吸着量（吸着時）, 吸着量（脱着時）");
        for (int thw = 45; thw <= 55; thw += 2)
        {
          for (int i = 40; i <= 100; i += 5)
          {
            double rt = i / 100d;
            ads.Update(22, mchw * rt, 27, mcdw, thw, mhw);
            sWriter.WriteLine(thw + ", " + rt + ", " + ads.COP.ToString("F3") + ", " + ads.CoolingLoad.ToString("F3") + ", " +
              +ads.CoolingWaterOutletTemperature + ", " + ads.HotWaterOutletTemperature + ", " + ads.ChilledWaterOutletTemperature + ", "
              + ads.WaterContent_Adsorption + ", " + ads.WaterContent_Desorption);
          }
        }

        sWriter.WriteLine("温水入口温度, 冷水入口温度, COP, 冷凍能力, 吸着温度, 脱着温度, 冷水出口温度, 吸着量（吸着時）, 吸着量（脱着時）");
        for (int thw = 45; thw <= 55; thw += 2)
        {
          for (int i = 20; i <= 28; i++)
          {
            ads.Update(i, mchw, 27, mcdw, thw, mhw);
            sWriter.WriteLine(thw + ", " + i + ", " + ads.COP.ToString("F3") + ", " + ads.CoolingLoad.ToString("F3") + ", " +
              +ads.CoolingWaterOutletTemperature + ", " + ads.HotWaterOutletTemperature + ", " + ads.ChilledWaterOutletTemperature + ", "
              + ads.WaterContent_Adsorption + ", " + ads.WaterContent_Desorption);
          }
        }

        sWriter.WriteLine("温水入口温度, 温水流量, COP, 冷凍能力, 吸着温度, 脱着温度, 冷水出口温度, 吸着量（吸着時）, 吸着量（脱着時）");
        for (int thw = 45; thw <= 55; thw += 2)
        {
          for (int i = 40; i <= 100; i += 5)
          {
            double rt = i / 100d;
            ads.Update(22, mchw, 27, mcdw, thw, mhw * rt);
            sWriter.WriteLine(thw + ", " + rt + ", " + ads.COP.ToString("F3") + ", " + ads.CoolingLoad.ToString("F3") + ", " +
              +ads.CoolingWaterOutletTemperature + ", " + ads.HotWaterOutletTemperature + ", " + ads.ChilledWaterOutletTemperature + ", "
              + ads.WaterContent_Adsorption + ", " + ads.WaterContent_Desorption);
          }
        }

        sWriter.WriteLine("冷却水入口温度, 温水入口温度, COP, 冷凍能力, 吸着温度, 脱着温度, 冷水出口温度, 吸着量（吸着時）, 吸着量（脱着時）");
        for (int i = 24; i <= 33; i += 3)
        {
          for (int thw = 45; thw <= 60; thw++)
          {
            ads.Update(22, mchw, i, mcdw, thw, mhw);
            sWriter.WriteLine(i + ", " + thw + ", " + ads.COP.ToString("F3") + ", " + ads.CoolingLoad.ToString("F3") + ", " +
              +ads.CoolingWaterOutletTemperature + ", " + ads.HotWaterOutletTemperature + ", " + ads.ChilledWaterOutletTemperature + ", "
              + ads.WaterContent_Adsorption + ", " + ads.WaterContent_Desorption);
          }
        }

        sWriter.WriteLine("温水入口温度, 冷却水流量, COP, 冷凍能力, 吸着温度, 脱着温度, 冷水出口温度, 吸着量（吸着時）, 吸着量（脱着時）");
        for (int thw = 45; thw <= 55; thw += 2)
        {
          for (int i = 40; i <= 100; i += 5)
          {
            double rt = i / 100d;
            ads.Update(22, mchw, 27, mcdw * rt, thw, mhw);
            sWriter.WriteLine(thw + ", " + rt + ", " + ads.COP.ToString("F3") + ", " + ads.CoolingLoad.ToString("F3") + ", " +
              +ads.CoolingWaterOutletTemperature + ", " + ads.HotWaterOutletTemperature + ", " + ads.ChilledWaterOutletTemperature + ", "
              + ads.WaterContent_Adsorption + ", " + ads.WaterContent_Desorption);
          }
        }

        sWriter.WriteLine("温水入口温度, 冷却水入口温度, COP, 冷凍能力, 吸着温度, 脱着温度, 冷水出口温度, 吸着量（吸着時）, 吸着量（脱着時）");
        for (int thw = 55; thw <= 65; thw += 5)
        {
          for (int i = 24; i <= 32; i++)
          {
            ads.Update(22, mchw, i, mcdw, thw, mhw);
            sWriter.WriteLine(thw + ", " + i + ", " + ads.COP.ToString("F3") + ", " + ads.CoolingLoad.ToString("F3") + ", " +
              +ads.CoolingWaterOutletTemperature + ", " + ads.HotWaterOutletTemperature + ", " + ads.ChilledWaterOutletTemperature + ", "
              + ads.WaterContent_Adsorption + ", " + ads.WaterContent_Desorption);
          }
        }

        sWriter.WriteLine("温水入口温度, サイクル周期比率, COP, 冷凍能力, 吸着温度, 脱着温度, 冷水出口温度, 吸着量（吸着時）, 吸着量（脱着時）");
        for (int thw = 45; thw <= 55; thw += 2)
        {
          for (int i = 2; i <= 10; i++)
          {
            ads.CyclingTimeRate = i * 0.5;
            ads.Update(22, mchw, 27, mcdw, thw, mhw);
            sWriter.WriteLine(thw + ", " + ads.CyclingTimeRate + ", " + ads.COP.ToString("F3") + ", " + ads.CoolingLoad.ToString("F3") + ", " +
              +ads.CoolingWaterOutletTemperature + ", " + ads.HotWaterOutletTemperature + ", " + ads.ChilledWaterOutletTemperature + ", "
              + ads.WaterContent_Adsorption + ", " + ads.WaterContent_Desorption);
          }
        }
      }
    }

    #endregion

    #region 地中熱交換器テスト//このモデルは問題あり。HVAC.HeatExchanger.SimpleGroundHeatExchangerを使え

    public static void groundHeatSourceTest1()
    {
      //土壌を作成
      GroundHeatSource ghs = new GroundHeatSource(5, 20, 0.2, 1, 1, 5, 40, 23, 1, 2300000);
      ghs.InitializeTemperature(20);
      ghs.TimeStep = 3600;

      double[] dbt, hrt, rad;
      bool[] fcf;
      RandomWeather wRnd = new RandomWeather(0, RandomWeather.Location.Tokyo);
      wRnd.MakeWeather(5, out dbt, out hrt, out rad, out fcf);
      Sun sun = new Sun(35.7, 139.8, 135);
      DateTime dTime = new DateTime(1999, 1, 1, 0, 0, 0);
      using (StreamWriter sWriter = new StreamWriter
        ("groundHeatSourceTest1.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        for (int i = 0; i < dbt.Length; i++)
        {
          //相当温度を計算・設定
          sun.Update(dTime);
          sun.SeparateGlobalHorizontalRadiation(rad[i], Sun.SeparationMethod.Erbs);
          ghs.SolAirTemperature = dbt[i] + 0.8 * sun.GlobalHorizontalRadiation / 23.0;

          //土壌温度を更新
          ghs.Update();

          //月初めに書き出し
          if (dTime.Day == 1 && dTime.Hour == 0)
          {
            Console.WriteLine(dTime.ToShortDateString());

            sWriter.Write(dTime.ToShortDateString());
            for (int j = 0; j < ghs.VerticalSplitNumber; j++) sWriter.Write("," + ghs.GetTemperature(0, j));
            sWriter.WriteLine();
          }
          dTime = dTime.AddHours(1);
        }
      }
    }

    public static void groundHeatSourceTest2()
    {
      //土壌を作成
      GroundHeatSource ghs = new GroundHeatSource(5, 150, 0.0875, 118, 2, 15, 31, 23, 1.53, 3030000);
      ghs.InitializeTemperature(19);
      ghs.TimeStep = 3600;
      ghs.SetLength(0, 0.01);
      ghs.SetLength(1, 0.02);
      ghs.SetLength(2, 0.03);
      ghs.SetLength(3, 0.05);
      ghs.SetLength(4, 0.10);
      ghs.SetLength(5, 0.15);
      ghs.SetLength(6, 0.20);
      ghs.SetLength(7, 0.20);
      ghs.SetLength(8, 0.30);
      ghs.SetLength(9, 0.30);
      ghs.SetLength(10, 0.50);
      ghs.SetLength(11, 0.50);
      ghs.SetLength(12, 0.50);
      ghs.SetLength(13, 1.00);
      ghs.SetLength(14, 1.14);
      ghs.SetDepth(0, 2);
      ghs.SetDepth(1, 3);
      for (int i = 2; i < ghs.VerticalSplitNumber; i++) ghs.SetDepth(i, 5);

      double[] dbt, hrt, rad;
      bool[] fcf;
      RandomWeather wRnd = new RandomWeather(0, RandomWeather.Location.Fukuoka);
      wRnd.MakeWeather(1, out dbt, out hrt, out rad, out fcf);
      Sun sun = new Sun(33.6, 130.4, 135);
      DateTime dTime = new DateTime(1999, 1, 1, 0, 0, 0);
      using (StreamWriter sWriter = new StreamWriter
        ("groundHeatSourceTest2.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        for (int i = 0; i < dbt.Length; i++)
        {
          if (dTime.Hour == 0) Console.WriteLine(dTime.ToShortDateString());

          //相当温度を計算・設定
          sun.Update(dTime);
          sun.SeparateGlobalHorizontalRadiation(rad[i], Sun.SeparationMethod.Erbs);
          ghs.SolAirTemperature = dbt[i] + 0.8 * sun.GlobalHorizontalRadiation / 23.0;

          //サーマルレスポンス試験
          if (dTime.Year == 1999 && dTime.Month == 4 && dTime.Day == 3 && dTime.Hour == 0)
            for (int j = 1; j < 25; j++) ghs.SetHeatFlow(j, 100);
          else if (dTime.Year == 1999 && dTime.Month == 4 && dTime.Day == 6 && dTime.Hour == 0)
            for (int j = 1; j < 25; j++) ghs.SetHeatFlow(j, 0);
          else if (dTime.Year == 1999 && dTime.Month == 4 && dTime.Day == 10 && dTime.Hour == 0)
            return;

          //土壌温度を更新
          ghs.Update();

          //書き出し
          if (dTime.Year == 1999 && dTime.Month == 4)
          {
            sWriter.Write(dTime.ToShortDateString() + " " + dTime.ToShortTimeString());
            for (int j = 0; j < ghs.VerticalSplitNumber; j++) sWriter.Write("," + ghs.GetTemperature(0, j));
            sWriter.WriteLine();
          }
          dTime = dTime.AddHours(1);
        }
      }
    }

    public static void groundHeatSourceTest3()
    {
      //土壌を作成
      GroundHeatSource ghs = new GroundHeatSource(2, 1, 1, 1, 0, 5, 2, 20, 99999, 3030000);
      ghs.InitializeTemperature(20);
      ghs.TimeStep = 60;

      /*ghs.SetLength(0, 0.01);
      ghs.SetLength(1, 0.01);
      ghs.SetLength(2, 0.08);
      ghs.SetLength(3, 0.10);
      ghs.SetLength(4, 0.10);
      ghs.SetLength(5, 0.10);
      ghs.SetLength(6, 0.10);
      ghs.SetLength(7, 0.10);
      ghs.SetLength(8, 0.10);
      ghs.SetLength(9, 0.10);
      ghs.SetLength(10, 0.10);
      ghs.SetLength(11, 1.10);*/
      //ghs.SetLength(3, 0.00010);

      /*ghs.SetLength(0, 0.1000);
      ghs.SetLength(1, 0.1000);
      ghs.SetLength(2, 0.6990);
      ghs.SetLength(3, 0.0010);
      ghs.SetDepth(0, 0.9990);
      ghs.SetDepth(1, 0.0005);
      ghs.SetDepth(2, 0.0005);*/
      ghs.SetDepth(0, 0.9);
      ghs.SetDepth(1, 0.1);
      ghs.SolAirTemperature = 30;
      Console.WriteLine
        ((ghs.SolAirTemperature - 20) * ghs.HeatTransferCoefficient
        * (ghs.GroundRadius * ghs.GroundRadius - ghs.PileRadius * ghs.PileRadius) * Math.PI * 0.0036 + " MJ");

      DateTime dTime = new DateTime(1999, 1, 1, 0, 0, 0);

      //100W/m2で100時間加熱=100W/m2×1m×100h*2*3.14=62800Wh    *0.0036= 226.8MJ
      //for (int i = 0; i < ghs.VerticalSplitNumber; i++) ghs.SetHeatFlow(i, 100);
      double buff = ghs.GetHeatStorage(20);
      Console.WriteLine(ghs.GetHeatStorage(20));
      for (int i = 0; i < 60; i++)
      {
        ghs.Update();
      }
      Console.WriteLine(ghs.GetHeatStorage(20));
      //全地中温度が同じになるまで計算
      ghs.HeatTransferCoefficient = 0;
      for (int i = 0; i < ghs.VerticalSplitNumber; i++) ghs.SetHeatFlow(i, 0);
      int iter = 0;
      while (true)
      {
        Console.WriteLine(ghs.GetHeatStorage(20) - buff);

        ghs.Update();
        double t00 = ghs.GetTemperature(0, 0);
        double maxErr = 0;
        for (int i = 0; i < ghs.RadialSplitNumber; i++)
          for (int j = 0; j < ghs.VerticalSplitNumber; j++)
            maxErr = Math.Max(maxErr, Math.Abs(ghs.GetTemperature(i, j) - t00));
        if (maxErr < 1e-5) break;
        iter++;
        //Console.WriteLine(iter + " : " + maxErr.ToString("F6"));
      }
      Console.WriteLine(ghs.GetTemperature(0, 0) + "C");
    }

    #endregion

    #region 簡易地中熱交換器テスト

    public static void gshpTest()
    {
      SimpleGroundHeatExchanger ghex = new SimpleGroundHeatExchanger(7.6 * 1000d / 3600d, 4.186, 0.75, SimpleGroundHeatExchanger.Type.Vertical);
      ghex.ConstantGroundTemperature = 18.7;
      ghex.InitTemperature(22);

      using (StreamReader sReader = new StreamReader("gshpTest.csv"))
      using (StreamWriter sWriter = new StreamWriter("gshpTestResult.csv"))
      {
        string line;
        while ((line = sReader.ReadLine()) != null)
        {
          string[] dat = line.Split(',');
          ghex.Update(double.Parse(dat[1]), double.Parse(dat[0]));

          sWriter.WriteLine(line + "," + ghex.NearGroundTemperature + "," + ghex.DistantGroundTemperature + "," + ghex.HeatExchange + "," + ghex.FluidOutletTemperature);
        }
      }
    }

    #endregion

    #region 熱源サブシステムテスト

    public static void HeatSourceSubsystemTest1()
    {
      AirHeatSourceModularChillersSystem ahSystem;
      DirectFiredAbsorptionChillerSystem arSystem;
      HeatSourceSystemModel hsSystem;
      SHASE_SimulationTest.MakeHeatSourceSystem(out hsSystem, out ahSystem, out arSystem);
      ImmutableAirHeatSourceModularChillers ahpChiller = ahSystem.AirHeatSourceModularChillers;
      ImmutableDirectFiredAbsorptionChiller arChiller = arSystem.DirectFiredAbsorptionChiller;

      using (StreamWriter sWriter = new StreamWriter
        ("HeatSourceSubsystemTest1.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        //タイトル行
        sWriter.WriteLine("時刻,負荷,AHP電力,AHP冷水ポンプ電力,AHP温水ポンプ電力,直焚電力,直焚ガス,"
          + "冷却塔電力,直焚冷水ポンプ電力,直焚温水ポンプ電力,冷却水ポンプ電力,冷水往温度,温水往温度");

        sWriter.WriteLine("冷却運転テスト");
        hsSystem.OutdoorAir = new MoistAir(34.4, 0.0194);
        hsSystem.SetOperatingMode(0, HeatSourceSystemModel.OperatingMode.Cooling);
        hsSystem.SetOperatingMode(1, HeatSourceSystemModel.OperatingMode.Cooling);
        for (int i = 0; i <= 10; i++)
        {
          double load = (527 + 300 + 300) * (0.1 * (10 - i));
          hsSystem.ForecastSupplyWaterTemperature(load / (5 * 4.186), 12, 0, 40);
          int ahpNum = ahSystem.OperatingChillerNumber;
          sWriter.WriteLine(load +
              ", " + (ahpChiller.ElectricConsumption * ahpChiller.OperatingNumber * ahpNum) +
              ", " + (ahSystem.ChilledWaterPump.GetElectricConsumption() * ahpNum) +
              ", " + (ahSystem.HotWaterPump.GetElectricConsumption() * ahpNum) +
              ", " + arChiller.ElectricConsumption + ", " + (arChiller.FuelConsumption * 3600) +
              ", " + arSystem.CoolingTower.ElectricConsumption +
              ", " + arSystem.ChilledWaterPump.GetElectricConsumption() +
              ", " + arSystem.HotWaterPump.GetElectricConsumption() +
              ", " + arSystem.CoolingWaterPump.GetElectricConsumption());
        }

        sWriter.WriteLine("加熱運転テスト");
        hsSystem.OutdoorAir = new MoistAir(2.0, 0.0014);
        hsSystem.SetOperatingMode(0, HeatSourceSystemModel.OperatingMode.Heating);
        hsSystem.SetOperatingMode(1, HeatSourceSystemModel.OperatingMode.Heating);
        for (int i = 0; i <= 10; i++)
        {
          double load = (346 + 300 + 300) * (0.1 * (10 - i));
          hsSystem.ForecastSupplyWaterTemperature(0, 12, load / (5 * 4.186), 40);
          int ahpNum = ahSystem.OperatingChillerNumber;
          sWriter.WriteLine(load +
              ", " + (ahpChiller.ElectricConsumption * ahpChiller.OperatingNumber * ahpNum) +
              ", " + (ahSystem.ChilledWaterPump.GetElectricConsumption() * ahpNum) +
              ", " + (ahSystem.HotWaterPump.GetElectricConsumption() * ahpNum) +
              ", " + arChiller.ElectricConsumption + ", " + (arChiller.FuelConsumption * 3600) +
              ", " + arSystem.CoolingTower.ElectricConsumption +
              ", " + arSystem.ChilledWaterPump.GetElectricConsumption() +
              ", " + arSystem.HotWaterPump.GetElectricConsumption() +
              ", " + arSystem.CoolingWaterPump.GetElectricConsumption());
        }
      }
    }

    public static void HeatSourceSubsystemTest2()
    {
      double[] dLoads = new double[]
      { 16336, 14358, 11126, 2690, 4998, 19979, 35228, 39168, 25232, 2894, 5027, 13463 };
      double[] sumLRates = new double[] { 0, 0, 0, 0, 0, 0, 0,
        1.79, 9.57, 9.17, 8.97, 9.27, 9.37, 9.27, 8.97, 10.69, 8.97, 9.27, 3.89, 0.40, 0.40, 0, 0, 0 };
      double[] winLRates = new double[] { 0, 0, 0, 0, 0, 0,
        0.30, 16.99, 12.29, 8.09, 10.29, 10.49, 10.29, 8.39, 8.19, 9.09, 5.59, 0, 0, 0, 0, 0, 0, 0 };
      double[] dbTemp = new double[] { 6.3, 5.9, 10.2, 14.8, 20.5, 21.6, 26.1, 27.0, 21.8, 18.2, 12.8, 8.3 };
      double[] rHumid = new double[] { 52.8, 48.6, 62.0, 57.9, 67.0, 73.4, 75.2, 72.6, 70.3, 63.6, 58.9, 54.1 };

      AirHeatSourceModularChillersSystem ahSystem;
      DirectFiredAbsorptionChillerSystem arSystem;
      HeatSourceSystemModel hsSystem;
      SHASE_SimulationTest.MakeHeatSourceSystem(out hsSystem, out ahSystem, out arSystem);
      ImmutableAirHeatSourceModularChillers ahpChiller = ahSystem.AirHeatSourceModularChillers;
      ImmutableDirectFiredAbsorptionChiller arChiller = arSystem.DirectFiredAbsorptionChiller;

      using (StreamWriter sWriter = new StreamWriter
        ("HeatSourceSubsystemTest2.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        //タイトル行
        sWriter.WriteLine("時刻,負荷,AHP電力,AHP冷水ポンプ電力,AHP温水ポンプ電力,直焚電力,直焚ガス,"
          + "冷却塔電力,直焚冷水ポンプ電力,直焚温水ポンプ電力,冷却水ポンプ電力,冷水往温度,温水往温度");

        for (int i = 0; i < 12; i++)
        {
          sWriter.WriteLine((i + 1) + "月");
          //運転モード設定
          bool isSummer = (4 <= i && i <= 9);
          HeatSourceSystemModel.OperatingMode mode;
          if (isSummer) mode = HeatSourceSystemModel.OperatingMode.Cooling;
          else mode = HeatSourceSystemModel.OperatingMode.Heating;
          hsSystem.SetOperatingMode(0, mode);
          hsSystem.SetOperatingMode(1, mode);

          double aHumid =
            MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(dbTemp[i], rHumid[i], 101.325);
          hsSystem.OutdoorAir = new MoistAir(dbTemp[i], aHumid);

          for (int j = 0; j < 24; j++)
          {
            double load;
            if (isSummer)
            {
              load = dLoads[i] * sumLRates[j] / 360d;  // MJ/h→kWに変換
              hsSystem.ForecastSupplyWaterTemperature(load / (5 * 4.186), 12, 0, 40);
            }
            else
            {
              load = dLoads[i] * winLRates[j] / 360d;  // MJ/h→kWに変換
              hsSystem.ForecastSupplyWaterTemperature(0, 12, load / (5 * 4.186), 40);
            }

            int ahpNum = ahSystem.OperatingChillerNumber;
            sWriter.WriteLine(j + ":00," + load +
              ", " + (ahpChiller.ElectricConsumption * ahpChiller.OperatingNumber * ahpNum) +
              ", " + (ahSystem.ChilledWaterPump.GetElectricConsumption() * ahpNum) +
              ", " + (ahSystem.HotWaterPump.GetElectricConsumption() * ahpNum) +
              ", " + arChiller.ElectricConsumption + ", " + (arChiller.FuelConsumption * 3600) +
              ", " + arSystem.CoolingTower.ElectricConsumption +
              ", " + arSystem.ChilledWaterPump.GetElectricConsumption() +
              ", " + arSystem.HotWaterPump.GetElectricConsumption() +
              ", " + arSystem.CoolingWaterPump.GetElectricConsumption() +
              ", " + hsSystem.ChilledWaterSupplyTemperature + ", " + hsSystem.HotWaterSupplyTemperature);
          }
        }
      }
    }

    public static void HeatSourceSubsystemTest3()
    {
      //時系列負荷
      double[] hLoads = new double[]
      { 0, 0, 0, 0, 0, 0, 0, 76, 407, 390, 381, 394, 398, 394, 381, 455, 381, 394, 165, 17, 17, 0, 0, 0 };

      //ターボ冷凍機の定格冷水・冷却水量[kg/s]
      const double NCH_FLOW = 500d / (12 - 7) / 4.186;
      const double NCD_FLOW = 1670d / 60;

      //ターボ冷凍機によるサブシステムの計算
      //個別機器のインスタンスを作成
      SimpleCentrifugalChiller chiller = new SimpleCentrifugalChiller(500d / 6d, 0.2, 12, 7, 37, NCH_FLOW, false);
      CentrifugalPump chPmp = new CentrifugalPump
        (150, 0.001 * NCH_FLOW, 140, 0.001 * NCH_FLOW, CentrifugalPump.ControlMethod.ConstantPressureWithInverter, 50);
      CentrifugalPump cdPmp = new CentrifugalPump
        (150, 0.001 * NCD_FLOW, 140, 0.001 * NCD_FLOW, CentrifugalPump.ControlMethod.ConstantPressureWithInverter, 50);
      CoolingTower cTower = new CoolingTower(37, 32, 27, NCD_FLOW, CoolingTower.AirFlowDirection.CrossFlow, false);
      //サブシステムを作成
      CentrifugalChillerSystem crSystem = new CentrifugalChillerSystem(chiller, chPmp, cdPmp, cTower, 1, 1);
      //熱源システムを作成
      HeatSourceSystemModel hsSystem = new HeatSourceSystemModel(new IHeatSourceSubSystem[] { crSystem });
      hsSystem.SetOperatingMode(0, HeatSourceSystemModel.OperatingMode.Cooling);
      hsSystem.SetChillingOperationSequence(0, 1);
      hsSystem.ChilledWaterSupplyTemperatureSetpoint = 7;
      hsSystem.OutdoorAir = new MoistAir(35, 0.0195);
      hsSystem.TimeStep = 3600;

      //24hの計算実行
      Console.WriteLine("冷熱供給,ターボ,冷水ポンプ,冷却水ポンプ,冷却塔");
      for (int i = 0; i < hLoads.Length; i++)
      {
        hsSystem.ForecastSupplyWaterTemperature(hLoads[i] / (4.186 * 5), 12, 0, 40);
        hsSystem.FixState();
        Console.WriteLine(hLoads[i].ToString("F0") + ", " +
          chiller.ElectricConsumption.ToString("F2") + ", " + chPmp.GetElectricConsumption().ToString("F2") + ", " +
          cdPmp.GetElectricConsumption().ToString("F2") + ", " + cTower.ElectricConsumption.ToString("F2"));
      }

      //蓄熱槽によるサブシステムの計算
      //個別機器のインスタンスを作成（冷凍機・冷水ポンプ・冷却水ポンプ・冷却塔は使い回す）
      PlateHeatExchanger pHex = new PlateHeatExchanger(500, 6, NCH_FLOW, 7, NCH_FLOW);
      MultipleStratifiedWaterTank wTank = new MultipleStratifiedWaterTank(10, 10 * 10, 0.8, 9.5, 20);
      wTank.HeatLossCoefficient = 0.001 * (0.037 / 0.05) * (10 * 10 * 6);
      CentrifugalPump chgPmp = new CentrifugalPump
        (150, 0.001 * NCH_FLOW, 140, 0.001 * NCH_FLOW, CentrifugalPump.ControlMethod.ConstantPressureWithBypass, 50);
      CentrifugalPump disPmp = new CentrifugalPump
        (150, 0.001 * NCH_FLOW, 140, 0.001 * NCH_FLOW, CentrifugalPump.ControlMethod.MinimumPressure, 50);
      //サブシステムを作成
      MultipleStratifiedWaterTankSystem wtSystem =
        new MultipleStratifiedWaterTankSystem(wTank, pHex, chiller, chPmp, cdPmp, chgPmp, disPmp, cTower, 1, 1);
      wtSystem.StorageTemperature = 5.0;
      //熱源システムを作成
      hsSystem = new HeatSourceSystemModel(new IHeatSourceSubSystem[] { wtSystem });
      hsSystem.SetOperatingMode(0, HeatSourceSystemModel.OperatingMode.Cooling);
      hsSystem.SetChillingOperationSequence(0, 1);
      hsSystem.ChilledWaterSupplyTemperatureSetpoint = 7;
      hsSystem.OutdoorAir = new MoistAir(35, 0.0195);
      hsSystem.TimeStep = 3600;

      //24hの計算を周期定常になるまで繰り返す
      Console.WriteLine("冷熱供給,ターボ,冷水ポンプ,冷却水ポンプ,冷却塔,蓄熱ポンプ,放熱ポンプ,冷熱製造,水槽温度");
      bool lastCalc = false;
      double lastST = 100;
      while (!lastCalc)
      {
        lastCalc = (Math.Abs(lastST - wTank.GetTemperature(10)) < 0.1);
        lastST = wTank.GetTemperature(10);
        for (int i = 0; i < hLoads.Length; i++)
        {
          if (i < 9 || 22 < i) wtSystem.Charging = true;
          else wtSystem.Charging = false;
          hsSystem.ForecastSupplyWaterTemperature(hLoads[i] / (4.186 * 5), 12, 0, 40);
          hsSystem.FixState();
          if (lastCalc)
          {
            Console.Write(hLoads[i].ToString("F0") + ", " +
              chiller.ElectricConsumption.ToString("F2") + ", " +
              chPmp.GetElectricConsumption().ToString("F2") + ", " +
              cdPmp.GetElectricConsumption().ToString("F2") + ", " +
              cTower.ElectricConsumption.ToString("F2") + ", " +
              chgPmp.GetElectricConsumption().ToString("F2") + ", " +
              disPmp.GetElectricConsumption().ToString("F2") + ", " +
              chiller.CoolingLoad.ToString("F1"));
            for (int j = 1; j < wTank.LayerNumber; j += 2)
              Console.Write(", " + wTank.GetTemperature(j).ToString("F2"));
            Console.WriteLine();
          }
        }
      }
    }

    public static void HeatSourceSubsystemTest4()
    {
      //ターボ冷凍機の定格冷水・冷却水量[kg/s]
      const double NCH_FLOW = 500d / (12 - 7) / 4.186;
      const double NCD_FLOW = 1670d / 60;

      //ターボ冷凍機によるサブシステムの計算
      //個別機器のインスタンスを作成
      SimpleCentrifugalChiller chiller = new SimpleCentrifugalChiller(500d / 6d, 0.2, 12, 7, 37, NCH_FLOW, false);
      CentrifugalPump chPmp = new CentrifugalPump
        (150, 1e-3 * NCH_FLOW, 140, 1e-3 * NCH_FLOW, CentrifugalPump.ControlMethod.ConstantPressureWithInverter, 50);
      CentrifugalPump cdPmp = new CentrifugalPump
        (150, 1e-3 * NCD_FLOW, 140, 1e-3 * NCD_FLOW, CentrifugalPump.ControlMethod.MinimumPressure, 50);
      CoolingTower cTower = new CoolingTower(37, 32, 27, NCD_FLOW, CoolingTower.AirFlowDirection.CrossFlow, true);
      //サブシステムを作成
      CentrifugalChillerSystem crSystem = new CentrifugalChillerSystem(chiller, chPmp, cdPmp, cTower, 1, 1);
      crSystem.ControlCoolingWaterTemperature = true;
      crSystem.AutoControlCoolingWaterFlowRate = false; //負荷比例では制御しない（自分で制御）
      crSystem.Mode = HeatSourceSystemModel.OperatingMode.Cooling;
      MoistAir oa = new MoistAir(24, 0.0125);
      crSystem.OutdoorAir = oa;

      double pLoad = 0.6;
      MultiMinimization.MinimizeFunction mFnc = delegate (IVector vec, int iteration)
      {
        //エネルギー消費量の計算
        crSystem.CoolingWaterTemperatureSetpoint = vec[0];
        crSystem.CoolingWaterFlowSetpoint = vec[1] * cTower.MaxWaterFlowRate;
        crSystem.ForecastSupplyWaterTemperature(500 * pLoad / (5 * 4.186), 0);
        double ecsmp = crSystem.Chiller.ElectricConsumption
        + crSystem.CoolingTower.ElectricConsumption + cdPmp.GetElectricConsumption();

        //ペナルティの計算
        double pSum = Math.Max(0, 12 - vec[0]) + Math.Max(0, vec[0] - 32);
        pSum += 10 * (Math.Max(0, 0.5 - vec[1]) + Math.Max(0, vec[1] - 1.0));

        return ecsmp + pSum * (1 + 0.5 * iteration);
      };

      IVector vecX = new Vector(2);
      using (StreamWriter sWriter = new StreamWriter
        ("HeatSourceSubsystemTest4.csv", false, Encoding.UTF8))
      {
        //網羅的に探索
        double[,] elcTB = new double[13, 11];
        double[,] elcCT = new double[13, 11];
        double[,] elcPmp1 = new double[13, 11];
        double[,] elcPmp2 = new double[13, 11];
        sWriter.WriteLine(",50%,55%,60%,65%,70%,75%,80%,85%,90%,95%,100%");

        for (int i = 0; i < 13; i++)
        {
          for (int j = 0; j < 11; j++)
          {
            vecX[0] = i + 20;
            vecX[1] = 0.5 + 0.05 * j;
            mFnc(vecX, 1);
            if (!crSystem.IsOverLoad_C) 
            {
              elcTB[i, j] = chiller.ElectricConsumption;
              elcCT[i, j] = cTower.ElectricConsumption;
              elcPmp1[i, j] = cdPmp.GetElectricConsumption();
              elcPmp2[i, j] = chPmp.GetElectricConsumption();
            }
          }
        }

        sWriter.WriteLine("チラー");
        for (int i = 0; i < 13; i++)
        {
          sWriter.Write((i + 20) + "C");
          for (int j = 0; j < 11; j++) sWriter.Write("," + elcTB[i, j].ToString("F3"));
          sWriter.WriteLine();
        }

        sWriter.WriteLine("冷却塔");
        for (int i = 0; i < 13; i++)
        {
          sWriter.Write((i + 20) + "C");
          for (int j = 0; j < 11; j++) sWriter.Write("," + elcCT[i, j].ToString("F3"));
          sWriter.WriteLine();
        }

        sWriter.WriteLine("冷却水ポンプ");
        for (int i = 0; i < 13; i++)
        {
          sWriter.Write((i + 20) + "C");
          for (int j = 0; j < 11; j++) sWriter.Write("," + elcPmp1[i, j].ToString("F3"));
          sWriter.WriteLine();
        }

        sWriter.WriteLine("冷水ポンプ");
        for (int i = 0; i < 13; i++)
        {
          sWriter.Write((i + 20) + "C");
          for (int j = 0; j < 11; j++) sWriter.Write("," + elcPmp2[i, j].ToString("F3"));
          sWriter.WriteLine();
        }

        sWriter.WriteLine("合計");
        for (int i = 0; i < 13; i++)
        {
          sWriter.Write((i + 20) + "C");
          for (int j = 0; j < 11; j++) sWriter.Write("," + (elcTB[i, j] + elcCT[i, j] + elcPmp1[i, j] + elcPmp2[i, j]).ToString("F3"));
          sWriter.WriteLine();
        }

        //準ニュートン法で探索
        sWriter.WriteLine("準ニュートン法 探索結果");
        int iter;
        vecX[0] = 32; vecX[1] = 1.0;
        MultiMinimization.QuasiNewton(ref vecX, mFnc, 50, 1e-4, 1e-4, 1e-4, out iter);
        sWriter.WriteLine("温度設定," + vecX[0] + ", 流量比, " + vecX[1]);

        //負荷率と外気湿球温度を変化させた場合の最適値を計算
        sWriter.WriteLine("負荷率,湿球温度,消費電力（無対策）,消費電力（最小）,最適冷却水温度,最適流量比");
        for (int i = 0; i < 6; i++)
        {
          pLoad = 0.5 + i * 0.1;
          for (int j = 0; j < 11; j++)
          {
            oa.WetbulbTemperature = 16 + j;
            sWriter.Write(pLoad + "," + oa.WetbulbTemperature);

            vecX[0] = 32; vecX[1] = 1.0;
            double ec = mFnc(vecX, 1);
            sWriter.Write("," + ec);

            MultiMinimization.QuasiNewton(ref vecX, mFnc, 50, 1e-4, 1e-4, 1e-4, out iter);
            double min = mFnc(vecX, 1);
            sWriter.WriteLine("," + min + "," + vecX[0] + "," + vecX[1]);
          }
        }
      }
    }

    public static void HeatSourceSubsystemTest5()
    {
      double mcEvpC = 178.3 / 60;
      double mcCndC = 216.7 / 60;
      double mcEvpH = 155.0 / 60;
      double mcCndH = 206.7 / 60;
      double qC = mcEvpC * 5 * 4.186;
      double qH = mcEvpH * 5 * 4.186;
      double[] tcd = new double[] { 20, 30, 40 };
      double[] ths = new double[] { -5, 0, 10, 15, 20, 25 };

      WaterHeatPump whp = new WaterHeatPump(62.4, mcEvpC, mcCndC, 7, 26, 13.3, 72.3, mcCndH, mcEvpH, 45, 12, 18.6);
      CentrifugalPump gPmp = new CentrifugalPump
        (150, 1e-3 * mcCndC, 140, 1e-3 * mcCndC, CentrifugalPump.ControlMethod.ConstantPressureWithInverter, 50);
      CentrifugalPump chPmp = new CentrifugalPump
        (150, 1e-3 * mcEvpC, 140, 1e-3 * mcEvpC, CentrifugalPump.ControlMethod.ConstantPressureWithInverter, 50);
      SimpleGroundHeatExchanger gHex = new SimpleGroundHeatExchanger(mcCndC, 4.186, 0.75, SimpleGroundHeatExchanger.Type.Vertical);
      //土壌物性調整
      gHex.NearGroundHeatConductance *= 5;
      gHex.NearGroundHeatCapacity *= 5;

      //GSHPサブシステムを作成
      GroundHeatSourceHeatPumpSystem gshpSystem = new GroundHeatSourceHeatPumpSystem(whp, gHex, gPmp, chPmp);

      //熱源システムを作成
      HeatSourceSystemModel hsSystem = new HeatSourceSystemModel(new IHeatSourceSubSystem[] { gshpSystem });
      hsSystem.TimeStep = 3600;
      hsSystem.SetChillingOperationSequence(0, 1);
      hsSystem.SetHeatingOperationSequence(0, 1);

      Console.WriteLine("熱源水往温度, 熱源水還温度, 近傍土壌温度, 遠方土壌温度, WHP消費エネルギー");
      //8hの冷却運転
      hsSystem.SetOperatingMode(0, HeatSourceSystemModel.OperatingMode.Cooling);
      hsSystem.ChilledWaterSupplyTemperatureSetpoint = 7;
      for (int i = 0; i < 8; i++)
      {
        hsSystem.ForecastSupplyWaterTemperature(0.8 * mcEvpC, 12, 0, 40);
        hsSystem.FixState();
        Console.WriteLine(
          whp.CoolingWaterOutletTemperature.ToString("F3") + ", " +
          whp.CoolingWaterInletTemperature.ToString("F3") + ", " +
          gHex.NearGroundTemperature.ToString("F3") + ", " +
          gHex.DistantGroundTemperature.ToString("F3") + ", " +
          whp.EnergyConsumption.ToString("F3"));
      }
      //16h回復
      for (int i = 0; i < 10; i++)
      {
        hsSystem.ForecastSupplyWaterTemperature(0, 12, 0, 40);
        hsSystem.FixState();
        Console.WriteLine(
          whp.CoolingWaterOutletTemperature.ToString("F3") + ", " +
          whp.CoolingWaterInletTemperature.ToString("F3") + ", " +
          gHex.NearGroundTemperature.ToString("F3") + ", " +
          gHex.DistantGroundTemperature.ToString("F3") + ", " +
          whp.EnergyConsumption.ToString("F3"));
      }
      //8hの加熱運転
      hsSystem.SetOperatingMode(0, HeatSourceSystemModel.OperatingMode.Heating);
      hsSystem.HotWaterSupplyTemperatureSetpoint = 45;
      for (int i = 0; i < 8; i++)
      {
        hsSystem.ForecastSupplyWaterTemperature(0, 12, 0.8 * mcCndH, 40);
        hsSystem.FixState();
        Console.WriteLine(
          whp.HeatSourceWaterOutletTemperature.ToString("F3") + ", " +
          whp.HeatSourceWaterInletTemperature.ToString("F3") + ", " +
          gHex.NearGroundTemperature.ToString("F3") + ", " +
          gHex.DistantGroundTemperature.ToString("F3") + ", " +
          whp.EnergyConsumption.ToString("F3"));
      }
      //16h回復
      for (int i = 0; i < 10; i++)
      {
        hsSystem.ForecastSupplyWaterTemperature(0, 12, 0, 40);
        hsSystem.FixState();
        Console.WriteLine(
          whp.CoolingWaterOutletTemperature.ToString("F3") + ", " +
          whp.CoolingWaterInletTemperature.ToString("F3") + ", " +
          gHex.NearGroundTemperature.ToString("F3") + ", " +
          gHex.DistantGroundTemperature.ToString("F3") + ", " +
          whp.EnergyConsumption.ToString("F3"));
      }
    }

    public static void HeatSourceSubsystemTest6()
    {
      const bool TEST_COOLING = false;

      //時系列負荷
      double[] hLoads = new double[]
      { 0, 0, 0, 0, 0, 0, 0, 500, 500, 500, 500, 500, 500, 500, 500, 500, 500, 500, 500, 500, 500, 0, 0, 0 };
      //{ 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

      //AHP関連のインスタンスを作成
      const double NCH_FLOW = 500d / (12 - 7) / 4.186;  //AHP定格冷温水量[kg/s]
      AirHeatSourceModularChillers ahp = new AirHeatSourceModularChillers(500, 7, NCH_FLOW, 35, NCH_FLOW * 2 * 1.2, 500d / 3.2,
        500, 47, NCH_FLOW, 0, NCH_FLOW * 2 * 1.2, 500d / 3.2, 1, 0);
      CentrifugalPump chgPmp = new CentrifugalPump  //蓄熱ポンプ
        (150, 0.001 * NCH_FLOW, 140, 0.001 * NCH_FLOW, CentrifugalPump.ControlMethod.ConstantPressureWithInverter, 50);

      //蓄熱槽関連のインスタンスを作成
      const double L_COEF = 1.2;  //AHPよりも放熱を大きめにする
      PlateHeatExchanger pHex = new PlateHeatExchanger(500 * L_COEF, 6, NCH_FLOW * L_COEF, 7, NCH_FLOW * 1.2);
      MultiConnectedWaterTank wTank = new MultiConnectedWaterTank(
        new double[] { 100, 100, 100, 100, 100, 100, 100, 100, 100, 100 }
        );
      CentrifugalPump rlsPmp1 = new CentrifugalPump //放熱一次ポンプ
        (150, 0.001 * NCH_FLOW * L_COEF, 140, 0.001 * NCH_FLOW * L_COEF, CentrifugalPump.ControlMethod.ConstantPressureWithBypass, 50);
      CentrifugalPump rlsPmp2 = new CentrifugalPump //放熱二次ポンプ
        (150, 0.001 * NCH_FLOW * L_COEF, 140, 0.001 * NCH_FLOW * L_COEF, CentrifugalPump.ControlMethod.MinimumPressure, 50);
      //サブシステムを作成
      MultiConnectedWaterTankSystem wtSystem =
        new MultiConnectedWaterTankSystem(wTank, pHex, ahp, chgPmp, rlsPmp1, rlsPmp2, 1);
      wtSystem.ChilledWaterStorageTemperature = 5;
      wtSystem.HotWaterStorageTemperature = 47;
      //熱源システムを作成
      HeatSourceSystemModel hsSystem = new HeatSourceSystemModel(new IHeatSourceSubSystem[] { wtSystem });
      hsSystem.SetOperatingMode(0, TEST_COOLING ? HeatSourceSystemModel.OperatingMode.Cooling : HeatSourceSystemModel.OperatingMode.Heating);
      hsSystem.SetChillingOperationSequence(0, 1);
      hsSystem.SetHeatingOperationSequence(0, 1);
      hsSystem.ChilledWaterSupplyTemperatureSetpoint = 7;
      hsSystem.HotWaterSupplyTemperatureSetpoint = 45;
      hsSystem.OutdoorAir = new MoistAir(TEST_COOLING ? 35 : 5, 0.0195);
      hsSystem.TimeStep = 3600;

      //24hの計算を周期定常になるまで繰り返す
      Console.WriteLine("冷熱供給,AHP_E,蓄熱ポンプ,放熱一次ポンプ,放熱二次ポンプ,冷熱製造,水槽温度");
      bool lastCalc = false;
      double[] lastST = new double[wTank.TankNumber];
      while (!lastCalc)
      {
        double errSum = 0;
        for (int i = 0; i < wTank.TankNumber; i++)
        {
          errSum += Math.Abs(lastST[i] - wTank.GetTemperature(i));
          lastST[i] = wTank.GetTemperature(i);
        }
        lastCalc = (errSum < 0.1);

        for (int i = 0; i < hLoads.Length; i++)
        {
          if (i < 9 || 22 < i) wtSystem.OperatingHeatSourceNumber = 1;
          else wtSystem.OperatingHeatSourceNumber = 0;
          double load = hLoads[i] / (4.186 * 5);
          hsSystem.ForecastSupplyWaterTemperature((TEST_COOLING ? load : 0), 12, (TEST_COOLING ? 0 : load), 40);
          hsSystem.FixState();

          //書き出し
          Console.Write(
              hLoads[i].ToString("F0") + ", " +
              ahp.ElectricConsumption.ToString("F2") + ", " +
              chgPmp.GetElectricConsumption().ToString("F2") + ", " +
              rlsPmp1.GetElectricConsumption().ToString("F2") + ", " +
              rlsPmp2.GetElectricConsumption().ToString("F2") + ", " +
              ahp.CoolingLoad.ToString("F1"));
          for (int j = 1; j < wTank.TankNumber; j++)
            Console.Write(", " + wTank.GetTemperature(j).ToString("F2"));
          Console.WriteLine();
        }
        Console.WriteLine("---------------------------");
      }

    }

    #endregion

    #region 蓄熱槽テスト

    public static void WaterHeatStorageTest1()
    {
      int tNum = 10;
      double wf = 0.6 / 60d;

      double[] volumes = new double[tNum];
      for (int i = 0; i < tNum; i++) volumes[i] = 15;
      MultiConnectedWaterTank wTank = new MultiConnectedWaterTank(volumes);
      wTank.InitializeTemperature(17);
      wTank.TimeStep = 600;
      for (int i = 0; i < tNum; i++) wTank.SetHeatLossCoefficient(i, 0.02);
      wTank.AmbientTemperature = 15;

      using (StreamWriter sw = new StreamWriter("WaterHeatStorageTest1.csv"))
      {
        for (int i = 0; i < 48; i++)
        {
          sw.Write(i);
          for (int j = 0; j < tNum; j++) sw.Write(", " + wTank.GetTemperature(j));
          sw.WriteLine();

          wTank.ForecastState(7, wf, true);
          wTank.FixState();
        }
        for (int i = 0; i < 48; i++)
        {
          sw.Write(i);
          for (int j = 0; j < tNum; j++) sw.Write(", " + wTank.GetTemperature(j));
          sw.WriteLine();

          wTank.ForecastState(17, wf, false);
          wTank.FixState();
        }
      }
    }

    public static void WaterHeatStorageTest2()
    {
      MultipleStratifiedWaterTank tank = new MultipleStratifiedWaterTank(0.8, 0.8 * 0.8, 0.05, 0.72, 100);
      tank.HeatLossCoefficient = 0.001 * 6 * 0.04 / 0.1;
      tank.AmbientTemperature = 20;
      tank.TimeStep = 10;

      using (StreamWriter sWriter = new StreamWriter
        ("WaterHeatStorageTest2.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        sWriter.WriteLine("ステップ温度上昇");
        double waterChangeRate = 0.0;
        double trig = 0.0;
        tank.InitializeTemperature(15);
        while (waterChangeRate < 1.0)
        {
          double inletTemp = 21;
          if (0.5 < waterChangeRate) inletTemp = 25;
          tank.ForecastState(inletTemp, 0.25 / 3600, true);
          waterChangeRate += (0.25 / 3600 * tank.TimeStep) / tank.WaterVolume;

          if (trig < waterChangeRate)
          {
            sWriter.Write(waterChangeRate.ToString("F1"));
            for (int j = 0; j < tank.LayerNumber; j++) sWriter.Write(", " + tank.GetTemperature(j));
            sWriter.WriteLine();
            trig += 0.1;
          }
        }

        sWriter.WriteLine("ステップ温度低下");
        waterChangeRate = 0.0;
        trig = 0.0;
        tank.InitializeTemperature(15);
        while (waterChangeRate < 1.0)
        {
          double inletTemp = 24;
          if (0.5 < waterChangeRate) inletTemp = 21;
          tank.ForecastState(inletTemp, 0.25 / 3600, true);
          waterChangeRate += (0.25 / 3600 * tank.TimeStep) / tank.WaterVolume;

          if (trig < waterChangeRate)
          {
            sWriter.Write(waterChangeRate.ToString("F1"));
            for (int j = 0; j < tank.LayerNumber; j++) sWriter.Write(", " + tank.GetTemperature(j));
            sWriter.WriteLine();
            trig += 0.1;
          }
        }

        sWriter.WriteLine("ステップ流量変化");
        waterChangeRate = 0.0;
        trig = 0.0;
        tank.InitializeTemperature(6);
        while (waterChangeRate < 1.0)
        {
          double waterFlowRate = 0.5 / 3600;
          if (0.3 < waterChangeRate && waterChangeRate < 0.8) waterFlowRate = 0.1 / 3600;
          tank.ForecastState(12, waterFlowRate, true);
          waterChangeRate += (waterFlowRate * tank.TimeStep) / tank.WaterVolume;

          if (trig < waterChangeRate)
          {
            sWriter.Write(waterChangeRate.ToString("F1"));
            for (int j = 0; j < tank.LayerNumber; j++) sWriter.Write(", " + tank.GetTemperature(j));
            sWriter.WriteLine();
            trig += 0.1;
          }
        }
      }
    }

    #endregion

  }
}
