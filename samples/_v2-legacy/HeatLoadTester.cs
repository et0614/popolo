using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;

using Popolo.Weather;
using Popolo.ThermalLoad;
using Popolo.ThermophysicalProperty;

namespace PopoloTester
{
  class HeatLoadTester
  {

    #region 壁体熱流テスト

    /// <summary>壁体熱流テスト</summary>
    private static void wallTest1()
    {
      WallLayer[] layers = new WallLayer[6];
      layers[0] = new WallLayer("フローリング", 0.120, 520, 0.012);
      layers[1] = new WallLayer("合板", 0.160, 720, 0.009);
      layers[2] = new WallLayer("ポリスチレンフォーム", 0.035, 80, 0.020);
      layers[3] = new WallLayer("合板", 0.160, 720, 0.009);
      layers[4] = new AirGapLayer("非密閉空気層", false, 0.05);
      layers[5] = new WallLayer("合板", 0.160, 720, 0.009);
      Wall wall = new Wall(5, layers);

      wall.TimeStep = 600;
      wall.Initialize(16);
      wall.SolAirTemperatureF = 30;
      wall.SolAirTemperatureB = 16;
      wall.ConvectiveCoefficientF = wall.ConvectiveCoefficientB = 4.8;
      wall.RadiativeCoefficientF = wall.RadiativeCoefficientB = 4.5;

      for (int i = 0; i <= 24; i++)
      {
        Console.Write((i * wall.TimeStep).ToString("F0"));
        for (int j = 0; j < wall.Temperatures.Length; j++)
          Console.Write("," + wall.Temperatures[j].ToString("F2"));
        Console.WriteLine();

        /*//シリアライズテスト//不要の場合にはコメントアウト////////////////////////////////////////
        FileStream fs = new FileStream("data.bin", FileMode.Create, FileAccess.Write);
        BinaryFormatter bf = new BinaryFormatter();
        bf.Serialize(fs, wall);
        fs.Close();

        fs = new FileStream("data.bin", FileMode.Open, FileAccess.Read);
        BinaryFormatter f = new BinaryFormatter();
        wall = (Wall)f.Deserialize(fs);
        fs.Close();
        //////////////////////////////////////////////////////////////////////////////////////////*/

        wall.Update();
      }
    }

    /// <summary>壁体熱流テスト:潜熱蓄熱材</summary>
    private static void wallTest2()
    {
      WallLayer[] layers = new WallLayer[6];
      layers[0] = new WallLayer("フローリング", 0.120, 520, 0.012);
      layers[1] = new PCMWallLayer("PCM", 19, 23, 0.02,
        new WallLayer("固体", 0.190, 5000, 0.02),
        new WallLayer("平衡", 0.205, 21000, 0.02),
        new WallLayer("液体", 0.220, 3000, 0.02));
      layers[2] = new WallLayer("ポリスチレンフォーム", 0.035, 80, 0.020);
      layers[3] = new WallLayer("合板", 0.160, 720, 0.009);
      layers[4] = new AirGapLayer("非密閉空気層", false, 0.05);
      layers[5] = new WallLayer("合板", 0.160, 720, 0.009);
      Wall wall = new Wall(5, layers);

      wall.TimeStep = 600;
      wall.Initialize(16);
      wall.ConvectiveCoefficientF = wall.ConvectiveCoefficientB = 4.8;
      wall.RadiativeCoefficientF = wall.RadiativeCoefficientB = 4.5;

      wall.SolAirTemperatureF = 30;
      wall.SolAirTemperatureB = 16;

      for (int i = 0; i <= 144; i++)
      {
        Console.Write((i * wall.TimeStep).ToString("F0"));
        for (int j = 0; j < wall.Temperatures.Length; j++)
          Console.Write("," + wall.Temperatures[j].ToString("F2"));
        Console.WriteLine();
        wall.Update();
      }
    }

    /// <summary>壁体熱流テスト:床冷暖房</summary>
    private static void wallTest3()
    {
      WallLayer[] layers = new WallLayer[6];
      layers[0] = new WallLayer("フローリング", 0.120, 520, 0.012);
      layers[1] = new WallLayer("合板", 0.160, 720, 0.009);
      layers[2] = new WallLayer("ポリスチレンフォーム", 0.035, 80, 0.020);
      layers[3] = new WallLayer("合板", 0.160, 720, 0.009);
      layers[4] = new AirGapLayer("非密閉空気層", false, 0.05);
      layers[5] = new WallLayer("合板", 0.160, 720, 0.009);
      Wall wall = new Wall(5, layers);

      wall.TimeStep = 600;
      wall.Initialize(16);
      wall.ConvectiveCoefficientF = wall.ConvectiveCoefficientB = 4.8;
      wall.RadiativeCoefficientF = wall.RadiativeCoefficientB = 4.5;

      wall.SolAirTemperatureF = 30;
      wall.SolAirTemperatureB = 16;

      wall.AddPipe(1, 0.05, 200, 40, 0.004, 0.0046, 0.47);
      wall.SetInletWater(1, 0.014, 20);

      for (int i = 0; i <= 24; i++)
      {
        Console.Write((i * wall.TimeStep).ToString("F0"));
        for (int j = 0; j < wall.Temperatures.Length; j++)
          Console.Write("," + wall.Temperatures[j].ToString("F2"));
        Console.WriteLine("," + wall.GetHeatTransferFromPipe(1).ToString("F2")
          + "," + wall.GetOutletWaterTemperature(1).ToString("F2"));
        wall.Update();
      }
    }

    /// <summary>壁体熱流テスト:熱水分同時移動</summary>
    /// <remarks>松本衛:博士論文pp.8-10より</remarks>
    private static void wallTest4()
    {
      WallLayer[] layers = new WallLayer[3];
      for (int i = 0; i < layers.Length; i++)
        layers[i] = new WallLayer
          ("木繊維板", 0.1116, 585, 0.000004694, 0.788, 3080, 1.715, 0.006);

      Wall wallA = new Wall(1, layers, true);
      Wall wallB = new Wall(1, layers, false);

      wallA.TimeStep = wallB.TimeStep = 60;
      wallA.Initialize(20, 0.008);
      wallB.Initialize(20);
      wallA.ConvectiveCoefficientF = wallB.ConvectiveCoefficientF = 4.8;
      wallA.ConvectiveCoefficientB = wallB.ConvectiveCoefficientB = 0;
      wallA.RadiativeCoefficientF = wallB.RadiativeCoefficientF =
        wallA.RadiativeCoefficientB = wallB.RadiativeCoefficientB = 0.0;

      wallA.SolAirTemperatureF = wallB.SolAirTemperatureF = 21;
      wallA.SolAirTemperatureB = wallB.SolAirTemperatureB = 20;
      wallA.HumidityRatioF = wallB.HumidityRatioF = 0.008;
      wallA.HumidityRatioB = wallB.HumidityRatioB = 0.008;
      for (int i = 0; i < 240; i++)
      {
        Console.WriteLine((i * 60) + ", " +
          wallA.Temperatures[0].ToString("F3") + ", " +
          wallB.Temperatures[0].ToString("F3") + ", " +
          wallA.Humidities[0].ToString("F6"));
        wallA.Update();
        wallB.Update();
      }
    }

    /// <summary>床熱流テスト:床・天井チャンバ</summary>
    private static void floorTest1()
    {
      WallLayer[] layers = new WallLayer[6];
      layers[0] = new WallLayer("フローリング", 0.120, 520, 0.012);
      layers[1] = new WallLayer("合板", 0.160, 720, 0.009);
      layers[2] = new WallLayer("ポリスチレンフォーム", 0.035, 80, 0.020);
      layers[3] = new WallLayer("合板", 0.160, 720, 0.009);
      layers[4] = new HorizontalAirChamber("床下チャンバ", 0.05);
      layers[5] = new WallLayer("合板", 0.160, 720, 0.009);
      Wall wall = new Wall(5, layers);

      HorizontalAirChamber chmb = wall.Layers[4] as HorizontalAirChamber;

      wall.TimeStep = 600;
      wall.Initialize(16);
      wall.SolAirTemperatureF = 16;
      wall.SolAirTemperatureB = 40;
      wall.ConvectiveCoefficientF = wall.ConvectiveCoefficientB = 4.8;
      wall.RadiativeCoefficientF = wall.RadiativeCoefficientB = 4.5;

      for (int i = 0; i <= 24; i++)
      {
        Console.Write((i * wall.TimeStep).ToString("F0"));
        Console.Write(", " +
          wall.Temperatures[4].ToString("F2") + ", " +
          wall.Temperatures[5].ToString("F2") + ", " +
          chmb.ConvectiveHeatTransferCoefficient.ToString("F3") + ", " +
          chmb.RadiativeHeatTransferCoefficient.ToString("F3") + ", " +
          chmb.HeatConductance.ToString("F3"));
        Console.WriteLine();
        wall.Update();

        wall.SolAirTemperatureB--;  //床下温度を下げていく
      }
    }

    #endregion

    #region 窓テスト

