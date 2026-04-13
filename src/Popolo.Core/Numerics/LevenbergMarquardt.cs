//Original public domain version by B.Garbow, K.Hillstrom, J.More' 
//(Argonne National Laboratory, MINPACK project, March 1980)
//Tranlation to C# Language by E.Togashi 2015

using System;

using Popolo.Core.Exceptions;
using Popolo.Core.Numerics.LinearAlgebra;

namespace Popolo.Core.Numerics
{
  /// <summary>LevenbergMarquardt法による最小二乗法クラス</summary>
  /// <remarks>
  /// J.J. More, The Levenberg-Marquardt algorithm: implementation and theory
  /// Conference on Numerical Analysis University of Dundee Scotland
  /// June 28 - July 1, 1977
  /// </remarks>
  [Serializable]
  public class LevenbergMarquardt
  {

    #region delegate

    /// <summary>誤差関数</summary>
    /// <param name="inputs">入力ベクトル</param>
    /// <param name="outputs">出力ベクトル</param>
    public delegate void ErrorFunction(IVector inputs, ref IVector outputs);

    #endregion

    #region インスタンス変数

    /// <summary>機械イプシロン</summary>
    private static readonly double MECH_EPS;

    /// <summary>誤差関数</summary>
    private ErrorFunction eFnc;

    /// <summary>許容誤差</summary>
    private double gtol, ftol, xtol;

    /// <summary>計算領域</summary>
    private IVector wa1, wa2, wa3, wa4, diag, qtf;

    /// <summary>入力ベクトル</summary>
    private IVector? inputs;

    /// <summary>出力ベクトル</summary>
    private IVector outputs;

    /// <summary>誤差関数評価回数上限</summary>
    private int maxfev;

    /// <summary>ヤコビアン</summary>
    private IMatrix fjac;

    /// <summary>ピボット配列</summary>
    private int[] ipvt;

    /// <summary>計算状態</summary>
    private int info = 0;

    /// <summary>数値微分用微小値</summary>
    private double epsfcn;

    #endregion

    #region プロパティ

    /// <summary>関数の数を取得する</summary>
    public int NumberOfFunctions { get; private set; }

    /// <summary>状態の数を取得する</summary>
    public int NumberOfVariables { get; private set; }

    /// <summary>誤差関数評価回数を取得する</summary>
    public int NumberOfFunctionEvaluates { get; private set; }

    /// <summary>誤差関数評価回数上限を設定・取得する</summary>
    public int MaxNumberOfFunctionEvaluate
    {
      get { return maxfev; }
      set { if (0 < value) maxfev = value; }
    }

    /// <summary>収束計算成功の真偽を取得する</summary>
    public bool SuccessfullyConverged { get { return (1 <= info && info <= 3); } }

    /// <summary>数値微分用微小値を設定・取得する</summary>
    public double Epsilon
    {
      get { return epsfcn; }
      set { if (0 < value) epsfcn = value; }
    }

    /// <summary>計算状況を取得する</summary>
    public string Status
    {
      get
      {
        switch (info)
        {
          case 1:
            return "both actual and predicted relative reductions "
              + "in the sum of squares are at most ftol.";
          case 2:
            return "relative error between two consecutive iterates "
              + "is at most xtol.";
          case 3:
            return "conditions for info = 1 and info = 2 both hold.";
          case 4:
            return "the cosine of the angle between fvec and any column of "
              + "the jacobian is at most gtol in absolute value.";
          case 5:
            return "number of calls to fcn has reached or exceeded maxfev.";
          case 6:
            return "ftol is too small. no further reduction "
              + "in the sum of squares is possible.";
          case 7:
            return "xtol is too small. no further improvement "
              + "in the approximate solution x is possible.";
          case 8:
            return "gtol is too small. fvec is orthogonal to the "
              + "columns of the jacobian to machine precision.";
          default:
            return "Successfully Converged";
        }
      }
    }

    /// <summary>ヤコビアン勾配に関する許容誤差を設定・取得する</summary>
    public double GradientTolerance
    {
      get { return gtol; }
      set { if (0 < value) gtol = value; }
    }

