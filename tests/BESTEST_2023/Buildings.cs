/* Buildings.cs
 *
 * Std 140-2023 Section 7 (Building Thermal Envelope and Fabric Load) のための
 * 建物・壁・窓ファクトリ群。旧 ASHRAE 140 BESTEST 由来 (低/重量2系統)。
 * ケース識別は <see cref="TestCase"/> [Flags] enum で表現し、各ファクトリは
 * フラグの合成で差分仕様を切り替える。日本独自仕様 (旧 C900_J1_1〜J3) は除外。
 */

using System;

using Popolo.Core.Building;
using Popolo.Core.Building.Envelope;
using Popolo.Core.Climate;
using Popolo.Core.Physics;

namespace BESTEST_2023
{
  /// <summary>
  /// Std 140-2023 Section 7 用の建物 / 壁 / 窓ファクトリ群。
  /// 旧 ASHRAE 140-2017 BESTEST 系のケース定義から日本独自拡張を除外したもの。
  /// </summary>
  internal static class Buildings
  {
    #region 定数

    private static readonly Incline INC_N = new Incline(Incline.Orientation.N, 0.5 * Math.PI);
    private static readonly Incline INC_E = new Incline(Incline.Orientation.E, 0.5 * Math.PI);
    private static readonly Incline INC_W = new Incline(Incline.Orientation.W, 0.5 * Math.PI);
    private static readonly Incline INC_S = new Incline(Incline.Orientation.S, 0.5 * Math.PI);
    private static readonly Incline INC_H = new Incline(Incline.Orientation.N, 0);
    /// <summary>水平面下向き (raised floor の F 側)。tilt=π で fs_to_sky=0、ground albedo の入射のみ。</summary>
    private static readonly Incline INC_F = new Incline(Incline.Orientation.N, Math.PI);

    //屋外側対流熱伝達率 (Std 140-2023 Table 7-7 既定; 風速 4.02 m/s 想定の年平均値)
    public const double AO_WALL = 11.9;    // h_conv,ext walls
    public const double AO_WINDOW = 8.0;   // h_conv,ext windows
    public const double AO_FLOOR = 0.8;    // h_conv,ext raised floor (sheltered)
    public const double AO_ROOF = 14.4;    // h_conv,ext roof
    //屋内側対流熱伝達率
    const double AI_WALL = 2.2;     // h_conv,int walls / floor
    const double AI_WINDOW = 2.4;   // h_conv,int windows
    const double AI_FLOOR = 2.2;    // h_conv,int floor
    const double AI_CEILING = 1.8;  // h_conv,int ceiling


    /// <summary>BESTEST2023では天空温度が提供されており夜間放射が計算可能</summary>
    public const bool NO_NOC_RAD = false;

    /// <summary>BESTEST 標準条件における空気密度 [kg/m³]。</summary>
    public const double AIR_DNS = PhysicsConstants.NominalMoistAirDensity * (1.0156 / 1.2255);

    #endregion

    #region TestCase 列挙

    /// <summary>BESTEST テストケース識別子。複数の差分仕様を [Flags] で合成する。</summary>
    [Flags]
    public enum TestCase : long
    {
      None = 0,
      C195 = 1,
      C200 = C195 * 2,
      C210 = C200 * 2,
      C215 = C210 * 2,
      C220 = C215 * 2,
      C230 = C220 * 2,
      C240 = C230 * 2,
      C250 = C240 * 2,
      C270 = C250 * 2,
      C280 = C270 * 2,
      C290 = C280 * 2,
      C300 = C290 * 2,
      C310 = C300 * 2,
      C320 = C310 * 2,
      C395 = C320 * 2,
      C400 = C395 * 2,
      C410 = C400 * 2,
      C420 = C410 * 2,
      C430 = C420 * 2,
      C440 = C430 * 2,
      C600 = C440 * 2,
      C610 = C600 * 2,
      C620 = C610 * 2,
      C630 = C620 * 2,
      C640 = C630 * 2,
      C650 = C640 * 2,
      C800 = C650 * 2,
      C810 = C800 * 2,
      C900 = C810 * 2,
      C910 = C900 * 2,
      C920 = C910 * 2,
      C930 = C920 * 2,
      C940 = C930 * 2,
      C950 = C940 * 2,
      C960 = C950 * 2,
      C990 = C960 * 2,
      C600FF = C990 * 2,
      C650FF = C600FF * 2,
      C900FF = C650FF * 2,
      C950FF = C900FF * 2,

      // Std 140-2020 で追加された新ケース
      C660 = C950FF * 2,    // low-e Argon 窓 (low-mass, base = 600)
      C670 = C660 * 2,      // 単板窓 (low-mass, base = 600)
      C680 = C670 * 2,      // 断熱増強 (low-mass, base = 600)
      C685 = C680 * 2,      // "20,20" Tstat (low-mass, base = 600)
      C695 = C685 * 2,      // 断熱増強 + "20,20" Tstat (low-mass, base = 680)
      C980 = C695 * 2,      // 断熱増強 (high-mass, base = 900)
      C985 = C980 * 2,      // "20,20" Tstat (high-mass, base = 900)
      C995 = C985 * 2,      // 断熱増強 + "20,20" Tstat (high-mass, base = 980)
      C680FF = C995 * 2,    // 自由温度 + 断熱増強 (low-mass, base = 680)
      C980FF = C680FF * 2,  // 自由温度 + 断熱増強 (high-mass, base = 980)
      C450 = C980FF * 2,    // 一定表面熱伝達係数 (内+外, base = 600)
      C460 = C450 * 2,      // 一定表面熱伝達係数 (内のみ, base = 600)
      C470 = C460 * 2,      // 一定表面熱伝達係数 (外のみ, base = 600)

