/* WindowOptics.cs
 *
 * ANSI/ASHRAE Standard 140-2023 Informative Annex B6, Section B6.2 に
 * 規定された clear glass の入射角依存光学特性 (per-pane) を計算し、
 * Popolo の <c>Window.SetAngleDependence</c> 用 5 次多項式係数に
 * フィットする補助クラス。
 *
 * Popolo の polynomial 形式: P(x) = c_0·x + c_1·x² + c_2·x³ + c_3·x⁴ + c_4·x⁵
 *   (x = cos(θ_i), θ_i = 入射角)
 * 透過率 T(θ) = T_normal × P_T(cos θ)
 * 反射率 R(θ) = 1 - (1 - R_normal) × P_R(cos θ)   ← 「1 引いた量」を多項式化
 *   ⇒ P_R(cos θ) = (1 - R(θ)) / (1 - R_normal)
 *   こうすることで grazing (cos θ=0, R→1) と多項式 (x=0 で 0) が自然に整合する。
 * 制約: x=1 で P=1 (= 法線入射で multiplier 1)。
 *
 * §B6.2 の式 (Snell + Fresnel + Bouguer) で per-pane (n=1) の T(θ), R(θ) を
 * 計算し、最小二乗で 5 係数にフィットする。R は §B6.2 が界面平均
 * R = (RPERP + RPAR)/2 までしか定義していないので多重反射級数は付加しない。
 * 低-e ガラス (Case 660) には §B6.2 が適用できないと spec 明記 — 別アプローチ。
 */

using System;

namespace BESTEST_2023
{
  /// <summary>
  /// Std 140-2023 Annex B6.2 の clear glass 公式を Popolo の
  /// <c>SetAngleDependence</c> 用多項式係数に変換する。
  /// </summary>
  internal static class WindowOptics
  {
    #region Public entry points

    /// <summary>
    /// 屈折率 n と吸収係数 K, ガラス厚 TH から、per-pane の入射角依存
    /// 多項式係数 (Popolo <c>SetAngleDependence</c> 用) を計算する。
    /// 出力された係数で <see cref="Popolo.Core.Building.Envelope.Window.SetAngleDependence"/>
    /// を呼ぶと、§B6.2 の単板光学特性が再現される。
    /// </summary>
    /// <param name="indexOfRefraction">ガラスの屈折率 [-] (§B6.2 調整済 clear glass: 1.493。
    ///   仕様書 nomenclature の 1.526 は元のガラス材料値で Table B6-4 計算には使用しない)</param>
    /// <param name="extinctionCoefficient">吸収係数 [1/mm] (§B6.2 調整済 clear glass: 0.0337)</param>
    /// <param name="thicknessMm">ガラス厚 [mm] (§B6.2 調整済 clear glass: 3.048)</param>
    /// <param name="tauCoef">出力: T(θ)/T(0°) を P_T(x)=Σ c_j × x^(j+1) で表す係数 [5 個]</param>
    /// <param name="rhoCoef">出力: (1-R(θ))/(1-R(0°)) を P_R(x)=Σ c_j × x^(j+1) で表す係数 [5 個]
    ///   (Popolo は R(θ)=1-(1-R_normal)×P_R で復元するため、この形で保持する)</param>
    /// <param name="tauNormal">出力: T_normal = T(0°) [-]</param>
    /// <param name="rhoNormal">出力: R_normal = R(0°) [-]</param>
    public static void ComputeB62Coefficients(
        double indexOfRefraction,
        double extinctionCoefficient,
        double thicknessMm,
        out double[] tauCoef,
        out double[] rhoCoef,
        out double tauNormal,
        out double rhoNormal)
    {
      // サンプル点: 0°, 10°, ..., 80° (9 点)。
      // 90° は cos=0 で polynomial も target も 0 なので除外。
      const int N = 9;
      double[] cosArr = new double[N];
      double[] tArr   = new double[N];
      double[] rArr   = new double[N];

      for (int i = 0; i < N; i++)
      {
        double aoiDeg = i * 10.0;
        double aoi = aoiDeg * Math.PI / 180.0;
        cosArr[i] = Math.Cos(aoi);
        var (T, R) = SinglePaneOptics(aoi, indexOfRefraction, extinctionCoefficient, thicknessMm);
        tArr[i] = T;
        rArr[i] = R;
      }

      tauNormal = tArr[0];
      rhoNormal = rArr[0];

      // 透過率: T(θ)/T(0°) を直接フィット
      // 反射率: Popolo の Window.SetAngleDependence は R(θ) = 1 - (1 - R_normal) × P_R
      //   で復元するので、フィット対象は P_R(cosθ) = (1 - R(θ)) / (1 - R_normal)。
      //   grazing で R→1 ⇒ (1-R)→0 と多項式 (x=0 で 0) が整合する。
      double[] tShape = new double[N];
      double[] rShape = new double[N];
      double oneMinusRn = 1.0 - rhoNormal;
      for (int i = 0; i < N; i++)
      {
        tShape[i] = tArr[i] / tauNormal;
        rShape[i] = (1.0 - rArr[i]) / oneMinusRn;
      }

      // P(x) = c_0·x + c_1·x² + ... + c_4·x⁵ を最小二乗フィット
      // 制約 P(1)=1 (= Σc_j = 1) を高重みで強制
      tauCoef = FitConstrainedPolynomial(cosArr, tShape);
      rhoCoef = FitConstrainedPolynomial(cosArr, rShape);
    }