    /// <summary>出力変化に関する許容誤差を設定・取得する</summary>
    public double OutputErrorTolerance
    {
      get { return ftol; }
      set { if (0 < value) ftol = value; }
    }

    /// <summary>入力変化に関する許容誤差を設定・取得する</summary>
    public double InputErrorTolerance
    {
      get { return xtol; }
      set { if (0 < value) xtol = value; }
    }

    /// <summary>出力ベクトルを取得する</summary>
    public ImmutableIVector Outputs { get { return outputs; } }

    /// <summary>入力ベクトルを取得する</summary>
    public ImmutableIVector? Inputs { get { return inputs; } }

    /// <summary>最大反復回数を設定・取得する</summary>
    public uint MaxIteration { get; set; }

    #endregion

    #region publicメソッド

    static LevenbergMarquardt()
    {
      //機械イプシロン初期化
      MECH_EPS = 1.0;
      while (true)
      {
        if (1.0 + MECH_EPS <= 1.0)
        {
          MECH_EPS *= 2;
          break;
        }
        else MECH_EPS = MECH_EPS * 0.5;
      }
    }

    /// <summary>コンストラクタ</summary>
    /// <param name="eFnc">誤差関数</param>
    /// <param name="numberOfFunctions">誤差関数の式の数</param>
    /// <param name="numberOfVariables">変数の数</param>
    public LevenbergMarquardt
      (ErrorFunction eFnc, int numberOfFunctions, int numberOfVariables)
    {
      if (numberOfFunctions < numberOfVariables)
        throw new PopoloArgumentException(
          $"numberOfFunctions ({numberOfFunctions}) must be greater than or equal to numberOfVariables ({numberOfVariables}).",
          nameof(numberOfFunctions));

      this.eFnc = eFnc;
      epsfcn = gtol = ftol = xtol = 1e-6;
      this.maxfev = numberOfFunctions * 10000;
      this.NumberOfFunctions = numberOfFunctions;
      this.NumberOfVariables = numberOfVariables;
      this.MaxIteration = 1000;

      //計算に備えて計算領域を確保
      outputs = new Vector(numberOfFunctions);
      wa1 = new Vector(numberOfVariables);
      wa2 = new Vector(numberOfVariables);
      wa3 = new Vector(numberOfVariables);
      wa4 = new Vector(numberOfFunctions);
      diag = new Vector(numberOfVariables);
      qtf = new Vector(numberOfVariables);
      ipvt = new int[numberOfVariables];
      fjac = new Matrix(numberOfFunctions, numberOfVariables);
    }