      // Composite flags (集合演算で差分仕様を引く)
      ControlBangBang = C195 | C200 | C210 | C215 | C220 | C230 | C240 | C250
                      | C270 | C280 | C290 | C300 | C310,
      ControlDeadBand = C320 | C395 | C400 | C410 | C420 | C430 | C440
                      | C600 | C610 | C620 | C630
                      | C800 | C810 | C900 | C910 | C920 | C930 | C960 | C990
                      | C660 | C670 | C680 | C980 | C450 | C460 | C470,
      ControlSetBack = C640 | C940,
      ControlVenting = C650 | C950 | C650FF | C950FF,
      ControlNone = C600FF | C650FF | C900FF | C950FF | C680FF | C980FF,
      ControlTight20 = C685 | C985 | C695 | C995,    // "20,20" Tstat (heat<20, cool>20)
      HeavyWeight = C800 | C810 | C900 | C910 | C920 | C930 | C940 | C950
                  | C900FF | C950FF | C990
                  | C980 | C985 | C995 | C980FF,
      HasHeatGain = C240 | C420 | C430 | C440 | C600 | C610 | C620 | C630 | C640 | C650
                  | C800 | C810 | C900 | C910 | C920 | C930 | C940 | C950 | C990
                  | C600FF | C650FF | C900FF | C950FF
                  | C660 | C670 | C680 | C685 | C695
                  | C980 | C985 | C995 | C680FF | C980FF
                  | C450 | C460 | C470,
      HasOpaqueWindow = C200 | C210 | C215 | C220 | C230 | C240 | C250
                      | C400 | C410 | C420 | C430 | C800,
      NoInfiltration = C195 | C200 | C210 | C215 | C220 | C240 | C250
                     | C270 | C280 | C290 | C300 | C310 | C320 | C395 | C400,
      LowIntIREmissivity = C195 | C200 | C210,
      LowExtIREmissivity = C195 | C200 | C215,
      LowIntSWEmissivity = C280 | C440 | C810,
      HighIntSWEmissivity = C270 | C290 | C300 | C310 | C320,
      LowExtSWEmissivity = C195 | C200 | C210 | C215 | C220 | C230 | C240
                         | C270 | C280 | C290 | C300 | C310 | C320
                         | C395 | C400 | C410 | C420,
      NoWindow = C195 | C395,
      HasHighConductanceWall = C210 | C215 | C220 | C230 | C240 | C250
                             | C400 | C410 | C420 | C430 | C440 | C800,
      HasEWWindow = C300 | C310 | C620 | C630 | C920 | C930,
      HasSunShade = C290 | C310 | C610 | C630 | C910 | C930 | C990,

      // 新規仕様フラグ (複数 bit の合成のみ; 単一 case と同 bit のフラグは
      // enum.ToString() で alias 名が返るため避け、case 直接比較で代替する)
      ExtraInsulation = C680 | C695 | C980 | C995 | C680FF | C980FF,
      ConstIntCoeffs = C450 | C460,
      ConstExtCoeffs = C450 | C470,
    }

    #endregion

    #region 建物モデル作成処理

