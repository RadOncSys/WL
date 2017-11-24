// Author: Gennady Gorlachev (ggorlachev@roiss.ru) 
//---------------------------------------------------------------------------
namespace WL
{
    public class Histogram
    {
        public bool comulative_ = false;
        public int nmax_ = 0;
        private double min_ = 0;
        public double max_ = 0;
        public double step_ = 0;
        public double total_ = 0;
        public double sum_ = 0;
        public double sum2_ = 0;
        public double maxValue_ = 0;
        private double[] d_ = null;

        public Histogram()
        {
        }

        public Histogram(double min, double step, int np)
        {
            Init(min, step, np);
        }

        #region Properties

        /// <summary>
        /// Индикатор того, что гистограмма уже была приведена к комулятивному виду
        /// </summary>
        public bool Comulative
        {
            get { return this.comulative_; }
            set { this.comulative_ = value; }
        }

        /// <summary>
        /// Максимальный индекс точек, где еще присутствуют значения частоты
        /// </summary>
        public int Nmax
        {
            get { return this.nmax_; }
        }

        /// <summary>
        /// Минимальное значение параметра
        /// </summary>
        public double Argmin
        {
            get { return this.min_; }
        }

        /// <summary>
        /// Максимальное значение параметра
        /// </summary>
        public double Argmax
        {
            get { return this.max_; }
        }

        /// <summary>
        /// Шаг гистограммы
        /// </summary>
        public double Step
        {
            get { return this.step_; }
        }

        /// <summary>
        /// 
        /// </summary>
        public double Total
        {
            get { return this.total_; }
        }

        /// <summary>
        /// 
        /// </summary>
        public double Sum
        {
            get { return this.sum_; }
        }

        /// <summary>
        /// 
        /// </summary>
        public double Sum2
        {
            get { return this.sum2_; }
        }

        /// <summary>
        /// 
        /// </summary>
        public double MaxValue
        {
            get { return this.maxValue_; }
        }

        /// <summary>
        /// Массив значений гистограммы
        /// </summary>
        public double[] D
        {
            get { return this.d_; }
        }

        /// <summary>
        /// Среднее значение величины
        /// </summary>
        public double Mean
        {
            get
            {
                return (total_ > 0) ? sum_ / total_ : 0;
            }
        }

        /// <summary>
        /// Среднеквадратичное отклонение величины
        /// </summary>
        public double Stdev
        {
            get
            {
                return (total_ > 0) ? System.Math.Sqrt((sum2_ - sum_ * sum_ / total_) / total_) : 0.0;
            }
        }

        #endregion

        #region Methods

        public Histogram Clone()
        {
            Histogram h = new Histogram(Argmin, Step, D.Length);
            h.comulative_ = comulative_;
            h.nmax_ = nmax_;
            h.max_ = max_;
            h.total_ = total_;
            h.sum_ = sum_;
            h.sum2_ = sum2_;
            h.maxValue_ = maxValue_;
            for (int i = 0; i < D.Length; i++) h.D[i] = D[i];
            return h;
        }

        /// <summary>
        /// Самплинг комулятивной гистограммы выдающий значение аргумента, 
        /// при котором гистограмма переваливает через порог
        /// </summary>
        /// <param name="f">порог значения комулятивной гистограмммы</param>
        /// <returns></returns>
        public double Sample(double f)
        {
            if (!comulative_)
                return 0;
            if (f < 0)
                return min_;
            if (f > 1)
                return max_;
            int i;
            for (i = 1; i < d_.Length - 1; i++) if (d_[i] >= f) break;
            double d1 = d_[i - 1], d2 = d_[i];
            return (d1 == d2) ? GetArg(i - 1) : min_ + (i + (f - d2) / (d2 - d1)) * step_;
        }

        /// <summary>
        /// Перпендикулярное самлингу значение, т.е. определение 
        /// значения гистограммы при заданном значении аргумента
        /// </summary>
        /// <param name="f">аргумент гистограммы</param>
        /// <returns></returns>
        public double ValueAt(double f)
        {
            if (f <= this.min_)
                return 0;
            else if (f >= this.max_)
                return 1;
            else
            {
                double x = (f - min_) / step_;
                int i = (int)x;
                double fx = (x - i) / step_;
                return i == 0 ? (1 - fx) * d_[0] : fx * d_[i - 1] + (1 - fx) * d_[i];
            }
        }

        public double GetArg(int i)
        {
            return min_ + (i + 0.5) * step_;
        }

        public void Init(double min, double step, int np)
        {
            min_ = min;
            step_ = step;
            d_ = new double[np];
            int i;
            for (i = 0; i < d_.Length; i++)
                d_[i] = 0;
        }

        public void Normalize()
        {
            if (comulative_)
                return;
            // Чтобы не было проблем из-за повторной нормировки сумму считаем заново
            int i;
            double sum = 0;
            maxValue_ = 0;
            for (i = 0; i < d_.Length; i++)
            {
                if (d_[i] > maxValue_) maxValue_ = d_[i];
                sum += d_[i];
            }
            if (sum == 0) return;
            for (i = 0; i < d_.Length; i++) d_[i] /= sum;
            maxValue_ /= sum;
        }

        public void MakeComulative()
        {
            if (comulative_)
                return;
            Normalize();
            for (int i = 1; i < d_.Length; i++)
                d_[i] += d_[i - 1];
            comulative_ = true;
        }

        public void AppendPair(HPair pr)
        {
            if (comulative_)
                return;
            if (pr.D < min_ || pr.W <= 0) return;
            int i = (int)((pr.D - min_) / step_);
            if (i >= d_.Length) return;
            d_[i] += pr.W;
            total_ += pr.W;
            sum_ += pr.D * pr.W;
            sum2_ += pr.D * pr.D * pr.W;
            if (max_ < pr.D) { max_ = pr.D; nmax_ = i; }
            if (d_[i] > maxValue_) maxValue_ = d_[i];
        }

        public void AppendHistogram(Histogram h)
        {
            if (comulative_)
                return;
            if (h.D.Length != d_.Length || h.Argmin != Argmin || h.Step != Step)
                return;
            for (int i = 0; i < d_.Length; i++)
            {
                if (h.D[i] <= 0) continue;
                d_[i] += h.D[i];
                total_ += h.D[i];
                if (d_[i] > maxValue_) maxValue_ = d_[i];
            }
            sum_ += h.Sum;
            sum2_ += h.Sum2;
            max_ = (max_ < h.Argmax) ? h.Argmax : max_;
            nmax_ = (nmax_ < h.Nmax) ? h.Nmax : nmax_;
        }

        #endregion
    }
}
