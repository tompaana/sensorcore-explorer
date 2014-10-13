using Lumia.Sense;
using Lumia.Sense.Testing;
using SensorCoreExplorer.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// The Pivot Application template is documented at http://go.microsoft.com/fwlink/?LinkID=391641

namespace SensorCoreExplorer
{
    public sealed partial class PivotPage : Page
    {
        private readonly string EmulatorDeviceName = "virtual";
        private readonly string ActivityMonitorRecordingFilePath = "SimulationData\\ActivityMonitor\\4_minutes_walk_stationary_idle.txt";
        private readonly string StepCounterRecordingFilePath = "SimulationData\\StepCounter\\132_minutes_running.txt";
        private readonly string TrackPointMonitorRecordingFilePath = "SimulationData\\TrackPointMonitor\\90_minutes_cycling.txt";
        private const int MaxControlCountPerPivotItem = 50;

        public EventHandler<bool> SensorCoreActivationStateChanged;
        private ResourceLoader _resourceLoader = new ResourceLoader();
        private bool _isEmulated = false;

        #region NavigationHelper 
        private readonly NavigationHelper navigationHelper;

        /// <summary>
        /// Gets the <see cref="NavigationHelper"/> associated with this <see cref="Page"/>.
        /// </summary>
        public NavigationHelper NavigationHelper
        {
            get { return this.navigationHelper; }
        }
        #endregion

        // SensorCore instances
        private IActivityMonitor _activityMonitor;
        private IPlaceMonitor _placeMonitor;
        private IStepCounter _stepCounter;
        private ITrackPointMonitor _trackPointMonitor;


        public PivotPage()
        {
            this.InitializeComponent();

            this.NavigationCacheMode = NavigationCacheMode.Required;
            this.navigationHelper = new NavigationHelper(this);

            Windows.Security.ExchangeActiveSyncProvisioning.EasClientDeviceInformation deviceInformation =
                new Windows.Security.ExchangeActiveSyncProvisioning.EasClientDeviceInformation();
            
            if (deviceInformation.SystemProductName.ToLower().StartsWith(EmulatorDeviceName))
            {
                System.Diagnostics.Debug.WriteLine("App is running in emulator.");
                _isEmulated = true;
            }

            SensorCoreActivationStateChanged += OnSensorCoreActivationStateChanged;

            GetSensorCoreMonitorInstancesAndActivateAsync();

            Window.Current.VisibilityChanged += (sender, eventArgs)
                => ChangeSensorCoreActivationStatusAsync(eventArgs.Visible);

            pivot.SelectionChanged += OnPivotSelectionChanged;

        }

        #region NavigationHelper registration

        /// <summary>
        /// The methods provided in this section are simply used to allow
        /// NavigationHelper to respond to the page's navigation methods.
        /// <para>
        /// Page specific logic should be placed in event handlers for the  
        /// <see cref="NavigationHelper.LoadState"/>
        /// and <see cref="NavigationHelper.SaveState"/>.
        /// The navigation parameter is available in the LoadState method 
        /// in addition to page state preserved during an earlier session.
        /// </para>
        /// </summary>
        /// <param name="e">Provides data for navigation methods and event
        /// handlers that cannot cancel the navigation request.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            this.navigationHelper.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            this.navigationHelper.OnNavigatedFrom(e);
        }

        #endregion