    /// <summary>
    /// 標準的な BESTEST 単室建物 (8m×6m×2.7m, 床面積 48 m²) を作成。
    /// SunSpace ケース (C960) と地中結合ケース (C990) は専用ファクトリへ委譲。
    /// </summary>
    public static void MakeBuilding(TestCase tCase,
        out MultiRoom mRoom, out Zone[] zones, out Wall[] walls, out Window[] windows)
    {
      if (tCase == TestCase.C960)
      {
        MakeSunZoneBuilding(out mRoom, out zones, out walls, out windows);
        return;
      }
      if (tCase == TestCase.C990)
      {
        MakeGroundCouplingBuilding(out mRoom, out zones, out walls, out windows);
        return;
      }

      bool hasEWWindow = (tCase & TestCase.HasEWWindow) == tCase;
      bool hasSunShade = (tCase & TestCase.HasSunShade) == tCase;
      bool hasHeatGain = (tCase & TestCase.HasHeatGain) == tCase;
      bool hasOpaqueWindow = (tCase & TestCase.HasOpaqueWindow) == tCase;
      bool isLowIntIREmissivity = (tCase & TestCase.LowIntIREmissivity) == tCase;
      bool isLowExtIREmissivity = (tCase & TestCase.LowExtIREmissivity) == tCase;
      bool isLowIntSWEmissivity = (tCase & TestCase.LowIntSWEmissivity) == tCase;
      bool isHighIntSWEmissivity = (tCase & TestCase.HighIntSWEmissivity) == tCase;
      bool noInfiltration = (tCase & TestCase.NoInfiltration) == tCase;
      bool isLowExtSWEmissivity = (tCase & TestCase.LowExtSWEmissivity) == tCase;
      bool noWindow = (tCase & TestCase.NoWindow) == tCase;
      bool isLowE   = tCase == TestCase.C660;
      bool isSinglePane = tCase == TestCase.C670;
      bool isConstIntCoeffs = (tCase & TestCase.ConstIntCoeffs) == tCase;
      bool isConstExtCoeffs = (tCase & TestCase.ConstExtCoeffs) == tCase;

      // 屋外表面長波長放射率
      double extlwEmissivity = isLowExtIREmissivity ? 0.1 : 0.9;
      // 屋外表面短波長吸収率
      double extswAbsorptance;
      if (tCase == TestCase.C250) extswAbsorptance = 0.9;
      else if (isLowExtSWEmissivity) extswAbsorptance = 0.1;
      else extswAbsorptance = 0.6;
      // 屋内表面長波長放射率
      double intlwEmissivity = isLowIntIREmissivity ? 0.1 : 0.9;
      // 屋内表面短波長吸収率
      double intswAbsorptance;
      if (isLowIntSWEmissivity) intswAbsorptance = 0.1;
      else if (isHighIntSWEmissivity) intswAbsorptance = 0.9;
      else intswAbsorptance = 0.6;

      // ゾーン
      zones = new Zone[1];
      zones[0] = new Zone("Zn1", 8 * 6 * 2.7 * AIR_DNS);
      zones[0].InitializeAirState(20, 0);
      if (hasHeatGain) zones[0].AddHeatGain(new SimpleHeatGain(200 * 0.4, 200 * 0.6, 0));
      if (tCase == TestCase.C230) zones[0].VentilationRate = zones[0].AirMass / 3600d;
      else if (noInfiltration) zones[0].VentilationRate = 0;
      else zones[0].VentilationRate = zones[0].AirMass * 0.5 / 3600d;

      // 壁
      WallLayer[] exwL, flwL, rfwL;
      MakeWallLayer(tCase, out exwL, out flwL, out rfwL);
      walls = new Wall[6];
      walls[0] = new Wall(48, flwL);                                         // 床
      walls[1] = new Wall(48, rfwL);                                         // 屋根
      walls[2] = new Wall(8 * 2.7, exwL);                                    // 北外壁
      walls[3] = new Wall(hasEWWindow ? 6 * 2.7 - 6 : 6 * 2.7, exwL);        // 東外壁
      walls[4] = new Wall(hasEWWindow ? 6 * 2.7 - 6 : 6 * 2.7, exwL);        // 西外壁
      walls[5] = new Wall(
          (noWindow || hasEWWindow) ? 8 * 2.7 : 8 * 2.7 - 6d - 6d, exwL);    // 南外壁

      // h_r ≈ 4εσT̄³ を T̄=20°C で線形化 (内側/外側それぞれ ε に応じて)
      double hrF = 4 * extlwEmissivity * PhysicsConstants.StefanBoltzmannConstant * Math.Pow(PhysicsConstants.ToKelvin(10), 3);
      double hrB = 4 * intlwEmissivity * PhysicsConstants.StefanBoltzmannConstant * Math.Pow(PhysicsConstants.ToKelvin(10), 3);
      for (int i = 0; i < walls.Length; i++)
      {
        walls[i].Initialize(25);

        //屋外側
        walls[i].ConvectiveCoefficientF = (i == 0) ? AO_FLOOR : (i == 1) ? AO_ROOF : AO_WALL;
        walls[i].RadiativeCoefficientF = hrF;
        walls[i].ShortWaveAbsorptanceF = extswAbsorptance;
        walls[i].LongWaveEmissivityF = extlwEmissivity;
        //屋内側
        walls[i].ConvectiveCoefficientB = (i == 0) ? AI_FLOOR : (i == 1) ? AI_CEILING : AI_WALL;  // 屋根=ceiling, 他=walls/floor
        walls[i].RadiativeCoefficientB = hrB;
        walls[i].ShortWaveAbsorptanceB = intswAbsorptance;
        walls[i].LongWaveEmissivityB = intlwEmissivity;
      }

      // Cases 450/460/470: 表面熱伝達係数を一定値で上書き (Std 140-2023 Table 7-46)
      // walls[0]=床(F=外側=地面), [1]=屋根, [2]=北外壁, [3]=東, [4]=西, [5]=南
      if (isConstIntCoeffs || isConstExtCoeffs)
      {
        // Wall: hcomb_int=1.8, hcomb_ext=21.6
        // Roof: hcomb_int=1.7, hcomb_ext=21.8
        // Floor (raised): hcomb_int=3.7, hcomb_ext=5.2
        // 室内側 (B side) は LongWave 放射を 0 にして全部 Convective に集約
        for (int i = 0; i < walls.Length; i++)
        {
          double intH = (i == 0) ? 3.7 : (i == 1) ? 1.7 : 1.8;   // 床 / 屋根 / 外壁
          double extH = (i == 0) ? 5.2 : (i == 1) ? 21.8 : 21.6;
          if (isConstIntCoeffs)
          {
            walls[i].ConvectiveCoefficientB = intH;
            walls[i].LongWaveEmissivityB = 0;        // 放射を全部 Convective に集約
          }
          if (isConstExtCoeffs)
          {
            walls[i].ConvectiveCoefficientF = extH;
            walls[i].LongWaveEmissivityF = 0;
          }
        }
      }

      // 窓
      if (!noWindow)
      {
        windows = new Window[2];
        Incline[] inc = hasEWWindow
            ? new[] { new Incline(Incline.Orientation.E, 0.5 * Math.PI),
                      new Incline(Incline.Orientation.W, 0.5 * Math.PI) }
            : new[] { new Incline(Incline.Orientation.S, 0.5 * Math.PI),
                      new Incline(Incline.Orientation.S, 0.5 * Math.PI) };

        for (int i = 0; i < 2; i++)
        {
          if (hasOpaqueWindow)
          {
            windows[i] = new Window(6, new[] { 0.0, 0.0 }, new[] { 1 - extswAbsorptance, 0.0 }, inc[i]);
          }
          else if (isSinglePane)
          {
            // Case 670: 単板窓 (Std 140-2023 Table 7-21/22).
            // Pane conductance 328 W/m²K → R = 0.00305 m²K/W.
            // 法線入射 trans=0.83446, refl=0.0391 は Annex B6.2 の調整済単板パラメータ
            // (n=1.493, K=0.0337/mm, TH=3.048mm) から計算した値で、Case 600 と同一材料。
            // GlazingCount=1 の真の単板として構築。
            windows[i] = new Window(6,
                new[] { 0.834 }, new[] { 0.075 },
                new[] { 0.834 }, new[] { 0.075 },
                inc[i]);
            windows[i].SetGlassResistance(0, 0.00305);
          }
          else if (isLowE)
          {
            // Case 660: low-e Argon 窓 (Std 140-2023 Tables 7-17/18/19).
            // Pane: 外 (low-e) cond=314 W/m²K → R=0.00318, 内 (clear) cond=328 → R=0.00305.
            // Argon gap effective conductance hs=1.792 W/m²K → R=0.5581 m²K/W.
            // 法線入射値: 外 low-e は Tables 7-17/18 由来 (F/B 反射が非対称)、
            //   内 clear は Annex B6.2 値 (T=0.83446, R=0.0391) で Case 600 と同一。
            // 角度依存: §B6.2 は coated glass に適用不可と仕様明記のため、簡易処置として
            //   両層に clear glass 多項式を流用。低-e の正確な角度シェイプは未対応 (TODO)。
            windows[i] = new Window(6,
                new[] { 0.452, 0.83446 }, new[] { 0.359, 0.0391 },     // F側 (外向き入射)
                new[] { 0.452, 0.83446 }, new[] { 0.397, 0.0391 },     // B側 (内向き入射、low-e は反射が異なる)
                inc[i]);
            windows[i].SetGlassResistance(0, 0.00318);   // 外 pane (3.180mm)
            windows[i].SetGlassResistance(1, 0.00305);   // 内 pane (3.048mm)
            windows[i].SetAirGapResistance(0, 0.5581);   // Argon gap
          }
          else
          {
            // Case 600 等: 標準クリア二重窓 (Std 140-2023 §B6.1)。
            // Pane 法線入射 T=0.83446, R=0.0391 は Annex B6.2 の調整済単板値
            // (n=1.493, K=0.0337/mm, TH=3.048mm)。両 pane 同一材料。
            // Pane R=0.003, AirGap R=0.1588 m²K/W。
            windows[i] = new Window(6,
                new[] { 0.834, 0.834 }, new[] { 0.075, 0.075 },
                new[] { 0.834, 0.834 }, new[] { 0.075, 0.075 },
                inc[i]);
            windows[i].SetGlassResistance(0, 0.003);
            windows[i].SetGlassResistance(1, 0.003);
            windows[i].SetAirGapResistance(0, 0.1588);
          }

          windows[i].LongWaveEmissivityF = extlwEmissivity;
          windows[i].LongWaveEmissivityB = intlwEmissivity;
          windows[i].ConvectiveCoefficientF = AO_WINDOW;
          windows[i].RadiativeCoefficientF = hrF;
          windows[i].ConvectiveCoefficientB = AI_WINDOW;
          windows[i].RadiativeCoefficientB = hrB;
          SetBESTESTWindowAngleDependence(windows[i]);

          // Cases 450/470: 窓も外側を 17.8 W/m²K に固定 (Table 7-46)
          // Cases 450/460: 窓の内側を 4.5 W/m²K に固定
          if (isConstExtCoeffs)
          {
            windows[i].ConvectiveCoefficientF = 17.8;
            windows[i].LongWaveEmissivityF = 0;
          }
          if (isConstIntCoeffs)
          {
            windows[i].ConvectiveCoefficientB = 4.5;
            windows[i].LongWaveEmissivityB = 0;
          }
        }

        if (hasSunShade)
        {
          if (hasEWWindow)
          {
            windows[0].SunShade = SunShade.MakeGridSunShade(3, 2, 1, 0, 0, 0, 0, inc[0]);
            windows[1].SunShade = SunShade.MakeGridSunShade(3, 2, 1, 0, 0, 0, 0, inc[1]);
          }
          else
          {
            windows[0].SunShade = SunShade.MakeHorizontalSunShade(3, 2, 1, 4.5, 0.5, 0.5, inc[0]);
            windows[1].SunShade = SunShade.MakeHorizontalSunShade(3, 2, 1, 0.5, 4.5, 0.5, inc[1]);
          }
        }
      }
      else windows = new Window[0];

      // 多数室
      mRoom = new MultiRoom(1, zones, walls, windows);
      mRoom.TimeStep = 3600;
      mRoom.Albedo = 0.2;
      mRoom.AddZone(0, 0);

      // 屋外表面設定
      // Std140-2023 §7.2.1.5.1: raised floor は床下空気が外気温に等しい (slab-on-grade ではない)。
      // F 側を OutsideWall (tilt=π, fs_to_sky=0) とし、§7.2.1.5.1.b で日射ゼロのため α_F=0。
      // 床下は風が当たらないので h_conv 動的更新の対象外 (Std140 が h_conv,floor=0.8 を規定)。
      mRoom.SetOutsideWall(0, true, INC_F);
      walls[0].ShortWaveAbsorptanceF = 0.0;
      walls[0].IsWindExposedF = false;
      mRoom.SetOutsideWall(1, true, INC_H);
      mRoom.SetOutsideWall(2, true, INC_N);
      mRoom.SetOutsideWall(3, true, INC_E);
      mRoom.SetOutsideWall(4, true, INC_W);
      mRoom.SetOutsideWall(5, true, INC_S);

      // 壁・窓をゾーンに追加
      for (int i = 0; i < walls.Length; i++) mRoom.AddWall(0, i, false);
      for (int i = 0; i < windows.Length; i++)
      {
        mRoom.AddWindow(0, i);
        // BESTEST では全日射がまず床に当たると仮定
        mRoom.SetSWDistributionRateToFloor(i, 0, false, 1.0);
      }
    }

