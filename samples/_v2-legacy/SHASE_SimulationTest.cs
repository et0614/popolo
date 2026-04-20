using System;
using System.IO;
using System.Text;

using System.Runtime.Serialization.Formatters.Binary;

using Popolo.ThermalLoad;
using Popolo.Weather;
using Popolo.HVAC.AirConditioner;
using Popolo.HVAC.SubSystem;
using Popolo.HVAC.HeatExchanger;
using Popolo.HVAC.Circuit;
using Popolo.HVAC.HeatSource;
using Popolo.ThermophysicalProperty;

using Popolo.HumanBody;
using Popolo.HVAC;

namespace PopoloTester
{
  /// <summary>SHASEシミュレーション評価法テストクラス</summary>
  public static class SHASE_SimulationTest
  {

    #region 定数宣言

    /// <summary>高断熱仕様か否か</summary>
    public static bool IS_HIGH_INSULATION = false;

    /// <summary>CO2制御実施の真偽</summary>
    public static bool USE_CO2CNTRL = false;

    /// <summary>外気冷房の真偽</summary>
    public static bool USE_OA_COOLING = false;

    /// <summary>全熱交換器導入の真偽</summary>
    public static bool USE_REGENERATOR = false;

    /// <summary>VWV制御導入の真偽</summary>
    public static bool USE_VWV_SYSTEM = false;

    /// <summary>高効率熱源導入の真偽</summary>
    public static bool IS_HIGHEFF_HSOURCE = false;

    /// <summary>クールビズ実施の真偽</summary>
    public static bool DO_COOLBIZ = false;

    #endregion

    #region SHASE委員会用定数宣言

    public static bool IS_CASE_JE210 = false;
    public static bool IS_CASE_JE211 = false;
    public static bool IS_CASE_JE212 = false;
    public static bool IS_CASE_JE240 = false;

    #endregion

    #region 熱負荷テスト関連

