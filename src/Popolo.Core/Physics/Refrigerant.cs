/* Refrigerant.cs
 *
 * Copyright (C) 2013 E.Togashi
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or (at
 * your option) any later version.
 *
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;
using Popolo.Core.Exceptions;
using Popolo.Core.Numerics;
using Popolo.Core.Numerics.LinearAlgebra;

namespace Popolo.Core.Physics
{
  /// <summary>
  /// Provides thermophysical property calculations for refrigerants
  /// based on a PVT equation of state developed for building energy simulation.
  /// </summary>
  /// <remarks>
  /// The equation of state and approximation coefficients are based on:
  /// Togashi, E., "Development of Equation of State for the Thermodynamic Properties
  /// of HFC32 (R32) for the Purpose of Annual Building Energy Simulation,"
  /// Transactions of the Society of Heating, Air-conditioning and Sanitary Engineers of Japan
  /// DOI: 10.18948/shase.39.204_69
  ///
  /// Three range layers are distinguished per fluid (paper §2.3, Fig.3):
  ///   1. Standard cycle    - typical operating point (subset of #2)
  ///   2. Validation range  - paper's &lt;1% accuracy claim holds here
  ///   3. Fit range         - data points used for LSQ; widest envelope
  /// The MinPressure / MaxPressure properties below correspond to layer #3
  /// (the safety envelope for convergence iterations). For each fluid:
  ///
  ///   Fluid       | Standard cycle             | Valid range  | Fit range
  ///   ------------|----------------------------|--------------|---------------
  ///   R32         | T_e=5°C, T_c=60°C (AC)     |  950-3900kPa |  700-4500 kPa
  ///   R410A       | T_e=5°C, T_c=50°C (AC)     |  936-3070kPa |  700-4000 kPa
  ///   R134a       | T_e=5°C, T_c=40°C (chiller)|  350-1020kPa |  200-1500 kPa
  ///   R1234ze(E)  | T_e=5°C, T_c=40°C (chiller)|  259- 766kPa |  150-1500 kPa
  ///   R1234yf     | T_e=5°C, T_c=45°C (mob. AC)|  373-1154kPa |  200-2000 kPa
  ///   R1233zd(E)  | T_e=5°C, T_c=35°C (centri.)|   60- 183kPa |   50- 500 kPa
  ///   R1224yd(Z)  | T_e=30°C, T_c=100°C (HP)   |  177-1162kPa |  100-2500 kPa
  ///   R290        | T_e=0°C, T_c=50°C (HP)     |  474-1713kPa |  250-2500 kPa
  ///
  /// Reference state follows ASHRAE/IIR convention:
  /// saturated liquid at 0 °C → enthalpy = 200 kJ/kg, entropy = 1.0 kJ/(kg·K).
  /// </remarks>
  public class Refrigerant
  {

    #region Enumerations

    /// <summary>
    /// Specifies the type of refrigerant.
    /// </summary>
    public enum Fluid
    {
      /// <summary>R410A (pseudo-azeotropic HFC mixture)</summary>
      R410A,
      /// <summary>R32 (difluoromethane, HFC-32)</summary>
      R32,
      /// <summary>R134a (1,1,1,2-tetrafluoroethane, HFC-134a)</summary>
      R134a,
      /// <summary>R1234ze(E) (trans-1,3,3,3-tetrafluoropropene, HFO-1234ze(E))</summary>
      R1234zeE,
      /// <summary>R1234yf (2,3,3,3-tetrafluoropropene, HFO-1234yf)</summary>
      R1234yf,
      /// <summary>R1233zd(E) (trans-1-chloro-3,3,3-trifluoropropene, HCFO-1233zd(E))</summary>
      R1233zdE,
      /// <summary>R1224yd(Z) (cis-1-chloro-2,3,3,3-tetrafluoropropene, HCFO-1224yd(Z))</summary>
      R1224ydZ,
      /// <summary>R290 (propane, C3H8, natural refrigerant, GWP=3, ASHRAE A3)</summary>
      R290
    }

    /// <summary>
    /// Specifies the thermodynamic phase of the refrigerant.
    /// </summary>
    public enum Phase
    {
      /// <summary>Superheated vapor (gas phase)</summary>
      Vapor,
      /// <summary>Subcooled liquid (liquid phase)</summary>
      Liquid,
      /// <summary>Two-phase equilibrium (vapor-liquid coexistence)</summary>
      Equilibrium
    }

    #endregion

    #region Properties

    /// <summary>Gets the type of refrigerant.</summary>
    public Fluid FluidType { get; private set; }

    /// <summary>
    /// Gets the maximum pressure [kPa] of the valid range
    /// (approximation fitting range from the reference paper, Fig.3).
    /// </summary>
    public double MaxPressure { get; private set; }

    /// <summary>
    /// Gets the minimum pressure [kPa] of the valid range
    /// (approximation fitting range from the reference paper, Fig.3).
    /// </summary>
    public double MinPressure { get; private set; }

    /// <summary>Gets the critical temperature [K].</summary>
    public double CriticalTemperature { get; private set; }

    /// <summary>Gets the critical density [kg/m³].</summary>
    public double CriticalDensity { get; private set; }

    /// <summary>Gets the critical pressure [kPa].</summary>
    public double CriticalPressure { get; private set; }

    #endregion

    #region Approximation coefficients (private fields)

    /// <summary>Number of m-direction approximation coefficients.</summary>
    private readonly int _mCount;

    /// <summary>Number of n-direction approximation coefficients.</summary>
    private readonly int _nCount;

    /// <summary>Specific gas constant [kJ/(kg·K)].</summary>
    private readonly double _gasConstant;

    /// <summary>Reference-state temperature [K].</summary>
    private readonly double _refTemperature;

    /// <summary>Reference-state density [kg/m³].</summary>
    private readonly double _refDensity;

    /// <summary>Reference-state enthalpy [kJ/kg].</summary>
    private readonly double _refEnthalpy;

    /// <summary>Reference-state entropy [kJ/(kg·K)].</summary>
    private readonly double _refEntropy;

    /// <summary>PVT approximation coefficients α[m,n].</summary>
    private readonly double[,] _alpha;

    /// <summary>Approximation coefficients for the ideal-gas isobaric specific heat.</summary>
    private readonly double[] _ccp;

    /// <summary>Approximation coefficients used to estimate the initial value of saturation pressure.</summary>
    private readonly double[] _cps;

    /// <summary>Approximation coefficients used to estimate the initial value of saturation temperature.</summary>
    private readonly double[] _cts;

    /// <summary>True when the saturation curve fit is available (see <see cref="FitSaturationCurves"/>).</summary>
    private bool _satFitValid;

    /// <summary>Wagner-type coefficients of the fitted saturation pressure curve.</summary>
    private readonly double[] _satWagner = new double[4];

    /// <summary>Coefficients of the fitted saturated liquid density curve.</summary>
    private readonly double[] _satLiqDens = new double[4];

    /// <summary>Coefficients of the fitted saturated vapor density curve.</summary>
    private readonly double[] _satVapDens = new double[5];

    /// <summary>Temperature range [K] of the saturation curve fit.</summary>
    private double _satFitTMin, _satFitTMax;

    #endregion

    #region Approximation coefficients (static data)

    // R32 coefficients
    // Original (Togashi 2014, Table 2-4): fit against REFPROP 7 via wxMaxima,
    //   constraint set = {P, H^r, S^r, G^r}.
    // Updated (2026-05-26): re-fit against REFPROP 10 via numpy.linalg.lstsq
    //   with constraint set = {P, H^r, S^r, G^r, Cv} (Cv weight w_cv = 0.005).
    //   Mean validation errors (P=950-4000 kPa, SC=10°C, SH=10-80°C, n=42):
    //                 P       H       S       Cv      Cp
    //     original:   0.032%  0.169%  0.135%  0.707%  0.480%
    //     updated:    0.029%  0.003%  0.004%  0.196%  0.174%
    private static readonly double[] AlphaR32 = {
         1.0849130E+04,  2.4794975E+04,  2.5104943E+04, -4.7522715E+04,  2.1827033E+04, -3.1822583E+03,
        -9.0730234E+02,  3.4680349E+04, -1.6193386E+05,  1.9553373E+05, -8.7146164E+04,  1.3020389E+04,
        -1.2321952E+05, -2.9672469E+05,  4.0173154E+05, -2.7093883E+05,  1.1213584E+05, -1.7414212E+04,
         1.1371753E+05,  4.5193776E+05, -4.3965229E+05,  1.3397163E+05, -3.7184868E+04,  6.9194528E+03,
        -3.8478087E+04, -1.8449693E+05,  1.6119624E+05, -5.7226966E+03, -1.2429377E+04,  1.4955712E+03
    };
    private static readonly double[] CcpR32 = { 4.0186023E+00, -3.1370881E-01, 6.8796834E-01, 2.6831619E+00, -1.3934091E+00 };
    private static readonly double[] CpsR32 = { 102795, -204886, 141476, -33645 };
    private static readonly double[] CtsR32 = { 148, -298, 269, 241 };

    // R410A coefficients (pseudo-pure fluid model; near-azeotropic R32/R125 50/50 mass% blend)
    // Original (Togashi 2014, Table 5-7): fit against REFPROP 7 via wxMaxima,
    //   constraint set = {P, H^r, S^r, G^r}.
    // Updated (2026-05-27): re-fit against REFPROP 10 via numpy.linalg.lstsq
    //   with constraint set = {P, H^r, S^r, G^r, Cv} (Cv weight w_cv = 0.003).
    //   Mean validation errors (P=950-4000 kPa, SC=10°C, SH=10-80°C, n=28):
    //                 P       H       S       Cv      Cp
    //     original:   0.219%  0.651%  0.511%  0.830%  0.587%
    //     updated:    0.044%  0.004%  0.003%  0.270%  0.183%
    private static readonly double[] AlphaR410A = {
         7.96456057369E+03,  3.96161230132E+04, -2.60836177027E+04,  3.12790189034E+03,  1.88923528834E+03, -4.61551549100E+02,
         4.08848492642E+02,  1.53627402874E+04, -6.68760286294E+04,  7.53877725971E+04, -3.18958596363E+04,  4.57252255380E+03,
        -8.63401359153E+04, -3.07883840750E+05,  3.76086218420E+05, -2.02414618930E+05,  6.69451776226E+04, -9.05652258978E+03,
         8.03500435532E+04,  4.45756873314E+05, -4.51160881381E+05,  1.47539602232E+05, -3.25031390610E+04,  4.56104457172E+03,
        -2.72532870792E+04, -1.77832225442E+05,  1.64316243737E+05, -2.22697540377E+04, -5.99983422526E+03,  9.36541074939E+02
    };
    private static readonly double[] CcpR410A = { 5.25889718276E+00, -3.50481683981E+00, 1.06656172982E+01, -5.82820807438E+00, 1.06897427100E+00 };
    private static readonly double[] CpsR410A = { 81398.1783024, -157420.927345, 104810.780248, -23909.9854978 };
    private static readonly double[] CtsR410A = { 195.861130515, -386.235417474, 312.449595141, 230.124374216 };

    // R134a coefficients
    // Original (Togashi 2014): fit against REFPROP 7 via wxMaxima, M=4 N=6 (25 coeffs),
    //   constraint set = {P, H^r, S^r, G^r}.
    // Updated (2026-05-27): re-fit against REFPROP 10 via numpy.linalg.lstsq,
    //   M=4 N=7 (30 coeffs) for consistency with the other 2026 fits,
    //   constraint set = {P, H^r, S^r, G^r, Cv} (Cv weight w_cv = 0.01).
    //   Mean validation errors (P=200-1500 kPa, SC=10°C, SH=10-80°C, n=40):
    //                 P       H       S       Cv      Cp
    //     original:   0.055%  0.273%  0.237%  0.202%  0.167%
    //     updated:    0.017%  0.003%  0.002%  0.094%  0.099%
    private static readonly double[] AlphaR134a = {
         4.26782983893E+03,  7.47779629958E+04, -1.03296552181E+05,  5.79048984254E+04, -1.46696620562E+04,  1.36007325097E+03,
         6.02145591946E+02,  1.80975607383E+04, -5.03642896407E+04,  4.25427135763E+04, -1.42378898748E+04,  1.64548187349E+03,
        -5.52175547350E+04, -4.99042592126E+05,  7.81822186227E+05, -4.52183000860E+05,  1.22452741221E+05, -1.26000145019E+04,
         4.14938288464E+04,  6.71281180765E+05, -1.00330650780E+06,  5.34011896482E+05, -1.32991311450E+05,  1.31556355591E+04,
        -1.23678952300E+04, -2.55465409214E+05,  3.79578355040E+05, -1.88658706485E+05,  4.13925807508E+04, -3.55464783055E+03
    };
    private static readonly double[] CcpR134a = { 1.73870161216E+00, 1.42901233558E+01, -6.31540422018E+00, 2.56772345114E+00, -4.63026205964E-01 };
    private static readonly double[] CpsR134a = { 64198.4477725, -121225.107696, 78245.2564068, -17195.4951804 };
    private static readonly double[] CtsR134a = { 329.420528733, -599.328860874, 416.791595917, 242.586553194 };

    // R1234ze(E) coefficients
    // Auto-fit from REFPROP 10 (2026-05-26) using the same pipeline as the
    // R32 update: linear LSQ over {P, H^r, S^r, G^r, Cv}, with Cv weight
    // w_cv = 0.003. Validation errors (P=150-1500 kPa, SC=10°C, SH=10-80°C,
    // n=45):
    //              P       H       S       Cv      Cp
    //   mean:     0.026%  0.019%  0.016%  0.097%  0.150%
    //   max:      0.300%  0.051%  0.037%  0.404%  1.181%
    // Note: 12 significant digits are kept for AlphaR1234zeE because lower
    // precision (7-8 sig figs) inflates the max P error at the low-pressure
    // subcooled-liquid corner from 0.30% to ~2%.
    private static readonly double[] AlphaR1234zeE = {
        -1.83086578854E+03,  3.39530175162E+04,  6.70994116056E+03, -2.58249830408E+04,  1.14954207793E+04, -1.51264700035E+03,
        -4.14522665479E+03,  8.52303864397E+04, -2.64673883831E+05,  2.65088459699E+05, -1.04738953371E+05,  1.42413957982E+04,
        -1.52005289179E+04, -3.25123780920E+05,  4.56538772295E+05, -3.56155903380E+05,  1.46147689363E+05, -2.14882879793E+04,
         3.27877482480E+03,  3.11624223232E+05, -2.00252485427E+05,  4.33874087798E+04, -2.47876405258E+04,  6.48082110885E+03,
        -2.21297314879E+02, -9.53392182169E+04, -1.95327621504E+03,  7.68766925789E+04, -3.10479194384E+04,  3.18504634291E+03
    };
    private static readonly double[] CcpR1234zeE = { 7.76379000506E+00, 2.22380610603E-01, 1.08458996728E+01, -6.16799631561E+00, 1.07067988024E+00 };
    private static readonly double[] CpsR1234zeE = { 63063.7099384, -123304.983350, 82995.3594800, -19140.9417857 };
    private static readonly double[] CtsR1234zeE = { 201.592825205, -404.667133640, 335.252071942, 257.691085558 };

    // R1234yf coefficients
    // Mobile-AC / R134a drop-in HFO (GWP < 1); majority blend component of
    // R454B (R410A successor). Auto-fit from REFPROP 10 (2026-07-06),
    // w_cv = 0.001.
    // Validation errors (P=200-2000 kPa, SC=10°C, SH=10-80°C, n=42):
    //              P       H       S       Cv      Cp
    //   mean:     0.062%  0.006%  0.005%  0.209%  0.204%
    //   max:      0.524%  0.057%  0.043%  0.707%  1.376%
    private static readonly double[] AlphaR1234yf = {
        -1.99624511950E+03,  3.58943114337E+04, -1.44722524373E+04,  1.43548676218E+04, -1.10132495346E+04,  2.39413894032E+03,
         1.11529399055E+03, -3.16189094226E+04,  6.65693480886E+04, -6.03383990857E+04,  2.71571204918E+04, -4.70994571195E+03,
        -2.87859813689E+04, -5.06070884405E+04, -3.06816810688E+05,  3.22508620242E+05, -9.94826410081E+04,  1.03720476639E+04,
         1.77949107265E+04,  9.15649331175E+04,  4.53060841858E+05, -5.34471151403E+05,  1.75536169942E+05, -1.79127921783E+04,
        -5.10053671083E+03, -3.63143996210E+04, -2.00721775381E+05,  2.61567876372E+05, -9.59041501553E+04,  1.09964166323E+04
    };
    private static readonly double[] CcpR1234yf = { 1.52394714234E+00, 1.60674668659E+01, -2.78240031819E+00, -1.35542861421E+00, 4.79380228590E-01 };
    private static readonly double[] CpsR1234yf = { 58202.0524394, -114793.926945, 78375.8330203, -18419.2252957 };
    private static readonly double[] CtsR1234yf = { 148.462871444, -317.119729120, 290.859290593, 250.316565391 };

    // R1233zd(E) coefficients
    // Auto-fit from REFPROP 10 (2026-05-26), w_cv = 0.03.
    // Validation errors (P=50-500 kPa, SC=10°C, SH=10-80°C, n=48):
    //              P       H       S       Cv      Cp
    //   mean:     0.010%  0.002%  0.002%  0.015%  0.029%
    //   max:      0.280%  0.007%  0.005%  0.053%  0.257%
    private static readonly double[] AlphaR1233zdE = {
        -6.17437665891E+03,  6.61630218055E+03,  3.25709306637E+04, -2.27918665410E+04,  3.43813401864E+03,  1.04494747503E+02,
        -1.53195211565E+03,  2.47993116224E+04, -3.39124123631E+04,  8.41005177568E+03,  3.67416395345E+03, -1.25226651079E+03,
        -3.65017557540E+03, -8.16577804730E+04, -8.73281050757E+04,  1.34948793510E+05, -4.63335973320E+04,  5.06948551321E+03,
        -9.64163517769E+03,  9.02924242395E+04,  1.29751149124E+05, -1.94704259920E+05,  6.87141198088E+04, -7.45445705357E+03,
         3.34960743547E+03, -3.34168621529E+04, -3.32608004757E+04,  6.32405247979E+04, -2.46188430583E+04,  2.87808619520E+03
    };
    private static readonly double[] CcpR1233zdE = { -3.16336665854E+00, 4.39682566277E+01, -4.62447281121E+01, 2.70759892457E+01, -6.25589913399E+00 };
    private static readonly double[] CpsR1233zdE = { 47962.1451067, -85556.6416572, 51734.3299131, -10561.7197673 };
    private static readonly double[] CtsR1233zdE = { 654.224630727, -1120.61928441, 671.668001111, 264.922237286 };

    // R1224yd(Z) coefficients
    // Auto-fit from REFPROP 10 (2026-05-26), w_cv = 0.003.
    // Validation errors (P=100-2500 kPa, SC=10°C, SH=10-80°C, n=42):
    //              P       H       S       Cv      Cp
    //   mean:     0.047%  0.003%  0.002%  0.117%  0.162%
    //   max:      0.737%  0.019%  0.012%  0.465%  1.697%
    private static readonly double[] AlphaR1224ydZ = {
        -7.27186538182E+03,  4.46560598082E+04, -3.32474905158E+04,  2.79130832934E+04, -1.38568201327E+04,  2.40465958955E+03,
        -1.20879397912E+03,  1.36356613101E+04, -3.17572138288E+03, -2.46383687471E+04,  1.95102386615E+04, -4.01393571621E+03,
        -4.76253045457E+03, -1.93814500594E+05,  9.20458738011E+03,  9.73979987870E+04, -3.90636902891E+04,  5.06861907048E+03,
        -4.16377925032E+03,  2.24727617564E+05,  7.27017494373E+04, -2.18708618564E+05,  7.91658465109E+04, -8.25488946387E+03,
         3.55757492510E+02, -7.95643797900E+04, -4.83354847268E+04,  1.20927561629E+05, -4.86353078025E+04,  5.71940387341E+03
    };
    private static readonly double[] CcpR1224ydZ = { -8.98378299708E+00, 6.19171577644E+01, -5.92644439555E+01, 2.76823791092E+01, -5.12664816466E+00 };
    private static readonly double[] CpsR1224ydZ = { 50261.1736339, -93429.8998659, 59240.9641591, -12764.0263927 };
    private static readonly double[] CtsR1224ydZ = { 426.575759089, -769.355380776, 516.391420205, 273.190159254 };

    // R290 (propane) coefficients
    // Natural refrigerant, GWP=3, ASHRAE A3 (flammable, 150 g charge limit
    // in many residential applications). Widely used in small monoblock
    // heat pumps (e.g., Daikin Altherma-3 series).
    // Auto-fit from REFPROP 10 (2026-05-27), w_cv = 0.001.
    // Validation errors (P=250-2500 kPa, SC=10°C, SH=10-80°C, n=42):
    //              P       H       S       Cv      Cp
    //   mean:     0.030%  0.004%  0.003%  0.163%  0.146%
    //   max:      0.234%  0.024%  0.019%  0.521%  0.680%
    private static readonly double[] AlphaR290 = {
        -2.75837231441E+03,  2.95737811629E+04,  1.22628922512E+04, -2.03897339334E+04,  6.11174134264E+03, -5.15451890339E+02,
        -1.39455329352E+03,  2.72560681057E+04, -9.16527883106E+04,  9.61941080163E+04, -3.91201760775E+04,  5.41391788241E+03,
        -2.73059354975E+04, -1.87948303082E+05,  6.16525872334E+03,  6.55642173269E+04, -2.97921977324E+03, -3.33981697197E+03,
         1.63605536098E+04,  2.06242482886E+05,  2.27458058938E+05, -3.85033388907E+05,  1.28282728537E+05, -1.16220791216E+04,
        -3.77605575809E+03, -6.77109799866E+04, -1.53722836202E+05,  2.45946789442E+05, -9.60548040090E+04,  1.13549903508E+04
    };
    private static readonly double[] CcpR290 = { 6.79531550489E+00, -9.76566742771E+00, 2.44880813725E+01, -1.35811860307E+01, 2.60455810308E+00 };
    private static readonly double[] CpsR290 = { 49046.9598740, -86960.6510847, 53501.4816859, -11361.4084943 };
    private static readonly double[] CtsR290 = { 252.023695253, -489.083473956, 382.853613617, 233.719575599 };

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance for the specified refrigerant type.
    /// </summary>
    /// <param name="fluid">The type of refrigerant.</param>
    public Refrigerant(Fluid fluid)
    {
      FluidType = fluid;

      int index;
      switch (fluid)
      {
        case Fluid.R410A:
          _mCount = 5; _nCount = 8;
          CriticalTemperature = 71.344 + PhysicsConstants.CelsiusToKelvinOffset;
          CriticalDensity = 459.030;
          CriticalPressure = 4901.18;
          _gasConstant = 8.3144621 / 72.5854;
          _refTemperature = PhysicsConstants.ToKelvin(0);
          _refDensity = 1169.95; _refEnthalpy = 200; _refEntropy = 1.0;
          _ccp = CcpR410A; _cps = CpsR410A; _cts = CtsR410A;
          MaxPressure = 4000; MinPressure = 700;
          _alpha = new double[_mCount, _nCount - 2];
          index = 0;
          for (int m = 0; m < _alpha.GetLength(0); m++)
            for (int n = 0; n < _alpha.GetLength(1); n++)
              _alpha[m, n] = AlphaR410A[index++];
          break;

        case Fluid.R32:
          _mCount = 5; _nCount = 8;
          CriticalTemperature = 78.105 + PhysicsConstants.CelsiusToKelvinOffset;
          CriticalDensity = 424.0;
          CriticalPressure = 5782;
          _gasConstant = 8.3144621 / 52.024;
          _refTemperature = PhysicsConstants.ToKelvin(0);
          _refDensity = 1055.3; _refEnthalpy = 200; _refEntropy = 1.0;
          _ccp = CcpR32; _cps = CpsR32; _cts = CtsR32;
          MaxPressure = 4500; MinPressure = 700;
          _alpha = new double[_mCount, _nCount - 2];
          index = 0;
          for (int m = 0; m < _alpha.GetLength(0); m++)
            for (int n = 0; n < _alpha.GetLength(1); n++)
              _alpha[m, n] = AlphaR32[index++];
          break;

        case Fluid.R134a:
          _mCount = 5; _nCount = 8;
          CriticalTemperature = 101.060 + PhysicsConstants.CelsiusToKelvinOffset;
          CriticalDensity = 511.900;
          CriticalPressure = 4059.11;
          _gasConstant = 8.3144621 / 102.0320;
          _refTemperature = PhysicsConstants.ToKelvin(0);
          _refDensity = 1294.78; _refEnthalpy = 200; _refEntropy = 1.0;
          _ccp = CcpR134a; _cps = CpsR134a; _cts = CtsR134a;
          MaxPressure = 1500; MinPressure = 200;
          _alpha = new double[_mCount, _nCount - 2];
          index = 0;
          for (int m = 0; m < _alpha.GetLength(0); m++)
            for (int n = 0; n < _alpha.GetLength(1); n++)
              _alpha[m, n] = AlphaR134a[index++];
          break;

        case Fluid.R1234zeE:
          _mCount = 5; _nCount = 8;
          CriticalTemperature = 109.363 + PhysicsConstants.CelsiusToKelvinOffset;
          CriticalDensity = 489.238;
          CriticalPressure = 3634.86;
          _gasConstant = 8.3144621 / 114.0416;
          _refTemperature = PhysicsConstants.ToKelvin(0);
          _refDensity = 1240.12; _refEnthalpy = 200; _refEntropy = 1.0;
          _ccp = CcpR1234zeE; _cps = CpsR1234zeE; _cts = CtsR1234zeE;
          MaxPressure = 1500; MinPressure = 150;
          _alpha = new double[_mCount, _nCount - 2];
          index = 0;
          for (int m = 0; m < _alpha.GetLength(0); m++)
            for (int n = 0; n < _alpha.GetLength(1); n++)
              _alpha[m, n] = AlphaR1234zeE[index++];
          break;

        case Fluid.R1234yf:
          _mCount = 5; _nCount = 8;
          CriticalTemperature = 94.700 + PhysicsConstants.CelsiusToKelvinOffset;
          CriticalDensity = 475.553;
          CriticalPressure = 3382.24;
          _gasConstant = 8.3144621 / 114.0416;
          _refTemperature = PhysicsConstants.ToKelvin(0);
          _refDensity = 1176.29; _refEnthalpy = 200; _refEntropy = 1.0;
          _ccp = CcpR1234yf; _cps = CpsR1234yf; _cts = CtsR1234yf;
          MaxPressure = 2000; MinPressure = 200;
          _alpha = new double[_mCount, _nCount - 2];
          index = 0;
          for (int m = 0; m < _alpha.GetLength(0); m++)
            for (int n = 0; n < _alpha.GetLength(1); n++)
              _alpha[m, n] = AlphaR1234yf[index++];
          break;

        case Fluid.R1233zdE:
          _mCount = 5; _nCount = 8;
          CriticalTemperature = 166.450 + PhysicsConstants.CelsiusToKelvinOffset;
          CriticalDensity = 480.226;
          CriticalPressure = 3623.64;
          _gasConstant = 8.3144621 / 130.4962;
          _refTemperature = PhysicsConstants.ToKelvin(0);
          _refDensity = 1321.27; _refEnthalpy = 200; _refEntropy = 1.0;
          _ccp = CcpR1233zdE; _cps = CpsR1233zdE; _cts = CtsR1233zdE;
          MaxPressure = 500; MinPressure = 50;
          _alpha = new double[_mCount, _nCount - 2];
          index = 0;
          for (int m = 0; m < _alpha.GetLength(0); m++)
            for (int n = 0; n < _alpha.GetLength(1); n++)
              _alpha[m, n] = AlphaR1233zdE[index++];
          break;

        case Fluid.R1224ydZ:
          _mCount = 5; _nCount = 8;
          CriticalTemperature = 155.540 + PhysicsConstants.CelsiusToKelvinOffset;
          CriticalDensity = 527.128;
          CriticalPressure = 3337.25;
          _gasConstant = 8.3144621 / 148.4867;
          _refTemperature = PhysicsConstants.ToKelvin(0);
          _refDensity = 1427.60; _refEnthalpy = 200; _refEntropy = 1.0;
          _ccp = CcpR1224ydZ; _cps = CpsR1224ydZ; _cts = CtsR1224ydZ;
          MaxPressure = 2500; MinPressure = 100;
          _alpha = new double[_mCount, _nCount - 2];
          index = 0;
          for (int m = 0; m < _alpha.GetLength(0); m++)
            for (int n = 0; n < _alpha.GetLength(1); n++)
              _alpha[m, n] = AlphaR1224ydZ[index++];
          break;

        case Fluid.R290:
          _mCount = 5; _nCount = 8;
          CriticalTemperature = 96.740 + PhysicsConstants.CelsiusToKelvinOffset;
          CriticalDensity = 220.478;
          CriticalPressure = 4251.16;
          _gasConstant = 8.3144621 / 44.0956;
          _refTemperature = PhysicsConstants.ToKelvin(0);
          _refDensity = 528.59; _refEnthalpy = 200; _refEntropy = 1.0;
          _ccp = CcpR290; _cps = CpsR290; _cts = CtsR290;
          MaxPressure = 2500; MinPressure = 250;
          _alpha = new double[_mCount, _nCount - 2];
          index = 0;
          for (int m = 0; m < _alpha.GetLength(0); m++)
            for (int n = 0; n < _alpha.GetLength(1); n++)
              _alpha[m, n] = AlphaR290[index++];
          break;

        default:
          _alpha = new double[0, 0];
          _ccp = _cps = _cts = Array.Empty<double>();
          break;
      }

      FitSaturationCurves();
    }

    #endregion

    #region PVT relations

    /// <summary>
    /// Gets the pressure [kPa] from the temperature [K] and density [kg/m³].
    /// </summary>
    /// <param name="temperature">Temperature [K]</param>
    /// <param name="density">Density [kg/m³]</param>
    /// <returns>Pressure [kPa]</returns>
    public double GetPressureFromTemperatureAndDensity(double temperature, double density)
    {
      double tau = CriticalTemperature / temperature;
      double rho = density / CriticalDensity;

      double pressure = 0;
      for (int m = _alpha.GetLength(0) - 1; 0 <= m; m--)
      {
        double buff = 0;
        for (int n = _alpha.GetLength(1) - 1; 0 <= n; n--)
          buff = buff * rho + _alpha[m, n];
        pressure = pressure * tau + buff;
      }
      return pressure * rho * rho + density * _gasConstant * temperature;
    }

    /// <summary>
    /// Gets the density [kg/m³] from the pressure [kPa] and temperature [K]
    /// using Newton's method. The density argument serves as the initial guess
    /// and is updated in place.
    /// </summary>
    /// <remarks>
    /// This is an internal method used during convergence iterations.
    /// No range validation is performed here because intermediate values
    /// during convergence may temporarily exceed the valid range.
    /// </remarks>
    private void GetDensityFromPressureAndTemperatureInternal(
        double pressure, double temperature, ref double density)
    {
      Roots.ErrorFunction eFnc = dns =>
          pressure - GetPressureFromTemperatureAndDensity(temperature, dns);

      Roots.ErrorFunction eFncD = dns =>
      {
        double tau = CriticalTemperature / temperature;
        double rho = dns / CriticalDensity;
        double dPdRho = 0;
        int len = _alpha.GetLength(1) + 1;
        for (int m = _alpha.GetLength(0) - 1; 0 <= m; m--)
        {
          double buff = 0;
          for (int n = len; 2 <= n; n--)
            buff = buff * rho + n * _alpha[m, n - 2];
          dPdRho = dPdRho * tau + buff;
        }
        dPdRho *= rho;
        return -(dPdRho / CriticalDensity + _gasConstant * temperature);
      };

      try
      {
        // 2026.05.27: Tightened tolerance 1e-3 → 1e-4 and extended max iterations to 30.
        // At this level the COP error of a standard refrigeration cycle is <0.1% (<0.05% for most refrigerants).
        // Thanks to the ×1.10 bump of the Rackett initial guess (in GetGibbsEnergyDifference)
        // and single-phase state searches starting from saturation, the density iteration
        // always starts on the stable branch, so plain Newton is sufficient.
        density = Roots.Newton(eFnc, eFncD, density, 1e-4, 1e-4, 30);
      }
      catch (Exception e)
      {
        throw new PopoloNumericalException(
            "GetDensityFromPressureAndTemperatureInternal",
            $"Newton iteration failed. pressure={pressure} kPa, "
            + $"temperature={temperature} K, density={density} kg/m³."
            + Environment.NewLine + e.Message);
      }
    }

    #endregion

    #region Saturation state calculation

    /// <summary>
    /// Gets the saturated liquid density [kg/m³], saturated vapor density [kg/m³],
    /// and saturation temperature [K] from the saturation pressure [kPa].
    /// </summary>
    /// <param name="pressure">Saturation pressure [kPa]</param>
    /// <param name="saturatedLiquidDensity">Saturated liquid density [kg/m³]</param>
    /// <param name="saturatedVaporDensity">Saturated vapor density [kg/m³]</param>
    /// <param name="saturatedTemperature">Saturation temperature [K]</param>
    /// <exception cref="PopoloOutOfRangeException">
    /// Thrown when the pressure is outside the valid range [MinPressure, MaxPressure].
    /// </exception>
    public void GetSaturatedPropertyFromPressure(double pressure,
        out double saturatedLiquidDensity, out double saturatedVaporDensity,
        out double saturatedTemperature)
    {
      ValidatePressure(pressure);

      //Fast path: evaluate the saturation curves fitted at construction
      //(ValidatePressure already restricts the pressure to the fitted range)
      if (_satFitValid)
      {
        saturatedTemperature = SolveFittedSaturationTemperature(pressure);
        GetFittedSaturatedDensities(saturatedTemperature,
          out saturatedLiquidDensity, out saturatedVaporDensity);
        return;
      }

      GetSaturatedPropertyFromPressureExact(pressure,
        out saturatedLiquidDensity, out saturatedVaporDensity, out saturatedTemperature);
    }

    /// <summary>
    /// Gets the saturation state from the pressure [kPa] by solving the
    /// equal-Gibbs-energy condition (Eq.17 in the reference).
    /// </summary>
    /// <param name="pressure">Pressure [kPa]</param>
    /// <param name="saturatedLiquidDensity">Saturated liquid density [kg/m³]</param>
    /// <param name="saturatedVaporDensity">Saturated vapor density [kg/m³]</param>
    /// <param name="saturatedTemperature">Saturation temperature [K]</param>
    private void GetSaturatedPropertyFromPressureExact(double pressure,
        out double saturatedLiquidDensity, out double saturatedVaporDensity,
        out double saturatedTemperature)
    {
      double sld = 0, svd = 0;
      Roots.ErrorFunction eFnc = tmp =>
          GetGibbsEnergyDifference(tmp, pressure, out sld, out svd);

      //Estimate the initial saturation temperature (cubic polynomial approximation)
      double pr = pressure / CriticalPressure;
      double ts = _cts[0];
      for (int i = 1; i < _cts.Length; i++) ts = ts * pr + _cts[i];
      //2020.02.23: -2K offset to handle convergence failures with R410A at high pressure
      ts -= 2.0;

      try
      {
        // 2026.05.27: Tightened tolerances (1e-5, 1e-3) → (1e-5, 1e-4) and extended max iterations to 30.
        // At this level the COP error is <0.1%. The errTol of 1e-5 (Gibbs diff, kJ/kg) is unchanged.
        saturatedTemperature = Roots.Newton(eFnc, ts, 1e-5, 1e-5, 1e-4, 30);
      }
      catch (Exception e)
      {
        throw new PopoloNumericalException(
            "GetSaturatedPropertyFromPressure",
            $"Newton iteration failed. pressure={pressure} kPa."
            + Environment.NewLine + e.Message);
      }
      saturatedLiquidDensity = sld;
      saturatedVaporDensity = svd;
    }

    /// <summary>
    /// Gets the saturated liquid density [kg/m³], saturated vapor density [kg/m³],
    /// and saturation pressure [kPa] from the saturation temperature [K].
    /// </summary>
    /// <param name="temperature">Saturation temperature [K]</param>
    /// <param name="saturatedLiquidDensity">Saturated liquid density [kg/m³]</param>
    /// <param name="saturatedVaporDensity">Saturated vapor density [kg/m³]</param>
    /// <param name="saturatedPressure">Saturation pressure [kPa]</param>
    /// <exception cref="PopoloOutOfRangeException">
    /// Thrown when the temperature is below absolute zero.
    /// </exception>
    public void GetSaturatedPropertyFromTemperature(double temperature,
        out double saturatedLiquidDensity, out double saturatedVaporDensity,
        out double saturatedPressure)
    {
      ValidateTemperature(temperature);

      //Fast path: evaluate the saturation curves fitted at construction
      if (_satFitValid && _satFitTMin <= temperature && temperature <= _satFitTMax)
      {
        saturatedPressure = GetFittedSaturationPressure(temperature);
        GetFittedSaturatedDensities(temperature,
          out saturatedLiquidDensity, out saturatedVaporDensity);
        return;
      }

      GetSaturatedPropertyFromTemperatureExact(temperature,
        out saturatedLiquidDensity, out saturatedVaporDensity, out saturatedPressure);
    }

    /// <summary>
    /// Gets the saturation state from the temperature [K] by solving the
    /// equal-Gibbs-energy condition (Eq.17 in the reference).
    /// </summary>
    /// <param name="temperature">Saturation temperature [K]</param>
    /// <param name="saturatedLiquidDensity">Saturated liquid density [kg/m³]</param>
    /// <param name="saturatedVaporDensity">Saturated vapor density [kg/m³]</param>
    /// <param name="saturatedPressure">Saturation pressure [kPa]</param>
    private void GetSaturatedPropertyFromTemperatureExact(double temperature,
        out double saturatedLiquidDensity, out double saturatedVaporDensity,
        out double saturatedPressure)
    {
      double sld = 0, svd = 0;
      Roots.ErrorFunction eFnc = pres =>
          GetGibbsEnergyDifference(temperature, pres, out sld, out svd);

      //Estimate the initial saturation pressure (cubic polynomial approximation)
      double tr = temperature / CriticalTemperature;
      double ps = _cps[0];
      for (int i = 1; i < _cps.Length; i++) ps = ps * tr + _cps[i];

      try
      {
        // 2026.05.27: Tightened tolerances (1e-4, 1e-3) → (1e-5, 1e-4) and extended max iterations to 30.
        // At this level the COP error is <0.1%.
        saturatedPressure = Roots.Newton(eFnc, ps, 1e-5, 1e-5, 1e-4, 30);
      }
      catch (Exception e)
      {
        throw new PopoloNumericalException(
            "GetSaturatedPropertyFromTemperature",
            $"Newton iteration failed. temperature={temperature} K."
            + Environment.NewLine + e.Message);
      }
      saturatedLiquidDensity = sld;
      saturatedVaporDensity = svd;
    }

    /// <summary>
    /// Computes the difference in residual Gibbs free energy between liquid and vapor phases.
    /// Used as the error function for saturation property convergence (Eq.17 in the reference).
    /// </summary>
    private double GetGibbsEnergyDifference(
        double temperature, double pressure,
        out double liquidDensity, out double vaporDensity)
    {
      double tr = temperature / CriticalTemperature;

      //Estimate the initial liquid density with the Rackett equation (Eq.15)
      double rc = CriticalPressure / (CriticalDensity * CriticalTemperature * _gasConstant);
      double rhol = 1.0 / (Math.Pow(rc, 1.0 + Math.Pow(1.0 - tr, 2.0 / 7.0))
          / CriticalPressure * _gasConstant * CriticalTemperature);
      // 2026-05-27: Rackett tends to under-predict sat-liquid density and for
      // some fluids (e.g., R410A at T = 278 K) falls inside the polynomial EOS
      // spinodal region (dP/dρ < 0), making Newton drift toward the wrong root.
      // Bump by 10% so the initial guess sits safely on the stable liquid branch;
      // Newton then descends monotonically to the sat-liquid root.
      rhol *= 1.10;
      GetDensityFromPressureAndTemperatureInternal(pressure, temperature, ref rhol);
      liquidDensity = rhol;

      //Estimate the initial vapor density with the ideal gas equation (Eq.1)
      double rhov = pressure / (temperature * _gasConstant);
      GetDensityFromPressureAndTemperatureInternal(pressure, temperature, ref rhov);
      vaporDensity = rhov;

      //Compute the Gibbs energy difference (Eq.17)
      double gL = GetResidualGibbsFreeEnergy(temperature, liquidDensity);
      double gV = GetResidualGibbsFreeEnergy(temperature, vaporDensity);
      return gL - gV + _gasConstant * temperature * Math.Log(liquidDensity / vaporDensity);
    }

    /// <summary>
    /// Computes the residual Gibbs free energy [kJ] (Eq.13 in the reference).
    /// </summary>
    private double GetResidualGibbsFreeEnergy(double temperature, double density)
    {
      double tau = CriticalTemperature / temperature;
      double rho = density / CriticalDensity;

      double gr = 0;
      int len = _alpha.GetLength(1) + 1;
      for (int m = _alpha.GetLength(0) - 1; 0 <= m; m--)
      {
        double buff = 0;
        for (int n = len; 2 <= n; n--)
          buff = buff * rho + _alpha[m, n - 2] * n / (n - 1.0);
        gr = gr * tau + buff;
      }
      gr *= rho;
      return gr / CriticalDensity;
    }

    #endregion

    #region Saturation curve fit

    /// <summary>
    /// Fits fast closed-form saturation curves against this model's own
    /// equal-Gibbs-energy solution (Eq.17) at construction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Saturation state queries are by far the most frequent refrigerant
    /// property calls in HVAC models, and each exact solution nests Newton
    /// iterations (saturation condition × liquid/vapor density searches).
    /// This method samples the exact solution over the validated pressure
    /// range and fits a four-term Wagner equation for the saturation
    /// pressure, ln(P/Pc) = (a1·τ + a2·τ^1.5 + a3·τ^3 + a4·τ^6) / Tr, and
    /// τ-power series for the saturated liquid and vapor densities
    /// (τ = 1 − T/Tc). The fits are a memoization of the model itself — not
    /// an additional physical approximation source — and their consistency
    /// with the exact solution is verified here; when it is worse than 0.1%
    /// (pressure) or 0.3% (densities), the fit is discarded and all queries
    /// keep using the exact solution.
    /// </para>
    /// <para>
    /// When the exact solution fails at the very edge of the validated range
    /// (it can be fragile there), the sampled range is shrunk slightly; the
    /// resulting curves are then extrapolated over the remaining sliver,
    /// which also makes the boundary usable where the exact solution is not.
    /// Temperature queries outside the fitted range fall back to the exact
    /// solution.
    /// </para>
    /// </remarks>
    private void FitSaturationCurves()
    {
      _satFitValid = false;
      if (_alpha.Length == 0 || _cts.Length == 0) return;

      //Determine the temperature range to fit from the initial-estimate polynomial
      //(the exact solution can be fragile just at the validated range boundary)
      double tMin = _satFitTMin = EstimateSaturationTemperature(MinPressure);
      double tMax = _satFitTMax = EstimateSaturationTemperature(MaxPressure);
      if (CriticalTemperature <= tMax) return;

      //Sample the exact saturation solution; when it fails at a range edge,
      //shrink the range slightly toward the center and retry
      const int N = 64;
      double[] ts = new double[N], ps = new double[N], lds = new double[N], vds = new double[N];
      bool sampled = false;
      for (int trial = 0; trial < 5 && !sampled; trial++)
      {
        try
        {
          for (int i = 0; i < N; i++)
          {
            ts[i] = tMin + (tMax - tMin) * i / (N - 1);
            GetSaturatedPropertyFromTemperatureExact(ts[i], out lds[i], out vds[i], out ps[i]);
          }
          sampled = true;
        }
        catch (PopoloNumericalException)
        {
          double shrink = 0.02 * (tMax - tMin);
          tMin += shrink;
          tMax -= shrink;
        }
      }
      if (!sampled) return; //keep the exact path

      //Least-squares fits (normal equations)
      double[] bss = new double[5];
      var fits = new (double[] coef, Func<double, double[]> basis, Func<int, double> target)[]
      {
        (_satWagner,
          tau => { double sqt = Math.Sqrt(tau); double t3 = tau * tau * tau;
            bss[0] = tau; bss[1] = tau * sqt; bss[2] = t3; bss[3] = t3 * t3; return bss; },
          i => Math.Log(ps[i] / CriticalPressure) * (ts[i] / CriticalTemperature)),
        (_satLiqDens,
          tau => { double cb = Math.Cbrt(tau);
            bss[0] = cb; bss[1] = cb * cb; bss[2] = tau; bss[3] = tau * cb; return bss; },
          i => lds[i] / CriticalDensity - 1.0),
        (_satVapDens,
          tau => { double cb = Math.Cbrt(tau); double t3 = tau * tau * tau;
            bss[0] = cb; bss[1] = Math.Pow(tau, 5.0 / 6.0); bss[2] = tau * Math.Sqrt(tau); bss[3] = t3; bss[4] = t3 * t3; return bss; },
          i => Math.Log(vds[i] / CriticalDensity)),
      };
      foreach (var (coef, basis, target) in fits)
      {
        int nc = coef.Length;
        Matrix ata = new Matrix(nc, nc);
        Vector atb = new Vector(nc);
        for (int i = 0; i < N; i++)
        {
          double[] b = basis(1.0 - ts[i] / CriticalTemperature);
          double y = target(i);
          for (int j = 0; j < nc; j++)
          {
            for (int k = 0; k < nc; k++) ata[j, k] += b[j] * b[k];
            atb[j] += b[j] * y;
          }
        }
        LinearAlgebraOperations.SolveLinearEquations(ata, atb);
        for (int j = 0; j < nc; j++) coef[j] = atb[j];
      }

      //Verify the consistency with the exact solution before enabling the fast path
      for (int i = 0; i < N; i++)
      {
        GetFittedSaturatedDensities(ts[i], out double ld, out double vd);
        if (0.001 < Math.Abs(GetFittedSaturationPressure(ts[i]) - ps[i]) / ps[i] ||
            0.003 < Math.Abs(ld - lds[i]) / lds[i] ||
            0.003 < Math.Abs(vd - vds[i]) / vds[i])
        {
          return;
        }
      }
      _satFitValid = true;
    }

    /// <summary>Estimates the saturation temperature [K] with the cubic initial-estimate polynomial.</summary>
    /// <param name="pressure">Pressure [kPa]</param>
    /// <returns>Estimated saturation temperature [K]</returns>
    private double EstimateSaturationTemperature(double pressure)
    {
      double pr = pressure / CriticalPressure;
      double t = _cts[0];
      for (int i = 1; i < _cts.Length; i++) t = t * pr + _cts[i];
      return t;
    }

    /// <summary>Gets the saturation pressure [kPa] from the fitted Wagner equation.</summary>
    /// <param name="temperature">Saturation temperature [K]</param>
    /// <returns>Saturation pressure [kPa]</returns>
    private double GetFittedSaturationPressure(double temperature)
    {
      double tr = temperature / CriticalTemperature;
      double tau = 1.0 - tr;
      double sqt = Math.Sqrt(tau);
      double t3 = tau * tau * tau;
      return CriticalPressure * Math.Exp(
        (_satWagner[0] * tau + _satWagner[1] * tau * sqt + _satWagner[2] * t3 + _satWagner[3] * t3 * t3) / tr);
    }

    /// <summary>Solves the fitted Wagner equation for the saturation temperature [K] with Newton's method.</summary>
    /// <param name="pressure">Pressure [kPa]</param>
    /// <returns>Saturation temperature [K]</returns>
    private double SolveFittedSaturationTemperature(double pressure)
    {
      //Initial estimate (cubic polynomial approximation)
      double pr = pressure / CriticalPressure;
      double t = _cts[0];
      for (int i = 1; i < _cts.Length; i++) t = t * pr + _cts[i];

      double lnPr = Math.Log(pr);
      for (int i = 0; i < 20; i++)
      {
        double tr = t / CriticalTemperature;
        double tau = Math.Max(0, 1.0 - tr);
        double sqt = Math.Sqrt(tau);
        double t3 = tau * tau * tau;
        double g = _satWagner[0] * tau + _satWagner[1] * tau * sqt + _satWagner[2] * t3 + _satWagner[3] * t3 * t3;
        double gp = _satWagner[0] + 1.5 * _satWagner[1] * sqt + 3.0 * _satWagner[2] * tau * tau + 6.0 * _satWagner[3] * t3 * tau * tau;
        //d(g(τ)/Tr)/dT with τ = 1 - Tr
        double dfdt = (-(gp * tr) - g) / (tr * tr) / CriticalTemperature;
        double dt = (g / tr - lnPr) / dfdt;
        t -= dt;
        if (Math.Abs(dt) < 1e-6) break;
      }
      return t;
    }

    /// <summary>Gets the saturated liquid and vapor densities [kg/m³] from the fitted curves.</summary>
    /// <param name="temperature">Saturation temperature [K]</param>
    /// <param name="liquidDensity">Saturated liquid density [kg/m³]</param>
    /// <param name="vaporDensity">Saturated vapor density [kg/m³]</param>
    private void GetFittedSaturatedDensities(double temperature,
      out double liquidDensity, out double vaporDensity)
    {
      double tau = Math.Max(0, 1.0 - temperature / CriticalTemperature);
      double cb = Math.Cbrt(tau);
      liquidDensity = CriticalDensity * (1.0
        + _satLiqDens[0] * cb + _satLiqDens[1] * cb * cb + _satLiqDens[2] * tau + _satLiqDens[3] * tau * cb);
      double t3 = tau * tau * tau;
      vaporDensity = CriticalDensity * Math.Exp(
        _satVapDens[0] * cb + _satVapDens[1] * Math.Pow(tau, 5.0 / 6.0)
        + _satVapDens[2] * tau * Math.Sqrt(tau) + _satVapDens[3] * t3 + _satVapDens[4] * t3 * t3);
    }

    #endregion

    #region Property calculation from temperature and density

    /// <summary>
    /// Gets the specific enthalpy [kJ/kg] from the temperature [K] and density [kg/m³]
    /// (Eq.12 and Eq.20 in the reference).
    /// </summary>
    /// <param name="temperature">Temperature [K]</param>
    /// <param name="density">Density [kg/m³]</param>
    /// <returns>Specific enthalpy [kJ/kg]</returns>
    public double GetEnthalpyFromTemperatureAndDensity(double temperature, double density)
    {
      double h = GetResidualEnthalpy(temperature, density)
          - GetResidualEnthalpy(_refTemperature, _refDensity);
      return h + GetIntegralCp0(temperature) + _refEnthalpy;
    }

    /// <summary>
    /// Gets the specific entropy [kJ/(kg·K)] from the temperature [K] and density [kg/m³]
    /// (Eq.11 and Eq.18 in the reference).
    /// </summary>
    /// <param name="temperature">Temperature [K]</param>
    /// <param name="density">Density [kg/m³]</param>
    /// <returns>Specific entropy [kJ/(kg·K)]</returns>
    public double GetEntropyFromTemperatureAndDensity(double temperature, double density)
    {
      double s = GetResidualEntropy(temperature, density)
          - GetResidualEntropy(_refTemperature, _refDensity);
      s += _gasConstant * Math.Log(_refDensity / density);
      s += GetIntegralCp0T(temperature) - _gasConstant * Math.Log(temperature / _refTemperature);
      return s + _refEntropy;
    }

    /// <summary>
    /// Gets the specific internal energy [kJ/kg] from the temperature [K] and density [kg/m³]
    /// (Eq.22 in the reference).
    /// </summary>
    /// <param name="temperature">Temperature [K]</param>
    /// <param name="density">Density [kg/m³]</param>
    /// <returns>Specific internal energy [kJ/kg]</returns>
    public double GetInternalEnergyFromTemperatureAndDensity(double temperature, double density)
    {
      double p = GetPressureFromTemperatureAndDensity(temperature, density);
      return GetEnthalpyFromTemperatureAndDensity(temperature, density) - p / density;
    }

    /// <summary>
    /// Gets the isochoric (constant-volume) specific heat [kJ/(kg·K)]
    /// from the temperature [K] and density [kg/m³].
    /// </summary>
    /// <param name="temperature">Temperature [K]</param>
    /// <param name="density">Density [kg/m³]</param>
    /// <returns>Isochoric specific heat [kJ/(kg·K)]</returns>
    public double GetIsovolumetricSpecificHeatFromTemperatureAndDensity(
        double temperature, double density)
    {
      double tau = CriticalTemperature / temperature;
      double rho = density / CriticalDensity;

      double cv = 0;
      int len = _alpha.GetLength(1) + 1;
      for (int m = _alpha.GetLength(0) - 1; 2 <= m; m--)
      {
        double buff = 0;
        for (int n = len; 2 <= n; n--)
          buff = buff * rho + _alpha[m, n - 2] * m * (m + 1) / (1.0 - n);
        cv = cv * tau + buff;
      }
      cv *= rho * tau * tau;
      return cv / (CriticalDensity * temperature)
          + GetIsovolumetricHeatCapacityOfIdealGas(temperature);
    }

    /// <summary>
    /// Gets the isobaric (constant-pressure) specific heat [kJ/(kg·K)]
    /// from the temperature [K] and density [kg/m³].
    /// </summary>
    /// <param name="temperature">Temperature [K]</param>
    /// <param name="density">Density [kg/m³]</param>
    /// <returns>Isobaric specific heat [kJ/(kg·K)]</returns>
    public double GetIsobaricSpecificHeatFromTemperatureAndDensity(
        double temperature, double density)
    {
      double tau = CriticalTemperature / temperature;
      double rho = density / CriticalDensity;

      double cp1 = 0, cp2 = 0;
      int len = _alpha.GetLength(1) + 1;
      for (int m = _alpha.GetLength(0) - 1; 0 <= m; m--)
      {
        double buff1 = 0, buff2 = 0;
        for (int n = len; 2 <= n; n--)
        {
          if (m != 0) buff1 = buff1 * rho + _alpha[m, n - 2] * m;
          buff2 = buff2 * rho + _alpha[m, n - 2] * n;
        }
        if (m != 0) cp1 = cp1 * tau + buff1;
        cp2 = cp2 * tau + buff2;
      }
      cp1 = density * _gasConstant - cp1 * rho * rho * tau / temperature;
      cp2 = _gasConstant * temperature + cp2 * rho / CriticalDensity;

      double bf = cp1 / density;
      return GetIsovolumetricSpecificHeatFromTemperatureAndDensity(temperature, density)
          + temperature * bf * bf / cp2;
    }

    /// <summary>
    /// Gets the specific heat ratio (Cp/Cv) [-]
    /// from the temperature [K] and density [kg/m³].
    /// </summary>
    /// <param name="temperature">Temperature [K]</param>
    /// <param name="density">Density [kg/m³]</param>
    /// <returns>Specific heat ratio [-]</returns>
    public double GetSpecificHeatRatioFromTemperatureAndDensity(
        double temperature, double density)
    {
      double cv = GetIsovolumetricSpecificHeatFromTemperatureAndDensity(temperature, density);
      double cp = GetIsobaricSpecificHeatFromTemperatureAndDensity(temperature, density);
      return cp / cv;
    }

    /// <summary>Residual enthalpy Hr [kJ/kg] (Eq.12 in the reference).</summary>
    private double GetResidualEnthalpy(double temperature, double density)
    {
      double tau = CriticalTemperature / temperature;
      double rho = density / CriticalDensity;

      double hr = 0;
      int len = _alpha.GetLength(1) + 1;
      for (int m = _alpha.GetLength(0) - 1; 0 <= m; m--)
      {
        double buff = 0;
        for (int n = len; 2 <= n; n--)
          buff = buff * rho + _alpha[m, n - 2] * (n + m) / (n - 1.0);
        hr = hr * tau + buff;
      }
      hr *= rho;
      return hr / CriticalDensity;
    }

    /// <summary>Residual entropy Sr [kJ/(kg·K)] (Eq.11 in the reference).</summary>
    private double GetResidualEntropy(double temperature, double density)
    {
      double tau = CriticalTemperature / temperature;
      double rho = density / CriticalDensity;

      double sr = 0;
      int len = _alpha.GetLength(1) + 1;
      for (int m = _alpha.GetLength(0) - 1; 1 <= m; m--)
      {
        double buff = 0;
        for (int n = len; 2 <= n; n--)
          buff = buff * rho + _alpha[m, n - 2] * m / (n - 1.0);
        sr = sr * tau + buff;
      }
      sr *= rho * tau;
      return sr / (CriticalDensity * temperature);
    }

    /// <summary>
    /// Integral of ideal-gas isobaric specific heat Cp0 [kJ/kg]
    /// from refTemperature to temperature (Eq.21 in the reference).
    /// </summary>
    private double GetIntegralCp0(double temperature)
    {
      double tr = temperature / CriticalTemperature;
      double tr0 = _refTemperature / CriticalTemperature;
      double cp0 = 0, cp0r = 0;
      for (int i = _ccp.Length - 1; 0 <= i; i--)
      {
        cp0 = (cp0 + _ccp[i] / (i + 1) * CriticalTemperature) * tr;
        cp0r = (cp0r + _ccp[i] / (i + 1) * CriticalTemperature) * tr0;
      }
      return (cp0 - cp0r) * _gasConstant;
    }

    /// <summary>
    /// Integral of Cp0/T [kJ/(kg·K)] from refTemperature to temperature (Eq.19 in the reference).
    /// </summary>
    private double GetIntegralCp0T(double temperature)
    {
      double tr = temperature / CriticalTemperature;
      double tr0 = _refTemperature / CriticalTemperature;
      double cp0 = 0, cp0r = 0;
      for (int i = _ccp.Length - 1; 0 < i; i--)
      {
        cp0 = cp0 * tr + _ccp[i] / i;
        cp0r = cp0r * tr0 + _ccp[i] / i;
      }
      cp0 *= tr;
      cp0r *= tr0;
      return ((cp0 + _ccp[0] * Math.Log(temperature))
            - (cp0r + _ccp[0] * Math.Log(_refTemperature))) * _gasConstant;
    }

    /// <summary>Ideal-gas isobaric specific heat Cp0 [kJ/(kg·K)] (Eq.8 in the reference).</summary>
    private double GetIsobaricHeatCapacityOfIdealGas(double temperature)
    {
      double tr = temperature / CriticalTemperature;
      double cp0 = _ccp[_ccp.Length - 1];
      for (int i = _ccp.Length - 2; 0 <= i; i--) cp0 = cp0 * tr + _ccp[i];
      return cp0 * _gasConstant;
    }

    /// <summary>Ideal-gas isochoric specific heat Cv0 [kJ/(kg·K)].</summary>
    private double GetIsovolumetricHeatCapacityOfIdealGas(double temperature)
        => GetIsobaricHeatCapacityOfIdealGas(temperature) - _gasConstant;

    #endregion

    #region State calculation from pressure and enthalpy

    /// <summary>
    /// Gets the thermodynamic state from the pressure [kPa] and specific enthalpy [kJ/kg].
    /// </summary>
    /// <param name="pressure">Pressure [kPa]</param>
    /// <param name="enthalpy">Specific enthalpy [kJ/kg]</param>
    /// <param name="temperature">Temperature [K]</param>
    /// <param name="density">Density [kg/m³]</param>
    /// <param name="entropy">Specific entropy [kJ/(kg·K)]</param>
    /// <param name="internalEnergy">Specific internal energy [kJ/kg]</param>
    /// <exception cref="PopoloOutOfRangeException">
    /// Thrown when the pressure is outside the valid range [MinPressure, MaxPressure].
    /// </exception>
    /// <exception cref="PopoloNumericalException">
    /// Thrown when the Newton iteration fails to converge.
    /// </exception>
    public void GetStateFromPressureAndEnthalpy(
        double pressure, double enthalpy,
        out double temperature, out double density,
        out double entropy, out double internalEnergy)
    {
      ValidatePressure(pressure);

      //Compute the saturation state and determine the phase
      GetSaturatedPropertyFromPressure(pressure, out double rhoL, out double rhoV, out double tSat);
      double hl = GetEnthalpyFromTemperatureAndDensity(tSat, rhoL);
      double hv = GetEnthalpyFromTemperatureAndDensity(tSat, rhoV);

      Phase phase;
      if (enthalpy < hl) phase = Phase.Liquid;
      else if (hv < enthalpy) phase = Phase.Vapor;
      else phase = Phase.Equilibrium;

      //Two-phase region: weighted average by vapor-liquid ratio
      if (phase == Phase.Equilibrium)
      {
        temperature = tSat;
        double vRate = (enthalpy - hl) / (hv - hl);
        double lRate = 1.0 - vRate;
        density = 1.0 / (vRate / rhoV + lRate / rhoL);
        double sL = GetEntropyFromTemperatureAndDensity(tSat, rhoL);
        double sV = GetEntropyFromTemperatureAndDensity(tSat, rhoV);
        entropy = sL * lRate + sV * vRate;
        double uL = GetInternalEnergyFromTemperatureAndDensity(tSat, rhoL);
        double uV = GetInternalEnergyFromTemperatureAndDensity(tSat, rhoV);
        internalEnergy = uL * lRate + uV * vRate;
        return;
      }

      //Single-phase region: iterate on temperature with Newton's method
      temperature = phase == Phase.Liquid ? tSat - 3.0 : tSat + 3.0;
      density = phase == Phase.Liquid ? rhoL : rhoV;

      double dns = density;
      Roots.ErrorFunction eFnc = tmp =>
      {
        GetDensityFromPressureAndTemperatureInternal(pressure, tmp, ref dns);
        return enthalpy - GetEnthalpyFromTemperatureAndDensity(tmp, dns);
      };

      try
      {
        //2023.04.11: Increased from 10 to 15 iterations because 10 was not enough in some cases
        //2026.05.27: Tightened tolerance 1e-3 → 1e-4 (kJ/kg, K) and extended max iterations to 30
        //            (COP error <0.1%)
        temperature = Roots.Newton(eFnc, temperature, 1e-5, 1e-4, 1e-4, 30);
      }
      catch (Exception e)
      {
        throw new PopoloNumericalException(
            "GetStateFromPressureAndEnthalpy",
            $"Newton iteration failed. pressure={pressure} kPa, enthalpy={enthalpy} kJ/kg."
            + Environment.NewLine + e.Message);
      }
      density = dns;
      entropy = GetEntropyFromTemperatureAndDensity(temperature, density);
      internalEnergy = GetInternalEnergyFromTemperatureAndDensity(temperature, density);
    }

    #endregion

    #region State calculation from pressure and entropy

    /// <summary>
    /// Gets the thermodynamic state from the pressure [kPa] and specific entropy [kJ/(kg·K)].
    /// </summary>
    /// <param name="pressure">Pressure [kPa]</param>
    /// <param name="entropy">Specific entropy [kJ/(kg·K)]</param>
    /// <param name="temperature">Temperature [K]</param>
    /// <param name="density">Density [kg/m³]</param>
    /// <param name="enthalpy">Specific enthalpy [kJ/kg]</param>
    /// <param name="internalEnergy">Specific internal energy [kJ/kg]</param>
    /// <exception cref="PopoloOutOfRangeException">
    /// Thrown when the pressure is outside the valid range [MinPressure, MaxPressure].
    /// </exception>
    /// <exception cref="PopoloNumericalException">
    /// Thrown when the Newton iteration fails to converge.
    /// </exception>
    public void GetStateFromPressureAndEntropy(
        double pressure, double entropy,
        out double temperature, out double density,
        out double enthalpy, out double internalEnergy)
    {
      ValidatePressure(pressure);

      GetSaturatedPropertyFromPressure(pressure, out double rhoL, out double rhoV, out double tSat);
      double sl = GetEntropyFromTemperatureAndDensity(tSat, rhoL);
      double sv = GetEntropyFromTemperatureAndDensity(tSat, rhoV);

      Phase phase;
      if (entropy < sl) phase = Phase.Liquid;
      else if (sv < entropy) phase = Phase.Vapor;
      else phase = Phase.Equilibrium;

      //Two-phase region: weighted average by vapor-liquid ratio
      if (phase == Phase.Equilibrium)
      {
        temperature = tSat;
        double vRate = (entropy - sl) / (sv - sl);
        double lRate = 1.0 - vRate;
        density = 1.0 / (vRate / rhoV + lRate / rhoL);
        double hL = GetEnthalpyFromTemperatureAndDensity(tSat, rhoL);
        double hV = GetEnthalpyFromTemperatureAndDensity(tSat, rhoV);
        enthalpy = hL * lRate + hV * vRate;
        double uL = GetInternalEnergyFromTemperatureAndDensity(tSat, rhoL);
        double uV = GetInternalEnergyFromTemperatureAndDensity(tSat, rhoV);
        internalEnergy = uL * lRate + uV * vRate;
        return;
      }

      //Single-phase region: iterate on temperature with Newton's method
      temperature = phase == Phase.Liquid ? tSat - 3.0 : tSat + 3.0;
      density = phase == Phase.Liquid ? rhoL : rhoV;

      double dns = density;
      Roots.ErrorFunction eFnc = tmp =>
      {
        GetDensityFromPressureAndTemperatureInternal(pressure, tmp, ref dns);
        return entropy - GetEntropyFromTemperatureAndDensity(tmp, dns);
      };

      try
      {
        //2026.05.27: Tightened tolerance 1e-3 → 1e-4 (kJ/(kg·K), K) and extended max iterations to 30
        //            (COP error <0.1%)
        //2026.07.06: Tightened only the entropy residual 1e-4 → 1e-6. A residual ds leaves
        //            an error of T·ds in h2, and for high-COP refrigerants with small
        //            compression work W=h2-h1 (R1234yf: W≈21 kJ/kg), ds=1e-4 is amplified
        //            to a COP error of ~0.15%. Quadratic convergence makes the extra cost about one iteration.
        temperature = Roots.Newton(eFnc, temperature, 1e-5, 1e-6, 1e-5, 30);
      }
      catch (Exception e)
      {
        throw new PopoloNumericalException(
            "GetStateFromPressureAndEntropy",
            $"Newton iteration failed. pressure={pressure} kPa, entropy={entropy} kJ/(kg·K)."
            + Environment.NewLine + e.Message);
      }
      density = dns;
      enthalpy = GetEnthalpyFromTemperatureAndDensity(temperature, density);
      internalEnergy = GetInternalEnergyFromTemperatureAndDensity(temperature, density);
    }

    #endregion

    #region State calculation from pressure and temperature

    /// <summary>
    /// Gets the thermodynamic state from the pressure [kPa] and temperature [K].
    /// </summary>
    /// <param name="pressure">Pressure [kPa]</param>
    /// <param name="temperature">Temperature [K]</param>
    /// <param name="entropy">Specific entropy [kJ/(kg·K)]</param>
    /// <param name="density">Density [kg/m³]</param>
    /// <param name="enthalpy">Specific enthalpy [kJ/kg]</param>
    /// <param name="internalEnergy">Specific internal energy [kJ/kg]</param>
    /// <remarks>
    /// In the two-phase region, pressure and temperature alone do not uniquely determine
    /// the thermodynamic state (Gibbs phase rule: F = C - P + 2 = 1 for a pure substance).
    /// In this case, the saturated liquid properties are returned as a convention.
    /// </remarks>
    /// <exception cref="PopoloOutOfRangeException">
    /// Thrown when the pressure is outside the valid range [MinPressure, MaxPressure].
    /// </exception>
    /// <exception cref="PopoloNumericalException">
    /// Thrown when the Newton iteration fails to converge.
    /// </exception>
    public void GetStateFromPressureAndTemperature(
        double pressure, double temperature,
        out double entropy, out double density,
        out double enthalpy, out double internalEnergy)
    {
      ValidatePressure(pressure);
      ValidateTemperature(temperature);

      GetSaturatedPropertyFromPressure(pressure, out double rhoL, out double rhoV, out double tSat);

      Phase phase;
      if (temperature < tSat) phase = Phase.Liquid;
      else if (tSat < temperature) phase = Phase.Vapor;
      else phase = Phase.Equilibrium;

      //Two-phase region: pressure and temperature alone cannot determine the vapor-liquid ratio, so return saturated liquid properties
      if (phase == Phase.Equilibrium)
      {
        density = rhoL;
        enthalpy = GetEnthalpyFromTemperatureAndDensity(tSat, rhoL);
        internalEnergy = GetInternalEnergyFromTemperatureAndDensity(tSat, rhoL);
        entropy = GetEntropyFromTemperatureAndDensity(tSat, rhoL);
        return;
      }

      density = phase == Phase.Liquid ? rhoL + 0.1 : rhoV - 0.1;

      double tmp = temperature;
      Roots.ErrorFunction eFnc = dns =>
          pressure - GetPressureFromTemperatureAndDensity(tmp, dns);

      try
      {
        //2026.05.27: Tightened tolerance 1e-3 → 1e-4 (kg/m³) and extended max iterations to 30.
        //            The initial density is based on the saturated liquid/vapor density, so it is always on the stable branch.
        density = Roots.Newton(eFnc, density, 1e-5, 1e-4, 1e-4, 30);
      }
      catch (Exception e)
      {
        throw new PopoloNumericalException(
            "GetStateFromPressureAndTemperature",
            $"Newton iteration failed. pressure={pressure} kPa, temperature={temperature} K."
            + Environment.NewLine + e.Message);
      }
      enthalpy = GetEnthalpyFromTemperatureAndDensity(temperature, density);
      internalEnergy = GetInternalEnergyFromTemperatureAndDensity(temperature, density);
      entropy = GetEntropyFromTemperatureAndDensity(temperature, density);
    }

    /// <summary>
    /// Gets the density [kg/m³] from the pressure [kPa] and temperature [K].
    /// </summary>
    /// <param name="pressure">Pressure [kPa]</param>
    /// <param name="temperature">Temperature [K]</param>
    /// <returns>Density [kg/m³]</returns>
    /// <exception cref="PopoloOutOfRangeException">
    /// Thrown when the pressure is outside the valid range [MinPressure, MaxPressure].
    /// </exception>
    public double GetDensityFromPressureAndTemperature(double pressure, double temperature)
    {
      ValidatePressure(pressure);
      ValidateTemperature(temperature);

      GetSaturatedPropertyFromPressure(pressure, out double rhoL, out double rhoV, out double satT);
      double rho = satT < temperature ? rhoV : rhoL;
      GetDensityFromPressureAndTemperatureInternal(pressure, temperature, ref rho);
      return rho;
    }

    #endregion

    #region Input validation

    /// <summary>Checks whether the pressure is within the applicable range.</summary>
    private void ValidatePressure(double pressure)
    {
      if (pressure < MinPressure || pressure > MaxPressure)
        throw new PopoloOutOfRangeException(
            "pressure", pressure, MinPressure, MaxPressure,
            $"Pressure is outside the valid range for {FluidType}.");
    }

    /// <summary>Checks whether the temperature is above absolute zero.</summary>
    private void ValidateTemperature(double temperature)
    {
      if (temperature <= 0)
        throw new PopoloOutOfRangeException(
            "temperature", temperature, 0.0, null,
            "Temperature in Kelvin must be positive.");
    }

    #endregion

  }
}