    /// <summary>Sun Zone 付き2室建物 (Case 960)。</summary>
    public static void MakeSunZoneBuilding(
        out MultiRoom mRoom, out Zone[] zones, out Wall[] walls, out Window[] windows)
    {
      // 表面熱伝達率は Std 140-2023 Tables 7-7/7-9 の h_conv (定数はクラス冒頭で共有)。
      // 放射は NR 経路で別途計算。
      const double extswEmissivity = 0.6;
      const double extlwEmissivity = 0.9;
      const double intswEmissivity = 0.6;
      const double intlwEmissivity = 0.9;

      zones = new Zone[2];
      zones[0] = new Zone("BackZone", 8 * 6 * 2.7 * AIR_DNS);
      zones[0].AddHeatGain(new SimpleHeatGain(200 * 0.4, 200 * 0.6, 0));
      zones[0].InitializeAirState(20, 0);
      zones[0].VentilationRate = zones[0].AirMass * 0.5 / 3600;
      zones[1] = new Zone("SunZone", 8 * 2 * 2.7 * AIR_DNS);
      zones[1].InitializeAirState(20, 0);
      zones[1].VentilationRate = zones[1].AirMass * 0.5 / 3600;

      // 壁
      WallLayer[] exwL_L, flwL_L, rfwL_L, exwL_H, flwL_H, rfwL_H;
      MakeWallLayer(TestCase.C200, out exwL_L, out flwL_L, out rfwL_L); // 軽量系壁材
      MakeWallLayer(TestCase.C900, out exwL_H, out flwL_H, out rfwL_H); // 重量系壁材
      WallLayer[] cwL = new[] { new WallLayer("CommonWall", 0.510, 1400d * 1000d / 1000d, 0.2) };

      walls = new Wall[11];
      walls[0]  = new Wall(48,         flwL_L); // 床1
      walls[1]  = new Wall(16,         flwL_H); // 床2
      walls[2]  = new Wall(48,         rfwL_L); // 屋根1
      walls[3]  = new Wall(16,         rfwL_H); // 屋根2
      walls[4]  = new Wall(8 * 2.7,    exwL_L); // 北外壁
      walls[5]  = new Wall(8 * 2.7 - 6 - 6, exwL_H); // 南外壁
      walls[6]  = new Wall(6 * 2.7,    exwL_L); // 東外壁1
      walls[7]  = new Wall(2 * 2.7,    exwL_H); // 東外壁2
      walls[8]  = new Wall(6 * 2.7,    exwL_L); // 西外壁1
      walls[9]  = new Wall(2 * 2.7,    exwL_H); // 西外壁2
      walls[10] = new Wall(8 * 2.7,    cwL);    // 共用壁

      // h_r 線形化 (T̄=20°C)
      double hrF = 4 * extlwEmissivity * PhysicsConstants.StefanBoltzmannConstant * Math.Pow(PhysicsConstants.ToKelvin(10), 3);
      double hrB = 4 * intlwEmissivity * PhysicsConstants.StefanBoltzmannConstant * Math.Pow(PhysicsConstants.ToKelvin(10), 3);
      // walls[0,1]=床, [2,3]=屋根, [4..9]=外壁, [10]=共用壁 (内部、ループ後に上書き)
      for (int i = 0; i < walls.Length; i++)
      {
        walls[i].ConvectiveCoefficientF = (i <= 1) ? AO_FLOOR : (i <= 3) ? AO_ROOF : AO_WALL;
        walls[i].RadiativeCoefficientF = hrF;
        walls[i].Initialize(25);
        walls[i].ShortWaveAbsorptanceF = extswEmissivity;
        walls[i].LongWaveEmissivityF = extlwEmissivity;
        walls[i].ShortWaveAbsorptanceB = intswEmissivity;
        walls[i].LongWaveEmissivityB = intlwEmissivity;
        walls[i].ConvectiveCoefficientB = (i <= 1) ? AI_FLOOR : (i <= 3) ? AI_CEILING : AI_WALL;
        walls[i].RadiativeCoefficientB = hrB;
      }
      // 共用壁: 両側室内なので F も h_conv,int で
      walls[10].ConvectiveCoefficientF = AI_WALL;
      walls[10].ShortWaveAbsorptanceF = intswEmissivity;
      walls[10].LongWaveEmissivityF = intlwEmissivity;

      // 窓 (Sun zone 用、Case 600 と同一の clear double-pane)。
      // 法線入射 T=0.83446, R=0.0391 は Annex B6.2 調整済単板値。
      windows = new Window[2];
      for (int i = 0; i < 2; i++)
      {
        windows[i] = new Window(6,
                new[] { 0.834, 0.834 }, new[] { 0.075, 0.075 },
                new[] { 0.834, 0.834 }, new[] { 0.075, 0.075 },
                INC_S);
        windows[i].SetGlassResistance(0, 0.003);
        windows[i].SetGlassResistance(1, 0.003);
        windows[i].SetAirGapResistance(0, 0.1588);
        windows[i].LongWaveEmissivityF = extlwEmissivity;
        windows[i].LongWaveEmissivityB = intlwEmissivity;
        windows[i].ConvectiveCoefficientF = AO_WINDOW;
        windows[i].RadiativeCoefficientF = hrF;
        windows[i].ConvectiveCoefficientB = AI_WINDOW;
        windows[i].RadiativeCoefficientB = hrB;
        SetBESTESTWindowAngleDependence(windows[i]);
      }

      // 多数室 (2室)
      mRoom = new MultiRoom(2, zones, walls, windows);
      mRoom.TimeStep = 3600;
      mRoom.Albedo = 0.2;
      mRoom.AddZone(0, 0);
      mRoom.AddZone(1, 1);

      // 屋外表面設定 (Std140-2023 §7.2.1.5.1: raised floor は外気温、no solar、wind shelter)
      mRoom.SetOutsideWall(0, true, INC_F);
      mRoom.SetOutsideWall(1, true, INC_F);
      walls[0].ShortWaveAbsorptanceF = 0.0;
      walls[1].ShortWaveAbsorptanceF = 0.0;
      walls[0].IsWindExposedF = false;
      walls[1].IsWindExposedF = false;
      mRoom.SetOutsideWall(2, true, INC_H);
      mRoom.SetOutsideWall(3, true, INC_H);
      mRoom.SetOutsideWall(4, true, INC_N);
      mRoom.SetOutsideWall(5, true, INC_S);
      mRoom.SetOutsideWall(6, true, INC_E);
      mRoom.SetOutsideWall(7, true, INC_E);
      mRoom.SetOutsideWall(8, true, INC_W);
      mRoom.SetOutsideWall(9, true, INC_W);

      // SunZone に窓を追加
      mRoom.AddWindow(1, 0);
      mRoom.AddWindow(1, 1);
      mRoom.SetSWDistributionRateToFloor(0, 1, false, 1.0);
      mRoom.SetSWDistributionRateToFloor(1, 1, false, 1.0);

      // BackZone に壁を追加
      mRoom.AddWall(0, 0, false);
      mRoom.AddWall(0, 2, false);
      mRoom.AddWall(0, 4, false);
      mRoom.AddWall(0, 6, false);
      mRoom.AddWall(0, 8, false);
      mRoom.AddWall(0, 10, true);

      // SunZone に壁を追加
      mRoom.AddWall(1, 1, false);
      mRoom.AddWall(1, 3, false);
      mRoom.AddWall(1, 5, false);
      mRoom.AddWall(1, 7, false);
      mRoom.AddWall(1, 9, false);
      mRoom.AddWall(1, 10, false);
    }

