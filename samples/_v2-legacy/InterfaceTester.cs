using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

using System.IO;

using Popolo.Weather;
using Popolo.Webpro;
using Popolo.ThermalLoad;
using Popolo.ThermalLoad.JsonConverters;

namespace PopoloTester
{
  class InterfaceTester
  {

    #region Json書き出しテスト

    private static void testJson()
    {
      BuildingThermalModel btModel1 = makeOfficeBuilding();

      //Json変換
      BuildingThermalModelJson btmJson = new BuildingThermalModelJson(btModel1);

      //書き出しオプション
      var wrOptions = new JsonWriterOptions
      {
        Indented = true, //インデント付
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping //エスケープしすぎない
      };

      //書き出す
      using (FileStream strm = new FileStream("jsonTest1.json", FileMode.Create))
      {
        using (var writer = new Utf8JsonWriter(strm, wrOptions))
        {
          btmJson.Write(writer, btmJson, null);
        }
      }

      //読み込みオプション
      var rdOptions = new JsonSerializerOptions { WriteIndented = true }; //インデント付

      //読み込んで再度書き出す。内容はjsonTest1.jsonに一致するはず
      using (StreamReader sReader = new StreamReader("jsonTest1.json"))
      using (FileStream strm = new FileStream("jsonTest2.json", FileMode.Create))
      {
        string buff = sReader.ReadToEnd();
        ReadOnlySpan<byte> jsonUtf8 = Encoding.UTF8.GetBytes(buff);
        var reader = new Utf8JsonReader(jsonUtf8, true, default);
        btmJson = btmJson.Read(ref reader, typeof(BuildingThermalModelJson), rdOptions);
        BuildingThermalModel btModel2 = btmJson.MakeBuildingThermalModel();

        using (var writer = new Utf8JsonWriter(strm, wrOptions))
        {
          btmJson.Write(writer, new BuildingThermalModelJson(btModel2), null);
        }
      }
    }

