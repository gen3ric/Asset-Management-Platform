using Asset_Management_Platform.Utility;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Ioc;

namespace Asset_Management_Platform.ViewModel
{
    /// <summary>
    /// This class contains properties that the main View can data bind to.
    /// <para>
    /// Use the <strong>mvvminpc</strong> snippet to add bindable properties to this ViewModel.
    /// </para>
    /// <para>
    /// You can also use Blend to data bind with the tool's support.
    /// </para>
    /// <para>
    /// See http://www.galasoft.ch/mvvm
    /// </para>
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        /// <summary>
        /// Initializes a new instance of the MainViewModel class.
        /// </summary>
        /// 

        SimpleIoc container = new SimpleIoc();

        public MainViewModel()
        {
            container.Register<StockDataService>();
            container.Register<PortfolioService>();
            container.Register<Portfolio>();
            ////if (IsInDesignMode)
            ////{
            ////    // Code runs in Blend --> create design time data.
            ////}
            ////else
            ////{
            ////    // Code runs "for real"
            ////}
        }


        

        
    }
}