    private static void windowTest()
    {
      Incline inc = new Incline(0.0, 0.5 * Math.PI);
      Window[] win = new Window[4];
      for (int i = 0; i < win.Length; i++)
      {
        win[i] = new Window(1,
          new double[] { 0.396, 0.859 }, new double[] { 0.355, 0.077 },
          new double[] { 0.396, 0.859 }, new double[] { 0.427, 0.077 }, inc);
        for (int j = 0; j < win[i].GlazingNumber; j++)
        {
          if (j != win[i].GlazingNumber - 1)
            win[i].SetAirGapResistance(j, 1 / 8d);
          win[i].SetGlassResistance(j, 0.006 / 1d);
        }
        win[i].ConvectiveCoefficientF = 18.5;
        win[i].RadiativeCoefficientF = 4.5;
        win[i].ConvectiveCoefficientB = 4.5;
        win[i].RadiativeCoefficientB = 4.5;
      }

      VenetianBlind[] blinds = new VenetianBlind[3];
      for (int i = 0; i < blinds.Length; i++)
      {
        blinds[i] = new VenetianBlind(25, 22.5, 0, 0, 0.7, 0.7);
        blinds[i].SlatAngle = 30 / 180d * Math.PI;
        blinds[i].Pulldowned = true;
      }
      win[1].SetShadingDevice(2, blinds[0]);
      win[2].SetShadingDevice(1, blinds[1]);
      win[3].SetShadingDevice(0, blinds[2]);

      Sun sun = new Sun(Sun.City.Tokyo);
      DateTime dTime = new DateTime(2001, 7, 20, 7, 0, 0);
      using (StreamWriter sWriter = new StreamWriter
        ("WindowTest.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        sWriter.WriteLine
          ("時刻,無透,無反,無吸,内透,内反,内吸,中透,中反,中吸,外透,外反,外吸");

        while (true)
        {
          if (dTime.Hour == 16) break;

          sWriter.Write(dTime.ToShortTimeString());
          sun.Update(dTime);
          for (int i = 0; i < win.Length; i++)
          {
            win[i].UpdateOpticalProperties(sun);
            sWriter.Write(
              "," + win[i].DirectSolarIncidentTransmittance.ToString("F3") +
              "," + win[i].DirectSolarIncidentReflectance.ToString("F3") +
              "," + win[i].DirectSolarIncidentAbsorptance.ToString("F3"));
          }
          sWriter.WriteLine();
          dTime = dTime.AddMinutes(5);
        }
      }
    }

    #endregion

    #region 日射遮蔽テスト

    private static void sunShadeTest()
    {
      Incline incS = new Incline(Incline.Orientation.S, 0.5 * Math.PI);
      Incline incW = new Incline(Incline.Orientation.W, 0.5 * Math.PI);
      SunShade[] ss = new SunShade[6];
      ss[0] = SunShade.MakeHorizontalSunShade(3.8, 2.7, 0.6, 0.1, 0.1, 0.1, incS);
      ss[1] = SunShade.MakeVerticalSunShade(3.8, 2.7, 0.6, 0.1, 0.1, 0.1, 0.1, incS);
      ss[2] = SunShade.MakeGridSunShade(3.8, 2.7, 0.6, 0.1, 0.1, 0.1, 0.1, incS);
      ss[3] = SunShade.MakeHorizontalSunShade(3.8, 2.7, 0.6, 0.1, 0.1, 0.1, incW);
      ss[4] = SunShade.MakeVerticalSunShade(3.8, 2.7, 0.6, 0.1, 0.1, 0.1, 0.1, incW);
      ss[5] = SunShade.MakeGridSunShade(3.8, 2.7, 0.6, 0.1, 0.1, 0.1, 0.1, incW);

      DateTime dTime = new DateTime(2015, 7, 20, 7, 0, 0);
      Sun sun = new Sun(Sun.City.Tokyo);
      while (true)
      {
        if (dTime.Hour == 19) break;
        Console.Write(dTime.ToShortTimeString());
        sun.Update(dTime);

        for (int i = 0; i < ss.Length; i++) Console.Write(", " + ss[i].GetShadowRate(sun).ToString("F3"));
        Console.WriteLine();
        dTime = dTime.AddMinutes(5);
      }
    }

    private static void venetianBlindTest1()
    {
      VenetianBlind[] blinds = new VenetianBlind[5];
      double[] sltRho = new double[] { 0.1, 0.3, 0.5, 0.7, 0.9 };
      for (int i = 0; i < blinds.Length; i++)
      {
        blinds[i] = new VenetianBlind(25, 22.5, 0, 0, sltRho[i], 0.5);
        blinds[i].ProfileAngle = 30 / 180d * Math.PI;
        blinds[i].Pulldowned = true;
      }

      using (StreamWriter sWriter = new StreamWriter
        ("VenetianBlindTest.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        sWriter.Write("SlatAngle");
        for (int i = 0; i < 5; i++) sWriter.Write(", " + sltRho[i] + ":DirTau");
        for (int i = 0; i < 5; i++) sWriter.Write(", " + sltRho[i] + ":DirRho");
        for (int i = 0; i < 5; i++) sWriter.Write(", " + sltRho[i] + ":DifTau");
        for (int i = 0; i < 5; i++) sWriter.Write(", " + sltRho[i] + ":DifRho");
        sWriter.WriteLine();

        double[] drTau = new double[blinds.Length];
        double[] drRho = new double[blinds.Length];
        double[] dfTau = new double[blinds.Length];
        double[] dfRho = new double[blinds.Length];
        for (int i = -90; i <= 90; i += 5)
        {
          sWriter.Write(i);
          for (int j = 0; j < blinds.Length; j++)
          {
            blinds[j].SlatAngle = i / 180d * Math.PI;
            blinds[j].ComputeOpticalProperties(false, true, out drTau[j], out drRho[j]);
            blinds[j].ComputeOpticalProperties(true, true, out dfTau[j], out dfRho[j]);
          }

          for (int j = 0; j < 5; j++) sWriter.Write(", " + drTau[j]);
          for (int j = 0; j < 5; j++) sWriter.Write(", " + drRho[j]);
          for (int j = 0; j < 5; j++) sWriter.Write(", " + dfTau[j]);
          for (int j = 0; j < 5; j++) sWriter.Write(", " + dfRho[j]);
          sWriter.WriteLine();
        }
      }
    }

    private static void venetianBlindTest2()
    {
      VenetianBlind blind = new VenetianBlind(25, 22.5, 0.0, 0.0, 0.7, 0.7);
      blind.ProfileAngle = 30 / 180d * Math.PI;
      blind.Pulldowned = true;

      using (StreamWriter sWriter = new StreamWriter
        ("VenetianBlindTest2.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        sWriter.WriteLine("SlatAngle, upperTrans, lowerTrans, upperDirTrans, lowerDirTrans, directTrans");

        for (int i = -90; i <= 90; i += 5)
        {
          blind.SlatAngle = i / 180d * Math.PI;
          double upDifT, lwDifT, upDrT, lwDirT, drT;
          double trs, rfc;
          blind.ComputeOpticalProperties(true, true, out trs, out rfc);
          blind.ComputeOpticalProperties(false, true, out trs, out rfc);
          blind.ComputeOpticalProperties(out upDifT, out lwDifT, out upDrT, out lwDirT, out drT);
          sWriter.WriteLine(i + "," + upDifT + "," + lwDifT + "," + upDrT + "," + lwDirT + "," + drT);
        }
      }
    }

    #endregion

    #region 熱負荷計算テスト

    /// <summary>周期定常熱負荷計算テスト</summary>
    public static void periodicUnsteadyStateTest()
    {
      double CAP_CL = -2000;  //冷房能力
      double CAP_HT = 2000;   //暖房能力
      double[] DBTSET = new double[] { 26, 22 };          //設定温度
      double[] HRTSET = new double[] { 0.0105, 0.0066 };  //設定湿度

      //気象データ
      double[][] dbt = new double[2][];
      double[][] hrt = new double[2][];
      double[][] rad = new double[2][];
      dbt[0] = new double[] { 24.9, 24.7, 23.8, 24.2, 24.2, 25, 25, 24.4, 24.1, 23.7, 24.6, 25, 25.3, 25.2, 24.9, 24.9, 25.3, 25.9, 25.8, 25.1, 24.2, 23.5, 23.6, 23.5 };
      dbt[1] = new double[] { 4.1, 4.2, 4.6, 5, 4.4, 3.8, 4.8, 4.4, 4.9, 6.5, 7.7, 8.1, 9, 10.1, 10.7, 10.2, 10.1, 9.8, 8.6, 7.9, 7.6, 7.1, 5.4, 4.1 };
      hrt[0] = new double[] { 12.9, 12.8, 13.9, 14.6, 14.7, 14.6, 13.5, 13.7, 14.8, 15.4, 16.1, 15.5, 15.6, 15.6, 16, 16.5, 16.6, 16.4, 16.8, 16, 16, 16.1, 16.2, 16.6 };
      hrt[1] = new double[] { 4.2, 4, 4, 3.6, 4, 3.7, 3.9, 3.9, 3.5, 3.2, 3.4, 3.5, 3.3, 3.2, 3, 3.1, 3.1, 2.8, 2.8, 3, 3.5, 3.1, 3, 3.1 };
      rad[0] = new double[] { 0, 0, 0, 0, 0, 0, 93, 288, 465, 629, 781, 860, 870, 827, 725, 598, 403, 217, 49, 0, 0, 0, 0, 0 };
      rad[1] = new double[] { 0, 0, 0, 0, 0, 0, 0, 1, 146, 318, 438, 506, 532, 467, 391, 232, 83, 0, 0, 0, 0, 0, 0, 0 };

      //建物データを作成
      BuildingThermalModel bMdl = makeSunSpaceBuildingModel();

      //収束判定配列
      double[][][] temp = new double[2][][];
      double[][][] load = new double[2][][];
      for (int i = 0; i < temp.Length; i++)
      {
        temp[i] = new double[2][];
        load[i] = new double[2][];
        for (int j = 0; j < temp[i].Length; j++)
        {
          temp[i][j] = new double[24];
          load[i][j] = new double[24];
        }
      }

      Sun sun = new Sun(Sun.City.Tokyo);
      DateTime dTime;
      for (int ssn = 0; ssn < 2; ssn++) //ssn=0で夏季、ssn=1で冬季の計算
      {
        if (ssn == 0) dTime = new DateTime(2001, 7, 20, 0, 0, 0);
        else dTime = new DateTime(2001, 1, 20, 0, 0, 0);
        double err = 0;
        while (true)
        {
          int hr = dTime.Hour;
          //日射は過去1時間の積算データのため、太陽位置を30分ずらす
          sun.Update(dTime.AddMinutes(30));
          sun.SeparateGlobalHorizontalRadiation(rad[ssn][hr], Sun.SeparationMethod.Erbs);
          bMdl.UpdateOutdoorCondition(dTime, sun, dbt[ssn][hr], hrt[ssn][hr], 0);

          if (8 < dTime.Hour && dTime.Hour < 18)
          {
            //空調時間帯
            bMdl.ControlDrybulbTemperature(0, 0, DBTSET[ssn]);
            bMdl.ControlDrybulbTemperature(0, 1, DBTSET[ssn]);
            bMdl.ControlHumidityRatio(0, 0, HRTSET[ssn]);
            bMdl.ControlHumidityRatio(0, 1, HRTSET[ssn]);
          }
          else
          {
            //非空調時間帯（自然室温）
            bMdl.ControlHeatSupply(0, 0, 0);
            bMdl.ControlHeatSupply(0, 1, 0);
            bMdl.ControlWaterSupply(0, 0, 0);
            bMdl.ControlWaterSupply(0, 1, 0);
          }
          //熱平衡予測
          bMdl.ForecastHeatTransfer();
          bMdl.ForecastWaterTransfer();
          //過負荷の場合には最大容量で成り行き計算
          for (int i = 0; i < 2; i++)
          {
            double hs = bMdl.MultiRoom[0].Zones[i].HeatSupply;
            if (hs < CAP_CL) bMdl.ControlHeatSupply(0, i, CAP_CL);
            else if (CAP_HT < hs) bMdl.ControlHeatSupply(0, i, CAP_HT);
          }
          //熱平衡を確定
          bMdl.ForecastHeatTransfer();
          bMdl.ForecastWaterTransfer();
          bMdl.FixState();

          //収束誤差
          ImmutableZone[] zn = bMdl.MultiRoom[0].Zones;
          for (int i = 0; i < zn.Length; i++)
          {
            err += Math.Abs(temp[ssn][i][dTime.Hour] - zn[i].Temperature);
            err += Math.Abs(load[ssn][i][dTime.Hour] - zn[i].HeatSupply);
            temp[ssn][i][dTime.Hour] = zn[i].Temperature;
            load[ssn][i][dTime.Hour] = zn[i].HeatSupply;
          }
          //収束判定
          if (dTime.Hour == 23)
          {
            dTime = dTime.AddHours(-23);
            if (err < 0.0001) break;
            else err = 0;
          }
          else dTime = dTime.AddHours(1);
        }
      }

      //出力処理
      using (StreamWriter sWriter = new StreamWriter
          ("heatLoadTest.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        sWriter.WriteLine
          ("時刻,Z0_DB,Z0_HLS,Z1_DB,Z1_HLS,Z0_DB,Z0_HLS,Z1_DB,Z1_HLS");

        for (int i = 0; i < 24; i++)
        {
          sWriter.Write(i);
          for (int ssn = 0; ssn < 2; ssn++)
          {
            sWriter.Write("," + load[ssn][0][i] + "," + load[ssn][1][i]
                 + "," + temp[ssn][0][i] + "," + temp[ssn][1][i]);
          }
          sWriter.WriteLine();
        }
      }
    }

    /// <summary>周期定常熱負荷計算テスト</summary>
    /// <remarks>熱供給能力情報を使って自動で過負荷判定</remarks>
    public static void periodicUnsteadyStateTest2()
    {
      double[] DBTSET = new double[] { 26, 22 };          //設定温度
      double[] HRTSET = new double[] { 0.0105, 0.0066 };  //設定湿度

      //気象データ
      double[][] dbt = new double[2][];
      double[][] hrt = new double[2][];
      double[][] rad = new double[2][];
      dbt[0] = new double[] { 24.9, 24.7, 23.8, 24.2, 24.2, 25, 25, 24.4, 24.1, 23.7, 24.6, 25, 25.3, 25.2, 24.9, 24.9, 25.3, 25.9, 25.8, 25.1, 24.2, 23.5, 23.6, 23.5 };
      dbt[1] = new double[] { 4.1, 4.2, 4.6, 5, 4.4, 3.8, 4.8, 4.4, 4.9, 6.5, 7.7, 8.1, 9, 10.1, 10.7, 10.2, 10.1, 9.8, 8.6, 7.9, 7.6, 7.1, 5.4, 4.1 };
      hrt[0] = new double[] { 12.9, 12.8, 13.9, 14.6, 14.7, 14.6, 13.5, 13.7, 14.8, 15.4, 16.1, 15.5, 15.6, 15.6, 16, 16.5, 16.6, 16.4, 16.8, 16, 16, 16.1, 16.2, 16.6 };
      hrt[1] = new double[] { 4.2, 4, 4, 3.6, 4, 3.7, 3.9, 3.9, 3.5, 3.2, 3.4, 3.5, 3.3, 3.2, 3, 3.1, 3.1, 2.8, 2.8, 3, 3.5, 3.1, 3, 3.1 };
      rad[0] = new double[] { 0, 0, 0, 0, 0, 0, 93, 288, 465, 629, 781, 860, 870, 827, 725, 598, 403, 217, 49, 0, 0, 0, 0, 0 };
      rad[1] = new double[] { 0, 0, 0, 0, 0, 0, 0, 1, 146, 318, 438, 506, 532, 467, 391, 232, 83, 0, 0, 0, 0, 0, 0, 0 };

      //建物データを作成
      BuildingThermalModel bMdl = makeSunSpaceBuildingModel();
      bMdl.SetHeatingCapacity(0, 0, 2000);
      bMdl.SetHeatingCapacity(0, 1, 2000);
      bMdl.SetCoolingCapacity(0, 0, 2000);
      bMdl.SetCoolingCapacity(0, 1, 2000);

      //収束判定配列
      double[][][] temp = new double[2][][];
      double[][][] load = new double[2][][];
      for (int i = 0; i < temp.Length; i++)
      {
        temp[i] = new double[2][];
        load[i] = new double[2][];
        for (int j = 0; j < temp[i].Length; j++)
        {
          temp[i][j] = new double[24];
          load[i][j] = new double[24];
        }
      }

      Sun sun = new Sun(Sun.City.Tokyo);
      DateTime dTime;
      for (int ssn = 0; ssn < 2; ssn++) //ssn=0で夏季、ssn=1で冬季の計算
      {
        if (ssn == 0) dTime = new DateTime(2001, 7, 20, 0, 0, 0);
        else dTime = new DateTime(2001, 1, 20, 0, 0, 0);
        double err = 0;
        while (true)
        {
          int hr = dTime.Hour;
          //日射は過去1時間の積算データのため、太陽位置を30分ずらす
          sun.Update(dTime.AddMinutes(30));
          sun.SeparateGlobalHorizontalRadiation(rad[ssn][hr], Sun.SeparationMethod.Erbs);
          bMdl.UpdateOutdoorCondition(dTime, sun, dbt[ssn][hr], hrt[ssn][hr], 0);

          if (8 < dTime.Hour && dTime.Hour < 18)
          {
            //空調時間帯
            bMdl.ControlDrybulbTemperature(0, 0, DBTSET[ssn]);
            bMdl.ControlDrybulbTemperature(0, 1, DBTSET[ssn]);
            bMdl.ControlHumidityRatio(0, 0, HRTSET[ssn]);
            bMdl.ControlHumidityRatio(0, 1, HRTSET[ssn]);
          }
          else
          {
            //非空調時間帯（自然室温）
            bMdl.ControlHeatSupply(0, 0, 0);
            bMdl.ControlHeatSupply(0, 1, 0);
            bMdl.ControlWaterSupply(0, 0, 0);
            bMdl.ControlWaterSupply(0, 1, 0);
          }
          //熱平衡予測
          bMdl.UpdateHeatTransferWithinCapacityLimit();

          //収束誤差
          ImmutableZone[] zn = bMdl.MultiRoom[0].Zones;
          for (int i = 0; i < zn.Length; i++)
          {
            err += Math.Abs(temp[ssn][i][dTime.Hour] - zn[i].Temperature);
            err += Math.Abs(load[ssn][i][dTime.Hour] - zn[i].HeatSupply);
            temp[ssn][i][dTime.Hour] = zn[i].Temperature;
            load[ssn][i][dTime.Hour] = zn[i].HeatSupply;
          }
          //収束判定
          if (dTime.Hour == 23)
          {
            dTime = dTime.AddHours(-23);
            if (err < 0.0001) break;
            else err = 0;
          }
          else dTime = dTime.AddHours(1);
        }
      }

      //出力処理
      using (StreamWriter sWriter = new StreamWriter
          ("heatLoadTest2.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        sWriter.WriteLine
          ("時刻,Z0_DB,Z0_HLS,Z1_DB,Z1_HLS,Z0_DB,Z0_HLS,Z1_DB,Z1_HLS");

        for (int i = 0; i < 24; i++)
        {
          sWriter.Write(i);
          for (int ssn = 0; ssn < 2; ssn++)
          {
            sWriter.Write("," + load[ssn][0][i] + "," + load[ssn][1][i]
                 + "," + temp[ssn][0][i] + "," + temp[ssn][1][i]);
          }
          sWriter.WriteLine();
        }
      }
    }

    public static BuildingThermalModel makeSunSpaceBuildingModel()
    {
      //傾斜面を作成
      Incline incN = new Incline(Incline.Orientation.N, 0.5 * Math.PI);
      Incline incE = new Incline(Incline.Orientation.E, 0.5 * Math.PI);
      Incline incW = new Incline(Incline.Orientation.W, 0.5 * Math.PI);
      Incline incS = new Incline(Incline.Orientation.S, 0.5 * Math.PI);
      Incline incH = new Incline(Incline.Orientation.N, 0);

      //壁構成を作成
      WallLayer[] exWL = new WallLayer[3];  //外壁
      exWL[0] = new WallLayer("コンクリートブロック", 0.51, 1400, 0.1);
      exWL[1] = new WallLayer("断熱材", 0.04, 14, 0.0615);
      exWL[2] = new WallLayer("木質系サイディング", 0.14, 477, 0.009);
      WallLayer[] flWL = new WallLayer[2];  //床
      flWL[0] = new WallLayer("コンクリート", 1.13, 1400, 0.08);
      flWL[1] = new WallLayer("断熱材（無限大）", 0.00001, 0.00001, 1.0);
      WallLayer[] rfWL = new WallLayer[3];  //屋根
      rfWL[0] = new WallLayer("プラスターボード", 0.16, 798, 0.01);
      rfWL[1] = new WallLayer("断熱材", 0.04, 10, 0.1118);
      rfWL[2] = new WallLayer("木質系屋根", 0.14, 477, 0.019);
      WallLayer[] inWL = new WallLayer[1];  //内壁
      inWL[0] = new WallLayer("コンクリートブロック", 0.51, 1400, 0.2);

      //壁を作成
      Wall[] walls = new Wall[11];
      walls[0] = new Wall(2 * 2.7, exWL);
      walls[1] = new Wall(8 * 2.7 - 6 * 2, exWL);
      walls[2] = new Wall(2 * 2.7, exWL);
      walls[3] = new Wall(2 * 8, rfWL);
      walls[4] = new Wall(2 * 8, flWL);
      walls[5] = new Wall(6 * 2.7, exWL);
      walls[6] = new Wall(8 * 2.7, exWL);
      walls[7] = new Wall(6 * 2.7, exWL);
      walls[8] = new Wall(6 * 8, rfWL);
      walls[9] = new Wall(6 * 8, flWL);
      walls[10] = new Wall(8 * 2.7, inWL);
      for (int i = 0; i < walls.Length; i++)
      {
        walls[i].LongWaveEmissivityF = walls[i].LongWaveEmissivityB = 0.9;
        walls[i].ShortWaveAbsorptanceF = walls[i].ShortWaveAbsorptanceB = 0.8;
      }

      //窓を作成
      Window[] windows = new Window[1];
      windows[0] = new Window(6 * 2, new double[] { 0.7, 0.7 }, new double[] { 0.04, 0.04 }, incS);

      //ゾーンを作成
      Zone[] zones = new Zone[2];
      zones[0] = new Zone("SunSpace", 2 * 8 * 2.7 * 1.2);
      zones[1] = new Zone("BackSpace", 6 * 8 * 2.7 * 1.2);
      //0.2回/hの漏気
      zones[0].VentilationRate = zones[0].AirMass / 3600d * 0.2;
      zones[1].VentilationRate = zones[1].AirMass / 3600d * 0.2;

      //MultiRoomsを作成
      MultiRooms mRoom = new MultiRooms(2, zones, walls, windows);

      //ゾーン設定
      mRoom.AddZone(0, 0);
      mRoom.AddZone(1, 1);
      //壁設定
      mRoom.AddWall(0, 0, true); mRoom.SetOutsideWall(0, false, incW);
      mRoom.AddWall(0, 1, true); mRoom.SetOutsideWall(1, false, incS);
      mRoom.AddWall(0, 2, true); mRoom.SetOutsideWall(2, false, incE);
      mRoom.AddWall(0, 3, true); mRoom.SetOutsideWall(3, false, incH);
      mRoom.AddWall(0, 4, true); mRoom.SetOutsideWall(4, false, incH);
      mRoom.AddWall(1, 5, true); mRoom.SetOutsideWall(5, false, incW);
      mRoom.AddWall(1, 6, true); mRoom.SetOutsideWall(6, false, incN);
      mRoom.AddWall(1, 7, true); mRoom.SetOutsideWall(7, false, incE);
      mRoom.AddWall(1, 8, true); mRoom.SetOutsideWall(8, false, incH);
      mRoom.AddWall(1, 9, true); mRoom.SetOutsideWall(9, false, incH);
      mRoom.AddWall(0, 1, 10);
      //窓設定
      mRoom.AddWindow(0, 0);
      //アルベド
      mRoom.Albedo = 0.2;

      //地中温度は20Cで固定
      mRoom.SetGroundTemperature(4, false, 20);
      mRoom.SetGroundTemperature(9, false, 20);

      BuildingThermalModel bMdl = new BuildingThermalModel(new MultiRooms[] { mRoom });
      bMdl.SetInsideConvectiveCoefficient(0, 5);
      bMdl.SetOutsideConvectiveCoefficient(0, 15);

      return bMdl;
    }

    public static void heatLoadTest()
    {
      const bool HEAT_WATER_SIMAL = false;

      //ゾーンリストの作成/////////////////////////
      Zone[] zones = new Zone[4];
      zones[0] = new Zone("西ペリメータ", 2 * 10 * 3 * 1.2);
      zones[1] = new Zone("西インテリア", 3 * 10 * 3 * 1.2);
      zones[2] = new Zone("東インテリア", 3 * 10 * 3 * 1.2);
      zones[3] = new Zone("東ペリメータ", 2 * 10 * 3 * 1.2);
      SimpleHeatGain hg = new SimpleHeatGain(0, 0, 0);
      //zones[1].AddHeatGain(hg);
      //for (int i = 0; i < zones.Length; i++) zones[i].AddHeatGain(hg);

      //壁リストの作成/////////////////////////////
      WallLayer[] layers = new WallLayer[2];
      layers[0] = new WallLayer("コンクリート", 1.4, 1934, 0.00044, 0.780, 700, 0.4, 0.03);
      layers[1] = new WallLayer("コンクリート", 1.4, 1934, 0.00044, 0.780, 700, 0.4, 0.03);

      Wall[] walls = new Wall[17];
      for (int i = 0; i < walls.Length; i++) walls[i] = new Wall(1, layers, HEAT_WATER_SIMAL);

      walls[0].Area = walls[3].Area = walls[8].Area = walls[11].Area = 2 * 3;
      walls[1].Area = walls[2].Area = walls[9].Area = walls[10].Area = 3 * 3;
      walls[4].Area = walls[7].Area = walls[12].Area = walls[15].Area = 2 * 10;
      walls[5].Area = walls[6].Area = walls[13].Area = walls[14].Area = 2 * 10;
      walls[16].Area = 10 * 3;

      Incline incN = new Incline(Incline.Orientation.N, 0.5 * Math.PI);
      Incline incE = new Incline(Incline.Orientation.E, 0.5 * Math.PI);
      Incline incW = new Incline(Incline.Orientation.W, 0.5 * Math.PI);
      Incline incS = new Incline(Incline.Orientation.S, 0.5 * Math.PI);
      Incline incH = new Incline(Incline.Orientation.N, 0);
      Incline incG = new Incline(Incline.Orientation.N, Math.PI);

      for (int i = 0; i < walls.Length; i++)
      {
        walls[i].ConvectiveCoefficientF = 15d;
        walls[i].ShortWaveAbsorptanceF = 0.7;
        walls[i].LongWaveEmissivityF = 0.9;
        walls[i].ConvectiveCoefficientB = 5d;
        walls[i].ShortWaveAbsorptanceB = 0.7;
        walls[i].LongWaveEmissivityB = 0.9;
        walls[i].Initialize(24);
      }
      walls[16].ConvectiveCoefficientF
        = walls[16].ConvectiveCoefficientB = 5d;

      //窓リストの作成/////////////////////////////
      Window[] windows = new Window[0];
      //windows[0] = new Window(10 * 3, new double[] { 0.7, 0.7 }, new double[] { 0.15, 0.15 }, incW);
      //windows[1] = new Window(10 * 3, new double[] { 0.7, 0.7 }, new double[] { 0.15, 0.15 }, incE);

      //多数室の作成とモデル構築///////////////////
      MultiRooms mRoom = new MultiRooms(2, zones, walls, windows);

      //室にゾーンを追加
      mRoom.AddZone(0, 0);
      mRoom.AddZone(0, 1);
      mRoom.AddZone(1, 2);
      mRoom.AddZone(1, 3);
      //ゾーン間換気の設定
      zones[0].VentilationRate = zones[0].AirMass / 3600d * 0.2;
      mRoom.SetCrossVentilation(0, 1, 0.5 * (zones[0].AirMass + zones[1].AirMass) / 3600d * 0.2);
      mRoom.SetCrossVentilation(1, 2, 0.5 * (zones[0].AirMass + zones[1].AirMass) / 3600d * 0.2);
      mRoom.SetCrossVentilation(2, 3, 0.5 * (zones[2].AirMass + zones[3].AirMass) / 3600d * 0.2);

      //DEBUG
      zones[0].HeatCapacity = zones[0].AirMass * 1006 * 9;

      //屋外表面を設定
      for (int i = 0; i < 4; i++) mRoom.SetOutsideWall(i, true, incS);
      for (int i = 4; i < 8; i++) mRoom.SetOutsideWall(i, true, incG);
      for (int i = 8; i < 12; i++) mRoom.SetOutsideWall(i, true, incN);
      for (int i = 12; i < 16; i++) mRoom.SetOutsideWall(i, true, incH);

      //ゾーンに窓を追加
      //mRoom.AddWindow(0, 0);
      //mRoom.AddWindow(3, 1);

      //ゾーンに壁を追加
      mRoom.AddWall(0, 0, false);
      mRoom.AddWall(0, 4, false);
      mRoom.AddWall(0, 8, false);
      mRoom.AddWall(0, 12, false);
      mRoom.AddWall(1, 1, false);
      mRoom.AddWall(1, 5, false);
      mRoom.AddWall(1, 9, false);
      mRoom.AddWall(1, 13, false);
      mRoom.AddWall(1, 16, true);
      mRoom.AddWall(2, 2, false);
      mRoom.AddWall(2, 6, false);
      mRoom.AddWall(2, 10, false);
      mRoom.AddWall(2, 14, false);
      mRoom.AddWall(2, 16, false);
      mRoom.AddWall(3, 3, false);
      mRoom.AddWall(3, 7, false);
      mRoom.AddWall(3, 11, false);
      mRoom.AddWall(3, 15, false);

      BuildingThermalModel bModel = new BuildingThermalModel(new MultiRooms[] { mRoom });

      //屋外条件///////////////////////////////////
      DateTime dTime = new DateTime(2001, 7, 20, 9, 0, 0);
      Sun sun = new Sun(Sun.City.Tokyo);
      sun.Update(dTime);
      //sun.SetGlobalHorizontalRadiation(0, 500);
      sun.SetGlobalHorizontalRadiation(0, 0);

      //更新処理
      //bModel.UpdateOutdoorCondition(dTime, sun, 30, 0.02, 100);
      bModel.UpdateOutdoorCondition(dTime, sun, 30, 0.015, 0);
      bModel.TimeStep = 3600;
      for (int i = 0; i < 96; i++)
      {
        Console.WriteLine(
          zones[0].Temperature.ToString("F2") + ", " +
          zones[1].Temperature.ToString("F2") + ", " +
          zones[2].Temperature.ToString("F2") + ", " +
          zones[3].Temperature.ToString("F2") + ", " +
          zones[0].HumidityRatio.ToString("F3") + ", " +
          zones[1].HumidityRatio.ToString("F3") + ", " +
          zones[2].HumidityRatio.ToString("F3") + ", " +
          zones[3].HumidityRatio.ToString("F3"));
        bModel.ForecastHeatTransfer();
        bModel.FixState();
      }
    }

    #endregion

    #region 大規模熱負荷計算テスト

    public static void largeHeatLoadTest()
    {
      BuildingThermalModel bModel = makeLargeBuilding();

      using (StreamWriter sWriter = new StreamWriter("heatLoadTestOut.csv", false, Encoding.GetEncoding("Shift_JIS")))
      using (StreamReader sReader = new StreamReader("bound.csv"))
      {
        DateTime dTime = new DateTime(1999, 1, 1, 0, 0, 0);
        Sun sun = new Sun(Sun.City.Tokyo);

        //ヘッダ
        sReader.ReadLine();
        for (int i = 0; i < bModel.MultiRoom.Length; i++)
          for (int j = 0; j < bModel.MultiRoom[i].ZoneNumber; j++)
            sWriter.Write("," + bModel.MultiRoom[i].Zones[j].Name + "乾球温度");
        for (int i = 0; i < bModel.MultiRoom.Length; i++)
          for (int j = 0; j < bModel.MultiRoom[i].ZoneNumber; j++)
            sWriter.Write("," + bModel.MultiRoom[i].Zones[j].Name + "絶対湿度");
        for (int i = 0; i < bModel.MultiRoom.Length; i++)
          for (int j = 0; j < bModel.MultiRoom[i].ZoneNumber; j++)
            sWriter.Write("," + bModel.MultiRoom[i].Zones[j].Name + "顕熱負荷");
        for (int i = 0; i < bModel.MultiRoom.Length; i++)
          for (int j = 0; j < bModel.MultiRoom[i].ZoneNumber; j++)
            sWriter.Write("," + bModel.MultiRoom[i].Zones[j].Name + "潜熱負荷");
        sWriter.WriteLine();

        for (int hour = 0; hour < 8760; hour++)
        {
          Console.WriteLine(dTime);

          string[] bf = sReader.ReadLine().Split(',');
          double dbt = double.Parse(bf[1]);
          double hrt = double.Parse(bf[2]);
          double dnr = double.Parse(bf[3]);
          double srd = double.Parse(bf[4]);
          double nrd = double.Parse(bf[5]);

          sun.Update(dTime);
          sun.SetGlobalHorizontalRadiation(srd, dnr);
          bModel.UpdateOutdoorCondition(dTime, sun, dbt, hrt, nrd);

          //空調設定
          if ((7 <= dTime.Hour && dTime.Hour < 21) && (dTime.DayOfWeek != DayOfWeek.Sunday && dTime.DayOfWeek != DayOfWeek.Saturday))
          {
            double dbtSet, hrtSet;
            if (12 <= dTime.Month || dTime.Month <= 3)
            {
              dbtSet = 22;
              hrtSet = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(22, 40, 101.325);
            }
            else if (6 <= dTime.Month && dTime.Month <= 9)
            {
              dbtSet = 26;
              hrtSet = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(26, 50, 101.325);
            }
            else
            {
              dbtSet = 24;
              hrtSet = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(24, 50, 101.325);
            }
            for (int fl = 0; fl < 7; fl++)
            {
              for (int i = 0; i < bModel.MultiRoom.Length; i++)
              {
                if ((i + 1) % 3 != 0)
                {
                  for (int j = 0; j < bModel.MultiRoom[i].ZoneNumber; j++)
                  {
                    bModel.ControlDrybulbTemperature(i, j, dbtSet);
                    bModel.ControlHumidityRatio(i, j, hrtSet);
                  }
                }
              }
            }
          }
          else
          {
            for (int i = 0; i < bModel.MultiRoom.Length; i++)
            {
              for (int j = 0; j < bModel.MultiRoom[i].ZoneNumber; j++)
              {
                bModel.ControlHeatSupply(i, j, 0);
                bModel.ControlWaterSupply(i, j, 0);
              }
            }
          }

          bModel.ForecastHeatTransfer();
          bModel.ForecastWaterTransfer();
          bModel.FixState();

          //書き出し
          sWriter.Write(dTime);
          for (int i = 0; i < bModel.MultiRoom.Length; i++)
            for (int j = 0; j < bModel.MultiRoom[i].ZoneNumber; j++)
              sWriter.Write("," + bModel.MultiRoom[i].Zones[j].Temperature);
          for (int i = 0; i < bModel.MultiRoom.Length; i++)
            for (int j = 0; j < bModel.MultiRoom[i].ZoneNumber; j++)
              sWriter.Write("," + bModel.MultiRoom[i].Zones[j].HumidityRatio);
          for (int i = 0; i < bModel.MultiRoom.Length; i++)
            for (int j = 0; j < bModel.MultiRoom[i].ZoneNumber; j++)
              sWriter.Write("," + bModel.MultiRoom[i].Zones[j].HeatSupply);
          for (int i = 0; i < bModel.MultiRoom.Length; i++)
            for (int j = 0; j < bModel.MultiRoom[i].ZoneNumber; j++)
              sWriter.Write("," + bModel.MultiRoom[i].Zones[j].WaterSupply);

          sWriter.WriteLine();

          dTime = dTime.AddHours(1);
        }
      }
    }

    /// <summary>巨大な熱負荷計算モデルを作成する</summary>
    /// <returns>巨大な熱負荷計算モデル</returns>
    public static BuildingThermalModel makeLargeBuilding()
    {
      //傾斜面の作成（四方位）//////////////
      Incline incN = new Incline(Incline.Orientation.N, 0.5 * Math.PI);
      Incline incE = new Incline(Incline.Orientation.E, 0.5 * Math.PI);
      Incline incW = new Incline(Incline.Orientation.W, 0.5 * Math.PI);
      Incline incS = new Incline(Incline.Orientation.S, 0.5 * Math.PI);
      Incline incH = new Incline(Incline.Orientation.S, 0);

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

      WallLayer[] exRF = new WallLayer[9];  //外壁屋根
      exRF[0] = new WallLayer("コンクリート", 1.6, 2000, 0.060);
      exRF[1] = new WallLayer("押出ポリスチレンフォーム1種", 0.040, 33, 0.050);
      exRF[2] = new WallLayer("セメント・モルタル", 1.5, 1600, 0.015);
      exRF[3] = new WallLayer("アスファルト類", 0.110, 920, 0.005);
      exRF[4] = new WallLayer("セメント・モルタル", 1.5, 1600, 0.015);
      exRF[5] = new WallLayer("コンクリート", 1.6, 2000, 0.150);
      exRF[6] = new AirGapLayer("非密閉中空層", false, 0.05);
      exRF[7] = new WallLayer("石膏ボード", 0.220, 830, 0.010);
      exRF[8] = new WallLayer("ロックウール化粧吸音板", 0.064, 290, 0.015);

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

      WallLayer[] inSWL = new WallLayer[1];  //内壁_テナント間仕切用仮想壁
      inSWL[0] = new WallLayer("仮想壁", 10000, 1, 0.01);

      //ゾーンを作成/////////////////////////
      Zone[][] znNs = new Zone[7][];
      Zone[][] znSs = new Zone[7][];
      Zone[][] znCs = new Zone[7][];
      //1F
      znNs[0] = new Zone[5];
      znSs[0] = new Zone[5];
      znCs[0] = new Zone[3];
      znNs[0][0] = new Zone("1-NWP", (3.25 * 14) * 2.7 * 1.2);
      znNs[0][1] = new Zone("1-NP-1", (9.75 * 4) * 2.7 * 1.2);
      znNs[0][2] = new Zone("1-NP-2", (13 * 4) * 2.7 * 1.2);
      znNs[0][3] = new Zone("1-NI-1", (9.75 * 10) * 2.7 * 1.2);
      znNs[0][4] = new Zone("1-NI-2", (13 * 10) * 2.7 * 1.2);
      znSs[0][0] = new Zone("1-SWP", (3.25 * 14) * 2.7 * 1.2);
      znSs[0][1] = new Zone("1-SP-1", (9.75 * 4) * 2.7 * 1.2);
      znSs[0][2] = new Zone("1-SP-2", (6.5 * 4) * 2.7 * 1.2);
      znSs[0][3] = new Zone("1-SI-1", (9.75 * 10) * 2.7 * 1.2);
      znSs[0][4] = new Zone("1-SI-2", (6.5 * 10) * 2.7 * 1.2);
      znCs[0][0] = new Zone("1-HL", 140 * 3.0 * 1.2);
      znCs[0][1] = new Zone("1-WC", 90 * 2.4 * 1.2);
      znCs[0][2] = new Zone("1-IL", 160 * 2.4 * 1.2);
      //2-7F
      for (int i = 1; i < 7; i++)
      {
        znNs[i] = new Zone[10];
        znSs[i] = new Zone[9];
        znCs[i] = new Zone[2];

        znNs[i][0] = new Zone((i + 1) + "-NWP", (3.25 * 14) * 2.7 * 1.2);
        znNs[i][1] = new Zone((i + 1) + "-NP-1", (9.75 * 4) * 2.7 * 1.2);
        znNs[i][2] = new Zone((i + 1) + "-NP-2", (6.5 * 4) * 2.7 * 1.2);
        znNs[i][3] = new Zone((i + 1) + "-NP-3", (10.0 * 4) * 2.7 * 1.2);
        znNs[i][4] = new Zone((i + 1) + "-NP-4", (9.75 * 4) * 2.7 * 1.2);
        znNs[i][5] = new Zone((i + 1) + "-NEP", (3.25 * 14) * 2.7 * 1.2);
        znNs[i][6] = new Zone((i + 1) + "-NI-1", (9.75 * 10) * 2.7 * 1.2);
        znNs[i][7] = new Zone((i + 1) + "-NI-2", (6.5 * 10) * 2.7 * 1.2);
        znNs[i][8] = new Zone((i + 1) + "-NI-3", (10.0 * 10) * 2.7 * 1.2);
        znNs[i][9] = new Zone((i + 1) + "-NI-4", (9.75 * 10) * 2.7 * 1.2);
        znSs[i][0] = new Zone((i + 1) + "-SWP", (3.25 * 14) * 2.7 * 1.2);
        znSs[i][1] = new Zone((i + 1) + "-SP-1", (9.75 * 4) * 2.7 * 1.2);
        znSs[i][2] = new Zone((i + 1) + "-SP-2", (6.5 * 4) * 2.7 * 1.2);
        znSs[i][3] = new Zone((i + 1) + "-SP-3", (10.0 * 4) * 2.7 * 1.2);
        znSs[i][4] = new Zone((i + 1) + "-SP-4", (6.5 * 4) * 2.7 * 1.2);
        znSs[i][5] = new Zone((i + 1) + "-SI-1", (9.75 * 10) * 2.7 * 1.2);
        znSs[i][6] = new Zone((i + 1) + "-SI-2", (6.5 * 10) * 2.7 * 1.2);
        znSs[i][7] = new Zone((i + 1) + "-SI-3", (10.0 * 10) * 2.7 * 1.2);
        znSs[i][8] = new Zone((i + 1) + "-SI-4", (6.5 * 10) * 2.7 * 1.2);
        znCs[i][0] = new Zone((i + 1) + "-WC", 90 * 2.4 * 1.2);
        znCs[i][1] = new Zone((i + 1) + "-IL", 160 * 2.4 * 1.2);
      }

      //内部発熱を設定*************************************************************************
      for (int fl = 0; fl < 7; fl++)
      {
        for (int i = 0; i < znNs[fl].Length; i++)
        {
          double area = znNs[fl][i].AirMass / 1.2 / 2.7;
          znNs[fl][i].AddHeatGain(new SHASE_SimulationTest.MyHeatGain(0.1 * area, 12 * area, 12 * area, true));
        }
        for (int i = 0; i < znSs[fl].Length; i++)
        {
          double area = znSs[fl][i].AirMass / 1.2 / 2.7;
          znSs[fl][i].AddHeatGain(new SHASE_SimulationTest.MyHeatGain(0.1 * area, 12 * area, 12 * area, true));
        }
      }

      ////隙間風と熱容量設定*************************************************************************
      for (int fl = 0; fl < 7; fl++)
      {
        for (int i = 0; i < znNs[fl].Length; i++)
        {
          znNs[fl][i].HeatCapacity = znNs[fl][i].AirMass * 1006 * 10;
          znNs[fl][i].VentilationRate = znNs[fl][i].AirMass / 3600d * 0.1;
          znNs[fl][i].InitializeAirState(22, 0.0105);
        }
        for (int i = 0; i < znSs[fl].Length; i++)
        {
          znSs[fl][i].HeatCapacity = znSs[fl][i].AirMass * 1006 * 10;
          znSs[fl][i].VentilationRate = znSs[fl][i].AirMass / 3600d * 0.1;
          znSs[fl][i].InitializeAirState(22, 0.0105);
        }
        for (int i = 0; i < znCs[fl].Length; i++)
        {
          znCs[fl][i].HeatCapacity = 0;
          znCs[fl][i].VentilationRate = znCs[fl][i].AirMass / 3600d * 0.1;
          znCs[fl][i].InitializeAirState(22, 0.0105);
        }
      }

      //壁体の作成***************************************************************************************
      Wall[] walls = new Wall[488];
      //1F
      //外壁
      walls[0] = new Wall(46.76, exWL);
      walls[1] = new Wall(12.60, exbmWL);
      walls[2] = new Wall(10.67, exWL);
      walls[3] = new Wall(2.93, exbmWL);
      walls[4] = new Wall(32.00, exWL);
      walls[5] = new Wall(8.78, exbmWL);
      walls[6] = new Wall(42.66, exWL);
      walls[7] = new Wall(11.70, exbmWL);
      walls[8] = new Wall(46.76, exWL);
      walls[9] = new Wall(12.60, exbmWL);
      walls[10] = new Wall(10.67, exWL);
      walls[11] = new Wall(2.93, exbmWL);
      walls[12] = new Wall(32.00, exWL);
      walls[13] = new Wall(8.78, exbmWL);
      walls[14] = new Wall(16.01, exWL);
      walls[15] = new Wall(5.85, exbmWL);
      walls[16] = new Wall(15.00, exWL);
      walls[17] = new Wall(32.50, exWL);
      walls[18] = new Wall(70.00, exWL);
      walls[19] = new Wall(12.50, exWL);
      //内壁
      walls[20] = new Wall(8.78, inWL);
      walls[21] = new Wall(26.33, inWL);
      walls[22] = new Wall(35.10, inWL);
      walls[23] = new Wall(27.00, inWL);
      walls[24] = new Wall(10.80, inWL);
      walls[25] = new Wall(8.78, inWL);
      walls[26] = new Wall(26.33, inWL);
      walls[27] = new Wall(17.55, inWL);
      walls[28] = new Wall(27.00, inWL);
      walls[29] = new Wall(10.80, inWL);
      walls[30] = new Wall(37.80, inWL);
      walls[31] = new Wall(37.80, inWL);
      walls[32] = new Wall(17.55, inWL);
      walls[33] = new Wall(81.00, inWL);
      walls[34] = new Wall(45.50, flWL);
      walls[35] = new Wall(39.00, flWL);
      walls[36] = new Wall(52.00, flWL);
      walls[37] = new Wall(97.50, flWL);
      walls[38] = new Wall(130.00, flWL);
      walls[39] = new Wall(45.50, flWL);
      walls[40] = new Wall(39.00, flWL);
      walls[41] = new Wall(26.00, flWL);
      walls[42] = new Wall(97.50, flWL);
      walls[43] = new Wall(65.00, flWL);
      walls[44] = new Wall(160.00, flWL);
      walls[45] = new Wall(90.00, flWL);
      walls[46] = new Wall(160.00, flWL);
      //2F-7F
      for (int fl = 1; fl < 7; fl++)
      {
        int ofs = 47 + 70 * (fl - 1);
        //外壁
        walls[ofs + 0] = new Wall(32.76, exWL);
        walls[ofs + 1] = new Wall(12.60, exbmWL);
        walls[ofs + 2] = new Wall(7.42, exWL);
        walls[ofs + 3] = new Wall(2.93, exbmWL);
        walls[ofs + 4] = new Wall(22.25, exWL);
        walls[ofs + 5] = new Wall(8.78, exbmWL);
        walls[ofs + 6] = new Wall(14.83, exWL);
        walls[ofs + 7] = new Wall(5.85, exbmWL);
        walls[ofs + 8] = new Wall(20.36, exWL);
        walls[ofs + 9] = new Wall(9.00, exbmWL);
        walls[ofs + 10] = new Wall(22.25, exWL);
        walls[ofs + 11] = new Wall(8.78, exbmWL);
        walls[ofs + 12] = new Wall(32.76, exWL);
        walls[ofs + 13] = new Wall(12.60, exbmWL);
        walls[ofs + 14] = new Wall(7.42, exWL);
        walls[ofs + 15] = new Wall(2.93, exbmWL);
        walls[ofs + 16] = new Wall(32.76, exWL);
        walls[ofs + 17] = new Wall(12.60, exbmWL);
        walls[ofs + 18] = new Wall(7.42, exWL);
        walls[ofs + 19] = new Wall(2.93, exbmWL);
        walls[ofs + 20] = new Wall(22.25, exWL);
        walls[ofs + 21] = new Wall(8.78, exbmWL);
        walls[ofs + 22] = new Wall(14.83, exWL);
        walls[ofs + 23] = new Wall(5.85, exbmWL);
        walls[ofs + 24] = new Wall(20.36, exWL);
        walls[ofs + 25] = new Wall(9.00, exbmWL);
        walls[ofs + 26] = new Wall(12.17, exWL);
        walls[ofs + 27] = new Wall(5.85, exbmWL);
        walls[ofs + 28] = new Wall(26.00, exWL);
        walls[ofs + 29] = new Wall(56.00, exWL);
        walls[ofs + 30] = new Wall(25.00, exWL);
        //内壁
        walls[ofs + 31] = new Wall(8.78, inWL);
        walls[ofs + 32] = new Wall(26.33, inWL);
        walls[ofs + 33] = new Wall(35.10, inWL);
        walls[ofs + 34] = new Wall(27.00, inWL);
        walls[ofs + 35] = new Wall(26.33, inWL);
        walls[ofs + 36] = new Wall(8.78, inWL);
        walls[ofs + 37] = new Wall(8.78, inWL);
        walls[ofs + 38] = new Wall(26.33, inWL);
        walls[ofs + 39] = new Wall(35.10, inWL);
        walls[ofs + 40] = new Wall(27.00, inWL);
        walls[ofs + 41] = new Wall(17.55, inWL);
        walls[ofs + 42] = new Wall(17.55, inWL);
        walls[ofs + 43] = new Wall(27.00, inWL);
        walls[ofs + 44] = new Wall(10.80, inWL);
        walls[ofs + 45] = new Wall(10.80, inSWL);
        walls[ofs + 46] = new Wall(27.00, inSWL);
        walls[ofs + 47] = new Wall(10.80, inSWL);
        walls[ofs + 48] = new Wall(27.00, inSWL);
        walls[ofs + 49] = new Wall(45.50, flWL);
        walls[ofs + 50] = new Wall(39.00, flWL);
        walls[ofs + 51] = new Wall(26.00, flWL);
        walls[ofs + 52] = new Wall(40.00, flWL);
        walls[ofs + 53] = new Wall(39.00, flWL);
        walls[ofs + 54] = new Wall(45.50, flWL);
        walls[ofs + 55] = new Wall(97.50, flWL);
        walls[ofs + 56] = new Wall(65.00, flWL);
        walls[ofs + 57] = new Wall(100.00, flWL);
        walls[ofs + 58] = new Wall(97.50, flWL);
        walls[ofs + 59] = new Wall(45.50, flWL);
        walls[ofs + 60] = new Wall(39.00, flWL);
        walls[ofs + 61] = new Wall(26.00, flWL);
        walls[ofs + 62] = new Wall(40.00, flWL);
        walls[ofs + 63] = new Wall(26.00, flWL);
        walls[ofs + 64] = new Wall(97.50, flWL);
        walls[ofs + 65] = new Wall(65.00, flWL);
        walls[ofs + 66] = new Wall(100.00, flWL);
        walls[ofs + 67] = new Wall(65.00, flWL);
        walls[ofs + 68] = new Wall(90.00, flWL);
        walls[ofs + 69] = new Wall(160.00, flWL);
      }
      //屋上スラブ
      walls[467] = new Wall(45.50, flWL);
      walls[468] = new Wall(39.00, flWL);
      walls[469] = new Wall(26.00, flWL);
      walls[470] = new Wall(40.00, flWL);
      walls[471] = new Wall(39.00, flWL);
      walls[472] = new Wall(45.50, flWL);
      walls[473] = new Wall(97.50, flWL);
      walls[474] = new Wall(65.00, flWL);
      walls[475] = new Wall(100.00, flWL);
      walls[476] = new Wall(97.50, flWL);
      walls[477] = new Wall(45.50, flWL);
      walls[478] = new Wall(39.00, flWL);
      walls[479] = new Wall(26.00, flWL);
      walls[480] = new Wall(40.00, flWL);
      walls[481] = new Wall(26.00, flWL);
      walls[482] = new Wall(97.50, flWL);
      walls[483] = new Wall(65.00, flWL);
      walls[484] = new Wall(100.00, flWL);
      walls[485] = new Wall(65.00, flWL);
      walls[486] = new Wall(90.00, flWL);
      walls[487] = new Wall(160.00, flWL);
      //壁の初期化
      for (int i = 0; i < walls.Length; i++)
      {
        walls[i].ShortWaveAbsorptanceF = walls[i].ShortWaveAbsorptanceB = 0.8;
        walls[i].LongWaveEmissivityF = walls[i].LongWaveEmissivityB = 0.9;
        walls[i].RadiativeCoefficientF = walls[i].RadiativeCoefficientB = 5;
        string nm = walls[i].Layers[0].Name;
        if (nm == "コンクリート" || nm == "タイル") walls[i].ConvectiveCoefficientF = 18;
        else walls[i].ConvectiveCoefficientF = 4;
        walls[i].ConvectiveCoefficientB = 4;
        walls[i].Initialize(20);
      }

      //窓を作成***************************************************************************************
      double[] TAU_WIN, RHO_WIN;
      TAU_WIN = new double[] { 0.815 }; //ガラスの透過率リスト[-]
      RHO_WIN = new double[] { 0.072 }; //ガラスの反射率リスト[-]
      Window[][][] win = new Window[7][][];
      //1F
      win[0] = new Window[3][];
      win[0][0] = new Window[4];
      win[0][1] = new Window[4];
      win[0][2] = new Window[1];
      win[0][0][0] = new Window(5.32 * 2.0, TAU_WIN, RHO_WIN, incW);
      win[0][0][1] = new Window(5.32 * 0.5, TAU_WIN, RHO_WIN, incN);
      win[0][0][2] = new Window(5.32 * 1.5, TAU_WIN, RHO_WIN, incN);
      win[0][0][3] = new Window(5.32 * 2.0, TAU_WIN, RHO_WIN, incN);
      win[0][1][0] = new Window(5.32 * 2.0, TAU_WIN, RHO_WIN, incW);
      win[0][1][1] = new Window(5.32 * 0.5, TAU_WIN, RHO_WIN, incS);
      win[0][1][2] = new Window(5.32 * 1.5, TAU_WIN, RHO_WIN, incS);
      win[0][1][3] = new Window(5.32 * 1.0, TAU_WIN, RHO_WIN, incS);
      win[0][2][0] = new Window(10.0 * 2.8, TAU_WIN, RHO_WIN, incS);
      //2-7F
      for (int fl = 1; fl < 7; fl++)
      {
        win[fl] = new Window[3][];
        win[fl][0] = new Window[8];
        win[fl][1] = new Window[6];
        win[fl][2] = new Window[0];
        win[fl][0][0] = new Window(5.32 * 2.0, TAU_WIN, RHO_WIN, incW);
        win[fl][0][1] = new Window(5.32 * 0.5, TAU_WIN, RHO_WIN, incN);
        win[fl][0][2] = new Window(5.32 * 1.5, TAU_WIN, RHO_WIN, incN);
        win[fl][0][3] = new Window(5.32 * 1.0, TAU_WIN, RHO_WIN, incN);
        win[fl][0][4] = new Window(5.32 * 2.0, TAU_WIN, RHO_WIN, incN);
        win[fl][0][5] = new Window(5.32 * 1.5, TAU_WIN, RHO_WIN, incN);
        win[fl][0][6] = new Window(5.32 * 0.5, TAU_WIN, RHO_WIN, incN);
        win[fl][0][7] = new Window(5.32 * 2.0, TAU_WIN, RHO_WIN, incE);
        win[fl][1][0] = new Window(5.32 * 2.0, TAU_WIN, RHO_WIN, incW);
        win[fl][1][1] = new Window(5.32 * 0.5, TAU_WIN, RHO_WIN, incS);
        win[fl][1][2] = new Window(5.32 * 1.5, TAU_WIN, RHO_WIN, incS);
        win[fl][1][3] = new Window(5.32 * 1.0, TAU_WIN, RHO_WIN, incS);
        win[fl][1][4] = new Window(5.32 * 2.0, TAU_WIN, RHO_WIN, incS);
        win[fl][1][5] = new Window(5.32 * 1.0, TAU_WIN, RHO_WIN, incS);
      }
      //初期化
      for (int fl = 0; fl < 7; fl++)
      {
        for (int i = 0; i < win[fl].Length; i++)
        {
          for (int j = 0; j < win[fl][i].Length; j++)
          {
            VenetianBlind blind = new VenetianBlind(25, 22.5, 0, 0, 0.66, 0.66);
            blind.SlatAngle = 0;
            win[fl][i][j].SetShadingDevice(1, blind);
            win[fl][i][j].ConvectiveCoefficientF = 18;
            win[fl][i][j].ConvectiveCoefficientB = 4;
            win[fl][i][j].LongWaveEmissivityF = win[fl][i][j].LongWaveEmissivityB = 0.9;
          }
        }
      }

      //多数室の作成************************************************************************************
      MultiRooms[] mRm = new MultiRooms[3 * 7];
      //1F
      mRm[0] = new MultiRooms(1, znNs[0], walls, win[0][0]);
      mRm[1] = new MultiRooms(1, znSs[0], walls, win[0][1]);
      mRm[2] = new MultiRooms(3, znCs[0], walls, win[0][2]);
      for (int i = 0; i < znNs[0].Length; i++) mRm[0].AddZone(0, i);
      for (int i = 0; i < znSs[0].Length; i++) mRm[1].AddZone(0, i);
      for (int i = 0; i < znCs[0].Length; i++) mRm[2].AddZone(i, i);
      //2-7F
      for (int fl = 1; fl < 7; fl++)
      {
        //北側事務室
        int rmN = 3 + (fl - 1) * 3;
        mRm[rmN] = new MultiRooms(2, znNs[fl], walls, win[fl][0]);
        mRm[rmN].AddZone(0, 0); mRm[rmN].AddZone(0, 1); mRm[rmN].AddZone(0, 2); mRm[rmN].AddZone(0, 6); mRm[rmN].AddZone(0, 7); //西側テナント区画
        mRm[rmN].AddZone(1, 3); mRm[rmN].AddZone(1, 4); mRm[rmN].AddZone(1, 5); mRm[rmN].AddZone(1, 8); mRm[rmN].AddZone(1, 9); //東側テナント区画
        //南側事務室
        rmN = 4 + (fl - 1) * 3;
        mRm[rmN] = new MultiRooms(2, znSs[fl], walls, win[fl][1]);
        mRm[rmN].AddZone(0, 0); mRm[rmN].AddZone(0, 1); mRm[rmN].AddZone(0, 2); mRm[rmN].AddZone(0, 5); mRm[rmN].AddZone(0, 6); //西側テナント区画
        mRm[rmN].AddZone(1, 3); mRm[rmN].AddZone(1, 4); mRm[rmN].AddZone(1, 7); mRm[rmN].AddZone(1, 8); //東側テナント区画
        //共用部
        rmN = 5 + (fl - 1) * 3;
        mRm[rmN] = new MultiRooms(2, znCs[fl], walls, win[fl][2]);
        mRm[rmN].AddZone(0, 0); //廊下・EVホール
        mRm[rmN].AddZone(1, 1); //便所
      }

      //外壁を登録***************************************************************************************
      //1F北側事務室
      mRm[0].AddWall(0, 0, false); mRm[0].SetOutsideWall(0, true, incW);
      mRm[0].AddWall(0, 1, false); mRm[0].SetOutsideWall(1, true, incW);
      mRm[0].AddWall(0, 2, false); mRm[0].SetOutsideWall(2, true, incN);
      mRm[0].AddWall(0, 3, false); mRm[0].SetOutsideWall(3, true, incN);
      mRm[0].AddWall(1, 4, false); mRm[0].SetOutsideWall(4, true, incN);
      mRm[0].AddWall(1, 5, false); mRm[0].SetOutsideWall(5, true, incN);
      mRm[0].AddWall(2, 6, false); mRm[0].SetOutsideWall(6, true, incN);
      mRm[0].AddWall(2, 7, false); mRm[0].SetOutsideWall(7, true, incN);
      //1F南側事務室
      mRm[1].AddWall(0, 8, false); mRm[1].SetOutsideWall(8, true, incW);
      mRm[1].AddWall(0, 9, false); mRm[1].SetOutsideWall(9, true, incW);
      mRm[1].AddWall(0, 10, false); mRm[1].SetOutsideWall(10, true, incS);
      mRm[1].AddWall(0, 11, false); mRm[1].SetOutsideWall(11, true, incS);
      mRm[1].AddWall(1, 12, false); mRm[1].SetOutsideWall(12, true, incS);
      mRm[1].AddWall(1, 13, false); mRm[1].SetOutsideWall(13, true, incS);
      mRm[1].AddWall(2, 14, false); mRm[1].SetOutsideWall(14, true, incS);
      mRm[1].AddWall(2, 15, false); mRm[1].SetOutsideWall(15, true, incS);
      //1F共用部
      mRm[2].AddWall(0, 16, false); mRm[2].SetOutsideWall(16, true, incS);
      mRm[2].AddWall(1, 17, false); mRm[2].SetOutsideWall(17, true, incS);
      mRm[2].AddWall(1, 18, false); mRm[2].SetOutsideWall(18, true, incE);
      mRm[2].AddWall(2, 19, false); mRm[2].SetOutsideWall(19, true, incE);
      //2-7F
      for (int fl = 1; fl < 7; fl++)
      {
        int ofs = 47 + 70 * (fl - 1);
        //北側事務室
        int rmN = 3 + (fl - 1) * 3;
        mRm[rmN].AddWall(0, ofs + 0, false); mRm[rmN].SetOutsideWall(ofs + 0, true, incW);
        mRm[rmN].AddWall(0, ofs + 1, false); mRm[rmN].SetOutsideWall(ofs + 1, true, incW);
        mRm[rmN].AddWall(0, ofs + 2, false); mRm[rmN].SetOutsideWall(ofs + 2, true, incN);
        mRm[rmN].AddWall(0, ofs + 3, false); mRm[rmN].SetOutsideWall(ofs + 3, true, incN);
        mRm[rmN].AddWall(1, ofs + 4, false); mRm[rmN].SetOutsideWall(ofs + 4, true, incN);
        mRm[rmN].AddWall(1, ofs + 5, false); mRm[rmN].SetOutsideWall(ofs + 5, true, incN);
        mRm[rmN].AddWall(2, ofs + 6, false); mRm[rmN].SetOutsideWall(ofs + 6, true, incN);
        mRm[rmN].AddWall(2, ofs + 7, false); mRm[rmN].SetOutsideWall(ofs + 7, true, incN);
        mRm[rmN].AddWall(3, ofs + 8, false); mRm[rmN].SetOutsideWall(ofs + 8, true, incN);
        mRm[rmN].AddWall(3, ofs + 9, false); mRm[rmN].SetOutsideWall(ofs + 9, true, incN);
        mRm[rmN].AddWall(4, ofs + 10, false); mRm[rmN].SetOutsideWall(ofs + 10, true, incN);
        mRm[rmN].AddWall(4, ofs + 11, false); mRm[rmN].SetOutsideWall(ofs + 11, true, incN);
        mRm[rmN].AddWall(5, ofs + 12, false); mRm[rmN].SetOutsideWall(ofs + 12, true, incN);
        mRm[rmN].AddWall(5, ofs + 13, false); mRm[rmN].SetOutsideWall(ofs + 13, true, incN);
        mRm[rmN].AddWall(5, ofs + 14, false); mRm[rmN].SetOutsideWall(ofs + 14, true, incE);
        mRm[rmN].AddWall(5, ofs + 15, false); mRm[rmN].SetOutsideWall(ofs + 15, true, incE);
        //南側事務室
        rmN++;
        mRm[rmN].AddWall(0, ofs + 16, false); mRm[rmN].SetOutsideWall(ofs + 16, true, incW);
        mRm[rmN].AddWall(0, ofs + 17, false); mRm[rmN].SetOutsideWall(ofs + 17, true, incW);
        mRm[rmN].AddWall(0, ofs + 18, false); mRm[rmN].SetOutsideWall(ofs + 18, true, incS);
        mRm[rmN].AddWall(0, ofs + 19, false); mRm[rmN].SetOutsideWall(ofs + 19, true, incS);
        mRm[rmN].AddWall(1, ofs + 20, false); mRm[rmN].SetOutsideWall(ofs + 20, true, incS);
        mRm[rmN].AddWall(1, ofs + 21, false); mRm[rmN].SetOutsideWall(ofs + 21, true, incS);
        mRm[rmN].AddWall(2, ofs + 22, false); mRm[rmN].SetOutsideWall(ofs + 22, true, incS);
        mRm[rmN].AddWall(2, ofs + 23, false); mRm[rmN].SetOutsideWall(ofs + 23, true, incS);
        mRm[rmN].AddWall(3, ofs + 24, false); mRm[rmN].SetOutsideWall(ofs + 24, true, incS);
        mRm[rmN].AddWall(3, ofs + 25, false); mRm[rmN].SetOutsideWall(ofs + 25, true, incS);
        mRm[rmN].AddWall(4, ofs + 26, false); mRm[rmN].SetOutsideWall(ofs + 26, true, incS);
        mRm[rmN].AddWall(4, ofs + 27, false); mRm[rmN].SetOutsideWall(ofs + 27, true, incS);
        //共用部
        rmN++;
        mRm[rmN].AddWall(0, ofs + 28, false); mRm[rmN].SetOutsideWall(ofs + 28, true, incS);
        mRm[rmN].AddWall(0, ofs + 29, false); mRm[rmN].SetOutsideWall(ofs + 29, true, incE);
        mRm[rmN].AddWall(1, ofs + 30, false); mRm[rmN].SetOutsideWall(ofs + 30, true, incE);
      }
      //7F屋上
      for (int i = 0; i < 10; i++)
      {
        mRm[18].AddWall(i, 467 + i, false);
        mRm[18].SetOutsideWall(467 + i, true, incH);
      }
      for (int i = 0; i < 9; i++)
      {
        mRm[19].AddWall(i, 477 + i, false);
        mRm[19].SetOutsideWall(477 + i, true, incH);
      }
      mRm[20].AddWall(0, 486, false); mRm[20].SetOutsideWall(486, true, incH);
      mRm[20].AddWall(1, 487, false); mRm[20].SetOutsideWall(487, true, incH);

      //内壁を登録***************************************************************************************
      //1F
      mRm[0].AddWall(0, 20, true); mRm[0].UseAdjacentSpaceFactor(20, false, 0.7);
      mRm[0].AddWall(3, 21, true); mRm[2].AddWall(2, 21, false);
      mRm[0].AddWall(4, 22, true); mRm[2].AddWall(2, 22, false);
      mRm[0].AddWall(4, 23, true); mRm[0].UseAdjacentSpaceFactor(23, false, 0.7);
      mRm[0].AddWall(2, 24, true); mRm[0].UseAdjacentSpaceFactor(24, false, 0.7);
      mRm[1].AddWall(0, 25, true); mRm[1].UseAdjacentSpaceFactor(25, false, 0.7);
      mRm[1].AddWall(3, 26, true); mRm[2].AddWall(2, 26, false);
      mRm[1].AddWall(4, 27, true); mRm[2].AddWall(2, 27, false);
      mRm[1].AddWall(4, 28, true); mRm[2].AddWall(0, 28, false);
      mRm[1].AddWall(2, 29, true); mRm[2].AddWall(0, 29, false);
      mRm[2].AddWall(0, 30, true); mRm[2].UseAdjacentSpaceFactor(30, false, 0.3);
      mRm[2].AddWall(1, 31, true); mRm[2].UseAdjacentSpaceFactor(31, false, 0.3);
      mRm[2].AddWall(1, 32, true); mRm[2].AddWall(2, 32, false);
      mRm[2].AddWall(2, 33, true); mRm[2].UseAdjacentSpaceFactor(33, false, 0.3);
      mRm[0].AddWall(0, 34, true); mRm[0].UseAdjacentSpaceFactor(34, false, 0.3);
      mRm[0].AddWall(1, 35, true); mRm[0].UseAdjacentSpaceFactor(35, false, 0.3);
      mRm[0].AddWall(2, 36, true); mRm[0].UseAdjacentSpaceFactor(36, false, 0.3);
      mRm[0].AddWall(3, 37, true); mRm[0].UseAdjacentSpaceFactor(37, false, 0.3);
      mRm[0].AddWall(4, 38, true); mRm[0].UseAdjacentSpaceFactor(38, false, 0.3);
      mRm[1].AddWall(0, 39, true); mRm[1].UseAdjacentSpaceFactor(39, false, 0.3);
      mRm[1].AddWall(1, 40, true); mRm[1].UseAdjacentSpaceFactor(40, false, 0.3);
      mRm[1].AddWall(2, 41, true); mRm[1].UseAdjacentSpaceFactor(41, false, 0.3);
      mRm[1].AddWall(3, 42, true); mRm[1].UseAdjacentSpaceFactor(42, false, 0.3);
      mRm[1].AddWall(4, 43, true); mRm[1].UseAdjacentSpaceFactor(43, false, 0.3);
      mRm[2].AddWall(0, 44, true); mRm[2].UseAdjacentSpaceFactor(44, false, 0.3);
      mRm[2].AddWall(1, 45, true); mRm[2].UseAdjacentSpaceFactor(45, false, 0.3);
      mRm[2].AddWall(2, 46, true); mRm[2].UseAdjacentSpaceFactor(46, false, 0.3);
      //2-7F
      for (int fl = 1; fl < 7; fl++)
      {
        int ofs = 47 + 70 * (fl - 1);
        int rmN0 = 3 + (fl - 1) * 3;
        int rmN1 = rmN0 + 1;
        int rmN2 = rmN1 + 1;
        mRm[rmN0].AddWall(0, ofs + 31, true); mRm[rmN0].UseAdjacentSpaceFactor(ofs + 31, false, 0.7);
        mRm[rmN0].AddWall(6, ofs + 32, true); mRm[rmN2].AddWall(1, ofs + 32, false);
        mRm[rmN0].AddWall(7, ofs + 33, true); mRm[rmN2].AddWall(1, ofs + 33, false);
        mRm[rmN0].AddWall(8, ofs + 34, true); mRm[rmN2].AddWall(1, ofs + 34, false);
        mRm[rmN0].AddWall(9, ofs + 35, true); mRm[rmN2].AddWall(1, ofs + 35, false);
        mRm[rmN0].AddWall(5, ofs + 36, true); mRm[rmN0].UseAdjacentSpaceFactor(ofs + 36, false, 0.7);
        mRm[rmN1].AddWall(0, ofs + 37, true); mRm[rmN1].UseAdjacentSpaceFactor(ofs + 37, false, 0.7);
        mRm[rmN1].AddWall(5, ofs + 38, true); mRm[rmN2].AddWall(1, ofs + 38, false);
        mRm[rmN1].AddWall(6, ofs + 39, true); mRm[rmN2].AddWall(1, ofs + 39, false);
        mRm[rmN1].AddWall(7, ofs + 40, true); mRm[rmN2].AddWall(1, ofs + 40, false);
        mRm[rmN1].AddWall(8, ofs + 41, true); mRm[rmN2].AddWall(1, ofs + 41, false);
        mRm[rmN2].AddWall(0, ofs + 42, true); mRm[rmN2].AddWall(1, ofs + 42, false);
        mRm[rmN2].AddWall(0, ofs + 43, true); mRm[rmN1].AddWall(8, ofs + 43, false);
        mRm[rmN2].AddWall(0, ofs + 44, true); mRm[rmN1].AddWall(4, ofs + 44, false);
        mRm[rmN0].AddWall(2, ofs + 45, true); mRm[rmN0].AddWall(3, ofs + 45, false);
        mRm[rmN0].AddWall(7, ofs + 46, true); mRm[rmN0].AddWall(8, ofs + 46, false);
        mRm[rmN1].AddWall(2, ofs + 47, true); mRm[rmN1].AddWall(3, ofs + 47, false);
        mRm[rmN1].AddWall(6, ofs + 48, true); mRm[rmN1].AddWall(7, ofs + 48, false);
        //床
        if (fl != 1)
        {
          for (int i = 0; i < 10; i++)
          {
            mRm[rmN0].AddWall(i, ofs + 49 + i, true);
            mRm[rmN0 - 3].AddWall(i, ofs + 49 + i, false);
          }
          for (int i = 0; i < 9; i++)
          {
            mRm[rmN1].AddWall(i, ofs + 59 + i, true);
            mRm[rmN1 - 3].AddWall(i, ofs + 59 + i, false);
          }
          mRm[rmN2].AddWall(0, ofs + 68, true); mRm[rmN2 - 3].AddWall(0, ofs + 68, false);
          mRm[rmN2].AddWall(1, ofs + 69, true); mRm[rmN2 - 3].AddWall(1, ofs + 69, false);
        }
      }
      //1F-2F間の床は対応関係が特殊
      mRm[3].AddWall(0, 96, true); mRm[0].AddWall(0, 96, false);
      mRm[3].AddWall(1, 97, true); mRm[0].AddWall(1, 97, false);
      mRm[3].AddWall(2, 98, true); mRm[0].AddWall(2, 98, false);
      mRm[3].AddWall(3, 99, true); mRm[0].AddWall(3, 99, false);
      mRm[3].AddWall(4, 100, true); mRm[3].UseAdjacentSpaceFactor(100, false, 0.3);
      mRm[3].AddWall(5, 101, true); mRm[3].UseAdjacentSpaceFactor(101, false, 0.3);
      mRm[3].AddWall(6, 102, true); mRm[0].AddWall(3, 102, false);
      mRm[3].AddWall(7, 103, true); mRm[0].AddWall(4, 103, false);
      mRm[3].AddWall(8, 104, true); mRm[0].AddWall(4, 104, false);
      mRm[3].AddWall(9, 105, true); mRm[3].UseAdjacentSpaceFactor(105, false, 0.3);
      mRm[4].AddWall(0, 106, true); mRm[1].AddWall(0, 106, false);
      mRm[4].AddWall(1, 107, true); mRm[1].AddWall(1, 107, false);
      mRm[4].AddWall(2, 108, true); mRm[1].AddWall(2, 108, false);
      mRm[4].AddWall(3, 109, true); mRm[2].AddWall(0, 109, false);
      mRm[4].AddWall(4, 110, true); mRm[4].UseAdjacentSpaceFactor(110, false, 0.3);
      mRm[4].AddWall(5, 111, true); mRm[1].AddWall(1, 111, false);
      mRm[4].AddWall(6, 112, true); mRm[1].AddWall(2, 112, false);
      mRm[4].AddWall(7, 113, true); mRm[2].AddWall(0, 113, false);
      mRm[4].AddWall(8, 114, true); mRm[4].UseAdjacentSpaceFactor(114, false, 0.3);
      mRm[5].AddWall(0, 115, true); mRm[2].AddWall(1, 115, false);
      mRm[5].AddWall(1, 116, true); mRm[2].AddWall(2, 116, false);

      //窓を登録***************************************************************************************
      //1F
      mRm[0].AddWindow(0, 0);
      mRm[0].AddWindow(0, 1);
      mRm[0].AddWindow(1, 2);
      mRm[0].AddWindow(2, 3);
      mRm[1].AddWindow(0, 0);
      mRm[1].AddWindow(0, 1);
      mRm[1].AddWindow(1, 2);
      mRm[1].AddWindow(2, 3);
      mRm[2].AddWindow(0, 0);
      //2-7F
      for (int fl = 1; fl < 7; fl++)
      {
        int rmN = 3 + (fl - 1) * 3;
        mRm[rmN].AddWindow(0, 0);
        mRm[rmN].AddWindow(0, 1);
        mRm[rmN].AddWindow(1, 2);
        mRm[rmN].AddWindow(2, 3);
        mRm[rmN].AddWindow(3, 4);
        mRm[rmN].AddWindow(4, 5);
        mRm[rmN].AddWindow(5, 6);
        mRm[rmN].AddWindow(5, 7);
        mRm[rmN + 1].AddWindow(0, 0);
        mRm[rmN + 1].AddWindow(0, 1);
        mRm[rmN + 1].AddWindow(1, 2);
        mRm[rmN + 1].AddWindow(2, 3);
        mRm[rmN + 1].AddWindow(3, 4);
        mRm[rmN + 1].AddWindow(4, 5);
      }

      //ペリメータ床に短波長優先配分
      const double SW_RATE_TO_FLOOR = 0.7;
      //1F
      mRm[0].SetSWDistributionRateToFloor(0, 34, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(1, 34, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(2, 35, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(3, 36, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(0, 39, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(1, 39, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(2, 40, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(3, 41, true, SW_RATE_TO_FLOOR);
      mRm[2].SetSWDistributionRateToFloor(0, 44, true, SW_RATE_TO_FLOOR);
      //2-7F
      for (int fl = 1; fl < 7; fl++)
      {
        int ofs = 47 + 70 * (fl - 1);
        int rmN = 3 + (fl - 1) * 3;
        mRm[rmN].SetSWDistributionRateToFloor(0, ofs + 49, true, SW_RATE_TO_FLOOR);
        mRm[rmN].SetSWDistributionRateToFloor(1, ofs + 49, true, SW_RATE_TO_FLOOR);
        mRm[rmN].SetSWDistributionRateToFloor(2, ofs + 50, true, SW_RATE_TO_FLOOR);
        mRm[rmN].SetSWDistributionRateToFloor(3, ofs + 51, true, SW_RATE_TO_FLOOR);
        mRm[rmN].SetSWDistributionRateToFloor(4, ofs + 52, true, SW_RATE_TO_FLOOR);
        mRm[rmN].SetSWDistributionRateToFloor(5, ofs + 53, true, SW_RATE_TO_FLOOR);
        mRm[rmN].SetSWDistributionRateToFloor(6, ofs + 54, true, SW_RATE_TO_FLOOR);
        mRm[rmN].SetSWDistributionRateToFloor(7, ofs + 54, true, SW_RATE_TO_FLOOR);
        mRm[rmN + 1].SetSWDistributionRateToFloor(0, ofs + 59, true, SW_RATE_TO_FLOOR);
        mRm[rmN + 1].SetSWDistributionRateToFloor(1, ofs + 59, true, SW_RATE_TO_FLOOR);
        mRm[rmN + 1].SetSWDistributionRateToFloor(2, ofs + 60, true, SW_RATE_TO_FLOOR);
        mRm[rmN + 1].SetSWDistributionRateToFloor(3, ofs + 61, true, SW_RATE_TO_FLOOR);
        mRm[rmN + 1].SetSWDistributionRateToFloor(4, ofs + 62, true, SW_RATE_TO_FLOOR);
        mRm[rmN + 1].SetSWDistributionRateToFloor(5, ofs + 63, true, SW_RATE_TO_FLOOR);
      }

      //建物モデルの作成
      BuildingThermalModel bModel = new BuildingThermalModel(mRm);
      bModel.TimeStep = 3600;

      //ゾーン間換気の設定
      const double cvRate = 150d * 1.2 / 3600d;
      //1F
      bModel.SetCrossVentilation(0, 0, 0, 1, 4.0 * cvRate);
      bModel.SetCrossVentilation(0, 0, 0, 3, 10.0 * cvRate);
      bModel.SetCrossVentilation(0, 1, 0, 3, 9.75 * cvRate);
      bModel.SetCrossVentilation(0, 1, 0, 2, 4.0 * cvRate);
      bModel.SetCrossVentilation(0, 3, 0, 4, 10.0 * cvRate);
      bModel.SetCrossVentilation(0, 2, 0, 4, 13.0 * cvRate);
      bModel.SetCrossVentilation(1, 0, 1, 1, 4.0 * cvRate);
      bModel.SetCrossVentilation(1, 0, 1, 3, 10.0 * cvRate);
      bModel.SetCrossVentilation(1, 1, 1, 3, 9.75 * cvRate);
      bModel.SetCrossVentilation(1, 1, 1, 2, 4.0 * cvRate);
      bModel.SetCrossVentilation(1, 3, 1, 4, 10.0 * cvRate);
      bModel.SetCrossVentilation(1, 2, 1, 4, 6.5 * cvRate);
      bModel.SetCrossVentilation(2, 0, 2, 2, 10.0 * cvRate);
      bModel.SetCrossVentilation(2, 1, 2, 2, 6.5 * cvRate);
      //2-7F
      for (int fl = 1; fl < 7; fl++)
      {
        int rmN = 3 + (fl - 1) * 3;
        bModel.SetCrossVentilation(rmN, 0, rmN, 1, 4.0 * cvRate);
        bModel.SetCrossVentilation(rmN, 1, rmN, 2, 4.0 * cvRate);
        bModel.SetCrossVentilation(rmN, 2, rmN, 3, 4.0 * cvRate);
        bModel.SetCrossVentilation(rmN, 3, rmN, 4, 4.0 * cvRate);
        bModel.SetCrossVentilation(rmN, 4, rmN, 5, 4.0 * cvRate);
        bModel.SetCrossVentilation(rmN, 0, rmN, 6, 10.0 * cvRate);
        bModel.SetCrossVentilation(rmN, 6, rmN, 7, 10.0 * cvRate);
        bModel.SetCrossVentilation(rmN, 7, rmN, 8, 10.0 * cvRate);
        bModel.SetCrossVentilation(rmN, 8, rmN, 9, 10.0 * cvRate);
        bModel.SetCrossVentilation(rmN, 9, rmN, 5, 10.0 * cvRate);
        bModel.SetCrossVentilation(rmN, 1, rmN, 6, 9.75 * cvRate);
        bModel.SetCrossVentilation(rmN, 2, rmN, 7, 6.5 * cvRate);
        bModel.SetCrossVentilation(rmN, 3, rmN, 8, 10.0 * cvRate);
        bModel.SetCrossVentilation(rmN, 4, rmN, 9, 9.75 * cvRate);
        bModel.SetCrossVentilation(rmN + 1, 0, rmN + 1, 1, 4.0 * cvRate);
        bModel.SetCrossVentilation(rmN + 1, 1, rmN + 1, 2, 4.0 * cvRate);
        bModel.SetCrossVentilation(rmN + 1, 2, rmN + 1, 3, 4.0 * cvRate);
        bModel.SetCrossVentilation(rmN + 1, 3, rmN + 1, 4, 4.0 * cvRate);
        bModel.SetCrossVentilation(rmN + 1, 0, rmN + 1, 5, 10.0 * cvRate);
        bModel.SetCrossVentilation(rmN + 1, 5, rmN + 1, 6, 10.0 * cvRate);
        bModel.SetCrossVentilation(rmN + 1, 6, rmN + 1, 7, 10.0 * cvRate);
        bModel.SetCrossVentilation(rmN + 1, 7, rmN + 1, 8, 10.0 * cvRate);
        bModel.SetCrossVentilation(rmN + 1, 1, rmN + 1, 5, 9.75 * cvRate);
        bModel.SetCrossVentilation(rmN + 1, 2, rmN + 1, 6, 6.5 * cvRate);
        bModel.SetCrossVentilation(rmN + 1, 3, rmN + 1, 7, 10.0 * cvRate);
        bModel.SetCrossVentilation(rmN + 1, 4, rmN + 1, 8, 6.5 * cvRate);
      }

      return bModel;
    }

    #endregion

  }
}