    /// <summary>地中結合付き建物 (Case 990)。地下外壁を土壌層付き壁体で表現。</summary>
    public static void MakeGroundCouplingBuilding(
        out MultiRoom mRoom, out Zone[] zones, out Wall[] walls, out Window[] windows)
    {
      // 表面熱伝達率は Std 140-2023 Tables 7-7/7-9 の h_conv (定数はクラス冒頭で共有)。
      // 地下外壁は SetGroundWall で F 側上書きされるので AO_WALL は実質バイパス。
      const double extswEmissivity = 0.6;
      const double extlwEmissivity = 0.9;
      const double intswEmissivity = 0.6;
      const double intlwEmissivity = 0.9;

      zones = new Zone[1];
      zones[0] = new Zone("Zn1", 8 * 6 * 2.7 * AIR_DNS);
      zones[0].AddHeatGain(new SimpleHeatGain(200 * 0.4, 200 * 0.6, 0));
      zones[0].InitializeAirState(20, 0);
      zones[0].VentilationRate = zones[0].AirMass * 0.5 / 3600;

      // 壁
      WallLayer[] exwL, flwL, rfwL;
      MakeWallLayer(TestCase.C990, out exwL, out flwL, out rfwL);

      // 床は土壌層を追加
      flwL = new WallLayer[]
      {
        new WallLayer("Ground",        1.3,   800d  * 1500d / 1000d, 2.0),
        new WallLayer("Concrete Slab", 1.130, 1400d * 1000d / 1000d, 0.08 / 3),
        new WallLayer("Concrete Slab", 1.130, 1400d * 1000d / 1000d, 0.08 / 3),
        new WallLayer("Concrete Slab", 1.130, 1400d * 1000d / 1000d, 0.08 / 3),
      };

      walls = new Wall[9];
      walls[0] = new Wall(48,        flwL);  // 床
      walls[1] = new Wall(48,        rfwL);  // 屋根
      walls[2] = new Wall(8 * 1.35,  exwL);  // 北外壁 (地上)
      walls[3] = new Wall(8 * 1.35,  flwL);  // 北外壁 (地下)
      walls[4] = new Wall(6 * 1.35,  exwL);  // 東外壁 (地上)
      walls[5] = new Wall(6 * 1.35,  flwL);  // 東外壁 (地下)
      walls[6] = new Wall(6 * 1.35,  exwL);  // 西外壁 (地上)
      walls[7] = new Wall(6 * 1.35,  flwL);  // 西外壁 (地下)
      walls[8] = new Wall(8 * 1.35,  flwL);  // 南外壁 (地下) ※地上は無し

      // h_r 線形化 (T̄=20°C)
      double hrF = 4 * extlwEmissivity * PhysicsConstants.StefanBoltzmannConstant * Math.Pow(PhysicsConstants.ToKelvin(10), 3);
      double hrB = 4 * intlwEmissivity * PhysicsConstants.StefanBoltzmannConstant * Math.Pow(PhysicsConstants.ToKelvin(10), 3);
      // walls[0]=床(slab), [1]=屋根, [2-8]=外壁(地上+地下混在、地下は SetGroundWall で F 側上書き)
      for (int i = 0; i < walls.Length; i++)
      {
        walls[i].ConvectiveCoefficientF = (i == 1) ? AO_ROOF : AO_WALL;  // 床(0)/地下壁は実質バイパス
        walls[i].RadiativeCoefficientF = hrF;
        walls[i].Initialize(25);
        walls[i].ShortWaveAbsorptanceF = extswEmissivity;
        walls[i].LongWaveEmissivityF = extlwEmissivity;
        walls[i].ShortWaveAbsorptanceB = intswEmissivity;
        walls[i].LongWaveEmissivityB = intlwEmissivity;
        walls[i].ConvectiveCoefficientB = (i == 1) ? AI_CEILING : AI_WALL;
        walls[i].RadiativeCoefficientB = hrB;
      }

      // 窓 (Case 990 用 clear double-pane、面積 5.4 m²)。
      // 法線入射 T=0.83446, R=0.0391 は Annex B6.2 調整済単板値。
      windows = new Window[2];
      for (int i = 0; i < 2; i++)
      {
        windows[i] = new Window(5.4,
                new[] { 0.834, 0.834 }, new[] { 0.075, 0.075 },
                new[] { 0.834, 0.834 }, new[] { 0.075, 0.075 },
                INC_S);
        windows[i].SetGlassResistance(0, 0.003);
        windows[i].SetGlassResistance(1, 0.003);
        windows[i].SetAirGapResistance(0, 0.1588);
        windows[i].LongWaveEmissivityF = extlwEmissivity;
        windows[i].LongWaveEmissivityB = intlwEmissivity;
        windows[i].ConvectiveCoefficientF = AO_WINDOW;
        windows[i].RadiativeCoefficientF = hrF;
        windows[i].ConvectiveCoefficientB = AI_WINDOW;
        windows[i].RadiativeCoefficientB = hrB;
        SetBESTESTWindowAngleDependence(windows[i]);
      }

      mRoom = new MultiRoom(1, zones, walls, windows);
      mRoom.TimeStep = 3600;
      mRoom.Albedo = 0.2;
      mRoom.AddZone(0, 0);

      // 屋外表面設定
      mRoom.SetGroundWall(0, true, 10000);
      mRoom.SetOutsideWall(1, true, INC_H);
      mRoom.SetOutsideWall(2, true, INC_N);
      mRoom.SetGroundWall(3, true, 10000);
      mRoom.SetOutsideWall(4, true, INC_E);
      mRoom.SetGroundWall(5, true, 10000);
      mRoom.SetOutsideWall(6, true, INC_W);
      mRoom.SetGroundWall(7, true, 10000);
      mRoom.SetGroundWall(8, true, 10000);

      // 窓と壁をゾーンに追加
      mRoom.AddWindow(0, 0);
      mRoom.AddWindow(0, 1);
      mRoom.SetSWDistributionRateToFloor(0, 0, false, 1.0);
      mRoom.SetSWDistributionRateToFloor(1, 0, false, 1.0);
      for (int i = 0; i < walls.Length; i++) mRoom.AddWall(0, i, false);
    }

