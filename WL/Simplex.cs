// Author: Gennady Gorlachev (ggorlachev@roiss.ru) 
//---------------------------------------------------------------------------
using System;
using System.Threading.Tasks;

namespace WL
{
    public class Simplex
    {
        # region Properties and variables

        public IOptimizationObject OptimizationObject { get; set; }

        /// <summary>
        /// Максимально дозволенное количество шагов оптимизации
        /// </summary>
        public int NMAX { get; set; }

        /// <summary>
        /// Количество шагов на одно информирование о прогрессе
        /// </summary>
        public int ProgressStep { get; set; }

        /// <summary>
        /// Минимальное значение изменения функции, при котором происходит
        /// остановка поиска как завершенного
        /// </summary>
        public double FTol { get; set; }

        /// <summary>
        /// Окончательный результат оптимизации
        /// </summary>
        public double[] Result { get; set; }

        /// <summary>
        /// Процессорная точность
        /// </summary>
        private double TINY { get; set; }

        /// <summary>
        /// Счетчик шагов оптимизации
        /// </summary>
        private int NFunk { get; set; }

        #endregion

        public Simplex()
        {
            NMAX = 5000;
            ProgressStep = 100;
            FTol = 1.0;
            TINY = 1.0e-10;
            NFunk = 0;
        }

        /// <summary>
        /// Extrapolates by a factor fac through the face of the simplex across from the high point,
        /// tries it, and replaces the high point if the new point is better.
        /// </summary>
        /// <param name="p"></param>
        /// <param name="y"></param>
        /// <param name="psum"></param>
        /// <param name="ndim"></param>
        /// <param name="ihi"></param>
        /// <param name="fac"></param>
        /// <returns></returns>
        async Task<double> amotry(double[,] p, double[] y, double[] psum, int ihi, double fac)
        {
            int j, ndim = psum.Length;
            double[] ptry = new double[ndim];
            double fac1 = (1.0 - fac) / ndim;
            double fac2 = fac1 - fac;

            for (j = 0; j < ndim; j++)
                ptry[j] = psum[j] * fac1 - p[ihi, j] * fac2;

            //Evaluate the function at the trial point.
            double ytry = await OptimizationObject.ValueAt(ptry);

            // If it's better than the highest, then replace the highest.
            if (ytry < y[ihi])
            {
                y[ihi] = ytry;
                for (j = 0; j < ndim; j++)
                {
                    psum[j] += ptry[j] - p[ihi, j];
                    p[ihi, j] = ptry[j];
                }
            }
            return ytry;
        }

