// Author: Gennady Gorlachev (ggorlachev@roiss.ru) 
//---------------------------------------------------------------------------
using System.Threading.Tasks;

namespace WL
{
    /// <summary>
    /// Интерфейс класса, представляющего задачу оптимизации
    /// </summary>
    public interface IOptimizationObject
    {
        /// <summary>
        /// Целевая функция
        /// </summary>
        /// <param name="p">массив оптимизируемых параметров</param>
        /// <returns>значение целевой функции</returns>
        Task<double> ValueAt(double[] p);

        /// <summary>
        /// Проверка, не желает ли заказчик прервать оптимизацию
        /// </summary>
        /// <returns></returns>
        bool DoCancel();

        /// <summary>
        /// Информирование заказчика о промежуточном состоянии оптимизации
        /// </summary>
        /// <param name="f">доля выполненной работы от 0 до 1</param>
        /// <param name="p">текущее значение параметров</param>
        void ProgressInfo(double f, double[] p);

        /// <summary>
        /// Установка результата по окончании оптимизации
        /// </summary>
        /// <param name="errCode">код результата</param>
        /// <param name="msg">текстовое сообщение о результате</param>
        void SetStatus(int errCode, string msg);

        /// <summary>
        /// Запрос текущих значений параметров
        /// Используется оптимизатором для определения стартовой точки
        /// </summary>
        /// <returns></returns>
        double[] GetParams();

        /// <summary>
        /// Информация оптимизатору о величинах шагов на старте
        /// </summary>
        /// <returns></returns>
        double[] StartSteps();
    }
}
