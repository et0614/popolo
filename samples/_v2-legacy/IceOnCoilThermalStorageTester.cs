using System;

using Popolo.HVAC.ThermalStorage;

namespace PopoloTester
{
  /// <summary>IceOnCoilThermalStorageの試験クラス</summary>
  /// <remarks>日本BACのカタログを使い、TSU-122MAとTSC-128MAを組み合わせてパラメータを推定</remarks>
  internal class IceOnCoilThermalStorageTester
  {
    // すべてのテストを実行する
    public static void RunAllTests()
    {
      Console.WriteLine("--- Test_FullCycle_And_HeatBalance ---");
      Test_FullCycle_And_HeatBalance();

      Console.WriteLine("\n--- Test_SubZeroIceCooling ---");
      Test_SubZeroIceCooling();

      Console.WriteLine("\n--- Test_PartialMelt_Then_Freeze ---");
      Test_PartialMelt_Then_Freeze();

      Console.WriteLine("\n--- Test_BubblingEffect ---");
      Test_BubblingEffect();

      Console.WriteLine("\n--- Test_AmbientHeatLoss_Soak ---");
      Test_AmbientHeatLoss_Soak();

      Console.WriteLine("\n--- Test_DailyCycle_With_HeatLoss ---");
      Test_DailyCycle_With_HeatLoss();

      Console.WriteLine("\nAll tests passed!");
    }

    /// <summary>
    /// シナリオ1：完全な凍結・融解サイクルと熱収支のテスト
    /// </summary>
    private static void Test_FullCycle_And_HeatBalance()
    {
      var storage = new IceOnCoilThermalStorage(7.57, 20, 14 * 4.116, 0.0260, 0.0272);
      storage.HeatLossCoefficient = 0; //テストのため熱損失を無視
      storage.Initialize(5);
      storage.TimeStep = 600; // テスト時間を短縮するためにタイムステップを大きくする
      double totalHeat = 0;

      // 40時間冷却
      for (int i = 0; i < 40 * 6; i++)
      {
        storage.Update(-5, 2.5);
        totalHeat += storage.HeatTransferToCoil * (storage.TimeStep / 3600); // kWhに換算
      }

      // アサーション：完全に凍結したか
      if (storage.GetIcePackingFactor() < 0.99)
        throw new Exception("Test Failed: 完全に凍結しません。IPF=" + storage.GetIcePackingFactor());

      // 40時間加熱
      for (int i = 0; i < 40 * 6; i++)
      {
        storage.Update(5, 2.5);
        totalHeat += storage.HeatTransferToCoil * (storage.TimeStep / 3600);
      }

      // アサーション：熱収支がほぼゼロか
      if (Math.Abs(totalHeat) > 1.0) // 1kWh以上の誤差は許容しない
        throw new Exception("Test Failed: 熱収支が合ません。Balance=" + totalHeat);

      Console.WriteLine("Heat balance: " + totalHeat.ToString("F2") + " kWh. OK.");
    }

    /// <summary>
    /// シナリオ2：過冷却氷のテスト
    /// </summary>
    private static void Test_SubZeroIceCooling()
    {
      var storage = new IceOnCoilThermalStorage(7.57, 20, 14 * 4.116, 0.0260, 0.0272);
      storage.TimeStep = 600;

      // 完全に凍らせる
      while (storage.GetIcePackingFactor() < 0.99)
      {
        storage.Update(-5, 2.5);
      }

      // さらに冷却を続ける
      storage.Update(-5, 2.5);
      storage.Update(-5, 2.5);

      // アサーション：氷温度がマイナスになったか
      double avgTemp = storage.GetAverageWaterIceTemperature();
      if (avgTemp >= 0)
        throw new Exception("Test Failed: 氷の過冷却に失敗しました。Temp=" + avgTemp);

      Console.WriteLine("Sub-zero ice temperature: " + avgTemp.ToString("F2") + " C. OK.");
    }

    /// <summary>
    /// シナリオ3：部分融解からの再凍結テスト
    /// </summary>
    private static void Test_PartialMelt_Then_Freeze()
    {
      var storage = new IceOnCoilThermalStorage(7.57, 20, 14 * 4.116, 0.0260, 0.0272);
      storage.TimeStep = 600;

      // 1. 完全に凍らせる
      while (storage.GetIcePackingFactor() < 0.99)
      {
        storage.Update(-5, 2.5);
      }

      // 2. 部分的に融解させる
      storage.Update(5, 2.5);
      storage.Update(5, 2.5);

      // 3. 再凍結させる
      storage.Update(-5, 2.5);

      // 検証：例外が発生せず、この行に到達すれば基本的な動作はOK
      Console.WriteLine("Re-freezing process completed without exceptions. OK.");
      // より厳密には、internalのiceInnerDiametersをテストプロジェクトから見えるようにして、
      // その値がPipeOuterDiameterと一致することを検証する。
    }