        /// <summary>
        /// Gets the SensorCore instances and activates them. If SensorCore is
        /// not supported on the device, an error dialog is shown.
        /// 
        /// If the app is run in emulator, the simulated instances, initialized
        /// with recorded simulation data, are used instead.
        /// </summary>
        private async void GetSensorCoreMonitorInstancesAndActivateAsync()
        {
            bool sensorCoreSupported =
                !(!await ActivityMonitor.IsSupportedAsync()
                  || !await PlaceMonitor.IsSupportedAsync()
                  || !await StepCounter.IsSupportedAsync()
                  || !await TrackPointMonitor.IsSupportedAsync());

            if (sensorCoreSupported || _isEmulated)
            {
                if (_isEmulated)
                {
                    SenseRecording recording = null;
                    bool success = true;

                    try
                    {
                        recording = await SenseRecording.LoadFromFileAsync(ActivityMonitorRecordingFilePath);
                        _activityMonitor = await ActivityMonitorSimulator.GetDefaultAsync(recording, DateTime.Now.AddDays(-10));

                        recording = await SenseRecording.LoadFromFileAsync(StepCounterRecordingFilePath);
                        _stepCounter = await StepCounterSimulator.GetDefaultAsync(recording, DateTime.Now.AddDays(-10));

                        recording = await SenseRecording.LoadFromFileAsync(TrackPointMonitorRecordingFilePath);
                        _trackPointMonitor = await TrackPointMonitorSimulator.GetDefaultAsync(recording, DateTime.Now.AddDays(-10));
                    }
                    catch (Exception e)
                    {
                        System.Diagnostics.Debug.WriteLine(e.Message);
                        success = false;
                    }

                    if (!success)
                    {
                        // Failed to load the recordings
                        _activityMonitor = await ActivityMonitorSimulator.GetDefaultAsync();
                        _stepCounter = await StepCounterSimulator.GetDefaultAsync();
                        _trackPointMonitor = await TrackPointMonitorSimulator.GetDefaultAsync();
                    }

                    _placeMonitor = await PlaceMonitorSimulator.GetDefaultAsync();
                }
                else
                {
                    _activityMonitor = await ActivityMonitor.GetDefaultAsync();
                    _placeMonitor = await PlaceMonitor.GetDefaultAsync();
                    _stepCounter = await StepCounter.GetDefaultAsync();
                    _trackPointMonitor = await TrackPointMonitor.GetDefaultAsync();
                }

                ChangeSensorCoreActivationStatusAsync(true);
            }
            else
            {
                MessageDialog dialog = new MessageDialog(
                    _resourceLoader.GetString("SensorCoreNotSupported/Text"),
                    _resourceLoader.GetString("Error/Text"));
                dialog.Commands.Add(new UICommand(_resourceLoader.GetString("Ok/Text"), command => Application.Current.Exit()));
                await dialog.ShowAsync();
                
            }
        }

        /// <summary>
        /// Activates/deactivates SensorCore.
        /// </summary>
        /// <param name="activate"></param>
        private async void ChangeSensorCoreActivationStatusAsync(bool activate)
        {
            if (_activityMonitor == null || _placeMonitor == null
                || _stepCounter == null || _trackPointMonitor == null)
            {
                System.Diagnostics.Debug.WriteLine("ChangeSensorCoreActivationStatus(" + activate + "): One or more SensorCore instances null");
                return;
            }

            if (activate)
            {
                progressBar.Visibility = Visibility.Visible;

                await CallSensorcoreApiAsync(async () => await _activityMonitor.ActivateAsync());
                await CallSensorcoreApiAsync(async () => await _placeMonitor.ActivateAsync());
                await CallSensorcoreApiAsync(async () => await _stepCounter.ActivateAsync());
                await CallSensorcoreApiAsync(async () => await _trackPointMonitor.ActivateAsync());
                System.Diagnostics.Debug.WriteLine("SensorCore instances activated.");
            }
            else
            {
                await CallSensorcoreApiAsync(async () => await _activityMonitor.DeactivateAsync());
                await CallSensorcoreApiAsync(async () => await _placeMonitor.DeactivateAsync());
                await CallSensorcoreApiAsync(async () => await _stepCounter.DeactivateAsync());
                await CallSensorcoreApiAsync(async () => await _trackPointMonitor.DeactivateAsync());
                System.Diagnostics.Debug.WriteLine("SensorCore instances deactivated.");
            }

            if (SensorCoreActivationStateChanged != null)
            {
                SensorCoreActivationStateChanged(this, activate);
            }
        }