    /// <summary>評価関数最小二乗和を最小化する入力ベクトルを求める</summary>
    /// <param name="inputs">入力ベクトル初期値</param>
    public void Minimize(ref IVector inputs)
    {
      info = 0;
      this.inputs = inputs;
      double factor = 100d;
      NumberOfFunctionEvaluates = 0;
      int m = NumberOfFunctions;
      int n = NumberOfVariables;
      double xnorm = 0.0;
      double delta = 0.0;

      //evaluate the function at the starting point and calculate its norm.
      eFnc(inputs, ref outputs);
      NumberOfFunctionEvaluates = 1;
      double fnorm = outputs.ComputeEuclideanNorm();

      //initialize levenberg-marquardt parameter and iteration counter.
      double par = 0;
      int iter = 1;

      //beginning of the outer loop.
      while (true)
      {
        //calculate the jacobian matrix.
        double eps = Math.Sqrt(Math.Max(epsfcn, MECH_EPS));
        for (int j = 0; j < NumberOfVariables; j++)
        {
          double tmp = inputs[j];
          double h = eps * Math.Abs(tmp);
          if (h == 0.0d) h = eps;
          inputs[j] = tmp + h;
          eFnc(inputs, ref wa4);
          inputs[j] = tmp;
          for (int i = 0; i < NumberOfFunctions; i++)
            fjac[i, j] = (wa4[i] - outputs[i]) / h;
        }
        NumberOfFunctionEvaluates += NumberOfFunctions;

        //compute the qr factorization of the jacobian.
        QrFac(ref fjac, ref wa1, ref wa2, ref wa3, ref ipvt);

        //on the first iteration,        
        if (iter == 1)
        {
          //scale according to the norms of the columns of the initial jacobian.
          for (int i = 0; i < n; i++)
          {
            diag[i] = wa2[i];
            if (wa2[i] == 0.0d) diag[i] = 1.0d;
            wa3[i] = diag[i] * inputs[i];
          }
          //calculate the norm of the scaled x and initialize the step bound delta.
          xnorm = wa3.ComputeEuclideanNorm();
          delta = factor * xnorm;
          if (delta == 0.0d) delta = factor;
        }

        //form (q transpose)*fvec and store the first n components in qtf.
        for (int i = 0; i < m; i++) wa4[i] = outputs[i];
        for (int i = 0; i < n; i++)
        {
          if (fjac[i, i] != 0.0d)
          {
            double sum = 0;
            for (int j = i; j < m; j++) sum += fjac[j, i] * wa4[j];
            double tmp = -sum / fjac[i, i];
            for (int j = i; j < m; j++) wa4[j] += fjac[j, i] * tmp;
          }
          fjac[i, i] = wa1[i];
          qtf[i] = wa4[i];
        }

        //compute the norm of the scaled gradient.
        double gnorm = 0.0d;
        if (fnorm != 0.0d)
        {
          for (int i = 0; i < n; i++)
          {
            int l = ipvt[i];
            if (wa2[l] != 0.0d)
            {
              double sum = 0.0d;
              for (int j = 0; j < i; j++) sum += fjac[j, i] * (qtf[j] / fnorm);
              gnorm = Math.Max(gnorm, Math.Abs(sum / wa2[l]));
            }
          }
        }

        //test for convergence of the gradient norm
        if (gnorm <= gtol)
        {
          info = 4;
          return;
        }

        //rescale
        for (int i = 0; i < n; i++) diag[i] = Math.Max(diag[i], wa2[i]);

        //beginning of the inner loop.
        while (true)
        {
          //determine the levenberg-marquardt parameter.
          IVector wa5 = new VectorView(wa4, 0, n);
          IMatrix r = new MatrixView(fjac, fjac.Columns, fjac.Columns, 0, 0);
          lmpar(ref r, ipvt, diag, qtf, delta
            , ref par, ref wa1, ref wa2, ref wa3, ref wa5);

          //store the direction p and x + p. calculate the norm of p.
          for (int j = 0; j < n; j++)
          {
            wa1[j] = -wa1[j];
            wa2[j] = inputs[j] + wa1[j];
            wa3[j] = diag[j] * wa1[j];
          }

          //on the first iteration, adjust the initial step bound.
          double pnorm = wa3.ComputeEuclideanNorm();
          if (iter == 1) delta = Math.Min(delta, pnorm);

          //evaluate the function at x + p and calculate its norm.
          eFnc(wa2, ref wa4);
          NumberOfFunctionEvaluates++;
          double fnorm1 = wa4.ComputeEuclideanNorm();

          //compute the scaled actual reduction.
          double actred = -1.0;
          if (0.1 * fnorm1 < fnorm) actred = 1.0 - Math.Pow(fnorm1 / fnorm, 2);

          //compute the scaled predicted reduction 
          //and the scaled directional derivative.
          for (int j = 0; j < n; j++)
          {
            wa3[j] = 0.0d;
            double tmp = wa1[ipvt[j]];
            for (int i = 0; i <= j; i++) wa3[i] += fjac[i, j] * tmp;
          }
          double tmp1 = wa3.ComputeEuclideanNorm() / fnorm;
          double tmp2 = (Math.Sqrt(par) * pnorm) / fnorm;
          double prered = tmp1 * tmp1 + tmp2 * tmp2 / 0.5;
          double dirder = -(tmp1 * tmp1 + tmp2 * tmp2);

          //compute the ratio of the actual to the predicted reduction.
          double ratio = 0.0d;
          if (prered != 0.0) ratio = actred / prered;

          //update the step bound.
          if (ratio <= 0.25)
          {
            double tmp = 0;
            if (0.0 <= actred) tmp = 0.5;
            if (actred < 0.0) tmp = 0.5 * dirder / (dirder + 0.5 * actred);
            if ((fnorm < 0.1 * fnorm1) || (tmp < 0.1)) tmp = 0.1;
            delta = tmp * Math.Min(delta, pnorm / 0.1);
            par /= tmp;
          }
          else
          {
            if ((par == 0.0) || (0.75 <= ratio))
            {
              delta = pnorm / 0.5;
              par *= 0.5;
            }
          }

          //successful iteration, update x, fvec, and their norms.
          if (1e-4 <= ratio)
          {
            for (int j = 0; j < n; j++)
            {
              inputs[j] = wa2[j];
              wa2[j] = diag[j] * inputs[j];
            }
            for (int i = 0; i < m; i++) outputs[i] = wa4[i];
            xnorm = wa2.ComputeEuclideanNorm();
            fnorm = fnorm1;
            iter++;
          }

          //test for convergence.
          if ((Math.Abs(actred) <= ftol) && (prered <= ftol)
            && (0.5 * ratio <= 1.0))
            info = 1;
          if (delta <= xtol * xnorm) info = 2;
          if ((Math.Abs(actred) <= ftol) && (prered <= ftol)
            && (0.5 * ratio <= 1.0) && (info == 2))
            info = 3;
          if (info != 0) return;

          //test for termination and stringent tolerances.
          if (maxfev <= NumberOfFunctionEvaluates) info = 5;
          if (Math.Abs(actred) <= MECH_EPS
            && prered <= MECH_EPS && 0.5 * ratio <= 1.0)
            info = 6;
          if (delta <= MECH_EPS * xnorm) info = 7;
          if (gnorm < MECH_EPS) info = 8;
          if (info != 0) return;

          if (0.0001 <= ratio) break;
        }
      }
    }

