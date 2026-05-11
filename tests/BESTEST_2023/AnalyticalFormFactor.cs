/* AnalyticalFormFactor.cs - BESTEST 2023 用 解析的形態係数ヘルパー
 *
 * Std 140-2023 標準箱型 (8m × 6m × 2.7m など) 単室・多室ケース用に、
 * 解析的形態係数を Popolo.Geometry.ViewFactor (parallel/perpendicular rectangles
 * 公式) で組み立てて MultiRoom.SetFormFactor に渡せる double[,] を返す。
 *
 * 対象: 直方体 (8×6×2.7) の各 room。窓は所属する壁 plane と co-planar として扱う。
 *   - MakeBuilding が生成する単室・直方体ケース (C600 系・C195-C320 など)
 *   - MakeSunZoneBuilding (C960): BackZone (8×6×2.7) と SunZone (8×2×2.7) の各 room
 * 対象外: C990 Ground Coupling (9 壁の特殊ジオメトリ)。
 *
 * アルゴリズム:
 *   1. 呼び出し側が classifier (Func) で各表面を 6 plane (床/天井/N/E/W/S) に分類。
 *   2. plane↔plane の F を ViewFactor の解析公式で計算。
 *   3. surface i (∈ plane_i) → surface j (∈ plane_j) の F:
 *        plane_i ≠ plane_j: F_{ij} = F_{plane_i→plane_j} × (A_j / A_plane_j)
 *        plane_i = plane_j: F_{ij} = 0 (co-planar)
 *      → 閉包則 (Σ_j F_ij = 1) と相反法則 (F_ij·A_i = F_ji·A_j) を維持。
 */

using System;
using System.Collections.Generic;
using Popolo.Core.Building.Envelope;
using Popolo.Core.Geometry;

namespace BESTEST_2023
{
  /// <summary>
  /// 解析的形態係数 (parallel/perpendicular rectangles) を組み立てて
  /// MultiRoom.SetFormFactor に渡すヘルパー。Std 140-2023 直方体 room 専用。
  /// </summary>
  internal static class AnalyticalFormFactor
  {
    // Plane index labels (Func 経由で呼び出し側がこの定数を使って分類する)
    public const int PL_FLOOR   = 0;
    public const int PL_CEILING = 1;
    public const int PL_N       = 2;
    public const int PL_E       = 3;
    public const int PL_W       = 4;
    public const int PL_S       = 5;
    private const int NUM_PLANES = 6;

    /// <summary>
    /// 直方体 room の解析形態係数行列を生成する。
    /// </summary>
    /// <param name="surfaces">対象 room の表面リスト (MultiRoom.GetRoomSurfaces 順)。</param>
    /// <param name="classify">各表面を <c>PL_FLOOR..PL_S</c> のいずれかに分類する関数。
    /// 各 plane の総面積はこの分類結果と <c>surfaces</c> の面積から自動集計する。</param>
    /// <param name="Lx">X 方向 (E-W) 長さ [m]。</param>
    /// <param name="Ly">Y 方向 (N-S) 長さ [m]。</param>
    /// <param name="Lz">Z 方向 (高さ) [m]。</param>
    /// <returns>N×N 形態係数行列。<see cref="Popolo.Core.Building.MultiRoom.SetFormFactor"/> にそのまま渡せる。</returns>
    public static double[,] ComputeBoxRoomFormFactor(
        IReadOnlyList<EnvelopeSurface> surfaces,
        Func<EnvelopeSurface, int> classify,
        double Lx, double Ly, double Lz)
    {
      int N = surfaces.Count;

      // 1. 各表面を 6 plane のどれかに分類 + 異常値検査
      int[] plane = new int[N];
      for (int i = 0; i < N; i++)
      {
        int p = classify(surfaces[i]);
        if (p < 0 || p >= NUM_PLANES)
          throw new InvalidOperationException(
              $"classify returned {p} for surface[{i}]; must be in [0, {NUM_PLANES}).");
        plane[i] = p;
      }

      // 2. plane ごとの実際の総面積を集計
      double[] planeArea = new double[NUM_PLANES];
      for (int i = 0; i < N; i++) planeArea[plane[i]] += surfaces[i].Area;

      // 3. plane↔plane の解析的形態係数行列 (理想ジオメトリ)
      double[,] FP = ComputePlaneToPlane(Lx, Ly, Lz);

      // 4. surface 行列への分配
      double[,] F = new double[N, N];
      for (int i = 0; i < N; i++)
      {
        for (int j = 0; j < N; j++)
        {
          if (i == j) { F[i, j] = 0; continue; }
          if (plane[i] == plane[j])
          {
            // 同一面上の表面は互いに見えない (co-planar)
            F[i, j] = 0;
          }
          else
          {
            // F_{i→j} = F_{plane_i→plane_j} × (A_j / A_plane_j)
            double pj = planeArea[plane[j]];
            F[i, j] = (pj > 0) ? FP[plane[i], plane[j]] * surfaces[j].Area / pj : 0;
          }
        }
      }

      return F;
    }