        /// <summary>
        /// Multidimensional minimization of the function funk(x) where x[1..ndim] is a vector in ndim
        /// dimensions, by the downhill simplex method of Nelder and Mead. The matrix p[1..ndim+1]
        /// [1..ndim] is input. Its ndim+1 rows are ndim-dimensional vectors which are the vertices of
        /// the starting simplex. Also input is the vector y[1..ndim+1], whose components must be preinitialized
        /// to the values of funk evaluated at the ndim+1 vertices (rows) of p; and ftol the
        /// fractional convergence tolerance to be achieved in the function value (n.b.!). On output, p and
        /// y will have been reset to ndim+1 new points all within ftol of a minimum function value, and
        /// nfunk gives the number of function evaluations taken.
        /// </summary>
        /// <param name="p"></param>
        /// <param name="y"></param>
        /// <param name="ftol"></param>
        async Task amoeba(double[,] p, double[] y, double ftol)
        {
            int i, j, ndim = y.Length - 1;
            double[] psum = new double[ndim];

            for (j = 0; j < ndim; j++)
            {
                double sum = 0;
                for (i = 0; i <= ndim; i++)
                    sum += p[i, j];
                psum[j] = sum;
            }

            for (; ; )
            {
                int inhi, ihi, ilo = 0;

                // First we must determine which point is the highest (worst), next-highest,
                // and lowest (best), by looping over the points in the simplex.

                if (y[0] > y[1]) { ihi = 0; inhi = 1; }
                else { ihi = 1; inhi = 0; }

                for (i = 0; i <= ndim; i++)
                {
                    if (y[i] <= y[ilo])
                        ilo = i;
                    if (y[i] > y[ihi])
                    {
                        inhi = ihi;
                        ihi = i;
                    }
                    else if (y[i] > y[inhi] && i != ihi)
                        inhi = i;
                }

                double rtol = 2.0 * System.Math.Abs(y[ihi] - y[ilo]) / (System.Math.Abs(y[ihi]) + System.Math.Abs(y[ilo]) + TINY);

                // Compute the fractional range from highest to lowest and return if satisfactory.
                // If returning, put best point and value in slot 0.
                if (rtol < ftol || NFunk >= NMAX || OptimizationObject.DoCancel())
                {
                    double ff = y[ilo];
                    y[ilo] = y[0];
                    y[0] = ff;
                    for (i = 0; i < ndim; i++)
                    {
                        ff = p[ilo, i];
                        p[ilo, i] = p[0, i];
                        p[0, i] = ff;
                    }
                    break;
                }

                if (NFunk % ProgressStep == 0)
                {
                    double[] pcurrent = new double[ndim];
                    for (i = 0; i < ndim; i++)
                        pcurrent[i] = p[ilo, i];
                    this.OptimizationObject.ProgressInfo((double)NFunk / NMAX, pcurrent);
                }

                NFunk += 2;

                // Begin a new iteration. First extrapolate by a factor −1 through the face of the simplex
                // across from the high point, i.e., reflect the simplex from the high point.
                double ytry = await amotry(p, y, psum, ihi, -1.0);

                if (ytry <= y[ilo])
                    // Gives a result better than the best point, so try an additional extrapolation by a factor 2.
                    ytry = await amotry(p, y, psum, ihi, 2.0);
                else if (ytry >= y[inhi])
                {
                    // The reflected point is worse than the second-highest, so look for an intermediate
                    // lower point, i.e., do a one-dimensional contraction.                
                    double ysave = y[ihi];
                    ytry = await amotry(p, y, psum, ihi, 0.5);

                    if (ytry >= ysave)
                    {
                        // Can't seem to get rid of that high point. Better
                        // contract around the lowest (best) point.               
                        for (i = 0; i <= ndim; i++)
                        {
                            if (i != ilo)
                            {
                                for (j = 0; j < ndim; j++)
                                    p[i, j] = psum[j] = 0.5 * (p[i, j] + p[ilo, j]);
                                y[i] = await OptimizationObject.ValueAt(psum);
                            }
                        }
                        // Keep track of function evaluations.
                        NFunk += ndim;

                        // Recompute psum.    
                        for (j = 0; j < ndim; j++)
                        {
                            double sum = 0;
                            for (i = 1; i <= ndim; i++)
                                sum += p[i, j];
                            psum[j] = sum;
                        }
                    }
                }
                else
                    --NFunk; // Correct the evaluation count.                
            }
            // Go back for the test of doneness and the next iteration.
        }

        /// <summary>
        /// Старт оптимизации
        /// </summary>
        async public Task Search()
        {
            // Проверяем корректность установки начальных данных
            if (OptimizationObject == null)
                throw new ApplicationException("Simplex: optimization object not set");

            double[] p = OptimizationObject.GetParams();
            if (p == null)
                throw new ApplicationException("Simplex: start point not set");
            int ndim = p.Length;

            double[] dp = OptimizationObject.StartSteps();
            if (dp == null)
                throw new ApplicationException("Simplex: start steps not set");
            if (dp.Length != ndim)
                throw new ApplicationException("Simplex: start point and start step sizes are different");

            // Рассчитываем стартовый симплекс
            double[,] pp = new double[ndim + 1, ndim];

            int i, j;
            for (j = 0; j < ndim; j++)
                pp[0, j] = p[j];

            for (i = 1; i <= ndim; i++)
            {
                for (j = 0; j < ndim; j++)
                    pp[i, j] = (i == j + 1) ? p[j] + dp[j] : p[j];
            }

            double[] y = new double[ndim + 1];
            double[] x = new double[ndim];
            for (i = 0; i <= ndim; i++)
            {
                for (j = 0; j < ndim; j++)
                    x[j] = pp[i, j];
                y[i] = await OptimizationObject.ValueAt(x);
            }

            await amoeba(pp, y, FTol);

            // Результат
            Result = new double[ndim];
            for (j = 0; j < ndim; j++)
                Result[j] = pp[0, j];
        }
    }
}