    #endregion

    #region privateメソッド

    private static void QrFac(ref IMatrix a, ref IVector rdiag,
      ref IVector acnorm, ref IVector wa, ref int[] ipvt)
    {
      int m = a.Rows;
      int n = a.Columns;

      //compute the initial column norms and initialize several arrays.
      for (int j = 0; j < n; j++)
      {
        acnorm[j] = rdiag[j] = wa[j] =
          new VectorView(a, false, 0, j).ComputeEuclideanNorm();
        ipvt[j] = j;
      }

      //reduce a to r with householder transformations.
      int min = Math.Min(m, n);
      for (int j = 0; j < min; j++)
      {
        //bring the column of largest norm into the pivot position.
        int kMax = j;
        for (int k = j; k < n; k++)
          if (rdiag[kMax] < rdiag[k]) kMax = k;
        if (kMax != j)
        {
          for (int i = 0; i < m; i++)
          {
            double dTmp = a[i, j];
            a[i, j] = a[i, kMax];
            a[i, kMax] = dTmp;
          }
          rdiag[kMax] = rdiag[j];
          wa[kMax] = wa[j];
          int iTmp = ipvt[j];
          ipvt[j] = ipvt[kMax];
          ipvt[kMax] = iTmp;
        }

        //compute the householder transformation to reduce the
        //j-th column of a to a multiple of the j - th unit vector.
        double aiNorm = new VectorView(a, false, j, j).ComputeEuclideanNorm();
        if (aiNorm != 0)
        {
          if (a[j, j] < 0.0d) aiNorm = -aiNorm;
          for (int i = j; i < m; i++) a[i, j] = a[i, j] / aiNorm;
          a[j, j] += 1.0d;

          //apply the transformation to the remaining columns and update the norms.
          int jp1 = j + 1;
          for (int k = jp1; k < n; k++)
          {
            double sum = 0.0d;
            for (int i = j; i < m; i++) sum += a[i, j] * a[i, k];
            double dTmp = sum / a[j, j];
            for (int i = j; i < m; i++) a[i, k] -= dTmp * a[i, j];
            if (rdiag[k] != 0.0d)
            {
              dTmp = a[j, k] / rdiag[k];
              rdiag[k] *= Math.Sqrt(Math.Max(0.0d, 1.0d - dTmp * dTmp));
              if (0.05 * Math.Pow(rdiag[k] / wa[k], 2) <= MECH_EPS)
              {
                rdiag[k] = new VectorView(a, false, jp1, k).ComputeEuclideanNorm();
                wa[k] = rdiag[k];
              }
            }
          }
        }
        rdiag[j] = -aiNorm;
      }
    }

