// Author: Gennady Gorlachev (ggorlachev@roiss.ru) 
//---------------------------------------------------------------------------
using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace WL
{
    public class ImageFile
    {
        /// <summary>
        /// Класс обслуживания файла пленки с тестом
        /// </summary>
        /// <param name="path"></param>
        public ImageFile(string path)
        {
            _path = path;
            _uri = new Uri(_path);
            _image = BitmapFrame.Create(_uri);
        }

        public override string ToString()
        {
            return Path;
        }

        private String _path;
        public String Path { get { return _path; } }
        private Uri _uri;
        public Uri Uri { get { return _uri; } }

        private BitmapFrame _image;
        public BitmapFrame Image { get { return _image; } }

    }

    public class WLParams : INotifyPropertyChanged
    {
        bool _flipVertical = false;
        bool _flipHorisontal = false;

        /// <summary>
        /// Флаг отражения по горизонтали
        /// </summary>
        public bool FlipVertical
        {
            get { return this._flipVertical; }
            set
            {
                this._flipVertical = value;
                OnPropertyChanged("FlipVertical");
            }
        }

        /// <summary>
        /// Флаг отражения по горизонтали
        /// </summary>
        public bool FlipHorisontal
        {
            get { return this._flipHorisontal; }
            set
            {
                this._flipHorisontal = value;
                OnPropertyChanged("FlipHorisontal");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(String info)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(info));
        }
    }

    /// <summary>
    /// Клас положения изоцентра формирующего устройства в пространстве (IEC).
    /// </summary>
    public class IsocenterParams : INotifyPropertyChanged
    {
        private double _x;
        private double _y;
        private double _z;

        /// <summary>
        /// Положение изоцентра по X
        /// </summary>
        public double X
        {
            get { return this._x; }
            set
            {
                this._x = value;
                OnPropertyChanged("X");
            }
        }

        /// <summary>
        /// Положение изоцентра по Y
        /// </summary>
        public double Y
        {
            get { return this._y; }
            set
            {
                this._y = value;
                OnPropertyChanged("Y");
            }
        }

        /// <summary>
        /// Положение изоцентра по Z
        /// </summary>
        public double Z
        {
            get { return this._z; }
            set
            {
                this._z = value;
                OnPropertyChanged("Z");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(String info)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(info));
        }
    }

    /// <summary>
    /// Все данные, относящиеся к отдельно взятому полю, 
    /// включая координаты изображения на общем снимке, 
    /// параметры подгонки положения изображения MLC и конического коллиматора.
    /// </summary>
    public class BeamParams : INotifyPropertyChanged
    {
        #region Local variables

        private double _ga;
        private double _ca;
        private double _ta;
        private Point _o;
        private double _fa;
        private double _fca;
        private Point _cc;
        private Point _mlc;
        private double _ps;

        #endregion

        #region Constructors

        public BeamParams()
        {
            Ga = 0;
            Ca = 0;
            Ta = 0;
        }

        public BeamParams(double ga, double ca, double ta)
        {
            Ga = ga;
            Ca = ca;
            Ta = ta;
        }

        public BeamParams(BeamParams b)
        {
            this.Set(b);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Угол поворота головки в градусах.
        /// </summary>
        public double Ga
        {
            get { return this._ga; }
            set
            {
                this._ga = value;
                OnPropertyChanged("Ga");
            }
        }

        /// <summary>
        /// Угол поворота коллиматора в градусах.
        /// </summary>
        public double Ca
        {
            get { return this._ca; }
            set
            {
                this._ca = value;
                OnPropertyChanged("Ca");
            }
        }

        /// <summary>
        /// Угол поворота стола в градусах.
        /// </summary>
        public double Ta
        {
            get { return this._ta; }
            set
            {
                this._ta = value;
                OnPropertyChanged("Ta");
            }
        }

        /// <summary>
        /// Положение центра поля (середины шарика) на изображении в пикселах
        /// </summary>
        public Point O
        {
            get { return this._o; }
            set
            {
                this._o = value;
                OnPropertyChanged("O");
            }
        }

        /// <summary>
        /// Угол направления на головку в градусах.
        /// Когда головка сверху изображения угол равен нулю, 
        /// далее с шагом 90 градусов по часовой стрелке.
        /// </summary>
        public double FA
        {
            get { return this._fa; }
            set
            {
                this._fa = value;
                OnPropertyChanged("FA");
            }
        }

        /// <summary>
        /// Небольшой угол коррекции поворота пленки от идеального положения.
        /// Используется для описания направления на головку совместно с FA.
        /// </summary>
        public double FCA
        {
            get { return this._fca; }
            set
            {
                this._fca = value;
                OnPropertyChanged("FCA");
            }
        }

        /// <summary>
        /// Положение центра поля конического коллиматора в см в системе координат коллиматора
        /// </summary>
        public Point CC
        {
            get { return this._cc; }
            set
            {
                this._cc = value;
                OnPropertyChanged("CC");
            }
        }

        /// <summary>
        /// Положение центра поля MLC в см в системе координат коллиматора
        /// </summary>
        public Point Mlc
        {
            get { return this._mlc; }
            set
            {
                this._mlc = value;
                OnPropertyChanged("Mlc");
            }
        }

        /// <summary>
        /// Размер пиксела изображения в см (стартует с 25.4 / DPI * 1.08).
        /// </summary>
        public double PS
        {
            get { return this._ps; }
            set
            {
                this._ps = value;
                OnPropertyChanged("PS");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(String info)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(info));
        }

        /// <summary>
        /// Выжимка properties, связанных с подгонкой
        /// </summary>
        public double[] FitParameters
        {
            get
            {
                //return new double[] { _o.X, _o.Y, _cc.X, _cc.Y, _mlc.X, _mlc.Y, _ps, _fca };
                return new double[] { _cc.X, _cc.Y, _mlc.X, _mlc.Y, _ps, _fca };
            }
            set
            {
                //_o = new Point(value[0], value[1]);
                //_cc = new Point(value[2], value[3]);
                //_mlc = new Point(value[4], value[5]);
                //_ps = value[6];
                //_fca = value[7];

                _cc = new Point(value[0], value[1]);
                _mlc = new Point(value[2], value[3]);
                _ps = value[4];
                _fca = value[5];
            }
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Создание полноценной копии объекта
        /// </summary>
        /// <returns></returns>
        public BeamParams Clone()
        {
            BeamParams b = new BeamParams();
            b.Set(this);
            return b;
        }

        /// <summary>
        /// Создание полноценной копии объекта
        /// </summary>
        /// <returns></returns>
        public void Set(BeamParams b)
        {
            Ga = b.Ga;
            Ca = b.Ca;
            Ta = b.Ta;
            FA = b.FA;
            FCA = b.FCA;
            PS = b.PS;
            if (b.O != null) O = new Point(b.O.X, b.O.Y);
            if (b.CC != null) CC = new Point(b.CC.X, b.CC.Y);
            if (b.Mlc != null) Mlc = new Point(b.Mlc.X, b.Mlc.Y);
        }

        /// <summary>
        /// Преобразование координат из системы MLC в систему пучка
        /// </summary>
        /// <param name="p">точка в системе MLC</param>
        /// <returns>точка в системе пучка</returns>
        public Point PointFromMlcToBeam(Point p)
        {
            double a = (FA + FCA) * System.Math.PI / 180.0;
            double sca = System.Math.Sin(a), cca = System.Math.Cos(a);
            double xm = Mlc.X + p.X, ym = Mlc.Y + p.Y;
            return new Point(xm * cca + ym * sca, -xm * sca + ym * cca);
        }

        /// <summary>
        /// Преобразование координат из системы CC в систему пучка
        /// </summary>
        /// <param name="p">точка в системе CC</param>
        /// <returns>точка в системе пучка</returns>
        public Point PointFromCCToBeam(Point p)
        {
            double a = (FA + FCA) * System.Math.PI / 180.0;
            double sca = System.Math.Sin(a), cca = System.Math.Cos(a);
            double xm = CC.X + p.X, ym = CC.Y + p.Y;
            return new Point(xm * cca + ym * sca, -xm * sca + ym * cca);
        }

        #endregion
    }

    [ValueConversion(typeof(double), typeof(String))]
    public class TaConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.Format("Ta = {0}", (double)value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }

}