    /// <summary>
    /// MakeBuilding が生成する標準レイアウト (walls index: 0=床, 1=天井, 2=N, 3=E, 4=W, 5=S +
    /// 窓は所属壁を Incline.HorizontalAngle で判定) 用の分類関数を返す。
    /// </summary>
    /// <param name="walls">標準 6 壁配列。</param>
    /// <param name="windows">窓配列 (length 0/2)。</param>
    public static Func<EnvelopeSurface, int> StandardLayoutClassifier(Wall[] walls, Window[] windows)
    {
      return s =>
      {
        for (int i = 0; i < walls.Length; i++)
        {
          if (s.Component == walls[i])
          {
            return i switch
            {
              0 => PL_FLOOR,
              1 => PL_CEILING,
              2 => PL_N,
              3 => PL_E,
              4 => PL_W,
              5 => PL_S,
              _ => throw new InvalidOperationException(
                  $"StandardLayoutClassifier expects walls[0..5]; index {i} is unexpected.")
            };
          }
        }
        for (int i = 0; i < windows.Length; i++)
        {
          if (s.Component == windows[i])
          {
            var inc = windows[i].SurfaceF.Incline;
            if (inc == null)
              throw new InvalidOperationException(
                  $"Window[{i}] has no Incline; cannot classify into a wall plane.");
            return PlaneFromAzimuth(inc.HorizontalAngle, i);
          }
        }
        throw new InvalidOperationException(
            "Surface.Component is not in walls[] nor windows[] of standard layout.");
      };
    }

    /// <summary>
    /// 方位角 (HorizontalAngle 規約: S=0, E=-π/2, W=+π/2, N=±π) から 6 plane のうち
    /// 垂直 4 plane (N/E/W/S) を返す。
    /// </summary>
    private static int PlaneFromAzimuth(double az, int diagIndex)
    {
      const double tol = 0.1;
      if (Math.Abs(az) < tol)                                            return PL_S;
      if (Math.Abs(az + Math.PI / 2) < tol)                              return PL_E;
      if (Math.Abs(az - Math.PI / 2) < tol)                              return PL_W;
      if (Math.Abs(az - Math.PI) < tol || Math.Abs(az + Math.PI) < tol)  return PL_N;
      throw new InvalidOperationException(
          $"Window[{diagIndex}] azimuth {az:F3} does not match any cardinal direction.");
    }

    /// <summary>
    /// 6 plane (床/天井/N/E/W/S) 間の plane-to-plane 形態係数行列を ViewFactor 公式で計算する。
    /// 戻り行列 FP[i,j] = F_{plane_i → plane_j}。閉包則・相反法則を満たす。
    /// </summary>
    /// <remarks>
    /// ViewFactor の引数規約 (width=Y=共有辺, height=Z=面1 perp dim, depth=X=面2 perp dim) と
    /// BESTEST ジオメトリの対応を各ペアで明示。各 plane の面積は理想形状での総面積。
    /// </remarks>
    private static double[,] ComputePlaneToPlane(double Lx, double Ly, double Lz)
    {
      // 理想 plane 面積
      double[] planeArea = new double[NUM_PLANES];
      planeArea[PL_FLOOR]   = Lx * Ly;
      planeArea[PL_CEILING] = Lx * Ly;
      planeArea[PL_N]       = Lx * Lz;
      planeArea[PL_S]       = Lx * Lz;
      planeArea[PL_E]       = Ly * Lz;
      planeArea[PL_W]       = Ly * Lz;

      double[,] FP = new double[NUM_PLANES, NUM_PLANES];

      // (i, j) ペアに F_{i→j} を設定し、相反法則で F_{j→i} を埋める
      void SetPair(int i, int j, double f_ij)
      {
        FP[i, j] = f_ij;
        FP[j, i] = (planeArea[j] > 0) ? f_ij * planeArea[i] / planeArea[j] : 0;
      }

      // 床 ↔ 天井: parallel rectangles (Lx × Ly) at distance Lz
      SetPair(PL_FLOOR, PL_CEILING,
          ViewFactor.GetViewFactorParallelRectangles(Lx, Ly, Lz));

      // 床 ↔ N 壁: perpendicular, shared edge = Lx
      //   ViewFactor 規約: width=Y(共有)=Lx, height=Z(面1=N の perp)=Lz, depth=X(面2=床 の perp)=Ly
      double fN_floor = ViewFactor.GetViewFactorPerpendicularRectangles(Lx, Lz, Ly);
      SetPair(PL_N, PL_FLOOR, fN_floor);
      SetPair(PL_S, PL_FLOOR, fN_floor);            // 床↔S 壁 (床↔N と対称)
      SetPair(PL_N, PL_CEILING, fN_floor);          // 天井↔N (Z 鏡像で床↔N と同)
      SetPair(PL_S, PL_CEILING, fN_floor);          // 天井↔S

      // 床 ↔ E 壁: perpendicular, shared edge = Ly
      double fE_floor = ViewFactor.GetViewFactorPerpendicularRectangles(Ly, Lz, Lx);
      SetPair(PL_E, PL_FLOOR, fE_floor);
      SetPair(PL_W, PL_FLOOR, fE_floor);
      SetPair(PL_E, PL_CEILING, fE_floor);
      SetPair(PL_W, PL_CEILING, fE_floor);

      // N ↔ S 壁: parallel rectangles (Lx × Lz) at distance Ly
      SetPair(PL_N, PL_S,
          ViewFactor.GetViewFactorParallelRectangles(Lx, Lz, Ly));

      // E ↔ W 壁: parallel rectangles (Ly × Lz) at distance Lx
      SetPair(PL_E, PL_W,
          ViewFactor.GetViewFactorParallelRectangles(Ly, Lz, Lx));

      // N ↔ E 壁: perpendicular, shared vertical edge = Lz
      //   ViewFactor: width=Lz (共有), height=Lx (N の水平 perp), depth=Ly (E の水平 perp)
      double fN_E = ViewFactor.GetViewFactorPerpendicularRectangles(Lz, Lx, Ly);
      SetPair(PL_N, PL_E, fN_E);
      SetPair(PL_N, PL_W, fN_E);
      SetPair(PL_S, PL_E, fN_E);
      SetPair(PL_S, PL_W, fN_E);

      return FP;
    }
  }
}