    /// <summary>
    /// シナリオ4：バブリング効果の検証テスト
    /// </summary>
    private static void Test_BubblingEffect()
    {
      // A: バブリングなし
      var storageA = new IceOnCoilThermalStorage(7.57, 20, 14 * 4.116, 0.0260, 0.0272);
      storageA.Initialize(5);
      storageA.IsBubbling = false;

      // B: バブリングあり
      var storageB = new IceOnCoilThermalStorage(7.57, 20, 14 * 4.116, 0.0260, 0.0272);
      storageB.Initialize(5);
      storageB.IsBubbling = true;

      // 10時間、製氷を行う
      for (int i = 0; i < 10 * 6; i++)
      {
        storageA.Update(-5, 2.5);
        storageB.Update(-5, 2.5);
      }

      double ipfA = storageA.GetIcePackingFactor();
      double ipfB = storageB.GetIcePackingFactor();

      Console.WriteLine($"IPF without bubbling: {ipfA:F3}");
      Console.WriteLine($"IPF with bubbling:    {ipfB:F3}");

      // 検証
      if (ipfB <= ipfA)
        throw new Exception("Test Failed: バブリングによる性能向上が見られませんでした。");

      Console.WriteLine("Bubbling enhances ice making performance. OK.");
    }

    /// <summary>
    /// シナリオ5：周囲環境からの熱損失（侵入）による融解テスト
    /// </summary>
    private static void Test_AmbientHeatLoss_Soak()
    {
      var storage = new IceOnCoilThermalStorage(7.57, 20, 14 * 4.116, 0.0260, 0.0272);
      storage.TimeStep = 600;

      // 1. 完全に凍らせ、-2℃程度にする
      while (storage.GetAverageWaterIceTemperature() > -2.0)
      {
        storage.Update(-5, 2.5);
      }
      double initialIpf = storage.GetIcePackingFactor();
      Console.WriteLine($"Initial state: Temp={storage.GetAverageWaterIceTemperature():F2}C, IPF={initialIpf:F3}");

      // 2. 熱損失あり、周囲温度25℃の条件で放置する
      storage.HeatLossCoefficient = 10; // 熱損失係数[W/K]を設定
      storage.AmbientTemperature = 25;  // 周囲温度[C]を設定

      // 3. ブライン流量ゼロで100時間放置
      double totalLoss = 0;
      for (int i = 0; i < 100 * 6; i++)
      {
        storage.Update(0, 0); // 温度は任意、流量はゼロ
        totalLoss += storage.HeatLoss * (storage.TimeStep / 3600); // kWhに換算
      }

      double finalIpf = storage.GetIcePackingFactor();
      Console.WriteLine($"Final state: Temp={storage.GetAverageWaterIceTemperature():F2}C, IPF={finalIpf:F3}");
      Console.WriteLine($"Heat gain from ambient: {storage.HeatLoss:F3} kW");
      Console.WriteLine($"Total heat gain from ambient: {totalLoss:F3} kWh");

      // 検証：熱損失によって氷が融けた（IPFが減少した）か
      if (finalIpf >= initialIpf)
      {
        throw new Exception("Test Failed: 周囲からの熱侵入による融解が確認できませんでした。");
      }
      Console.WriteLine("Ambient heat gain correctly melts the ice. OK.");
    }

    /// <summary>
    /// シナリオ6：熱損失を考慮した24時間運用サイクルテスト
    /// </summary>
    private static void Test_DailyCycle_With_HeatLoss()
    {
      var storage = new IceOnCoilThermalStorage(7.57, 20, 14 * 4.116, 0.0260, 0.0272);
      storage.TimeStep = 600;
      storage.HeatLossCoefficient = 11; // 熱損失係数[W/K]を設定（50mm断熱材で表面積14m2ほど）
      storage.AmbientTemperature = 25;  // 周囲温度[C]を設定
      storage.Initialize(20); // 初期水温

      Console.WriteLine("Test started. Initial Temp: 20.0 C, IPF: 0.000");

      // 1. 夜間（8時間）製氷
      for (int i = 0; i < 8 * 6; i++) storage.Update(-8, 2.5);
      double ipf_after_charge = storage.GetIcePackingFactor();
      Console.WriteLine($"After 8h charging:  Temp={storage.GetAverageWaterIceTemperature():F2}C, IPF={ipf_after_charge:F3}");

      // 検証：氷が作られているか
      if (ipf_after_charge < 0.5) // 例：最低でも50%は凍っているはず
        throw new Exception("Test Failed: 夜間運転で十分に製氷されませんでした。");

      // 2. 午前（4時間）放置
      for (int i = 0; i < 4 * 6; i++) storage.Update(0, 0);
      double ipf_after_soak1 = storage.GetIcePackingFactor();
      Console.WriteLine($"After 4h soaking:    Temp={storage.GetAverageWaterIceTemperature():F2}C, IPF={ipf_after_soak1:F3}");

      // 検証：放置中に氷が融けているか
      if (ipf_after_soak1 >= ipf_after_charge)
        throw new Exception("Test Failed: 放置中の熱侵入による融解が確認できませんでした。");

      // 3. 昼間（8時間）融解
      for (int i = 0; i < 8 * 6; i++) storage.Update(10, 2.0);
      double ipf_after_discharge = storage.GetIcePackingFactor();
      Console.WriteLine($"After 8h discharging:Temp={storage.GetAverageWaterIceTemperature():F2}C, IPF={ipf_after_discharge:F3}");

      // 検証：氷がほぼ無くなっているか
      if (ipf_after_discharge > 0.01)
        throw new Exception("Test Failed: 昼間運転で氷が融けきりませんでした。");

      Console.WriteLine("Daily cycle simulation behaved as expected. OK.");
    }
  }
}