    /// <summary>建物モデルを作成する</summary>
    /// <returns>建物モデル</returns>
    public static BuildingThermalModel MakeBuildingThermalModel()
    {
      //傾斜面の作成（四方位）//////////////
      Incline incN = new Incline(Incline.Orientation.N, 0.5 * Math.PI);
      Incline incE = new Incline(Incline.Orientation.E, 0.5 * Math.PI);
      Incline incW = new Incline(Incline.Orientation.W, 0.5 * Math.PI);
      Incline incS = new Incline(Incline.Orientation.S, 0.5 * Math.PI);

      //壁構成を作成////////////////////////
      WallLayer[] exWL = new WallLayer[6];  //外壁一般部分
      exWL[0] = new WallLayer("タイル", 1.3, 2000, 0.010);
      exWL[1] = new WallLayer("セメント・モルタル", 1.5, 1600, 0.025);
      exWL[2] = new WallLayer("コンクリート", 1.6, 2000, 0.150);
      if(IS_HIGH_INSULATION) exWL[3] = new WallLayer("押出ポリスチレンフォーム3種", 0.028, 33, 0.025);
      else exWL[3] = new WallLayer("押出ポリスチレンフォーム1種", 0.040, 33, 0.025);
      exWL[4] = new AirGapLayer("非密閉中空層", false, 0.05);
      exWL[5] = new WallLayer("石膏ボード", 0.22, 830, 0.008);

      WallLayer[] exbmWL = new WallLayer[4];  //外壁梁部分
      exbmWL[0] = new WallLayer("タイル", 1.3, 2000, 0.010);
      exbmWL[1] = new WallLayer("セメント・モルタル", 1.5, 1600, 0.025);
      exbmWL[2] = new WallLayer("コンクリート", 1.6, 2000, 0.750);
      if (IS_HIGH_INSULATION) exbmWL[3] = new WallLayer("押出ポリスチレンフォーム3種", 0.028, 33, 0.025);
      else exbmWL[3] = new WallLayer("押出ポリスチレンフォーム1種", 0.040, 33, 0.025);

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
      Zone[] zn0 = new Zone[4];
      Zone[] zn1 = new Zone[3];
      Zone[] zn2 = new Zone[4];
      zn0[0] = new Zone("NI", 292.5 * 2.7 * 1.2);
      zn0[1] = new Zone("NWP", 57.5 * 2.7 * 1.2);
      zn0[2] = new Zone("NP", 187.5 * 2.7 * 1.2);
      zn0[3] = new Zone("NEP", 57.5 * 2.7 * 1.2);
      zn1[0] = new Zone("SI", 279.0 * 2.7 * 1.2);
      zn1[1] = new Zone("SWP", 57.5 * 2.7 * 1.2);
      zn1[2] = new Zone("SP", 167.5 * 2.7 * 1.2);
      zn2[0] = new Zone("EV", 12.0 * 2.6 * 1.2);
      zn2[1] = new Zone("IL", 144.0 * 2.4 * 1.2);
      zn2[2] = new Zone("MC", 21.8 * 2.4 * 1.2);
      zn2[3] = new Zone("WC", 69.2 * 2.4 * 1.2);

      //内部発熱を設定
      zn0[0].AddHeatGain(new MyHeatGain(0.1 * 292.5, 12 * 292.5, 12 * 292.5, true));
      zn0[1].AddHeatGain(new MyHeatGain(0.1 * 57.5, 12 * 57.5, 12 * 57.5, true));
      zn0[2].AddHeatGain(new MyHeatGain(0.1 * 187.5, 12 * 187.5, 12 * 187.5, true));
      zn0[3].AddHeatGain(new MyHeatGain(0.1 * 57.5, 12 * 57.5, 12 * 57.5, true));
      zn1[0].AddHeatGain(new MyHeatGain(0.1 * 279.0, 12 * 279.0, 12 * 279.0, true));
      zn1[1].AddHeatGain(new MyHeatGain(0.1 * 57.5, 12 * 57.5, 12 * 57.5, true));
      zn1[2].AddHeatGain(new MyHeatGain(0.1 * 167.5, 12 * 167.5, 12 * 167.5, true));
      zn2[0].AddHeatGain(new MyHeatGain(0, 15 * 12.0, 0, false));
      zn2[1].AddHeatGain(new MyHeatGain(0, 15 * 144.0, 0, false));
      zn2[2].AddHeatGain(new MyHeatGain(0, 15 * 21.8, 0, false));
      zn2[3].AddHeatGain(new MyHeatGain(0, 15 * 69.2, 0, false));

      ////隙間風と熱容量設定
      for (int i = 0; i < zn0.Length; i++)
      {
        zn0[i].HeatCapacity = zn0[i].AirMass * 1006 * 10;
        zn0[i].VentilationRate = zn0[i].AirMass / 3600d * 0.1;
        zn0[i].InitializeAirState(22, 0.0105);
      }
      for (int i = 0; i < zn1.Length; i++)
      {
        zn1[i].HeatCapacity = zn1[i].AirMass * 1006 * 10;
        zn1[i].VentilationRate = zn1[i].AirMass / 3600d * 0.1;
        zn1[i].InitializeAirState(22, 0.0105);
      }
      for (int i = 0; i < zn0.Length; i++)
      {
        zn2[i].HeatCapacity = 0;
        zn2[i].VentilationRate = zn2[i].AirMass / 3600d * 0.1;
        zn2[i].InitializeAirState(22, 0.0105);
      }

      //壁体の作成//////////////////////////
      Wall[] walls = new Wall[49];
      walls[0] = new Wall(32.8, exWL);
      walls[1] = new Wall(12.6, exbmWL);
      walls[2] = new Wall(94.5, exWL);
      walls[3] = new Wall(38.3, exbmWL);
      walls[4] = new Wall(32.8, exWL);
      walls[5] = new Wall(12.6, exbmWL);
      walls[6] = new Wall(32.8, exWL);
      walls[7] = new Wall(12.6, exbmWL);
      walls[8] = new Wall(79.6, exWL);
      walls[9] = new Wall(32.4, exbmWL);
      walls[10] = new Wall(16.8, exWL);
      walls[11] = new Wall(42.6, exWL);
      walls[12] = new Wall(26.0, exWL);
      walls[13] = new Wall(292.5, flWL);
      walls[14] = new Wall(292.5, flWL);
      walls[15] = new Wall(16.2, inWL);
      walls[16] = new Wall(8.1, inWL);
      walls[17] = new Wall(64.8, inWL);
      walls[18] = new Wall(57.5, flWL);
      walls[19] = new Wall(57.5, flWL);
      walls[20] = new Wall(13.5, inWL);
      walls[21] = new Wall(187.5, flWL);
      walls[22] = new Wall(187.5, flWL);
      walls[23] = new Wall(57.5, flWL);
      walls[24] = new Wall(57.5, flWL);
      walls[25] = new Wall(13.5, inWL);
      walls[26] = new Wall(79.7, inWL);
      walls[27] = new Wall(8.0, inWL);
      walls[28] = new Wall(28.4, inWL);
      walls[29] = new Wall(279.0, flWL);
      walls[30] = new Wall(279.0, flWL);
      walls[31] = new Wall(57.5, flWL);
      walls[32] = new Wall(57.5, flWL);
      walls[33] = new Wall(13.5, inWL);
      walls[34] = new Wall(167.5, flWL);
      walls[35] = new Wall(167.5, flWL);
      walls[36] = new Wall(13.5, inWL);
      walls[37] = new Wall(31.2, inWL);
      walls[38] = new Wall(12.0, flWL);
      walls[39] = new Wall(12.0, flWL);
      walls[40] = new Wall(10.8, inWL);
      walls[41] = new Wall(158.4, inWL);
      walls[42] = new Wall(144.0, flWL);
      walls[43] = new Wall(144.0, flWL);
      walls[44] = new Wall(16.9, inWL);
      walls[45] = new Wall(21.8, flWL);
      walls[46] = new Wall(21.8, flWL);
      walls[47] = new Wall(69.2, flWL);
      walls[48] = new Wall(69.2, flWL);

      //壁の初期化
      for (int i = 0; i < walls.Length; i++)
      {
        walls[i].ShortWaveAbsorptanceF = walls[i].ShortWaveAbsorptanceB = 0.8;
        walls[i].LongWaveEmissivityF = walls[i].LongWaveEmissivityB = 0.9;
        walls[i].RadiativeCoefficientF = walls[i].RadiativeCoefficientB = 5;
        if (i <= 12) walls[i].ConvectiveCoefficientF = 18;
        else walls[i].ConvectiveCoefficientF = 4;
        walls[i].ConvectiveCoefficientB = 4;
        walls[i].Initialize(20);
      }

      //窓を作成
      double[] TAU_WIN, RHO_WIN;
      if (IS_HIGH_INSULATION)
      {
        TAU_WIN = new double[] { 0.627, 0.815 }; //ガラスの透過率リスト[-]
        RHO_WIN = new double[] { 0.211, 0.072 }; //ガラスの反射率リスト[-]
      }
      else
      {
        TAU_WIN = new double[] { 0.815 }; //ガラスの透過率リスト[-]
        RHO_WIN = new double[] { 0.072 }; //ガラスの反射率リスト[-]
      }
      Window[][] win = new Window[3][];
      win[0] = new Window[3];
      win[1] = new Window[2];
      win[2] = new Window[0];
      win[0][0] = new Window(10.6, TAU_WIN, RHO_WIN, incW);
      win[0][1] = new Window(37.2, TAU_WIN, RHO_WIN, incN);
      win[0][2] = new Window(10.6, TAU_WIN, RHO_WIN, incE);
      win[1][0] = new Window(10.6, TAU_WIN, RHO_WIN, incW);
      win[1][1] = new Window(31.9, TAU_WIN, RHO_WIN, incS);
      for (int i = 0; i < win.Length; i++)
      {
        for (int j = 0; j < win[i].Length; j++)
        {
          VenetianBlind blind = new VenetianBlind(25, 22.5, 0, 0, 0.66, 0.66);
          blind.SlatAngle = 0;
          win[i][j].SetShadingDevice(1, blind);
          win[i][j].ConvectiveCoefficientF = 18;
          win[i][j].ConvectiveCoefficientB = 4;
          win[i][j].LongWaveEmissivityF = win[i][j].LongWaveEmissivityB = 0.9;
        }
      }

      //多数室の作成
      MultiRooms[] mRm = new MultiRooms[3];
      mRm[0] = new MultiRooms(1, zn0, walls, win[0]);
      mRm[1] = new MultiRooms(1, zn1, walls, win[1]);
      mRm[2] = new MultiRooms(4, zn2, walls, win[2]);

      //ゾーンを室に登録
      mRm[0].AddZone(0, 0);
      mRm[0].AddZone(0, 1);
      mRm[0].AddZone(0, 2);
      mRm[0].AddZone(0, 3);
      mRm[1].AddZone(0, 0);
      mRm[1].AddZone(0, 1);
      mRm[1].AddZone(0, 2);
      mRm[2].AddZone(0, 0);
      mRm[2].AddZone(1, 1);
      mRm[2].AddZone(2, 2);
      mRm[2].AddZone(3, 3);

      //外壁を登録
      mRm[0].AddWall(1, 0, false); mRm[0].SetOutsideWall(0, true, incW);
      mRm[0].AddWall(1, 1, false); mRm[0].SetOutsideWall(1, true, incW);
      mRm[0].AddWall(2, 2, false); mRm[0].SetOutsideWall(2, true, incN);
      mRm[0].AddWall(2, 3, false); mRm[0].SetOutsideWall(3, true, incN);
      mRm[0].AddWall(3, 4, false); mRm[0].SetOutsideWall(4, true, incE);
      mRm[0].AddWall(3, 5, false); mRm[0].SetOutsideWall(5, true, incE);
      mRm[1].AddWall(1, 6, false); mRm[1].SetOutsideWall(6, true, incW);
      mRm[1].AddWall(1, 7, false); mRm[1].SetOutsideWall(7, true, incW);
      mRm[1].AddWall(2, 8, false); mRm[1].SetOutsideWall(8, true, incS);
      mRm[1].AddWall(2, 9, false); mRm[1].SetOutsideWall(9, true, incS);
      mRm[2].AddWall(2, 10, false); mRm[2].SetOutsideWall(10, true, incE);
      mRm[2].AddWall(3, 11, false); mRm[2].SetOutsideWall(11, true, incE);
      mRm[2].AddWall(3, 12, false); mRm[2].SetOutsideWall(12, true, incS);

      //内壁を登録
      mRm[0].AddWall(0, 0, 13);
      mRm[0].AddWall(0, 0, 14);
      mRm[0].AddWall(0, 15, true); mRm[2].AddWall(1, 15, false);
      mRm[0].AddWall(0, 16, true); mRm[2].AddWall(0, 16, false);
      mRm[0].AddWall(0, 17, false); mRm[0].UseAdjacentSpaceFactor(17, true, 0.5);
      mRm[0].AddWall(1, 1, 18);
      mRm[0].AddWall(1, 1, 19);
      mRm[0].AddWall(1, 20, false); mRm[0].UseAdjacentSpaceFactor(20, true, 0.5);
      mRm[0].AddWall(2, 2, 21);
      mRm[0].AddWall(2, 2, 22);
      mRm[0].AddWall(3, 3, 23);
      mRm[0].AddWall(3, 3, 24);
      mRm[0].AddWall(3, 25, false); mRm[0].UseAdjacentSpaceFactor(25, true, 0.5);
      mRm[1].AddWall(0, 26, true); mRm[2].AddWall(1, 26, false);
      mRm[1].AddWall(0, 27, true); mRm[2].AddWall(2, 27, false);
      mRm[1].AddWall(0, 28, false); mRm[1].UseAdjacentSpaceFactor(28, true, 0.5);
      mRm[1].AddWall(0, 0, 29);
      mRm[1].AddWall(0, 0, 30);
      mRm[1].AddWall(1, 1, 31);
      mRm[1].AddWall(1, 1, 32);
      mRm[1].AddWall(1, 33, false); mRm[1].UseAdjacentSpaceFactor(33, true, 0.5);
      mRm[1].AddWall(2, 2, 34);
      mRm[1].AddWall(2, 2, 35);
      mRm[1].AddWall(2, 36, true); mRm[2].AddWall(3, 36, false);
      mRm[2].AddWall(0, 37, false); mRm[2].UseAdjacentSpaceFactor(37, true, 0.5);
      mRm[2].AddWall(0, 0, 38);
      mRm[2].AddWall(0, 0, 39);
      mRm[2].AddWall(1, 2, 40);
      mRm[2].AddWall(1, 41, false); mRm[2].UseAdjacentSpaceFactor(41, true, 0.5);
      mRm[2].AddWall(1, 1, 42);
      mRm[2].AddWall(1, 1, 43);
      mRm[2].AddWall(2, 44, true); mRm[2].AddWall(3, 44, false);
      mRm[2].AddWall(2, 2, 45);
      mRm[2].AddWall(2, 2, 46);
      mRm[2].AddWall(3, 3, 47);
      mRm[2].AddWall(3, 3, 48);

      //窓を登録
      mRm[0].AddWindow(1, 0);
      mRm[0].AddWindow(2, 1);
      mRm[0].AddWindow(3, 2);
      mRm[1].AddWindow(1, 0);
      mRm[1].AddWindow(2, 1);

      //ペリメータ床に短波長優先配分
      const double SW_RATE_TO_FLOOR = 0.7;
      mRm[0].SetSWDistributionRateToFloor(0, 18, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(1, 21, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(2, 23, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(0, 31, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(1, 34, true, SW_RATE_TO_FLOOR);

      //建物モデルの作成
      BuildingThermalModel bModel = new BuildingThermalModel(mRm);
      bModel.TimeStep = 3600;

      //ゾーン間換気の設定
      const double cvRate = 150d * 1.2 / 3600d;
      bModel.SetCrossVentilation(0, 0, 0, 1, 9.0 * cvRate);
      bModel.SetCrossVentilation(0, 0, 0, 2, 32.5 * cvRate);
      bModel.SetCrossVentilation(0, 0, 0, 3, 9.0 * cvRate);
      bModel.SetCrossVentilation(0, 1, 0, 2, 7.0 * cvRate);
      bModel.SetCrossVentilation(0, 2, 0, 3, 7.0 * cvRate);
      bModel.SetCrossVentilation(1, 0, 1, 1, 9.0 * cvRate);
      bModel.SetCrossVentilation(1, 0, 1, 2, 31.0 * cvRate);
      bModel.SetCrossVentilation(1, 1, 1, 2, 7.0 * cvRate);
      bModel.SetCrossVentilation(2, 0, 2, 1, 3.0 * cvRate);
      bModel.SetCrossVentilation(2, 1, 2, 2, 2.0 * cvRate);
      bModel.SetCrossVentilation(2, 2, 2, 3, 2.0 * cvRate);

      return bModel;
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

    public static void MakeWeatherData
      (uint seed, out double[] dbt, out double[] hrt, out double[] dnr, out double[] dhr, out double[] ncr)
    {
      double[] rad;
      bool[] fcf;
      dnr = new double[8760];
      dhr = new double[8760];
      ncr = new double[8760];
      RandomWeather wRnd = new RandomWeather(seed, RandomWeather.Location.Tokyo);
      wRnd.MakeWeather(1, out dbt, out hrt, out rad, out fcf);
      for (int i = 0; i < hrt.Length; i++) hrt[i] *= 0.001; //g/kg→kg/kgに換算

      DateTime dt = new DateTime(2014, 1, 1, 0, 0, 0);
      Sun sun = new Sun(Sun.City.Tokyo);
      for (int i = 0; i < 8760; i++)
      {
        sun.Update(dt);
        sun.SeparateGlobalHorizontalRadiation(rad[i], Sun.SeparationMethod.Udagawa);
        dnr[i] = sun.DirectNormalRadiation;
        dhr[i] = sun.DiffuseHorizontalRadiation;
        double vp = MoistAir.GetWaterVaporPartialPressureFromHumidityRatio(hrt[i], 101.325);
        ncr[i] = Sky.GetNocturnalRadiation(dbt[i], fcf[i] ? 0 : 5, vp);
        dt = dt.AddHours(1);
      }
    }

    public static void SetHVACControl(BuildingThermalModel bModel)
    {
      //温湿度判定
      double dbt, hrt;
      DateTime dt = bModel.CurrentDateTime;
      if (6 <= dt.Month && dt.Month < 10)
      {
        dbt = 26;
        hrt = 0.00930;
      }
      else if (dt.Month == 12 || dt.Month < 4)
      {
        dbt = 22;
        hrt = 0.00656;
      }
      else
      {
        dbt = 24;
        hrt = 0.01050;
      }

      for (int i = 0; i < bModel.MultiRoom.Length; i++)
      {
        for (int j = 0; j < bModel.MultiRoom[i].ZoneNumber; j++)
        {
          bool ffloat;
          if (IsHoliday(dt)) ffloat = true;
          else if (i == 2) ffloat = (dt.Hour < 7 || 21 <= dt.Hour) || (j == 4);
          else ffloat = (dt.Hour < 8 || 21 <= dt.Hour); 
          if (ffloat)
          {
            bModel.ControlHeatSupply(i, j, 0);
            bModel.ControlWaterSupply(i, j, 0);
          }
          else
          {
            bModel.ControlDrybulbTemperature(i, j, dbt);
            bModel.ControlHumidityRatio(i, j, hrt);
          }
        }
      }
    }

    /// <summary>土日祝日か否かを判定する</summary>
    /// <param name="dt">現在の日時</param>
    /// <returns>土日祝日の場合にtrue</returns>
    public static bool IsHoliday(DateTime dt)
    {
      if (dt.DayOfWeek == DayOfWeek.Saturday) return true;
      if (dt.DayOfWeek == DayOfWeek.Sunday) return true;
      if (dt.Month == 1 && dt.Day == 2) return true;
      if (dt.Month == 1 && dt.Day == 3) return true;
      if (dt.Month == 1 && dt.Day == 9) return true;
      if (dt.Month == 2 && dt.Day == 11) return true;
      if (dt.Month == 3 && dt.Day == 21) return true;
      if (dt.Month == 4 && dt.Day == 29) return true;
      if (dt.Month == 5 && dt.Day == 3) return true;
      if (dt.Month == 5 && dt.Day == 4) return true;
      if (dt.Month == 5 && dt.Day == 5) return true;
      if (dt.Month == 7 && dt.Day == 17) return true;
      if (dt.Month == 9 && dt.Day == 18) return true;
      if (dt.Month == 9 && dt.Day == 23) return true;
      if (dt.Month == 10 && dt.Day == 9) return true;
      if (dt.Month == 11 && dt.Day == 3) return true;
      if (dt.Month == 11 && dt.Day == 23) return true;
      if (dt.Month == 12 && dt.Day == 23) return true;
      if (dt.Month == 12 && dt.Day == 29) return true;
      return false;
    }

    #endregion

    #region 熱源テスト関連

    public static void MakeHeatSourceSystem(out HeatSourceSystemModel hss,
      out AirHeatSourceModularChillersSystem ahSystem, out DirectFiredAbsorptionChillerSystem arSystem)
    {
      CentrifugalPump.ControlMethod P1_CTRL, P2_CTRL;
      if(USE_VWV_SYSTEM) P1_CTRL = P2_CTRL = CentrifugalPump.ControlMethod.ConstantPressureWithInverter;
      else P1_CTRL = P2_CTRL = CentrifugalPump.ControlMethod.ConstantPressureWithBypass;

      //SHASE委員会対応**************************
      if (IS_CASE_JE240) P2_CTRL = CentrifugalPump.ControlMethod.MinimumPressure;
      //SHASE委員会対応ここまで******************

      //空気熱源ヒートポンプシステムの作成
      double mf = 430d / 60;  //冷温水質量流量[kg/s]
      double COP_C, COP_H;
      if (IS_HIGHEFF_HSOURCE)
      {
        COP_C = 4.6;
        COP_H = 3.5;
      }
      else COP_C = COP_H = 3.0;

      //SHASE委員会対応**************************
      if (IS_CASE_JE210 || IS_CASE_JE212)
      {
        COP_C /= 0.8;
        COP_H /= 0.8;
      }
      //SHASE委員会対応ここまで******************

      AirHeatSourceModularChillers ahpChiler = new AirHeatSourceModularChillers
        (150, 7, mf, 35, 850d / 60 * 1.2, 150 / COP_C, 150, 45, mf, 7, 850d / 60 * 1.2, 150 / COP_H, 2, 1.9);
      ahpChiler.MaximizeEfficiency = IS_HIGHEFF_HSOURCE;
      CentrifugalPump chwPump = new CentrifugalPump(150, 0.001 * mf, 140, 0.001 * mf, P1_CTRL, 1);
      CentrifugalPump hwPump = new CentrifugalPump(150, 0.001 * mf, 140, 0.001 * mf, P1_CTRL, 1);
      ahSystem = new AirHeatSourceModularChillersSystem(ahpChiler, chwPump, hwPump, 2);

      //吸収冷温水機システムの作成
      mf = 1512d / 60;  //冷温水質量流量[kg/s]
      DirectFiredAbsorptionChiller arChiller = new DirectFiredAbsorptionChiller
        (32.4d / 3600, 31.8d / 3600, 12, 7, 32, 37, 51.7, 55, mf, 2500d / 60, mf, 5.1, Boiler.Fuel.Gas13A);
      //SHASE委員会対応**************************
      if (IS_CASE_JE211 || IS_CASE_JE212)
      {
        arChiller = new DirectFiredAbsorptionChiller
          (25.92d / 3600, 25.44d / 3600, 12, 7, 32, 37, 51.7, 55, mf, 2500d / 60, mf, 5.1, Boiler.Fuel.Gas13A);
      }
      //SHASE委員会対応ここまで******************
      arChiller.HasSolutionInverterPump = IS_HIGHEFF_HSOURCE;
      CoolingTower cTower = new CoolingTower
        (37, 32, 27, 2693d / 60, 1783d / 60 * 1.2, CoolingTower.AirFlowDirection.CrossFlow, 7.5 / 0.92, false);
      chwPump = new CentrifugalPump(150, 0.001 * mf, 140, 0.001 * mf, P1_CTRL, 1);
      hwPump = new CentrifugalPump(150, 0.001 * mf, 140, 0.001 * mf, P1_CTRL, 1);
      CentrifugalPump cdwPump = new CentrifugalPump
        (250, 2.693 / 60, 240, 2.693 / 60, CentrifugalPump.ControlMethod.MinimumPressure, 50);
      arSystem = new DirectFiredAbsorptionChillerSystem(arChiller, chwPump, hwPump, cdwPump, cTower, 1, 1);
      arSystem.ControlCoolingWaterFlowRate = true;
      arSystem.ControlCoolingWaterTemperature = true;

      //熱源サブシステムの作成
      CentrifugalPump cp2 = new CentrifugalPump(250, 1.077 / 60, 240, 1.077 / 60, P2_CTRL, 1);
      PumpSystem cp2system = new PumpSystem(cp2, 240, 1.077 / 60 * 3, 50, 3); //BugFix,2017.01.08. E.Togashi
      CentrifugalPump hp2 = new CentrifugalPump(250, 1.077 / 60, 240, 1.077 / 60, P2_CTRL, 1);
      PumpSystem hp2system = new PumpSystem(hp2, 240, 1.077 / 60 * 3, 50, 3); //BugFix,2017.01.08. E.Togashi
      hss = new HeatSourceSystemModel(new IHeatSourceSubSystem[] { ahSystem, arSystem }, cp2system, hp2system);
      hss.ChilledWaterSupplyTemperatureSetpoint = 7;
      hss.HotWaterSupplyTemperatureSetpoint = 45;
      hss.SetChillingOperationSequence(0, 1);
      hss.SetChillingOperationSequence(1, 2);
      hss.SetHeatingOperationSequence(0, 1);
      hss.SetHeatingOperationSequence(1, 2);
    }

    public static void ControlHeatSourceSystem(HeatSourceSystemModel hss)
    {
      DateTime dTime = hss.CurrentDateTime;

      //冷房期間
      if (6 <= dTime.Month && dTime.Month <= 9)
      {
        hss.SetOperatingMode(0, HeatSourceSystemModel.OperatingMode.Cooling);
        hss.SetOperatingMode(1, HeatSourceSystemModel.OperatingMode.Cooling);
      }
      //暖房期間
      else if (12 <= dTime.Month || dTime.Month <= 3)
      {
        hss.SetOperatingMode(0, HeatSourceSystemModel.OperatingMode.Heating);
        hss.SetOperatingMode(1, HeatSourceSystemModel.OperatingMode.Heating);
      }
      //中間期
      else
      {
        hss.SetOperatingMode(0, HeatSourceSystemModel.OperatingMode.ShutOff);
        hss.SetOperatingMode(1, HeatSourceSystemModel.OperatingMode.ShutOff);
      }
    }

    #endregion

    #region 空調テスト関連

    public static void MakeAHUSystem(out BuildingThermalModel bModel, out AHUSystem ahuSystem, bool isCAVSystem)
    {
      //二次側システムモデル作成
      bModel = MakeBuildingThermalModel();
      AirHandlingUnit[] ahu = makeAHUs();
      ahuSystem = new AHUSystem(bModel, ahu);

      //CAV,VAVを作成
      const double MIN_RATE = 0.2;
      //NI系統
      AHUSystem.VolumeController[] vcNI = new AHUSystem.VolumeController[1];
      vcNI[0] = new AHUSystem.VolumeController
        (0, 0, 6212d / 3600 * 1.2, 5700d / 3600 * 1.2, 6212d / 3600 * 1.2 * MIN_RATE);
      //NP系統
      AHUSystem.VolumeController[] vcNP = new AHUSystem.VolumeController[3];
      vcNP[0] = new AHUSystem.VolumeController
        (0, 1, 1543d / 3600 * 1.2, 1433d / 3600 * 1.2, 1543d / 3600 * 1.2 * MIN_RATE);
      vcNP[1] = new AHUSystem.VolumeController
        (0, 2, 4151d / 3600 * 1.2, 3857d / 3600 * 1.2, 4151d / 3600 * 1.2 * MIN_RATE);
      vcNP[2] = new AHUSystem.VolumeController
        (0, 3, 1782d / 3600 * 1.2, 1656d / 3600 * 1.2, 1782d / 3600 * 1.2 * MIN_RATE);
      //SI系統
      AHUSystem.VolumeController[] vcSI = new AHUSystem.VolumeController[1];
      vcSI[0] = new AHUSystem.VolumeController
        (1, 0, 5730d / 3600 * 1.2, 5242d / 3600 * 1.2, 5730d / 3600 * 1.2 * MIN_RATE);
      //SP系統
      AHUSystem.VolumeController[] vcSP = new AHUSystem.VolumeController[2];
      vcSP[0] = new AHUSystem.VolumeController
        (1, 1, 1510d / 3600 * 1.2, 1393d / 3600 * 1.2, 1510d / 3600 * 1.2 * MIN_RATE);
      vcSP[1] = new AHUSystem.VolumeController
        (1, 2, 3596d / 3600 * 1.2, 3319d / 3600 * 1.2, 3596d / 3600 * 1.2 * MIN_RATE);

      if (isCAVSystem)
      {
        ahuSystem.SetCAV(0, vcNI);
        ahuSystem.SetCAV(1, vcNP);
        ahuSystem.SetCAV(2, vcSI);
        ahuSystem.SetCAV(3, vcSP);
      }
      else
      {
        ahuSystem.SetVAV(0, vcNI);
        ahuSystem.SetVAV(1, vcNP);
        ahuSystem.SetVAV(2, vcSI);
        ahuSystem.SetVAV(3, vcSP);
      }      
    }

    private static AirHandlingUnit[] makeAHUs()
    {
      AirHandlingUnit[] ahus = new AirHandlingUnit[4];
      CrossFinHeatExchanger cCoil, hCoil;
      CentrifugalFan saFan, raFan;
      RotaryRegenerator regenerator;
      double msa, mra, moa;

      double dPHEX = 0;
      if (USE_REGENERATOR) dPHEX = 0.2;

      //AHU-1Iを作成
      msa = 6212d / 3600 * 1.2;
      mra = 5700d / 3600 * 1.2;
      moa = 1463d / 3600 * 1.2;
      cCoil = new CrossFinHeatExchanger(0.82, 0.910, 6, 24, msa, 27.46, 0.01206, 95,
        111d / 60, 111d / 60, 7, CrossFinHeatExchanger.WaterFlowType.HalfFlow, 43.5, true);
      hCoil = new CrossFinHeatExchanger(0.82, 0.910, 4, 24, msa, 17.46, 0.00554, 95,
        93d / 60, 93d / 60, 50, CrossFinHeatExchanger.WaterFlowType.HalfFlow, 41.5, true);
      saFan = new CentrifugalFan(0.85 + dPHEX, msa / 1.2, 0.85 + dPHEX, msa / 1.2, 3, true);
      raFan = new CentrifugalFan(0.35 + dPHEX, mra / 1.2, 0.35 + dPHEX, mra / 1.2, 3, true);
      saFan.MinimumRotationRatio = 0.4;
      raFan.MinimumRotationRatio = 0.4;
      regenerator = new RotaryRegenerator
        (0.34, moa * 3600 / 1.2, (moa + mra - msa) * 3600 / 1.2, true, 34.4, 0.0194, 26, 0.0105);
      ahus[0] = new AirHandlingUnit
        (cCoil, hCoil, AirHandlingUnit.HumidifierType.DropPervaporation, saFan, raFan, regenerator);
      ahus[0].SetOutdoorAirFlowRange(moa, msa);
      ahus[0].SetAirFlowRate(mra, msa);
      
      //AHU-1Pを作成
      msa = 7476d / 3600 * 1.2;
      mra = 6946d / 3600 * 1.2;
      moa = 1513d / 3600 * 1.2;
      cCoil = new CrossFinHeatExchanger(0.82, 0.910, 6, 24, msa, 27.46, 0.01206, 95,
        127d / 60, 127d / 60, 7, CrossFinHeatExchanger.WaterFlowType.HalfFlow, 49.6, true);
      hCoil = new CrossFinHeatExchanger(0.82, 0.910, 4, 24, msa, 17.46, 0.00554, 95,
        105d / 60, 105d / 60, 50, CrossFinHeatExchanger.WaterFlowType.HalfFlow, 46.9, true);
      saFan = new CentrifugalFan(0.8 + dPHEX, msa / 1.2, 0.8 + dPHEX, msa / 1.2, 3, true);
      raFan = new CentrifugalFan(0.3 + dPHEX, mra / 1.2, 0.3 + dPHEX, mra / 1.2, 3, true);
      saFan.MinimumRotationRatio = 0.4;
      raFan.MinimumRotationRatio = 0.4;
      regenerator = new RotaryRegenerator
        (0.34, moa * 3600 / 1.2, (moa + mra - msa) * 3600 / 1.2, true, 34.4, 0.0194, 26, 0.0105);
      ahus[1] = new AirHandlingUnit
        (cCoil, hCoil, AirHandlingUnit.HumidifierType.DropPervaporation, saFan, raFan, regenerator);
      ahus[1].SetOutdoorAirFlowRange(moa, msa);
      ahus[1].SetAirFlowRate(mra, msa);

      //AHU-2Pを作成
      msa = 5730d / 3600 * 1.2;
      mra = 5242d / 3600 * 1.2;
      moa = 1395d / 3600 * 1.2;
      cCoil = new CrossFinHeatExchanger(0.82, 0.910, 6, 24, msa, 27.46, 0.01206, 95,
        104d / 60, 104d / 60, 7, CrossFinHeatExchanger.WaterFlowType.HalfFlow, 40.5, true);
      hCoil = new CrossFinHeatExchanger(0.82, 0.910, 4, 24, msa, 17.46, 0.00554, 95,
        86d / 60, 86d / 60, 50, CrossFinHeatExchanger.WaterFlowType.HalfFlow, 38.4, true);
      saFan = new CentrifugalFan(0.9 + dPHEX, msa / 1.2, 0.9 + dPHEX, msa / 1.2, 3, true);
      raFan = new CentrifugalFan(0.4 + dPHEX, mra / 1.2, 0.4 + dPHEX, mra / 1.2, 3, true);
      saFan.MinimumRotationRatio = 0.4;
      raFan.MinimumRotationRatio = 0.4;
      regenerator = new RotaryRegenerator
        (0.34, moa * 3600 / 1.2, (moa + mra - msa) * 3600 / 1.2, true, 34.4, 0.0194, 26, 0.0105);
      ahus[2] = new AirHandlingUnit
        (cCoil, hCoil, AirHandlingUnit.HumidifierType.DropPervaporation, saFan, raFan, regenerator);
      ahus[2].SetOutdoorAirFlowRange(moa, msa);
      ahus[2].SetAirFlowRate(mra, msa);

      //AHU-2Pを作成
      msa = 5106d / 3600 * 1.2;
      mra = 4712d / 3600 * 1.2;
      moa = 1125d / 3600 * 1.2;
      cCoil = new CrossFinHeatExchanger(0.82, 0.910, 6, 24, msa, 27.46, 0.01206, 95,
        88d / 60, 88d / 60, 7, CrossFinHeatExchanger.WaterFlowType.HalfFlow, 34.5, true);
      hCoil = new CrossFinHeatExchanger(0.82, 0.910, 4, 24, msa, 17.46, 0.00554, 95,
        93d / 60, 93d / 60, 50, CrossFinHeatExchanger.WaterFlowType.HalfFlow, 32.3, true);
      saFan = new CentrifugalFan(0.85 + dPHEX, msa / 1.2, 0.85 + dPHEX, msa / 1.2, 3, true);
      raFan = new CentrifugalFan(0.35 + dPHEX, mra / 1.2, 0.35 + dPHEX, mra / 1.2, 3, true);
      saFan.MinimumRotationRatio = 0.4;
      raFan.MinimumRotationRatio = 0.4;
      regenerator = new RotaryRegenerator
        (0.34, moa * 3600 / 1.2, (moa + mra - msa) * 3600 / 1.2, true, 34.4, 0.0194, 26, 0.0105);
      ahus[3] = new AirHandlingUnit
        (cCoil, hCoil, AirHandlingUnit.HumidifierType.DropPervaporation, saFan, raFan, regenerator);
      ahus[3].SetOutdoorAirFlowRange(moa, msa);
      ahus[3].SetAirFlowRate(mra, msa);

      //共通の設定
      for (int i = 0; i < ahus.Length; i++)
      {
        if(USE_OA_COOLING) ahus[i].OutdoorAirCooling = AirHandlingUnit.OutdoorAirCoolingControl.Enthalpy;
        else ahus[i].OutdoorAirCooling = AirHandlingUnit.OutdoorAirCoolingControl.None;
        ahus[i].BypassRegenerator = !USE_REGENERATOR;
        ahus[i].MinimizeAirFlow = true;
        ahus[i].UpperTemperatureLimit_H = 37d;
        ahus[i].LowerTemperatureLimit_H = 27d;
        ahus[i].UpperTemperatureLimit_C = 19d;
        ahus[i].LowerTemperatureLimit_C = 15d;
      }      
      return ahus;
    }

    public static void ControlAHUSystem(BuildingThermalModel bModel, AHUSystem ahuSystem)
    {
      //温湿度判定
      double dbt = 0;
      double hrt = 0;
      DateTime dt = bModel.CurrentDateTime;
      bool midSeason = false;
      if (6 <= dt.Month && dt.Month < 10)
      {
        if (DO_COOLBIZ) dbt = 28;
        else dbt = 26;
        hrt = 0.00930;
        for (int i = 0; i < 4; i++) ahuSystem.Controllers[i].Mode = AHUSystem.OperatingMode.Cooling;
      }
      else if (dt.Month == 12 || dt.Month < 4)
      {
        if (DO_COOLBIZ) dbt = 20;
        else dbt = 22;
        hrt = 0.00656;
        for (int i = 0; i < 4; i++)
        {
          ahuSystem.Controllers[i].Mode = AHUSystem.OperatingMode.Heating;
          ahuSystem.Controllers[i].MinimumHumidity = hrt;
        }
      }
      else midSeason = true;

      //事務室//CAV,VAVコントローラを制御
      if ((IsHoliday(dt)) || (dt.Hour < 7 || 21 <= dt.Hour) || midSeason)
        for (int i = 0; i < 4; i++) ahuSystem.Controllers[i].Mode = AHUSystem.OperatingMode.ShutOff;
      else
      {
        //外気カットウォーミングアップ設定
        if (dt.Hour == 7) for (int i = 0; i < 4; i++) ahuSystem.SetOutdoorAirFlow(i, 0, 0);
        else
        {
          ahuSystem.SetOutdoorAirFlow(0, 1463d / 3600 * 1.2, 1463d / 3600 * 1.2);
          ahuSystem.SetOutdoorAirFlow(1, 1513d / 3600 * 1.2, 1513d / 3600 * 1.2);
          ahuSystem.SetOutdoorAirFlow(2, 1395d / 3600 * 1.2, 1395d / 3600 * 1.2);
          ahuSystem.SetOutdoorAirFlow(3, 1125d / 3600 * 1.2, 1125d / 3600 * 1.2);
        }

        //ゾーン温度を設定(VAV用)
        ahuSystem.ControlZoneTemperature(0, 0, dbt);
        ahuSystem.ControlZoneTemperature(1, 0, dbt);
        ahuSystem.ControlZoneTemperature(1, 1, dbt);
        ahuSystem.ControlZoneTemperature(1, 2, dbt);
        ahuSystem.ControlZoneTemperature(2, 0, dbt);
        ahuSystem.ControlZoneTemperature(3, 0, dbt);
        ahuSystem.ControlZoneTemperature(3, 1, dbt);
        //(CAV用)
        for (int i = 0; i < ahuSystem.AHUs.Length; i++)
        {
          ahuSystem.Controllers[i].IsRATemperatureControl = true;
          ahuSystem.Controllers[i].SetpointTemperature = dbt;
        }
      }

      //廊下・便所は直接に熱負荷計算
      for (int i = 0; i < bModel.MultiRoom[2].ZoneNumber; i++)
      {
        bool ffloat = (IsHoliday(dt)) || (dt.Hour < 7 || 21 <= dt.Hour) || (i == 3) || midSeason;
        if (ffloat)
        {
          bModel.ControlHeatSupply(2, i, 0);
          bModel.ControlWaterSupply(2, i, 0);
        }
        else
        {
          bModel.ControlDrybulbTemperature(2, i, dbt);
          bModel.ControlHumidityRatio(2, i, hrt);
        }
      }

      //風の流れを設定
      if (!IsHoliday(dt) && (dt.Hour < 8 || 21 <= dt.Hour))
      {
        bModel.SetAirFlow(0, 0, 2, 1, 985d / 3600 * 1.2);
        bModel.SetAirFlow(1, 0, 2, 1, 940d / 3600 * 1.2);
        bModel.SetAirFlow(2, 1, 2, 2, 1925d / 3600 * 1.2);
        bModel.SetAirFlow(2, 2, 2, 3, 1925d / 3600 * 1.2);
      }
      else
      {
        const double cvRate = 150d * 1.2 / 3600d;
        bModel.SetAirFlow(2, 1, 2, 2, 0);
        bModel.SetAirFlow(2, 2, 2, 3, 0);
        bModel.SetCrossVentilation(2, 1, 2, 2, 2.0 * cvRate);
        bModel.SetCrossVentilation(2, 2, 2, 3, 2.0 * cvRate);
      }

      if (USE_CO2CNTRL) ApplyCO2Control(ahuSystem, bModel.CurrentDateTime);
    }

    public static void ApplyCO2Control(AHUSystem ahuSystem, DateTime dTime)
    {
      double oaRatio;
      if (IsHoliday(dTime) || (dTime.Hour < 8 || 21 <= dTime.Hour)) oaRatio = 0;
      else if (20 <= dTime.Hour && dTime.Hour < 21) oaRatio = 0.2;
      else if (19 <= dTime.Hour && dTime.Hour < 20) oaRatio = 0.3;
      else if (18 <= dTime.Hour && dTime.Hour < 19) oaRatio = 0.5;
      else if (12 <= dTime.Hour && dTime.Hour < 13) oaRatio = 0.6;
      else oaRatio = 1.0;

      AirHandlingUnit ahu;
      ahu = (AirHandlingUnit)ahuSystem.AHUs[0];
      ahu.SetOutdoorAirFlowRange(1463d / 3600 * 1.2 * oaRatio, ahu.MaxOAFlowRate);
      ahu = (AirHandlingUnit)ahuSystem.AHUs[1];
      ahu.SetOutdoorAirFlowRange(1513d / 3600 * 1.2 * oaRatio, ahu.MaxOAFlowRate);
      ahu = (AirHandlingUnit)ahuSystem.AHUs[2];
      ahu.SetOutdoorAirFlowRange(1395d / 3600 * 1.2 * oaRatio, ahu.MaxOAFlowRate);
      ahu = (AirHandlingUnit)ahuSystem.AHUs[3];
      ahu.SetOutdoorAirFlowRange(1125d / 3600 * 1.2 * oaRatio, ahu.MaxOAFlowRate);
    }

    #endregion

    #region 機器単体テスト関連

    public static void PumpTest()
    {
      double nomFlow = 1.077 / 60;

      //入力条件：流量と圧力
      CentrifugalPump pmp = new CentrifugalPump(245, nomFlow, 245, nomFlow, CentrifugalPump.ControlMethod.ConstantPressureWithInverter, 0);
      pmp.SetEfficiencyCoefficient(0, 121, -9635, 392029, -6416169);
      pmp.SetPressureCoefficient(299.08, 651.25, -208472);

      double[] pressure = new double[] { 245.1, 268.6, 284.3, 299, 138.2, 152, 159.8, 168.6, 61.8, 67.6, 71.6, 74.5 };
      double[] fRate = new double[] { 1.00, 0.75, 0.50, 0.25, 0.75, 0.5625, 0.375, 0.1875, 0.50, 0.375, 0.25, 0.125 };
      double[] minR = new double[] { 1.00, 1.00, 1.00, 1.00, 0.75, 0.75, 0.75, 0.75, 0.50, 0.50, 0.50, 0.50 };

      for (int i = 0; i < pressure.Length; i++)
      {
        pmp.MinimumRotationRatio = minR[i];
        pmp.PressureSetpoint = pressure[i];
        pmp.UpdateState(fRate[i] * nomFlow);
        Console.WriteLine(
          (100 * ((pmp.VolumetricFlowRate - pmp.BypassFlowRate) / nomFlow)).ToString("F0") + "," +
          pmp.Pressure.ToString("F1") + "," +
          (100 * pmp.RotationRatio).ToString("F0") + "," + 
          (100 * pmp.GetFluidMachineryEfficiency()).ToString("F1") + "," +
          (pmp.GetElectricConsumption() * pmp.MotorEfficiency * pmp.GetInverterEfficiency()).ToString("F2") + "," +
          pmp.GetElectricConsumption().ToString("F2") + "," + 
          (100 * pmp.MotorEfficiency).ToString("F0") + "," +
          (100 * pmp.GetInverterEfficiency()).ToString("F0")
          );
      }

    }


    #endregion

    #region 内部発熱要素クラス

    /// <summary>内部発熱クラス</summary>
    [Serializable]
    public class MyHeatGain : IHeatGain
    {

      /// <summary>事務室か否か</summary>
      private bool isOffice;

      /// <summary>最大人数[人]を取得する</summary>
      public double MaxOccupancy { get; private set; }

      /// <summary>最大照明発熱[W]を取得する</summary>
      public double MaxLightingLoad { get; private set; }

      /// <summary>最大機器発熱[W]を取得する</summary>
      public double MaxPlugLoad { get; private set; }

      /// <summary>人数[人]を取得する</summary>
      public double Occupancy { get; private set; }

      /// <summary>照明発熱[W]を取得する</summary>
      public double LightingLoad { get; private set; }

      /// <summary>機器発熱[W]を取得する</summary>
      public double PlugLoad { get; private set; }

      /// <summary>現在時刻</summary>
      private DateTime currentDT;

      /// <summary>初期化する</summary>
      /// <param name="maxOccupancy">最大人数[人]</param>
      /// <param name="maxLightingLoad">最大照明発熱[W]</param>
      /// <param name="maxPlugLoad">最大機器発熱[W]</param>
      /// <param name="isOffice">事務室か否か</param>
      public MyHeatGain(double maxOccupancy, double maxLightingLoad, double maxPlugLoad, bool isOffice)
      {
        MaxOccupancy = maxOccupancy;
        MaxLightingLoad = maxLightingLoad;
        MaxPlugLoad = maxPlugLoad;
        this.isOffice = isOffice;
      }

      /// <summary>顕熱取得[W]の内、対流成分を取得する</summary>
      /// <param name="zone">発熱要素が属するゾーン</param>
      /// <returns>顕熱取得の対流成分[kW]</returns>
      public double GetConvectiveHeatGain(ImmutableZone zone)
      {
        updateLoad(zone.MultiRoom.CurrentDateTime);
        return (LightingLoad + PlugLoad + Occupancy * (229 - 6.2 * zone.Temperature)) * 0.4;
      }

      /// <summary>顕熱取得[W]の内、放射成分を取得する</summary>
      /// <param name="zone">発熱要素が属するゾーン</param>
      /// <returns>顕熱取得の放射成分[kW]</returns>
      public double GetRadiativeHeatGain(ImmutableZone zone)
      {
        updateLoad(zone.MultiRoom.CurrentDateTime);
        return (LightingLoad + PlugLoad + Occupancy * (229 - 6.2 * zone.Temperature)) * 0.6;
      }

      /// <summary>発生水分[kg/s]を取得する</summary>
      /// <param name="zone">発熱要素が属するゾーン</param>
      /// <returns>発生水分[kg/s]</returns>
      public double GetWaterGain(ImmutableZone zone)
      {
        updateLoad(zone.MultiRoom.CurrentDateTime);
        return Occupancy * (6.2 * zone.Temperature - 108) / 2500000d;
      }

      /// <summary>負荷を更新する</summary>
      /// <param name="dt">現在の日時</param>
      private void updateLoad(DateTime dt)
      {
        if (currentDT == dt) return;
        else currentDT = dt;

        double rLt, rOcc, rPlg;
        if (isOffice) //事務室の場合
        {
          if (IsHoliday(dt) || (dt.Hour < 8 || 21 <= dt.Hour))
          {
            rOcc = 0;
            rLt = 0;
            rPlg = 0.25;
          }
          else if (20 <= dt.Hour && dt.Hour < 21)
          {
            rOcc = 0.2;
            rLt = 0.8;
            rPlg = 0.5;
          }
          else if (19 <= dt.Hour && dt.Hour < 20)
          {
            rOcc = 0.3;
            rLt = 1.0;
            rPlg = 0.5;
          }
          else if (18 <= dt.Hour && dt.Hour < 19)
          {
            rOcc = 0.5;
            rLt = 1.0;
            rPlg = 1.0;
          }
          else if (12 <= dt.Hour && dt.Hour < 13)
          {
            rOcc = 0.6;
            rLt = 0.5;
            rPlg = 0.8;
          }
          else
          {
            rOcc = 1.0;
            rLt = 1.0;
            rPlg = 1.0;
          }
        }
        else //廊下などの場合
        {
          rOcc = 0;
          rLt = 0;
          rPlg = 0.0;
          if (!IsHoliday(dt) && 8 <= dt.Hour && dt.Hour < 21)
          {
            rOcc = 1.0;
            rLt = 1.0;
          }
        }
        LightingLoad = MaxLightingLoad * rLt;
        PlugLoad = MaxPlugLoad * rPlg;
        Occupancy = MaxOccupancy * rOcc;
      }
    }

    #endregion

    #region SHASEシミュレーション評価法テスト

    public static void SHASE_HeatLoadTest()
    {
      //気象データ読み込み
      double[] dbt, hrt, dnr, dhr, ncr;
      SHASE_SimulationTest.LoadWeatherData("3639999.has", out dbt, out hrt, out dnr, out dhr, out ncr);

      //建物モデル作成
      BuildingThermalModel building = SHASE_SimulationTest.MakeBuildingThermalModel();
      Sun sun = new Sun(Sun.City.Tokyo);

      using (StreamWriter sWriter = new StreamWriter("output.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        //タイトル行書き出し
        sWriter.Write(",,外気乾球温度,外気絶対湿度,法線面直達日射,水平面全天日射,夜間放射");
        for (int j = 0; j < building.MultiRoom.Length; j++)
        {
          ImmutableMultiRooms mRM = building.MultiRoom[j];
          for (int k = 0; k < mRM.ZoneNumber; k++)
          {
            string nm = mRM.Zones[k].Name;
            sWriter.Write(", " + nm + ":室温," + nm + ":絶対湿度," + nm + ":顕熱負荷," + nm + ":潜熱負荷");
          }
        }
        sWriter.WriteLine();

        //12月を助走計算期間とする
        DateTime dt = new DateTime(2006, 12, 1, 0, 0, 0);
        for (int i = 0; i < 31 * 24; i++)
        {
          //気象条件更新
          sun.SetGlobalHorizontalRadiation(dhr[8016 + i], dnr[8016 + i]);
          sun.Update(dt.AddMinutes(30)); //過去一時間のデータのため30分シフト
          building.UpdateOutdoorCondition(dt, sun, dbt[8016 + i], hrt[8016 + i], ncr[8016 + i]);

          //熱平衡を更新
          SHASE_SimulationTest.SetHVACControl(building);
          building.ForecastHeatTransfer();
          building.ForecastWaterTransfer();
          building.FixState();
          dt = dt.AddHours(1);
        }

        //8760時間の計算実行
        dt = new DateTime(2006, 1, 1, 0, 0, 0);
        for (int i = 0; i < 8760; i++)
        {
          //気象条件更新
          sun.SetGlobalHorizontalRadiation(dhr[i], dnr[i]);
          sun.Update(dt.AddMinutes(30)); //過去一時間のデータのため30分シフト
          building.UpdateOutdoorCondition(dt, sun, dbt[i], hrt[i], ncr[i]);

          //熱平衡を更新
          SHASE_SimulationTest.SetHVACControl(building);
          building.ForecastHeatTransfer();
          building.ForecastWaterTransfer();
          building.FixState();

          //書き出し
          sWriter.Write(dt.ToShortDateString() + "," + dt.ToShortTimeString());
          sWriter.Write("," + dbt[i] + "," + hrt[i] + "," + dnr[i] + "," + dhr[i] + "," + ncr[i]);
          for (int j = 0; j < building.MultiRoom.Length; j++)
          {
            ImmutableMultiRooms mRM = building.MultiRoom[j];
            for (int k = 0; k < mRM.ZoneNumber; k++)
            {
              ImmutableZone zn = mRM.Zones[k];
              sWriter.Write
                (", " + zn.Temperature + ", " + zn.HumidityRatio + ", " + zn.HeatSupply + ", " + zn.WaterSupply);
            }
          }
          sWriter.WriteLine();

          if (dt.Hour == 0) Console.WriteLine(dt.ToShortDateString());
          dt = dt.AddHours(1);
        }
      }
    }

    public static void SHASE_AHUTest(bool isCAVSystem)
    {
      //気象データ読み込み
      double[] dbt, hrt, dnr, dhr, ncr;
      SHASE_SimulationTest.LoadWeatherData("3639999.has", out dbt, out hrt, out dnr, out dhr, out ncr);

      //建物モデル作成
      BuildingThermalModel bModel;
      AHUSystem ahuSystem;
      SHASE_SimulationTest.MakeAHUSystem(out bModel, out ahuSystem, isCAVSystem);
      Sun sun = new Sun(Sun.City.Tokyo);

      using (StreamWriter sWriter = new StreamWriter("output.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        //タイトル行書き出し
        sWriter.Write(",,外気乾球温度,外気絶対湿度,法線面直達日射,水平面全天日射,夜間放射");
        for (int j = 0; j < bModel.MultiRoom.Length; j++)
        {
          for (int k = 0; k < bModel.MultiRoom[j].ZoneNumber; k++)
          {
            ImmutableZone zn = bModel.MultiRoom[j].Zones[k];
            sWriter.Write(
              ", " + zn.Name + ":室温" + ", " + zn.Name + ":絶対湿度" +
              ", " + zn.Name + ":平均放射温度" + ", " + zn.Name + ":PMV");
          }
        }
        ImmutableAirHandlingUnit[] ahus = ahuSystem.AHUs;
        for (int j = 0; j < ahus.Length; j++) sWriter.Write(",AHU" + j + ":冷熱処理");
        for (int j = 0; j < ahus.Length; j++) sWriter.Write(",AHU" + j + ":温熱処理");
        for (int j = 0; j < ahus.Length; j++) sWriter.Write(",AHU" + j + ":加湿量");
        for (int j = 0; j < ahus.Length; j++) sWriter.Write(",AHU" + j + ":給気ファン動力");
        for (int j = 0; j < ahus.Length; j++) sWriter.Write(",AHU" + j + ":還気ファン動力");
        sWriter.WriteLine(",冷水流量,冷水還温度,冷熱負荷,温水流量,温水還温度,温熱負荷");

        //12月を助走計算期間とする
        DateTime dt = new DateTime(2006, 12, 1, 0, 0, 0);
        for (int i = 0; i < 31 * 24; i++)
        {
          //気象条件更新
          sun.SetGlobalHorizontalRadiation(dhr[8016 + i], dnr[8016 + i]);
          sun.Update(dt.AddMinutes(30)); //過去一時間のデータのため30分シフト
          bModel.UpdateOutdoorCondition(dt, sun, dbt[8016 + i], hrt[8016 + i], ncr[8016 + i]);

          //熱平衡を更新
          SHASE_SimulationTest.ControlAHUSystem(bModel, ahuSystem);
          ahuSystem.ForecastReturnWaterTemperature(7, 50);
          bModel.FixState();
          dt = dt.AddHours(1);
        }

        //8760時間の計算実行
        dt = new DateTime(2006, 1, 1, 0, 0, 0);
        for (int i = 0; i < 8760; i++)
        {
          //気象条件更新
          sun.SetGlobalHorizontalRadiation(dhr[i], dnr[i]);
          sun.Update(dt.AddMinutes(30)); //過去一時間のデータのため30分シフト
          bModel.UpdateOutdoorCondition(dt, sun, dbt[i], hrt[i], ncr[i]);
          ahuSystem.OutdoorAir = new MoistAir(dbt[i], hrt[i]);

          //空調制御//熱平衡を確定
          SHASE_SimulationTest.ControlAHUSystem(bModel, ahuSystem);
          ahuSystem.ForecastReturnWaterTemperature(7, 50);
          bModel.FixState();

          //書き出し
          sWriter.Write(dt.ToShortDateString() + "," + dt.ToShortTimeString());
          sWriter.Write("," + dbt[i] + "," + hrt[i] + "," + dnr[i] + "," + dhr[i] + "," + ncr[i]);
          //着量を設定
          double clo = 0.7; //中間期
          if (SHASE_SimulationTest.DO_COOLBIZ)
          {
            if (6 <= dt.Month && dt.Month <= 9) clo = 0.5;  //夏季着衣量
            else if (dt.Month == 12 || dt.Month <= 3) clo = 0.92;  //冬季着衣量
          }
          for (int j = 0; j < bModel.MultiRoom.Length; j++)
          {
            for (int k = 0; k < bModel.MultiRoom[j].ZoneNumber; k++)
            {
              //PMV計算
              ImmutableZone zn = bModel.MultiRoom[j].Zones[k];
              double tRad = zn.GetMeanSurfaceTemperature();
              double rHmd = MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio
                (zn.Temperature, zn.HumidityRatio, 101.325);
              double pmv = ThermalComfort.GetPMV(zn.Temperature, tRad, rHmd, 0.2, clo, 1.2, 0); //オフィス作業相当
              if (6 <= dt.Month && dt.Month <= 9 && SHASE_SimulationTest.DO_COOLBIZ)
                pmv = ThermalComfort.GetPMV(zn.Temperature, tRad, rHmd, 0.55, clo, 1.2, 0); //オフィス作業相当//CLBZ
              sWriter.Write(", " + zn.Temperature + ", " + zn.HumidityRatio + ", " + tRad + ", " + pmv);
            }
          }

          //冷水温水流量・還温度・負荷を計算
          for (int j = 0; j < ahus.Length; j++) sWriter.Write("," + ahus[j].CoolingCoil.HeatTransfer);
          for (int j = 0; j < ahus.Length; j++) sWriter.Write("," + ahus[j].HeatingCoil.HeatTransfer);
          for (int j = 0; j < ahus.Length; j++) sWriter.Write("," + ahus[j].WaterConsumption);
          for (int j = 0; j < ahus.Length; j++) sWriter.Write("," + ahus[j].SupplyAirFan.GetElectricConsumption());
          for (int j = 0; j < ahus.Length; j++) sWriter.Write("," + ahus[j].ReturnAirFan.GetElectricConsumption());
          double tco, tho, mcc, mhc, qc, qh;
          tco = tho = mcc = mhc = qc = qh = 0;
          for (int j = 0; j < ahus.Length; j++)
          {
            mcc += ahus[j].CoolingCoil.WaterFlowRate;
            mhc += ahus[j].HeatingCoil.WaterFlowRate;
            tco += ahus[j].CoolingCoil.WaterFlowRate * ahus[j].CoolingCoil.OutletWaterTemperature;
            tho += ahus[j].HeatingCoil.WaterFlowRate * ahus[j].HeatingCoil.OutletWaterTemperature;
            qc += ahus[j].CoolingCoil.HeatTransfer;
            qh += ahus[j].HeatingCoil.HeatTransfer;
          }
          if (mcc == 0) tco = 7;
          else tco /= mcc;
          if (mhc == 0) tho = 50;
          else tho /= mhc;
          sWriter.WriteLine("," + mcc + "," + tco + "," + qc + "," + mhc + "," + tho + "," + qh);

          if (dt.Hour == 0) Console.WriteLine(dt.ToShortDateString());
          dt = dt.AddHours(1);
        }
      }
    }

    public static void SHASE_HVACSystemTest()
    {
      SHASE_SimulationTest.IS_HIGH_INSULATION = false;  //高断熱仕様フラグ
      SHASE_SimulationTest.USE_CO2CNTRL = false;        //CO2制御フラグ
      SHASE_SimulationTest.USE_OA_COOLING = false;      //外気冷房フラグ
      SHASE_SimulationTest.USE_REGENERATOR = false;     //全熱交換器フラグ
      SHASE_SimulationTest.USE_VWV_SYSTEM = false;      //VWV制御フラグ
      SHASE_SimulationTest.IS_HIGHEFF_HSOURCE = false;  //高効率熱源フラグ
      SHASE_SimulationTest.DO_COOLBIZ = false;          //クールビズ実施フラグ
      SHASE_HVACSystemTest("outputA.csv");

      SHASE_SimulationTest.IS_HIGH_INSULATION = true;
      SHASE_HVACSystemTest("outputB.csv");

      SHASE_SimulationTest.USE_CO2CNTRL = true;
      SHASE_HVACSystemTest("outputC.csv");

      SHASE_SimulationTest.USE_OA_COOLING = true;
      SHASE_HVACSystemTest("outputD.csv");

      SHASE_SimulationTest.USE_REGENERATOR = true;
      SHASE_HVACSystemTest("outputE.csv");

      SHASE_SimulationTest.USE_VWV_SYSTEM = true;
      SHASE_HVACSystemTest("outputF.csv");

      SHASE_SimulationTest.IS_HIGHEFF_HSOURCE = true;
      SHASE_HVACSystemTest("outputG.csv");

      SHASE_SimulationTest.DO_COOLBIZ = true;
      SHASE_HVACSystemTest("outputH.csv");
    }

    public static void SHASE_HVACSystemTest(string outputFile)
    {
      //建物モデル作成
      BuildingThermalModel bModel;
      AHUSystem ahuSystem;
      SHASE_SimulationTest.MakeAHUSystem(out bModel, out ahuSystem, false); //ここでVAV切り替え
      Sun sun = new Sun(Sun.City.Tokyo);

      //熱源モデル作成
      HeatSourceSystemModel hss;
      AirHeatSourceModularChillersSystem ahSystem;
      DirectFiredAbsorptionChillerSystem arSystem;
      SHASE_SimulationTest.MakeHeatSourceSystem(out hss, out ahSystem, out arSystem);

      //HVACモデル作成
      HVACSystemModel hvacSystem = new HVACSystemModel
        (bModel, new IAirConditioningSystemModel[] { ahuSystem }, hss);
      hvacSystem.ChilledWaterSupplyTemperatureSetpoint = 7; //CASE JE220-222変更箇所
      hvacSystem.HotWaterSupplyTemperatureSetpoint = 50;    //CASE JE220-222変更箇所
      hvacSystem.SetACFactor(0, 6); //2F-7F

      /*//シリアライズテスト//不要の場合にはコメントアウト////////////////////////////////////////
      FileStream fs = new FileStream("data.bin", FileMode.Create, FileAccess.Write);
      BinaryFormatter bf = new BinaryFormatter();
      bf.Serialize(fs, hvacSystem);
      fs.Close();

      fs = new FileStream("data.bin", FileMode.Open, FileAccess.Read);
      BinaryFormatter f = new BinaryFormatter();
      hvacSystem = (HVACSystemModel)f.Deserialize(fs);
      fs.Close();

      bModel = (BuildingThermalModel)hvacSystem.BuildingThermalModel;
      ahuSystem = (AHUSystem)hvacSystem.AirConditioningSystemModel[0];
      hss = (HeatSourceSystemModel)hvacSystem.HeatSourceSystemModel;
      //////////////////////////////////////////////////////////////////////////////////////////*/

      //気象データ読み込み
      double[] dbt, hrt, dnr, dhr, ncr;
      SHASE_SimulationTest.LoadWeatherData("3639999.has", out dbt, out hrt, out dnr, out dhr, out ncr);
      using (StreamWriter sWriter = new StreamWriter(outputFile, false, Encoding.GetEncoding("Shift_JIS")))
      {
        //タイトル行書き出し
        //外界条件
        sWriter.Write(",,外気乾球温度,外気絶対湿度,法線面直達日射,水平面全天日射,夜間放射");
        //一次側
        sWriter.Write(", 冷水往温度, 冷水還温度, 温水往温度, 温水還温度");
        sWriter.Write(", AHP電力, AHP冷水ポンプ電力, AHP温水ポンプ電力, AHP冷却, AHP加熱, AHP台数");
        sWriter.Write(", ARガス, AR電力, AR冷水ポンプ電力, AR温水ポンプ電力");
        sWriter.Write(", AR冷却水ポンプ電力, AR冷却塔電力, AR冷却, AR加熱");
        sWriter.Write(", 冷水二次ポンプ電力, 温水二次ポンプ電力");
        sWriter.Write(", 二次側冷水流量, 二次側温水流量, 冷水バイパス流量, 温水バイパス流量");
        //二次側
        for (int j = 0; j < bModel.MultiRoom.Length; j++)
        {
          for (int k = 0; k < bModel.MultiRoom[j].ZoneNumber; k++)
          {
            string nm = bModel.MultiRoom[j].Zones[k].Name;
            sWriter.Write(", " + nm + ":室温, " + nm + ":絶対湿度, " + nm + ":平均放射温度," + nm + ":PMV");
          }
        }
        ImmutableAirHandlingUnit[] ahus = ahuSystem.AHUs;
        for (int j = 0; j < ahus.Length; j++)
        {
          string sb = ",AHU" + j;
          sWriter.Write(sb + ":冷却" + sb + ":加熱" + sb + ":加湿" + sb + ":SAファン" + sb + ":RAファン");
        }
        sWriter.WriteLine();

        //12月を助走計算期間とする
        DateTime dt = new DateTime(2006, 12, 1, 0, 0, 0);
        for (int i = 0; i < 31 * 24; i++)
        {
          //気象条件更新
          sun.SetGlobalHorizontalRadiation(dhr[8016 + i], dnr[8016 + i]);
          sun.Update(dt.AddMinutes(30)); //過去一時間のデータのため30分シフト
          hvacSystem.UpdateOutdoorCondition(dt, sun, dbt[8016 + i], hrt[8016 + i], ncr[8016 + i]);

          //熱平衡を更新
          SHASE_SimulationTest.ControlAHUSystem(bModel, ahuSystem);
          SHASE_SimulationTest.ControlHeatSourceSystem(hss);
          hvacSystem.Update();
          dt = dt.AddHours(1);
        }

        //8760時間の計算実行
        dt = new DateTime(2006, 1, 1, 0, 0, 0);
        for (int i = 0; i < 8760; i++)
        {
          //気象条件更新
          sun.SetGlobalHorizontalRadiation(dhr[i], dnr[i]);
          sun.Update(dt.AddMinutes(30)); //過去一時間のデータのため30分シフト
          hvacSystem.UpdateOutdoorCondition(dt, sun, dbt[i], hrt[i], ncr[i]);

          //状態更新
          SHASE_SimulationTest.ControlAHUSystem(bModel, ahuSystem);
          SHASE_SimulationTest.ControlHeatSourceSystem(hss);
          hvacSystem.Update();

          //書き出し
          //外界条件
          sWriter.Write(dt.ToShortDateString() + "," + dt.ToShortTimeString());
          sWriter.Write("," + dbt[i] + "," + hrt[i] + "," + dnr[i] + "," + dhr[i] + "," + ncr[i]);
          //一次側
          sWriter.Write(
            "," + hvacSystem.ChilledWaterSupplyTemperature +
            "," + hvacSystem.ChilledWaterReturnTemperature +
            "," + hvacSystem.HotWaterSupplyTemperature +
            "," + hvacSystem.HotWaterReturnTemperature);
          ImmutableAirHeatSourceModularChillers ahp = ahSystem.AirHeatSourceModularChillers;
          sWriter.Write(
            "," + (ahp.ElectricConsumption * ahp.OperatingNumber) +
            "," + (ahSystem.ChilledWaterPump.GetElectricConsumption() +
            "," + ahSystem.HotWaterPump.GetElectricConsumption()) +
            "," + ahp.CoolingLoad + "," + ahp.HeatingLoad + "," + ahSystem.OperatingChillerNumber);
          ImmutableDirectFiredAbsorptionChiller ar = arSystem.DirectFiredAbsorptionChiller;
          sWriter.Write(
            "," + ar.FuelConsumption + "," + ar.ElectricConsumption +
            "," + (arSystem.ChilledWaterPump.GetElectricConsumption() +
            "," + arSystem.HotWaterPump.GetElectricConsumption()) +
            "," + arSystem.CoolingWaterPump.GetElectricConsumption() +
            "," + arSystem.CoolingTower.ElectricConsumption +
            "," + ar.CoolingLoad + "," + ar.HeatingLoad);
          ImmutablePumpSystem ps2c = hss.ChilledWaterPumpSystem;
          ImmutablePumpSystem ps2h = hss.HotWaterPumpSystem;
          sWriter.Write(
            "," + ps2c.GetElectricConsumption() + "," + ps2h.GetElectricConsumption() +
            "," + (ps2c.TotalFlowRate * 1000) + "," + (ps2h.TotalFlowRate * 1000) +
            "," + hss.ChilledWaterBypassFlowRate + "," + hss.HotWaterBypassFlowRate);
          //二次側
          double clo = 0.7; //中間期着衣量
          if (SHASE_SimulationTest.DO_COOLBIZ)
          {
            if (6 <= dt.Month && dt.Month <= 9) clo = 0.5;  //夏季着衣量
            else if (dt.Month == 12 || dt.Month <= 3) clo = 0.92;  //冬季着衣量
          }
          for (int j = 0; j < bModel.MultiRoom.Length; j++)
          {
            for (int k = 0; k < bModel.MultiRoom[j].ZoneNumber; k++)
            {
              ImmutableZone zn = bModel.MultiRoom[j].Zones[k];
              double tRad = zn.GetMeanSurfaceTemperature();
              double rHmd = MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio
                (zn.Temperature, zn.HumidityRatio, 101.325);
              double pmv = ThermalComfort.GetPMV(zn.Temperature, tRad, rHmd, 0.2, clo, 1.2, 0);
              if (6 <= dt.Month && dt.Month <= 9 && SHASE_SimulationTest.DO_COOLBIZ)
                pmv = ThermalComfort.GetPMV(zn.Temperature, tRad, rHmd, 0.55, clo, 1.2, 0); //採涼アイテム
              sWriter.Write(", " + zn.Temperature + ", " + zn.HumidityRatio + ", " + tRad + ", " + pmv);
            }
          }
          for (int j = 0; j < ahus.Length; j++)
            sWriter.Write(
              "," + ahus[j].CoolingCoil.HeatTransfer + "," + (-ahus[j].HeatingCoil.HeatTransfer) +
              "," + ahus[j].WaterConsumption + "," + ahus[j].SupplyAirFan.GetElectricConsumption() +
              "," + ahus[j].ReturnAirFan.GetElectricConsumption());

          //SHASE委員会_小野さん対応******************************************************************************
          bool isCooling = (6 <= dt.Month && dt.Month <= 9);
          double[] sh = new double[4];
          double[] lh = new double[4];
          ImmutableCrossFinHeatExchanger[] coil = new ImmutableCrossFinHeatExchanger[4];
          for (int j = 0; j < 4; j++)
          {
            if (isCooling) coil[j] = ahus[j].CoolingCoil;
            else coil[j] = ahus[j].HeatingCoil;
            sh[j] = (coil[j].InletAirTemperature - ahus[j].SATemperature)
              * MoistAir.GetSpecificHeat(coil[j].InletAirHumidityRatio) * ahus[j].SupplyAirFan.VolumetricFlowRate * 1.2;
            lh[j] = (coil[j].InletAirHumidityRatio - ahus[j].SAHumidityRatio)
              * MoistAir.LatentHeatOfVaporization * ahus[j].SupplyAirFan.VolumetricFlowRate * 1.2;
          }
          sWriter.Write(
            "," + sh[1] + "," + lh[1] + "," + ahus[1].SupplyAirFan.VolumetricFlowRate * 3600 +
            "," + (ahus[1].RAFlowRate - ahus[1].EAFlowRate) / 1.2 * 3600 + "," + ahus[1].OAFlowRate / 1.2 * 3600 +
            "," + ahus[1].OATemperature + "," + ahus[1].RATemperature + "," + coil[1].InletAirTemperature + "," + coil[1].OutletAirTemperature + "," + ahus[1].SATemperature +
            "," + ahus[1].OAHumidityRatio + "," + ahus[1].RAHumidityRatio + "," + coil[1].InletAirHumidityRatio + "," + coil[1].OutletAirHumidityRatio + "," + ahus[1].SAHumidityRatio);
          sWriter.Write("," + sh[0] + "," + sh[1] + "," + sh[2] + "," + sh[3] + "," + lh[0] + "," + lh[1] + "," + lh[2] + "," + lh[3]);

          //sWriter.Write("," + ps2c.Pump.VolumetricFlowRate * 1000 + "," + ps2c.Pump.Pressure + "," + ps2c.Pump.RotationRatio + "," + ps2c.Pump.GetElectricConsumption());
          //sWriter.Write("," + ps2h.Pump.VolumetricFlowRate * 1000 + "," + ps2h.Pump.Pressure + "," + ps2h.Pump.RotationRatio + "," + ps2h.Pump.GetElectricConsumption());
          //SHASE委員会_小野さん対応******************************************************************************
          sWriter.WriteLine();

          if (dt.Hour == 0) Console.WriteLine(dt.ToShortDateString());
          dt = dt.AddHours(1);
        }
      }
    }

    #endregion

  }

}
