using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Popolo.Webpro;
using Popolo.Webpro.JsonConverters;
using Popolo.ThermalLoad;
using Popolo.Weather;

using System.Text.Json;
using System.IO;

namespace PopoloTester
{
  internal class WebproConvertTester
  {


    public static void TestWebproConvert()
    {
      // JSONファイルを読み込む****************************
      string jsonString = "";
      //using (StreamReader sReader = new StreamReader("builelib_input.json"))
      using (StreamReader sReader = new StreamReader("input.json"))
      {
        jsonString = sReader.ReadToEnd();
      }
      var rdOptions = new JsonSerializerOptions { WriteIndented = true }; //インデント付
      ReadOnlySpan<byte> jsonUtf8 = Encoding.UTF8.GetBytes(jsonString);
      var reader = new Utf8JsonReader(jsonUtf8, true, default);

      // Popoloの建物モデルに変換**************************
      WebproModelJson wModel = new WebproModelJson();
      wModel = wModel.Read(ref reader, typeof(WebproModelJson), rdOptions);
      int regionNumber = wModel.RegionNumber;
      BuildingThermalModel model = wModel.MakeBuildingThermalModel();
      model.InitializeAirState(20, 0); //モデル内の温度を20度に初期化
      model.SetGroundTemperature(20);  //地中温度は20度で一定と仮定
      ImmutableZone[] zones = model.GetZones(); //室リストを取得

      // Webproの地域に合わせて気象データを自動生成********
      // 省エネ法の8地域をPopoloの5地域に変換
      RandomWeather rWet; //気象データ生成クラス
      Sun sun; //太陽
      switch (regionNumber)
      {
        case 1:
        case 2:
          rWet = new RandomWeather(100, RandomWeather.Location.Sapporo);
          sun = new Sun(43 + 3d / 60d, 141 + 20d / 60d, 135d);
          break;
        case 3:
        case 4:
          rWet = new RandomWeather(100, RandomWeather.Location.Sendai);
          sun = new Sun(38 + 16d / 60d, 140 + 52d / 60d, 135d);
          break;
        case 5:
        case 6:
          rWet = new RandomWeather(100, RandomWeather.Location.Tokyo);
          sun = new Sun(Sun.City.Tokyo);
          break;
        case 7:
          rWet = new RandomWeather(100, RandomWeather.Location.Fukuoka);
          sun = new Sun(33 + 35d / 60d, 130 + 24d / 60d, 135d);
          break;
        case 8:
          rWet = new RandomWeather(100, RandomWeather.Location.Naha);
          sun = new Sun(26 + 12d / 60d, 127 + 40d / 60d, 135d);
          break;
        default:
          rWet = new RandomWeather(100, RandomWeather.Location.Tokyo);
          sun = new Sun(Sun.City.Tokyo);
          break;
      }
      rWet.MakeWeather(1, out double[] dbTemp, out double[] hmdRatio, out double[] radiation, out bool[] isFair);


      // 年間計算実行**************************      
      DateTime dTime = new DateTime(2015, 1, 1, 0, 0, 0); // Webproのスケジュールに合わせて1月1日が木曜日である2015年を使う
      using (StreamWriter swResult = new StreamWriter("webproConvertResult.csv", false, Encoding.UTF8))
      {
        //タイトル行
        swResult.Write("日時");
        for (int i = 0; i < zones.Length; i++) swResult.Write("," + zones[i].Name + "_乾球温度[C]");
        for (int i = 0; i < zones.Length; i++) swResult.Write("," + zones[i].Name + "_絶対湿度[g/kg]");
        for (int i = 0; i < zones.Length; i++) swResult.Write("," + zones[i].Name + "_顕熱負荷[kW]");
        for (int i = 0; i < zones.Length; i++) swResult.Write("," + zones[i].Name + "_潜熱負荷[kW]");
        swResult.WriteLine();

        // 計算実行+書き出し
        for (int i = 0; i < dbTemp.Length; i++)
        {
          if (dTime.Hour == 0) Console.WriteLine(dTime.ToString("MM/dd"));

          // 外気条件設定（直散分離）
          sun.Update(dTime);
          sun.SeparateGlobalHorizontalRadiation(radiation[i], Sun.SeparationMethod.Erbs);
          model.UpdateOutdoorCondition(dTime, sun, dbTemp[i], 0.001 * hmdRatio[i], 100);

          // WEBPROのスケジュールに合わせて室温湿度制御
          for (int j = 0; j < model.MultiRoom[0].Zones.Length; j++)
          {
            WebproHeatGainScheduler sch = (WebproHeatGainScheduler)model.MultiRoom[0].Zones[j].GetHeatGains()[0];
            sch.ControlACSystem((Zone)model.MultiRoom[0].Zones[j]);
          }

          // 負荷計算
          model.ForecastHeatTransfer();
          model.ForecastWaterTransfer();
          model.FixState();

          // 書き出し
          swResult.Write(dTime.ToString("MM/dd HH:mm"));
          for (int j = 0; j < zones.Length; j++) swResult.Write("," + zones[j].Temperature.ToString("F2"));
          for (int j = 0; j < zones.Length; j++) swResult.Write("," + (zones[j].HumidityRatio * 1000d).ToString("F2"));
          for (int j = 0; j < zones.Length; j++) swResult.Write("," + (zones[j].HeatSupply * 0.001).ToString("F3"));
          for (int j = 0; j < zones.Length; j++) swResult.Write("," + (zones[j].WaterSupply * 2.5d).ToString("F3"));
          swResult.WriteLine();

          //時刻を進める
          dTime = dTime.AddHours(1);
        }
      }

    }
    

  }
}
