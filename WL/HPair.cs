// Author: Gennady Gorlachev (ggorlachev@roiss.ru) 
//---------------------------------------------------------------------------
namespace WL
{
    public class HPair
    {
        public HPair()
        {
            D = 0.0;
            W = 0.0;
        }

        public HPair(double d, double w)
        {
            D = d;
            W = w;
        }

        public void Set(double d, double w)
        {
            D = d;
            W = w;
        }

        public HPair(double d)
        {
            D = d;
            W = 1.0;
        }

        public double D { get; set; }

        public double W { get; set; }
    }
}