    /// <summary>
    /// ASHRAE 140-2023 Case 600 (clear double-pane) の per-pane 係数。
    /// §B6.2 が Table B6-4 を生成するときの **調整済** 単板パラメータ
    /// (n=1.493, K=0.0337/mm, TH=3.048mm) を使用する。これは元のガラス
    /// 材料値 (n=1.526, K=0.0196, TH=3.175 — 仕様書 nomenclature 記載値)
    /// ではなく、WINDOW 7 の単板法線入射値に合うようフィットされた値で、
    /// per-pane T_n=0.8345, R_n=0.0391 を再現する。Case 670 と同じ材料。
    /// </summary>
    public static (double[] tau, double[] rho) BESTESTClearGlass_C600 => ClearGlass.Value;

    /// <summary>
    /// ASHRAE 140-2023 Case 670 (clear single-pane) の per-pane 係数。
    /// Case 600 と同じ §B6.2 調整済単板パラメータ (n=1.493, K=0.0337, TH=3.048)。
    /// </summary>
    public static (double[] tau, double[] rho) BESTESTClearGlass_C670 => ClearGlass.Value;

    /// <summary>
    /// §B6.2 の調整済 clear glass 単板パラメータ。Case 600 / 670 共通。
    /// </summary>
    private static readonly Lazy<(double[], double[])> ClearGlass = new(() =>
    {
      ComputeB62Coefficients(1.493, 0.0337, 3.048, out var t, out var r, out _, out _);
      return (t, r);
    });