    private static BuildingThermalModel makeOfficeBuilding()
    {
      //傾斜面の作成****************************************
      Incline incN = new Incline(Incline.Orientation.N, 0.5 * Math.PI);
      Incline incE = new Incline(Incline.Orientation.E, 0.5 * Math.PI);
      Incline incW = new Incline(Incline.Orientation.W, 0.5 * Math.PI);
      Incline incS = new Incline(Incline.Orientation.S, 0.5 * Math.PI);

      //壁層の作成******************************************
      //外壁
      WallLayer[] exWL = new WallLayer[]
      {
        new WallLayer("タイル", 1.3, 2000, 0.010),
        new WallLayer("モルタル", 1.5, 1600, 0.025),
        new WallLayer("コンクリート", 1.6, 2000, 0.150),
        new WallLayer("押出ポリスチレンフォーム1種", 0.040, 33, 0.025),
        new AirGapLayer("非密閉中空層", false, 0.05),
        new WallLayer("石膏ボード", 0.22, 830, 0.008)
      };
      //内壁1
      WallLayer[] inWL1 = new WallLayer[]
      {
        new WallLayer("石膏ボード", 0.220, 830, 0.012),
        new AirGapLayer("非密閉中空層", false, 0.05),
        new WallLayer("石膏ボード", 0.220, 830, 0.012)
      };
      //内壁2
      WallLayer[] inWL2 = new WallLayer[]
      {
        new WallLayer("石膏ボード", 0.220, 830, 0.012),
        new AirGapLayer("非密閉中空層", false, 0.05),
        new WallLayer("コンクリート", 1.6, 2000, 0.120),
        new AirGapLayer("非密閉中空層", false, 0.05),
        new WallLayer("石膏ボード", 0.220, 830, 0.012)
      };
      //床
      WallLayer[] flWL = new WallLayer[]
      {
        new WallLayer("ビニル系床材", 0.190, 2000, 0.003),
        new AirGapLayer("非密閉中空層", false, 0.05),
        new WallLayer("コンクリート", 1.6, 2000, 0.150),
        new AirGapLayer("非密閉中空層", false, 0.8),
        new WallLayer("石膏ボード", 0.220, 830, 0.009),
        new WallLayer("岩綿吸音板", 0.064, 290, 0.015)
      };

      //壁の作成********************************************
      Wall[] walls = new Wall[]
      {
        new Wall(16 * 4, exWL), //00
        new Wall(15 * 4, exWL),
        new Wall(15 * 4, exWL),
        new Wall(9 * 4, exWL),
        new Wall(9 * 4, exWL),
        new Wall(24 * 4, exWL), //05
        new Wall(9 * 4, inWL1),
        new Wall(7 * 4, inWL2),
        new Wall(8 * 4, inWL2),
        new Wall(8 * 4, inWL2),
        new Wall(15 * 4, inWL2), //10
        new Wall(19 * 4, inWL2),
        new Wall(4 * 4, inWL2),
        new Wall(5 * 4, inWL2),
        new Wall(67.5, flWL),
        new Wall(62.5, flWL), //15
        new Wall(40.0, flWL),
        new Wall(62.5, flWL),
        new Wall(32.5, flWL),
        new Wall(40.0, flWL),
        new Wall(32.5, flWL), //20
        new Wall(107.5, flWL),
        new Wall(76.0, flWL)
      };

      //ゾーンの作成****************************************
      //北側ゾーン
      Zone[] nZones = new Zone[]
      {
        new Zone("NW0", 67.5 * 2.6, 67.5),
        new Zone("NW1", 62.5 * 2.6, 62.5),
        new Zone("NW2", 40.0 * 2.6, 40.0),
        new Zone("NE0", 62.5 * 2.6, 62.5),
        new Zone("NE1", 32.5 * 2.6, 32.5),
        new Zone("NE2", 40.0 * 2.6, 40.0)
      };
      //南側ゾーン
      Zone[] sZones = new Zone[]
      {
        new Zone("S0", 32.5 * 2.6, 32.5),
        new Zone("S1", 107.5 * 2.6, 107.5),
        new Zone("S2", 76.0 * 2.6, 76.0)
      };

      //窓の作成****************************************
      //ガラスの透過率と反射率
      double[] tau = new double[] { 0.7, 0.7 };
      double[] rho = new double[] { 0.1, 0.1 };
      //北側窓
      Window[] nWindows = new Window[]
      {
        new Window(19.38,tau, rho, incW),
        new Window(19.38,tau, rho, incN),
        new Window(19.38,tau, rho, incN),
        new Window(12.92,tau, rho, incE)
      };
      //南側窓
      Window[] sWindows = new Window[]
      {
        new Window(12.92,tau, rho, incW),
        new Window(25.84,tau, rho, incS)
      };
      //日射遮蔽物と日除け
      SimpleShadingDevice rollerShade = new SimpleShadingDevice(SimpleShadingDevice.PredefinedDevices.TranslucentRollerShade);
      VenetianBlind vBlind = new VenetianBlind(25, 20, 0.2, 0.2, 0.7, 0.7);
      SunShade horizontalShade = SunShade.MakeHorizontalSunShade(3.8, 1.7, 0.6, 0.1, new Incline(Incline.Orientation.S, 0.5 * Math.PI));
      for (int i = 0; i < nWindows.Length; i++)  //北側は屋内側のローラーシェードのみ
        nWindows[i].SetShadingDevice(nWindows[i].GlazingNumber, rollerShade);
      for (int i = 0; i < sWindows.Length; i++)  //南側は水平庇と空気層内ブラインドを追加
      {
        sWindows[i].SetShadingDevice(sWindows[i].GlazingNumber, rollerShade);
        sWindows[i].SetShadingDevice(1, vBlind);
        sWindows[i].SunShade = horizontalShade;
      }

      //多数室の作成*************************************
      //北側多数室****
      MultiRooms nMRs = new MultiRooms(2, nZones, walls, nWindows);
      //ゾーンの設定
      nMRs.AddZone(0, 0);
      nMRs.AddZone(0, 1);
      nMRs.AddZone(0, 2);
      nMRs.AddZone(1, 3);
      nMRs.AddZone(1, 4);
      nMRs.AddZone(1, 5);
      //壁の設定
      nMRs.SetOutsideWall(0, true, incW); nMRs.AddWall(0, 0, false); //外壁
      nMRs.SetOutsideWall(1, true, incN); nMRs.AddWall(0, 1, false);
      nMRs.SetOutsideWall(2, true, incN); nMRs.AddWall(1, 2, false);
      nMRs.SetOutsideWall(3, true, incE); nMRs.AddWall(1, 3, false);
      nMRs.AddWall(0, 7, true); //共有壁
      nMRs.UseAdjacentSpaceFactor(8, true, 0.4); nMRs.AddWall(0, 8, false); //隣室温度差
      nMRs.UseAdjacentSpaceFactor(9, true, 0.4); nMRs.AddWall(0, 9, false);
      nMRs.UseAdjacentSpaceFactor(10, true, 0.4); nMRs.AddWall(1, 10, false);
      nMRs.AddWall(0, 0, 14); //床天井循環
      nMRs.AddWall(1, 1, 15);
      nMRs.AddWall(2, 2, 16);
      nMRs.AddWall(3, 3, 17);
      nMRs.AddWall(4, 4, 18);
      nMRs.AddWall(5, 5, 19);
      //窓の追加
      nMRs.AddWindow(0, 0);
      nMRs.AddWindow(0, 1);
      nMRs.AddWindow(1, 2);
      nMRs.AddWindow(1, 3);
      //ゾーン間換気//同一室内の隣接ゾーンは気積1回相当
      nMRs.SetCrossVentilation(0, 1, nZones[0].AirMass * 1.0 / 3600);
      nMRs.SetCrossVentilation(0, 2, nZones[0].AirMass * 1.0 / 3600);
      nMRs.SetCrossVentilation(1, 2, nZones[1].AirMass * 1.0 / 3600);
      nMRs.SetCrossVentilation(3, 4, nZones[3].AirMass * 1.0 / 3600);
      nMRs.SetCrossVentilation(3, 5, nZones[3].AirMass * 1.0 / 3600);
      nMRs.SetCrossVentilation(4, 5, nZones[4].AirMass * 1.0 / 3600);
      //南側多数室****
      MultiRooms sMRs = new MultiRooms(1, sZones, walls, sWindows);
      //ゾーンの設定
      sMRs.AddZone(0, 0);
      sMRs.AddZone(0, 1);
      sMRs.AddZone(0, 2);
      //壁の設定
      sMRs.SetOutsideWall(4, true, incW); sMRs.AddWall(0, 0, false); //外壁
      sMRs.SetOutsideWall(5, true, incS); sMRs.AddWall(1, 1, false);
      sMRs.AddWall(0, 7, false); //共有壁
      sMRs.UseAdjacentSpaceFactor(11, true, 0.4); sMRs.AddWall(2, 11, false); //隣室温度差
      sMRs.UseAdjacentSpaceFactor(12, true, 0.4); sMRs.AddWall(2, 12, false);
      sMRs.UseAdjacentSpaceFactor(13, true, 0.4); sMRs.AddWall(1, 13, false);
      sMRs.AddWall(0, 0, 20); //床天井循環
      sMRs.AddWall(1, 1, 21);
      sMRs.AddWall(2, 2, 22);
      //窓の追加
      sMRs.AddWindow(0, 0);
      sMRs.AddWindow(0, 1);
      //ゾーン間換気//同一室内の隣接ゾーンは気積1回相当
      sMRs.SetCrossVentilation(0, 1, nZones[0].AirMass * 1.0 / 3600);
      sMRs.SetCrossVentilation(0, 2, nZones[0].AirMass * 1.0 / 3600);
      sMRs.SetCrossVentilation(1, 2, nZones[1].AirMass * 1.0 / 3600);

      //BuildingThermalModelの作成
      BuildingThermalModel btModel = new BuildingThermalModel(new MultiRooms[] { nMRs, sMRs });
      Sun sun = new Sun(Sun.City.Tokyo);
      DateTime dTime = new DateTime(1980, 6, 14, 0, 0, 0);
      btModel.UpdateOutdoorCondition(dTime, sun, 26, 0.02, 0);
      return btModel;
    }

