using System;
using Popolo.Core.Physics;

namespace Popolo.Core.Building.Envelope
{

  /// <summary>
  /// Represents a horizontal air chamber (attic or crawl space) with detailed convective
  /// heat transfer calculation. F side is upper; B side is lower.
  /// </summary>
  /// <remarks>
  /// Uses the convective heat transfer correlation for an infinite horizontal fluid layer.
  /// </remarks>
  public class HorizontalAirChamber : WallLayer
  {

    #region 定数宣言

    /// <summary>Temperature difference threshold [K] below which properties are not recalculated.</summary>
    private const double RECALC_TMP = 0.1;

    /// <summary>Assumed humidity ratio of the chamber air [kg/kg] (annual average, 20°C/60%).</summary>
    private const double HUMIDITY_RATIO = 0.010;

    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>Surface temperatures [°C] at the last property update.</summary>
    private double lstUpTmp, lstDwnTmp;

    /// <summary>Gets the radiative heat transfer coefficient [W/(m²·K)].</summary>
    public double RadiativeHeatTransferCoefficient { private set; get; }

    /// <summary>Gets the convective heat transfer coefficient [W/(m²·K)].</summary>
    public double ConvectiveHeatTransferCoefficient { private set; get; }

    /// <summary>Gets the emissivity of the upper surface [-].</summary>
    public double UpperEmissivity { private set; get; } = 0.9;

    /// <summary>Gets the emissivity of the lower surface [-].</summary>
    public double LowerEmissivity { private set; get; } = 0.9;

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance with specified geometry and surface emissivities.</summary>
    /// <param name="name">Layer name.</param>
    /// <param name="thickness">Chamber thickness (height) [m].</param>
    /// <param name="upperEmissivity">Emissivity of the upper surface [-].</param>
    /// <param name="lowerEmissivity">Emissivity of the lower surface [-].</param>
    public HorizontalAirChamber(string name, double thickness, double upperEmissivity, double lowerEmissivity)
    {
      IsVariableProperties = true;
      Name = name;
      Thickness = thickness;
      UpperEmissivity = upperEmissivity;
      LowerEmissivity = lowerEmissivity;

      VolSpecificHeat = MoistAir.GetSpecificHeat(HUMIDITY_RATIO) * 1.2;
      HeatCapacity_B = HeatCapacity_F = 0.5 * VolSpecificHeat * Thickness * 1000;

      lstDwnTmp = lstUpTmp = 0;
      UpdateState(26, 26);
    }

    /// <summary>Initializes a new instance with default emissivities (0.9/0.9).</summary>
    /// <param name="name">Layer name.</param>
    /// <param name="thickness">Chamber thickness [m].</param>
    public HorizontalAirChamber(string name, double thickness)
      : this(name, thickness, 0.9, 0.9) { }

    #endregion

    /// <summary>Updates heat transfer coefficients based on the upper (F) and lower (B) surface temperatures.</summary>
    /// <param name="temperatureF">Upper surface temperature [°C].</param>
    /// <param name="temperatureB">Lower surface temperature [°C].</param>
    /// <returns>True if properties were updated; otherwise false.</returns>
    public override bool UpdateState(double temperatureF, double temperatureB)
    {
      const double RAY_LMT = 1708;  //限界レイリー数

      //前回の物性計算時と温度差がRECALC_TMP未満ならば更新しない（計算速度確保のため）
      if (Math.Abs(temperatureF - lstUpTmp) < RECALC_TMP && Math.Abs(temperatureB - lstDwnTmp) < RECALC_TMP)
        return false;
      lstUpTmp = temperatureF;
      lstDwnTmp = temperatureB;

      //平均空気温度
      double aveTemp = 0.5 * (temperatureF + temperatureB);
      ThermalConductivity = MoistAir.GetThermalConductivity(aveTemp);
      RadiativeHeatTransferCoefficient = 4 * UpperEmissivity * LowerEmissivity * Math.Pow(PhysicsConstants.ToKelvin(aveTemp), 3) * PhysicsConstants.StefanBoltzmannConstant;  //放射熱伝達の線形近似式

      //温度が逆転している場合には対流が生じない
      if (temperatureB <= temperatureF)
        ConvectiveHeatTransferCoefficient = ThermalConductivity / Thickness;
      else
      {
        //無次元数の計算        
        double nu = MoistAir.GetDynamicViscosity(aveTemp, HUMIDITY_RATIO, PhysicsConstants.StandardAtmosphericPressure); //動粘性係数[m2/s]
        double alpha = MoistAir.GetThermalDiffusivity(aveTemp, HUMIDITY_RATIO, PhysicsConstants.StandardAtmosphericPressure);  //熱拡散係数[m2/s]
        double plandtl = nu / alpha;  //プラントル数[-]
        double beta = MoistAir.GetExpansionCoefficient(aveTemp);  //体積膨張率[1/K]
        double grashof = 9.8 * Math.Pow(Thickness, 3) * beta * (temperatureB - temperatureF) / Math.Pow(nu, 2);  //グラスホフ数[-]
        double rayleigh = plandtl * grashof;  //レイリー数[-]

        //対流が生じない場合
        if (rayleigh < RAY_LMT)
          ConvectiveHeatTransferCoefficient = ThermalConductivity / Thickness;
        else
        {
          //平均ヌセルト数から熱伝達率[W/m2K]を計算
          double fpr = Math.Pow(1 + Math.Pow(0.5 / plandtl, 9d / 16d), -16d / 9d);
          double nud = Math.Pow(Math.Pow(1d + 1.466 * (1.0 - RAY_LMT / rayleigh), 15) + Math.Pow(rayleigh * fpr / 1420d, 5), 1d / 15d);
          ConvectiveHeatTransferCoefficient = nud * ThermalConductivity / Thickness;
        }
      }

      HeatConductance = ConvectiveHeatTransferCoefficient + RadiativeHeatTransferCoefficient;
      return true;
    }

  }
}
