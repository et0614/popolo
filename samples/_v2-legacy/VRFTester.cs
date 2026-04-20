using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;

using Popolo.HVAC.MultiplePackagedHeatPump;
using Popolo.ThermophysicalProperty;
using Popolo.Weather;
using Popolo.Numerics;
using Popolo.ThermalLoad;

namespace PopoloTester
{
  class VRFTester
  {

    #region 成り行き状態計算のテスト

    public static void TestNoControl1()
    {
      //室外機作成:ダイキン22.8kW
      VRFSystem vrf = VRFInitializer.MakeOutdoorUnit(VRFInitializer.OutdoorUnitModel.Daikin_VRVX, VRFInitializer.CoolingCapacity.C22_4, 0, false);
      //室内機リスト
      VRFUnit[] iUnits = new VRFUnit[]
      {
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6)
      };
      vrf.AddIndoorUnit(iUnits);
      vrf.MinEvaporatingTemperature = 5;
      vrf.MaxEvaporatingTemperature = 30;
      vrf.MinCondensingTemperature = 20;
      vrf.MaxCondensingTemperature = 50;
      vrf.ControlThermoOffWithSensibleHeat = false; //全熱基準でサーモを制御（処理負荷を完全に合わせるため）
      for (int i = 0; i < iUnits.Length; i++) vrf.SetIndoorUnitInletAirState(i, 25, 0.010); //吸い込み温湿度の初期化

      //状態書き出し用ローカル関数
      void writeConsole()
      {
        Console.WriteLine(
          vrf.CompressorElectricity.ToString("F2") + ", " +
          vrf.PartialLoadRate.ToString("F3") + ", " +
          vrf.EvaporatingTemperature.ToString("F1") + ", " +
          vrf.CondensingTemperature.ToString("F1") + ", " +
          iUnits[0].OutletAirTemperature.ToString("F1") + ", " +
          (iUnits[0].OutletAirHumidityRatio * 1000).ToString("F1") + ", " +
          iUnits[0].HeatTransfer.ToString("F1") + ", " +
          iUnits[1].OutletAirTemperature.ToString("F1") + ", " +
          (iUnits[1].OutletAirHumidityRatio * 1000).ToString("F1") + ", " +
          iUnits[1].HeatTransfer.ToString("F1")
          );
      }

      //冷却モード************************
      vrf.OutdoorAirDrybulbTemperature = 35;
      vrf.CurrentMode = VRFSystem.Mode.Cooling;
      vrf.SetIndoorUnitMode(VRFUnit.Mode.Cooling);
      vrf.TargetEvaporatingTemperature = vrf.MinEvaporatingTemperature;

      Console.WriteLine("冷却_風量上昇テスト");
      for (int i = 0; i < 10; i++)
      {
        for (int j = 0; j < iUnits.Length; j++) iUnits[j].AirFlowRate = iUnits[j].NominalAirFlowRate * (0.1 + 0.1 * i);
        vrf.UpdateState(false);
        writeConsole();
      }
      Console.WriteLine();
      for (int i = 0; i < iUnits.Length; i++) iUnits[i].AirFlowRate = iUnits[i].NominalAirFlowRate; //風量を戻す

      Console.WriteLine("冷却_蒸発温度上昇テスト");
      for (double i = vrf.MinEvaporatingTemperature; i < vrf.MaxEvaporatingTemperature; i++)
      {
        vrf.TargetEvaporatingTemperature = i;
        vrf.UpdateState(false);
        writeConsole();
      }
      Console.WriteLine();
      vrf.TargetEvaporatingTemperature = vrf.MinEvaporatingTemperature; //蒸発温度を戻す

      Console.WriteLine("冷却_吸い込み温度上昇テスト");
      for (int i = 0; i < 40; i+=2)
      {
        for (int j = 0; j < iUnits.Length; j++) iUnits[j].InletAirTemperature = 0 + i;

        iUnits[1].InletAirTemperature = 80;

        vrf.UpdateState(false);
        writeConsole();
      }
      for (int i = 0; i < iUnits.Length; i++) iUnits[i].InletAirTemperature = 25; //温度を戻す
      Console.WriteLine();

      Console.WriteLine("冷却_不均一風量上昇テスト");
      for (int i = 0; i < 10; i++)
      {
        iUnits[0].AirFlowRate = iUnits[0].NominalAirFlowRate * (0.1 + 0.1 * i);
        vrf.UpdateState(false);
        writeConsole();
      }
      for (int i = 0; i < iUnits.Length; i++) iUnits[i].AirFlowRate = iUnits[i].NominalAirFlowRate; //風量を戻す
      Console.WriteLine();

      Console.WriteLine("冷却_外気温度上昇テスト");
      for (int i = 20; i < 40; i++)
      {
        vrf.OutdoorAirDrybulbTemperature = i;
        vrf.UpdateState(false);
        writeConsole();
      }
      Console.WriteLine();
      vrf.OutdoorAirDrybulbTemperature = 35; //外気温度を戻す


      //加熱モード************************
      vrf.OutdoorAirDrybulbTemperature = 7;
      vrf.CurrentMode = VRFSystem.Mode.Heating;
      vrf.SetIndoorUnitMode(VRFUnit.Mode.Heating);
      vrf.TargetCondensingTemperature = vrf.MaxCondensingTemperature;

      Console.WriteLine("加熱_風量上昇テスト");
      for (int i = 0; i < 10; i++)
      {
        for (int j = 0; j < iUnits.Length; j++) iUnits[j].AirFlowRate = iUnits[j].NominalAirFlowRate * (0.1 + 0.1 * i);
        vrf.UpdateState(false);
        writeConsole();
      }
      Console.WriteLine();
      for (int i = 0; i < iUnits.Length; i++) iUnits[i].AirFlowRate = iUnits[i].NominalAirFlowRate; //風量を戻す

      Console.WriteLine("加熱_凝縮温度下降テスト");
      for (double i = vrf.MaxCondensingTemperature; vrf.MinCondensingTemperature < i; i--)
      {
        vrf.TargetCondensingTemperature = i;
        vrf.UpdateState(false);
        writeConsole();
      }
      Console.WriteLine();
      vrf.TargetCondensingTemperature = vrf.MaxCondensingTemperature; //凝縮温度を戻す

      Console.WriteLine("加熱_吸い込み温度下降テスト");
      for (int i = 0; i < 40; i+=2)
      {
        for (int j = 0; j < iUnits.Length; j++) iUnits[j].InletAirTemperature = 40 - i;
        vrf.UpdateState(false);
        writeConsole();
      }
      for (int i = 0; i < iUnits.Length; i++) iUnits[i].InletAirTemperature = 25; //温度を戻す
      Console.WriteLine();

      Console.WriteLine("加熱_不均一風量上昇テスト");
      for (int i = 0; i < 10; i++)
      {
        iUnits[0].AirFlowRate = iUnits[0].NominalAirFlowRate * (0.1 + 0.1 * i);
        vrf.UpdateState(false);
        writeConsole();
      }
      for (int i = 0; i < iUnits.Length; i++) iUnits[i].AirFlowRate = iUnits[i].NominalAirFlowRate; //風量を戻す
      Console.WriteLine();

      Console.WriteLine("冷却_外気温度下降テスト");
      for (int i = 20; -10 < i; i--)
      {
        vrf.OutdoorAirDrybulbTemperature = i;
        vrf.UpdateState(false);
        writeConsole();
      }
      Console.WriteLine();
      vrf.OutdoorAirDrybulbTemperature = 35; //外気温度を戻す

    }

    public static void TestNoControl2()
    {
      MersenneTwister rnd = new MersenneTwister(1); 

      //室外機作成:ダイキン22.8kW
      VRFSystem vrf = VRFInitializer.MakeOutdoorUnit(VRFInitializer.OutdoorUnitModel.Daikin_VRVX, VRFInitializer.CoolingCapacity.C22_4, 0, false);
      //室内機リスト
      VRFUnit[] iUnits = new VRFUnit[]
      {
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6)
      };
      vrf.AddIndoorUnit(iUnits);
      vrf.MinEvaporatingTemperature = 5;
      vrf.MaxEvaporatingTemperature = 30;
      vrf.MinCondensingTemperature = 20;
      vrf.MaxCondensingTemperature = 50;
      vrf.ControlThermoOffWithSensibleHeat = false; //全熱基準でサーモを制御（処理負荷を完全に合わせるため）
      for (int i = 0; i < iUnits.Length; i++) vrf.SetIndoorUnitInletAirState(i, 25, 0.010); //吸い込み温湿度の初期化

      //状態書き出し用ローカル関数
      void writeConsole()
      {
        Console.Write(
          vrf.OutdoorAirDrybulbTemperature.ToString("F1") + ", " + 
          vrf.TargetEvaporatingTemperature.ToString("F1") + ", " +
          vrf.TargetCondensingTemperature.ToString("F1") + ", "
          );
      }