    /// <summary>
    /// ASHRAE 140-2023 Case 660 outer Low-E pane の per-pane 角度依存係数と法線入射値。
    /// </summary>
    /// <remarks>
    /// 仕様書 Table 7-18 footnote (i) は §B6.2 (Snell+Fresnel+Bouguer) では
    /// coated glass を記述できないと明記。代わりに Table 7-19 (双板の角度依存値)
    /// を Popolo の matrix model + inner pane モデルとの組合せ式から逆解析して
    /// per-pane outer Low-E 角度依存値を導出し、5 次多項式に fit。
    ///
    /// 逆解析手順:
    ///   双板組合せ:
    ///     T_d   = T_o · T_i / (1 - R_o_B · R_i)
    ///     R_F_d = R_o_F + T_o² · R_i / (1 - R_o_B · R_i)
    ///     R_B_d = R_i + T_i² · R_o_B / (1 - R_o_B · R_i)
    ///   inner pane (T_i, R_i) は Popolo の clear glass モデル (BESTESTClearGlass_C600
    ///   多項式 + R_normal=0.075 spec input) で計算。
    ///   各角度の (T_d, R_F_d, R_B_d) ∈ Table 7-19 から (T_o, R_o_F, R_o_B) を解析的に解く。
    ///
    /// 法線入射値: 逆解析結果を使用 (spec Table 7-17 値 0.452/0.359/0.397 とは
    /// 1〜4% 程度乖離するが、Popolo matrix model 経由で Table 7-19 双板値を
    /// 再現する整合性を優先)。Hemis T (= 拡散透過率) は仕様 0.329 と一致。
    /// </remarks>
    public static (double[] tauF, double[] tauB, double[] rhoF, double[] rhoB,
                   double tauNormalF, double tauNormalB,
                   double rhoNormalF, double rhoNormalB) BESTESTLowEGlass_C660_Outer => (
        // T 多項式 (F/B 同値; Helmholtz 相反性)
        tauF: new[] { 1.693537, 4.652851, -15.906382, 16.124045, -5.564052 },
        tauB: new[] { 1.693537, 4.652851, -15.906382, 16.124045, -5.564052 },
        // R 多項式 (F/B 非対称; Low-E coating の効果)
        rhoF: new[] { 4.407546, -7.485958, 5.018595, 0.038946, -0.979129 },
        rhoB: new[] { -3.359753, 35.594060, -83.480546, 79.550575, -27.304336 },
        // 法線入射値 (逆解析結果)
        tauNormalF: 0.45863, tauNormalB: 0.45863,
        rhoNormalF: 0.36376, rhoNormalB: 0.38222
    );

    #endregion

    #region §B6.2 single pane calculation (Snell + Fresnel + Bouguer)

    /// <summary>
    /// 入射角 <paramref name="aoi"/> [rad] に対する単板 (n=1) 透過率と反射率。
    /// §B6.2 公式に厳密に従う (§B6.2 は R に対して多重反射級数を定義していないので付加しない):
    ///   Snell:    AOR = arcsin(sin(AOI) / n_g)
    ///   Fresnel:  RPERP = sin²(AOR-AOI)/sin²(AOR+AOI), RPAR = tan²/tan²
    ///             R = (RPERP + RPAR) / 2     ← §B6.2 の R 定義はここまで
    ///   Bouguer:  L = TH / cos(AOR), Tabs = exp(-K · L)
    ///   透過率:   Tr = 0.5 · [(1-RPERP)/(1+RPERP) + (1-RPAR)/(1+RPAR)]   (= 各偏光で計算してから平均)
    ///             T = Tr · Tabs
    /// </summary>
    private static (double T, double R) SinglePaneOptics(
        double aoi, double n_g, double K, double TH)
    {
      if (aoi >= 0.5 * Math.PI - 1e-9) return (0.0, 1.0);   // 90° = grazing

      double sinAOI = Math.Sin(aoi);
      double sinAOR = sinAOI / n_g;
      double aor = Math.Asin(sinAOR);

      // Fresnel reflectance per polarization
      double rPerp, rPar;
      if (aoi < 1e-9)
      {
        // 法線入射 (degenerate Fresnel): R = ((n-1)/(n+1))² (両偏光同値)
        double r0 = (n_g - 1.0) / (n_g + 1.0);
        rPerp = rPar = r0 * r0;
      }
      else
      {
        double aMinus = aor - aoi;
        double aPlus  = aor + aoi;
        double sinM = Math.Sin(aMinus), sinP = Math.Sin(aPlus);
        rPerp = (sinM * sinM) / (sinP * sinP);
        // tan のオーバーフロー回避 (aPlus が π/2 近傍)
        if (Math.Abs(0.5 * Math.PI - aPlus) < 1e-9) rPar = 1.0;
        else
        {
          double tanM = Math.Tan(aMinus), tanP = Math.Tan(aPlus);
          rPar = (tanM * tanM) / (tanP * tanP);
        }
      }
      double rIface = 0.5 * (rPerp + rPar);

      // 単板 (n=1) の §B6.2 透過率: 各偏光で Tr = (1-R)/(1+R), 平均
      double trPerp = (1.0 - rPerp) / (1.0 + rPerp);
      double trPar  = (1.0 - rPar)  / (1.0 + rPar);
      double tr = 0.5 * (trPerp + trPar);

      // Bouguer 吸収 (n=1)
      double L = TH / Math.Cos(aor);
      double tAbs = Math.Exp(-K * L);

      double T = tr * tAbs;

      // §B6.2 の R は界面平均のみ。多重反射級数は仕様に無いので加えない。
      double R = rIface;

      return (T, R);
    }