    private static void qrsolv(ref IMatrix r, int[] ipvt, IVector diag,
      IVector qtb, ref IVector x, ref IVector sdiag, ref IVector wa)
    {
      int n = r.Rows;

      //copy r and (q transpose)*b to preserve input and initialize s.
      //in particular, save the diagonal elements of r in x.
      for (int j = 0; j < n; j++)
      {
        for (int i = 0; i < n; i++) r[i, j] = r[j, i];
        x[j] = r[j, j];
        wa[j] = qtb[j];
      }

      //eliminate the diagonal matrix d using a givens rotation.
      for (int j = 0; j < n; j++)
      {
        //prepare the row of d to be eliminated, locating the
        //diagonal element using p from the qr factorization.
        int l = ipvt[j];
        if (diag[l] != 0.0d)
        {
          for (int k = j; k < n; k++) sdiag[k] = 0.0d;
          sdiag[j] = diag[l];

          //the transformations to eliminate the row of d modify only a single
          //element of(q transpose) * b beyond the first n, which is initially zero.
          double qtbpj = 0.0d;
          for (int k = j; k < n; k++)
          {
            //determine a givens rotation which eliminates the
            //appropriate element in the current row of d.
            if (sdiag[k] != 0.0d)
            {
              double sin, cos;
              if (Math.Abs(sdiag[k]) <= Math.Abs(r[k, k]))
              {
                double tan = sdiag[k] / r[k, k];
                cos = 0.5 / Math.Sqrt(0.25 + 0.25 * tan * tan);
                sin = cos * tan;
              }
              else
              {
                double cotan = r[k, k] / sdiag[k];
                sin = 0.5 / Math.Sqrt(0.25 + 0.25 * cotan * cotan);
                cos = sin * cotan;
              }

              //compute the modified diagonal element of r and
              //the modified element of((q transpose) * b,0).
              r[k, k] = cos * r[k, k] + sin * sdiag[k];
              double tmp = cos * wa[k] + sin * qtbpj;
              qtbpj = -sin * wa[k] + cos * qtbpj;
              wa[k] = tmp;

              //accumulate the tranformation in the row of s.
              int kp1 = k + 1;
              for (int i = kp1; i < n; i++)
              {
                tmp = cos * r[i, k] + sin * sdiag[i];
                sdiag[i] = -sin * r[i, k] + cos * sdiag[i];
                r[i, k] = tmp;
              }
            }
          }
        }
        //store the diagonal element of s and restore 
        //the corresponding diagonal element of r.
        sdiag[j] = r[j, j];
        r[j, j] = x[j];
      }

      //solve the triangular system for z. if the system is
      //singular, then obtain a least squares solution.
      int nsing = n - 1;
      for (int j = 0; j < n; j++)
      {
        if (sdiag[j] == 0.0 && nsing + 1 == n) nsing = j;
        if (nsing + 1 < n) wa[j] = 0.0d;
      }
      for (int k = 0; k <= nsing; k++)
      {
        int j = nsing - k;
        double sum = 0.0d;
        int jp1 = j + 1;
        for (int i = jp1; i <= nsing; i++) sum += r[i, j] * wa[i];
        wa[j] = (wa[j] - sum) / sdiag[j];
      }

      //permute the components of z back to components of x.
      for (int j = 0; j < n; j++) x[ipvt[j]] = wa[j];
    }