      int iterNum = 1;
      while (true)
      {
        Console.Write(iterNum + ": ");

        //冷却
        vrf.CurrentMode = VRFSystem.Mode.Cooling;
        vrf.SetIndoorUnitMode(VRFUnit.Mode.Cooling);
        vrf.TargetEvaporatingTemperature = vrf.MinEvaporatingTemperature + rnd.NextDouble() * 
          (vrf.MaxEvaporatingTemperature - vrf.MinEvaporatingTemperature);
        vrf.OutdoorAirDrybulbTemperature = 10 + rnd.NextDouble() * 30;
        for (int i = 0; i < iUnits.Length; i++)
        {
          iUnits[i].AirFlowRate = iUnits[i].NominalAirFlowRate * rnd.NextDouble();
          iUnits[i].InletAirTemperature = 40 * rnd.NextDouble();
          iUnits[i].InletAirHumidityRatio = 0.02 * rnd.NextDouble();
        }
        writeConsole();
        vrf.UpdateState(false);

        //加熱
        vrf.CurrentMode = VRFSystem.Mode.Heating;
        vrf.SetIndoorUnitMode(VRFUnit.Mode.Heating);
        vrf.TargetCondensingTemperature = vrf.MinCondensingTemperature + rnd.NextDouble() * 
          (vrf.MaxCondensingTemperature - vrf.MinCondensingTemperature);
        vrf.OutdoorAirDrybulbTemperature = -10 + rnd.NextDouble() * 30;
        vrf.OutdoorAirHumidityRatio = rnd.NextDouble() * 0.01;
        for (int i = 0; i < iUnits.Length; i++)
        {
          iUnits[i].AirFlowRate = iUnits[i].NominalAirFlowRate * rnd.NextDouble();
          iUnits[i].InletAirTemperature = 40 * rnd.NextDouble();
          iUnits[i].InletAirHumidityRatio = 0.02 * rnd.NextDouble();
        }
        writeConsole();
        vrf.UpdateState(false);

        Console.WriteLine();
        iterNum++;
      }
    }

    #endregion

    #region NEDO試験条件のテスト

    public static void NEDOTest1(bool useJIS)
    {
      //冷媒物性計算インスタンス作成
      Refrigerant r410a = new Refrigerant(Refrigerant.Fluid.R410A);

      //室外機作成:ダイキン22.8kWをベースにNEDO試験情報を追加
      VRFSystem vrfSystem;
      if (useJIS)
        vrfSystem = VRFInitializer.MakeOutdoorUnit(VRFInitializer.OutdoorUnitModel.Daikin_VRVX, VRFInitializer.CoolingCapacity.C22_4, 0, false);
      else
      {
        VRFUnit iHex = VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C11_2);
        vrfSystem = new VRFSystem(r410a,
          218 * 1.2 / 60d, 0.26 * 2, -21.04, 6.89, -9.54, 2.63, -10.58, 1.66, //NEDO実試験冷房
          218 * 1.2 / 60d, 0.26 * 2, 23.32, 7.49, 12.41, 3.88, //NEDO実試験暖房
          //218 * 1.2 / 60d, 0.26 * 2, -22.4, 6.07, -10.1, 1.89, -10.6, 1.55, //JIS冷房
          //218 * 1.2 / 60d, 0.26 * 2, 25.0, 6.32, 11.3, 2.06, //JIS暖房
          7.5, 100, 0.88, 100, 1.00, iHex);
        vrfSystem.MinimumPartialLoadRate = 0.18;
      }

      //室内機リスト
      VRFUnit[] iHexes = new VRFUnit[]
      {
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6)
      };
      vrfSystem.AddIndoorUnit(iHexes);
      vrfSystem.MinEvaporatingTemperature = 5;
      vrfSystem.MaxEvaporatingTemperature = 20;
      vrfSystem.MinCondensingTemperature = 30;
      vrfSystem.ControlThermoOffWithSensibleHeat = false; //全熱基準でサーモを制御（処理負荷を完全に合わせるため）

      //各テストケースの室内機負荷と吸込温湿度条件リスト**************************
      //冷房時の室内機負荷
      Dictionary<string, double[]> pLoadsC = new Dictionary<string, double[]>()
      {
        {"NOM", new double[]{ 5.6, 5.6, 5.6, 5.6 } },
        {"ML", new double[]{ 2.8, 2.8, 2.8, 2.8 } },
        {"MLMT", new double[]{ 2.8, 2.8, 2.8, 2.8 } },
        {"22_1", new double[]{ 5.20,  5.28,  5.26,  5.30 } },
        {"22_2", new double[]{ 5.12,  5.37,  5.26,  5.17 } },
        {"24", new double[]{ 2.43,  2.47,  2.44,  2.21 } },
        {"28", new double[]{ 3.12,  2.50,  2.48,  2.49 } },
        {"34", new double[]{ 5.23,  1.60,  1.29,  1.53 } },
        {"35", new double[]{ 2.77,  2.68,  2.64,  2.74 } },
        {"23", new double[]{ 3.97, 3.91, 3.96, 3.97 } },
        {"25", new double[]{ 1.11, 1.08, 1.07, 1.16 } },
        {"25_2", new double[]{ 1.05, 1.04, 1.18, 1.12 } },
        {"26", new double[]{ 0.59, 0.54, 0.52, 0.60 } },
        {"26_2", new double[]{ 0.45, 0.45, 0.54, 0.51 } },
        {"27", new double[]{ 5.15, 5.27, 5.12, 5.22 } },
        {"29", new double[]{ 1.09, 1.08, 1.07, 1.07 } },
        {"30", new double[]{ 5.39, 5.46, 5.43, 5.49 } },
        {"31", new double[]{ 2.80, 2.72, 2.80, 2.85 } },
        {"32", new double[]{ 1.38, 1.40, 1.44, 1.45 } },
        {"33", new double[]{ 2.06, 2.10, 3.83, 2.13 } }
      };

      //冷房時の室内機吸込乾球温度
      Dictionary<string, double[]> iDBTC = new Dictionary<string, double[]>()
      {
        {"NOM", new double[]{ 27.0, 27.0, 27.0, 27.0 } },
        {"ML", new double[]{ 27.0, 27.0, 27.0, 27.0 } },
        {"MLMT", new double[]{ 27.0, 27.0, 27.0, 27.0 } },
        {"22_1", new double[]{ 27.58, 27.61, 28.11, 27.94 } },
        {"22_2", new double[]{ 26.55, 26.22, 26.75, 26.73 } },
        {"24", new double[]{ 26.28, 26.13, 26.43, 26.71 } },
        {"28", new double[]{ 25.88, 26.39, 26.53, 26.42 } },
        {"34", new double[]{ 27.83, 26.04, 25.44, 26.06 } },
        {"35", new double[]{ 22.23, 26.12, 25.69, 22.09 } },
        {"23", new double[]{ 26.54, 26.52, 26.74, 26.68 } },
        {"25", new double[]{ 25.91, 25.96, 25.48, 25.90 } },
        {"25_2", new double[]{ 26.84, 26.89, 26.29, 26.94 } },
        {"26", new double[]{ 25.72, 25.76, 25.35, 25.82 } },
        {"26_2", new double[]{ 26.69, 26.71, 26.28, 26.76 } },
        {"27", new double[]{ 26.61, 26.49, 26.73, 26.82 } },
        {"29", new double[]{ 25.84, 25.94, 25.47, 25.94 } },
        {"30", new double[]{ 25.50, 25.36, 25.74, 25.74 } },
        {"31", new double[]{ 21.69, 21.58, 21.45, 21.56 } },
        {"32", new double[]{ 20.73, 20.86, 20.48, 20.76 } },
        {"33", new double[]{ 26.08, 26.04, 26.70, 26.06 } }
      };

      //冷房時の室内機吸込湿球温度
      Dictionary<string, double[]> iWBTC = new Dictionary<string, double[]>()
      {
        {"NOM", new double[]{ 19.0, 19.0, 19.0, 19.0 } },
        {"ML", new double[]{ 19.0, 19.0, 19.0, 19.0 } },
        {"MLMT", new double[]{ 19.0, 19.0, 19.0, 19.0 } },
        {"22_1", new double[]{ 19.11, 19.15, 19.60, 19.24 } },
        {"22_2", new double[]{ 18.61, 18.42, 18.67, 18.61 } },
        {"24", new double[]{ 18.69, 18.57, 18.64, 18.85 } },
        {"28", new double[]{ 18.00, 18.85, 18.87, 18.85 } },
        {"34", new double[]{ 19.34, 18.54, 18.15, 18.57 } },
        {"35", new double[]{ 15.50, 18.57, 18.25, 15.35 } },
        {"23", new double[]{ 18.80, 18.78, 18.89, 18.82 } },
        {"25", new double[]{ 18.70, 18.68, 18.28, 18.56 } },
        {"25_2", new double[]{ 19.27, 19.25, 18.85, 19.29 } },
        {"26", new double[]{ 18.47, 18.40, 18.20, 18.49 } },
        {"26_2", new double[]{ 19.01, 19.02, 18.65, 18.96 } },
        {"27", new double[]{ 18.64, 18.56, 18.64, 18.69 } },
        {"29", new double[]{ 18.64, 18.57, 18.30, 18.63 } },
        {"30", new double[]{ 17.30, 17.21, 17.31, 17.34 } },
        {"31", new double[]{ 15.15, 15.02, 14.86, 14.94 } },
        {"32", new double[]{ 14.54, 14.51, 14.30, 14.48 } },
        {"33", new double[]{ 18.62, 18.62, 18.83, 18.56 } }
      };

      //暖房時の室内機負荷
      Dictionary<string, double[]> pLoadsH = new Dictionary<string, double[]>()
      {
        {"NOM", new double[]{ 6.25, 6.25, 6.25, 6.25 } },
        {"ML", new double[]{ 2.83, 2.83, 2.83, 2.83 } },
        {"8_1", new double[]{ 5.79, 5.73, 5.89, 5.90 } },
        {"8_2", new double[]{ 5.85, 5.70, 5.85, 5.88 } },
        {"10", new double[]{ 3.14, 3.09, 3.11, 3.07 } },
        {"20", new double[]{ 2.03, 2.02, 5.96, 2.00 } },
        {"21", new double[]{ 3.12, 3.12, 3.07, 3.10 } },
        {"9", new double[]{ 4.54, 4.56, 4.58, 4.52 } },
        {"11", new double[]{ 1.52, 1.52, 1.48, 1.54 } },
        {"12", new double[]{ 0.89, 0.81, 0.84, 0.82 } },
        {"13", new double[]{ 4.56, 4.64, 4.47, 4.50 } },
        {"13.5", new double[]{ 3.96, 3.94, 3.97, 3.97 } },
        {"14", new double[]{ 3.15, 3.12, 3.23, 3.12 } },
        {"15", new double[]{ 1.51, 1.50, 1.47, 1.45 } },
        {"16", new double[]{ 5.75, 5.62, 5.71, 5.71 } },
        {"17", new double[]{ 3.42, 3.17, 3.32, 3.24 } },
        {"18", new double[]{ 1.68, 1.82, 1.68, 1.81 } },
        {"19", new double[]{ 2.47, 2.52, 4.54, 2.56 } }
      };

      //暖房時の室内機吸込乾球温度
      Dictionary<string, double[]> iDBTH = new Dictionary<string, double[]>()
       {
        {"NOM", new double[]{ 20.00, 20.00, 20.00, 20.00 } },
        {"ML", new double[]{ 20.00, 20.00, 20.00, 20.00 } },
        {"8_1", new double[]{ 19.62, 19.48, 19.08, 19.34 } },
        {"8_2", new double[]{ 19.82, 19.89, 19.77, 20.04 } },
        {"10", new double[]{ 19.22, 19.14, 19.09, 19.53 } },
        {"20", new double[]{ 21.77, 21.34, 19.76, 21.52 } },
        {"21", new double[]{ 18.53, 24.11, 23.94, 19.28 } },
        {"9", new double[]{ 18.88, 19.08, 19.25, 19.75 } },
        {"11", new double[]{ 20.41, 20.31, 20.26, 20.14 } },
        {"12", new double[]{ 19.32, 19.29, 19.24, 19.79 } },
        {"13", new double[]{ 13.13, 12.91, 13.03, 13.15 } },
        {"13.5", new double[]{ 19.51, 19.51, 19.35, 19.66 } },
        {"14", new double[]{ 19.22, 19.32, 19.02, 19.65 } },
        {"15", new double[]{ 18.94, 19.13, 18.90, 19.32 } },
        {"16", new double[]{ 23.80, 23.83, 23.46, 23.68 } },
        {"17", new double[]{ 23.73, 23.82, 23.73, 24.21 } },
        {"18", new double[]{ 24.12, 24.04, 24.04, 24.26 } },
        {"19", new double[]{ 19.68, 19.56, 19.23, 19.80 } }
      };

      //暖房時の室内機吸込湿球温度
      Dictionary<string, double[]> iWBTH = new Dictionary<string, double[]>()
       {
        {"NOM", new double[]{ 16.00, 16.00, 16.00, 16.00 } },
        {"ML", new double[]{ 16.00, 16.00, 16.00, 16.00 } },
        {"8_1", new double[]{ 15.34, 15.25, 14.89, 15.07 } },
        {"8_2", new double[]{ 15.41, 15.55, 15.40, 15.59 } },
        {"10", new double[]{ 14.47, 14.49, 14.36, 14.68 } },
        {"20", new double[]{ 16.22, 15.91, 15.39, 15.93 } },
        {"21", new double[]{ 14.00, 18.78, 18.60, 14.48 } },
        {"9", new double[]{ 14.53, 14.70, 14.72, 15.06 } },
        {"11", new double[]{ 15.15, 15.07, 14.94, 14.82 } },
        {"12", new double[]{ 14.19, 14.15, 14.08, 14.47 } },
        {"13", new double[]{ 10.74, 10.57, 10.75, 10.82 } },
        {"13.5", new double[]{ 14.88, 14.95, 14.74, 14.94 } },
        {"14", new double[]{ 14.44, 14.55, 14.26, 14.72 } },
        {"15", new double[]{ 14.00, 14.16, 13.94, 14.25 } },
        {"16", new double[]{ 18.93, 19.06, 18.65, 18.82 } },
        {"17", new double[]{ 18.36, 18.55, 18.38, 18.73 } },
        {"18", new double[]{ 18.45, 18.34, 18.36, 18.44 } },
        {"19", new double[]{ 14.71, 14.64, 14.75, 14.72 } }
      };

      //冷房テスト********************************************
      Console.WriteLine("Cooling mode test");
      for (int i = 0; i < 4; i++) vrfSystem.SetIndoorUnitMode(i, VRFUnit.Mode.Cooling);
      vrfSystem.CurrentMode = VRFSystem.Mode.Cooling;
      foreach (string key in pLoadsC.Keys)
      {
        //外気条件
        bool midcnd = (key == "27" || key == "28" || key == "29" || key == "MLMT"); //中温条件
        vrfSystem.OutdoorAirDrybulbTemperature = (midcnd ? 29.0 : 35.0);
        vrfSystem.OutdoorAirHumidityRatio = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(
          (midcnd ? 29.0 : 35.0), (midcnd ? 19.0 : 24.0), 101.325);

        //室内機条件の設定
        double loadSum = 0;
        for (int i = 0; i < 4; i++)
        {
          loadSum += pLoadsC[key][i];
          //吸込空気状態
          double dbt_i = iDBTC[key][i];
          double hrt_i = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(dbt_i, iWBTC[key][i], 101.325);
          vrfSystem.SetIndoorUnitInletAirState(i, dbt_i, hrt_i);
          //給気温度
          iHexes[i].CurrentMode = pLoadsC[key][i] == 0 ? VRFUnit.Mode.ThermoOff : VRFUnit.Mode.Cooling;
          iHexes[i].SolveHeatLoad(-pLoadsC[key][i], iHexes[i].NominalAirFlowRate, dbt_i, hrt_i, false);
          vrfSystem.SetIndoorUnitSetpointTemperature(i, iHexes[i].OutletAirTemperature);
          vrfSystem.SetIndoorUnitSetpointHumidityRatio(i, iHexes[i].OutletAirHumidityRatio);
        }
        vrfSystem.UpdateState();

        //書き出し
        double ttlHeat = 0;
        double ssbHeat = 0;
        for (int i = 0; i < 4; i++)
        {
          ttlHeat -= iHexes[i].HeatTransfer;
          ssbHeat -= iHexes[i].SensibleHeatTransfer;
        }
        Console.WriteLine(
          key + "," +
          loadSum.ToString("F2") + "," +
          ttlHeat.ToString("F2") + "," +
          vrfSystem.PartialLoadRate.ToString("F3") + "," +
          (0.001 * vrfSystem.CompressorInletPressure).ToString("F3") + "," +
          (0.001 * vrfSystem.CompressorOutletPressure).ToString("F3") + "," +
          vrfSystem.CompressorElectricity.ToString("F2") + "," +
          (ssbHeat / ttlHeat).ToString("F3"));
      }

      Console.WriteLine();

      //暖房テスト********************************************
      Console.WriteLine("Heating mode test");
      for (int i = 0; i < 4; i++) vrfSystem.SetIndoorUnitMode(i, VRFUnit.Mode.Heating);
      vrfSystem.CurrentMode = VRFSystem.Mode.Heating;
      foreach (string key in pLoadsH.Keys)
      {
        //外気条件
        bool cldWin = (key == "13" || key == "13.5" || key == "14" || key == "15"); //厳寒条件
        vrfSystem.OutdoorAirDrybulbTemperature = (cldWin ? 2.0 : 7.0);
        vrfSystem.OutdoorAirHumidityRatio = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(
          (cldWin ? 2.0 : 7.0), (cldWin ? 1.0 : 6.0), 101.325);

        //室内機条件の設定
        double loadSum = 0;
        for (int i = 0; i < 4; i++)
        {
          loadSum += pLoadsH[key][i];
          //吸込空気状態
          double dbt_i = iDBTH[key][i];
          double hrt_i = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(dbt_i, iWBTH[key][i], 101.325);
          vrfSystem.SetIndoorUnitInletAirState(i, dbt_i, hrt_i);
          //給気温度
          iHexes[i].CurrentMode = pLoadsH[key][i] == 0 ? VRFUnit.Mode.ThermoOff : VRFUnit.Mode.Heating;
          iHexes[i].SolveHeatLoad(pLoadsH[key][i], iHexes[i].NominalAirFlowRate, dbt_i, hrt_i, false);
          vrfSystem.SetIndoorUnitSetpointTemperature(i, iHexes[i].OutletAirTemperature);
          vrfSystem.SetIndoorUnitSetpointHumidityRatio(i, iHexes[i].OutletAirHumidityRatio);
        }
        vrfSystem.UpdateState();

        //書き出し
        double ttlHeat = 0;
        double ssbHeat = 0;
        for (int i = 0; i < 4; i++)
        {
          ttlHeat += iHexes[i].HeatTransfer;
          ssbHeat += iHexes[i].SensibleHeatTransfer;
        }
        Console.WriteLine(
          key + "," +
          loadSum.ToString("F2") + "," +
          ttlHeat.ToString("F2") + "," +
          vrfSystem.PartialLoadRate.ToString("F3") + "," +
          (0.001 * vrfSystem.CompressorInletPressure).ToString("F3") + "," +
          (0.001 * vrfSystem.CompressorOutletPressure).ToString("F3") + "," +
          vrfSystem.CompressorElectricity.ToString("F2") + "," +
          (ssbHeat / ttlHeat).ToString("F3") + "," + 
          vrfSystem.OutdoorUnit_H.DefrostLoad
          );
      }

      Console.WriteLine();

    }

    /// <summary></summary>
    /// <param name="setting">モデル構成ファイル ex. vrfsetting.csv</param>
    /// <param name="boundary">境界条件ファイル ex. boundary_C.csv</param>
    /// <param name="isCooling">冷房か否か</param>
    public static void NEDOTest2(string setting, string boundary, bool isCooling)
    {

      //**モデル作成処理**************************************************************************************************************
      //冷媒物性計算インスタンス作成
      Refrigerant r410a = new Refrigerant(Refrigerant.Fluid.R410A);

      VRFSystem vrfSystem;
      VRFUnit[] iHexes;
      using (StreamReader sReader = new StreamReader(setting))
      {
        //室外機初期化用の室内機を読み込む
        sReader.ReadLine(); //ヘッダ
        string[] buff = sReader.ReadLine().Split(',');
        VRFUnit iHex = VRFSystem.MakeIndoorUnit(
          double.Parse(buff[0]) * 1.2 / 60d,
          double.Parse(buff[1]), -double.Parse(buff[2]), //冷房能力
          double.Parse(buff[3]), double.Parse(buff[4])); //暖房能力

        //室外機初期化
        sReader.ReadLine(); //ヘッダ
        buff = sReader.ReadLine().Split(',');
        vrfSystem = new VRFSystem(r410a,
          double.Parse(buff[0]) * 1.2 / 60d, double.Parse(buff[1]), //冷房風量、ファン消費電力
          -double.Parse(buff[2]), double.Parse(buff[3]), //冷房定格 
          -double.Parse(buff[4]), double.Parse(buff[5]), //冷房中間
          -double.Parse(buff[6]), double.Parse(buff[7]), //冷房中間中温
          double.Parse(buff[9]) * 1.2 / 60d, double.Parse(buff[10]), //暖房風量、ファン消費電力
          double.Parse(buff[11]), double.Parse(buff[12]), //暖房定格
          double.Parse(buff[13]), double.Parse(buff[14]), //暖房中間
          7.5, 100, double.Parse(buff[8]), 100, double.Parse(buff[15]), iHex);
        vrfSystem.MinimumPartialLoadRate = double.Parse(buff[16]);
        if (17 < buff.Length) vrfSystem.NumberOfOutdoorUnitDivisions = int.Parse(buff[17]);

        //室内機初期化
        sReader.ReadLine(); //ヘッダ
        string line;
        List<VRFUnit> ihs = new List<VRFUnit>();
        while ((line = sReader.ReadLine()) != null)
        {
          buff = line.Split(',');
          VRFUnit ih;
          if (5 < buff.Length && bool.Parse(buff[5]))
          {
            ih = new VRFUnit(
              double.Parse(buff[0]) * 1.2 / 60d, 
              6.0, -double.Parse(buff[2]), 33, 0.0220, 99, double.Parse(buff[1]), 
              46.0, double.Parse(buff[4]), 7, 0.0053, double.Parse(buff[3]));
          }
          else
          {
            ih = VRFSystem.MakeIndoorUnit(
              double.Parse(buff[0]) * 1.2 / 60d,
              double.Parse(buff[1]), -double.Parse(buff[2]),
              double.Parse(buff[3]), double.Parse(buff[4]));
          }
          ihs.Add(ih);
        }
        iHexes = ihs.ToArray();
      }

      vrfSystem.AddIndoorUnit(iHexes);
      vrfSystem.MinEvaporatingTemperature = 6;
      vrfSystem.MaxEvaporatingTemperature = 11;
      vrfSystem.MinCondensingTemperature = 41;
      vrfSystem.MaxCondensingTemperature = 46;
      vrfSystem.ControlThermoOffWithSensibleHeat = false; //全熱基準でサーモを制御（処理負荷を完全に合わせるため）

      //計算の実行
      if (isCooling) exeCooling(vrfSystem, iHexes, boundary);
      else exeHeating(vrfSystem, iHexes, boundary);
    }

    private static void exeCooling
      (VRFSystem vrfSystem, VRFUnit[] iHexes, string boundaryFile)
    {
      Console.WriteLine("Cooling mode test");
      vrfSystem.CurrentMode = VRFSystem.Mode.Cooling;
      for (int i = 0; i < iHexes.Length; i++)
        vrfSystem.SetIndoorUnitMode(i, VRFUnit.Mode.Cooling);
      using (StreamReader sReader = new StreamReader(boundaryFile))
      using (StreamWriter sWriter = new StreamWriter(boundaryFile.Remove(boundaryFile.Length - 4) + "_result.csv", false, Encoding.UTF8))
      {
        //最初の2行はヘッダ
        sReader.ReadLine();
        sReader.ReadLine();

        //書き出しファイルのヘッダ
        sWriter.Write("熱負荷[kW],処理負荷[kW],負荷率（実質）[-],低圧[MPa],高圧[MPa],圧縮機消費電力[kW],室外機ファン消費電力[kW],室内機消費電力（合算）[kW],圧縮機COP[-],システムCOP[-]");
        for (int i = 0; i < iHexes.Length; i++) sWriter.Write(",室内機" + (i + 1) + "処理負荷[kW],室内機" + (i + 1) + "顕熱比[-]");
        sWriter.WriteLine();

        //CSVの最終行まで繰り返す
        string line;
        while ((line = sReader.ReadLine()) != null)
        {
          string[] buff = line.Split(',');

          //外気条件設定
          double oaDbt = double.Parse(buff[0]);
          vrfSystem.OutdoorAirDrybulbTemperature = oaDbt;
          vrfSystem.OutdoorAirHumidityRatio =
            MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(oaDbt, double.Parse(buff[1]), 101.325);

          //室内機条件の設定
          double loadSum = 0;
          for (int i = 0; i < iHexes.Length; i++)
          {
            //吸込空気状態
            double dbt_i = double.Parse(buff[3 * i + 3]);
            double hrt_i = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(dbt_i, double.Parse(buff[3 * i + 4]), 101.325);
            vrfSystem.SetIndoorUnitInletAirState(i, dbt_i, hrt_i);
            //給気温度//処理負荷から逆算
            double cLoad = double.Parse(buff[3 * i + 2]);
            loadSum += cLoad;
            iHexes[i].SolveHeatLoad(-cLoad, iHexes[i].NominalAirFlowRate, dbt_i, hrt_i, false);
            vrfSystem.SetIndoorUnitSetpointTemperature(i, iHexes[i].OutletAirTemperature);
            vrfSystem.SetIndoorUnitSetpointHumidityRatio(i, iHexes[i].OutletAirHumidityRatio);
          }
          vrfSystem.UpdateState();

          //書き出し
          double ttlHeat = 0;
          //処理負荷を集計
          for (int i = 0; i < iHexes.Length; i++)
            ttlHeat -= iHexes[i].HeatTransfer;


          //vrfSystem.OutdoorUnit_C.FanOperatingRate
          //系統全体
          sWriter.Write(
            loadSum.ToString("F2") + "," +
            ttlHeat.ToString("F2") + "," +
            vrfSystem.PartialLoadRate.ToString("F3") + "," +
            (0.001 * vrfSystem.CompressorInletPressure).ToString("F3") + "," +
            (0.001 * vrfSystem.CompressorOutletPressure).ToString("F3") + "," +
            vrfSystem.CompressorElectricity.ToString("F2") + "," +
            vrfSystem.OutdoorUnitFanElectricity.ToString("F2") + "," +
            vrfSystem.IndoorUnitFanElectricity.ToString("F2") + "," +
            (loadSum == 0 ? 0 : (loadSum / vrfSystem.CompressorElectricity).ToString("F2")) + "," +
            (loadSum == 0 ? 0 : (loadSum / (vrfSystem.CompressorElectricity + vrfSystem.OutdoorUnitFanElectricity + vrfSystem.IndoorUnitFanElectricity)).ToString("F2"))
            );
          //室内機ごと
          for (int i = 0; i < iHexes.Length; i++)
          {
            double shf = iHexes[i].HeatTransfer == 0 ? 0 : (iHexes[i].SensibleHeatTransfer / iHexes[i].HeatTransfer);
            sWriter.Write(
              "," + (-iHexes[i].HeatTransfer).ToString("F2") +
              "," + shf.ToString("F3"));
          }
          sWriter.WriteLine();

        }
      }
    }

    private static void exeHeating
      (VRFSystem vrfSystem, VRFUnit[] iHexes, string boundaryFile)
    {
      Console.WriteLine("Heating mode test");
      vrfSystem.CurrentMode = VRFSystem.Mode.Heating;
      for (int i = 0; i < iHexes.Length; i++)
        vrfSystem.SetIndoorUnitMode(i, VRFUnit.Mode.Heating);
      using (StreamReader sReader = new StreamReader(boundaryFile))
      using (StreamWriter sWriter = new StreamWriter(boundaryFile.Remove(boundaryFile.Length - 4) + "_result.csv", false, Encoding.UTF8))
      {
        //最初の2行はヘッダ
        sReader.ReadLine();
        sReader.ReadLine();

        //書き出しファイルのヘッダ
        sWriter.Write("熱負荷[kW],処理負荷[kW],負荷率（実質）[-],低圧[MPa],高圧[MPa],圧縮機消費電力[kW],室外機ファン消費電力[kW],室内機消費電力（合算）[kW],圧縮機COP[-],システムCOP[-]");
        for (int i = 0; i < iHexes.Length; i++) sWriter.Write(",室内機" + (i + 1) + "処理負荷[kW]");
        sWriter.WriteLine();

        //CSVの最終行まで繰り返す
        string line;
        while ((line = sReader.ReadLine()) != null)
        {
          string[] buff = line.Split(',');

          //外気条件設定
          double oaDbt = double.Parse(buff[0]);
          vrfSystem.OutdoorAirDrybulbTemperature = oaDbt;
          vrfSystem.OutdoorAirHumidityRatio =
            MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(oaDbt, double.Parse(buff[1]), 101.325);

          //室内機条件の設定
          double loadSum = 0;
          for (int i = 0; i < iHexes.Length; i++)
          {
            //吸込空気状態
            double dbt_i = double.Parse(buff[3 * i + 3]);
            double hrt_i = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(dbt_i, double.Parse(buff[3 * i + 4]), 101.325);
            vrfSystem.SetIndoorUnitInletAirState(i, dbt_i, hrt_i);
            //給気温度//処理負荷から逆算
            double hLoad = double.Parse(buff[3 * i + 2]);
            loadSum += hLoad;
            iHexes[i].SolveHeatLoad(hLoad, iHexes[i].NominalAirFlowRate, dbt_i, hrt_i, false);
            vrfSystem.SetIndoorUnitSetpointTemperature(i, iHexes[i].OutletAirTemperature);
            vrfSystem.SetIndoorUnitSetpointHumidityRatio(i, iHexes[i].OutletAirHumidityRatio);
          }
          vrfSystem.UpdateState();

          //書き出し
          double ttlHeat = 0;
          //処理負荷を集計
          for (int i = 0; i < iHexes.Length; i++)
            ttlHeat += iHexes[i].HeatTransfer;

          //系統全体
          sWriter.Write(
            loadSum.ToString("F2") + "," +
            ttlHeat.ToString("F2") + "," +
            vrfSystem.PartialLoadRate.ToString("F3") + "," +
            (0.001 * vrfSystem.CompressorInletPressure).ToString("F3") + "," +
            (0.001 * vrfSystem.CompressorOutletPressure).ToString("F3") + "," +
            vrfSystem.CompressorElectricity.ToString("F2") + "," +
            vrfSystem.OutdoorUnitFanElectricity.ToString("F2") + "," +
            vrfSystem.IndoorUnitFanElectricity.ToString("F2") + "," +
            (loadSum == 0 ? 0 : (loadSum / vrfSystem.CompressorElectricity).ToString("F2")) + "," +
            (loadSum == 0 ? 0 : (loadSum / (vrfSystem.CompressorElectricity + vrfSystem.OutdoorUnitFanElectricity + vrfSystem.IndoorUnitFanElectricity)).ToString("F2"))
            );
          //室内機ごと
          for (int i = 0; i < iHexes.Length; i++)
            sWriter.Write("," + (-iHexes[i].HeatTransfer).ToString("F2"));
          sWriter.WriteLine();

        }
      }
    }

    public static void NEDOTest3()
    {
      makeVRFSystem(out VRFSystem v1, out VRFSystem v2, out VRFSystem v3, out VRFUnit[] iUnits);

      using (StreamReader sReader = new StreamReader("heatload.csv"))
      using (StreamWriter sWriter = new StreamWriter("result.csv"))
      {
        string line;
        sReader.ReadLine();//ヘッダ
        sWriter.Write("Date,Time,State,OADBT,OAHRT");
        for (int i = 0; i < iUnits.Length; i++)
          sWriter.Write(",Unit-" + i + " Tsp[C],Unit-" + i + " xsp[g/kg],Unit-" + i + " QS[W],Unit-" + i + " QL[W]");
        sWriter.WriteLine();

        while ((line = sReader.ReadLine()) != null)
        {
          string[] buff = line.Split(',');

          //冷暖発停
          if (buff[2] == "heating")
          {
            v1.CurrentMode = VRFSystem.Mode.Heating;
            v1.SetIndoorUnitMode(VRFUnit.Mode.Heating);
          }
          else if (buff[2] == "cooling")
          {
            v1.CurrentMode = VRFSystem.Mode.Cooling;
            v1.SetIndoorUnitMode(VRFUnit.Mode.Cooling);
          }
          else v1.CurrentMode = VRFSystem.Mode.ShutOff;

          //吸込温湿度と負荷を設定
          for (int i = 0; i < iUnits.Length - 1; i++)
          {
            int ofst = 5 + i * 5;
            iUnits[i].InletAirTemperature = double.Parse(buff[ofst + 0]);
            iUnits[i].InletAirHumidityRatio = double.Parse(buff[ofst + 1]);
            double sLoad = 0.001 * double.Parse(buff[ofst + 3]);
            iUnits[i].OutletAirSetpointTemperature = iUnits[i].InletAirTemperature + sLoad / (iUnits[i].NominalAirFlowRate * 1.06);
          }

          //外調機給気温湿度を設定
          int month = int.Parse(buff[0].Split('/')[0]);
          int day = int.Parse(buff[0].Split('/')[1]);
          VRFUnit oUnit = iUnits[iUnits.Length - 1];
          oUnit.InletAirTemperature = double.Parse(buff[3]);
          oUnit.InletAirHumidityRatio = double.Parse(buff[4]);
          if (6 <= month && month <= 9)
          {
            oUnit.OutletAirSetpointTemperature = 26;
            oUnit.OutletAirSetpointHumidityRatio = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(26, 50, 101.325);
          }
          else if (month == 12 || month <= 3)
          {
            oUnit.OutletAirSetpointTemperature = 22;
            oUnit.OutletAirSetpointHumidityRatio = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(22, 40, 101.325);
          }
          else
          {
            oUnit.OutletAirSetpointTemperature = 24;
            oUnit.OutletAirSetpointHumidityRatio = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(24, 50, 101.325);
          }

          //VRFシステムを更新
          v1.UpdateState();

          //書き出し
          sWriter.Write(buff[0] + "," + buff[1] + "," + buff[2] + "," + buff[3] + "," + buff[4]);
          for (int i = 0; i < iUnits.Length; i++)
          {
            sWriter.Write("," +
              iUnits[i].OutletAirTemperature + "," +
              1000 * iUnits[i].OutletAirHumidityRatio + "," +
              1000 * iUnits[i].SensibleHeatTransfer + "," +
              1000 * iUnits[i].LatentHeatTransfer);
          }
          sWriter.WriteLine();
        }
      }
    }

    private static void makeVRFSystem
      (out VRFSystem vs_1, out VRFSystem vs_2, out VRFSystem vs_3, out VRFUnit[] iUnits)
    {
      //冷媒
      Refrigerant r410a = new Refrigerant(Refrigerant.Fluid.R410A);

      //初期化用室内機
      const double IHEX_CAP_C = 14.0;
      const double IHEX_CAP_H = 16.0;
      const double NOM_IHEX_AFLOW = 34.5 * 1.2 / 60d;
      VRFUnit iHex = VRFSystem.MakeIndoorUnit(NOM_IHEX_AFLOW, 0, -IHEX_CAP_C, 0, IHEX_CAP_H);

      //室外機
      vs_1 = new VRFSystem(
        r410a,
        723d * 1.2 / 60, 3.21, -106.4, 33.4, -47.9, 12.9, -49.2, 8.6,
        723d * 1.2 / 60, 3.21, 118.7, 39.8, 53.5, 11.3,
        7.5, 100, 0.9, 100, 0.9, iHex);

      vs_2 = new VRFSystem(
        r410a,
        497d * 1.2 / 60, 2.21, -73.1, 23.0, -32.9, 8.9, -33.8, 5.9,
        497d * 1.2 / 60, 2.21, 81.6, 27.3, 36.7, 7.8,
        7.5, 100, 0.9, 100, 0.9, iHex);

      vs_3 = new VRFSystem(
        r410a,
        226d * 1.2 / 60, 1.01, -33.3, 10.5, -15.0, 4.0, -15.4, 2.7,
        226d * 1.2 / 60, 1.01, 37.2, 12.5, 16.7, 3.5,
        7.5, 100, 0.9, 100, 0.9, iHex);

      //蒸発・凝縮温度範囲設定
      vs_1.MaxEvaporatingTemperature = vs_2.MaxEvaporatingTemperature = vs_3.MaxEvaporatingTemperature = 11;
      vs_1.MinEvaporatingTemperature = vs_2.MinEvaporatingTemperature = vs_3.MinEvaporatingTemperature = 6;
      vs_1.MaxCondensingTemperature = vs_2.MaxCondensingTemperature = vs_3.MaxCondensingTemperature = 46;
      vs_1.MinCondensingTemperature = vs_2.MinCondensingTemperature = vs_3.MinCondensingTemperature = 41;

      VRFUnit w_1 = VRFSystem.MakeIndoorUnit(1681d * 1.2 / 3600, 0.072, -(7.40 + 1.29), 95, 0.064, 10.82);
      VRFUnit i_1 = VRFSystem.MakeIndoorUnit(1581d * 1.2 / 3600, 0.067, -(6.96 + 1.22), 95, 0.060, 10.18);
      VRFUnit i_2 = VRFSystem.MakeIndoorUnit(1584d * 1.2 / 3600, 0.067, -(6.97 + 1.22), 95, 0.060, 10.20);
      VRFUnit i_3 = VRFSystem.MakeIndoorUnit(1221d * 1.2 / 3600, 0.052, -(5.38 + 0.94), 95, 0.046, 7.86);
      VRFUnit i_4 = VRFSystem.MakeIndoorUnit(1217d * 1.2 / 3600, 0.052, -(5.36 + 0.94), 95, 0.046, 7.84);
      VRFUnit i_5 = VRFSystem.MakeIndoorUnit(1670d * 1.2 / 3600, 0.071, -(7.35 + 1.29), 95, 0.063, 10.75);
      VRFUnit sw_1 = VRFSystem.MakeIndoorUnit(972d * 1.2 / 3600, 0.041, -(4.28 + 0.75), 95, 0.037, 6.26);
      VRFUnit s_1 = VRFSystem.MakeIndoorUnit(916d * 1.2 / 3600, 0.039, -(4.03 + 0.71), 95, 0.035, 5.90);
      VRFUnit s_2 = VRFSystem.MakeIndoorUnit(916d * 1.2 / 3600, 0.039, -(4.03 + 0.71), 95, 0.035, 5.90);
      VRFUnit s_3 = VRFSystem.MakeIndoorUnit(700d * 1.2 / 3600, 0.030, -(3.08 + 0.54), 95, 0.027, 4.51);
      VRFUnit s_4 = VRFSystem.MakeIndoorUnit(699d * 1.2 / 3600, 0.030, -(3.08 + 0.54), 95, 0.027, 4.50);
      VRFUnit s_5 = VRFSystem.MakeIndoorUnit(967d * 1.2 / 3600, 0.041, -(4.26 + 0.75), 95, 0.037, 6.23);
      VRFUnit oaUnt = new VRFUnit(2570d * 1.2 / 3600, 0.966, -(13.3 + 20.0), 33, 0.0220, 99, 0.966, 46, 19.0 + 9.5, 7, 0.00538, 0.966);
      oaUnt.UseHumidifier = true; //外調機は加湿有り
      iUnits = new VRFUnit[] { w_1, i_1, i_2, i_3, i_4, i_5, sw_1, s_1, s_2, s_3, s_4, s_5, oaUnt };

      vs_1.AddIndoorUnit(iUnits);
      vs_2.AddIndoorUnit(new VRFUnit[] { w_1, i_1, i_2, i_3, i_4, i_5, sw_1, s_1, s_2, s_3, s_4, s_5 });
      vs_3.AddIndoorUnit(new VRFUnit[] { oaUnt });
    }

    #endregion

    #region VRF年間計算

    private static void measureSpeed()
    {
      //ストップウォッチ
      System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
      for (int i = 0; i < 10; i++)
      {
        sw.Reset();
        sw.Start();
        executeAnnualEnergySimulation(false);
        sw.Stop();
        Console.WriteLine(sw.Elapsed);
      }
      Console.WriteLine("END");
      Console.ReadLine();
    }

    private static void makeVRFSystem
      (out VRFSystem vrfSystem, out VRFUnit[] iHexes)
    {
      const double PIPE_FC_C = 0.90; //配管長補正係数
      const double PIPE_FC_H = 0.90; //配管長補正係数
      const double NOM_PIPE_LENGTH = 7.5; //定格配管長
      const double LONG_PIPE_LENGTH_C = 100; //補正係数適用時の配管長
      const double LONG_PIPE_LENGTH_H = 100; //補正係数適用時の配管長
      const double IHEX_CAP_C = 14.0;
      const double IHEX_CAP_H = 16.0;
      const double NOM_IHEX_AFLOW = 34.5 * 1.2 / 60d;
      const double NOM_OHEX_AFLOW = 680 * 1.2 / 60;

      //VRFヒートポンプモデルの作成*************************************************
      Refrigerant r410a = new Refrigerant(Refrigerant.Fluid.R410A);
      VRFUnit iHex = VRFSystem.MakeIndoorUnit(NOM_IHEX_AFLOW, 0, -IHEX_CAP_C, 0, IHEX_CAP_H);
      iHex.CurrentMode = VRFUnit.Mode.Heating;

      //室外機を用意
      const double RCAP = 1.0; //PARAM
      vrfSystem = new VRFSystem(
        r410a,
        NOM_OHEX_AFLOW, 0.208 * RCAP, -85.0 * RCAP, 24.8 * RCAP, -38.3 * RCAP, 7.58 * RCAP, -40.2 * RCAP, 6.08 * RCAP,
        NOM_OHEX_AFLOW, 0.208 * RCAP, 90.0 * RCAP, 25.8 * RCAP, 42.8 * RCAP, 8.33 * RCAP,
        NOM_PIPE_LENGTH, LONG_PIPE_LENGTH_C, PIPE_FC_C, LONG_PIPE_LENGTH_H, PIPE_FC_H, iHex);
      vrfSystem.CurrentMode = VRFSystem.Mode.Heating;
      vrfSystem.MinimumPartialLoadRate = 0.05;
      vrfSystem.MaxEvaporatingTemperature = 10; //PARAM
      vrfSystem.MinCondensingTemperature = 46; //PARAM
      vrfSystem.IndoorUnitHeight = 0; //PARAM
      vrfSystem.UseWaterSpray = false; //PARAM
      vrfSystem.PipeLength = 100 + vrfSystem.IndoorUnitHeight; //PARAM

      //室内機を追加
      iHexes = new VRFUnit[]
      {
        VRFSystem.MakeIndoorUnit(30.0 * 1.2 / 60d, 0.128, -9.0, 0.128, 10.0),
        VRFSystem.MakeIndoorUnit(23.5 * 1.2 / 60d, 0.086, -8.0, 0.086, 9.0),
        VRFSystem.MakeIndoorUnit(23.5 * 1.2 / 60d, 0.086, -8.0, 0.086, 9.0),
        VRFSystem.MakeIndoorUnit(22.0 * 1.2 / 60d, 0.072, -7.1, 0.072, 8.0),
        VRFSystem.MakeIndoorUnit(22.0 * 1.2 / 60d, 0.072, -7.1, 0.072, 8.0),
        VRFSystem.MakeIndoorUnit(30.0 * 1.2 / 60d, 0.128, -9.0, 0.128, 10.0),
        VRFSystem.MakeIndoorUnit(15.5 * 1.2 / 60d, 0.043, -5.6, 0.043, 6.3),
        VRFSystem.MakeIndoorUnit(14.5 * 1.2 / 60d, 0.037, -4.5, 0.037, 5.0),
        VRFSystem.MakeIndoorUnit(14.5 * 1.2 / 60d, 0.037, -4.5, 0.037, 5.0),
        VRFSystem.MakeIndoorUnit(12.5 * 1.2 / 60d, 0.028, -3.6, 0.028, 4.0),
        VRFSystem.MakeIndoorUnit(12.5 * 1.2 / 60d, 0.028, -3.6, 0.028, 4.0),
        VRFSystem.MakeIndoorUnit(15.5 * 1.2 / 60d, 0.043, -5.6, 0.043, 6.3),
      };
      vrfSystem.AddIndoorUnit(iHexes);
      for (int i = 0; i < vrfSystem.IndoorUnitNumber; i++)
        vrfSystem.SetIndoorUnitMode(i, VRFUnit.Mode.Heating);
    }

    /// <summary>年間エネルギー計算（基準条件）</summary>
    private static void executeAnnualEnergySimulation(bool outputConsole)
    {
      VRFSystem vrfSystem;
      VRFUnit[] iHexes;
      makeVRFSystem(out vrfSystem, out iHexes);

      //年間計算の実行********************************************
      using (StreamReader sReader = new StreamReader("annualCalc.csv"))
      using (StreamWriter sWriter = new StreamWriter("annualCalcEnergy.csv"))
      {
        DateTime dtNow = new DateTime(1999, 1, 1, 0, 0, 0);
        sReader.ReadLine();

        //ヘッダ書き出し
        sWriter.Write("Date, Time, Cmp. Electricity, OFan Electricity, IFan Electricity, Partial Load Rate, Compression Ratio, EvpTemp, CndTemp");
        for (int i = 0; i < iHexes.Length; i++)
        {
          sWriter.Write("," + (i + 1) + "_SHLoad");
        }
        sWriter.WriteLine();

        string buff;
        while ((buff = sReader.ReadLine()) != null)
        {
          bool isCooling = (5 <= dtNow.Month && dtNow.Month <= 10);
          vrfSystem.CurrentMode = isCooling ? VRFSystem.Mode.Cooling : VRFSystem.Mode.Heating;
          bool isShutOff = (dtNow.DayOfWeek == DayOfWeek.Saturday || dtNow.DayOfWeek == DayOfWeek.Sunday) || (dtNow.Hour < 7 || 21 <= dtNow.Hour);
          if (isShutOff)
            vrfSystem.CurrentMode = VRFSystem.Mode.ShutOff;

          //5/1に冷房運転に切替
          if (dtNow.Month == 5 && dtNow.Day == 1 && dtNow.Hour == 0)
          {
            vrfSystem.CurrentMode = VRFSystem.Mode.Cooling;
            for (int i = 0; i < vrfSystem.IndoorUnitNumber; i++)
              vrfSystem.SetIndoorUnitMode(i, VRFUnit.Mode.Cooling);
          }
          //11/1に暖房運転に切替
          if (dtNow.Month == 11 && dtNow.Day == 1 && dtNow.Hour == 0)
          {
            vrfSystem.CurrentMode = VRFSystem.Mode.Heating;
            for (int i = 0; i < vrfSystem.IndoorUnitNumber; i++)
              vrfSystem.SetIndoorUnitMode(i, VRFUnit.Mode.Heating);
          }

          string[] buff2 = buff.Split(',');
          vrfSystem.OutdoorAirDrybulbTemperature = double.Parse(buff2[2]);
          vrfSystem.OutdoorAirHumidityRatio = double.Parse(buff2[3]);
          for (int i = 0; i < iHexes.Length; i++)
          {
            double dbt = double.Parse(buff2[4 + i * 5]);
            double hrt = double.Parse(buff2[5 + i * 5]);
            double sLoad = 0.001 * double.Parse(buff2[7 + i * 5]);
            iHexes[i].SolveHeatLoad(sLoad, iHexes[i].NominalAirFlowRate, dbt, hrt, false);
            vrfSystem.SetIndoorUnitSetpointTemperature(i, iHexes[i].OutletAirTemperature);
          }
          vrfSystem.UpdateState();

          //書き出し
          sWriter.Write(dtNow.ToString("MM/dd") + "," + dtNow.ToString("HH:mm"));
          sWriter.Write(
            "," + vrfSystem.CompressorElectricity +
            "," + vrfSystem.OutdoorUnitFanElectricity +
            "," + vrfSystem.IndoorUnitFanElectricity +
            "," + vrfSystem.PartialLoadRate +
            "," + vrfSystem.CompressionRatio +
            "," + vrfSystem.EvaporatingTemperature +
            "," + vrfSystem.CondensingTemperature);
          for (int i = 0; i < iHexes.Length; i++)
          {
            sWriter.Write("," + iHexes[i].SensibleHeatTransfer);
          }
          sWriter.WriteLine();

          dtNow = dtNow.AddHours(1);
          if (dtNow.Hour == 0 && outputConsole) Console.WriteLine(dtNow);
        }
      }

    }

    #endregion

    #region 建物熱負荷計算処理

    private static void executeAnnualSimulation()
    {
      //建物モデル作成
      SimpleHeatGain[] sHGains;
      BuildingThermalModel bModel = makeBuilding(out sHGains);
      Sun sun = new Sun(Sun.City.Tokyo);

      //気象データ読み込み
      double[] dbt, hrt, dnr, dhr, ncr;
      LoadWeatherData("3639999.has", out dbt, out hrt, out dnr, out dhr, out ncr);

      //1ヶ月の助走運転
      DateTime dtNow = new DateTime(1999, 1, 1, 0, 0, 0);
      for (int hour = 0; hour < 720; hour++)
      {
        //外界条件更新
        int hour2 = hour % 24;
        sun.Update(dtNow);
        sun.SetGlobalHorizontalRadiation(dhr[hour2], dnr[hour2]);
        bModel.UpdateOutdoorCondition(dtNow, sun, dbt[hour2], hrt[hour2], ncr[hour2]);

        //制御・スケジュール更新
        controlBuilding(bModel, sHGains);

        //状態更新
        bModel.ForecastHeatTransfer();
        bModel.ForecastWaterTransfer();
        bModel.FixState();

        dtNow = dtNow.AddHours(1);
      }

      //年間計算実行
      using (StreamWriter sWriter = new StreamWriter("annualCalc.csv"))
      {
        sWriter.Write("Date, Time, OADBT, OAHRT");
        for (int i = 0; i < bModel.MultiRoom[0].ZoneNumber; i++)
        {
          ImmutableZone zn = bModel.MultiRoom[0].Zones[i];
          sWriter.Write(",ZN" + (i + 1) + "_Temp.,ZN" + (i + 1) + "_HRatio,ZN" + (i + 1) + "_MRT,ZN" + (i + 1) + "_SHSupplyRate,ZN" + (i + 1) + "_LHSupplyRate");
        }
        sWriter.WriteLine();

        dtNow = new DateTime(1999, 1, 1, 0, 0, 0);
        for (int hour = 0; hour < 8760; hour++)
        {
          //外界条件更新
          sun.Update(dtNow);
          sun.SetGlobalHorizontalRadiation(dhr[hour], dnr[hour]);
          bModel.UpdateOutdoorCondition(dtNow, sun, dbt[hour], hrt[hour], ncr[hour]);
          //sun.SetGlobalHorizontalRadiation(0, 0);
          //bModel.UpdateOutdoorCondition(dtNow, sun, 20, 0, 0);

          //制御・スケジュール更新
          controlBuilding(bModel, sHGains);

          //状態更新
          bModel.ForecastHeatTransfer();
          bModel.ForecastWaterTransfer();
          bModel.FixState();

          //結果書き出し
          sWriter.Write(dtNow.Month + "/" + dtNow.Day + "," + dtNow.Hour + "," + bModel.OutdoorTemperature + "," + bModel.OutdoorHumidityRatio);
          for (int i = 0; i < bModel.MultiRoom[0].ZoneNumber; i++)
          {
            ImmutableZone zn = bModel.MultiRoom[0].Zones[i];
            sWriter.Write(
              "," + zn.Temperature +
              "," + zn.HumidityRatio +
              "," + zn.GetMeanSurfaceTemperature() +
              //"," + (zn.HeatSupply / zn.FloorArea) +
              //"," + (zn.WaterSupply * MoistAir.LatentHeatOfVaporization * 1000 / zn.FloorArea));
              "," + (zn.HeatSupply) +
              "," + (zn.WaterSupply * MoistAir.LatentHeatOfVaporization * 1000));
          }
          sWriter.WriteLine();

          dtNow = dtNow.AddHours(1);
          if (dtNow.Hour == 0) Console.WriteLine(dtNow.ToString()); ;
        }
      }

    }

    private static BuildingThermalModel makeBuilding(out SimpleHeatGain[] sHGains)
    {
      //傾斜面の作成//////////////
      Incline incW = new Incline(Incline.Orientation.W, 0.5 * Math.PI);
      Incline incS = new Incline(Incline.Orientation.S, 0.5 * Math.PI);

      //壁構成を作成////////////////////////
      WallLayer[] exWL = new WallLayer[6];  //外壁一般部分
      exWL[0] = new WallLayer("タイル", 1.3, 2000, 0.010);
      exWL[1] = new WallLayer("セメント・モルタル", 1.5, 1600, 0.025);
      exWL[2] = new WallLayer("コンクリート", 1.6, 2000, 0.150);
      exWL[3] = new WallLayer("押出ポリスチレンフォーム1種", 0.040, 33, 0.025);
      exWL[4] = new AirGapLayer("非密閉中空層", false, 0.05);
      exWL[5] = new WallLayer("石膏ボード", 0.22, 830, 0.008);

      WallLayer[] exbmWL = new WallLayer[4];  //外壁梁部分
      exbmWL[0] = new WallLayer("タイル", 1.3, 2000, 0.010);
      exbmWL[1] = new WallLayer("セメント・モルタル", 1.5, 1600, 0.025);
      exbmWL[2] = new WallLayer("コンクリート", 1.6, 2000, 0.750);
      exbmWL[3] = new WallLayer("押出ポリスチレンフォーム1種", 0.040, 33, 0.025);

      WallLayer[] flWL = new WallLayer[6];  //床・天井
      flWL[0] = new WallLayer("ビニル系床材", 0.190, 2000, 0.003);
      flWL[1] = new AirGapLayer("非密閉中空層", false, 0.05);
      flWL[2] = new WallLayer("コンクリート", 1.6, 2000, 0.150);
      flWL[3] = new AirGapLayer("非密閉中空層", false, 0.05);
      flWL[4] = new WallLayer("石膏ボード", 0.220, 830, 0.009);
      flWL[5] = new WallLayer("ロックウール化粧吸音板", 0.064, 290, 0.015);

      WallLayer[] inWL = new WallLayer[3];  //内壁
      inWL[0] = new WallLayer("石膏ボード", 0.220, 830, 0.012);
      inWL[1] = new AirGapLayer("非密閉中空層", false, 0.05);
      inWL[2] = new WallLayer("石膏ボード", 0.220, 830, 0.012);

      //ゾーンを作成/////////////////////////
      Zone[] znSs = new Zone[12];
      znSs[0] = new Zone("W1", (6.5 * 9) * 2.7 * 1.2, 6.5 * 9);
      znSs[1] = new Zone("I1", (6.5 * 9) * 2.7 * 1.2, 6.5 * 9);
      znSs[2] = new Zone("I2", (6.5 * 9) * 2.7 * 1.2, 6.5 * 9);
      znSs[3] = new Zone("I3", (5.0 * 9) * 2.7 * 1.2, 5.0 * 9);
      znSs[4] = new Zone("I4", (5.0 * 9) * 2.7 * 1.2, 5.0 * 9);
      znSs[5] = new Zone("I5", (6.5 * 9) * 2.7 * 1.2, 6.5 * 9);
      znSs[6] = new Zone("SW1", (6.5 * 5) * 2.7 * 1.2, 6.5 * 5);
      znSs[7] = new Zone("S1", (6.5 * 5) * 2.7 * 1.2, 6.5 * 5);
      znSs[8] = new Zone("S2", (6.5 * 5) * 2.7 * 1.2, 6.5 * 5);
      znSs[9] = new Zone("S3", (5.0 * 5) * 2.7 * 1.2, 5.0 * 5);
      znSs[10] = new Zone("S4", (5.0 * 5) * 2.7 * 1.2, 5.0 * 5);
      znSs[11] = new Zone("S5", (6.5 * 5) * 2.7 * 1.2, 6.5 * 5);

      //内部発熱を追加
      sHGains = new SimpleHeatGain[12];
      for (int i = 0; i < znSs.Length; i++)
      {
        sHGains[i] = new SimpleHeatGain(0, 0, 0);
        znSs[i].AddHeatGain(sHGains[i]);
      }

      //窓を作成***************************************************************************************
      double[] TAU_WIN, RHO_WIN;
      TAU_WIN = new double[] { 0.815 }; //ガラスの透過率リスト[-]
      RHO_WIN = new double[] { 0.072 }; //ガラスの反射率リスト[-]
      Window[] win = new Window[8];
      /*win[0] = new Window(WIN_AREA * 1.5, TAU_WIN, RHO_WIN, incW); //W1
      win[1] = new Window(WIN_AREA * 0.5, TAU_WIN, RHO_WIN, incW); //SW1
      win[2] = new Window(WIN_AREA * 1.0, TAU_WIN, RHO_WIN, incS); //SW1
      win[3] = new Window(WIN_AREA * 1.0, TAU_WIN, RHO_WIN, incS); //S1
      win[4] = new Window(WIN_AREA * 1.0, TAU_WIN, RHO_WIN, incS); //S2
      win[5] = new Window(WIN_AREA * 1.0, TAU_WIN, RHO_WIN, incS); //S3
      win[6] = new Window(WIN_AREA * 1.0, TAU_WIN, RHO_WIN, incS); //S4
      win[7] = new Window(WIN_AREA * 1.0, TAU_WIN, RHO_WIN, incS); //S5*/
      win[0] = new Window(9.0 * 1.4, TAU_WIN, RHO_WIN, incW); //W1
      win[1] = new Window(5.0 * 1.4, TAU_WIN, RHO_WIN, incW); //SW1
      win[2] = new Window(6.5 * 1.4, TAU_WIN, RHO_WIN, incS); //SW1
      win[3] = new Window(6.5 * 1.4, TAU_WIN, RHO_WIN, incS); //S1
      win[4] = new Window(6.5 * 1.4, TAU_WIN, RHO_WIN, incS); //S2
      win[5] = new Window(5.0 * 1.4, TAU_WIN, RHO_WIN, incS); //S3
      win[6] = new Window(5.0 * 1.4, TAU_WIN, RHO_WIN, incS); //S4
      win[7] = new Window(6.5 * 1.4, TAU_WIN, RHO_WIN, incS); //S5
      //初期化
      for (int i = 0; i < win.Length; i++)
      {
        VenetianBlind blind = new VenetianBlind(25, 22.5, 0, 0, 0.80, 0.90);
        blind.SlatAngle = 0;
        win[i].SetShadingDevice(0, blind);
        win[i].ConvectiveCoefficientF = 18;
        win[i].ConvectiveCoefficientB = 4;
        win[i].LongWaveEmissivityF = win[i].LongWaveEmissivityB = 0.9;
      }

      //壁体の作成***************************************************************************************
      Wall[] walls = new Wall[36];
      //外壁
      walls[0] = new Wall(9.0 * 3.1 - win[0].Area, exWL);  //W1
      walls[1] = new Wall(9.0 * 0.9, exbmWL);
      walls[2] = new Wall(5.0 * 3.1 - win[1].Area, exWL);  //SW1
      walls[3] = new Wall(5.0 * 0.9, exbmWL);
      walls[4] = new Wall(6.5 * 3.1 - win[2].Area, exWL);  //SW1
      walls[5] = new Wall(6.5 * 0.9, exbmWL);
      walls[6] = new Wall(6.5 * 3.1 - win[3].Area, exWL);  //S1
      walls[7] = new Wall(6.5 * 0.9, exbmWL);
      walls[8] = new Wall(6.5 * 3.1 - win[4].Area, exWL);  //S2
      walls[9] = new Wall(6.5 * 0.9, exbmWL);
      walls[10] = new Wall(5.0 * 3.1 - win[5].Area, exWL);  //S3
      walls[11] = new Wall(5.0 * 0.9, exbmWL);
      walls[12] = new Wall(5.0 * 3.1 - win[6].Area, exWL);  //S4
      walls[13] = new Wall(5.0 * 0.9, exbmWL);
      walls[14] = new Wall(6.5 * 3.1 - win[7].Area, exWL);  //S5
      walls[15] = new Wall(6.5 * 0.9, exbmWL);
      //内壁
      walls[16] = new Wall(6.5 * 4.0, inWL); //W1
      walls[17] = new Wall(6.5 * 4.0, inWL); //I1
      walls[18] = new Wall(6.5 * 4.0, inWL); //I2
      walls[19] = new Wall(5.0 * 4.0, inWL); //I3
      walls[20] = new Wall(5.0 * 4.0, inWL); //I4
      walls[21] = new Wall(6.5 * 4.0, inWL); //I5
      walls[22] = new Wall(9.0 * 4.0, inWL); //I5
      walls[23] = new Wall(5.0 * 4.0, inWL); //S5
      //床天井
      walls[24] = new Wall(6.5 * 9.0, flWL); //W1
      walls[25] = new Wall(6.5 * 9.0, flWL); //I1
      walls[26] = new Wall(6.5 * 9.0, flWL); //I2
      walls[27] = new Wall(5.0 * 9.0, flWL); //I3
      walls[28] = new Wall(5.0 * 9.0, flWL); //I4
      walls[29] = new Wall(6.5 * 9.0, flWL); //I5
      walls[30] = new Wall(6.5 * 5.0, flWL); //SW1
      walls[31] = new Wall(6.5 * 5.0, flWL); //S1
      walls[32] = new Wall(6.5 * 5.0, flWL); //S2
      walls[33] = new Wall(5.0 * 5.0, flWL); //S3
      walls[34] = new Wall(5.0 * 5.0, flWL); //S4
      walls[35] = new Wall(6.5 * 5.0, flWL); //S5

      //壁の初期化
      for (int i = 0; i < walls.Length; i++)
      {
        walls[i].ShortWaveAbsorptanceF = walls[i].ShortWaveAbsorptanceB = 0.8;
        walls[i].LongWaveEmissivityF = walls[i].LongWaveEmissivityB = 0.9;
        //walls[i].LongWaveEmissivityF = walls[i].LongWaveEmissivityB = 0.0;
        //walls[i].RadiativeCoefficientF = walls[i].RadiativeCoefficientB = 5; //長波長放射率で初期化されるはず
        string nm = walls[i].Layers[0].Name;
        if (nm == "コンクリート" || nm == "タイル") walls[i].ConvectiveCoefficientF = 18;
        else walls[i].ConvectiveCoefficientF = 4;
        walls[i].ConvectiveCoefficientB = 4;
        walls[i].Initialize(20);
      }

      //多数室の作成************************************************************************************
      MultiRooms mRm = new MultiRooms(1, znSs, walls, win);
      for (int i = 0; i < znSs.Length; i++) mRm.AddZone(0, i);

      //外壁を登録***************************************************************************************
      mRm.AddWall(0, 0, false); mRm.SetOutsideWall(0, true, incW);
      mRm.AddWall(0, 1, false); mRm.SetOutsideWall(1, true, incW);
      mRm.AddWall(6, 2, false); mRm.SetOutsideWall(2, true, incW);
      mRm.AddWall(6, 3, false); mRm.SetOutsideWall(3, true, incW);
      mRm.AddWall(6, 4, false); mRm.SetOutsideWall(4, true, incS);
      mRm.AddWall(6, 5, false); mRm.SetOutsideWall(5, true, incS);
      mRm.AddWall(7, 6, false); mRm.SetOutsideWall(6, true, incS);
      mRm.AddWall(7, 7, false); mRm.SetOutsideWall(7, true, incS);
      mRm.AddWall(8, 8, false); mRm.SetOutsideWall(8, true, incS);
      mRm.AddWall(8, 9, false); mRm.SetOutsideWall(9, true, incS);
      mRm.AddWall(9, 10, false); mRm.SetOutsideWall(10, true, incS);
      mRm.AddWall(9, 11, false); mRm.SetOutsideWall(11, true, incS);
      mRm.AddWall(10, 12, false); mRm.SetOutsideWall(12, true, incS);
      mRm.AddWall(10, 13, false); mRm.SetOutsideWall(13, true, incS);
      mRm.AddWall(11, 14, false); mRm.SetOutsideWall(14, true, incS);
      mRm.AddWall(11, 15, false); mRm.SetOutsideWall(15, true, incS);

      //内壁を登録***************************************************************************************
      mRm.AddWall(0, 16, true); mRm.UseAdjacentSpaceFactor(16, false, 0.7);
      mRm.AddWall(1, 17, true); mRm.UseAdjacentSpaceFactor(17, false, 0.7);
      mRm.AddWall(2, 18, true); mRm.UseAdjacentSpaceFactor(18, false, 0.7);
      mRm.AddWall(3, 19, true); mRm.UseAdjacentSpaceFactor(19, false, 0.7);
      mRm.AddWall(4, 20, true); mRm.UseAdjacentSpaceFactor(20, false, 0.7);
      mRm.AddWall(5, 21, true); mRm.UseAdjacentSpaceFactor(21, false, 0.7);
      mRm.AddWall(5, 22, true); mRm.UseAdjacentSpaceFactor(22, false, 0.7);
      mRm.AddWall(11, 23, true); mRm.UseAdjacentSpaceFactor(23, false, 0.7);
      //床
      for (int i = 0; i < znSs.Length; i++)
      {
        mRm.AddWall(i, 24 + i, true);
        mRm.AddWall(i, 24 + i, false);
      }

      //窓を登録***************************************************************************************
      mRm.AddWindow(0, 0);
      mRm.AddWindow(6, 1);
      mRm.AddWindow(6, 2);
      mRm.AddWindow(7, 3);
      mRm.AddWindow(8, 4);
      mRm.AddWindow(9, 5);
      mRm.AddWindow(10, 6);
      mRm.AddWindow(11, 7);

      //ペリメータ床に短波長優先配分
      const double SW_RATE_TO_FLOOR = 0.7;
      //const double SW_RATE_TO_FLOOR = 1.0;
      mRm.SetSWDistributionRateToFloor(0, 24, true, SW_RATE_TO_FLOOR);
      mRm.SetSWDistributionRateToFloor(1, 30, true, SW_RATE_TO_FLOOR);
      mRm.SetSWDistributionRateToFloor(2, 30, true, SW_RATE_TO_FLOOR);
      mRm.SetSWDistributionRateToFloor(3, 31, true, SW_RATE_TO_FLOOR);
      mRm.SetSWDistributionRateToFloor(4, 32, true, SW_RATE_TO_FLOOR);
      mRm.SetSWDistributionRateToFloor(5, 33, true, SW_RATE_TO_FLOOR);
      mRm.SetSWDistributionRateToFloor(6, 34, true, SW_RATE_TO_FLOOR);
      mRm.SetSWDistributionRateToFloor(7, 35, true, SW_RATE_TO_FLOOR);

      ////隙間風と熱容量設定*************************************************************************
      for (int i = 0; i < znSs.Length; i++)
      {
        znSs[i].HeatCapacity = znSs[i].AirMass * 1006 * 10;
        znSs[i].InitializeAirState(22, 0.0105);
      }

      //建物モデルの作成
      BuildingThermalModel bModel = new BuildingThermalModel(new MultiRooms[] { mRm });

      //ゾーン間換気の設定
      //const double cvRate = 0;
      const double cvRate = 150d * 1.2 / 3600d;
      bModel.SetCrossVentilation(0, 0, 0, 1, 9.0 * cvRate);
      bModel.SetCrossVentilation(0, 1, 0, 2, 9.0 * cvRate);
      bModel.SetCrossVentilation(0, 2, 0, 3, 9.0 * cvRate);
      bModel.SetCrossVentilation(0, 3, 0, 4, 9.0 * cvRate);
      bModel.SetCrossVentilation(0, 4, 0, 5, 9.0 * cvRate);
      bModel.SetCrossVentilation(0, 6, 0, 7, 5.0 * cvRate);
      bModel.SetCrossVentilation(0, 7, 0, 8, 5.0 * cvRate);
      bModel.SetCrossVentilation(0, 8, 0, 9, 5.0 * cvRate);
      bModel.SetCrossVentilation(0, 9, 0, 10, 5.0 * cvRate);
      bModel.SetCrossVentilation(0, 10, 0, 11, 5.0 * cvRate);
      bModel.SetCrossVentilation(0, 0, 0, 6, 6.5 * cvRate);
      bModel.SetCrossVentilation(0, 1, 0, 7, 6.5 * cvRate);
      bModel.SetCrossVentilation(0, 2, 0, 8, 6.5 * cvRate);
      bModel.SetCrossVentilation(0, 3, 0, 9, 5.0 * cvRate);
      bModel.SetCrossVentilation(0, 4, 0, 10, 5.0 * cvRate);
      bModel.SetCrossVentilation(0, 5, 0, 11, 6.5 * cvRate);

      return bModel;
    }

    private static void controlBuilding(BuildingThermalModel bModel, SimpleHeatGain[] sHGains)
    {
      DateTime dtNow = bModel.CurrentDateTime;

      //内部発熱設定************************************
      const double RAD_RATE = 0.6;
      bool isNoLoadDTime = (dtNow.DayOfWeek == DayOfWeek.Saturday || dtNow.DayOfWeek == DayOfWeek.Sunday) || (dtNow.Hour < 8 || 21 <= dtNow.Hour);
      if (isNoLoadDTime)
      {
        for (int i = 0; i < bModel.MultiRoom[0].ZoneNumber; i++)
        {
          double hg = bModel.MultiRoom[0].Zones[i].FloorArea * 0.25 * 12;
          sHGains[i].ConvectiveHeatGain = (1 - RAD_RATE) * hg;
          sHGains[i].RadiativeHeatGain = RAD_RATE * hg;
          sHGains[i].WaterGain = 0;
        }
      }
      else
      {
        double shRate, lhRate;
        if (dtNow.Hour < 12)
        {
          shRate = 1.0 * 12 + 1.0 * 8 + 1.0 * 12;
          lhRate = 1.0 * 4;
        }
        else if (dtNow.Hour < 13)
        {
          shRate = 0.5 * 12 + 0.6 * 8 + 0.8 * 12;
          lhRate = 0.6 * 4;
        }
        else if (dtNow.Hour < 18)
        {
          shRate = 1.0 * 12 + 1.0 * 8 + 1.0 * 12;
          lhRate = 1.0 * 4;
        }
        else if (dtNow.Hour < 19)
        {
          shRate = 1.0 * 12 + 0.5 * 8 + 1.0 * 12;
          lhRate = 0.5 * 4;
        }
        else if (dtNow.Hour < 20)
        {
          shRate = 1.0 * 12 + 0.3 * 8 + 0.5 * 12;
          lhRate = 0.3 * 4;
        }
        else
        {
          shRate = 0.8 * 12 + 0.2 * 8 + 0.5 * 12;
          lhRate = 0.2 * 4;
        }
        for (int i = 0; i < bModel.MultiRoom[0].ZoneNumber; i++)
        {
          double shg = bModel.MultiRoom[0].Zones[i].FloorArea * shRate;
          double lhg = bModel.MultiRoom[0].Zones[i].FloorArea * lhRate;
          sHGains[i].ConvectiveHeatGain = (1 - RAD_RATE) * shg;
          sHGains[i].RadiativeHeatGain = RAD_RATE * shg;
          sHGains[i].WaterGain = 0.001 * lhg / MoistAir.LatentHeatOfVaporization; //水分量に変換
        }
      }

      //空調制御****************************************
      bool isNoHVACDTime = (dtNow.DayOfWeek == DayOfWeek.Saturday || dtNow.DayOfWeek == DayOfWeek.Sunday) || (dtNow.Hour < 7 || 21 <= dtNow.Hour);
      //isNoHVACDTime = true;
      if (isNoHVACDTime)
      {
        for (int i = 0; i < bModel.MultiRoom[0].ZoneNumber; i++)
        {
          bModel.ControlHeatSupply(0, i, 0);
          bModel.ControlWaterSupply(0, i, 0);
        }
      }
      else
      {
        double dbt, hrt;
        if (6 <= dtNow.Month && dtNow.Month <= 9)
        {
          dbt = 26;
          hrt = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(26, 50, 101.325);
        }
        else if (dtNow.Month == 12 || dtNow.Month <= 3)
        {
          dbt = 22;
          hrt = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(22, 40, 101.325);
        }
        else
        {
          dbt = 24;
          hrt = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(24, 50, 101.325);
        }
        for (int i = 0; i < bModel.MultiRoom[0].ZoneNumber; i++)
        {
          bModel.ControlDrybulbTemperature(0, i, dbt);
          bModel.ControlHumidityRatio(0, i, hrt);
        }

      }

      //外気導入****************************************
      //bool isNoOADTime = (dtNow.DayOfWeek == DayOfWeek.Saturday || dtNow.DayOfWeek == DayOfWeek.Sunday) || (dtNow.Hour < 8 || 21 <= dtNow.Hour);
      bool isNoOADTime = true; //外気負荷は別系統で処理することとし、漏気のみを計上
      if (isNoOADTime)
      {
        for (int i = 0; i < bModel.MultiRoom[0].ZoneNumber; i++)
        {
          double vRate = 0.05e-3 * bModel.MultiRoom[0].Zones[i].GetWindowSurface(); //窓面積法:6m/sで中程度の気密性サッシ
          bModel.SetVentilationRate(0, i, vRate);
        }
      }
      else
      {
        for (int i = 0; i < bModel.MultiRoom[0].ZoneNumber; i++)
        {
          double vRate = 5 * 1.2 / 3600 * bModel.MultiRoom[0].Zones[i].FloorArea; //5 CMH/m2
          bModel.SetVentilationRate(0, i, vRate);
        }
      }

    }

    /// <summary>気象データを作成する</summary>
    /// <param name="hasDataPath">HASP形式データファイルへのパス</param>
    /// <param name="dbt">乾球温度リスト</param>
    /// <param name="hrt">絶対湿度リスト</param>
    /// <param name="dnr">法線面直達日射リスト</param>
    /// <param name="dhr">水平面全天日射リスト</param>
    /// <param name="ncr">夜間放射リスト</param>
    public static void LoadWeatherData
      (string hasDataPath, out double[] dbt, out double[] hrt, out double[] dnr, out double[] dhr, out double[] ncr)
    {
      dbt = new double[8760];
      hrt = new double[8760];
      dnr = new double[8760];
      dhr = new double[8760];
      ncr = new double[8760];
      using (StreamReader sReader = new StreamReader(hasDataPath))
      {
        string buff = sReader.ReadToEnd();
        string[] lines = WeatherConverter.HASPtoCSV(buff).Split
          (new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < 8760; i++)
        {
          string[] sbf = lines[i + 1].Split(',');
          dbt[i] = double.Parse(sbf[3]);
          hrt[i] = double.Parse(sbf[4]) / 1000d;
          dnr[i] = double.Parse(sbf[5]) * 1e6 / 3600d;
          dhr[i] = double.Parse(sbf[6]) * 1e6 / 3600d;
          ncr[i] = double.Parse(sbf[7]) * 1e6 / 3600d;
        }
      }
    }

    #endregion

    #region VRF動作テスト（暖房）

    public static void testVRF1_H()
    {
      const double PIPE_FC_C = 0.89; //配管長補正係数
      const double PIPE_FC_H = 0.91; //配管長補正係数
      const double NOM_PIPE_LENGTH = 7.5; //定格配管長
      const double LONG_PIPE_LENGTH_C = 100; //補正係数適用時の配管長
      const double LONG_PIPE_LENGTH_H = 80; //補正係数適用時の配管長
      const double IHEX_CAP_C = 14.0;
      const double IHEX_CAP_H = 16.0;
      const double NOM_IHEX_AFLOW = 34.5 * 1.2 / 60d;
      const double NOM_OHEX_AFLOW = 187 * 1.2 / 60;

      Refrigerant r410a = new Refrigerant(Refrigerant.Fluid.R410A);
      VRFUnit iHex = VRFSystem.MakeIndoorUnit(NOM_IHEX_AFLOW, 0, -IHEX_CAP_C, 0, IHEX_CAP_H);
      iHex.CurrentMode = VRFUnit.Mode.Heating;

      //室外機を用意
      VRFSystem vrfSystem = new VRFSystem(
        r410a,
        NOM_OHEX_AFLOW, 0, -28.0, 8.93, -12.6, 2.35, -13.2, 1.94,
        NOM_OHEX_AFLOW, 0, 31.5, 8.68, 14.2, 2.54,
        NOM_PIPE_LENGTH, LONG_PIPE_LENGTH_C, PIPE_FC_C, LONG_PIPE_LENGTH_H, PIPE_FC_H, iHex);
      vrfSystem.CurrentMode = VRFSystem.Mode.Heating;

      //室内機を追加
      VRFUnit[] iHexes = new VRFUnit[]
      {
        VRFSystem.MakeIndoorUnit(NOM_IHEX_AFLOW, 0, -IHEX_CAP_C, 0, IHEX_CAP_H),
        VRFSystem.MakeIndoorUnit(NOM_IHEX_AFLOW, 0, -IHEX_CAP_C, 0, IHEX_CAP_H)
      };
      vrfSystem.AddIndoorUnit(iHexes);
      for (int i = 0; i < vrfSystem.IndoorUnitNumber; i++)
        vrfSystem.SetIndoorUnitMode(i, VRFUnit.Mode.Heating);

      //定格条件テスト**************
      double oHmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(7, 6, 101.325);
      vrfSystem.OutdoorAirDrybulbTemperature = 7;
      vrfSystem.OutdoorAirHumidityRatio = oHmd;
      double iHmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(20, 15, 101.325);
      iHexes[0].SolveHeatLoad(31.5d / 2d, NOM_IHEX_AFLOW, 20, iHmd, false);
      double tSP = iHexes[0].OutletAirTemperature;
      for (int i = 0; i < vrfSystem.IndoorUnitNumber; i++)
      {
        vrfSystem.SetIndoorUnitMode(i, VRFUnit.Mode.Heating);
        vrfSystem.SetIndoorUnitSetpointTemperature(i, tSP);
        vrfSystem.SetIndoorUnitInletAirState(i, 20, iHmd);
      }
      vrfSystem.UpdateState();
      Console.WriteLine("Nominal Condition");
      Console.WriteLine((2 * iHexes[0].HeatTransfer).ToString("F3") + "," + vrfSystem.CompressorElectricity.ToString("F3"));

      //中間冷房標準条件テスト**************
      iHexes[0].SolveHeatLoad(14.2 / 2d, NOM_IHEX_AFLOW, 20, iHmd, false);
      for (int i = 0; i < vrfSystem.IndoorUnitNumber; i++)
        vrfSystem.SetIndoorUnitSetpointTemperature(i, iHexes[0].OutletAirTemperature);
      vrfSystem.UpdateState();
      Console.WriteLine("Mid load Condition");
      Console.WriteLine((2 * iHexes[0].HeatTransfer).ToString("F3") + "," + vrfSystem.CompressorElectricity.ToString("F3"));

      //デフロスト運転テスト**************
      vrfSystem.OutdoorAirDrybulbTemperature = 3;
      vrfSystem.OutdoorAirHumidityRatio = 0.0045;
      for (int i = 0; i < vrfSystem.IndoorUnitNumber; i++)
      {
        vrfSystem.SetIndoorUnitSetpointTemperature(i, tSP);
        vrfSystem.SetIndoorUnitInletAirState(i, 20, iHmd);
      }
      vrfSystem.UpdateState();
      Console.WriteLine("Defrost Condition");
      Console.WriteLine((2 * iHexes[0].HeatTransfer).ToString("F3") + "," + vrfSystem.CompressorElectricity.ToString("F3"));
    }

    public static void testVRF2_H()
    {
      const double PIPE_FC_C = 0.89; //配管長補正係数
      const double PIPE_FC_H = 0.91; //配管長補正係数
      const double NOM_PIPE_LENGTH = 7.5; //定格配管長
      const double LONG_PIPE_LENGTH_C = 100; //補正係数適用時の配管長
      const double LONG_PIPE_LENGTH_H = 80; //補正係数適用時の配管長
      const double IHEX_CAP_C = 14.0;
      const double IHEX_CAP_H = 16.0;
      const double NOM_IHEX_AFLOW = 34.5 * 1.2 / 60d;
      const double NOM_OHEX_AFLOW = 187 * 1.2 / 60;

      Refrigerant r410a = new Refrigerant(Refrigerant.Fluid.R410A);
      VRFUnit iHex = VRFSystem.MakeIndoorUnit(NOM_IHEX_AFLOW, 0, -IHEX_CAP_C, 0, IHEX_CAP_H);
      iHex.CurrentMode = VRFUnit.Mode.Heating;

      //室外機を用意
      VRFSystem vrfSystem = new VRFSystem(
        r410a,
        NOM_OHEX_AFLOW, 0, -28.0, 8.93, -12.6, 2.35, -13.2, 1.94,
        NOM_OHEX_AFLOW, 0, 31.5, 8.68, 14.2, 2.54,
        NOM_PIPE_LENGTH, LONG_PIPE_LENGTH_C, PIPE_FC_C, LONG_PIPE_LENGTH_H, PIPE_FC_H, iHex);
      vrfSystem.CurrentMode = VRFSystem.Mode.Heating;

      //室内機を追加
      VRFUnit[] iHexes = new VRFUnit[]
      {
        VRFSystem.MakeIndoorUnit(NOM_IHEX_AFLOW, 0, -IHEX_CAP_C, 0, IHEX_CAP_H),
        VRFSystem.MakeIndoorUnit(NOM_IHEX_AFLOW, 0, -IHEX_CAP_C, 0, IHEX_CAP_H)
      };
      vrfSystem.AddIndoorUnit(iHexes);
      for (int i = 0; i < vrfSystem.IndoorUnitNumber; i++)
        vrfSystem.SetIndoorUnitMode(i, VRFUnit.Mode.Heating);

      //屋内機吸込条件はJISと同じで固定
      double iHmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(20, 15, 101.325);
      for (int i = 0; i < vrfSystem.IndoorUnitNumber; i++) vrfSystem.SetIndoorUnitMode(i, VRFUnit.Mode.Heating);
      double oHmd;

      //負荷率と外気条件に関する感度分析*******************
      double[] dbt = new double[] { 1, 7, 13, 19 };
      double[] wbt = new double[] { 1, 6, 11, 16 };
      iHexes[0].InletAirTemperature = iHexes[1].InletAirTemperature = 20;
      iHexes[0].InletAirHumidityRatio = iHexes[1].InletAirHumidityRatio = iHmd;
      for (int i = 0; i < 20; i++) Console.Write("," + (5 * (i + 1)));
      Console.WriteLine();
      for (int oaIndx = 0; oaIndx < dbt.Length; oaIndx++)
      {
        //屋外機条件を設定
        oHmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature
          (dbt[oaIndx], wbt[oaIndx], 101.325);
        vrfSystem.OutdoorAirDrybulbTemperature = dbt[oaIndx];
        vrfSystem.OutdoorAirHumidityRatio = oHmd;

        //負荷率を変更して計算
        Console.Write("DB Temp = " + dbt[oaIndx].ToString("F1") + ": WB Temp = " + wbt[oaIndx].ToString("F1"));
        for (int i = 0; i < 20; i++)
        {
          double pl = 0.05 * (i + 1);
          iHexes[0].SolveHeatLoad(15.75 * pl, NOM_IHEX_AFLOW, 20, iHmd, false);
          for (int j = 0; j < vrfSystem.IndoorUnitNumber; j++)
            vrfSystem.SetIndoorUnitSetpointTemperature(j, iHexes[0].OutletAirTemperature);
          vrfSystem.UpdateState();
          Console.Write("," + vrfSystem.CompressorElectricity.ToString("F3"));
        }
        Console.WriteLine("");
      }
      Console.WriteLine(""); Console.WriteLine("");

      //負荷率と負荷偏在に関する感度分析*******************
      for (int i = 0; i < 20; i++) Console.Write("," + (5 + 5 * i));
      Console.WriteLine();
      //屋外機条件を設定
      oHmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(7, 6, 101.325);
      vrfSystem.OutdoorAirDrybulbTemperature = 7;
      vrfSystem.OutdoorAirHumidityRatio = oHmd;

      //負荷率を変更して計算
      List<double> uniElcs = new List<double>();
      List<double> nUniElcs = new List<double>();
      List<double> uniDPs = new List<double>();
      List<double> nUniDPs = new List<double>();
      for (int i = 0; i < 20; i++)
      {
        double pl = 0.05 + 0.05 * i;
        iHexes[0].SolveHeatLoad(15.75 * pl, NOM_IHEX_AFLOW, 20, iHmd, false);
        for (int j = 0; j < vrfSystem.IndoorUnitNumber; j++)
          vrfSystem.SetIndoorUnitSetpointTemperature(j, iHexes[0].OutletAirTemperature);
        vrfSystem.UpdateState();
        uniElcs.Add(vrfSystem.CompressorElectricity);
        uniDPs.Add(vrfSystem.CondensingPressure - vrfSystem.EvaporatingPressure);
      }
      for (int i = 0; i < 20; i++)
      {
        double pl = 2 * (0.05 + 0.05 * i) - 0.05;
        double pl1 = Math.Min(1.00, pl);
        double pl2 = 0.05 + (pl - pl1);
        iHexes[0].SolveHeatLoad(15.75 * pl1, NOM_IHEX_AFLOW, 20, iHmd, false);
        vrfSystem.SetIndoorUnitSetpointTemperature(0, iHexes[0].OutletAirTemperature);
        iHexes[1].SolveHeatLoad(15.75 * pl2, NOM_IHEX_AFLOW, 20, iHmd, false);
        vrfSystem.SetIndoorUnitSetpointTemperature(1, iHexes[1].OutletAirTemperature);
        vrfSystem.UpdateState();
        nUniElcs.Add(vrfSystem.CompressorElectricity);
        nUniDPs.Add(vrfSystem.CondensingPressure - vrfSystem.EvaporatingPressure);
      }
      //出力
      Console.Write("Uniform Electricity");
      for (int i = 0; i < uniElcs.Count; i++) Console.Write("," + uniElcs[i].ToString("F3"));
      Console.WriteLine();
      Console.Write("Non Uniform Electricity");
      for (int i = 0; i < nUniElcs.Count; i++) Console.Write("," + nUniElcs[i].ToString("F3"));
      Console.WriteLine();
      Console.Write("Uniform Delta P");
      for (int i = 0; i < uniDPs.Count; i++) Console.Write("," + (0.001 * uniDPs[i]).ToString("F3"));
      Console.WriteLine();
      Console.Write("Non Uniform Delta P");
      for (int i = 0; i < nUniDPs.Count; i++) Console.Write("," + (0.001 * nUniDPs[i]).ToString("F3"));
      Console.WriteLine(); Console.WriteLine();

      //屋内外機の吸込温度に関する感度分析*******************
      double[,] elecMap = new double[21, 21];
      double[,] capMap = new double[21, 21];
      string[,] dwfState = new string[21, 21];
      double oRHmd = MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndWetBulbTemperature(7, 6, 101.325);
      double iRHmd = MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndWetBulbTemperature(20, 15, 101.325);
      for (int i = 0; i <= 20; i++)
      {
        double oaDBT = 0.5 * i;
        vrfSystem.OutdoorAirDrybulbTemperature = oaDBT;
        vrfSystem.OutdoorAirHumidityRatio = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(oaDBT, oRHmd, 101.325);
        for (int j = 0; j <= 20; j++)
        {
          double iaDBT = 15 + 0.5 * j;
          double iaHRT = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(iaDBT, iRHmd, 101.325);
          iHexes[0].SolveHeatLoad(15.75, NOM_IHEX_AFLOW, iaDBT, iaHRT, false);
          for (int k = 0; k < iHexes.Length; k++)
          {
            iHexes[k].InletAirTemperature = iaDBT;
            iHexes[k].InletAirHumidityRatio = iaHRT;
            iHexes[k].OutletAirSetpointTemperature = iHexes[0].OutletAirTemperature;
          }
          vrfSystem.UpdateState();
          elecMap[i, j] = vrfSystem.CompressorElectricity;
          capMap[i, j] = iHexes[0].HeatTransfer * 2;
          dwfState[i, j] = 0 < vrfSystem.OutdoorUnit_H.FrostSurfaceArea ? "F" :
            0 < vrfSystem.OutdoorUnit_H.WetSurfaceArea ? "W" : "D";
        }
      }

      for (int i = 0; i <= 20; i++) Console.Write("," + (15 + 0.5 * i).ToString("F1"));
      Console.WriteLine();
      for (int i = 0; i <= 20; i++)
      {
        Console.Write(0.5 * i);
        for (int j = 0; j <= 20; j++)
          Console.Write("," + elecMap[i, j].ToString("F3"));
        Console.WriteLine();
      }
      Console.WriteLine();
      for (int i = 0; i <= 20; i++) Console.Write("," + (15 + 0.5 * i).ToString("F1"));
      Console.WriteLine();
      for (int i = 0; i <= 20; i++)
      {
        Console.Write(0.5 * i);
        for (int j = 0; j <= 20; j++)
          Console.Write("," + (capMap[i, j] / elecMap[i, j]).ToString("F3"));
        Console.WriteLine();
      }
      Console.WriteLine();
      for (int i = 0; i <= 20; i++) Console.Write("," + (15 + 0.5 * i).ToString("F1"));
      Console.WriteLine();
      for (int i = 0; i <= 20; i++)
      {
        Console.Write(0.5 * i);
        for (int j = 0; j <= 20; j++)
          Console.Write("," + dwfState[i, j]);
        Console.WriteLine();
      }
      Console.WriteLine(); Console.WriteLine();

      //屋外機の吸込温湿度に関する感度分析*******************
      elecMap = new double[21, 17];
      capMap = new double[21, 17];
      dwfState = new string[21, 17];
      //室内機は定格条件
      iHexes[0].InletAirTemperature = iHexes[1].InletAirTemperature = 20;
      iHexes[0].InletAirHumidityRatio = iHexes[1].InletAirHumidityRatio =
          MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(20, 15, 101.325);
      iHexes[0].SolveHeatLoad(15.75, NOM_IHEX_AFLOW, iHexes[0].InletAirTemperature, iHexes[0].InletAirHumidityRatio, false);
      for (int i = 0; i < iHexes.Length; i++) iHexes[i].OutletAirSetpointTemperature = iHexes[0].OutletAirTemperature;
      //感度分析
      for (int i = 0; i <= 20; i++)
      {
        double oaDBT = 0.5 * i;
        vrfSystem.OutdoorAirDrybulbTemperature = oaDBT;
        for (int j = 0; j <= 16; j++)
        {
          double oaRHMD = 20 + 5 * j;
          vrfSystem.OutdoorAirHumidityRatio = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(oaDBT, oaRHMD, 101.325);
          vrfSystem.UpdateState();
          elecMap[i, j] = vrfSystem.CompressorElectricity;
          capMap[i, j] = iHexes[0].HeatTransfer * 2;
          dwfState[i, j] = 0 < vrfSystem.OutdoorUnit_H.FrostSurfaceArea ? "F" :
            0 < vrfSystem.OutdoorUnit_H.WetSurfaceArea ? "W" : "D";
        }
      }

      for (int i = 0; i <= 16; i++) Console.Write("," + (20 + 5 * i).ToString("F1"));
      Console.WriteLine();
      for (int i = 0; i <= 20; i++)
      {
        Console.Write(0.5 * i);
        for (int j = 0; j <= 16; j++)
          Console.Write("," + elecMap[i, j].ToString("F3"));
        Console.WriteLine();
      }
      Console.WriteLine();
      for (int i = 0; i <= 16; i++) Console.Write("," + (20 + 5 * i).ToString("F1"));
      Console.WriteLine();
      for (int i = 0; i <= 20; i++)
      {
        Console.Write(0.5 * i);
        for (int j = 0; j <= 16; j++)
          Console.Write("," + (capMap[i, j]).ToString("F3"));
        //Console.Write("," + (capMap[i, j] / elecMap[i, j]).ToString("F3"));
        Console.WriteLine();
      }
      Console.WriteLine();
      for (int i = 0; i <= 16; i++) Console.Write("," + (20 + 5 * i).ToString("F1"));
      Console.WriteLine();
      for (int i = 0; i <= 20; i++)
      {
        Console.Write(0.5 * i);
        for (int j = 0; j <= 16; j++)
          Console.Write("," + dwfState[i, j]);
        Console.WriteLine();
      }
      Console.WriteLine(); Console.WriteLine();

      //高低差と配管長に関する感度分析*******************
      capMap = new double[20, 11];
      elecMap = new double[20, 11];
      vrfSystem.OutdoorAirDrybulbTemperature = 7;
      vrfSystem.OutdoorAirHumidityRatio =
        MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(7, 6, 101.325);
      //室内機は定格条件
      iHexes[0].InletAirTemperature = iHexes[1].InletAirTemperature = 20;
      iHexes[0].InletAirHumidityRatio = iHexes[1].InletAirHumidityRatio =
          MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(20, 15, 101.325);
      iHexes[0].SolveHeatLoad(15.75, NOM_IHEX_AFLOW, iHexes[0].InletAirTemperature, iHexes[0].InletAirHumidityRatio, false);
      for (int i = 0; i < iHexes.Length; i++) iHexes[i].OutletAirSetpointTemperature = iHexes[0].OutletAirTemperature;
      //感度分析
      for (int i = 0; i <= 19; i++)
      {
        vrfSystem.PipeLength = 5 + 5 * i;
        for (int j = 0; j <= 10; j++)
        {
          vrfSystem.IndoorUnitHeight = -j * 5;
          vrfSystem.UpdateState();
          capMap[i, j] = iHexes[0].HeatTransfer + iHexes[1].HeatTransfer;
          elecMap[i, j] = vrfSystem.CompressorElectricity;
        }
      }

      for (int i = 0; i <= 10; i++) Console.Write("," + (i * 5).ToString("F1"));
      Console.WriteLine();
      for (int i = 0; i <= 19; i++)
      {
        Console.Write(5 + 5 * i + "m");
        for (int j = 0; j <= 10; j++)
          Console.Write("," + capMap[i, j].ToString("F3"));
        Console.WriteLine();
      }
      Console.WriteLine();

    }

    public static void testGHP()
    {
      const double PIPE_FC_C = 0.89; //配管長補正係数
      const double PIPE_FC_H = 0.91; //配管長補正係数
      const double NOM_PIPE_LENGTH = 7.5; //定格配管長
      const double LONG_PIPE_LENGTH_C = 100; //補正係数適用時の配管長
      const double LONG_PIPE_LENGTH_H = 80; //補正係数適用時の配管長
      const double IHEX_CAP_C = 14.0;
      const double IHEX_CAP_H = 16.0;
      const double NOM_IHEX_AFLOW = 34.5 * 1.2 / 60d;
      const double NOM_OHEX_AFLOW = 210 * 1.2 / 60;

      Refrigerant r410a = new Refrigerant(Refrigerant.Fluid.R410A);
      VRFUnit iHex = VRFSystem.MakeIndoorUnit(NOM_IHEX_AFLOW, 0, -IHEX_CAP_C, 0, IHEX_CAP_H);
      iHex.CurrentMode = VRFUnit.Mode.Heating;

      //室外機を用意
      VRFSystem vrfSystem = new VRFSystem(
        r410a,
        NOM_OHEX_AFLOW, 0, -28.0, 8.93, -12.6, 2.35, -13.2, 1.94,
        NOM_OHEX_AFLOW, 0, 31.5, 26.5, 14.5, 9.0,
        NOM_PIPE_LENGTH, LONG_PIPE_LENGTH_C, PIPE_FC_C, LONG_PIPE_LENGTH_H, PIPE_FC_H, iHex, 0.8); //廃熱回収の有無を設定
      vrfSystem.CurrentMode = VRFSystem.Mode.Heating;

      //室内機を追加
      VRFUnit[] iHexes = new VRFUnit[]
      {
        VRFSystem.MakeIndoorUnit(NOM_IHEX_AFLOW, 0, -IHEX_CAP_C, 0, IHEX_CAP_H),
        VRFSystem.MakeIndoorUnit(NOM_IHEX_AFLOW, 0, -IHEX_CAP_C, 0, IHEX_CAP_H)
      };
      vrfSystem.AddIndoorUnit(iHexes);

      //屋内機吸込条件はJISと同じで固定
      double iHmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(20, 15, 101.325);
      for (int i = 0; i < vrfSystem.IndoorUnitNumber; i++)
      {
        vrfSystem.SetIndoorUnitMode(i, VRFUnit.Mode.Heating);
        vrfSystem.SetIndoorUnitInletAirState(i, 20, iHmd);
      }

      //負荷率と外気条件に関する感度分析*******************
      double[,] elecMap = new double[21, 21];
      double[,] capMap = new double[21, 21];
      string[,] dwfState = new string[21, 21];
      double oRHmd = MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndWetBulbTemperature(7, 6, 101.325);
      for (int i = 0; i <= 20; i++)
      {
        double oaDBT = 0.5 * i;
        vrfSystem.OutdoorAirDrybulbTemperature = oaDBT;
        vrfSystem.OutdoorAirHumidityRatio = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(oaDBT, oRHmd, 101.325);
        for (int j = 0; j <= 20; j++)
        {
          double pl = 0.6 + 0.02 * j;
          iHexes[0].SolveHeatLoad(15.75 * pl, NOM_IHEX_AFLOW, iHexes[0].InletAirTemperature, iHexes[0].InletAirHumidityRatio, true);
          iHexes[0].OutletAirSetpointTemperature = iHexes[1].OutletAirSetpointTemperature = iHexes[0].OutletAirTemperature;
          vrfSystem.UpdateState();
          elecMap[i, j] = vrfSystem.CompressorElectricity;
          capMap[i, j] = iHexes[0].HeatTransfer * 2;
          dwfState[i, j] = 0 < vrfSystem.OutdoorUnit_H.FrostSurfaceArea ? "F" :
            0 < vrfSystem.OutdoorUnit_H.WetSurfaceArea ? "W" : "D";
        }
      }

      for (int i = 0; i <= 20; i++) Console.Write("," + (60 + 2 * i).ToString("F1"));
      Console.WriteLine();
      for (int i = 0; i <= 20; i++)
      {
        Console.Write(0.5 * i);
        for (int j = 0; j <= 20; j++)
          Console.Write("," + elecMap[i, j].ToString("F3"));
        Console.WriteLine();
      }
      Console.WriteLine();
      for (int i = 0; i <= 20; i++) Console.Write("," + (60 + 2 * i).ToString("F1"));
      Console.WriteLine();
      for (int i = 0; i <= 20; i++)
      {
        Console.Write(0.5 * i);
        for (int j = 0; j <= 20; j++)
          Console.Write("," + (capMap[i, j] / elecMap[i, j]).ToString("F3"));
        Console.WriteLine();
      }
      Console.WriteLine();
      for (int i = 0; i <= 20; i++) Console.Write("," + (60 + 2 * i).ToString("F1"));
      Console.WriteLine();
      for (int i = 0; i <= 20; i++)
      {
        Console.Write(0.5 * i);
        for (int j = 0; j <= 20; j++)
          Console.Write("," + dwfState[i, j]);
        Console.WriteLine();
      }

    }

    public static void testVRFSpeed_H()
    {
      const double PIPE_FC_C = 0.89; //配管長補正係数
      const double PIPE_FC_H = 0.91; //配管長補正係数
      const double NOM_PIPE_LENGTH = 7.5; //定格配管長
      const double LONG_PIPE_LENGTH_C = 100; //補正係数適用時の配管長
      const double LONG_PIPE_LENGTH_H = 80; //補正係数適用時の配管長
      const double IHEX_CAP_C = 5.6;
      const double IHEX_CAP_H = 6.3;
      const double NOM_IHEX_AFLOW = 15.5 * 1.2 / 60d;
      const double NOM_OHEX_AFLOW = (191 + 160) * 1.2 / 60;

      Refrigerant r410a = new Refrigerant(Refrigerant.Fluid.R410A);
      VRFUnit iHex = VRFSystem.MakeIndoorUnit(NOM_IHEX_AFLOW, 0, -IHEX_CAP_C, 0, IHEX_CAP_H);
      iHex.CurrentMode = VRFUnit.Mode.Heating;

      //室外機を用意
      VRFSystem vrfSystem = new VRFSystem(
        r410a,
        NOM_OHEX_AFLOW, 0, -56, 15.9, -25.2, 4.66, -26.4, 3.81,
        NOM_OHEX_AFLOW, 0, 63.0, 16.6, 28.4, 5.19,
        NOM_PIPE_LENGTH, LONG_PIPE_LENGTH_C, PIPE_FC_C, LONG_PIPE_LENGTH_H, PIPE_FC_H, iHex);
      vrfSystem.CurrentMode = VRFSystem.Mode.Heating;

      //室内機を追加
      VRFUnit[] iHexes = new VRFUnit[10];
      for (int i = 0; i < iHexes.Length; i++)
        iHexes[i] = VRFSystem.MakeIndoorUnit(NOM_IHEX_AFLOW, 0, -IHEX_CAP_C, 0, IHEX_CAP_H);
      vrfSystem.AddIndoorUnit(iHexes);
      for (int i = 0; i < vrfSystem.IndoorUnitNumber; i++)
        vrfSystem.SetIndoorUnitMode(i, VRFUnit.Mode.Heating);

      //乱数製造機
      MersenneTwister mRnd = new MersenneTwister(1);

      //ストップウォッチ
      System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

      //時間計測開始
      for (int trial = 0; trial < 10; trial++)
      {
        Console.Write((trial + 1) + " : ");
        sw.Reset();
        sw.Start();

        for (int i = 0; i < 10000; i++)
        {
          //屋外機条件
          double oaDBT = 15 * mRnd.NextDouble();
          double oaRHM = 20 + 50 * mRnd.NextDouble();
          double oaHRT = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(oaDBT, oaRHM, 101.325);
          vrfSystem.OutdoorAirDrybulbTemperature = oaDBT;
          vrfSystem.OutdoorAirHumidityRatio = oaHRT;

          //屋内機条件
          for (int iuNum = 0; iuNum < vrfSystem.IndoorUnitNumber; iuNum++)
          {
            double iaDBT = 15 + 10 * mRnd.NextDouble();
            double iaRHM = 20 + 50 * mRnd.NextDouble();
            double iaSPT = iaDBT + 10 * mRnd.NextDouble();
            double iaHRT = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(iaDBT, iaRHM, 101.325);
            vrfSystem.SetIndoorUnitInletAirState(iuNum, iaDBT, iaHRT);
            vrfSystem.SetIndoorUnitSetpointTemperature(iuNum, iaSPT);
          }
          vrfSystem.UpdateState();
        }
        sw.Stop();
        Console.WriteLine(sw.Elapsed);
      }

      Console.Read();
    }

    #endregion

    #region VRF動作テスト（冷房）

    private static void testVRF1_C()
    {
      const double PIPE_FC = 0.89; //配管長補正係数
      const double NOM_PIPE_LENGTH = 7.5; //定格配管長
      const double LONG_PIPE_LENGTH = 100; //補正係数適用時の配管長
      const double IHEX_CAP_C = 14.0;
      const double NOM_IHEX_AFLOW = 34.5 * 1.2 / 60d;

      Refrigerant r410a = new Refrigerant(Refrigerant.Fluid.R410A);
      VRFUnit iHex
        = VRFSystem.MakeIndoorUnit_Cooling(NOM_IHEX_AFLOW, 0, -IHEX_CAP_C);

      //室外機を用意
      VRFSystem vrfSystem = new VRFSystem(
        r410a, 187 * 1.2 / 60, 0,
        -28.0, 8.93, -12.6, 2.35, -13.2, 1.94,
        NOM_PIPE_LENGTH, LONG_PIPE_LENGTH, PIPE_FC, iHex);
      vrfSystem.CurrentMode = VRFSystem.Mode.Cooling;

      //室内機を追加
      VRFUnit[] iHexes = new VRFUnit[]
      {
        VRFSystem.MakeIndoorUnit_Cooling(NOM_IHEX_AFLOW, 0, -IHEX_CAP_C),
        VRFSystem.MakeIndoorUnit_Cooling(NOM_IHEX_AFLOW, 0, -IHEX_CAP_C)
      };
      vrfSystem.AddIndoorUnit(iHexes);

      vrfSystem.MaxEvaporatingTemperature = 18;

      //定格条件テスト**************
      double oHmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(35, 24, 101.325);
      vrfSystem.OutdoorAirDrybulbTemperature = 35;
      vrfSystem.OutdoorAirHumidityRatio = oHmd;
      double iHmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(27, 19, 101.325);
      iHexes[0].SolveHeatLoad(-14.0, NOM_IHEX_AFLOW, 27, iHmd, false);
      for (int i = 0; i < vrfSystem.IndoorUnitNumber; i++)
      {
        vrfSystem.SetIndoorUnitMode(i, VRFUnit.Mode.Cooling);
        //vrfSystem.SetIndoorUnitSetpointTemperature(i, iHexes[0].OutletAirTemperature - 1);
        vrfSystem.SetIndoorUnitSetpointTemperature(i, iHexes[0].OutletAirTemperature);
      }
      vrfSystem.UpdateState();
      Console.WriteLine("Nominal Condition");
      Console.WriteLine((-2 * iHexes[0].HeatTransfer).ToString("F3") + "," + vrfSystem.CompressorElectricity.ToString("F3") + "," + vrfSystem.PartialLoadRate.ToString("F3"));

      //中間冷房標準条件テスト**************
      iHexes[0].SolveHeatLoad(-12.6 / 2d, NOM_IHEX_AFLOW, 27, iHmd, false);
      for (int i = 0; i < vrfSystem.IndoorUnitNumber; i++)
      {
        vrfSystem.SetIndoorUnitMode(i, VRFUnit.Mode.Cooling);
        vrfSystem.SetIndoorUnitSetpointTemperature(i, iHexes[0].OutletAirTemperature);
      }
      vrfSystem.UpdateState();
      Console.WriteLine("Mid load Condition");
      Console.WriteLine((-2 * iHexes[0].HeatTransfer).ToString("F3") + "," + vrfSystem.CompressorElectricity.ToString("F3") + "," + vrfSystem.PartialLoadRate.ToString("F3"));

      //中間冷房中間条件テスト**************
      oHmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(29, 19, 101.325);
      vrfSystem.OutdoorAirDrybulbTemperature = 29;
      vrfSystem.OutdoorAirHumidityRatio = oHmd;
      iHexes[0].SolveHeatLoad(-13.2 / 2d, NOM_IHEX_AFLOW, 27, iHmd, false);
      for (int i = 0; i < vrfSystem.IndoorUnitNumber; i++)
      {
        vrfSystem.SetIndoorUnitMode(i, VRFUnit.Mode.Cooling);
        vrfSystem.SetIndoorUnitSetpointTemperature(i, iHexes[0].OutletAirTemperature);
      }
      vrfSystem.UpdateState();
      Console.WriteLine("Mid load, Mid temp Condition");
      Console.WriteLine((-2 * iHexes[0].HeatTransfer).ToString("F3") + "," + vrfSystem.CompressorElectricity.ToString("F3") + "," + vrfSystem.PartialLoadRate.ToString("F3"));
    }

    private static void testVRF2_C()
    {
      const double PIPE_FC = 0.89; //配管長補正係数
      const double NOM_PIPE_LENGTH = 7.5; //定格配管長
      const double LONG_PIPE_LENGTH = 100; //補正係数適用時の配管長
      const double IHEX_CAP_C = 14.0;
      const double NOM_IHEX_AFLOW = 34.5 * 1.2 / 60d;

      Refrigerant r410a = new Refrigerant(Refrigerant.Fluid.R410A);
      VRFUnit iHex
        = VRFSystem.MakeIndoorUnit_Cooling(NOM_IHEX_AFLOW, 0, -IHEX_CAP_C);

      //室外機を用意
      VRFSystem vrfSystem = new VRFSystem(
        r410a, 187 * 1.2 / 60, 0,
        -28.0, 8.93, -12.6, 2.35, -13.2, 1.94,
        NOM_PIPE_LENGTH, LONG_PIPE_LENGTH, PIPE_FC, iHex);
      vrfSystem.CurrentMode = VRFSystem.Mode.Cooling;

      //室内機を追加
      VRFUnit[] iHexes = new VRFUnit[]
      {
        VRFSystem.MakeIndoorUnit_Cooling(NOM_IHEX_AFLOW, 0, -IHEX_CAP_C),
        VRFSystem.MakeIndoorUnit_Cooling(NOM_IHEX_AFLOW, 0, -IHEX_CAP_C)
      };
      vrfSystem.AddIndoorUnit(iHexes);

      //屋内機吸込条件はJISと同じで固定
      double iHmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(27, 19, 101.325);
      for (int i = 0; i < vrfSystem.IndoorUnitNumber; i++) vrfSystem.SetIndoorUnitMode(i, VRFUnit.Mode.Cooling);
      double oHmd;

      //負荷率と外気条件に関する感度分析*******************
      double[] dbt = new double[] { 41, 35, 29, 23 };
      double[] wbt = new double[] { 29, 24, 19, 14 };
      for (int i = 0; i < 20; i++) Console.Write("," + (5 * (i + 1)));
      Console.WriteLine();
      for (int oaIndx = 0; oaIndx < dbt.Length; oaIndx++)
      {
        //屋外機条件を設定
        oHmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature
          (dbt[oaIndx], wbt[oaIndx], 101.325);
        vrfSystem.OutdoorAirDrybulbTemperature = dbt[oaIndx];
        vrfSystem.OutdoorAirHumidityRatio = oHmd;

        //負荷率を変更して計算
        Console.Write("DB Temp = " + dbt[oaIndx].ToString("F1") + ": WB Temp = " + wbt[oaIndx].ToString("F1"));
        for (int i = 0; i < 20; i++)
        {
          double pl = 0.05 * (i + 1);
          iHexes[0].SolveHeatLoad(-14.0 * pl, NOM_IHEX_AFLOW, 27, iHmd, false);
          for (int j = 0; j < vrfSystem.IndoorUnitNumber; j++)
            vrfSystem.SetIndoorUnitSetpointTemperature(j, iHexes[0].OutletAirTemperature);
          vrfSystem.UpdateState();
          Console.Write("," + vrfSystem.CompressorElectricity.ToString("F3"));
        }
        Console.WriteLine("");
      }
      Console.WriteLine(""); Console.WriteLine("");

      //負荷率と負荷偏在に関する感度分析*******************
      for (int i = 0; i < 20; i++) Console.Write("," + (5 + 5 * i));
      Console.WriteLine();
      //屋外機条件を設定
      oHmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(35, 24, 101.325);
      vrfSystem.OutdoorAirDrybulbTemperature = 35;
      vrfSystem.OutdoorAirHumidityRatio = oHmd;

      //負荷率を変更して計算
      List<double> uniElcs = new List<double>();
      List<double> nUniElcs = new List<double>();
      List<double> uniDPs = new List<double>();
      List<double> nUniDPs = new List<double>();
      for (int i = 0; i < 20; i++)
      {
        double pl = 0.05 + 0.05 * i;
        iHexes[0].SolveHeatLoad(-14 * pl, NOM_IHEX_AFLOW, 27, iHmd, false);
        for (int j = 0; j < vrfSystem.IndoorUnitNumber; j++)
          vrfSystem.SetIndoorUnitSetpointTemperature(j, iHexes[0].OutletAirTemperature);
        vrfSystem.UpdateState();
        uniElcs.Add(vrfSystem.CompressorElectricity);
        uniDPs.Add(vrfSystem.CondensingPressure - vrfSystem.EvaporatingPressure);
      }
      for (int i = 0; i < 20; i++)
      {
        double pl = 2 * (0.05 + 0.05 * i) - 0.05;
        double pl1 = Math.Min(1.00, pl);
        double pl2 = 0.05 + (pl - pl1);
        iHexes[0].SolveHeatLoad(-14 * pl1, NOM_IHEX_AFLOW, 27, iHmd, false);
        vrfSystem.SetIndoorUnitSetpointTemperature(0, iHexes[0].OutletAirTemperature);
        iHexes[1].SolveHeatLoad(-14 * pl2, NOM_IHEX_AFLOW, 27, iHmd, false);
        vrfSystem.SetIndoorUnitSetpointTemperature(1, iHexes[1].OutletAirTemperature);
        vrfSystem.UpdateState();
        nUniElcs.Add(vrfSystem.CompressorElectricity);
        nUniDPs.Add(vrfSystem.CondensingPressure - vrfSystem.EvaporatingPressure);
      }
      //出力
      Console.Write("Uniform Electricity");
      for (int i = 0; i < uniElcs.Count; i++) Console.Write("," + uniElcs[i].ToString("F3"));
      Console.WriteLine();
      Console.Write("Non Uniform Electricity");
      for (int i = 0; i < nUniElcs.Count; i++) Console.Write("," + nUniElcs[i].ToString("F3"));
      Console.WriteLine();
      Console.Write("Uniform Delta P");
      for (int i = 0; i < uniDPs.Count; i++) Console.Write("," + (0.001 * uniDPs[i]).ToString("F3"));
      Console.WriteLine();
      Console.Write("Non Uniform Delta P");
      for (int i = 0; i < nUniDPs.Count; i++) Console.Write("," + (0.001 * nUniDPs[i]).ToString("F3"));
      Console.WriteLine(); Console.WriteLine();

      //屋内外機の吸込温度に関する感度分析*******************
      double[,] elecMap = new double[21, 21];
      double[,] copMap = new double[21, 21];
      double oRHmd = 70;
      double iRHmd = 47;
      for (int i = 0; i <= 20; i++)
      {
        double oaDBT = 27 + 0.5 * i;
        vrfSystem.OutdoorAirDrybulbTemperature = oaDBT;
        vrfSystem.OutdoorAirHumidityRatio = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(oaDBT, oRHmd, 101.325);
        for (int j = 0; j <= 20; j++)
        {
          double iaDBT = 25 + 0.5 * j;
          double iaHRT = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(iaDBT, iRHmd, 101.325);
          iHexes[0].SolveHeatLoad(-14, NOM_IHEX_AFLOW, iaDBT, iaHRT, false);
          for (int k = 0; k < iHexes.Length; k++)
          {
            iHexes[k].InletAirTemperature = iaDBT;
            iHexes[k].InletAirHumidityRatio = iaHRT;
            iHexes[k].OutletAirSetpointTemperature = iHexes[0].OutletAirTemperature;
          }
          vrfSystem.UpdateState();
          elecMap[i, j] = vrfSystem.CompressorElectricity;
          copMap[i, j] = -iHexes[0].HeatTransfer * 2 / vrfSystem.CompressorElectricity;
        }
      }

      for (int i = 0; i <= 20; i++) Console.Write("," + (25 + 0.5 * i).ToString("F1"));
      Console.WriteLine();
      for (int i = 0; i <= 20; i++)
      {
        Console.Write(27 + 0.5 * i);
        for (int j = 0; j <= 20; j++)
          Console.Write("," + elecMap[i, j].ToString("F3"));
        Console.WriteLine();
      }
      Console.WriteLine();
      for (int i = 0; i <= 20; i++)
      {
        Console.Write(27 + 0.5 * i);
        for (int j = 0; j <= 20; j++)
          Console.Write("," + copMap[i, j].ToString("F3"));
        Console.WriteLine();
      }
      Console.WriteLine(); Console.WriteLine();

      //屋外機の吸込温湿度に関する感度分析*******************
      vrfSystem.UseWaterSpray = true;
      elecMap = new double[21, 21];
      copMap = new double[21, 21];
      //室内機は定格条件
      for (int i = 0; i < 2; i++)
      {
        iHexes[i].InletAirTemperature = 27;
        iHexes[i].InletAirHumidityRatio =
          MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(27, 19, 101.325);
      }
      iHexes[0].SolveHeatLoad(-14, NOM_IHEX_AFLOW, iHexes[0].InletAirTemperature, iHexes[0].InletAirHumidityRatio, false);
      for (int i = 0; i < 2; i++) iHexes[i].OutletAirSetpointTemperature = iHexes[0].OutletAirTemperature;
      //感度分析
      for (int i = 0; i <= 20; i++)
      {
        double oaDBT = 27 + 0.5 * i;
        vrfSystem.OutdoorAirDrybulbTemperature = oaDBT;
        for (int j = 0; j <= 20; j++)
        {
          double oaRHMD = 5 * j;
          vrfSystem.OutdoorAirHumidityRatio = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(oaDBT, oaRHMD, 101.325);
          vrfSystem.UpdateState();
          elecMap[i, j] = vrfSystem.CompressorElectricity;
          copMap[i, j] = -iHexes[0].HeatTransfer * 2 / vrfSystem.CompressorElectricity;
        }
      }
      vrfSystem.UseWaterSpray = false;

      for (int i = 0; i <= 20; i++) Console.Write("," + (5 * i).ToString("F1"));
      Console.WriteLine();
      for (int i = 0; i <= 20; i++)
      {
        Console.Write(27 + 0.5 * i);
        for (int j = 0; j <= 20; j++)
          Console.Write("," + elecMap[i, j].ToString("F3"));
        Console.WriteLine();
      }
      Console.WriteLine();
      for (int i = 0; i <= 20; i++)
      {
        Console.Write(27 + 0.5 * i);
        for (int j = 0; j <= 20; j++)
          Console.Write("," + copMap[i, j].ToString("F3"));
        Console.WriteLine();
      }
      Console.WriteLine(); Console.WriteLine();

      //高低差と配管長に関する感度分析*******************
      double[,] capMap = new double[20, 11];
      vrfSystem.OutdoorAirDrybulbTemperature = 35;
      vrfSystem.OutdoorAirHumidityRatio =
        MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(35, 24, 101.325);
      //室内機は定格条件
      for (int i = 0; i < 2; i++)
      {
        iHexes[i].InletAirTemperature = 27;
        iHexes[i].InletAirHumidityRatio =
          MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(27, 19, 101.325);
      }
      iHexes[0].SolveHeatLoad(-14, NOM_IHEX_AFLOW, iHexes[0].InletAirTemperature, iHexes[0].InletAirHumidityRatio, false);
      for (int i = 0; i < 2; i++) iHexes[i].OutletAirSetpointTemperature = iHexes[0].OutletAirTemperature;
      //感度分析
      for (int i = 0; i <= 19; i++)
      {
        vrfSystem.PipeLength = 5 + 5 * i;
        for (int j = 0; j <= 10; j++)
        {
          vrfSystem.IndoorUnitHeight = j * 5;
          vrfSystem.UpdateState();
          for (int k = 0; k < 2; k++)
            capMap[i, j] -= iHexes[k].HeatTransfer;
        }
      }

      for (int i = 0; i <= 10; i++) Console.Write("," + (i * 5).ToString("F1"));
      Console.WriteLine();
      for (int i = 0; i <= 19; i++)
      {
        Console.Write(5 + 5 * i + "m");
        for (int j = 0; j <= 10; j++)
          Console.Write("," + capMap[i, j].ToString("F3"));
        Console.WriteLine();
      }
      Console.WriteLine();

    }

    private static void testVRFSpeed_C()
    {
      const double PIPE_FC = 0.88; //配管長補正係数
      const double NOM_PIPE_LENGTH = 7.5; //定格配管長
      const double LONG_PIPE_LENGTH = 100; //補正係数適用時の配管長
      const double IHEX_CAP_C = 5.6;
      const double NOM_IHEX_AFLOW = 15.5 * 1.2 / 60d;

      Refrigerant r410a = new Refrigerant(Refrigerant.Fluid.R410A);
      VRFUnit iHex = VRFSystem.MakeIndoorUnit_Cooling(NOM_IHEX_AFLOW, 0, -IHEX_CAP_C);

      //室外機を用意
      VRFSystem vrfSystem = new VRFSystem(
        r410a, (191 + 160) * 1.2 / 60, 0,
        -56.0, 17.3, -25.2, 4.97, -26.4, 4.02,
        NOM_PIPE_LENGTH, LONG_PIPE_LENGTH, PIPE_FC, iHex);
      vrfSystem.CurrentMode = VRFSystem.Mode.Cooling;

      //室内機を追加
      for (int i = 0; i < 10; i++)
        vrfSystem.AddIndoorUnit(
          VRFSystem.MakeIndoorUnit_Cooling(NOM_IHEX_AFLOW, 0, -IHEX_CAP_C));

      //乱数製造機
      MersenneTwister mRnd = new MersenneTwister(1);

      //ストップウォッチ
      System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

      //時間計測開始
      for (int trial = 0; trial < 10; trial++)
      {
        Console.Write((trial + 1) + " : ");
        sw.Reset();
        sw.Start();

        for (int i = 0; i < 10000; i++)
        {
          //屋外機条件
          double oaDBT = 25 + 15 * mRnd.NextDouble();
          double oaRHM = 20 + 50 * mRnd.NextDouble();
          double oaHRT = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(oaDBT, oaRHM, 101.325);
          vrfSystem.OutdoorAirDrybulbTemperature = oaDBT;
          vrfSystem.OutdoorAirHumidityRatio = oaHRT;

          //屋内機条件
          for (int iuNum = 0; iuNum < vrfSystem.IndoorUnitNumber; iuNum++)
          {
            double iaDBT = 20 + 10 * mRnd.NextDouble();
            double iaRHM = 20 + 50 * mRnd.NextDouble();
            double iaSPT = iaDBT - 12 * mRnd.NextDouble();
            double iaHRT = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(iaDBT, iaRHM, 101.325);
            vrfSystem.SetIndoorUnitInletAirState(iuNum, iaDBT, iaHRT);
            vrfSystem.SetIndoorUnitSetpointTemperature(iuNum, iaSPT);
          }
          vrfSystem.UpdateState();
        }
        sw.Stop();
        Console.WriteLine(sw.Elapsed);
      }

      Console.Read();
    }

    #endregion

    #region 室内機蒸発・凝縮温度検討

    private static void testIndoorHexTemperature()
    {
      //ダイキンSラウンドフロー
      //double[] bpfs = new double[] { 0.22, 0.22, 0.24, 0.17, 0.13, 0.11, 0.18, 0.07, 0.07, 0.07 };
      double[] shfs = new double[] { 0.91, 0.79, 0.75, 0.73, 0.79, 0.78, 0.79, 0.69, 0.69, 0.69 };
      double[] cCaps = new double[] { 2.8, 3.6, 4.5, 5.6, 7.1, 8.0, 9.0, 11.2, 14.0, 16.0 };
      double[] hCaps = new double[] { 3.2, 4.0, 5.0, 6.3, 8.0, 9.0, 10.0, 12.5, 16.0, 18.0 };
      double[] aFlows = new double[] { 12.5, 14.5, 15.5, 22, 23.5, 30, 30, 33, 34.5, 35.5 };
      //日立天カセ4方向
      //double[] cCaps = new double[] { 2.8, 3.6, 4.0, 4.5, 5.0, 5.6, 6.3, 7.1, 8.0, 9.0, 11.2, 14.0, 16.0 };
      //double[] hCaps = new double[] { 3.2, 4.0, 4.8, 5.0, 5.6, 6.3, 7.5, 8.5, 9.0, 10.0, 12.5, 16.0, 18.0 };
      //double[] aFlows = new double[] { 15, 17, 20, 20, 22, 22, 26, 27, 27, 29, 36, 37, 37 };
      //東芝4方向吹き出し
      //double[] cCaps = new double[] { 2.8, 3.6, 4.5, 5.6, 6.3, 7.1, 8.0, 9.0, 11.2, 14.0, 16.0 };
      //double[] hCaps = new double[] { 3.2, 4.0, 5.0, 6.3, 7.5, 8.5, 9.0, 10.0, 12.5, 16.0, 18.0 };
      //double[] aFlows = new double[] { 14.1, 14.1, 16, 17.5, 20.8, 22.9, 22.9, 36.4, 37.7, 37.7 };

      double[] evpTemps = new double[] { 3, 4, 5, 6, 7, 8, 9, 10 }; //11以上だと伝熱面積初期化エラー
      const double IA_DBT_C = 27;
      const double IA_WBT_C = 19;
      const double IA_DBT_H = 27;
      double hmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature
        (IA_DBT_C, IA_WBT_C, 101.325);

      VRFUnit iHex;
      double[,] sAreas = new double[aFlows.Length, evpTemps.Length];
      double[,] cndTemps = new double[aFlows.Length, evpTemps.Length];
      double[,] shfErr = new double[aFlows.Length, evpTemps.Length];

      for (int i = 0; i < aFlows.Length; i++)
      {
        double evpS = 0;
        double aFlow = aFlows[i] * 1.2 / 60;
        Roots.ErrorFunction eFnc = delegate (double cTemp)
        {
          iHex = new VRFUnit
              (aFlow, 0, cTemp, hCaps[i], IA_DBT_H, 0.006);
          return evpS - iHex.SurfaceArea_Condenser;
        };

        for (int etIndex = 0; etIndex < evpTemps.Length; etIndex++)
        {
          iHex = new VRFUnit(aFlow, evpTemps[etIndex], 0, -cCaps[i], IA_DBT_C, hmd, 95);
          iHex.SolveHeatLoad(-cCaps[i], aFlow, IA_DBT_C, hmd, false);
          evpS = iHex.SurfaceArea_Evaporator;
          sAreas[i, etIndex] = evpS;
          //shfErr[i, etIndex] = (iHex.SensibleHeatTransfer / iHex.HeatTransfer) - shfs[i];
          shfErr[i, etIndex] = (iHex.SensibleHeatTransfer / iHex.HeatTransfer);

          //収束計算
          double cdt = Roots.NewtonBisection(eFnc, 55, 0.001, 0.01, 0.01, 10);
          cndTemps[i, etIndex] = cdt;
        }
      }

      for (int i = 0; i < cndTemps.GetLength(0); i++)
      {
        Console.Write(cCaps[i]);
        for (int j = 0; j < cndTemps.GetLength(1); j++)
          Console.Write("," + cndTemps[i, j]);
        Console.WriteLine();
      }

      Console.WriteLine(); Console.WriteLine();
      for (int i = 0; i < shfErr.GetLength(0); i++)
      {
        Console.Write(cCaps[i]);
        for (int j = 0; j < shfErr.GetLength(1); j++)
          Console.Write("," + shfErr[i, j]);
        Console.WriteLine();
      }

    }

    #endregion

    #region ビルマル初期化検証

    private static void initTest_Heating()
    {
      double[] nomCaps = new double[] { 25, 31.5, 37.5, 45, 16, 18, 25, 31.5, 37.5, 45, 50, 56, 14, 16, 22.4, 28, 33.5, 40, 45, 31.5, 37.5, 45, 50, 56, 61.5, 25, 31.5, 37.5, 25, 31.5, 37.5, 45, 50, 56, 63, 69, 77.5, 16, 18, 25, 31.5, 37.5, 45, 50, 56 };
      double[] nomElcs = new double[] { 6.32, 8.68, 10, 13.1, 3.81, 4.48, 6.85, 9.09, 10.8, 13.9, 16, 17.4, 3.66, 4.35, 5.53, 7.56, 9.8, 11, 13.8, 10.1, 10.6, 12.25, 14.55, 17.17, 19.1, 6.08, 8.85, 9.45, 6.22, 8.93, 11.7, 15.2, 17.4, 20.2, 23.5, 22.9, 29, 3.53, 4.06, 6.06, 8.58, 9.39, 12.7, 15.4, 17.3 };
      double[] midCaps = new double[] { 11.3, 14.2, 16.9, 20.3, 7.2, 8.1, 11.3, 14.2, 16.9, 20.3, 22.5, 25.2, 6.4, 7.2, 10.1, 12.6, 15.1, 19.2, 21, 14.2, 16.9, 20.3, 22.5, 25.5, 27.7, 11.3, 14.2, 16.9, 11.3, 14.2, 17, 20.3, 22.7, 25.6, 28.4, 31.1, 34.9, 7.4, 9, 11.3, 14.2, 16.9, 20.3, 22.5, 25.6 };
      double[] midElcs = new double[] { 2.06, 2.54, 2.94, 3.89, 1.27, 1.42, 2.23, 2.76, 3.06, 3.94, 4.3, 5.22, 1.28, 1.47, 1.89, 2.27, 3.02, 3.69, 4.26, 2.84, 3.23, 4.16, 4.7, 5.38, 5.7, 2.05, 2.69, 3.16, 1.99, 2.55, 3.24, 3.43, 3.96, 4.54, 5.14, 5.65, 6.37, 1.29, 1.68, 1.93, 2.48, 2.79, 3.5, 4.05, 4.82 };
      double[] oHexFlows = new double[] { 218, 187, 187, 210, 160, 160, 183, 164, 191, 243, 281, 254, 160, 165, 165, 175, 193, 200, 216, 223, 223, 292, 292, 304, 315, 230, 290, 290, 165, 170, 190, 239, 256, 256, 329, 329, 348, 150, 170, 185, 219, 219, 243, 326, 362 };

      const double PIPE_FC = 0.91; //配管長補正係数
      const double NOM_PIPE_LENGTH = 7.5; //定格配管長
      const double LONG_PIPE_LENGTH = 80; //補正係数適用時の配管長
      const double IHEX_CAP_C = 14.0;
      const double IHEX_CAP_H = 16.0;
      const double NOM_IHEX_AFLOW = 34.5 * 1.2 / 60d;

      Refrigerant r410a = new Refrigerant(Refrigerant.Fluid.R410A);
      VRFUnit iHex = VRFSystem.MakeIndoorUnit(NOM_IHEX_AFLOW, 0, -IHEX_CAP_C, 0, IHEX_CAP_H);
      iHex.CurrentMode = VRFUnit.Mode.Heating;

      double[] pCoefs = new double[nomCaps.Length];
      double[] nomHeads = new double[nomCaps.Length];
      double[] midHeads = new double[nomCaps.Length];

      for (int i = 0; i < pCoefs.Length; i++)
      {
        double pCoef, nomHead, midHead;
        VRFUnit oUnit;

        VRFSystem.EstimateOutdoorUnitNominalParameters_Heating(
          r410a, oHexFlows[i] * 1.2 / 60d, 0, nomCaps[i], 0, NOM_PIPE_LENGTH, LONG_PIPE_LENGTH, PIPE_FC,
          out pCoef, out nomHead, out oUnit);
        VRFSystem.EstimatePartialLoadParameters_Heating(
          r410a, NOM_PIPE_LENGTH, pCoef, nomHead, nomCaps[i], oUnit, iHex, 0, midCaps[i], out midHead);

        VRFSystem.MakePartialLoadCharacteristicCurve(nomHead, nomElcs[i], midHead, midElcs[i], out double cA, out double cB);

        pCoefs[i] = pCoef;
        nomHeads[i] = nomHead;
        midHeads[i] = midHead;
      }

      for (int i = 0; i < pCoefs.Length; i++)
      {
        Console.Write("No. " + (i + 1));
        Console.WriteLine(", " + nomHeads[i] + ", " + nomElcs[i]);
        Console.WriteLine(", " + midHeads[i] + ", " + midElcs[i]);
        Console.WriteLine("");
      }
    }

    private static void initTest_Cooling()
    {
      double[] nomCaps = new double[] { 22.4, 28, 33.5, 40, 14, 16, 22.4, 28, 33.5, 40, 45, 50, 14, 16, 22.4, 28, 33.5, 40, 45, 28, 33.5, 40, 45, 50, 56, 22.4, 28, 33.5, 22.4, 28, 33.5, 40, 45, 50, 56, 61.5, 67, 14, 16, 22.4, 28, 33.5, 40, 45, 50 };
      double[] nomElcs = new double[] { 6.07, 8.93, 9.74, 12.5, 3.64, 4.47, 6.01, 9.69, 10.4, 13.3, 16.2, 18.9, 4.17, 5.32, 6.65, 9.97, 11.9, 14.7, 15.9, 9.3, 11.04, 12.65, 15.62, 19.21, 21, 6.18, 8.59, 9.29, 6.5, 10.1, 10.8, 14.7, 15.7, 19, 23.6, 22.6, 24.5, 3.32, 3.86, 6.21, 8.71, 10.7, 14.3, 13.2, 15.4 };
      double[] midCaps1 = new double[] { 10.1, 12.6, 15.1, 18, 7.4, 7.2, 10.1, 12.6, 15.1, 18, 20.3, 22.5, 6.9, 7.2, 10.2, 12.8, 15.2, 18, 20.4, 12.6, 15.1, 18, 20.3, 22.7, 25.2, 10.1, 12.6, 15.1, 10.1, 12.6, 15.1, 18, 20.3, 22.7, 25.2, 27.7, 30.2, 7.2, 8.8, 10.1, 12.6, 15.1, 18, 20.3, 22.5 };
      double[] midElcs1 = new double[] { 1.89, 2.35, 2.59, 3.5, 1.51, 1.51, 2.21, 2.66, 2.82, 3.79, 4.34, 5.08, 1.29, 1.35, 1.9, 2.82, 3, 3.72, 4.22, 2.42, 2.89, 3.67, 4.3, 5.07, 5.2, 1.84, 2.45, 2.84, 1.84, 2.37, 2.89, 3.54, 4.1, 4.69, 5.27, 6.06, 6.44, 1.3, 1.68, 1.74, 2.27, 2.72, 3.34, 4.1, 4.58 };
      double[] midCaps2 = new double[] { 10.6, 13.2, 15.7, 18.8, 7.8, 7.8, 10.6, 14.4, 15.8, 18.7, 21.2, 23.9, 7.5, 7.8, 10.4, 13, 16.3, 19, 21.6, 13, 15.3, 17.8, 19.7, 22.2, 24.7, 9.9, 12.5, 15, 10.6, 13.2, 15.9, 18.7, 21.3, 23.6, 26.3, 29.1, 31.7, 7.6, 9.2, 10.6, 13.2, 15.9, 18.9, 21.3, 23.6 };
      double[] midElcs2 = new double[] { 1.55, 1.94, 2.12, 2.74, 1.21, 1.24, 1.74, 2.18, 2.32, 3.27, 3.71, 4.35, 1.1, 1.16, 1.48, 2.09, 2.54, 3.23, 3.65, 2.1, 2.37, 3, 3.52, 4.13, 4.05, 1.48, 1.85, 2.2, 1.53, 1.94, 2.29, 2.89, 3.37, 3.85, 4.21, 4.74, 5.04, 1.07, 1.34, 1.39, 1.79, 2.16, 2.64, 3.05, 3.43 };
      double[] oHexFlows = new double[] { 218, 187, 187, 210, 160, 160, 183, 164, 191, 243, 281, 254, 160, 165, 165, 175, 193, 200, 216, 223, 223, 292, 292, 304, 315, 230, 290, 290, 165, 170, 190, 239, 256, 256, 329, 329, 348, 150, 170, 185, 219, 219, 243, 326, 362 };

      const double NOM_IHEX_AFLOW = 15.5 * 1.2 / 60d;
      const double PIPE_FC = 0.88; //配管長補正係数
      const double NOM_PIPE_LENGTH = 7.5; //定格配管長
      const double FC_PIPE_LENGTH = 100; //補正係数適用時の配管長
      const double IHEX_CAP_C = 5.6;

      double[] pCoefs = new double[nomCaps.Length];
      double[] nomHeads = new double[nomCaps.Length];
      double[] midHeads1 = new double[nomCaps.Length];
      double[] midHeads2 = new double[nomCaps.Length];

      Refrigerant r410a = new Refrigerant(Refrigerant.Fluid.R410A);
      VRFUnit iHex = VRFSystem.MakeIndoorUnit_Cooling(NOM_IHEX_AFLOW, 0, -IHEX_CAP_C);
      for (int i = 0; i < pCoefs.Length; i++)
      {
        double pCoef, nomHead, midHead1, midHead2;
        VRFUnit oUnit;

        VRFSystem.EstimateOutdoorUnitNominalParameters_Cooling(
          r410a, oHexFlows[i] * 1.2 / 60d, 0, -nomCaps[i], NOM_PIPE_LENGTH, FC_PIPE_LENGTH, PIPE_FC,
          out pCoef, out nomHead, out oUnit);
        VRFSystem.EstimatePartialLoadParameters_Cooling(
          r410a, NOM_PIPE_LENGTH, pCoef, nomHead, -nomCaps[i], oUnit, iHex, -midCaps1[i], -midCaps2[i], out midHead1, out midHead2);

        pCoefs[i] = pCoef;
        nomHeads[i] = nomHead;
        midHeads1[i] = midHead1;
        midHeads2[i] = midHead2;

        double cA, cB;
        VRFSystem.MakePartialLoadCharacteristicCurve(
          nomHead, nomElcs[i], midHead1, midElcs1[i], midHead2, midElcs2[i], out cA, out cB);
      }

      for (int i = 0; i < pCoefs.Length; i++)
      {
        Console.Write("No. " + (i + 1));
        Console.WriteLine(", " + nomHeads[i] + ", " + nomElcs[i]);
        Console.WriteLine(", " + midHeads1[i] + ", " + midElcs1[i]);
        Console.WriteLine(", " + midHeads2[i] + ", " + midElcs2[i]);
      }
    }

    #endregion

    #region クロスフィン熱交換器単体の動作検証

    /// <summary>蒸発器の冷却・除湿・着氷現象の試験</summary>
    private static void CrossFinEvaporatorTest()
    {
      //蒸発器初期化
      double hr = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(7, 6, 101.325);
      VRFUnit evp = new VRFUnit(167.0 / 60 * 1.2, 0, 2, -13, 7, hr, 95);
      //VRFUnit evp = new VRFUnit(187.0 / 60 * 1.2, 0, 10, -23.0, 7, hr, 95);
      evp.CurrentMode = VRFUnit.Mode.Cooling;

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
            evp.UpdateWithRefrigerantTemperature(te[j], evp.NominalAirFlowRate, i, hr, false);
            sWriter.Write("," + (-evp.HeatTransfer));
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
            evp.UpdateWithRefrigerantTemperature(-10, evp.NominalAirFlowRate, i, hr, false);
            sWriter.Write("," + (-evp.HeatTransfer));
            sWriter.Write("," + evp.DefrostLoad + "," + evp.OutletAirTemperature);
          }
          sWriter.WriteLine();
        }
      }
    }

    /// <summary>水噴霧形凝縮器の試験</summary>
    private static void CrossFinCondensorTest()
    {
      //凝縮器初期化
      double hr = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(35, 55, 101.325);
      VRFUnit cnd = new VRFUnit(167d / 60 * 1.2, 0, 45, 25, 35, hr);
      cnd.CurrentMode = VRFUnit.Mode.Heating;
      cnd.UseWaterSpray = true;
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
            cnd.UpdateWithRefrigerantTemperature(45, cnd.NominalAirFlowRate, i, hr, false);
            sWriter.Write("," + cnd.HeatTransfer);
            cnd.SprayEffectiveness = 0.6;
            cnd.UpdateWithRefrigerantTemperature(45, cnd.NominalAirFlowRate, i, hr, false);
            sWriter.Write("," + cnd.HeatTransfer);
            sWriter.Write("," + cnd.WaterSupply * 3600);
          }
          sWriter.WriteLine();
        }
      }
    }

    #endregion

    #region デフォルトモデル作成テスト

    private static void testDefaultOutdoorUnitInitialization()
    {
      foreach (VRFInitializer.OutdoorUnitModel ouModel in Enum.GetValues(typeof(VRFInitializer.OutdoorUnitModel)))
      {
        foreach (VRFInitializer.CoolingCapacity cap in Enum.GetValues(typeof(VRFInitializer.CoolingCapacity)))
        {
          Console.Write(ouModel + "  :  " + cap);
          try
          {
            VRFSystem vrfs = VRFInitializer.MakeOutdoorUnit(ouModel, cap, 0, false);
            ImmutableVRFUnit oUnitC = vrfs.OutdoorUnit_C;
            ImmutableVRFUnit oUnitH = vrfs.OutdoorUnit_H;
            Console.WriteLine(
              "  :  " + oUnitC.SurfaceArea_Condenser.ToString("F2") + " , " + oUnitH.SurfaceArea_Evaporator.ToString("F2") +
              "  :  " + vrfs.HeadEfficiencyRatioCoefA_C.ToString("F2") + " , " + vrfs.HeadEfficiencyRatioCoefB_C.ToString("F2") +
              "  :  " + vrfs.HeadEfficiencyRatioCoefA_H.ToString("F2") + " , " + vrfs.HeadEfficiencyRatioCoefB_H.ToString("F2") +
              "  :  " + oUnitC.NominalFanElectricity_H.ToString("F2") + " , " + oUnitH.NominalFanElectricity_C.ToString("F2"));
          }
          catch (Exception exp)
          {
            Console.WriteLine(exp.Message);
          }
        }
      }
    }

    private static void testDefaultIndoorUnitInitialization()
    {
      foreach (VRFInitializer.IndoorUnitType iuType in Enum.GetValues(typeof(VRFInitializer.IndoorUnitType)))
      {
        foreach (VRFInitializer.CoolingCapacity cap in Enum.GetValues(typeof(VRFInitializer.CoolingCapacity)))
        {
          Console.Write(iuType + "  :  " + cap);
          try
          {
            VRFUnit unt = VRFInitializer.MakeIndoorUnit_Hitachi(iuType, cap);
            Console.WriteLine(
              "  :  " + unt.SurfaceArea_Condenser.ToString("F2") + " , " + unt.SurfaceArea_Evaporator.ToString("F2") +
              "  :  " + unt.NominalFanElectricity_H.ToString("F2") + " , " + unt.NominalFanElectricity_C.ToString("F2"));
          }
          catch (Exception exp)
          {
            Console.WriteLine(exp.Message);
          }
        }
      }
    }

    #endregion

    #region 宮田さんの論文の実測結果による精度検証

    private static void TestMiyataData()
    {
      //東芝製SMMS-i（スーパーモジュールヒートポンプ）
      VRFSystem vrfSystem = VRFInitializer.MakeOutdoorUnit
        (VRFInitializer.OutdoorUnitModel.Toshiba_MMY, VRFInitializer.CoolingCapacity.Miyata22_4, 0, false);
      VRFUnit[] iHexes = new VRFUnit[]
      {
        VRFInitializer.MakeIndoorUnit_Toshiba(VRFInitializer.IndoorUnitType.CeilingFourWay, VRFInitializer.CoolingCapacity.C5_6),
        VRFInitializer.MakeIndoorUnit_Toshiba(VRFInitializer.IndoorUnitType.CeilingFourWay, VRFInitializer.CoolingCapacity.C5_6),
        VRFInitializer.MakeIndoorUnit_Toshiba(VRFInitializer.IndoorUnitType.CeilingFourWay, VRFInitializer.CoolingCapacity.C5_6),
        VRFInitializer.MakeIndoorUnit_Toshiba(VRFInitializer.IndoorUnitType.CeilingFourWay, VRFInitializer.CoolingCapacity.C5_6)
      };
      vrfSystem.AddIndoorUnit(iHexes);
      vrfSystem.MaxEvaporatingTemperature = 20;
      vrfSystem.MinCondensingTemperature = 30;
      vrfSystem.ControlThermoOffWithSensibleHeat = false; //全熱基準でサーモを制御（処理負荷を完全に合わせるため）

      //各テストケースの室内機負荷と吸込温湿度条件リスト**************************
      //冷房時の室内機負荷
      Dictionary<string, double[]> pLoadsC = new Dictionary<string, double[]>()
      {
        {"C-H1a", new double[]{ 5.49, 5.89, 4.72, 4.73 } },
        {"C-H1b", new double[]{ 6.02, 6.34, 5.76, 6.08 } },
        {"C-H2", new double[]{ 1.51, 5.19, 4.05, 4.01 } },
        {"C-H3", new double[]{ 4.70, 4.88, 4.41, 4.03 } },
        {"C-H4", new double[]{ 4.83, 4.94, 2.97, 3.15 } },
        {"C-H5", new double[]{ 5.13, 0.00, 4.60, 4.57 } },
        {"C-M1", new double[]{ 3.08, 3.19, 2.93, 2.95 } },
        {"C-M2", new double[]{ 3.01, 3.22, 3.03, 2.93 } },
        {"C-M3", new double[]{ 1.06, 4.07, 3.44, 3.27 } },
        {"C-M4", new double[]{ 1.68, 1.83, 3.60, 3.59 } },
        {"C-M5", new double[]{ 0.78, 0.96, 4.45, 4.53 } },
        {"C-M6", new double[]{ 4.12, 0.00, 3.53, 3.61 } },
        {"C-M7", new double[]{ 5.63, 5.81, 0.00, 0.00 } },
        {"C-L1", new double[]{ 1.42, 1.71, 1.05, 1.36 } },
        {"C-L2", new double[]{ 1.79, 0.00, 1.31, 1.62 } },
        {"C-L3", new double[]{ 0.00, 0.00, 1.79, 3.79 } },
        {"C-L4", new double[]{ 1.23, 1.53, 0.85, 1.73 } },
        {"C-L5", new double[]{ 1.68, 0.00, 1.29, 1.38 } },
        {"C-L6", new double[]{ 0.00, 0.00, 1.66, 1.81 } }
      };

      //冷房時の室内機吸込温度
      Dictionary<string, double[]> itempsC = new Dictionary<string, double[]>()
      {
        {"C-H1a", new double[]{ 28.44,28.70,27.44,28.92 } },
        {"C-H1b", new double[]{ 27.03,27.49,27.17,28.58 } },
        {"C-H2", new double[]{ 26.81,29.22,28.01,29.47 } },
        {"C-H3", new double[]{ 27.69,27.42,28.03,28.35 } },
        {"C-H4", new double[]{ 30.21,29.81,27.44,28.42 } },
        {"C-H5", new double[]{ 28.56,0.00,27.75,29.21 } },
        {"C-M1", new double[]{ 26.82,26.63,27.04,28.12 } },
        {"C-M2", new double[]{ 26.82,26.84,27.17,28.04 } },
        {"C-M3", new double[]{ 26.67,27.51,27.48,28.43 } },
        {"C-M4", new double[]{ 27.06,27.77,27.63,28.92 } },
        {"C-M5", new double[]{ 26.67,27.20,27.97,29.35 } },
        {"C-M6", new double[]{ 28.30,0.00,27.13,28.29 } },
        {"C-M7", new double[]{ 28.49,27.52,0.00,0.00 } },
        {"C-L1", new double[]{ 26.83,27.23,27.65,27.66 } },
        {"C-L2", new double[]{ 26.72,0.00,27.56,27.49 } },
        {"C-L3", new double[]{ 0.00,0.00,26.37,27.09 } },
        {"C-L4", new double[]{ 26.57,27.02,27.19,27.12 } },
        {"C-L5", new double[]{ 26.38,0.00,27.34,27.49 } },
        {"C-L6", new double[]{ 0.00,0.00,26.93,27.20 } }
      };

      //暖房時の室内機負荷
      Dictionary<string, double[]> pLoadsH = new Dictionary<string, double[]>()
      {
        {"H-H1", new double[]{ 5.83,5.26,5.72,6.00 } },
        {"H-H2", new double[]{ 0.98,5.28,6.01,6.27 } },
        {"H-H3", new double[]{ 4.17,3.82,4.03,4.45 } },
        {"H-H4", new double[]{ 5.42,5.02,2.71,2.98 } },
        {"H-H5", new double[]{ 5.38,0.00,5.81,4.94 } },
        {"H-M1", new double[]{ 2.78,2.52,2.99,2.71 } },
        {"H-M2", new double[]{ 2.75,2.69,2.79,3.18 } },
        {"H-M3", new double[]{ 5.07,1.87,2.77,2.61 } },
        {"H-M4", new double[]{ 1.03,0.76,4.58,4.74 } },
        {"H-M5", new double[]{ 3.93,0.00,4.10,3.70 } },
        {"H-M6", new double[]{ 0.00,0.00,5.60,6.05 } },
        {"H-L1", new double[]{ 1.42,1.04,1.22,1.79 } },
        {"H-L2", new double[]{ 2.03,0.00,1.48,2.26 } },
        {"H-L3", new double[]{ 0.00,0.00,2.42,3.33 } },
        {"H-L4", new double[]{ 0.96,0.83,0.85,0.76 } },
        {"H-L5", new double[]{ 0.94,0.00,1.58,1.12 } },
        {"H-L6", new double[]{ 0.00,0.00,1.45,1.64 } }
      };

      //暖房時の室内機吸込温度
      Dictionary<string, double[]> itempsH = new Dictionary<string, double[]>()
      {
        {"H-H1", new double[]{ 19.27,19.71,20.33,19.68 } },
        {"H-H2", new double[]{ 21.77,19.72,19.10,18.67 } },
        {"H-H3", new double[]{ 19.73,19.82,19.95,20.38 } },
        {"H-H4", new double[]{ 14.73,15.10,20.86,20.74 } },
        {"H-H5", new double[]{ 20.19,0.00,19.80,20.13 } },
        {"H-M1", new double[]{ 20.49,19.97,20.44,19.86 } },
        {"H-M2", new double[]{ 20.95,20.34,21.40,21.26 } },
        {"H-M3", new double[]{ 14.24,20.56,20.52,20.32 } },
        {"H-M4", new double[]{ 21.21,21.18,16.10,15.74 } },
        {"H-M5", new double[]{ 19.17,0.00,20.26,19.94 } },
        {"H-M6", new double[]{ 0.00,0.00,19.95,19.38 } },
        {"H-L1", new double[]{ 20.36,20.43,21.34,21.27 } },
        {"H-L2", new double[]{ 20.01,0.00,21.34,21.14 } },
        {"H-L3", new double[]{ 0.00,0.00,20.82,20.53 } },
        {"H-L4", new double[]{ 20.65,21.23,21.15,21.07 } },
        {"H-L5", new double[]{ 20.70,0.00,21.15,21.21 } },
        {"H-L6", new double[]{ 0.00,0.00,21.50,21.35 } }
      };

      //冷房テスト********************************************
      Console.WriteLine("Cooling mode test");
      for (int i = 0; i < 4; i++) vrfSystem.SetIndoorUnitMode(i, VRFUnit.Mode.Cooling);
      vrfSystem.CurrentMode = VRFSystem.Mode.Cooling;
      //外気条件は固定
      vrfSystem.OutdoorAirDrybulbTemperature = 35.0;
      vrfSystem.OutdoorAirHumidityRatio = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(35.0, 24.0, 101.325); //論文ではWBTが29度
      double inletHmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(27.0, 19.0, 101.325); //湿度の記載は無い。JISに合わせて27C/19CWB相当とする

      foreach (string key in pLoadsC.Keys)
      {
        //室内機条件の設定
        double loadSum = 0;
        for (int i = 0; i < 4; i++)
        {
          loadSum += pLoadsC[key][i];
          //吸込空気状態
          vrfSystem.SetIndoorUnitInletAirState(i, itempsC[key][i], inletHmd);
          //給気温度
          iHexes[i].CurrentMode = pLoadsC[key][i] == 0 ? VRFUnit.Mode.ThermoOff : VRFUnit.Mode.Cooling;
          iHexes[i].SolveHeatLoad(-pLoadsC[key][i], iHexes[i].NominalAirFlowRate, itempsC[key][i], inletHmd, false);
          vrfSystem.SetIndoorUnitSetpointTemperature(i, iHexes[i].OutletAirTemperature);
          vrfSystem.SetIndoorUnitSetpointHumidityRatio(i, iHexes[i].OutletAirHumidityRatio);
        }
        vrfSystem.UpdateState();

        //書き出し
        double hTransfer = 0;
        for (int i = 0; i < 4; i++) hTransfer -= iHexes[i].HeatTransfer;
        Console.WriteLine(
          key + "," +
          loadSum.ToString("F2") + "," +
          hTransfer.ToString("F2") + "," +
          vrfSystem.PartialLoadRate.ToString("F3") + "," +
          (0.001 * vrfSystem.CompressorInletPressure).ToString("F3") + "," +
          (0.001 * vrfSystem.CompressorOutletPressure).ToString("F3") + "," +
          vrfSystem.CompressorElectricity.ToString("F2"));
      }

      Console.WriteLine();

      //暖房テスト********************************************
      Console.WriteLine("Heating mode test");
      for (int i = 0; i < 4; i++) vrfSystem.SetIndoorUnitMode(i, VRFUnit.Mode.Heating);
      vrfSystem.CurrentMode = VRFSystem.Mode.Heating;
      //外気条件は固定
      vrfSystem.OutdoorAirDrybulbTemperature = 7.0;
      vrfSystem.OutdoorAirHumidityRatio = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(7.0, 6.0, 101.325);
      inletHmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(20.0, 15.0, 101.325); //湿度の記載は無い。JISに合わせて20C/15CWB相当とする

      foreach (string key in pLoadsH.Keys)
      {
        //室内機条件の設定
        double loadSum = 0;
        for (int i = 0; i < 4; i++)
        {
          loadSum += pLoadsH[key][i];
          //吸込空気状態
          vrfSystem.SetIndoorUnitInletAirState(i, itempsH[key][i], inletHmd); //湿度の記載は無いので22C/40%相当とする
          //給気温度
          iHexes[i].SolveHeatLoad(pLoadsH[key][i], iHexes[i].NominalAirFlowRate, itempsH[key][i], inletHmd, false);
          vrfSystem.SetIndoorUnitSetpointTemperature(i, iHexes[i].OutletAirTemperature);
        }
        vrfSystem.UpdateState();

        //書き出し
        double hTransfer = 0;
        for (int i = 0; i < 4; i++) hTransfer += iHexes[i].HeatTransfer;
        Console.WriteLine(
          key + "," +
          loadSum.ToString("F2") + "," +
          hTransfer.ToString("F2") + "," +
          vrfSystem.PartialLoadRate.ToString("F3") + "," +
          (0.001 * vrfSystem.CompressorInletPressure).ToString("F3") + "," +
          (0.001 * vrfSystem.CompressorOutletPressure).ToString("F3") + "," +
          vrfSystem.CompressorElectricity.ToString("F2"));
      }

    }

    #endregion

    #region 20240723熱収支デバッグ

    public static void TestTotalHeatTransfer()
    {
      VRFSystem vrf_np = VRFInitializer.MakeOutdoorUnit(
        VRFInitializer.OutdoorUnitModel.Daikin_VRVX, //一旦ダイキン
        VRFInitializer.CoolingCapacity.C56_0, //室内機容量合算は52.0kW
        0, false);

      vrf_np.AddIndoorUnit([
       VRFInitializer.MakeIndoorUnit_Toshiba(VRFInitializer.IndoorUnitType.CeilingFourWay, VRFInitializer.CoolingCapacity.C4_5), //N1
        VRFInitializer.MakeIndoorUnit_Toshiba(VRFInitializer.IndoorUnitType.CeilingFourWay, VRFInitializer.CoolingCapacity.C4_5), //N2
        VRFInitializer.MakeIndoorUnit_Toshiba(VRFInitializer.IndoorUnitType.CeilingFourWay, VRFInitializer.CoolingCapacity.C4_5), //N3
        VRFInitializer.MakeIndoorUnit_Toshiba(VRFInitializer.IndoorUnitType.CeilingFourWay, VRFInitializer.CoolingCapacity.C7_1), //N4
        VRFInitializer.MakeIndoorUnit_Toshiba(VRFInitializer.IndoorUnitType.CeilingFourWay, VRFInitializer.CoolingCapacity.C4_5), //N5
        VRFInitializer.MakeIndoorUnit_Toshiba(VRFInitializer.IndoorUnitType.CeilingFourWay, VRFInitializer.CoolingCapacity.C4_5), //N6
        VRFInitializer.MakeIndoorUnit_Toshiba(VRFInitializer.IndoorUnitType.CeilingFourWay, VRFInitializer.CoolingCapacity.C5_6), //N7
        VRFInitializer.MakeIndoorUnit_Toshiba(VRFInitializer.IndoorUnitType.CeilingFourWay, VRFInitializer.CoolingCapacity.C5_6), //N8
        VRFInitializer.MakeIndoorUnit_Toshiba(VRFInitializer.IndoorUnitType.CeilingFourWay, VRFInitializer.CoolingCapacity.C5_6), //N17
        VRFInitializer.MakeIndoorUnit_Toshiba(VRFInitializer.IndoorUnitType.CeilingFourWay, VRFInitializer.CoolingCapacity.C5_6)  //N18
       ]);

      vrf_np.CurrentMode = VRFSystem.Mode.Cooling;
      vrf_np.OutdoorAirDrybulbTemperature = 29.6;
      vrf_np.OutdoorAirHumidityRatio = 0.00174;
      vrf_np.ControlThermoOffWithSensibleHeat = false;

      double[] sld = new double[] { -874.3597964, -788.7657147, -808.887289, -1182.142493, -787.1405106, -910.5012395, -918.3176973, -1086.100671, -945.4818226, -1102.739665 };
      double[] lld = new double[] { -622.2209916, -622.2209916, -622.2209916, -958.0965019, -622.2209916, -622.2209916, -778.5501462, -778.5501462, -778.5501462, -778.5501462 };
      for (int i = 0; i < sld.Length; i++)
      {
        vrf_np.SetIndoorUnitInletAirState(i, 26, 0.0105);
        VRFUnit iunit = (VRFUnit)vrf_np.IndoorUnits[i];
        iunit.SolveHeatLoad(0.001 * (sld[i] + lld[i]), iunit.NominalAirFlowRate, 26, 0.0105, false);
        vrf_np.SetIndoorUnitSetpointTemperature(i, iunit.OutletAirTemperature);
        vrf_np.SetIndoorUnitSetpointHumidityRatio(i, iunit.OutletAirHumidityRatio);
      }

      vrf_np.UpdateState();
      for (int i = 0; i < sld.Length; i++)
      {
        Console.WriteLine((1000 * vrf_np.IndoorUnits[i].HeatTransfer * ( 1 -vrf_np.IndoorUnits[i].ThermoOffRate)).ToString("F1"));
      }
    }

    #endregion

    #region 外気温度極低

    public static void TestWinterSeason()
    {
      //冷媒はR410a
      Refrigerant r410a = new Refrigerant(Refrigerant.Fluid.R410A);

      //4方向吹き出し11.2 kW
      VRFUnit iHex = VRFInitializer.MakeIndoorUnit_Toshiba(VRFInitializer.IndoorUnitType.CeilingFourWay, VRFInitializer.CoolingCapacity.C11_2);

      VRFSystem vrf = new VRFSystem(r410a,
        150 * 1.2 / 60d, 1.00, //冷房室外機条件 
        -22.4, 6.41, //定格冷房標準
        -10.1, 1.78, //中間冷房標準
        -10.2, 1.36, //中間冷房中温
        150 * 1.2 / 60d, 1.00, //暖房室外機条件
        25.0, 6.55, //定格暖房標準
        11.3, 1.91, //中間暖房標準
        7.5, 100, 0.88, 100, 1.00, iHex);

      VRFUnit[] iUnits = new VRFUnit[3];
      iUnits[0] = VRFInitializer.MakeIndoorUnit_Toshiba(VRFInitializer.IndoorUnitType.CeilingFourWay, VRFInitializer.CoolingCapacity.C11_2);
      iUnits[1] = VRFInitializer.MakeIndoorUnit_Toshiba(VRFInitializer.IndoorUnitType.CeilingFourWay, VRFInitializer.CoolingCapacity.C5_6);
      iUnits[2] = VRFInitializer.MakeIndoorUnit_Toshiba(VRFInitializer.IndoorUnitType.CeilingFourWay, VRFInitializer.CoolingCapacity.C5_6);
      vrf.AddIndoorUnit(iUnits);


      //運転条件
      vrf.CurrentMode = VRFSystem.Mode.Heating; 
      vrf.OutdoorAirDrybulbTemperature = -9;
      vrf.OutdoorAirHumidityRatio = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(-9, 80, 101.325);

      for (int i = 0; i < 3; i++)
      {
        vrf.SetIndoorUnitInletAirState(i, 24, 0.0065);
        vrf.SetIndoorUnitMode(i, VRFUnit.Mode.Heating);

        iUnits[i].SolveHeatLoad(iUnits[i].NominalHeatingCapacity * 0.5, iUnits[i].NominalAirFlowRate, 24, 0.0065, false);
        vrf.SetIndoorUnitSetpointTemperature(i, iUnits[i].OutletAirTemperature);
        vrf.SetIndoorUnitSetpointHumidityRatio(i, iUnits[i].OutletAirHumidityRatio);
      }
      vrf.UpdateState();

      Console.WriteLine(vrf.CompressorElectricity + ", " + vrf.GetHeatLoad());
    }

    #endregion

  }
}