    #endregion

    #region Webproテスト

    private static void testWebpro()
    {
      //WEBPRO入力シート（JSON化済）を読み込む
      BuildingThermalModel bModel;
      var rdOptions = new JsonSerializerOptions { WriteIndented = true }; //インデント付
      using (StreamReader sReader = new StreamReader("sample.json"))
      {
        string buff = sReader.ReadToEnd();
        ReadOnlySpan<byte> jsonUtf8 = Encoding.UTF8.GetBytes(buff);
        var reader = new Utf8JsonReader(jsonUtf8, true, default);
        Popolo.Webpro.JsonConverters.WebproModelJson wModel = new Popolo.Webpro.JsonConverters.WebproModelJson();
        wModel = wModel.Read(ref reader, typeof(Popolo.Webpro.JsonConverters.WebproModelJson), rdOptions);
        bModel = wModel.MakeBuildingThermalModel();
        bModel.InitializeAirState(20, 0);
      }

      //外気条件を作成
      RandomWeather rWet = new RandomWeather(100, RandomWeather.Location.Tokyo);
      rWet.MakeWeather(1, out double[] dbTemp, out double[] hmdRatio, out double[] radiation, out bool[] isFair);

      //室リストを取得
      ImmutableZone[] zones = bModel.GetZones();

      DateTime dTime = new DateTime(2015, 1, 1, 0, 0, 0); // 1/1が木曜日の年とする
      Sun sun = new Sun(Sun.City.Tokyo);
      bModel.SetGroundTemperature(20); //地中温度は20度で一定
      using (StreamWriter sWriter = new StreamWriter("webproTest.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        //タイトル行
        sWriter.Write("日時");
        for (int i = 0; i < zones.Length; i++) sWriter.Write("," + zones[i].Name + "_乾球温度[C]");
        for (int i = 0; i < zones.Length; i++) sWriter.Write("," + zones[i].Name + "_絶対湿度[g/kg]");
        for (int i = 0; i < zones.Length; i++) sWriter.Write("," + zones[i].Name + "_顕熱負荷[kW]");
        for (int i = 0; i < zones.Length; i++) sWriter.Write("," + zones[i].Name + "_潜熱負荷[kW]");
        sWriter.WriteLine();

        //計算実行+書き出し
        for (int i = 0; i < dbTemp.Length; i++)
        {
          if (dTime.Hour == 0)
            Console.WriteLine(dTime.ToString("MM/dd HH:mm"));

          //外気条件設定（直散分離）
          sun.Update(dTime);
          sun.SeparateGlobalHorizontalRadiation(radiation[i], Sun.SeparationMethod.Erbs);
          bModel.UpdateOutdoorCondition(dTime, sun, dbTemp[i], 0.001 * hmdRatio[i], 100);

          //室温湿度制御
          for (int j = 0; j < bModel.MultiRoom[0].Zones.Length; j++)
          {
            Popolo.Webpro.WebproHeatGainScheduler sch =
              (Popolo.Webpro.WebproHeatGainScheduler)bModel.MultiRoom[0].Zones[j].GetHeatGains()[0];
            sch.ControlACSystem((Zone)bModel.MultiRoom[0].Zones[j]);
          }

          //負荷計算
          bModel.ForecastHeatTransfer();
          bModel.ForecastWaterTransfer();
          bModel.FixState();

          //書き出し
          sWriter.Write(dTime.ToString("MM/dd HH:mm"));
          for (int j = 0; j < zones.Length; j++) sWriter.Write("," + zones[j].Temperature);
          for (int j = 0; j < zones.Length; j++) sWriter.Write("," + zones[j].HumidityRatio * 1000d);
          for (int j = 0; j < zones.Length; j++) sWriter.Write("," + zones[j].HeatSupply * 0.001);
          for (int j = 0; j < zones.Length; j++) sWriter.Write("," + zones[j].WaterSupply * 2.5d);
          sWriter.WriteLine();

          //時刻を進める
          dTime = dTime.AddHours(1);
        }
      }
    }

    #endregion

  }
}
