// Author: Gennady Gorlachev (ggorlachev@roiss.ru) 
//---------------------------------------------------------------------------
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace WL
{
    public class Fusion : IOptimizationObject
    {
        #region Локальные переменные

        // Исходное изображение фотопленки
        private TransformedBitmap image;

        // Матрица в окрестности подгоняемого пучка
        byte[] pixels = null;
        int fnx;    // размер фрагмента
        int fny;
        double fx0; // координаты левого нижнего угла фрагмента
        double fy0;

        // Радиус области расчета целевой функции
        private double R = 9.0;

        /// <summary>
        /// Таблицы преобразования интенсивностей
        /// </summary>
        private ushort[] filmLUT = null;

        /// <summary>
        /// Значения каждой из 2D матриц обоих исследований загоняются 
        /// в диапазон от 0 до Inorm, в данном случае до 256
        /// </summary>
        private ushort inorm = 256;

        /// <summary>
        /// Подгоняемый пучок
        /// </summary>
        public BeamParams CurrentBeam { get; set; }

        /// <summary>
        /// Текущиие стартовые шаги оптимизации.
        /// Необходимо для поддержки повторения оптимизации при изменении сетки расчета целевой функции
        /// </summary>
        private double[] OptStartSteps
        {
            get
            {
                //return new double[] { 2.0, 2.0, 1.0, 1.0, 1.0, 1.0, 0.01, 2.0 };
                //return new double[] { 1.0, 1.0, 0.5, 0.5, 0.5, 0.5, 0.005, 1.0 };
                return new double[] { 0.5, 0.5, 0.5, 0.5, 0.005, 1.0 };
            }
        }

        #endregion

        /// <summary>
        /// Конструктор
        /// </summary>
        /// <param name="i">сканированная пленка целиком</param>
        public Fusion(TransformedBitmap i)
        {
            image = i;
        }

        /// <summary>
        /// Создание таблиц преобразования интенсивностей изображений
        /// в новый динамический диапазон.
        /// Самый простой вариант - это линейное масштабирование.
        /// Динамическое масштабирование не исследовано, но может оказаться полезным.
        /// </summary>
        private void correctIntencityRange(BeamParams b)
        {
            filmLUT = new ushort[256];

            double r = image.DpiX * R * 2.0 / 25.4;
            double x0 = b.O.X;
            double y0 = b.O.Y;

            Int32Rect rect = new Int32Rect();
            rect.X = (int)(x0 - r);
            rect.Y = (int)(y0 - r);
            rect.Width = (int)(2 * r);
            rect.Height = (int)(2 * r);
            if (rect.X + rect.Width >= image.PixelWidth)
                rect.X -= (int)(rect.X + rect.Width - image.PixelWidth + 1);
            if (rect.Y + rect.Height >= image.PixelHeight)
                rect.Y -= (int)(rect.Y + rect.Height - image.PixelHeight + 1);
            if (rect.X < 0)
            {
                rect.Width += rect.X;
                if (rect.Width < 1) rect.Width = 1;
                rect.X = 0;
            }
            if (rect.Y < 0)
            {
                rect.Height += rect.Y;
                if (rect.Height < 1) rect.Height = 1;
                rect.Y = 0;
            }

            fx0 = (double)rect.X;
            fy0 = (double)rect.Y;
            fnx = rect.Width;
            fny = rect.Height;
            pixels = new byte[fnx * fny * 4];
            image.CopyPixels(rect, pixels, fnx * 4, 0);

            byte humax = 0;
            int i, j;
            for (i = 0; i < rect.Width; i++)
            {
                for (j = 0; j < rect.Height; j++)
                {
                    byte bv = (byte)(255 - pixels[(i * rect.Height + j) * 4]);
                    pixels[(i * rect.Height + j) * 4] = bv;
                    if (bv > humax) humax = bv;
                }
            }

            double scale = (double)(inorm - 1) / (double)humax;
            for (i = 0; i < 256; i++)
                filmLUT[i] = (ushort)(scale * i);

            // Debug
            //DumpPixels("C:/tmp/pixels.txt");
        }

        /// <summary>
        /// Функция, осуществляющая подгонку вторичной серии к референсной
        /// </summary>
        async public Task<BeamParams> DoFusion(BeamParams b)
        {
            CurrentBeam = b.Clone();

            // Настройка шкалы яркостей на диапазон от 0 до 255 градаций
            correctIntencityRange(b);

            try
            {
                // Предварительное определение положения центрального шврика
                FitCentralBallPosition(CurrentBeam);
                FitCentralBallPosition(CurrentBeam);
                FitCentralBallPosition(CurrentBeam);

                Simplex fitter = new Simplex();
                fitter.FTol = 1e-12;
                fitter.NMAX = 1000;
                fitter.OptimizationObject = this;

                //ROISS.RST.Math.Solvers.SimplexAnnealing fitter = new RST.Math.Solvers.SimplexAnnealing();
                //fitter.FTol = 1e-12;
                //fitter.NMax = 100;
                //fitter.NRuns = 20;
                //fitter.TT = 0.5;
                //fitter.OptimizationObject = this;

                // Поиск
                await fitter.Search();

                CurrentBeam.FitParameters = fitter.Result;

                // DEBUG
                //DumpChisqDistributions("C:/tmp/chisq.txt");
            }
            catch (System.Exception except)
            {
                System.Windows.MessageBox.Show("Ошибка при чтении данных: \n" + except.Message, "Ошибка!",
                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }

            return CurrentBeam;
        }

        private void FitCentralBallPosition(BeamParams b)
        {
            // Радиус области интереса
            double rTO = 3.5;
            double ps = b.PS;
            double x1 = b.O.X - fx0, y1 = b.O.Y - fy0;

            // Строим гистограмму в окрестности.
            // Находим центр масс точек с плотностью меньше медианного значения
            Histogram h = new Histogram(0, 1, 256);
            HPair p = new HPair(0, 1);

            // Гистограмма плотностей
            int kk, i;
            for (kk = 0; kk < fny; kk++)
            {
                double yF = (kk - y1) * ps;    // координата пиксела относительно центра шарика
                if (yF * yF > rTO * rTO) continue;

                for (i = 0; i < fnx; i++)
                {
                    double xF = (i - x1) * ps;
                    if (yF * yF + xF * xF > rTO * rTO) continue;

                    p.D = (double)pixels[(i + kk * fnx) * 4];
                    if (p.D > 64)
                        h.AppendPair(p);
                }
            }

            //h.MakeComulative();
            byte level = (byte)h.Mean;

            // Середина
            int count = 0;
            double xc = 0, yc = 0;
            for (kk = 0; kk < fny; kk++)
            {
                double yF = (kk - y1) * ps;
                if (yF * yF > rTO * rTO) continue;

                for (i = 0; i < fnx; i++)
                {
                    double xF = (i - x1) * ps;
                    if (yF * yF + xF * xF > rTO * rTO) continue;

                    if (pixels[(i + kk * fnx) * 4] < level)
                    {
                        xc += xF;
                        yc += yF;
                        count++;
                    }
                }
            }

            xc /= count;
            yc /= count;

            b.O = new Point(b.O.X + xc / ps, b.O.Y + yc / ps);
        }

        #region Optimization implementation

        /// <summary>
        /// Целевая функция
        /// </summary>
        /// <param name="p">массив оптимизируемых параметров</param>
        /// <returns>значение целевой функции</returns>
        async public Task<double> ValueAt(double[] p)
        {
            BeamParams b = CurrentBeam.Clone();
            b.FitParameters = p;
            //return 1.0 / Correlation(b);
            //return 1.0 / PatternIntensity(b);
            return 1.0 / (await Task.Run<double>(() => JointHist2(b)));
            //return 1.0 / JointHist3(b);
        }

        /// <summary>
        /// Проверка, не желает ли заказчик прервать оптимизацию
        /// </summary>
        /// <returns></returns>
        public bool DoCancel()
        {
            return false;
        }

        /// <summary>
        /// Информирование заказчика о промежуточном состоянии оптимизации
        /// </summary>
        /// <param name="p"></param>
        public void ProgressInfo(double f, double[] p)
        {
        }

        /// <summary>
        /// Установка результата по окончании оптимизации
        /// </summary>
        /// <param name="errCode">код результата</param>
        /// <param name="msg">текстовое сообщение о результате</param>
        public void SetStatus(int errCode, string msg)
        {
        }

        /// <summary>
        /// Запрос текущих значений параметров
        /// Используется оптимизатором для определения стартовой точки
        /// </summary>
        /// <returns></returns>
        public double[] GetParams()
        {
            return CurrentBeam.FitParameters;
        }

        /// <summary>
        /// Информация оптимизатору о величинах шагов на старте
        /// </summary>
        /// <returns></returns>
        public double[] StartSteps()
        {
            return OptStartSteps;
        }

        #endregion

        #region Measure function

        /// <summary>
        /// Объединенная гистограмма по версии перебора точек на исходном изображении
        /// </summary>
        /// <returns>Значение целевой функции</returns>
        public double JointHist3(BeamParams b)
        {
            // Размеры областей объектов WL
            double rCC = 6.25;
            double rTO = 2.5;
            double rM = 3.0;

            int ii, jj;

            // Гистограмма
            double[,] joinHistogram = new double[inorm, inorm];
            for (ii = 0; ii < inorm; ii++)
                for (jj = 0; jj < inorm; jj++)
                    joinHistogram[ii, jj] = 0;

            // Коэффициенты преобразования координат из первого исследования во второе
            double a = (b.FA + b.FCA) * System.Math.PI / 180.0;
            double sca = System.Math.Sin(a), cca = System.Math.Cos(a);
            double ps = b.PS;
            double x1 = b.O.X - fx0, y1 = b.O.Y - fy0;

            // Цикл по координатам воображаемой матрицы, связанной с кругом конического коллиматора.
            // Первым изображением является синтетическое изображение,
            // вторым - изображение на фотопленке WL теста.

            Parallel.For(0, fny, kk =>
            //for (int kk = 0; kk < fny; kk++)
            {
                double yF = (kk - y1) * ps;    // координата пиксела относительно центра шарика

                for (int i = 0; i < fnx; i++)
                {
                    double xF = (i - x1) * ps;
                    ushort ihF = 255;

                    // Координата пиксела в системе пучка (учет поворота изображения)
                    double x = xF * cca + yF * sca;
                    double y = xF * sca - yF * cca;

                    // Координаты в системе конического коллиматора с учетом смещения 
                    // относительно шарика
                    double xc = System.Math.Abs(b.CC.X - x), yc = System.Math.Abs(b.CC.Y - y);

                    if (xc * xc + yc * yc > rCC * rCC)
                        ihF = 0;
                    else
                    {
                        if (x * x + y * y < rTO * rTO)
                            ihF = (ushort)((ihF * 4) / 5);

                        double xm = System.Math.Abs(b.Mlc.X - x), ym = System.Math.Abs(b.Mlc.Y - y);
                        if (xm > 3 * rM || ym > 3 * rM || (xm > rM && ym > rM))
                            ihF = 0;
                    }

                    // Суммирование вкладов всех 4 соседних точек
                    joinHistogram[ihF, filmLUT[(ushort)pixels[(i + kk * fnx) * 4]]] += 1;
                }
                //}
            });

            // Нормировка объединенной гистограммы
            double HAB = 0;
            for (ii = 0; ii < (int)inorm; ii++)
            {
                for (jj = 0; jj < (int)inorm; jj++)
                    HAB += joinHistogram[ii, jj];
            }
            for (ii = 0; ii < (int)inorm; ii++)
            {
                for (jj = 0; jj < (int)inorm; jj++)
                    joinHistogram[ii, jj] /= HAB;
            }

            // Гистограммы исследований
            double[] h1 = new double[inorm];
            double[] h2 = new double[inorm];
            for (ii = 0; ii < inorm; ii++) { h1[ii] = 0; h2[ii] = 0; }

            for (ii = 0; ii < (int)inorm; ii++)
            {
                for (jj = 0; jj < (int)inorm; jj++)
                {
                    h1[ii] += joinHistogram[ii, jj];
                    h2[jj] += joinHistogram[ii, jj];
                }
            }

            double ret = 0;
            for (ii = 0; ii < (int)inorm; ii++)
            {
                double f1 = h1[ii];
                if (f1 <= 0) continue;
                for (jj = 0; jj < (int)inorm; jj++)
                {
                    double f = joinHistogram[ii, jj],
                           f2 = h2[jj];
                    if (f > 0 && f2 > 0)
                    {
                        double c = f / (f1 * f2);
                        ret += System.Math.Log(c) * f;
                    }
                }
            }

            return ret;
        }

        /// <summary>
        /// Объединенная гистограмма, по сути мера совпадения изображений
        /// </summary>
        /// <returns>Значение целевой функции</returns>
        public double JointHist2(BeamParams b)
        {
            // Размеры областей объектов WL
            double rCC = 6.25;
            double rTO = 2.5;
            double rM = 3.0;

            // Размер пиксела виртуальной синтетической матрицы в см
            double vps = 0.05;

            // Радиус матрица расчета объединенной гистограммы в единицах пикселов виртуальной матрицы.
            int ir = (int)(R / vps);

            int ii, jj;
            // Гистограмма
            double[,] joinHistogram = new double[inorm, inorm];
            for (ii = 0; ii < inorm; ii++)
                for (jj = 0; jj < inorm; jj++)
                    joinHistogram[ii, jj] = 0;

            // Коэффициенты преобразования координат из первого исследования во второе
            double a = (b.FA + b.FCA) * System.Math.PI / 180.0;
            double sca = System.Math.Sin(a), cca = System.Math.Cos(a);
            double ps = b.PS;
            double x1 = b.O.X - fx0, y1 = b.O.Y - fy0;

            // Цикл по координатам воображаемой матрицы, связанной с кругом конического коллиматора.
            // Первым изображением является синтетическое изображение,
            // вторым - изображение на фотопленке WL теста.

            Parallel.For(-ir, ir + 1, kk =>
            //for (int kk = -ir; kk <= ir; kk++)
            {
                int i;
                double y = kk * vps;

                for (i = -ir; i <= ir; i++)
                {
                    if (i * i + kk * kk > ir * ir)
                        continue;

                    double x = i * vps;
                    ushort ihF = 255;

                    // Координаты в системе конического коллиматоа с учетом смещения 
                    // относительно шарика
                    double xc = System.Math.Abs(b.CC.X - x), yc = System.Math.Abs(b.CC.Y - y);

                    if (xc * xc + yc * yc > rCC * rCC)
                        ihF = 0;
                    else
                    {
                        if (x * x + y * y < rTO * rTO)
                            ihF = (ushort)((ihF * 4) / 5);

                        double xm = System.Math.Abs(b.Mlc.X - x), ym = System.Math.Abs(b.Mlc.Y - y);
                        if (xm > 3 * rM || ym > 3 * rM || (xm > rM && ym > rM))
                            ihF = 0;
                    }

                    // Индексы во втором изображении
                    double xF = (x * cca + y * sca) / ps + x1;
                    double yF = y1 + (x * sca - y * cca) / ps;
                    int ix = (int)(xF);
                    int iy = (int)(yF);

                    if (ix < 0 || ix >= fnx - 1 || iy < 1 || iy >= fny)
                        continue;

                    // Ближайшие точки и их веса
                    double wx2 = xF - ix, wx1 = 1 - wx2;
                    double wy1 = yF - iy, wy2 = 1 - wy1;

                    // Суммирование вкладов всех 4 соседних точек
                    joinHistogram[ihF, filmLUT[(ushort)pixels[(ix + iy * fnx) * 4]]] += wx1 * wy1;
                    joinHistogram[ihF, filmLUT[(ushort)pixels[(ix + (iy - 1) * fnx) * 4]]] += wx1 * wy2;
                    joinHistogram[ihF, filmLUT[(ushort)pixels[(ix + 1 + iy * fnx) * 4]]] += wx2 * wy1;
                    joinHistogram[ihF, filmLUT[(ushort)pixels[(ix + 1 + (iy - 1) * fnx) * 4]]] += wx2 * wy2;
                }
                //}
            });

            // Нормировка объединенной гистограммы
            double HAB = 0;
            for (ii = 0; ii < (int)inorm; ii++)
            {
                for (jj = 0; jj < (int)inorm; jj++)
                    HAB += joinHistogram[ii, jj];
            }
            for (ii = 0; ii < (int)inorm; ii++)
            {
                for (jj = 0; jj < (int)inorm; jj++)
                    joinHistogram[ii, jj] /= HAB;
            }

            // Гистограммы исследований
            double[] h1 = new double[inorm];
            double[] h2 = new double[inorm];
            for (ii = 0; ii < inorm; ii++) { h1[ii] = 0; h2[ii] = 0; }

            for (ii = 0; ii < (int)inorm; ii++)
            {
                for (jj = 0; jj < (int)inorm; jj++)
                {
                    h1[ii] += joinHistogram[ii, jj];
                    h2[jj] += joinHistogram[ii, jj];
                }
            }

            double ret = 0;
            for (ii = 0; ii < (int)inorm; ii++)
            {
                double f1 = h1[ii];
                if (f1 <= 0) continue;
                for (jj = 0; jj < (int)inorm; jj++)
                {
                    double f = joinHistogram[ii, jj],
                           f2 = h2[jj];
                    if (f > 0 && f2 > 0)
                    {
                        double c = f / (f1 * f2);
                        ret += System.Math.Log(c) * f;
                    }
                }
            }

            return ret;
        }

        /// <summary>
        /// Pattern Intensity Measure
        /// </summary>
        /// <returns>Значение целевой функции</returns>
        public double PatternIntensity(BeamParams b)
        {
            // Размеры областей объектов WL
            double rCC = 6.25;
            double rTO = 2.5;
            double rM = 3.0;

            // Размер пиксела виртуальной синтетической матрицы в см
            double vps = 0.1;

            // Радиус матрица расчета объединенной гистограммы в единицах пикселов виртуальной матрицы.
            int ir = (int)(R / vps);
            int nx = 2 * ir + 1, ny = nx;

            // Матрицы изображений
            double[,] Is = new double[nx, ny];
            double[,] Idiff = new double[nx, ny];

            // Коэффициенты преобразования координат из первого исследования во второе
            double a = (b.FA + b.FCA) * System.Math.PI / 180.0;
            double sca = System.Math.Sin(a), cca = System.Math.Cos(a);
            double ps = b.PS;
            double x1 = b.CC.X, y1 = b.CC.Y;
            double x2 = b.O.X - fx0, y2 = fny - 1 - b.O.Y + fy0;

            // Строим изображение разницы
            Parallel.For(-ir, ir + 1, kk =>
            //for (int kk = -ir; kk <= ir; kk++)
            {
                int i;
                double y = kk * vps;

                for (i = -ir; i <= ir; i++)
                {
                    if (i * i + kk * kk > ir * ir)
                    {
                        Idiff[i + ir, kk + ir] = 0;
                        continue;
                    }

                    double x = i * vps;

                    // Индексы во втором изображении
                    double xF = (x1 + x * cca + y * sca) / ps + x2;
                    double yF = y2 - (y1 - x * sca + y * cca) / ps;
                    int ix = (int)(xF);
                    int iy = (int)(yF);

                    if (ix < 0 || ix >= fnx - 1 || iy < 1 || iy >= fny)
                        continue;

                    double ihF = 255;

                    // Координаты в системе конического коллиматоа с учетом смещения 
                    // относительно шарика
                    double xc = System.Math.Abs(b.CC.X + x), yc = System.Math.Abs(b.CC.Y + y);

                    if (xc * xc + yc * yc > rCC * rCC)
                        ihF = 0;
                    else
                    {
                        double xt = x1 + x, yt = y1 + y;
                        if (xt * xt + yt * yt < rTO * rTO)
                            ihF = ihF * 0.8;

                        double xm = System.Math.Abs(b.Mlc.X + x), ym = System.Math.Abs(b.Mlc.Y + y);
                        if (xm > 3 * rM || ym > 3 * rM || (xm > rM && ym > rM))
                            ihF = 0;
                    }

                    // Ближайшие точки и их веса
                    double wx2 = xF - ix, wx1 = 1 - wx2;
                    double wy1 = yF - iy, wy2 = 1 - wy1;

                    // Масштабирование по интенсивности и линейная интерполяция интенсивности
                    double f = filmLUT[(ushort)pixels[(ix + iy * fnx) * 4]] * wx1 * wy1 +
                        filmLUT[(ushort)pixels[(ix + (iy - 1) * fnx) * 4]] * wx1 * wy2 +
                        filmLUT[(ushort)pixels[(ix + 1 + iy * fnx) * 4]] * wx2 * wy1 +
                        filmLUT[(ushort)pixels[(ix + 1 + (iy - 1) * fnx) * 4]] * wx2 * wy2;

                    Idiff[i + ir, kk + ir] = (ihF - f);
                }
                //}
            });

            // DEBUG
            //{
            //    int iv, jw;
            //    System.IO.StreamWriter file = new System.IO.StreamWriter("C:/tmp/Idiff.txt");

            //    for (iv = 0; iv < nx; iv++) file.Write("\t{0}", iv);
            //    file.WriteLine();

            //    for (jw = 0; jw < ny; jw++)
            //    {
            //        file.Write("{0}", jw);
            //        for (iv = 0; iv < nx; iv++) file.Write("\t{0}", Idiff[iv,jw]);
            //        file.WriteLine();
            //    }
            //    file.Close();
            //}

            // Прамаетры целевой функции
            double s2 = 100, r2 = 9;
            double ret = 0, count = 0;
            int ii, jj, v, w;
            for (ii = 0; ii < nx; ii++)
            {
                int v1 = ii - 3, v2 = ii + 3;
                if (v1 < 0) v1 = 0;
                if (v2 >= nx) v2 = nx - 1;
                for (jj = 0; jj < ny; jj++)
                {
                    if ((ii - ir) * (ii - ir) + (jj - ir) * (jj - ir) > (ir - 3) * (ir - 3))
                        continue;

                    int w1 = jj - 3, w2 = jj + 3;
                    if (w1 < 0) w1 = 0;
                    if (w2 >= ny) w2 = ny - 1;

                    for (v = v1; v <= v2; v++)
                    {
                        for (w = w1; w <= w2; w++)
                        {
                            if ((v - ii) * (v - ii) + (w - jj) * (w - jj) > r2) continue;
                            double ff = Idiff[ii, jj] - Idiff[v, w];
                            ret += s2 / (s2 + ff * ff);
                            count += 1.0;
                        }
                    }
                }
            }

            return count / ret;
        }

        /// <summary>
        /// Normalized Cross Correlation Measure
        /// </summary>
        /// <returns>Значение целевой функции</returns>
        public double Correlation(BeamParams b)
        {
            // Размеры областей объектов WL
            double rCC = 6.25;
            double rTO = 2.5;
            double rM = 3.0;

            // Размер пиксела виртуальной синтетической матрицы в см
            double vps = 0.05;

            // Радиус матрица расчета объединенной гистограммы в единицах пикселов виртуальной матрицы.
            int ir = (int)(R / vps);
            int nx = 2 * ir + 1, ny = nx;

            // Матрицы изображений
            double[,] Is = new double[nx, ny];
            double[,] If = new double[nx, ny];

            // Коэффициенты преобразования координат из первого исследования во второе
            double a = (b.FA + b.FCA) * System.Math.PI / 180.0;
            double sca = System.Math.Sin(a), cca = System.Math.Cos(a);
            double ps = b.PS;
            double x1 = b.CC.X, y1 = b.CC.Y;
            double x2 = b.O.X - fx0, y2 = fny - 1 - b.O.Y + fy0;

            // Строим изображения
            double Smean = 0;
            double Fmean = 0;
            double count = 0;

            Parallel.For(-ir, ir + 1, kk =>
            //for (int kk = -ir; kk <= ir; kk++)
            {
                int i;
                double y = kk * vps;

                for (i = -ir; i <= ir; i++)
                {
                    if (i * i + kk * kk > ir * ir)
                        continue;

                    double x = i * vps;

                    // Индексы во втором изображении
                    double xF = (x1 + x * cca + y * sca) / ps + x2;
                    double yF = y2 - (y1 - x * sca + y * cca) / ps;
                    int ix = (int)(xF);
                    int iy = (int)(yF);

                    if (ix < 0 || ix >= fnx - 1 || iy < 1 || iy >= fny)
                        continue;

                    double ihF = 255;

                    // Координаты в системе конического коллиматоа с учетом смещения 
                    // относительно шарика
                    double xc = System.Math.Abs(b.CC.X + x), yc = System.Math.Abs(b.CC.Y + y);

                    if (xc * xc + yc * yc > rCC * rCC)
                        ihF = 0;
                    else
                    {
                        double xt = x1 + x, yt = y1 + y;
                        if (xt * xt + yt * yt < rTO * rTO)
                            ihF = ihF * 0.8;

                        double xm = System.Math.Abs(b.Mlc.X + x), ym = System.Math.Abs(b.Mlc.Y + y);
                        if (xm > 3 * rM || ym > 3 * rM || (xm > rM && ym > rM))
                            ihF = 0;
                    }

                    // Ближайшие точки и их веса
                    double wx2 = xF - ix, wx1 = 1 - wx2;
                    double wy2 = yF - iy, wy1 = 1 - wy2;

                    // Масштабирование по интенсивности и линейная интерполяция интенсивности
                    double f = filmLUT[(ushort)pixels[(ix + iy * fnx) * 4]] * wx1 * wy1 +
                        filmLUT[(ushort)pixels[(ix + (iy - 1) * fnx) * 4]] * wx1 * wy2 +
                        filmLUT[(ushort)pixels[(ix + 1 + iy * fnx) * 4]] * wx2 * wy1 +
                        filmLUT[(ushort)pixels[(ix + 1 + (iy - 1) * fnx) * 4]] * wx2 * wy2;

                    Is[i + ir, kk + ir] = ihF;
                    If[i + ir, kk + ir] = f;
                    Smean += ihF;
                    Fmean += f;
                    count += 1.0;
                }
                //}
            });

            Smean /= count;
            Fmean /= count;

            // Целевая функция           
            int ii, jj;
            double f1 = 0, f2 = 0, f12 = 0;

            for (ii = 0; ii < nx; ii++)
            {
                int v1 = ii - 3, v2 = ii + 3;
                if (v1 < 0) v1 = 0;
                if (v2 >= nx) v2 = nx - 1;
                for (jj = 0; jj < ny; jj++)
                {
                    if ((ii - ir) * (ii - ir) + (jj - ir) * (jj - ir) > ir * ir)
                        continue;

                    double aa = Is[ii, jj] - Smean;
                    double bb = If[ii, jj] - Fmean;
                    f1 += aa * aa;
                    f2 += bb * bb;
                    f12 += aa * bb;
                }
            }

            return f12 / (System.Math.Sqrt(f1) * System.Math.Sqrt(f2));
        }

        #endregion

        #region DEBUG

        /// <summary>
        /// DEBUG function
        /// </summary>
        private void DumpPixels(string fname)
        {
            int i, j;
            System.IO.StreamWriter file = new System.IO.StreamWriter(fname);

            for (i = 0; i < fnx; i++) file.Write("\t{0}", i);
            file.WriteLine();

            for (j = 0; j < fny; j++)
            {
                file.Write("{0}", j);
                for (i = 0; i < fnx; i++) file.Write("\t{0}", (ushort)pixels[((fny - j - 1) * fnx + i) * 4]);
                file.WriteLine();
            }
            file.Close();
        }

        async private void DumpChisqDistributions(string fname)
        {
            int i, j;
            double shiftPixelStep = 0.2;
            double shiftStep = 0.02;
            double angleStep = 0.05;
            double[] ds = new double[8];
            double[,] chq = new double[9, 101];

            // В версии без подгонки центра шарика изменения его положения следует передавать через CurrentBeam
            BeamParams bbackup = CurrentBeam.Clone();

            for (i = 0; i < 8; i++)
            {
                // Вектор шага
                for (j = 0; j < 8; j++) ds[j] = 0;
                if (i < 2) ds[i] = shiftPixelStep;
                else if (i == 7) ds[i] = angleStep;
                else if (i == 6) ds[i] = shiftStep * 0.01; // размер пиксела с учетом расстояния до пленки
                else ds[i] = shiftStep;

                CurrentBeam = bbackup.Clone();
                for (j = 0; j < 50; j++) shiftBeam(CurrentBeam, ds, -1.0);
                for (j = 0; j <= 100; j++)
                {
                    chq[i, j] = await ValueAt(CurrentBeam.FitParameters);
                    shiftBeam(CurrentBeam, ds, 1.0);
                }
            }

            System.IO.StreamWriter file = new System.IO.StreamWriter(fname);

            file.WriteLine("\tB.O.X\tB.O.Y\tB.CC.X\tB.CC.Y\tB.Mlc.X\tB.Mlc.Y\tB.PS\tB.FCA");

            for (i = 0; i < 8; i++)
            {
                for (j = 0; j <= 100; j++)
                {
                    file.Write("{0}", j - 50);
                    for (i = 0; i < 8; i++)
                        file.Write("\t{0}", chq[i, j]);
                    file.WriteLine();
                }
            }

            file.Close();

            CurrentBeam = bbackup.Clone();
        }

        /// <summary>
        /// Добавление вектора смещения к параметрам пучка
        /// </summary>
        /// <param name="bb">изменяемый пучок</param>
        /// <param name="s">вектор смещения</param>
        private void shiftBeam(BeamParams b, double[] s, double d)
        {
            b.O = new Point(b.O.X + d * s[0], b.O.Y + d * s[1]);
            b.CC = new Point(b.CC.X + d * s[2], b.CC.Y + d * s[3]);
            b.Mlc = new Point(b.Mlc.X + d * s[4], b.Mlc.Y + d * s[5]);
            b.PS += d * s[6];
            b.FCA += d * s[7];
        }

        #endregion
    }
}
