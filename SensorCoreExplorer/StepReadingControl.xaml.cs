using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace SensorCoreExplorer
{
    public sealed partial class StepReadingControl : UserControl
    {
        public string Title { get { return TitleTextBlock.Text; } set { TitleTextBlock.Text = value; } }
        public string Count { get { return StepCountTextBlock.Text; } set { StepCountTextBlock.Text = value; } }
        public string Time { get { return StepTimeTextBlock.Text; } set { StepTimeTextBlock.Text = value; } }

        public StepReadingControl()
        {
            this.InitializeComponent();
        }
    }
}