    #endregion

    #region Polynomial fit

    /// <summary>
    /// データ点 (<paramref name="x"/>[i], <paramref name="y"/>[i]) を
    /// y(x) = c_0·x + c_1·x² + c_2·x³ + c_3·x⁴ + c_4·x⁵ で最小二乗フィット。
    /// 制約 y(1) = Σc_j = 1 を高重み (W=1000) で強制。
    /// </summary>
    private static double[] FitConstrainedPolynomial(double[] x, double[] y)
    {
      const int K = 5;
      int N = x.Length;
      const double W = 1000.0;       // 制約の重み

      // 行列 A[(N+1) × K] と b[N+1] を構築
      // A[i, j] = x[i]^(j+1) for i<N
      // A[N, j] = W (制約: Σ c_j × 1^(j+1) = Σ c_j = 1)
      double[,] A = new double[N + 1, K];
      double[] b = new double[N + 1];
      for (int i = 0; i < N; i++)
      {
        double xp = x[i];
        for (int j = 0; j < K; j++) { A[i, j] = xp; xp *= x[i]; }
        b[i] = y[i];
      }
      for (int j = 0; j < K; j++) A[N, j] = W;
      b[N] = W * 1.0;

      // 正規方程式: (Aᵀ A) c = Aᵀ b
      double[,] AtA = new double[K, K];
      double[] Atb = new double[K];
      for (int j = 0; j < K; j++)
      {
        for (int k = 0; k < K; k++)
        {
          double s = 0;
          for (int i = 0; i <= N; i++) s += A[i, j] * A[i, k];
          AtA[j, k] = s;
        }
        double t = 0;
        for (int i = 0; i <= N; i++) t += A[i, j] * b[i];
        Atb[j] = t;
      }

      return SolveGauss(AtA, Atb);
    }

    /// <summary>5×5 程度の正方系を部分ピボット付きガウス消去で解く。</summary>
    private static double[] SolveGauss(double[,] A, double[] b)
    {
      int n = b.Length;
      double[,] M = new double[n, n + 1];
      for (int i = 0; i < n; i++)
      {
        for (int j = 0; j < n; j++) M[i, j] = A[i, j];
        M[i, n] = b[i];
      }
      // 前進消去 + 部分ピボット
      for (int p = 0; p < n; p++)
      {
        int maxRow = p;
        double maxVal = Math.Abs(M[p, p]);
        for (int i = p + 1; i < n; i++)
        {
          if (Math.Abs(M[i, p]) > maxVal) { maxVal = Math.Abs(M[i, p]); maxRow = i; }
        }
        if (maxRow != p)
          for (int j = p; j <= n; j++) (M[p, j], M[maxRow, j]) = (M[maxRow, j], M[p, j]);

        for (int i = p + 1; i < n; i++)
        {
          double f = M[i, p] / M[p, p];
          for (int j = p; j <= n; j++) M[i, j] -= f * M[p, j];
        }
      }
      // 後退代入
      double[] x = new double[n];
      for (int i = n - 1; i >= 0; i--)
      {
        double s = M[i, n];
        for (int j = i + 1; j < n; j++) s -= M[i, j] * x[j];
        x[i] = s / M[i, i];
      }
      return x;
    }

    #endregion
  }
}