        /// <summary> 
        /// Performs asynchronous SensorCore SDK operation and handles any exceptions 
        /// </summary> 
        /// <param name="action"></param> 
        /// <returns><c>true</c> if call was successful, <c>false</c> otherwise</returns> 
        private async Task<bool> CallSensorcoreApiAsync(Func<Task> action)
        {
            Exception failure = null;

            try
            {
                await action();
            }
            catch (Exception e)
            {
                failure = e;
            }

            if (failure != null)
            {
                MessageDialog dialog;
                switch (SenseHelper.GetSenseError(failure.HResult))
                {
                    case SenseError.LocationDisabled:
                        dialog = new MessageDialog("Location has been disabled. Do you want to open Location settings now?", "Information");
                        dialog.Commands.Add(new UICommand("Yes", async cmd => await SenseHelper.LaunchLocationSettingsAsync()));
                        dialog.Commands.Add(new UICommand("No"));
                        await dialog.ShowAsync();
                        new System.Threading.ManualResetEvent(false).WaitOne(500);
                        return false;

                    case SenseError.SenseDisabled:
                        dialog = new MessageDialog("Motion data has been disabled. Do you want to open Motion data settings now?", "Information");
                        dialog.Commands.Add(new UICommand("Yes", async cmd => await SenseHelper.LaunchSenseSettingsAsync()));
                        dialog.Commands.Add(new UICommand("No"));
                        await dialog.ShowAsync();
                        new System.Threading.ManualResetEvent(false).WaitOne(500);
                        return false;

                    default:
                        dialog = new MessageDialog("Failure: " + SenseHelper.GetSenseError(failure.HResult), "");
                        await dialog.ShowAsync();
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Retrieves the SensorCore data and populates the UI when the
        /// SensorCore instances are activated. When the instance are
        /// deactivated, the UI items are cleared.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void OnSensorCoreActivationStateChanged(object sender, bool e)
        {
            if (e)
            {
                await PopulateActivityAsync();
                await PopulatePlacesAsync();
                await PopulateStepsAsync();
                await PopulateTrackPointsAsync();

                progressBar.Visibility = Visibility.Collapsed;
            }
            else
            {
                ClearUI();
            }
        }

        private async Task<int> PopulateActivityAsync()
        {
            // Get the activity monitor readings from the last 10 days
            IList<ActivityMonitorReading> activityMonitorReadings =
                await _activityMonitor.GetActivityHistoryAsync(
                    DateTime.Now.AddDays(-10), TimeSpan.FromDays(10));

            /* Other ActivityMonitor methods include GetActivityAtAsync() and
             * GetCurrentReadingAsync().
             */

            System.Diagnostics.Debug.WriteLine("Activity monitor reading count: " + activityMonitorReadings.Count);
            int i = 1;

            foreach (ActivityMonitorReading reading in activityMonitorReadings)
            {
                /*System.Diagnostics.Debug.WriteLine("Activity monitor reading: "
                    + reading.Timestamp + "; " + reading.Mode);*/

                ActivityMonitorReadingControl control = new ActivityMonitorReadingControl();
                control.Title = "Activity, " + reading.Timestamp.ToString();
                control.Mode = reading.Mode.ToString();

                ActivityMonitorReadingControlContainer.Children.Add(control);
                
                if (++i > MaxControlCountPerPivotItem)
                {
                    break;
                }
            }

            return i - 1;
        }

        private async Task<int> PopulatePlacesAsync()
        {
            /* You can get home and work with the separate methods
             * (GetHomeAsync() and GetWorkAsync()), but GetKnownPlacesAsync()
             * will return those too in addition to other places.
             */
            IList<Place> places = await _placeMonitor.GetKnownPlacesAsync();

            System.Diagnostics.Debug.WriteLine("Places count: " + places.Count);
            int i = 1;

            foreach (Place place in places)
            {
                /*System.Diagnostics.Debug.WriteLine("Place: "
                    + place.Id + "; " + place.Kind + "; "
                    + place.Position.Latitude + "; " + place.Position.Longitude + "; "
                    + place.Radius);*/

                PlaceControl control = new PlaceControl();
                control.Title = place.Id.ToString() + ": " + place.Kind.ToString();
                control.Position = place.Position.Latitude.ToString() + "; " + place.Position.Longitude.ToString();
                control.Radius = place.Radius.ToString();

                PlaceControlContainer.Children.Add(control);
                i++;
            }

            return i - 1;
        }

        private async Task<int> PopulateStepsAsync()
        {
            // Get the step readings from the last ten days
            IList<StepCounterReading> steps =
                await _stepCounter.GetStepCountHistoryAsync(
                    DateTime.Now.AddDays(-10), TimeSpan.FromDays(10));

            /* Other StepCounter methods are GetCurrentReadingAsync() and 
             * GetStepCountAtAsync().
             */

            System.Diagnostics.Debug.WriteLine("Step reading count: " + steps.Count);
            StepCounterReading previousReading = null;
            int i = 1;

            foreach (StepCounterReading reading in steps)
            {
                /*System.Diagnostics.Debug.WriteLine("Step reading: "
                    + reading.Timestamp + "; "
                    + reading.RunningStepCount + "; " + reading.RunTime + "; "
                    + reading.WalkingStepCount + "; " + reading.WalkTime);*/

                if (previousReading != null
                    && reading.WalkingStepCount == previousReading.WalkingStepCount
                    && reading.WalkTime == previousReading.WalkTime
                    && reading.RunningStepCount == previousReading.RunningStepCount
                    && reading.RunTime == previousReading.RunTime)
                {
                    /* Do not list item with same data (even if the timestamp
                     * is different) twise.
                     */
                    continue;
                }

                previousReading = reading;
                bool isStep = reading.WalkingStepCount > 0;

                StepReadingControl control = new StepReadingControl();
                control.Title = (isStep ? "Walking step, " : "Running step, ") + reading.Timestamp;

                if (isStep)
                {
                    control.Count = reading.WalkingStepCount.ToString();
                    control.Time = reading.WalkTime.ToString();
                }
                else
                {
                    control.Count = reading.RunningStepCount.ToString();
                    control.Time = reading.RunTime.ToString();
                }

                StepReadingControlContainer.Children.Add(control);

                if (++i > MaxControlCountPerPivotItem)
                {
                    break;
                }
            }

            return i - 1;
        }

        private async Task<int> PopulateTrackPointsAsync()
        {
            IList<TrackPoint> trackPoints =
                await _trackPointMonitor.GetTrackPointsAsync(
                    DateTimeOffset.MinValue, TimeSpan.MaxValue);

            /* You can get a specific track point at a given time with
             * GetPointAtAsync() method.
             */

            System.Diagnostics.Debug.WriteLine("Track points count: " + trackPoints.Count);
            int i = 1;

            foreach (TrackPoint trackPoint in trackPoints)
            {
                /*System.Diagnostics.Debug.WriteLine("Track point: "
                    + trackPoint.Timestamp + "; " + trackPoint.LengthOfStay + "; "
                    + trackPoint.Position.Latitude + "; " + trackPoint.Position.Longitude + "; "
                    + trackPoint.Radius);*/

                TrackPointControl control = new TrackPointControl();
                control.Title = "Track point, " + trackPoint.Timestamp.ToString();
                control.LengthOfStay = trackPoint.LengthOfStay.ToString();
                control.Position = trackPoint.Position.Latitude.ToString() + "; " + trackPoint.Position.Longitude.ToString();
                control.Radius = trackPoint.Radius.ToString();

                TrackPointControlContainer.Children.Add(control);

                if (++i > MaxControlCountPerPivotItem)
                {
                    break;
                }
            }

            return i - 1;
        }

        /// <summary>
        /// Clears the UI of all the SensorCore data items.
        /// </summary>
        private void ClearUI()
        {
            ActivityMonitorReadingControlContainer.Children.Clear();
            PlaceControlContainer.Children.Clear();
            StepReadingControlContainer.Children.Clear();
            TrackPointControlContainer.Children.Clear();
        }

        private static async Task CreateMessageDialog(string message, string title, string label, UICommandInvokedHandler command, bool no)
        {
            var dialog = new MessageDialog(message, title);
            dialog.Commands.Add(new UICommand(label, command));
            if (no) dialog.Commands.Add(new UICommand("No"));

            await dialog.ShowAsync();
        }

        /// <summary>
        /// Clears and repopulates the UI.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnRefreshButtonClicked(object sender, RoutedEventArgs e)
        {
            ClearUI();
            PopulateActivityAsync();
            PopulatePlacesAsync();
            PopulateStepsAsync();
            PopulateTrackPointsAsync();
        }

        #region UI styles and animations

        // Constants
        private readonly Color[] Colors =
        {
            Color.FromArgb(255, 210, 140, 0), // Yellow
            Color.FromArgb(255, 107, 167, 0), // Green
            Color.FromArgb(255, 242, 80, 34), // Orange
            Color.FromArgb(255, 0, 164, 239), // Blue
        };

        private readonly Color[] AccentColors =
        {
            Color.FromArgb(255, 170, 100, 0),
            Color.FromArgb(255, 60, 120, 0),
            Color.FromArgb(255, 200, 40, 10),
            Color.FromArgb(255, 0, 120, 200),
        };

        private const double ColorAnimationDurationInMs = 500;

        private void OnPivotSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Color toColor = Colors[pivot.SelectedIndex];
            //Color toAccentToColor = AccentColors[pivot.SelectedIndex];

            BeginColorAnimation(pivot.Background, "Background.Color",
                ((SolidColorBrush)pivot.Background).Color, toColor);
            /*BeginColorAnimation(commandBar.Background, "Background.Color",
                ((SolidColorBrush)commandBar.Background).Color, toAccentToColor);*/
        }

        private void BeginColorAnimation(DependencyObject target, string targetProperty, Color fromColor, Color toColor)
        {
            ColorAnimation colorAnimation = new ColorAnimation();
            colorAnimation.From = fromColor;
            colorAnimation.To = toColor;
            colorAnimation.Duration = TimeSpan.FromMilliseconds(ColorAnimationDurationInMs);
            
            Storyboard.SetTarget(colorAnimation, target);
            Storyboard.SetTargetProperty(colorAnimation, targetProperty);

            Storyboard storyboard = new Storyboard();
            storyboard.Children.Add(colorAnimation);
            
            storyboard.Begin();
        }

        #endregion
    }
}