    #endregion

    #region 壁層作成処理

    /// <summary>
    /// 外壁・床・屋根の層構成 (低重量系 / 重量系、断熱増強オプション) を返す。
    /// 増強仕様 (Cases 680/980/695/995/680FF/980FF) は Std 140-2023 Tables 7-25/7-31 に準拠。
    /// </summary>
    public static void MakeWallLayer(TestCase tCase,
        out WallLayer[] exterior, out WallLayer[] floor, out WallLayer[] roof)
    {
      bool isLightWeight = !((tCase & TestCase.HeavyWeight) == tCase);
      bool isExtraIns    = (tCase & TestCase.ExtraInsulation) == tCase;

      if (isLightWeight)
      {
        // 外壁: 通常は Fibreglas quilt 0.066m、断熱増強で Foam insulation 0.250m (Table 7-25)
        exterior = isExtraIns
            ? new WallLayer[]
            {
              new WallLayer("Wood Siding",     0.140, 530d * 900d  / 1000d, 0.009),
              new WallLayer("Foam Insulation", 0.040,  10d * 1400d / 1000d, 0.250),
              new WallLayer("Plasterboard",    0.160, 950d * 840d  / 1000d, 0.012),
            }
            : new WallLayer[]
            {
              new WallLayer("Wood Siding",     0.140, 530d * 900d  / 1000d, 0.009),
              new WallLayer("Fibreglas quilt", 0.040,  12d * 840d  / 1000d, 0.066),
              new WallLayer("Plasterboard",    0.160, 950d * 840d  / 1000d, 0.012),
            };
        // 床: Case 600/680 とも同じ (断熱増強でも床は変えない、§7.2.2.1.8.1 注 c)
        floor = new WallLayer[]
        {
          new WallLayer("Insulation1",     0.040, 0.0001,                0.500),
          new WallLayer("Insulation2",     0.040, 0.0001,                0.5003),
          new WallLayer("Timber flooring", 0.140, 650d * 1200d / 1000d,  0.025),
        };
        // 屋根: 通常 Fibreglas quilt 0.1118m、断熱増強で 0.4m
        roof = new WallLayer[]
        {
          new WallLayer("Roofdeck",        0.140, 530d * 900d  / 1000d, 0.019),
          new WallLayer("Fibreglas quilt", 0.040,  12d * 840d  / 1000d, isExtraIns ? 0.4 : 0.1118),
          new WallLayer("Plasterboard",    0.160, 950d * 840d  / 1000d, 0.010),
        };
        return;
      }

      // 重量系
      // 外壁 Foam: 通常 0.0615m、断熱増強で 0.2452m (Table 7-31)
      exterior = new WallLayer[]
      {
        new WallLayer("Wood Siding",     0.140,  530d * 900d  / 1000d, 0.009),
        new WallLayer("Foam Insulation", 0.040,   10d * 1400d / 1000d, isExtraIns ? 0.2452 : 0.0615),
        new WallLayer("Concrete Block",  0.510, 1400d * 1000d / 1000d, 0.1 / 3),
        new WallLayer("Concrete Block",  0.510, 1400d * 1000d / 1000d, 0.1 / 3),
        new WallLayer("Concrete Block",  0.510, 1400d * 1000d / 1000d, 0.1 / 3),
      };
      floor = new WallLayer[]
      {
        new WallLayer("Insulation1",     0.040, 0.001,                  0.50035),
        new WallLayer("Insulation2",     0.040, 0.001,                  0.50035),
        new WallLayer("Concrete Slab",   1.130, 1400d * 1000d / 1000d,  0.08 / 3),
        new WallLayer("Concrete Slab",   1.130, 1400d * 1000d / 1000d,  0.08 / 3),
        new WallLayer("Concrete Slab",   1.130, 1400d * 1000d / 1000d,  0.08 / 3),
      };
      // 屋根: 重量系も Case 600 と同じ。断熱増強で 0.1118 → 0.4m (Table 7-31)
      roof = new WallLayer[]
      {
        new WallLayer("Roofdeck",        0.140,  530d * 900d  / 1000d, 0.019),
        new WallLayer("Fibreglas quilt", 0.040,   12d * 840d  / 1000d, isExtraIns ? 0.4 : 0.1118),
        new WallLayer("Plasterboard",    0.160,  950d * 840d  / 1000d, 0.010),
      };
    }

    #endregion

    #region 窓入射角特性設定

    /// <summary>
    /// Std 140-2023 Annex B6.2 の clear glass 公式 (Snell + Fresnel + Bouguer) から
    /// フィットした 5 次多項式係数を、窓の全グレージング層に適用する。Case 600
    /// (clear double-pane) と Case 670 (clear single-pane) は同一材料なので、
    /// per-pane の角度依存シェイプは共通で扱える。Case 660 の clear 内側 pane も同様。
    /// </summary>
    public static void SetBESTESTWindowAngleDependence(Window win)
    {
      var (tau, rho) = WindowOptics.BESTESTClearGlass_C600;
      for (int i = 0; i < win.GlazingCount; i++)
        win.SetAngleDependence(i, tau, tau, rho, rho);
    }

    #endregion
  }
}