    private static void lmpar(ref IMatrix r, int[] ipvt, IVector diag,
      IVector qtb, double delta, ref double par, ref IVector x,
      ref IVector sdiag, ref IVector wa1, ref IVector wa2)
    {
      int n = r.Rows;
      double dwarf = double.Epsilon;

      //compute and store in x the gauss-newton direction. if the
      //jacobian is rank - deficient, obtain a least squares solution.
      int nsing = n - 1;
      for (int j = 0; j < n; j++)
      {
        wa1[j] = qtb[j];
        if (r[j, j] == 0.0 && nsing + 1 == n) nsing = j - 1;
        if (nsing + 1 < n) wa1[j] = 0;
      }

      for (int k = 0; k <= nsing; k++)
      {
        int j = nsing - k;
        wa1[j] = wa1[j] / r[j, j];
        double tmp = wa1[j];
        int jm1 = j - 1;
        if (0 <= jm1)
          for (int i = 0; i <= jm1; i++) wa1[i] -= r[i, j] * tmp;
      }
      for (int j = 0; j < n; j++) x[ipvt[j]] = wa1[j];

      //initialize the iteration counter. evaluate the function at the origin, 
      //and test for acceptance of the gauss - newton direction.
      int iter = 0;
      for (int j = 0; j < n; j++) wa2[j] = diag[j] * x[j];
      double dxnorm = wa2.ComputeEuclideanNorm();
      double fp = dxnorm - delta;
      if (0.1 * delta < fp)
      {
        //if the jacobian is not rank deficient, the newton step provides a 
        //lower bound, parl, for the zero of the function.
        //otherwise set this bound to zero.
        double parl = 0.0d;
        if (n <= nsing + 1)
        {
          for (int j = 0; j < n; j++)
          {
            int l = ipvt[j];
            wa1[j] = diag[l] * (wa2[l] / dxnorm);
          }
          for (int j = 0; j < n; j++)
          {
            double sum = 0.0d;
            int jm1 = j - 1;
            for (int i = 0; i <= jm1; i++) sum += r[i, j] * wa1[i];
            wa1[j] = (wa1[j] - sum) / r[j, j];
          }
          double tmp = wa1.ComputeEuclideanNorm();
          parl = ((fp / delta) / tmp) / tmp;
        }

        //calculate an upper bound, paru, for the zero of the function.
        for (int j = 0; j < n; j++)
        {
          double sum = 0.0d;
          for (int i = 0; i <= j; i++) sum += r[i, j] * qtb[i];
          wa1[j] = sum / diag[ipvt[j]];
        }
        double gnorm = wa1.ComputeEuclideanNorm();
        double paru = gnorm / delta;
        if (paru == 0.0d) paru = dwarf / Math.Min(delta, 0.1);

        //if the input par lies outside of the interval (parl,paru),
        //set par to the closer endpoint.
        par = Math.Max(Math.Min(par, paru), parl);
        if (par == 0.0d) par = gnorm / dxnorm;

        //beginning of an iteration
        while (true)
        {
          iter++;
          //evaluate the function at the current value of par.
          if (par == 0.0d) par = Math.Max(dwarf, 0.001 * paru);
          double tmp = Math.Sqrt(par);
          for (int j = 0; j < n; j++) wa1[j] = tmp * diag[j];

          qrsolv(ref r, ipvt, wa1, qtb, ref x, ref sdiag, ref wa2);
          for (int j = 0; j < n; j++) wa2[j] = diag[j] * x[j];
          dxnorm = wa2.ComputeEuclideanNorm();
          tmp = fp;
          fp = dxnorm - delta;

          //if the function is small enough, accept the current value of par.
          //also test for the exceptional cases where parl is zero or the 
          //number of iterations has reached 10.
          if ((iter == 10) || (Math.Abs(fp) <= 0.1 * delta)
            || (parl == 0.0 && fp <= tmp && tmp < 0.0))
            break;

          //compute the newton correction
          for (int j = 0; j < n; j++)
          {
            int l = ipvt[j];
            wa1[j] = diag[l] * (wa2[l] / dxnorm);
          }

          for (int j = 0; j < n; j++)
          {
            wa1[j] /= sdiag[j];
            tmp = wa1[j];
            int jp1 = j + 1;
            if (jp1 <= n)
              for (int i = jp1; i < n; i++) wa1[i] -= r[i, j] * tmp;
          }
          tmp = wa1.ComputeEuclideanNorm();
          double parc = ((fp / delta) / tmp) / tmp;

          //depending on the sign of the function, update parl or paru.
          if (0.0 < fp) parl = Math.Max(parl, par);
          if (fp < 0.0) paru = Math.Min(paru, par);

          //compute an improved estimate for par
          par = Math.Max(parl, par + parc);
        }
      }
      if (iter == 0.0) par = 0.0;
    }

    #endregion

  }
}